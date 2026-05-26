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
        ValidateDocumentSelection(input, form);

        if (!ModelState.IsValid)
        {
            return View(new OrderCreateViewModel { Form = form, Input = input });
        }

        try
        {
            var result = await _orderRegistration.CreateOrderAsync(MapToRequest(input, form), cancellationToken);

            TempData["OrderCreateSuccess"] =
                $"Створено замовлення {result.ReferralNumber}, проба {result.SampleNumber}, документів: {result.Documents.Count}.";

            if (input.OpenPdfAfterCreate && result.Documents.Count == 1)
            {
                return RedirectToAction(
                    "Fill",
                    "PdfWorkspace",
                    new { templateVersionId = result.Documents[0].TemplateVersionId, orderId = result.OrderId });
            }

            return RedirectToAction(nameof(Details), new { id = result.OrderId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(new OrderCreateViewModel { Form = form, Input = input });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _orderRegistration.GetOrderDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);

        return View(new OrderDetailViewModel
        {
            Detail = detail,
            Branches = form.Branches
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDocuments(
        SendOrderDocumentsInputModel input,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orderRegistration.SendDocumentsToLabAsync(
                new SendOrderDocumentsRequest
                {
                    OrderId = input.OrderId,
                    OrderDocumentIds = input.OrderDocumentIds
                },
                cancellationToken);

            TempData["OrderDetailSuccess"] = "Обрані документи відправлено в лабораторію.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["OrderDetailError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = input.OrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRouting(
        UpdateOrderDocumentRoutingInputModel input,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orderRegistration.UpdateDocumentRoutingAsync(
                new UpdateOrderDocumentRoutingRequest
                {
                    OrderId = input.OrderId,
                    OrderDocumentId = input.OrderDocumentId,
                    TargetBranchId = input.TargetBranchId
                },
                cancellationToken);

            TempData["OrderDetailSuccess"] = "Маршрут документа оновлено.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["OrderDetailError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = input.OrderId });
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

    private void ValidateDocumentSelection(OrderCreateInputModel input, OrderCreateFormDto form)
    {
        if (input.SelectedTemplateVersionIds.Count == 0)
        {
            return;
        }

        var allowedIds = form.TemplateOptions
            .Where(option => option.InvestigationTypeId == input.InvestigationTypeId)
            .Select(option => option.TemplateVersionId)
            .ToHashSet();

        if (input.SelectedTemplateVersionIds.Any(id => !allowedIds.Contains(id)))
        {
            ModelState.AddModelError(
                nameof(input.SelectedTemplateVersionIds),
                "Обрано недоступний шаблон для цього типу дослідження.");
        }

        if (input.SelectedTemplateVersionIds.Distinct().Count() != input.SelectedTemplateVersionIds.Count)
        {
            ModelState.AddModelError(
                nameof(input.SelectedTemplateVersionIds),
                "Кожен шаблон можна обрати лише один раз.");
        }
    }

    private static CreateOrderRequest MapToRequest(OrderCreateInputModel input, OrderCreateFormDto form)
    {
        var useExisting = string.Equals(input.CustomerMode, "existing", StringComparison.OrdinalIgnoreCase);
        var defaultBranchId = form.DefaultBranchId ?? Guid.Empty;

        var documents = new List<CreateOrderDocumentRequest>();
        for (var index = 0; index < input.SelectedTemplateVersionIds.Count; index++)
        {
            var templateVersionId = input.SelectedTemplateVersionIds[index];
            var targetBranchId = index < input.DocumentTargetBranchIds.Count
                && input.DocumentTargetBranchIds[index] != Guid.Empty
                ? input.DocumentTargetBranchIds[index]
                : defaultBranchId;

            documents.Add(new CreateOrderDocumentRequest
            {
                TemplateVersionId = templateVersionId,
                TargetBranchId = targetBranchId
            });
        }

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
            TemplateVersionId = input.TemplateVersionId,
            Documents = documents
        };
    }
}
