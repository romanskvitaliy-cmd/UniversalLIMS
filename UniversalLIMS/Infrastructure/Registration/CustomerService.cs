using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _context;

    public CustomerService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(
        string? query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Clamp(take, 1, 100);
        var trimmedQuery = query?.Trim();

        var customersQuery = _context.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            var pattern = $"%{trimmedQuery}%";
            customersQuery = customersQuery.Where(customer =>
                EF.Functions.Like(customer.FullName, pattern) ||
                (customer.OrganizationName != null && EF.Functions.Like(customer.OrganizationName, pattern)) ||
                (customer.ContactPhone != null && EF.Functions.Like(customer.ContactPhone, pattern)) ||
                (customer.Edrpou != null && EF.Functions.Like(customer.Edrpou, pattern)) ||
                (customer.Rnokpp != null && EF.Functions.Like(customer.Rnokpp, pattern)));
        }

        return await customersQuery
            .OrderBy(customer => customer.FullName)
            .Take(normalizedTake)
            .Select(customer => new CustomerSearchResult
            {
                Id = customer.Id,
                FullName = customer.FullName,
                OrganizationName = customer.OrganizationName,
                ContactPhone = customer.ContactPhone,
                Edrpou = customer.Edrpou
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerSearchResult?> GetAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .Where(customer => customer.Id == customerId)
            .Select(customer => new CustomerSearchResult
            {
                Id = customer.Id,
                FullName = customer.FullName,
                OrganizationName = customer.OrganizationName,
                ContactPhone = customer.ContactPhone,
                Edrpou = customer.Edrpou
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var customer = new Customer
        {
            Kind = request.Kind,
            FullName = request.FullName.Trim(),
            OrganizationName = request.OrganizationName?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            Edrpou = request.Edrpou?.Trim(),
            Rnokpp = request.Rnokpp?.Trim(),
            Notes = request.Notes?.Trim()
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }

    public async Task UpdateAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var customer = await _context.Customers
            .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);

        if (customer is null)
        {
            throw new InvalidOperationException("Замовника не знайдено.");
        }

        customer.Kind = request.Kind;
        customer.FullName = request.FullName.Trim();
        customer.OrganizationName = request.OrganizationName?.Trim();
        customer.ContactPhone = request.ContactPhone?.Trim();
        customer.Email = request.Email?.Trim();
        customer.Address = request.Address?.Trim();
        customer.Edrpou = request.Edrpou?.Trim();
        customer.Rnokpp = request.Rnokpp?.Trim();
        customer.Notes = request.Notes?.Trim();

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AnnulAsync(Guid customerId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Причина анулювання є обов'язковою.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);

        if (customer is null)
        {
            throw new InvalidOperationException("Замовника не знайдено.");
        }

        customer.AnnulmentReason = reason.Trim();
        _context.Remove(customer);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRequest(CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new InvalidOperationException("ПІБ замовника є обов'язковим.");
        }
    }
}
