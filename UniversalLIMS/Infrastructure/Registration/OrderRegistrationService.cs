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

        var pageOrders = await ordersQuery
            .OrderByDescending(order => order.RegisteredAtUtc ?? order.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new
            {
                order.Id,
                order.ReferralNumber,
                CustomerFullName = order.Customer.FullName,
                OrderDate = order.RegisteredAtUtc ?? order.CreatedAtUtc,
                order.Status,
                SampleCount = order.Samples.Count(sample => !sample.IsAnnulled),
                TargetBranchName = order.Branch.Name,
                Documents = order.OrderDocuments
                    .Where(document => !document.IsAnnulled)
                    .Select(document => new
                    {
                        document.TemplateVersionId,
                        document.Status,
                        BranchName = document.TargetBranch.Name
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var items = pageOrders.Select(order =>
        {
            var docStatuses = order.Documents.Select(document => document.Status).ToList();
            var branchNames = order.Documents
                .Select(document => document.BranchName)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return new OrderListItemDto
            {
                OrderId = order.Id,
                ReferralNumber = order.ReferralNumber,
                CustomerFullName = order.CustomerFullName,
                OrderDate = order.OrderDate,
                Status = order.Status,
                SampleCount = order.SampleCount,
                TargetBranchName = branchNames.Count switch
                {
                    0 => order.TargetBranchName,
                    1 => branchNames[0],
                    _ => string.Join(", ", branchNames)
                },
                PrimaryTemplateVersionId = order.Documents
                    .Select(document => (Guid?)document.TemplateVersionId)
                    .FirstOrDefault(),
                DocumentCount = order.Documents.Count,
                WorkflowSummaryUk = OrderDocumentStatusDisplay.SummarizeWorkflow(docStatuses)
            };
        }).ToList();

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
        var availableTemplateOptions = await GetAvailableOrderTemplateOptionsAsync(cancellationToken);

        var investigationTypeDtos = availableTemplateOptions
            .Select(option => new InvestigationTypeOptionDto
            {
                Id = option.InvestigationTypeId,
                TemplateVersionId = option.TemplateVersionId,
                Code = option.InvestigationTypeCode,
                NameUk = option.TemplateNameUk
            })
            .ToList();

        var templateDtos = availableTemplateOptions
            .Select(option => new OrderTemplateOptionDto
            {
                InvestigationTypeId = option.InvestigationTypeId,
                TemplateVersionId = option.TemplateVersionId,
                TemplateNameUk = option.TemplateNameUk,
                VersionNumber = option.VersionNumber,
                IsDefault = true
            })
            .ToList();

        var branches = await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.IsActive)
            .OrderBy(branch => branch.Code)
            .Select(branch => new BranchOptionDto
            {
                Id = branch.Id,
                Code = branch.Code,
                Name = branch.Name
            })
            .ToListAsync(cancellationToken);

        return new OrderCreateFormDto
        {
            InvestigationTypes = investigationTypeDtos,
            TemplateOptions = templateDtos,
            Branches = branches,
            DefaultBranchId = _currentUser.BranchId
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

        var documentPlans = await ResolveDocumentPlansAsync(
            request.InvestigationTypeId,
            request.Documents,
            request.TemplateVersionId,
            branchId,
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
        await _context.SaveChangesAsync(cancellationToken);

        var createdDocuments = new List<CreatedOrderDocumentDto>();

        foreach (var plan in documentPlans)
        {
            var version = await _context.TemplateVersions
                .AsNoTracking()
                .FirstAsync(item => item.Id == plan.TemplateVersionId, cancellationToken);

            var orderDocument = new OrderDocument
            {
                OrderId = order.Id,
                SampleId = sample.Id,
                TemplateId = version.TemplateId,
                TemplateVersionId = version.Id,
                TargetBranchId = plan.TargetBranchId,
                Status = OrderDocumentStatus.Pending
            };

            _context.OrderDocuments.Add(orderDocument);
            await _context.SaveChangesAsync(cancellationToken);

            createdDocuments.Add(new CreatedOrderDocumentDto
            {
                OrderDocumentId = orderDocument.Id,
                TemplateVersionId = version.Id,
                TargetBranchId = plan.TargetBranchId
            });
        }

        return new CreateOrderResult
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateVersionId = documentPlans[0].TemplateVersionId,
            ReferralNumber = referralNumber,
            SampleNumber = sampleNumber,
            Documents = createdDocuments
        };
    }

    public async Task<OrderDetailDto?> GetOrderDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var orderQuery = _context.Orders
            .AsNoTracking()
            .Where(order => order.Id == orderId && !order.IsAnnulled);

        if (_currentUser.BranchId is Guid branchId)
        {
            orderQuery = orderQuery.Where(order => order.BranchId == branchId);
        }

        var order = await orderQuery
            .Select(item => new
            {
                item.Id,
                item.ReferralNumber,
                CustomerFullName = item.Customer.FullName,
                item.Status,
                OrderDate = item.RegisteredAtUtc ?? item.CreatedAtUtc,
                Sample = item.Samples
                    .Where(sample => !sample.IsAnnulled)
                    .OrderBy(sample => sample.CreatedAtUtc)
                    .Select(sample => new
                    {
                        sample.Id,
                        sample.Number,
                        InvestigationTypeNameUk = sample.InvestigationType.NameUk
                    })
                    .FirstOrDefault(),
                Documents = item.OrderDocuments
                    .Where(document => !document.IsAnnulled)
                    .OrderBy(document => document.CreatedAtUtc)
                    .Select(document => new
                    {
                        document.Id,
                        document.TemplateVersionId,
                        TemplateNameUk = document.TemplateVersion.Template.NameUk,
                        document.TemplateVersion.VersionNumber,
                        document.TargetBranchId,
                        TargetBranchName = document.TargetBranch.Name,
                        document.Status
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null || order.Sample is null)
        {
            return null;
        }

        var documentDtos = order.Documents
            .Select(document => new OrderDocumentItemDto
            {
                OrderDocumentId = document.Id,
                TemplateVersionId = document.TemplateVersionId,
                TemplateNameUk = document.TemplateNameUk,
                VersionNumber = document.VersionNumber,
                TargetBranchId = document.TargetBranchId,
                TargetBranchName = document.TargetBranchName,
                Status = document.Status,
                CanFill = document.Status == OrderDocumentStatus.Pending,
                CanSendToLab = document.Status == OrderDocumentStatus.Pending
            })
            .ToList();

        return new OrderDetailDto
        {
            OrderId = order.Id,
            ReferralNumber = order.ReferralNumber,
            CustomerFullName = order.CustomerFullName,
            Status = order.Status,
            OrderDate = order.OrderDate,
            SampleId = order.Sample.Id,
            SampleNumber = order.Sample.Number,
            InvestigationTypeNameUk = order.Sample.InvestigationTypeNameUk,
            WorkflowSummaryUk = OrderDocumentStatusDisplay.SummarizeWorkflow(
                documentDtos.Select(document => document.Status).ToList()),
            Documents = documentDtos
        };
    }

    public async Task SendDocumentsToLabAsync(
        SendOrderDocumentsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OrderDocumentIds.Count == 0)
        {
            throw new InvalidOperationException("Оберіть хоча б один документ для відправки.");
        }

        var order = await LoadOrderForMutationAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("Замовлення не знайдено.");

        var documentIds = request.OrderDocumentIds.Distinct().ToList();
        var documents = order.OrderDocuments
            .Where(document => !document.IsAnnulled && documentIds.Contains(document.Id))
            .ToList();

        if (documents.Count != documentIds.Count)
        {
            throw new InvalidOperationException("Деякі документи не належать цьому замовленню.");
        }

        if (documents.Any(document => document.Status != OrderDocumentStatus.Pending))
        {
            throw new InvalidOperationException("Відправити можна лише документи зі статусом «Очікує».");
        }

        var sentAtUtc = _dateTimeProvider.UtcNow;

        foreach (var document in documents)
        {
            document.Status = OrderDocumentStatus.SentToLab;
            document.SentToLabAtUtc = sentAtUtc;
        }

        var sample = order.Samples.FirstOrDefault(item => !item.IsAnnulled);
        if (sample is not null && sample.Status == SampleStatus.Registered)
        {
            sample.Status = SampleStatus.Routed;
            sample.RoutedAtUtc = sentAtUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDocumentRoutingAsync(
        UpdateOrderDocumentRoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        var branchExists = await _context.Branches
            .AnyAsync(branch => branch.Id == request.TargetBranchId && branch.IsActive, cancellationToken);

        if (!branchExists)
        {
            throw new InvalidOperationException("Оберіть активну філію призначення.");
        }

        var order = await LoadOrderForMutationAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("Замовлення не знайдено.");

        var document = order.OrderDocuments
            .FirstOrDefault(item => item.Id == request.OrderDocumentId && !item.IsAnnulled)
            ?? throw new InvalidOperationException("Документ не знайдено.");

        if (document.Status != OrderDocumentStatus.Pending)
        {
            throw new InvalidOperationException("Змінити маршрут можна лише до відправки в лабораторію.");
        }

        document.TargetBranchId = request.TargetBranchId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Order?> LoadOrderForMutationAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var query = _context.Orders
            .Include(order => order.Samples)
            .Include(order => order.OrderDocuments)
            .Where(order => order.Id == orderId && !order.IsAnnulled);

        if (_currentUser.BranchId is Guid branchId)
        {
            query = query.Where(order => order.BranchId == branchId);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private sealed record OrderDocumentPlan(Guid TemplateVersionId, Guid TargetBranchId);

    private async Task<List<OrderDocumentPlan>> ResolveDocumentPlansAsync(
        Guid investigationTypeId,
        IReadOnlyList<CreateOrderDocumentRequest> documents,
        Guid? legacyTemplateVersionId,
        Guid defaultBranchId,
        CancellationToken cancellationToken)
    {
        var explicitDocuments = documents
            .Where(document => document.TemplateVersionId != Guid.Empty)
            .ToList();

        if (explicitDocuments.Count == 0)
        {
            var singleVersionId = await ResolveTemplateVersionIdAsync(
                investigationTypeId,
                legacyTemplateVersionId,
                cancellationToken);

            return
            [
                new OrderDocumentPlan(singleVersionId, defaultBranchId)
            ];
        }

        if (explicitDocuments.Select(document => document.TemplateVersionId).Distinct().Count()
            != explicitDocuments.Count)
        {
            throw new InvalidOperationException("Кожен PDF-шаблон можна додати до справи лише один раз.");
        }

        var branchIds = explicitDocuments
            .Select(document => document.TargetBranchId != Guid.Empty
                ? document.TargetBranchId
                : defaultBranchId)
            .Distinct()
            .ToList();
        var activeBranchIds = await _context.Branches
            .AsNoTracking()
            .Where(branch => branchIds.Contains(branch.Id) && branch.IsActive)
            .Select(branch => branch.Id)
            .ToListAsync(cancellationToken);

        if (activeBranchIds.Count != branchIds.Count)
        {
            throw new InvalidOperationException("Оберіть активні філії для всіх документів.");
        }

        var plans = new List<OrderDocumentPlan>();

        foreach (var document in explicitDocuments)
        {
            var versionId = await ResolveTemplateVersionIdAsync(
                investigationTypeId,
                document.TemplateVersionId,
                cancellationToken);

            var targetBranchId = document.TargetBranchId != Guid.Empty
                ? document.TargetBranchId
                : defaultBranchId;

            plans.Add(new OrderDocumentPlan(versionId, targetBranchId));
        }

        return plans;
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
        var availableTemplateOptions = await GetAvailableOrderTemplateOptionsAsync(cancellationToken);
        var matchingOptions = availableTemplateOptions
            .Where(option => option.InvestigationTypeId == investigationTypeId)
            .ToList();

        if (explicitTemplateVersionId is Guid versionId)
        {
            if (matchingOptions.All(option => option.TemplateVersionId != versionId))
            {
                throw new InvalidOperationException("Обрана PDF-версія шаблону недоступна для цього типу дослідження.");
            }

            return versionId;
        }

        var resolvedId = matchingOptions
            .Select(option => option.TemplateVersionId)
            .FirstOrDefault();

        if (resolvedId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Для обраного типу дослідження немає опублікованого PDF-шаблону. Зверніться до адміністратора.");
        }

        return resolvedId;
    }

    private async Task<List<AvailableOrderTemplateOption>> GetAvailableOrderTemplateOptionsAsync(
        CancellationToken cancellationToken)
    {
        var investigationTypes = await _context.InvestigationTypes
            .AsNoTracking()
            .Where(type => type.IsActive && !type.IsAnnulled)
            .OrderBy(type => type.SortOrder)
            .Select(type => new ActiveInvestigationType(
                type.Id,
                type.Code,
                type.NameUk,
                type.SortOrder))
            .ToListAsync(cancellationToken);

        if (investigationTypes.Count == 0)
        {
            return [];
        }

        var activePublishedTemplates = await (
                from template in _context.Templates.AsNoTracking()
                join version in _context.TemplateVersions.AsNoTracking()
                    on template.CurrentPublishedVersionId equals version.Id
                where template.Status == TemplateStatus.Active
                      && !template.IsAnnulled
                      && version.Status == TemplateVersionStatus.Published
                      && !version.IsAnnulled
                      && version.DocumentFormat == TemplateDocumentFormat.Pdf
                select new ActivePublishedTemplate(
                    template.Id,
                    version.Id,
                    template.Code,
                    template.NameUk,
                    version.VersionNumber))
            .ToListAsync(cancellationToken);

        if (activePublishedTemplates.Count == 0)
        {
            return [];
        }

        var templateIds = activePublishedTemplates
            .Select(template => template.TemplateId)
            .ToList();
        var activeLinks = await _context.InvestigationTypeTemplates
            .AsNoTracking()
            .Where(link => link.IsActive && templateIds.Contains(link.TemplateId))
            .Select(link => new ActiveTemplateLink(
                link.TemplateId,
                link.InvestigationTypeId,
                link.SortOrder))
            .ToListAsync(cancellationToken);
        var linksByTemplateId = activeLinks
            .GroupBy(link => link.TemplateId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var options = new List<AvailableOrderTemplateOption>();
        foreach (var template in activePublishedTemplates)
        {
            var templateLinks = linksByTemplateId.GetValueOrDefault(template.TemplateId) ?? [];
            var linkedTypeIds = templateLinks
                .Select(link => link.InvestigationTypeId)
                .ToHashSet();
            var investigationType = ResolveBestInvestigationType(
                template.NameUk,
                investigationTypes,
                linkedTypeIds);

            if (investigationType is null)
            {
                continue;
            }

            var linkSortOrder = templateLinks
                .Where(link => link.InvestigationTypeId == investigationType.Id)
                .Select(link => (int?)link.SortOrder)
                .FirstOrDefault();

            options.Add(new AvailableOrderTemplateOption(
                investigationType.Id,
                investigationType.Code,
                investigationType.SortOrder,
                template.TemplateVersionId,
                template.TemplateCode,
                template.NameUk,
                template.VersionNumber,
                linkSortOrder ?? int.MaxValue));
        }

        return options
            .OrderBy(option => option.InvestigationTypeSortOrder)
            .ThenBy(option => option.LinkSortOrder)
            .ThenBy(option => option.TemplateCode)
            .ThenBy(option => option.TemplateNameUk)
            .ToList();
    }

    private static ActiveInvestigationType? ResolveBestInvestigationType(
        string templateNameUk,
        IReadOnlyList<ActiveInvestigationType> investigationTypes,
        IReadOnlySet<Guid> linkedTypeIds)
    {
        var scoredTypes = investigationTypes
            .Select(type => new
            {
                Type = type,
                Score = GetTemplateTypeMatchScore(templateNameUk, type.Code, type.NameUk)
            })
            .Where(item => item.Score > 0)
            .ToList();

        if (scoredTypes.Count > 0)
        {
            var bestScore = scoredTypes.Max(item => item.Score);
            return scoredTypes
                .Where(item => item.Score == bestScore)
                .OrderByDescending(item => linkedTypeIds.Contains(item.Type.Id))
                .ThenBy(item => item.Type.SortOrder)
                .Select(item => item.Type)
                .First();
        }

        var linkedTypes = investigationTypes
            .Where(type => linkedTypeIds.Contains(type.Id))
            .OrderBy(type => type.SortOrder)
            .ToList();
        if (linkedTypes.Count == 1)
        {
            return linkedTypes[0];
        }

        return investigationTypes.Count == 1
            ? investigationTypes[0]
            : null;
    }

    private static int GetTemplateTypeMatchScore(
        string templateNameUk,
        string investigationTypeCode,
        string investigationTypeNameUk)
    {
        var templateName = NormalizeForMatch(templateNameUk);
        var typeName = NormalizeForMatch(investigationTypeNameUk);

        if (templateName == typeName)
        {
            return 100;
        }

        if (templateName.Contains(typeName, StringComparison.Ordinal)
            || typeName.Contains(templateName, StringComparison.Ordinal))
        {
            return 80;
        }

        var score = investigationTypeCode.ToUpperInvariant() switch
        {
            "WATER" when templateName.Contains("вод", StringComparison.Ordinal) => 60,
            "FOOD" when templateName.Contains("харч", StringComparison.Ordinal)
                || templateName.Contains("страв", StringComparison.Ordinal)
                || templateName.Contains("напівфабрикат", StringComparison.Ordinal) => 60,
            "INDOOR_AIR" when templateName.Contains("повітр", StringComparison.Ordinal) => 60,
            _ => 0
        };

        var typeTokens = typeName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 3)
            .ToHashSet(StringComparer.Ordinal);
        var commonTokenCount = templateName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(token => typeTokens.Contains(token));

        return score + commonTokenCount;
    }

    private static string NormalizeForMatch(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private sealed record ActiveInvestigationType(
        Guid Id,
        string Code,
        string NameUk,
        int SortOrder);

    private sealed record ActivePublishedTemplate(
        Guid TemplateId,
        Guid TemplateVersionId,
        string TemplateCode,
        string NameUk,
        int VersionNumber);

    private sealed record ActiveTemplateLink(
        Guid TemplateId,
        Guid InvestigationTypeId,
        int SortOrder);

    private sealed record AvailableOrderTemplateOption(
        Guid InvestigationTypeId,
        string InvestigationTypeCode,
        int InvestigationTypeSortOrder,
        Guid TemplateVersionId,
        string TemplateCode,
        string TemplateNameUk,
        int VersionNumber,
        int LinkSortOrder);
}
