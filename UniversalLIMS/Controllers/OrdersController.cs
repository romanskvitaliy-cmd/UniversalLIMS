using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Registration;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.RegisterSamples)]
public sealed class OrdersController : Controller
{
    private static readonly HashSet<string> ReservedDynamicKeys =
    [
        "Customer.FullName",
        "Customer.OrganizationName",
        "Customer.ContactPhone",
        "Sample.Number",
        "Sample.RegisteredAt",
        "Branch.Code",
        "Branch.Name",
        "Conclusion.Text"
    ];

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOrderRegistrationService _orderRegistrationService;
    private readonly IReferralPdfGenerator _referralPdfGenerator;

    public OrdersController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IOrderRegistrationService orderRegistrationService,
        IReferralPdfGenerator referralPdfGenerator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _orderRegistrationService = orderRegistrationService;
        _referralPdfGenerator = referralPdfGenerator;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var orders = await _context.Orders
            .OrderByDescending(order => order.RegisteredAtUtc ?? order.CreatedAtUtc)
            .Select(order => new OrderListItemViewModel
            {
                OrderId = order.Id,
                ReferralNumber = order.ReferralNumber,
                Status = order.Status,
                RegisteredAtUtc = order.RegisteredAtUtc,
                CustomerFullName = order.Customer.FullName,
                RegistrationBranchName = order.Branch.Name
            })
            .ToListAsync(cancellationToken);

        return View(orders);
    }

    public async Task<IActionResult> Register(Guid? customerId, CancellationToken cancellationToken)
    {
        var model = await BuildRegisterViewModelAsync(customerId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterSampleViewModel model, CancellationToken cancellationToken)
    {
        await PopulateRegisterLookupsAsync(model, cancellationToken);

        ValidateDynamicFields(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _orderRegistrationService.RegisterSampleAsync(new RegisterSampleRequest
            {
                CustomerId = model.CustomerId,
                InvestigationTypeId = model.InvestigationTypeId,
                RegistrationBranchId = model.RegistrationBranchId,
                TargetBranchId = model.TargetBranchId,
                RegisteredAtUtc = model.RegisteredAtUtc,
                OrderNotes = model.OrderNotes,
                SampleNotes = model.SampleNotes,
                DynamicFieldValues = model.DynamicFields
                    .Where(field => !string.IsNullOrWhiteSpace(field.StoredValue))
                    .Select(field => new OrderFieldValueInput
                    {
                        DataFieldId = field.DataFieldId,
                        StoredValue = field.StoredValue
                    })
                    .ToList()
            }, cancellationToken);

            TempData["RegistrationSuccess"] =
                $"Зареєстровано пробу {result.SampleNumber}, направлення {result.ReferralNumber}.";

            return RedirectToAction(nameof(Details), new { id = result.OrderId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var details = await _orderRegistrationService.GetOrderDetailsAsync(id, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        return View(new OrderDetailsViewModel
        {
            OrderId = details.OrderId,
            ReferralNumber = details.ReferralNumber,
            Status = details.Status,
            RegisteredAtUtc = details.RegisteredAtUtc,
            CustomerFullName = details.CustomerFullName,
            CustomerOrganizationName = details.CustomerOrganizationName,
            CustomerContactPhone = details.CustomerContactPhone,
            RegistrationBranchName = details.RegistrationBranchName,
            Samples = details.Samples.Select(sample => new OrderSampleItemViewModel
            {
                SampleId = sample.SampleId,
                Number = sample.Number,
                RegisteredAt = sample.RegisteredAt,
                InvestigationTypeName = sample.InvestigationTypeName,
                Status = sample.Status
            }).ToList(),
            Documents = details.Documents.Select(document => new OrderDocumentItemViewModel
            {
                OrderDocumentId = document.OrderDocumentId,
                TemplateCode = document.TemplateCode,
                TemplateName = document.TemplateName,
                TemplateVersionNumber = document.TemplateVersionNumber,
                TargetBranchName = document.TargetBranchName,
                Status = document.Status
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RouteSample(Guid sampleId, Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            await _orderRegistrationService.RouteSampleAsync(sampleId, cancellationToken);
            TempData["RegistrationSuccess"] = "Пробу маршрутизовано до лабораторії.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["RegistrationWarning"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id = orderId });
    }

    public async Task<IActionResult> ReferralPdf(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var pdfBytes = await _referralPdfGenerator.GenerateAsync(id, cancellationToken);
            var fileName = $"referral-{id:N}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException exception)
        {
            TempData["RegistrationWarning"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task<RegisterSampleViewModel> BuildRegisterViewModelAsync(
        Guid? customerId,
        CancellationToken cancellationToken)
    {
        var model = new RegisterSampleViewModel();
        await PopulateRegisterLookupsAsync(model, cancellationToken);

        if (customerId is not null)
        {
            model.CustomerId = customerId.Value;
            var customer = await _context.Customers
                .AsNoTracking()
                .Where(item => item.Id == customerId.Value)
                .Select(item => new { item.FullName, item.OrganizationName })
                .FirstOrDefaultAsync(cancellationToken);

            if (customer is not null)
            {
                model.SelectedCustomerDisplay = string.IsNullOrWhiteSpace(customer.OrganizationName)
                    ? customer.FullName
                    : $"{customer.FullName} ({customer.OrganizationName})";
            }
        }

        if (_currentUserService.BranchId is Guid userBranchId &&
            model.Branches.Any(branch => branch.Value == userBranchId.ToString()))
        {
            model.RegistrationBranchId = userBranchId;
            model.TargetBranchId = userBranchId;
        }

        return model;
    }

    private async Task PopulateRegisterLookupsAsync(
        RegisterSampleViewModel model,
        CancellationToken cancellationToken)
    {
        model.InvestigationTypes = await _context.InvestigationTypes
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new SelectListItem(item.NameUk, item.Id.ToString()))
            .ToListAsync(cancellationToken);

        model.Branches = await _context.Branches
            .Where(branch => branch.IsActive)
            .OrderBy(branch => branch.Name)
            .Select(branch => new SelectListItem($"{branch.Name} ({branch.Code})", branch.Id.ToString()))
            .ToListAsync(cancellationToken);

        model.Customers = await _context.Customers
            .OrderBy(customer => customer.FullName)
            .Take(200)
            .Select(customer => new SelectListItem(
                string.IsNullOrWhiteSpace(customer.OrganizationName)
                    ? customer.FullName
                    : $"{customer.FullName} ({customer.OrganizationName})",
                customer.Id.ToString()))
            .ToListAsync(cancellationToken);

        model.DynamicFields = await _context.DataFields
            .Where(field => field.IsActive &&
                            !field.IsSystem &&
                            (field.Scope == DataFieldScope.Registration || field.Scope == DataFieldScope.Sample) &&
                            !ReservedDynamicKeys.Contains(field.Key))
            .OrderBy(field => field.DisplayNameUk)
            .Select(field => new DynamicFieldInputViewModel
            {
                DataFieldId = field.Id,
                DisplayNameUk = field.DisplayNameUk,
                IsRequired = field.IsRequired,
                MaxLength = field.MaxLength
            })
            .ToListAsync(cancellationToken);
    }

    private void ValidateDynamicFields(RegisterSampleViewModel model)
    {
        for (var index = 0; index < model.DynamicFields.Count; index++)
        {
            var field = model.DynamicFields[index];
            if (field.IsRequired && string.IsNullOrWhiteSpace(field.StoredValue))
            {
                ModelState.AddModelError(
                    $"DynamicFields[{index}].StoredValue",
                    $"Поле «{field.DisplayNameUk}» є обов'язковим.");
            }
        }
    }
}
