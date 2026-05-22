using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;
using UniversalLIMS.ViewModels.PdfWorkspace;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.RegisterSamples)]
public sealed class PdfWorkspaceController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateOriginalOpenTokenIssuer _openTokenIssuer;
    private readonly IPdfWorkspaceFillService _fillService;
    private readonly ILogger<PdfWorkspaceController> _logger;

    public PdfWorkspaceController(
        ApplicationDbContext context,
        ITemplateOriginalOpenTokenIssuer openTokenIssuer,
        IPdfWorkspaceFillService fillService,
        ILogger<PdfWorkspaceController> logger)
    {
        _context = context;
        _openTokenIssuer = openTokenIssuer;
        _fillService = fillService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var versions = await _context.TemplateVersions
            .AsNoTracking()
            .Include(version => version.Template)
            .Where(version => version.DocumentFormat == TemplateDocumentFormat.Pdf && !version.IsAnnulled)
            .OrderByDescending(version => version.CreatedAtUtc)
            .Take(100)
            .Select(version => new PdfWorkspaceVersionListItemViewModel
            {
                TemplateVersionId = version.Id,
                TemplateId = version.TemplateId,
                TemplateNameUk = version.Template.NameUk,
                VersionNumber = version.VersionNumber,
                Status = version.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return View(new PdfWorkspaceVersionListViewModel { Versions = versions });
    }

    [HttpGet("PdfWorkspace/Fill/{templateVersionId:guid}")]
    public async Task<IActionResult> Fill(
        Guid templateVersionId,
        Guid? orderId,
        CancellationToken cancellationToken)
    {
        var model = await BuildFillViewModelAsync(templateVersionId, orderId, cancellationToken);
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
                values,
                cancellationToken);

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
                })
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

    [HttpGet("PdfWorkspace/Fill/{templateVersionId:guid}/final")]
    public async Task<IActionResult> FinalPdf(
        Guid templateVersionId,
        Guid orderId,
        bool download = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pdfBytes = await _fillService.GenerateFilledPdfAsync(templateVersionId, orderId, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .AsNoTracking()
            .Include(item => item.Template)
            .Include(item => item.Fields.Where(field => !field.IsAnnulled))
                .ThenInclude(field => field.Segments.Where(segment => !segment.IsAnnulled))
            .Include(item => item.Fields.Where(field => !field.IsAnnulled))
                .ThenInclude(field => field.DataField)
            .FirstOrDefaultAsync(item => item.Id == templateVersionId, cancellationToken);

        if (version is null || version.DocumentFormat != TemplateDocumentFormat.Pdf)
        {
            return null;
        }

        var segments = version.Fields
            .OrderBy(field => field.SortOrder)
            .SelectMany(field => field.Segments
                .OrderBy(segment => segment.Sequence)
                .Select(segment => new PdfWorkspaceFillSegmentViewModel
                {
                    SegmentId = segment.Id,
                    TemplateFieldId = field.Id,
                    Tag = field.Tag,
                    Title = field.Title,
                    DataFieldKey = field.DataField?.Key ?? field.Tag,
                    Sequence = segment.Sequence,
                    PageNumber = segment.PageNumber,
                    PositionX = segment.PositionX,
                    PositionY = segment.PositionY,
                    Width = segment.Width,
                    Height = segment.Height,
                    AllowMultiline = field.AllowMultiline,
                    TextOffsetX = field.TextOffsetX,
                    TextOffsetY = field.TextOffsetY,
                    FontSize = segment.FontSize,
                    FontName = segment.FontName,
                    HorizontalAlignment = segment.HorizontalAlignment ?? segment.TextAlignment.ToString(),
                    VerticalAlignment = segment.VerticalAlignment
                }))
            .ToList();

        Dictionary<string, string?> savedValues = new(StringComparer.Ordinal);
        if (orderId.HasValue)
        {
            savedValues = new Dictionary<string, string?>(
                await _fillService.GetSavedValuesByKeyAsync(orderId.Value, templateVersionId, cancellationToken),
                StringComparer.Ordinal);
        }

        return new PdfWorkspaceFillViewModel
        {
            TemplateVersionId = version.Id,
            TemplateId = version.TemplateId,
            TemplateNameUk = version.Template.NameUk,
            VersionNumber = version.VersionNumber,
            OrderId = orderId,
            PdfPreviewUrl = BuildOpenOriginalLink(version.Id),
            SavedValuesByKey = savedValues,
            Segments = segments
        };
    }

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
