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
    private readonly ILaboratoryBranchContext _laboratoryBranchContext;
    private readonly ApplicationDbContext _context;

    public LaboratoryController(
        ILaboratoryJournalService journal,
        ILaboratoryPdfFillService pdfFill,
        ILaboratoryBranchContext laboratoryBranchContext,
        ApplicationDbContext context)
    {
        _journal = journal;
        _pdfFill = pdfFill;
        _laboratoryBranchContext = laboratoryBranchContext;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] SampleJournalFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _journal.GetSamplesAsync(filter, cancellationToken);
        var branchContext = await _laboratoryBranchContext.GetStateAsync(cancellationToken);

        return View(new LaboratoryIndexViewModel
        {
            Filter = filter,
            Result = result,
            CanSelectLaboratoryBranch = branchContext.CanSelectBranch,
            ActiveLaboratoryBranchId = branchContext.ActiveBranchId,
            ActiveLaboratoryBranchName = branchContext.ActiveBranchId is Guid branchId
                ? branchContext.Branches.FirstOrDefault(branch => branch.Id == branchId)?.Name
                : null,
            LaboratoryBranches = branchContext.Branches
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLaboratoryBranch(
        Guid? activeLaboratoryBranchId,
        CancellationToken cancellationToken)
    {
        await _laboratoryBranchContext.SetSelectedBranchAsync(activeLaboratoryBranchId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Відкриває PDF Workspace для заповнення полів на бланку.</summary>
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
                new
                {
                    templateVersionId = target.TemplateVersionId,
                    orderId = target.OrderId,
                    orderDocumentId = target.OrderDocumentId,
                    sampleId
                });
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
