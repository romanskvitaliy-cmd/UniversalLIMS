using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;
using UniversalLIMS.ViewModels.Templates;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.ManageSystem)]
public sealed class TemplateFieldsController : Controller
{
    private readonly ITemplateFieldMappingService _fieldMappingService;
    private readonly IPdfWorkspaceFillService _pdfWorkspaceFillService;
    private readonly ApplicationDbContext _context;
    private readonly ITemplateOriginalOpenTokenIssuer _openTokenIssuer;

    public TemplateFieldsController(
        ApplicationDbContext context,
        ITemplateFieldMappingService fieldMappingService,
        IPdfWorkspaceFillService pdfWorkspaceFillService,
        ITemplateOriginalOpenTokenIssuer openTokenIssuer)
    {
        _context = context;
        _fieldMappingService = fieldMappingService;
        _pdfWorkspaceFillService = pdfWorkspaceFillService;
        _openTokenIssuer = openTokenIssuer;
    }

    public async Task<IActionResult> Map(Guid templateVersionId, CancellationToken cancellationToken)
    {
        var model = await BuildMappingViewModelAsync(templateVersionId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Map(TemplateFieldMappingViewModel model, CancellationToken cancellationToken)
    {
        var wantsJson = WantsMapJsonResponse();
        var hasDeletions = model.DeletedFieldIds.Count > 0;
        if (!ModelState.IsValid)
        {
            if (wantsJson)
            {
                return BadRequest(ModelState);
            }

            TempData["TemplateWarning"] = "Не вдалося зберегти поля: перевірте коректність форми і повторіть спробу.";
            return RedirectToAction(nameof(Map), new { templateVersionId = model.TemplateVersionId });
        }

        if (model.Fields.Count == 0 && !hasDeletions)
        {
            const string emptyFieldsMessage = "Не вдалося зберегти поля: перевірте коректність форми і повторіть спробу.";
            if (wantsJson)
            {
                return BadRequest(new { message = emptyFieldsMessage });
            }

            TempData["TemplateWarning"] = emptyFieldsMessage;
            return RedirectToAction(nameof(Map), new { templateVersionId = model.TemplateVersionId });
        }

        var mappings = model.Fields.ToDictionary(field => field.FieldId, field => field.DataFieldId);
        var capacities = model.Fields.ToDictionary(
            field => field.FieldId,
            field => new TemplateFieldCapacityUpdate(
                field.EstimatedCapacityChars,
                field.MaxLines,
                field.AllowMultiline,
                field.OverflowPolicy));
        var layouts = model.Fields.ToDictionary(
            field => field.FieldId,
            field => new TemplateFieldLayoutUpdate(
                field.PageNumber,
                field.PositionX,
                field.PositionY,
                field.Width,
                field.Height));
        var documentFormat = await _context.TemplateVersions
            .AsNoTracking()
            .Where(version => version.Id == model.TemplateVersionId)
            .Select(version => version.DocumentFormat)
            .FirstOrDefaultAsync(cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (hasDeletions)
            {
                await _fieldMappingService.DeleteFieldsAsync(model.TemplateVersionId, model.DeletedFieldIds, cancellationToken);
            }
            await _fieldMappingService.UpdateMappingsAsync(model.TemplateVersionId, mappings, cancellationToken);
            await _fieldMappingService.UpdateCapacityAsync(model.TemplateVersionId, capacities, cancellationToken);
            if (documentFormat != TemplateDocumentFormat.Pdf)
            {
                await _fieldMappingService.UpdateLayoutAsync(model.TemplateVersionId, layouts, cancellationToken);
            }
            else
            {
                var textOffsets = model.Fields.ToDictionary(
                    field => field.FieldId,
                    field => new TemplateFieldTextOffsetUpdate(field.TextOffsetX, field.TextOffsetY));
                await _fieldMappingService.UpdateTextOffsetsAsync(model.TemplateVersionId, textOffsets, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            const string warning = "Версію шаблону було змінено паралельно. Оновіть сторінку і повторіть збереження.";
            if (wantsJson)
            {
                return BadRequest(new { message = warning });
            }

            TempData["TemplateWarning"] = warning;
            return RedirectToAction(nameof(Map), new { templateVersionId = model.TemplateVersionId });
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (wantsJson)
            {
                return BadRequest(new { message = exception.Message });
            }

            TempData["TemplateWarning"] = exception.Message;
            return RedirectToAction(nameof(Map), new { templateVersionId = model.TemplateVersionId });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (wantsJson)
        {
            return Ok(new { success = true, message = "Поля шаблону успішно збережено." });
        }

        TempData["TemplateSuccess"] = "Поля шаблону успішно збережено.";
        return RedirectToAction(nameof(Map), new { templateVersionId = model.TemplateVersionId });
    }

    [HttpPost("/TemplateFields/AddPdfField")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPdfField(CreatePdfTemplateFieldViewModel model, CancellationToken cancellationToken)
    {
        var wantsJson = WantsMapJsonResponse();
        if (!ModelState.IsValid)
        {
            const string validationMessage = "Не вдалося додати PDF-тег. Перевірте Tag та Title.";
            if (wantsJson)
            {
                return BadRequest(new { message = validationMessage });
            }

            TempData["TemplateWarning"] = validationMessage;
            return RedirectToAction(nameof(Map), new { templateVersionId = model.TemplateVersionId });
        }

        try
        {
            var createdFieldId = await _fieldMappingService.CreatePdfFieldAsync(
                model.TemplateVersionId,
                model.Tag,
                model.Title,
                cancellationToken);
            var successMessage = $"Поле '{model.Tag}' успішно додано.";

            if (wantsJson)
            {
                return Ok(new
                {
                    success = true,
                    message = successMessage,
                    fieldId = createdFieldId,
                    tag = model.Tag,
                    title = model.Title
                });
            }

            TempData["TemplateSuccess"] = successMessage;

            // Новий тег стає активним і відразу відкривається режим редагування.
            return RedirectToAction(nameof(Map), new
            {
                templateVersionId = model.TemplateVersionId,
                refresh = "true",
                fieldId = createdFieldId,
                edit = "true"
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            const string concurrencyMessage = "Версію шаблону було змінено паралельно. Оновіть сторінку та спробуйте ще раз.";
            if (wantsJson)
            {
                return BadRequest(new { message = concurrencyMessage });
            }

            TempData["TemplateWarning"] = concurrencyMessage;
        }
        catch (InvalidOperationException ex)
        {
            if (wantsJson)
            {
                return BadRequest(new { message = ex.Message });
            }

            TempData["TemplateWarning"] = ex.Message;
        }

        if (wantsJson)
        {
            return BadRequest(new { message = "Не вдалося додати PDF-тег." });
        }

        // Важливо: форсований рефреш
        return RedirectToAction(nameof(Map), new
        {
            templateVersionId = model.TemplateVersionId,
            refresh = "true"
        });
    }

    [HttpGet("/TemplateFields/AddPdfField")]
    public IActionResult AddPdfField(Guid? templateVersionId = null)
    {
        TempData["TemplateWarning"] = "Додавання PDF-тегу виконується тільки POST-формою зі сторінки мапінгу полів.";
        if (templateVersionId.HasValue)
        {
            return RedirectToAction(nameof(Map), new { templateVersionId = templateVersionId.Value });
        }

        return RedirectToAction("Index", "Templates");
    }

    [HttpGet("/TemplateFields/CreateDataField")]
    public async Task<IActionResult> CreateDataField(Guid templateFieldId, CancellationToken cancellationToken)
    {
        var field = await _context.TemplateFields
            .Include(item => item.TemplateVersion)
            .FirstOrDefaultAsync(item => item.Id == templateFieldId, cancellationToken);

        if (field is null)
        {
            TempData["TemplateWarning"] = "Поле шаблону не знайдено. Спробуйте оновити сторінку мапінгу полів.";
            return RedirectToAction("Index", "Templates");
        }

        return View(new CreateDataFieldFromTemplateFieldViewModel
        {
            TemplateFieldId = field.Id,
            TemplateVersionId = field.TemplateVersionId,
            Tag = field.Tag,
            DisplayNameUk = field.Title ?? field.Tag,
            MaxLength = field.EstimatedCapacityChars
        });
    }

    [HttpPost("/TemplateFields/CreateDataField")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDataField(
        CreateDataFieldFromTemplateFieldViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _fieldMappingService.CreateDataFieldFromTemplateFieldAsync(
                model.TemplateFieldId,
                model.FieldType,
                model.Scope,
                model.DisplayNameUk,
                model.DescriptionUk,
                model.MaxLength,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Поле шаблону було змінено паралельно. Оновіть сторінку та спробуйте ще раз.");
            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        return RedirectToAction(nameof(Map), new { templateVersionId = model.TemplateVersionId });
    }

    public async Task<IActionResult> Permissions(Guid templateVersionId, CancellationToken cancellationToken)
    {
        var model = await BuildPermissionsViewModelAsync(templateVersionId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(
        TemplateFieldPermissionMatrixViewModel model,
        CancellationToken cancellationToken)
    {
        var accessLevels = model.Fields.ToDictionary(
            field => field.FieldId,
            field => (IReadOnlyDictionary<string, FieldAccessLevel>)field.RolePermissions.ToDictionary(
                rolePermission => rolePermission.RoleName,
                rolePermission => rolePermission.AccessLevel));

        await _fieldMappingService.UpdatePermissionsAsync(model.TemplateVersionId, accessLevels, cancellationToken);

        return RedirectToAction(nameof(Permissions), new { templateVersionId = model.TemplateVersionId });
    }

    [HttpGet("/api/template-fields/{templateVersionId:guid}")]
    public async Task<IActionResult> GetPdfFields(Guid templateVersionId, CancellationToken cancellationToken)
    {
        var versionExists = await _context.TemplateVersions
            .AsNoTracking()
            .AnyAsync(item => item.Id == templateVersionId, cancellationToken);
        if (!versionExists)
        {
            return NotFound();
        }

        var fields = await _context.TemplateFields
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(field => field.TemplateVersionId == templateVersionId && !field.IsAnnulled)
            .OrderBy(field => field.SortOrder)
            .Select(field => new
            {
                id = field.Id,
                dataFieldId = field.DataFieldId,
                tag = field.Tag,
                title = field.Title,
                fieldType = field.FieldType.ToString(),
                overflowPolicy = field.OverflowPolicy.ToString(),
                sortOrder = field.SortOrder,
                textOffsetX = field.TextOffsetX,
                textOffsetY = field.TextOffsetY,
                segments = field.Segments
                    .Where(segment => !segment.IsAnnulled)
                    .OrderBy(segment => segment.Sequence)
                    .Select(segment => new
                    {
                        id = segment.Id,
                        sequence = segment.Sequence,
                        page = segment.PageNumber,
                        x = segment.PositionX,
                        y = segment.PositionY,
                        width = segment.Width,
                        height = segment.Height,
                        isPrimary = segment.IsPrimary,
                        textAlignment = segment.TextAlignment.ToString(),
                        fontSize = segment.FontSize,
                        horizontalAlignment = segment.HorizontalAlignment ?? segment.TextAlignment.ToString(),
                        verticalAlignment = segment.VerticalAlignment,
                        rowVersion = segment.RowVersion
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Json(fields);
    }

    [HttpPut("/api/template-fields/{templateVersionId:guid}/text-offsets")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTextOffsets(
        Guid templateVersionId,
        [FromBody] SaveTemplateFieldTextOffsetsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Fields.Count == 0)
        {
            return BadRequest(new { message = "Порожній список offset." });
        }

        try
        {
            await _fieldMappingService.EnsureEditableTemplateVersionAsync(templateVersionId, cancellationToken);
            var offsets = request.Fields.ToDictionary(
                field => field.FieldId,
                field => new TemplateFieldTextOffsetUpdate(field.TextOffsetX, field.TextOffsetY));
            await _fieldMappingService.UpdateTextOffsetsAsync(templateVersionId, offsets, cancellationToken);
            return Ok(new { success = true, message = "Text offset збережено." });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("/api/template-fields/{templateVersionId:guid}/calibration-preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CalibrationPreviewPdf(
        Guid templateVersionId,
        [FromBody] CalibrationPreviewPdfRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _fieldMappingService.EnsureEditableTemplateVersionAsync(templateVersionId, cancellationToken);
            var samples = (request.Samples ?? [])
                .Where(sample => sample.FieldId != Guid.Empty)
                .ToDictionary(sample => sample.FieldId, sample => sample.Text ?? string.Empty);

            var pdfBytes = await _pdfWorkspaceFillService.GenerateCalibrationPreviewPdfAsync(
                templateVersionId,
                samples,
                cancellationToken);

            return File(pdfBytes, "application/pdf", "calibration-preview.pdf");
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("/api/template-fields/{templateVersionId:guid}/segments")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePdfFieldSegments(
        Guid templateVersionId,
        [FromBody] SaveTemplateFieldSegmentsRequest request,
        CancellationToken cancellationToken)
    {
        var hasFieldUpdates = request.Fields.Count > 0;
        var hasDeletions = request.DeletedFieldIds.Count > 0;
        if (!hasFieldUpdates && !hasDeletions)
        {
            return BadRequest(new { message = "Segment payload is empty." });
        }

        var clientReferenceBySegment = new Dictionary<TemplateFieldSegment, string>();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _fieldMappingService.EnsureEditableTemplateVersionAsync(templateVersionId, cancellationToken);

            if (hasDeletions)
            {
                await _fieldMappingService.DeleteFieldsAsync(
                    templateVersionId,
                    request.DeletedFieldIds,
                    cancellationToken);
            }

            foreach (var field in request.Fields.OrderBy(field => field.FieldId))
            {
                var segmentUpdates = field.Segments
                    .OrderBy(segment => segment.Sequence)
                    .Select(segment => new TemplateFieldSegmentLayoutUpdate(
                        segment.Id,
                        field.FieldId,
                        segment.ClientReferenceId,
                        segment.Sequence,
                        segment.Page,
                        segment.X,
                        segment.Y,
                        segment.Width,
                        segment.Height,
                        segment.IsPrimary,
                        segment.TextAlignment,
                        segment.FontSize,
                        segment.HorizontalAlignment,
                        segment.VerticalAlignment,
                        ResolveSegmentRowVersion(segment)))
                    .ToList();

                await _fieldMappingService.ProcessFieldSegmentsAsync(
                    templateVersionId,
                    field.FieldId,
                    segmentUpdates,
                    clientReferenceBySegment,
                    cancellationToken);

                await _fieldMappingService.UpdateTextOffsetsAsync(
                    templateVersionId,
                    new Dictionary<Guid, TemplateFieldTextOffsetUpdate>
                    {
                        [field.FieldId] = new(
                            field.TextOffsetX ?? 0m,
                            field.TextOffsetY ?? 0m)
                    },
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var saveResult = await _fieldMappingService.BuildSegmentLayoutSaveResultAsync(
                templateVersionId,
                clientReferenceBySegment,
                cancellationToken);

            return Json(new
            {
                segments = saveResult.Segments.Select(segment => new
                {
                    clientReferenceId = segment.ClientReferenceId,
                    id = segment.Id,
                    fieldId = segment.FieldId,
                    sequence = segment.Sequence,
                    page = segment.PageNumber,
                    x = segment.PositionX,
                    y = segment.PositionY,
                    width = segment.Width,
                    height = segment.Height,
                    isPrimary = segment.IsPrimary,
                    textAlignment = segment.TextAlignment,
                    fontSize = segment.FontSize,
                    horizontalAlignment = segment.HorizontalAlignment,
                    verticalAlignment = segment.VerticalAlignment,
                    rowVersion = Convert.ToBase64String(segment.RowVersion)
                })
            });
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { message = exception.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Сегменти змінилися в іншій сесії. Перезавантажте сторінку і повторіть збереження." });
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { message = ResolveSegmentSaveExceptionMessage(exception) });
        }
    }

    private static string ResolveSegmentSaveExceptionMessage(DbUpdateException exception)
    {
        var details = exception.InnerException?.Message ?? exception.Message;
        if (details.Contains("IX_TemplateFieldSegments_TemplateFieldId_Sequence", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return "Не вдалося зберегти сегменти: конфлікт порядку або дублікат sequence. Перезавантажте сторінку і повторіть збереження.";
        }

        return $"Не вдалося зберегти сегменти: {details}";
    }

    private static byte[]? ResolveSegmentRowVersion(TemplateFieldSegmentDto segment)
    {
        if (segment.RowVersion is { Length: > 0 })
        {
            return segment.RowVersion;
        }

        if (string.IsNullOrWhiteSpace(segment.RowVersionBase64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(segment.RowVersionBase64.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task<TemplateFieldMappingViewModel?> BuildMappingViewModelAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .Include(item => item.Template)
            .Include(item => item.Fields)
                .ThenInclude(field => field.Segments)
            .FirstOrDefaultAsync(item => item.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            return null;
        }

        var dataFields = await _context.DataFields
            .Where(dataField => dataField.IsActive)
            .OrderBy(dataField => dataField.Key)
            .Select(dataField => new DataFieldOptionViewModel
            {
                Id = dataField.Id,
                Key = dataField.Key,
                DisplayNameUk = dataField.DisplayNameUk,
                GroupName = dataField.Scope.ToString(),
                Label = $"{dataField.Key} - {dataField.DisplayNameUk}" +
                    (dataField.MaxLength.HasValue ? $" (max {dataField.MaxLength})" : string.Empty)
            })
            .ToListAsync(cancellationToken);

        return new TemplateFieldMappingViewModel
        {
            TemplateVersionId = version.Id,
            TemplateId = version.TemplateId,
            TemplateNameUk = version.Template.NameUk,
            VersionNumber = version.VersionNumber,
            DocumentFormat = version.DocumentFormat,
            IsEditable = IsEditable(version),
            DataFields = dataFields,
            OpenInWordUri = version.DocumentFormat == TemplateDocumentFormat.DocxLegacy
                ? BuildOpenInWordLink(version.Id)
                : null,
            PdfPreviewUrl = version.DocumentFormat == TemplateDocumentFormat.Pdf
                ? BuildOpenOriginalLink(version.Id)
                : null,
            NewPdfField = new CreatePdfTemplateFieldViewModel
            {
                TemplateVersionId = version.Id
            },
            Fields = version.Fields
                .OrderBy(field => field.SortOrder)
                .Select(field =>
                {
                    var primarySegment = field.GetPrimarySegment();
                    return new TemplateFieldMappingItemViewModel
                    {
                        FieldId = field.Id,
                        Tag = field.Tag,
                        Title = field.Title,
                        WordControlType = field.WordControlType,
                        DataFieldId = field.DataFieldId,
                        IsRequired = field.IsRequired,
                        EstimatedCapacityChars = field.EstimatedCapacityChars,
                        MaxLines = field.MaxLines,
                        AllowMultiline = field.AllowMultiline,
                        OverflowPolicy = field.OverflowPolicy,
                        PageNumber = primarySegment?.PageNumber,
                        PositionX = primarySegment?.PositionX,
                        PositionY = primarySegment?.PositionY,
                        Width = primarySegment?.Width,
                        Height = primarySegment?.Height,
                        TextOffsetX = field.TextOffsetX,
                        TextOffsetY = field.TextOffsetY
                    };
                })
                .ToList()
        };
    }

    private async Task<TemplateFieldPermissionMatrixViewModel?> BuildPermissionsViewModelAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .Include(item => item.Template)
            .Include(item => item.Fields)
                .ThenInclude(field => field.Permissions)
            .FirstOrDefaultAsync(item => item.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            return null;
        }

        return new TemplateFieldPermissionMatrixViewModel
        {
            TemplateVersionId = version.Id,
            TemplateId = version.TemplateId,
            TemplateNameUk = version.Template.NameUk,
            VersionNumber = version.VersionNumber,
            IsEditable = IsEditable(version),
            Fields = version.Fields
                .OrderBy(field => field.SortOrder)
                .Select(field => new TemplateFieldPermissionRowViewModel
                {
                    FieldId = field.Id,
                    Tag = field.Tag,
                    Title = field.Title,
                    RolePermissions = LimsRoles.All
                        .Select(role => new RolePermissionItemViewModel
                        {
                            RoleName = role,
                            AccessLevel = field.Permissions
                                .FirstOrDefault(permission => permission.RoleName == role)
                                ?.AccessLevel ?? FieldAccessLevel.None
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private static bool IsEditable(TemplateVersion version)
    {
        return version.Status is TemplateVersionStatus.Draft or TemplateVersionStatus.ReadyForPublication;
    }

    private bool WantsMapJsonResponse()
    {
        if (string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private string? BuildOpenInWordLink(Guid versionId)
    {
        var token = _openTokenIssuer.CreateToken(versionId);
        var url = Url.Action(
            nameof(TemplateVersionsController.OpenOriginalForWord),
            "TemplateVersions",
            new { id = versionId, token },
            Request.Scheme,
            Request.Host.ToUriComponent());

        return string.IsNullOrEmpty(url) ? null : WordDesktopOpenUri.CreateOpenForEdit(url);
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
