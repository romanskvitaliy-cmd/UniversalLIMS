using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class OrderRegistrationService : IOrderRegistrationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICustomerService _customerService;
    private readonly INumberingService _numberingService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public OrderRegistrationService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ICustomerService customerService,
        INumberingService numberingService,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _customerService = customerService;
        _numberingService = numberingService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PagedResult<OrderListItemDto>> GetOrdersAsync(
        OrderFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var ordersQuery = _context.Orders
            .AsNoTracking()
            .Where(order => !order.IsAnnulled && !order.Customer.IsAnnulled);

        if (_currentUser.BranchId is Guid branchId)
        {
            ordersQuery = ordersQuery.Where(order => order.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ReferralNumber))
        {
            var pattern = $"%{filter.ReferralNumber.Trim()}%";
            ordersQuery = ordersQuery.Where(order =>
                order.ReferralNumber != null && EF.Functions.Like(order.ReferralNumber, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerFullName))
        {
            var pattern = $"%{filter.CustomerFullName.Trim()}%";
            ordersQuery = ordersQuery.Where(order =>
                EF.Functions.Like(order.Customer.FullName, pattern));
        }

        if (filter.Status.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.Status == filter.Status.Value);
        }

        if (filter.DateFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.DateFrom.Value.Date, DateTimeKind.Utc);
            ordersQuery = ordersQuery.Where(order =>
                (order.RegisteredAtUtc ?? order.CreatedAtUtc) >= fromUtc);
        }

        if (filter.DateTo.HasValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(filter.DateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            ordersQuery = ordersQuery.Where(order =>
                (order.RegisteredAtUtc ?? order.CreatedAtUtc) < toExclusiveUtc);
        }

        var totalCount = await ordersQuery.CountAsync(cancellationToken);

        var items = await ordersQuery
            .OrderByDescending(order => order.RegisteredAtUtc ?? order.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new OrderListItemDto
            {
                OrderId = order.Id,
                ReferralNumber = order.ReferralNumber,
                CustomerFullName = order.Customer.FullName,
                OrderDate = order.RegisteredAtUtc ?? order.CreatedAtUtc,
                Status = order.Status,
                SampleCount = order.Samples.Count(sample => !sample.IsAnnulled),
                TargetBranchName = order.OrderDocuments
                    .Where(document => !document.IsAnnulled)
                    .OrderByDescending(document => document.CreatedAtUtc)
                    .Select(document => document.TargetBranch.Name)
                    .FirstOrDefault() ?? order.Branch.Name,
                PrimaryTemplateVersionId = order.OrderDocuments
                    .Where(document => !document.IsAnnulled)
                    .OrderByDescending(document => document.CreatedAtUtc)
                    .Select(document => (Guid?)document.TemplateVersionId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<OrderCreateFormDto> GetCreateFormAsync(CancellationToken cancellationToken = default)
    {
        var investigationTypes = await _context.InvestigationTypes
            .AsNoTracking()
            .Where(type => type.IsActive && !type.IsAnnulled)
            .OrderBy(type => type.SortOrder)
            .Select(type => new InvestigationTypeOptionDto
            {
                Id = type.Id,
                Code = type.Code,
                NameUk = type.NameUk
            })
            .ToListAsync(cancellationToken);

        var templateOptions = await (
                from link in _context.InvestigationTypeTemplates.AsNoTracking()
                join template in _context.Templates.AsNoTracking() on link.TemplateId equals template.Id
                join version in _context.TemplateVersions.AsNoTracking() on template.CurrentPublishedVersionId equals version.Id
                where link.IsActive
                      && !template.IsAnnulled
                      && version.Status == TemplateVersionStatus.Published
                      && !version.IsAnnulled
                      && version.DocumentFormat == TemplateDocumentFormat.Pdf
                orderby link.InvestigationTypeId, link.SortOrder
                select new
                {
                    link.InvestigationTypeId,
                    link.SortOrder,
                    version.Id,
                    template.NameUk,
                    version.VersionNumber
                })
            .ToListAsync(cancellationToken);

        var defaultSortByType = templateOptions
            .GroupBy(option => option.InvestigationTypeId)
            .ToDictionary(group => group.Key, group => group.Min(item => item.SortOrder));

        var templateDtos = templateOptions
            .Select(option => new OrderTemplateOptionDto
            {
                InvestigationTypeId = option.InvestigationTypeId,
                TemplateVersionId = option.Id,
                TemplateNameUk = option.NameUk,
                VersionNumber = option.VersionNumber,
                IsDefault = option.SortOrder == defaultSortByType[option.InvestigationTypeId]
            })
            .ToList();

        return new OrderCreateFormDto
        {
            InvestigationTypes = investigationTypes,
            TemplateOptions = templateDtos
        };
    }

    public async Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.BranchId is not Guid branchId)
        {
            throw new InvalidOperationException("Для створення замовлення потрібна філія користувача.");
        }

        var customerId = await ResolveCustomerIdAsync(request, cancellationToken);

        var investigationTypeExists = await _context.InvestigationTypes
            .AnyAsync(
                type => type.Id == request.InvestigationTypeId && type.IsActive && !type.IsAnnulled,
                cancellationToken);

        if (!investigationTypeExists)
        {
            throw new InvalidOperationException("Оберіть коректний тип дослідження.");
        }

        var templateVersionId = await ResolveTemplateVersionIdAsync(
            request.InvestigationTypeId,
            request.TemplateVersionId,
            cancellationToken);

        var referralNumber = await _numberingService.AssignReferralNumberAsync(branchId, cancellationToken);
        var sampleNumber = await _numberingService.AssignSampleNumberAsync(branchId, cancellationToken);
        var registeredAtUtc = _dateTimeProvider.UtcNow;

        var order = new Order
        {
            CustomerId = customerId,
            BranchId = branchId,
            Status = OrderStatus.Registered,
            ReferralNumber = referralNumber,
            RegisteredAtUtc = registeredAtUtc
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        var sample = new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = request.InvestigationTypeId,
            Number = sampleNumber,
            RegisteredAt = registeredAtUtc,
            Status = SampleStatus.Registered
        };

        _context.Samples.Add(sample);

        var version = await _context.TemplateVersions
            .AsNoTracking()
            .FirstAsync(item => item.Id == templateVersionId, cancellationToken);

        _context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateId = version.TemplateId,
            TemplateVersionId = version.Id,
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.Pending
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateVersionId = templateVersionId,
            ReferralNumber = referralNumber,
            SampleNumber = sampleNumber
        };
    }

    private async Task<Guid> ResolveCustomerIdAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId is Guid customerId)
        {
            var exists = await _context.Customers
                .AnyAsync(customer => customer.Id == customerId && !customer.IsAnnulled, cancellationToken);

            if (!exists)
            {
                throw new InvalidOperationException("Замовника не знайдено або запис анульовано.");
            }

            return customerId;
        }

        if (request.NewCustomer is null)
        {
            throw new InvalidOperationException("Оберіть існуючого замовника або заповніть дані нового.");
        }

        return await _customerService.CreateAsync(request.NewCustomer, cancellationToken);
    }

    private async Task<Guid> ResolveTemplateVersionIdAsync(
        Guid investigationTypeId,
        Guid? explicitTemplateVersionId,
        CancellationToken cancellationToken)
    {
        if (explicitTemplateVersionId is Guid versionId)
        {
            var isValid = await (
                    from link in _context.InvestigationTypeTemplates.AsNoTracking()
                    join template in _context.Templates.AsNoTracking() on link.TemplateId equals template.Id
                    join version in _context.TemplateVersions.AsNoTracking() on versionId equals version.Id
                    where link.InvestigationTypeId == investigationTypeId
                          && link.IsActive
                          && template.CurrentPublishedVersionId == version.Id
                          && version.DocumentFormat == TemplateDocumentFormat.Pdf
                          && version.Status == TemplateVersionStatus.Published
                          && !version.IsAnnulled
                    select version.Id)
                .AnyAsync(cancellationToken);

            if (!isValid)
            {
                throw new InvalidOperationException("Обрана PDF-версія шаблону недоступна для цього типу дослідження.");
            }

            return versionId;
        }

        var resolvedId = await (
                from link in _context.InvestigationTypeTemplates.AsNoTracking()
                join template in _context.Templates.AsNoTracking() on link.TemplateId equals template.Id
                join version in _context.TemplateVersions.AsNoTracking() on template.CurrentPublishedVersionId equals version.Id
                where link.InvestigationTypeId == investigationTypeId
                      && link.IsActive
                      && !template.IsAnnulled
                      && version.Status == TemplateVersionStatus.Published
                      && !version.IsAnnulled
                      && version.DocumentFormat == TemplateDocumentFormat.Pdf
                orderby link.SortOrder
                select version.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (resolvedId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Для обраного типу дослідження немає опублікованого PDF-шаблону. Зверніться до адміністратора.");
        }

        return resolvedId;
    }
}
