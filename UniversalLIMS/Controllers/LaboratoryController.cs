using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.ViewModels.Laboratory;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.EnterLaboratoryResults)]
[RequireActiveLimsRole]
public sealed class LaboratoryController : Controller
{
    private readonly ILaboratoryJournalService _journal;
    private readonly ILaboratoryPdfFillService _pdfFill;
    private readonly ILaboratoryBranchContext _laboratoryBranchContext;

    public LaboratoryController(
        ILaboratoryJournalService journal,
        ILaboratoryPdfFillService pdfFill,
        ILaboratoryBranchContext laboratoryBranchContext)
    {
        _journal = journal;
        _pdfFill = pdfFill;
        _laboratoryBranchContext = laboratoryBranchContext;
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

    /// <summary>Картка проби — ланцюжок документів (заповнення + відправка експерту).</summary>
    [HttpGet]
    public async Task<IActionResult> SampleDetails(Guid sampleId, CancellationToken cancellationToken)
    {
        var detail = await _pdfFill.GetSampleDetailsAsync(sampleId, cancellationToken);
        if (detail is null)
        {
            TempData["LaboratoryWarning"] =
                "Пробу не знайдено або для неї немає документів у лабораторному workflow.";
            return RedirectToAction(nameof(Index));
        }

        return View(new LaboratorySampleDetailsViewModel { Detail = detail });
    }

    /// <summary>Legacy alias — перенаправлення на картку проби.</summary>
    [HttpGet]
    public IActionResult Results(Guid sampleId) =>
        RedirectToAction(nameof(SampleDetails), new { sampleId });
}
