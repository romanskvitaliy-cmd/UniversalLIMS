using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.ViewModels.Registration;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.RegisterSamples)]
[RequireActiveLimsRole]
public sealed class OrdersController : Controller
{
    private readonly IOrderRegistrationService _orderRegistration;

    public OrdersController(IOrderRegistrationService orderRegistration)
    {
        _orderRegistration = orderRegistration;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] OrderFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _orderRegistration.GetOrdersAsync(filter, cancellationToken);

        return View(new OrdersIndexViewModel
        {
            Filter = filter,
            Result = result
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);
        return View(new OrderCreateViewModel { Form = form });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        OrderCreateInputModel input,
        CancellationToken cancellationToken)
    {
        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);

        ValidateCustomerSelection(input);

        if (!ModelState.IsValid)
        {
            return View(new OrderCreateViewModel { Form = form, Input = input });
        }

        try
        {
            var result = await _orderRegistration.CreateOrderAsync(MapToRequest(input), cancellationToken);

            TempData["OrderCreateSuccess"] =
                $"Створено замовлення {result.ReferralNumber}, проба {result.SampleNumber}.";

            if (input.OpenPdfAfterCreate)
            {
                return RedirectToAction(
                    "Fill",
                    "PdfWorkspace",
                    new { templateVersionId = result.TemplateVersionId, orderId = result.OrderId });
            }

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(new OrderCreateViewModel { Form = form, Input = input });
        }
    }

    private void ValidateCustomerSelection(OrderCreateInputModel input)
    {
        if (string.Equals(input.CustomerMode, "new", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(input.NewCustomerFullName))
            {
                ModelState.AddModelError(nameof(input.NewCustomerFullName), "ПІБ або назва нового замовника є обов'язковими.");
            }

            return;
        }

        if (!input.SelectedCustomerId.HasValue || input.SelectedCustomerId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(input.SelectedCustomerId), "Оберіть замовника з результатів пошуку.");
        }
    }

    private static CreateOrderRequest MapToRequest(OrderCreateInputModel input)
    {
        var useExisting = string.Equals(input.CustomerMode, "existing", StringComparison.OrdinalIgnoreCase);

        return new CreateOrderRequest
        {
            CustomerId = useExisting ? input.SelectedCustomerId : null,
            NewCustomer = useExisting
                ? null
                : new CreateCustomerRequest
                {
                    Kind = input.NewCustomerKind,
                    FullName = input.NewCustomerFullName ?? string.Empty,
                    OrganizationName = input.NewCustomerOrganizationName,
                    ContactPhone = input.NewCustomerContactPhone,
                    Address = input.NewCustomerAddress,
                    Edrpou = input.NewCustomerEdrpou
                },
            InvestigationTypeId = input.InvestigationTypeId,
            TemplateVersionId = input.TemplateVersionId
        };
    }
}
