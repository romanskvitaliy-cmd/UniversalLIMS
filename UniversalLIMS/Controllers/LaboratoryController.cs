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

    public LaboratoryController(ILaboratoryJournalService journal)
    {
        _journal = journal;
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
}
