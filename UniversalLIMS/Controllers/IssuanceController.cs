using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.ViewModels.Issuance;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.RegisterSamples)]
[RequireActiveLimsRole]
public sealed class IssuanceController : Controller
{
    private readonly ISampleDeliveryService _delivery;

    public IssuanceController(ISampleDeliveryService delivery)
    {
        _delivery = delivery;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] SampleDeliveryQueueFilter filter,
        CancellationToken cancellationToken)
    {
        filter.SampleNumber = string.IsNullOrWhiteSpace(filter.SampleNumber) ? null : filter.SampleNumber.Trim();
        filter.CustomerFullName = string.IsNullOrWhiteSpace(filter.CustomerFullName)
            ? null
            : filter.CustomerFullName.Trim();

        var result = await _delivery.GetQueueAsync(filter, cancellationToken);
        return View(new IssuanceIndexViewModel
        {
            Filter = filter,
            Result = result
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkIssued(Guid sampleId, CancellationToken cancellationToken)
    {
        var issued = await _delivery.MarkIssuedAsync(sampleId, cancellationToken);
        TempData[issued ? "IssuanceSuccess" : "IssuanceWarning"] = issued
            ? "Результати по пробі позначено як видані клієнту."
            : "Не вдалося зафіксувати видачу (проба не в статусі «Готово до видачі» або немає затвердження).";

        return RedirectToAction(nameof(Index));
    }
}
