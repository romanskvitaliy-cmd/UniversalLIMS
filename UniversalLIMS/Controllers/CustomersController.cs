using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Registration;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.RegisterSamples)]
public sealed class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICustomerService _customerService;

    public CustomersController(ApplicationDbContext context, ICustomerService customerService)
    {
        _context = context;
        _customerService = customerService;
    }

    public async Task<IActionResult> Index(string? query, CancellationToken cancellationToken)
    {
        var results = await _customerService.SearchAsync(query, 50, cancellationToken);

        var model = new CustomerSearchViewModel
        {
            Query = query,
            Results = results.Select(customer => new CustomerListItemViewModel
            {
                Id = customer.Id,
                FullName = customer.FullName,
                OrganizationName = customer.OrganizationName,
                ContactPhone = customer.ContactPhone,
                Edrpou = customer.Edrpou
            }).ToList()
        };

        return View(model);
    }

    public IActionResult Create()
    {
        return View(new CustomerEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customerId = await _customerService.CreateAsync(MapToRequest(model), cancellationToken);
        return RedirectToAction(nameof(Details), new { id = customerId });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        return View(new CustomerDetailsViewModel
        {
            Id = customer.Id,
            Kind = customer.Kind,
            FullName = customer.FullName,
            OrganizationName = customer.OrganizationName,
            ContactPhone = customer.ContactPhone,
            Email = customer.Email,
            Address = customer.Address,
            Edrpou = customer.Edrpou,
            Rnokpp = customer.Rnokpp,
            Notes = customer.Notes
        });
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        return View(new CustomerEditViewModel
        {
            Id = customer.Id,
            Kind = customer.Kind,
            FullName = customer.FullName,
            OrganizationName = customer.OrganizationName,
            ContactPhone = customer.ContactPhone,
            Email = customer.Email,
            Address = customer.Address,
            Edrpou = customer.Edrpou,
            Rnokpp = customer.Rnokpp,
            Notes = customer.Notes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CustomerEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _customerService.UpdateAsync(id, MapToRequest(model), cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Annul(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        return View(new CustomerAnnulViewModel
        {
            Id = customer.Id,
            FullName = customer.FullName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Annul(Guid id, CustomerAnnulViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _customerService.AnnulAsync(id, model.Reason, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> SearchJson(string? query, CancellationToken cancellationToken)
    {
        var results = await _customerService.SearchAsync(query, 20, cancellationToken);
        return Json(results.Select(customer => new
        {
            customer.Id,
            label = $"{customer.FullName}" +
                    (string.IsNullOrWhiteSpace(customer.OrganizationName) ? "" : $" ({customer.OrganizationName})") +
                    (string.IsNullOrWhiteSpace(customer.ContactPhone) ? "" : $" — {customer.ContactPhone}")
        }));
    }

    private static UpdateCustomerRequest MapToRequest(CustomerEditViewModel model) =>
        new()
        {
            Kind = model.Kind,
            FullName = model.FullName,
            OrganizationName = model.OrganizationName,
            ContactPhone = model.ContactPhone,
            Email = model.Email,
            Address = model.Address,
            Edrpou = model.Edrpou,
            Rnokpp = model.Rnokpp,
            Notes = model.Notes
        };
}
