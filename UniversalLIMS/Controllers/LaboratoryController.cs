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
    private readonly IResultEntryService _resultEntry;

    public LaboratoryController(
        ILaboratoryJournalService journal,
        IResultEntryService resultEntry)
    {
        _journal = journal;
        _resultEntry = resultEntry;
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

    [HttpGet]
    public async Task<IActionResult> Results(Guid sampleId, CancellationToken cancellationToken)
    {
        var form = await _resultEntry.GetResultEntryFormAsync(sampleId, cancellationToken);
        if (form is null)
        {
            return NotFound();
        }

        return View(new LaboratoryResultsViewModel
        {
            Form = form,
            Input = BuildInputFromForm(form)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Results(
        Guid sampleId,
        LaboratoryResultsViewModel model,
        CancellationToken cancellationToken)
    {
        var form = await _resultEntry.GetResultEntryFormAsync(sampleId, cancellationToken);
        if (form is null)
        {
            return NotFound();
        }

        var saveResult = await _resultEntry.SaveResultValuesAsync(
            sampleId,
            model.Input ?? new SaveResultEntryRequest(),
            cancellationToken);

        form = await _resultEntry.GetResultEntryFormAsync(sampleId, cancellationToken) ?? form;

        return View(new LaboratoryResultsViewModel
        {
            Form = form,
            Input = model.Input ?? BuildInputFromForm(form),
            StatusMessage = saveResult.Message,
            StatusType = saveResult.Success ? "success" : "danger"
        });
    }

    private static SaveResultEntryRequest BuildInputFromForm(ResultEntryFormDto form)
    {
        return new SaveResultEntryRequest
        {
            Values = form.Fields
                .Select(field => new SaveResultEntryFieldRequest
                {
                    DataFieldId = field.DataFieldId,
                    Value = field.CurrentValue,
                    Uncertainty = field.CurrentUncertainty,
                    EquipmentId = field.CurrentEquipmentId
                })
                .ToList()
        };
    }
}
