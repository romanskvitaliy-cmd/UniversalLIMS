using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Templates;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.ManageSystem)]
public sealed class TemplatesController : Controller
{
    private readonly ApplicationDbContext _context;

    public TemplatesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var templates = await _context.Templates
            .Include(template => template.Versions)
            .OrderBy(template => template.Code)
            .Select(template => new TemplateListItemViewModel
            {
                Id = template.Id,
                Code = template.Code,
                NameUk = template.NameUk,
                Status = template.Status,
                VersionCount = template.Versions.Count,
                CurrentPublishedVersionNumber = template.CurrentPublishedVersion == null
                    ? null
                    : template.CurrentPublishedVersion.VersionNumber
            })
            .ToListAsync(cancellationToken);

        return View(templates);
    }

    public IActionResult Create()
    {
        return View(new TemplateEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TemplateEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var codeExists = await _context.Templates
            .AnyAsync(template => template.Code == model.Code.Trim(), cancellationToken);

        if (codeExists)
        {
            ModelState.AddModelError(nameof(model.Code), "Шаблон із таким кодом уже існує.");
            return View(model);
        }

        var template = new Template
        {
            Code = model.Code.Trim(),
            NameUk = model.NameUk.Trim(),
            DescriptionUk = model.DescriptionUk?.Trim(),
            Status = TemplateStatus.Draft
        };

        _context.Templates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id = template.Id });
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var template = await _context.Templates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        return View(new TemplateEditViewModel
        {
            Id = template.Id,
            Code = template.Code,
            NameUk = template.NameUk,
            DescriptionUk = template.DescriptionUk
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, TemplateEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var template = await _context.Templates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        var normalizedCode = model.Code.Trim();
        var codeExists = await _context.Templates
            .AnyAsync(item => item.Id != id && item.Code == normalizedCode, cancellationToken);

        if (codeExists)
        {
            ModelState.AddModelError(nameof(model.Code), "Шаблон із таким кодом уже існує.");
            return View(model);
        }

        template.Code = normalizedCode;
        template.NameUk = model.NameUk.Trim();
        template.DescriptionUk = model.DescriptionUk?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var template = await _context.Templates
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (template is null)
        {
            return NotFound();
        }

        var versionIds = template.Versions.Select(version => version.Id).ToList();
        var fieldCounts = versionIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _context.TemplateFields
                .IgnoreQueryFilters()
                .Where(field => versionIds.Contains(field.TemplateVersionId) && !field.IsAnnulled)
                .GroupBy(field => field.TemplateVersionId)
                .Select(group => new { VersionId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.VersionId, item => item.Count, cancellationToken);

        var model = new TemplateDetailsViewModel
        {
            Id = template.Id,
            Code = template.Code,
            NameUk = template.NameUk,
            DescriptionUk = template.DescriptionUk,
            Status = template.Status,
            Versions = template.Versions
                .OrderByDescending(version => version.VersionNumber)
                .Select(version => new TemplateVersionListItemViewModel
                {
                    Id = version.Id,
                    VersionNumber = version.VersionNumber,
                    Status = version.Status,
                    OriginalFileName = version.OriginalFileName,
                    FieldCount = fieldCounts.GetValueOrDefault(version.Id),
                    UploadedAtUtc = version.UploadedAtUtc,
                    PublishedAtUtc = version.PublishedAtUtc
                })
                .ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> Annul(Guid id, CancellationToken cancellationToken)
    {
        var template = await _context.Templates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        return View(new AnnulTemplateViewModel
        {
            Id = template.Id,
            NameUk = template.NameUk
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Annul(Guid id, AnnulTemplateViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var template = await _context.Templates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        template.AnnulmentReason = model.AnnulmentReason.Trim();
        _context.Templates.Remove(template);
        await _context.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
