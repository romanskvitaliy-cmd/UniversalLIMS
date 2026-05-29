using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Organization;
using UniversalLIMS.Application.Organization.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Organization;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Organization;

public sealed class BranchServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesBranchWithNormalizedCode()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var branchId = await service.CreateAsync(new CreateBranchRequest
        {
            Code = " ber ",
            Name = "Бердичівський відділ",
            City = "Бердичів",
            Address = "вул. Тестова, 1"
        });

        var branch = await context.Branches.SingleAsync(item => item.Id == branchId);
        Assert.Equal("BER", branch.Code);
        Assert.Equal("Бердичівський відділ", branch.Name);
        Assert.True(branch.IsActive);
        Assert.False(branch.IsAnnulled);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateCode()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.CreateAsync(new CreateBranchRequest
        {
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир"
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateBranchRequest
            {
                Code = "zhy",
                Name = "Інша назва",
                City = "Житомир"
            }));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesNameCityAddressAndDeactivates()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "KOR",
            Name = "Стара назва",
            City = "Коростень",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.UpdateAsync(branchId, new UpdateBranchRequest
        {
            Name = "Коростенський відділ",
            City = "Коростень",
            Address = "вул. Нова, 2",
            IsActive = false
        });

        var branch = await context.Branches.SingleAsync(item => item.Id == branchId);
        Assert.Equal("Коростенський відділ", branch.Name);
        Assert.Equal("вул. Нова, 2", branch.Address);
        Assert.False(branch.IsActive);
        Assert.Equal("KOR", branch.Code);
    }

    [Fact]
    public async Task AnnulAsync_BlocksWhenWorkflowDocumentsExist()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var registrarBranchId = Guid.NewGuid();
        context.Branches.Add(new Branch
        {
            Id = registrarBranchId,
            Code = "REG",
            Name = "Реєстратура",
            City = "Житомир"
        });
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир"
        });
        await context.SaveChangesAsync();

        var customer = new Customer { FullName = "Клієнт" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var order = new Order
        {
            CustomerId = customer.Id,
            BranchId = registrarBranchId,
            ReferralNumber = "REF-1",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var sample = new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = Guid.NewGuid(),
            Number = "ZHY-2026-001",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Routed
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

        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnnulAsync(branchId, "Закриття філії"));
    }

    [Fact]
    public async Task AnnulAsync_SucceedsWhenOnlyPendingDocumentsExist()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var registrarBranchId = Guid.NewGuid();
        context.Branches.Add(new Branch
        {
            Id = registrarBranchId,
            Code = "REG",
            Name = "Реєстратура",
            City = "Житомир"
        });
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир"
        });
        await context.SaveChangesAsync();

        var customer = new Customer { FullName = "Клієнт" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var order = new Order
        {
            CustomerId = customer.Id,
            BranchId = registrarBranchId,
            ReferralNumber = "REF-2",
            Status = OrderStatus.Draft
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var sample = new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = Guid.NewGuid(),
            Number = "ZHY-2026-002",
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

        var service = CreateService(context);
        await service.AnnulAsync(branchId, "Філію об'єднано з іншою");

        var annulled = await context.Branches
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == branchId);

        Assert.True(annulled.IsAnnulled);
        Assert.Equal("Філію об'єднано з іншою", annulled.AnnulmentReason);
    }

    [Fact]
    public async Task GetListAsync_ReturnsStatistics()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var customer = new Customer { FullName = "Клієнт" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var order = new Order
        {
            CustomerId = customer.Id,
            BranchId = branchId,
            ReferralNumber = "REF-3",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var sample = new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = Guid.NewGuid(),
            Number = "ZHY-2026-003",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Routed
        };
        context.Samples.Add(sample);
        context.OrderDocuments.AddRange(
            new OrderDocument
            {
                OrderId = order.Id,
                SampleId = sample.Id,
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.SentToLab
            },
            new OrderDocument
            {
                OrderId = order.Id,
                SampleId = sample.Id,
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.Pending
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var list = await service.GetListAsync();

        var item = Assert.Single(list);
        Assert.Equal(1, item.WorkflowDocumentCount);
        Assert.Equal(1, item.PendingDocumentCount);
    }

    private static BranchService CreateService(ApplicationDbContext context) =>
        new(context, new TestCurrentUserService(), new TestDateTimeProvider(DateTime.UtcNow));

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string? UserId => "test-user";

        public string? UserName => "test-user";

        public string? UserFullName => "Test User";

        public Guid? BranchId => null;

        public string? IpAddress => "127.0.0.1";

        public string? UserAgent => "tests";

        public string? CorrelationId => "test-correlation";

        public bool IsAuthenticated => true;
    }

    private sealed class TestDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
