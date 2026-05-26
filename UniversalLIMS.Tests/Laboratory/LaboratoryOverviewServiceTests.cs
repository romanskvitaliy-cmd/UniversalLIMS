using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Laboratory;

public sealed class LaboratoryOverviewServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_ReturnsPerBranchSampleCounts()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchA, branchB);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт огляду");

        await SeedSampleWithDocumentAsync(
            context,
            customer.Id,
            branchA,
            investigationTypeId,
            "SMP-A-001",
            SampleStatus.InProgress,
            branchA);
        await SeedSampleWithDocumentAsync(
            context,
            customer.Id,
            branchB,
            investigationTypeId,
            "SMP-B-001",
            SampleStatus.Routed,
            branchB);
        await SeedSampleWithDocumentAsync(
            context,
            customer.Id,
            branchB,
            investigationTypeId,
            "SMP-B-002",
            SampleStatus.ResultsEntered,
            branchB);

        var service = CreateService(context, activeBranchId: branchB);
        var overview = await service.GetOverviewAsync();

        Assert.Equal(3, overview.TotalActiveSampleCount);
        Assert.Equal(1, overview.TotalInProgressSampleCount);
        Assert.Equal(1, overview.TotalResultsEnteredSampleCount);
        Assert.Equal(branchB, overview.ActiveBranchId);

        var branchAOverview = Assert.Single(overview.Branches, branch => branch.BranchId == branchA);
        Assert.Equal(1, branchAOverview.ActiveSampleCount);
        Assert.Equal(1, branchAOverview.InProgressSampleCount);

        var branchBOverview = Assert.Single(overview.Branches, branch => branch.BranchId == branchB);
        Assert.Equal(2, branchBOverview.ActiveSampleCount);
        Assert.Equal(1, branchBOverview.ResultsEnteredSampleCount);
    }

    [Fact]
    public async Task GetOverviewAsync_ExcludesPendingDocuments()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт pending");

        var order = new Order
        {
            CustomerId = customer.Id,
            BranchId = branchId,
            ReferralNumber = "REF-PENDING",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var sample = new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = investigationTypeId,
            Number = "SMP-PENDING",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Registered
        };
        context.Samples.Add(sample);
        context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, activeBranchId: null);
        var overview = await service.GetOverviewAsync();

        Assert.Equal(0, overview.TotalActiveSampleCount);
        Assert.Equal(0, overview.Branches[0].ActiveSampleCount);
    }

    private static LaboratoryOverviewService CreateService(
        ApplicationDbContext context,
        Guid? activeBranchId) =>
        new(context, new FixedLaboratoryBranchContext(activeBranchId));

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

    private static async Task SeedSampleWithDocumentAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid orderBranchId,
        Guid investigationTypeId,
        string sampleNumber,
        SampleStatus status,
        Guid targetBranchId)
    {
        var order = new Order
        {
            CustomerId = customerId,
            BranchId = orderBranchId,
            ReferralNumber = $"REF-{sampleNumber}",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var sample = new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = investigationTypeId,
            Number = sampleNumber,
            RegisteredAt = DateTime.UtcNow,
            Status = status
        };
        context.Samples.Add(sample);
        context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = targetBranchId,
            Status = OrderDocumentStatus.SentToLab
        });
        await context.SaveChangesAsync();
    }

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
