using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Laboratory;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.EnterLaboratoryResults)]
[RequireActiveLimsRole]
public sealed class LaboratoryController : Controller
{
    private readonly ILaboratoryJournalService _journal;
    private readonly ILaboratoryPdfFillService _pdfFill;
    private readonly ApplicationDbContext _context;

    public LaboratoryController(
        ILaboratoryJournalService journal,
        ILaboratoryPdfFillService pdfFill,
        ApplicationDbContext context)
    {
        _journal = journal;
        _pdfFill = pdfFill;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] SampleJournalFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _journal.GetSamplesAsync(filter, cancellationToken);

        return View(new LaboratoryIndexViewModel
        {
            Filter = filter,
            Result = result
        });
    }

    /// <summary>Відкриває PDF Workspace замість табличного внесення результатів.</summary>
    [HttpGet]
    public async Task<IActionResult> Results(Guid sampleId, CancellationToken cancellationToken)
    {
        var sampleMeta = await _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Id == sampleId && !sample.IsAnnulled)
            .Select(sample => new
            {
                sample.Number,
                sample.InvestigationType.NameUk
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleMeta is null)
        {
            return NotFound();
        }

        var targets = await _pdfFill.GetFillTargetsAsync(sampleId, cancellationToken);
        if (targets.Count == 0)
        {
            TempData["LaboratoryWarning"] =
                "Для цієї проби немає PDF-документів у статусі «У лабораторії» / «В роботі» і не знайдено опублікованого PDF-шаблону типу дослідження.";
            return RedirectToAction(nameof(Index));
        }

        if (targets.Count == 1)
        {
            var target = targets[0];
            return RedirectToAction(
                "Fill",
                "PdfWorkspace",
                new { templateVersionId = target.TemplateVersionId, orderId = target.OrderId });
        }

        return View(
            "ChooseDocument",
            new LaboratoryChooseDocumentViewModel
            {
                SampleNumber = sampleMeta.Number,
                InvestigationTypeName = sampleMeta.NameUk,
                Targets = targets
            });
    }
}
