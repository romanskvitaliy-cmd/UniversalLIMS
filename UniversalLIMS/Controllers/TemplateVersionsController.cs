using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;
using UniversalLIMS.ViewModels.Templates;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.ManageSystem)]
public sealed class TemplateVersionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateVersionService _templateVersionService;
    private readonly ITemplateDocumentStorage _documentStorage;
    private readonly ITemplateOriginalOpenTokenIssuer _openTokenIssuer;

    public TemplateVersionsController(
        ApplicationDbContext context,
        ITemplateVersionService templateVersionService,
        ITemplateDocumentStorage documentStorage,
        ITemplateOriginalOpenTokenIssuer openTokenIssuer)
    {
        _context = context;
        _templateVersionService = templateVersionService;
        _documentStorage = documentStorage;
        _openTokenIssuer = openTokenIssuer;
    }

    public async Task<IActionResult> Upload(Guid templateId, CancellationToken cancellationToken)
    {
        var template = await _context.Templates.FirstOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        return View(new TemplateVersionUploadViewModel
        {
            TemplateId = template.Id,
            TemplateNameUk = template.NameUk,
            TagSourceVersions = await GetTagSourceVersionsAsync(template.Id, cancellationToken: cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(TemplateVersionUploadViewModel model, CancellationToken cancellationToken)
    {
        var template = await _context.Templates.FirstOrDefaultAsync(item => item.Id == model.TemplateId, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        model.TemplateNameUk = template.NameUk;

        if (!ModelState.IsValid)
        {
            model.TagSourceVersions = await GetTagSourceVersionsAsync(model.TemplateId, cancellationToken: cancellationToken);
            return View(model);
        }

        if (model.Document is null)
        {
            ModelState.AddModelError(nameof(model.Document), "Оберіть файл .pdf, .docx або .doc.");
            model.TagSourceVersions = await GetTagSourceVersionsAsync(model.TemplateId, cancellationToken: cancellationToken);
            return View(model);
        }

        var extension = Path.GetExtension(model.Document.FileName);
        if (!IsAllowedUploadExtension(extension))
        {
            ModelState.AddModelError(nameof(model.Document), "Дозволено завантажувати тільки файли .pdf, .docx або .doc.");
            model.TagSourceVersions = await GetTagSourceVersionsAsync(model.TemplateId, cancellationToken: cancellationToken);
            return View(model);
        }

        Guid versionId;
        try
        {
            await using var stream = model.Document.OpenReadStream();
            versionId = await _templateVersionService.CreateDraftVersionAsync(
                model.TemplateId,
                model.Document.FileName,
                model.Document.ContentType,
                stream,
                model.CopyFieldsFromVersionId,
                cancellationToken);
        }
        catch (InvalidDataException)
        {
            ModelState.AddModelError(nameof(model.Document), "Файл пошкоджений або має некоректний формат.");
            model.TagSourceVersions = await GetTagSourceVersionsAsync(model.TemplateId, cancellationToken: cancellationToken);
            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(model.Document), exception.Message);
            model.TagSourceVersions = await GetTagSourceVersionsAsync(model.TemplateId, cancellationToken: cancellationToken);
            return View(model);
        }

        if (model.CopyFieldsFromVersionId.HasValue)
        {
            TempData["TemplateSuccess"] = "Нову версію створено з копією тегів і координат overlay.";
        }

        return RedirectToAction(nameof(Details), new { id = versionId });
    }

    public async Task<IActionResult> DownloadOriginal(Guid id, CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (version is null)
        {
            return NotFound();
        }

        if (!await _documentStorage.ExistsAsync(version.StorageKey, cancellationToken))
        {
            return NotFound();
        }

        var stream = await _documentStorage.OpenReadAsync(version.StorageKey, cancellationToken);
        return File(stream, version.ContentType, version.OriginalFileName);
    }

    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> OpenOriginal(Guid id, string token, CancellationToken cancellationToken)
    {
        if (!_openTokenIssuer.TryValidateToken(token, out var versionId) || versionId != id)
        {
            return NotFound();
        }

        var version = await _context.TemplateVersions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (version is null)
        {
            return NotFound();
        }

        if (!await _documentStorage.ExistsAsync(version.StorageKey, cancellationToken))
        {
            return NotFound();
        }

        var stream = await _documentStorage.OpenReadAsync(version.StorageKey, cancellationToken);
        Response.Headers.ContentDisposition = $"inline; filename=\"{Uri.EscapeDataString(version.OriginalFileName)}\"";
        return File(stream, version.ContentType, enableRangeProcessing: true);
    }

    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> OpenOriginalForWord(Guid id, string token, CancellationToken cancellationToken)
    {
        if (!_openTokenIssuer.TryValidateToken(token, out var versionId) || versionId != id)
        {
            return NotFound();
        }

        var version = await _context.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (version is null)
        {
            return NotFound();
        }

        if (!await _documentStorage.ExistsAsync(version.StorageKey, cancellationToken))
        {
            return NotFound();
        }

        var stream = await _documentStorage.OpenReadAsync(version.StorageKey, cancellationToken);
        Response.Headers.ContentDisposition = $"inline; filename=\"{Uri.EscapeDataString(version.OriginalFileName)}\"";
        return File(stream, version.ContentType, enableRangeProcessing: true);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildDetailsViewModelAsync(id, [], cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    public async Task<IActionResult> Annul(Guid id, CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .Include(item => item.Template)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (version is null)
        {
            return NotFound();
        }

        return View(new AnnulTemplateVersionViewModel
        {
            Id = version.Id,
            TemplateId = version.TemplateId,
            TemplateNameUk = version.Template.NameUk,
            VersionNumber = version.VersionNumber,
            Status = version.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Annul(
        Guid id,
        AnnulTemplateVersionViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _templateVersionService.AnnulAsync(id, model.AnnulmentReason, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(model.AnnulmentReason), exception.Message);
            return View(model);
        }

        return RedirectToAction("Details", "Templates", new { id = model.TemplateId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescanFields(Guid id, CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (version is null)
        {
            return NotFound();
        }

        if (version.DocumentFormat != TemplateDocumentFormat.DocxLegacy)
        {
            TempData["TemplateWarning"] = "Повторне читання полів доступне тільки для legacy .docx версій.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _templateVersionService.RescanFieldsAsync(id, cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportTags(
        Guid id,
        Guid sourceVersionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _templateVersionService.CopyFieldsFromVersionAsync(id, sourceVersionId, cancellationToken);
            TempData["TemplateSuccess"] = "Теги та координати overlay скопійовано з обраної версії.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["TemplateWarning"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id, string? publicationNotesUk, CancellationToken cancellationToken)
    {
        var result = await _templateVersionService.PublishAsync(id, publicationNotesUk, cancellationToken);
        if (result.IsValid)
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = await BuildDetailsViewModelAsync(id, result.Errors, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(nameof(Details), model);
    }

    private async Task<TemplateVersionDetailsViewModel?> BuildDetailsViewModelAsync(
        Guid id,
        IReadOnlyCollection<string> validationErrors,
        CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .Include(item => item.Template)
            .Include(item => item.Fields)
                .ThenInclude(field => field.DataField)
            .Include(item => item.Fields)
                .ThenInclude(field => field.Permissions)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (version is null)
        {
            return null;
        }

        var fields = version.Fields
            .OrderBy(field => field.SortOrder)
            .Select(field => new TemplateFieldListItemViewModel
            {
                Id = field.Id,
                Tag = field.Tag,
                Title = field.Title,
                WordControlType = field.WordControlType,
                DataFieldKey = field.DataField?.Key,
                DataFieldDisplayNameUk = field.DataField?.DisplayNameUk,
                IsRequired = field.IsRequired,
                EstimatedCapacityChars = field.EstimatedCapacityChars,
                PermissionCount = field.Permissions.Count,
            })
            .ToList();
        var isEditable = version.Status is TemplateVersionStatus.Draft or TemplateVersionStatus.ReadyForPublication;
        var tagSourceVersions = fields.Count == 0 && isEditable
            ? await GetTagSourceVersionsAsync(
                version.TemplateId,
                version.DocumentFormat,
                version.Id,
                cancellationToken)
            : [];

        return new TemplateVersionDetailsViewModel
        {
            Id = version.Id,
            TemplateId = version.TemplateId,
            TemplateNameUk = version.Template.NameUk,
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            DocumentFormat = version.DocumentFormat,
            OriginalFileName = version.OriginalFileName,
            FileSizeBytes = version.FileSizeBytes,
            Sha256Hash = version.Sha256Hash,
            UploadedAtUtc = version.UploadedAtUtc,
            PublishedAtUtc = version.PublishedAtUtc,
            PublicationNotesUk = version.PublicationNotesUk,
            ValidationErrors = validationErrors,
            Fields = fields,
            OpenInWordUri = version.DocumentFormat == TemplateDocumentFormat.DocxLegacy
                ? BuildOpenInWordLink(version.Id)
                : null,
            OpenOriginalUrl = BuildOpenOriginalLink(version.Id),
            CanRescanFields = version.DocumentFormat == TemplateDocumentFormat.DocxLegacy,
            CanImportTags = fields.Count == 0 && isEditable && tagSourceVersions.Count > 0,
            TagSourceVersions = tagSourceVersions
        };
    }

    private async Task<IReadOnlyCollection<TemplateVersionTagSourceOptionViewModel>> GetTagSourceVersionsAsync(
        Guid templateId,
        TemplateDocumentFormat? documentFormat = null,
        Guid? excludeVersionId = null,
        CancellationToken cancellationToken = default)
    {
        var versions = await _context.TemplateVersions
            .AsNoTracking()
            .Where(version => version.TemplateId == templateId)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new
            {
                version.Id,
                version.VersionNumber,
                version.OriginalFileName,
                version.DocumentFormat
            })
            .ToListAsync(cancellationToken);

        if (versions.Count == 0)
        {
            return [];
        }

        var versionIds = versions
            .Where(version => !excludeVersionId.HasValue || version.Id != excludeVersionId.Value)
            .Select(version => version.Id)
            .ToList();

        if (versionIds.Count == 0)
        {
            return [];
        }

        var fieldCounts = await _context.TemplateFields
            .IgnoreQueryFilters()
            .Where(field => versionIds.Contains(field.TemplateVersionId) && !field.IsAnnulled)
            .GroupBy(field => field.TemplateVersionId)
            .Select(group => new { VersionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.VersionId, item => item.Count, cancellationToken);

        return versions
            .Where(version => !excludeVersionId.HasValue || version.Id != excludeVersionId.Value)
            .Where(version => documentFormat is null || version.DocumentFormat == documentFormat)
            .Select(version => new TemplateVersionTagSourceOptionViewModel
            {
                Id = version.Id,
                VersionNumber = version.VersionNumber,
                OriginalFileName = version.OriginalFileName,
                FieldCount = fieldCounts.GetValueOrDefault(version.Id)
            })
            .Where(version => version.FieldCount > 0)
            .ToList();
    }

    private static bool IsAllowedUploadExtension(string? extension)
    {
        return extension is not null &&
               (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".doc", StringComparison.OrdinalIgnoreCase));
    }

    private string? BuildOpenInWordLink(Guid versionId)
    {
        var token = _openTokenIssuer.CreateToken(versionId);
        var url = Url.Action(
            nameof(OpenOriginalForWord),
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
            nameof(OpenOriginal),
            "TemplateVersions",
            new { id = versionId, token });
    }
}
