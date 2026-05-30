using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Laboratory;

public sealed class LaboratoryJournalServiceTests
{
    [Fact]
    public async Task GetSamplesAsync_FiltersByCurrentUserBranch()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchA, branchB);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Іваненко Петро");
        await SeedSampleAsync(context, customer.Id, branchA, investigationTypeId, "SMP-A-00001");
        await SeedSampleAsync(context, customer.Id, branchB, investigationTypeId, "SMP-B-00001");

        var service = CreateService(context, branchA);

        var result = await service.GetSamplesAsync(new SampleJournalFilter());

        Assert.Single(result.Items);
        Assert.Equal("SMP-A-00001", result.Items[0].SampleNumber);
    }

    [Fact]
    public async Task GetSamplesAsync_AdminAllLabs_ReturnsSamplesFromAllTargetBranches()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchA, branchB);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Адмін Усі");
        await SeedSampleAsync(context, customer.Id, branchA, investigationTypeId, "SMP-A-00001");
        await SeedSampleAsync(context, customer.Id, branchB, investigationTypeId, "SMP-B-00001");

        var service = CreateService(context, branchId: null);

        var result = await service.GetSamplesAsync(new SampleJournalFilter { PageSize = 50 });

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item.SampleNumber == "SMP-A-00001");
        Assert.Contains(result.Items, item => item.SampleNumber == "SMP-B-00001");
    }

    [Fact]
    public async Task GetSamplesAsync_UsesCustomerFullName_NotOrderFieldValue()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Коваль Олена");
        var orderId = await SeedOrderWithSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "REF-SSOT",
            "SMP-SSOT-00001");

        var service = CreateService(context, branchId);

        var result = await service.GetSamplesAsync(new SampleJournalFilter { SampleNumber = "SSOT" });

        Assert.Single(result.Items);
        Assert.Equal("Коваль Олена", result.Items[0].CustomerFullName);
        Assert.Equal("SMP-SSOT-00001", result.Items[0].SampleNumber);
        Assert.Equal(orderId, result.Items[0].OrderId);
        Assert.DoesNotContain(
            context.OrderFieldValues,
            fieldValue => fieldValue.OrderId == orderId);
    }

    [Fact]
    public async Task GetSamplesAsync_ExcludesAnnulledOrdersAndCustomers()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var activeCustomer = await SeedCustomerAsync(context, "Активний Клієнт");
        await SeedSampleAsync(context, activeCustomer.Id, branchId, investigationTypeId, "SMP-ACTIVE");

        var annulledCustomer = new Customer
        {
            FullName = "Анульований Клієнт",
            IsAnnulled = true
        };
        context.Customers.Add(annulledCustomer);
        await context.SaveChangesAsync();

        await SeedSampleAsync(context, activeCustomer.Id, branchId, investigationTypeId, "SMP-ANNULLED-ORDER", isOrderAnnulled: true);
        await SeedSampleAsync(context, annulledCustomer.Id, branchId, investigationTypeId, "SMP-ANNULLED-CUSTOMER");

        var service = CreateService(context, branchId);

        var result = await service.GetSamplesAsync(new SampleJournalFilter { PageSize = 50 });

        Assert.Single(result.Items);
        Assert.Equal("SMP-ACTIVE", result.Items[0].SampleNumber);
    }

    [Fact]
    public async Task GetSamplesAsync_ExcludesAnnulledSamples()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Тест");
        await SeedSampleAsync(context, customer.Id, branchId, investigationTypeId, "SMP-ACTIVE");

        var annulledOrderId = await SeedOrderWithSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "REF-ANN",
            "SMP-ANNULLED-SAMPLE");
        var annulledSample = await context.Samples.SingleAsync(sample => sample.OrderId == annulledOrderId);
        annulledSample.IsAnnulled = true;
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);

        var result = await service.GetSamplesAsync(new SampleJournalFilter { PageSize = 50 });

        Assert.Single(result.Items);
        Assert.Equal("SMP-ACTIVE", result.Items[0].SampleNumber);
    }

    [Fact]
    public async Task GetSamplesAsync_FiltersByRegisteredDateRange()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Тест");

        var orderEarly = new Order
        {
            CustomerId = customer.Id,
            BranchId = branchId,
            ReferralNumber = "REF-EARLY",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(orderEarly);
        await context.SaveChangesAsync();
        var sampleEarly = new Sample
        {
            OrderId = orderEarly.Id,
            InvestigationTypeId = investigationTypeId,
            Number = "SMP-EARLY",
            RegisteredAt = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            Status = SampleStatus.Registered
        };
        context.Samples.Add(sampleEarly);
        context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = orderEarly.Id,
            SampleId = sampleEarly.Id,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.SentToLab
        });

        var orderLate = new Order
        {
            CustomerId = customer.Id,
            BranchId = branchId,
            ReferralNumber = "REF-LATE",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(orderLate);
        await context.SaveChangesAsync();
        var sampleLate = new Sample
        {
            OrderId = orderLate.Id,
            InvestigationTypeId = investigationTypeId,
            Number = "SMP-LATE",
            RegisteredAt = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc),
            Status = SampleStatus.Registered
        };
        context.Samples.Add(sampleLate);
        context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = orderLate.Id,
            SampleId = sampleLate.Id,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.SentToLab
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);

        var result = await service.GetSamplesAsync(new SampleJournalFilter
        {
            DateFrom = new DateTime(2026, 5, 20),
            DateTo = new DateTime(2026, 5, 31),
            PageSize = 50
        });

        Assert.Single(result.Items);
        Assert.Equal("SMP-LATE", result.Items[0].SampleNumber);
    }

    [Fact]
    public async Task GetSamplesAsync_FiltersBySampleNumberAndStatus()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Тест");
        await SeedSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "LAB-2025-00001",
            SampleStatus.Registered);
        await SeedSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "LAB-2025-00002",
            SampleStatus.InProgress);

        var service = CreateService(context, branchId);

        var byNumber = await service.GetSamplesAsync(new SampleJournalFilter { SampleNumber = "00001" });
        Assert.Single(byNumber.Items);
        Assert.Equal("LAB-2025-00001", byNumber.Items[0].SampleNumber);

        var byStatus = await service.GetSamplesAsync(new SampleJournalFilter
        {
            Status = SampleStatus.InProgress,
            PageSize = 50
        });
        Assert.Single(byStatus.Items);
        Assert.Equal(SampleStatus.InProgress, byStatus.Items[0].Status);
    }

    [Fact]
    public async Task GetIncomingSinceAsync_ReturnsSamplesRoutedAfterTimestamp()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Нова Проба");
        var orderId = await SeedOrderWithSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "REF-NEW",
            "SMP-NEW-001",
            SampleStatus.Routed);

        var sample = await context.Samples.SingleAsync(sample => sample.OrderId == orderId);
        sample.RoutedAtUtc = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);
        var since = new DateTime(2026, 5, 30, 11, 0, 0, DateTimeKind.Utc);

        var result = await service.GetIncomingSinceAsync(since);

        Assert.Single(result);
        Assert.Equal("SMP-NEW-001", result[0].SampleNumber);
        Assert.Equal("Нова Проба", result[0].CustomerFullName);
    }

    [Fact]
    public async Task GetIncomingSinceAsync_RespectsLaboratoryBranchContext()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchA, branchB);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Фільтр філії");

        var orderA = await SeedRoutedSampleAsync(context, customer.Id, branchA, investigationTypeId, "SMP-A-ROUTE");
        var orderB = await SeedRoutedSampleAsync(context, customer.Id, branchB, investigationTypeId, "SMP-B-ROUTE");
        _ = orderA;
        _ = orderB;

        var service = CreateService(context, branchA);
        var since = new DateTime(2026, 5, 29, 0, 0, 0, DateTimeKind.Utc);

        var result = await service.GetIncomingSinceAsync(since);

        Assert.Single(result);
        Assert.Equal("SMP-A-ROUTE", result[0].SampleNumber);
    }

    [Fact]
    public async Task GetReworkSinceAsync_ReturnsSampleReturnedAfterTimestamp()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Rework Клієнт");
        var orderId = await SeedOrderWithSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "REF-RW",
            "SMP-RW-001",
            SampleStatus.InProgress);

        var sample = await context.Samples.SingleAsync(item => item.OrderId == orderId);
        var returnedAt = new DateTime(2026, 5, 30, 14, 0, 0, DateTimeKind.Utc);
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sample.Id,
            Status = ExpertConclusionStatus.ReturnedForRework,
            ReturnedForReworkAtUtc = returnedAt,
            ReworkReasonUk = "Виправити формулювання"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);
        var result = await service.GetReworkSinceAsync(returnedAt.AddMinutes(-5));

        Assert.Single(result);
        Assert.Equal("SMP-RW-001", result[0].SampleNumber);
        Assert.Equal("Виправити формулювання", result[0].ReworkReasonUk);
    }

    [Fact]
    public async Task GetReworkSinceAsync_RespectsLaboratoryBranchContext()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchA, branchB);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Rework філія");
        var returnedAt = new DateTime(2026, 5, 30, 15, 0, 0, DateTimeKind.Utc);

        var orderA = await SeedReworkSampleAsync(context, customer.Id, branchA, investigationTypeId, "SMP-A-RW", returnedAt);
        var orderB = await SeedReworkSampleAsync(context, customer.Id, branchB, investigationTypeId, "SMP-B-RW", returnedAt);
        _ = orderA;
        _ = orderB;

        var service = CreateService(context, branchA);
        var result = await service.GetReworkSinceAsync(returnedAt.AddMinutes(-5));

        Assert.Single(result);
        Assert.Equal("SMP-A-RW", result[0].SampleNumber);
    }

    [Fact]
    public async Task GetReworkSinceAsync_ExcludesReturnsBeforeTimestamp()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Старе rework");
        await SeedReworkSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "SMP-OLD-RW",
            new DateTime(2026, 5, 29, 10, 0, 0, DateTimeKind.Utc));

        var service = CreateService(context, branchId);
        var result = await service.GetReworkSinceAsync(new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(result);
    }

    private static async Task<Guid> SeedReworkSampleAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid branchId,
        Guid investigationTypeId,
        string sampleNumber,
        DateTime returnedAtUtc)
    {
        var orderId = await SeedOrderWithSampleAsync(
            context,
            customerId,
            branchId,
            investigationTypeId,
            $"REF-{sampleNumber}",
            sampleNumber,
            SampleStatus.InProgress);

        var sample = await context.Samples.SingleAsync(item => item.OrderId == orderId);
        var document = await context.OrderDocuments.SingleAsync(item => item.SampleId == sample.Id);
        document.Status = OrderDocumentStatus.InProgress;

        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sample.Id,
            Status = ExpertConclusionStatus.ReturnedForRework,
            ReturnedForReworkAtUtc = returnedAtUtc,
            ReworkReasonUk = "Тестова причина"
        });
        await context.SaveChangesAsync();
        return orderId;
    }

    private static async Task<Guid> SeedRoutedSampleAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid branchId,
        Guid investigationTypeId,
        string sampleNumber)
    {
        var orderId = await SeedOrderWithSampleAsync(
            context,
            customerId,
            branchId,
            investigationTypeId,
            $"REF-{sampleNumber}",
            sampleNumber,
            SampleStatus.Routed);

        var sample = await context.Samples.SingleAsync(item => item.OrderId == orderId);
        sample.RoutedAtUtc = new DateTime(2026, 5, 30, 10, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();
        return orderId;
    }

    private static async Task SeedBranchesAsync(ApplicationDbContext context, params Guid[] branchIds)
    {
        foreach (var branchId in branchIds)
        {
            context.Branches.Add(new Branch
            {
                Id = branchId,
                Code = branchId.ToString("N")[..6],
                Name = $"Філія {branchId.ToString("N")[..4]}",
                City = "Житомир"
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedInvestigationTypeAsync(ApplicationDbContext context)
    {
        var investigationType = new InvestigationType
        {
            Code = "LAB",
            NameUk = "Мікробіологія",
            SortOrder = 1
        };
        context.InvestigationTypes.Add(investigationType);
        await context.SaveChangesAsync();
        return investigationType.Id;
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext context, string fullName)
    {
        var customer = new Customer { FullName = fullName };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    private static async Task<Guid> SeedOrderWithSampleAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid branchId,
        Guid investigationTypeId,
        string referralNumber,
        string sampleNumber,
        SampleStatus status = SampleStatus.Registered,
        bool isOrderAnnulled = false)
    {
        var order = new Order
        {
            CustomerId = customerId,
            BranchId = branchId,
            ReferralNumber = referralNumber,
            Status = OrderStatus.Registered,
            IsAnnulled = isOrderAnnulled
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var sample = new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = investigationTypeId,
            Number = sampleNumber,
            RegisteredAt = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc),
            Status = status
        };
        context.Samples.Add(sample);
        context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.SentToLab
        });
        await context.SaveChangesAsync();
        return order.Id;
    }

    private static async Task SeedSampleAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid branchId,
        Guid investigationTypeId,
        string sampleNumber,
        SampleStatus status = SampleStatus.Registered,
        bool isOrderAnnulled = false) =>
        await SeedOrderWithSampleAsync(
            context,
            customerId,
            branchId,
            investigationTypeId,
            $"REF-{sampleNumber}",
            sampleNumber,
            status,
            isOrderAnnulled);

    private static LaboratoryJournalService CreateService(
        ApplicationDbContext context,
        Guid? branchId) =>
        new(context, new FixedLaboratoryBranchContext(branchId));

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FixedLaboratoryBranchContext : ILaboratoryBranchContext
    {
        private readonly Guid? _branchId;

        public FixedLaboratoryBranchContext(Guid? branchId) => _branchId = branchId;

        public Task<LaboratoryBranchContextState> GetStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LaboratoryBranchContextState { ActiveBranchId = _branchId });

        public Task SetSelectedBranchAsync(Guid? branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
