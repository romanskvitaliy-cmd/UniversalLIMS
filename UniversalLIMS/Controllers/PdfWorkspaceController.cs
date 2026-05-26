using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversalLIMS.Application.Home;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;
using UniversalLIMS.ViewModels.PdfWorkspace;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.FillPdfWorkspace)]
[RequireActiveLimsRole]
public sealed class PdfWorkspaceController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateOriginalOpenTokenIssuer _openTokenIssuer;
    private readonly IPdfWorkspaceFillService _fillService;
    private readonly IFieldTextLibraryService _textLibrary;
    private readonly ITemplateFieldMappingService _fieldMapping;
    private readonly IActiveLimsRoleService _activeRole;
    private readonly ILogger<PdfWorkspaceController> _logger;

    public PdfWorkspaceController(
        ApplicationDbContext context,
        ITemplateOriginalOpenTokenIssuer openTokenIssuer,
        IPdfWorkspaceFillService fillService,
        IFieldTextLibraryService textLibrary,
        ITemplateFieldMappingService fieldMapping,
        IActiveLimsRoleService activeRole,
        ILogger<PdfWorkspaceController> logger)
    {
        _context = context;
        _openTokenIssuer = openTokenIssuer;
        _fillService = fillService;
        _textLibrary = textLibrary;
        _fieldMapping = fieldMapping;
        _activeRole = activeRole;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var activeRole = _activeRole.ResolveActiveRole(User);
        var isRegistrarMode = activeRole == LimsRoles.Registrar;

        var versionsQuery = _context.TemplateVersions
            .AsNoTracking()
            .Include(version => version.Template)
            .Where(version => version.DocumentFormat == TemplateDocumentFormat.Pdf && !version.IsAnnulled);

        if (isRegistrarMode)
        {
            versionsQuery = versionsQuery.Where(version => version.Status == TemplateVersionStatus.Published);
        }

        var versions = await versionsQuery
            .OrderByDescending(version => version.CreatedAtUtc)
            .Take(100)
            .Select(version => new PdfWorkspaceVersionListItemViewModel
            {
                TemplateVersionId = version.Id,
                TemplateId = version.TemplateId,
                TemplateNameUk = version.Template.NameUk,
                VersionNumber = version.VersionNumber,
                Status = version.Status
            })
            .ToListAsync(cancellationToken);

        return View(new PdfWorkspaceVersionListViewModel
        {
            Versions = versions,
            IsRegistrarMode = isRegistrarMode
        });
    }

    [HttpGet("PdfWorkspace/Fill/{templateVersionId:guid}")]
    public async Task<IActionResult> Fill(
        Guid templateVersionId,
        Guid? orderId,
        Guid? orderDocumentId,
        CancellationToken cancellationToken)
    {
        var model = await BuildFillViewModelAsync(templateVersionId, orderId, orderDocumentId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(model.PdfPreviewUrl))
        {
            TempData["TemplateWarning"] = "Не вдалося побудувати URL для preview PDF.";
        }

        return View(model);
    }

    [HttpPost("PdfWorkspace/Fill/{templateVersionId:guid}/values")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveValues(
        Guid templateVersionId,
        [FromBody] PdfWorkspaceSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Тіло запиту порожнє." });
        }

        try
        {
            var rawValues = request.Values ?? [];
            _logger.LogInformation(
                "PdfWorkspace SaveValues: version={TemplateVersionId}, order={OrderId}, items={Count}",
                templateVersionId,
                request.OrderId,
                rawValues.Count);

            var values = rawValues
                .Select(item => new PdfWorkspaceFieldValueDto
                {
                    TemplateFieldId = Guid.TryParse(item.TemplateFieldId, out var fieldId) ? fieldId : null,
                    Value = item.Value
                })
                .ToList();

            foreach (var item in values)
            {
                var preview = item.Value?.Length > 0
                    ? item.Value.Length <= 80
                        ? item.Value
                        : $"{item.Value[..80]}…"
                    : "(empty)";
                _logger.LogInformation(
                    "PdfWorkspace SaveValues item: templateFieldId={TemplateFieldId}, length={Length}, preview={Preview}",
                    item.TemplateFieldId,
                    item.Value?.Length ?? 0,
                    preview);
            }

            var result = await _fillService.SaveValuesAsync(
                templateVersionId,
                request.OrderId,
                request.OrderDocumentId,
                values,
                cancellationToken);

            var libraryResults = new List<object>();
            if (request.LibraryAdditions is { Count: > 0 }
                && result.FailedFields.Count == 0)
            {
                foreach (var addition in request.LibraryAdditions)
                {
                    if (!Guid.TryParse(addition.TemplateFieldId, out var templateFieldId)
                        || string.IsNullOrWhiteSpace(addition.Body))
                    {
                        continue;
                    }

                    try
                    {
                        var libraryResult = await _textLibrary.UpsertAsync(
                            templateVersionId,
                            new FieldTextLibraryUpsertRequest
                            {
                                OrderId = result.OrderId,
                                TemplateFieldId = templateFieldId,
                                Body = addition.Body,
                                ShortLabel = addition.ShortLabel,
                                ScopeToTemplateVersion = addition.ScopeToTemplateVersion
                            },
                            cancellationToken);

                        libraryResults.Add(new
                        {
                            templateFieldId,
                            created = libraryResult.Created,
                            message = libraryResult.Message,
                            entryId = libraryResult.Entry.Id
                        });
                    }
                    catch (InvalidOperationException libraryException)
                    {
                        libraryResults.Add(new
                        {
                            templateFieldId,
                            error = libraryException.Message
                        });
                    }
                }
            }

            _logger.LogInformation(
                "PdfWorkspace SaveValues result: order={OrderId}, received={Received}, saved={Saved}, unmapped={Unmapped}, failed={Failed}",
                result.OrderId,
                result.Received,
                result.Saved,
                result.SkippedUnmapped,
                result.FailedFields.Count);

            return Json(new
            {
                orderId = result.OrderId,
                orderDocumentId = request.OrderDocumentId,
                received = result.Received,
                mapped = result.Mapped,
                saved = result.Saved,
                skippedUnmapped = result.SkippedUnmapped,
                skippedEmpty = result.SkippedEmpty,
                message = result.Message,
                failedFields = result.FailedFields.Select(failure => new
                {
                    templateFieldId = failure.TemplateFieldId,
                    reason = failure.Reason
                }),
                libraryResults
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(500, new
            {
                message = "Помилка збереження на сервері.",
                detail = exception.Message,
                inner = exception.InnerException?.Message
            });
        }
    }

    [HttpGet("PdfWorkspace/Fill/{templateVersionId:guid}/library")]
    public async Task<IActionResult> ListLibrary(
        Guid templateVersionId,
        Guid templateFieldId,
        Guid? orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _textLibrary.ListForFieldAsync(
                templateVersionId,
                templateFieldId,
                orderId,
                cancellationToken);

            return Json(new
            {
                entries = result.Entries.Select(entry => new
                {
                    id = entry.Id,
                    body = entry.Body,
                    shortLabel = entry.ShortLabel,
                    usageCount = entry.UsageCount,
                    rowVersionBase64 = entry.RowVersionBase64
                }),
                totalCount = result.TotalCount
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("PdfWorkspace/Fill/{templateVersionId:guid}/library")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpsertLibrary(
        Guid templateVersionId,
        [FromBody] FieldTextLibraryUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Тіло запиту порожнє." });
        }

        try
        {
            var result = await _textLibrary.UpsertAsync(templateVersionId, request, cancellationToken);
            return Json(new
            {
                entry = MapLibraryEntry(result.Entry),
                created = result.Created,
                message = result.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("PdfWorkspace/Fill/{templateVersionId:guid}/library/{entryId:guid}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateLibrary(
        Guid templateVersionId,
        Guid entryId,
        [FromBody] FieldTextLibraryUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Тіло запиту порожнє." });
        }

        try
        {
            var result = await _textLibrary.UpdateAsync(templateVersionId, entryId, request, cancellationToken);
            return Json(new
            {
                entry = MapLibraryEntry(result.Entry),
                message = result.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete("PdfWorkspace/Fill/{templateVersionId:guid}/library/{entryId:guid}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteLibrary(
        Guid templateVersionId,
        Guid entryId,
        Guid templateFieldId,
        Guid? orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _textLibrary.AnnulAsync(
                templateVersionId,
                entryId,
                templateFieldId,
                orderId,
                cancellationToken);

            return Json(new { message = "Запис прибрано з бібліотеки." });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("PdfWorkspace/Fill/{templateVersionId:guid}/library/{entryId:guid}/use")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RecordLibraryUsage(
        Guid templateVersionId,
        Guid entryId,
        [FromBody] FieldTextLibraryUsageRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Тіло запиту порожнє." });
        }

        try
        {
            await _textLibrary.RecordUsageAsync(
                templateVersionId,
                entryId,
                request.TemplateFieldId,
                request.OrderId,
                cancellationToken);

            return Json(new { ok = true });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("PdfWorkspace/Fill/{templateVersionId:guid}/layout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveLayout(
        Guid templateVersionId,
        [FromBody] PdfWorkspaceFillLayoutSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Fields is null || request.Fields.Count == 0)
        {
            return BadRequest(new { message = "Немає полів макету для збереження." });
        }

        try
        {
            var result = await _fieldMapping.SaveFillLayoutRefinementAsync(
                templateVersionId,
                request.Fields,
                cancellationToken);

            return Json(new { saved = result.Saved, message = result.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("PdfWorkspace/Fill/{templateVersionId:guid}/final")]
    public async Task<IActionResult> FinalPdf(
        Guid templateVersionId,
        Guid orderId,
        Guid? orderDocumentId,
        bool download = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pdfBytes = await _fillService.GenerateFilledPdfAsync(
                templateVersionId,
                orderId,
                orderDocumentId,
                cancellationToken);
            var version = await _context.TemplateVersions
                .AsNoTracking()
                .Include(item => item.Template)
                .FirstAsync(item => item.Id == templateVersionId, cancellationToken);

            var fileName = $"{SanitizeFileName(version.Template.NameUk)}-v{version.VersionNumber}-filled.pdf";

            if (download)
            {
                return File(pdfBytes, "application/pdf", fileName);
            }

            return File(pdfBytes, "application/pdf");
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    private async Task<PdfWorkspaceFillViewModel?> BuildFillViewModelAsync(
        Guid templateVersionId,
        Guid? orderId,
        Guid? orderDocumentId,
        CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .AsNoTracking()
            .Include(item => item.Template)
            .FirstOrDefaultAsync(item => item.Id == templateVersionId, cancellationToken);

        if (version is null || version.DocumentFormat != TemplateDocumentFormat.Pdf)
        {
            return null;
        }

        var layoutSegmentCount = await _fillService.GetLayoutSegmentCountAsync(templateVersionId, cancellationToken);
        var segmentDtos = await _fillService.GetFillSegmentsAsync(templateVersionId, cancellationToken);
        var segments = segmentDtos
            .Select(segment => new PdfWorkspaceFillSegmentViewModel
            {
                SegmentId = segment.SegmentId,
                TemplateFieldId = segment.TemplateFieldId,
                Tag = segment.Tag,
                Title = segment.Title,
                DataFieldId = segment.DataFieldId,
                DataFieldKey = segment.DataFieldKey,
                Sequence = segment.Sequence,
                PageNumber = segment.PageNumber,
                PositionX = segment.PositionX,
                PositionY = segment.PositionY,
                Width = segment.Width,
                Height = segment.Height,
                AllowMultiline = segment.AllowMultiline,
                TextOffsetX = segment.TextOffsetX,
                TextOffsetY = segment.TextOffsetY,
                FontSize = segment.FontSize,
                FontName = segment.FontName,
                HorizontalAlignment = segment.HorizontalAlignment,
                VerticalAlignment = segment.VerticalAlignment,
                TextAlignment = segment.TextAlignment,
                LineHeight = segment.LineHeight,
                SvgPathData = segment.SvgPathData,
                IsPrimary = segment.IsPrimary,
                SegmentRowVersionBase64 = segment.SegmentRowVersion is { Length: > 0 }
                    ? Convert.ToBase64String(segment.SegmentRowVersion)
                    : null,
                AccessLevel = segment.AccessLevel
            })
            .ToList();

        Dictionary<string, string?> savedValues = new(StringComparer.Ordinal);
        if (orderId.HasValue)
        {
            savedValues = new Dictionary<string, string?>(
                await _fillService.GetSavedValuesByKeyAsync(
                    orderId.Value,
                    templateVersionId,
                    orderDocumentId,
                    cancellationToken),
                StringComparer.Ordinal);
        }

        var activeRole = _activeRole.ResolveActiveRole(User);
        var roleDisplay = RolePortalCatalog.FindByRoleCode(activeRole)?.DisplayName ?? activeRole;

        return new PdfWorkspaceFillViewModel
        {
            TemplateVersionId = version.Id,
            TemplateId = version.TemplateId,
            TemplateNameUk = version.Template.NameUk,
            VersionNumber = version.VersionNumber,
            OrderId = orderId,
            OrderDocumentId = orderDocumentId,
            PdfPreviewUrl = BuildOpenOriginalLink(version.Id),
            SavedValuesByKey = savedValues,
            Segments = segments,
            ActiveRoleCode = activeRole,
            ActiveRoleDisplayName = roleDisplay,
            LayoutSegmentCount = layoutSegmentCount,
            WritableFieldCount = segments.Count(segment => segment.CanWrite),
            ReadOnlyFieldCount = segments.Count(segment => !segment.CanWrite),
            FieldPermissionsUrl = Url.Action(
                "Permissions",
                "TemplateFields",
                new { templateVersionId = version.Id })
        };
    }

    private static object MapLibraryEntry(FieldTextLibraryEntryDto entry) =>
        new
        {
            id = entry.Id,
            body = entry.Body,
            shortLabel = entry.ShortLabel,
            usageCount = entry.UsageCount,
            rowVersionBase64 = entry.RowVersionBase64,
            scopeToTemplateVersion = entry.ScopeToTemplateVersion
        };

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "document" : cleaned;
    }

    private string? BuildOpenOriginalLink(Guid versionId)
    {
        var token = _openTokenIssuer.CreateToken(versionId);
        return Url.Action(
            nameof(TemplateVersionsController.OpenOriginal),
            "TemplateVersions",
            new { id = versionId, token });
    }
}
