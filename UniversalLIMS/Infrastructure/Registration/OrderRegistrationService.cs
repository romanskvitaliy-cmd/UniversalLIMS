using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class OrderRegistrationService : IOrderRegistrationService
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INumberingService _numberingService;
    private readonly IOrderFieldValueService _orderFieldValueService;

    public OrderRegistrationService(
        ApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        INumberingService numberingService,
        IOrderFieldValueService orderFieldValueService)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _numberingService = numberingService;
        _orderFieldValueService = orderFieldValueService;
    }

    public async Task<RegisterSampleResult> RegisterSampleAsync(
        RegisterSampleRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateRegistrationRequestAsync(request, cancellationToken);

        var registeredAt = request.RegisteredAtUtc ?? _dateTimeProvider.UtcNow;
        var referralNumber = await _numberingService.AssignReferralNumberAsync(
            request.RegistrationBranchId,
            cancellationToken);
        var sampleNumber = await _numberingService.AssignSampleNumberAsync(
            request.RegistrationBranchId,
            cancellationToken);

        var order = new Order
        {
            CustomerId = request.CustomerId,
            BranchId = request.RegistrationBranchId,
            Status = OrderStatus.Registered,
            ReferralNumber = referralNumber,
            RegisteredAtUtc = registeredAt,
            Notes = request.OrderNotes?.Trim()
        };

        var sample = new Sample
        {
            Order = order,
            Number = sampleNumber,
            RegisteredAt = registeredAt,
            InvestigationTypeId = request.InvestigationTypeId,
            Status = SampleStatus.Registered,
            Notes = request.SampleNotes?.Trim()
        };

        _context.Orders.Add(order);
        _context.Samples.Add(sample);
        await _context.SaveChangesAsync(cancellationToken);

        var documentsCreated = await CreateOrderDocumentsAsync(
            order,
            sample,
            request.TargetBranchId,
            cancellationToken);

        if (request.DynamicFieldValues.Count > 0)
        {
            var values = request.DynamicFieldValues
                .Select(value => new OrderFieldValueInput
                {
                    DataFieldId = value.DataFieldId,
                    SampleId = value.SampleId ?? sample.Id,
                    StoredValue = value.StoredValue
                })
                .ToList();

            await _orderFieldValueService.UpsertAsync(order.Id, values, cancellationToken);
        }

        return new RegisterSampleResult
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            ReferralNumber = referralNumber,
            SampleNumber = sampleNumber,
            DocumentsCreated = documentsCreated
        };
    }

    public async Task<OrderDetailsResult?> GetOrderDetailsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(order => order.Id == orderId)
            .Select(order => new OrderDetailsResult
            {
                OrderId = order.Id,
                ReferralNumber = order.ReferralNumber,
                Status = order.Status,
                RegisteredAtUtc = order.RegisteredAtUtc,
                CustomerFullName = order.Customer.FullName,
                CustomerOrganizationName = order.Customer.OrganizationName,
                CustomerContactPhone = order.Customer.ContactPhone,
                RegistrationBranchName = order.Branch.Name,
                Samples = order.Samples
                    .OrderBy(sample => sample.RegisteredAt)
                    .Select(sample => new SampleDetailsResult
                    {
                        SampleId = sample.Id,
                        Number = sample.Number,
                        RegisteredAt = sample.RegisteredAt,
                        InvestigationTypeName = sample.InvestigationType.NameUk,
                        Status = sample.Status
                    })
                    .ToList(),
                Documents = order.OrderDocuments
                    .OrderBy(document => document.CreatedAtUtc)
                    .Select(document => new OrderDocumentDetailsResult
                    {
                        OrderDocumentId = document.Id,
                        TemplateCode = document.Template.Code,
                        TemplateName = document.Template.NameUk,
                        TemplateVersionNumber = document.TemplateVersion.VersionNumber,
                        TargetBranchName = document.TargetBranch.Name,
                        Status = document.Status
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RouteSampleAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        var sample = await _context.Samples
            .Include(item => item.OrderDocuments)
            .FirstOrDefaultAsync(item => item.Id == sampleId, cancellationToken);

        if (sample is null)
        {
            throw new InvalidOperationException("Пробу не знайдено.");
        }

        if (sample.Status != SampleStatus.Registered)
        {
            throw new InvalidOperationException("Маршрутизація доступна лише для проби у статусі «Зареєстровано».");
        }

        var timestampUtc = _dateTimeProvider.UtcNow;
        sample.Status = SampleStatus.Routed;
        sample.RoutedAtUtc = timestampUtc;

        foreach (var document in sample.OrderDocuments.Where(document => document.Status == OrderDocumentStatus.Pending))
        {
            document.Status = OrderDocumentStatus.SentToLab;
            document.SentToLabAtUtc = timestampUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateRegistrationRequestAsync(
        RegisterSampleRequest request,
        CancellationToken cancellationToken)
    {
        var customerExists = await _context.Customers
            .AnyAsync(customer => customer.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
        {
            throw new InvalidOperationException("Замовника не знайдено.");
        }

        var investigationTypeExists = await _context.InvestigationTypes
            .AnyAsync(investigationType => investigationType.Id == request.InvestigationTypeId && investigationType.IsActive, cancellationToken);

        if (!investigationTypeExists)
        {
            throw new InvalidOperationException("Тип дослідження не знайдено або неактивний.");
        }

        await EnsureBranchExistsAsync(request.RegistrationBranchId, "Філію реєстрації не знайдено.", cancellationToken);
        await EnsureBranchExistsAsync(request.TargetBranchId, "Цільову філію не знайдено.", cancellationToken);
    }

    private async Task EnsureBranchExistsAsync(
        Guid branchId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var exists = await _context.Branches.AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private async Task<int> CreateOrderDocumentsAsync(
        Order order,
        Sample sample,
        Guid targetBranchId,
        CancellationToken cancellationToken)
    {
        var templateLinks = await _context.InvestigationTypeTemplates
            .Where(link => link.InvestigationTypeId == sample.InvestigationTypeId && link.IsActive)
            .OrderBy(link => link.SortOrder)
            .Select(link => new
            {
                link.TemplateId,
                link.Template.CurrentPublishedVersionId
            })
            .ToListAsync(cancellationToken);

        var documentsCreated = 0;

        foreach (var link in templateLinks)
        {
            if (link.CurrentPublishedVersionId is null)
            {
                continue;
            }

            var publishedVersion = await _context.TemplateVersions
                .FirstOrDefaultAsync(
                    version => version.Id == link.CurrentPublishedVersionId &&
                               version.Status == TemplateVersionStatus.Published,
                    cancellationToken);

            if (publishedVersion is null)
            {
                continue;
            }

            _context.OrderDocuments.Add(new OrderDocument
            {
                OrderId = order.Id,
                SampleId = sample.Id,
                TemplateId = link.TemplateId,
                TemplateVersionId = publishedVersion.Id,
                TargetBranchId = targetBranchId,
                Status = OrderDocumentStatus.Pending
            });

            documentsCreated++;
        }

        if (documentsCreated > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return documentsCreated;
    }
}
