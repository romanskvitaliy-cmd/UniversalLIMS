using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class NumberingServiceTests
{
    [Fact]
    public async Task AssignSampleNumberAsync_InSameUnitOfWork_AssignsDistinctNumbers()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var customer = await SeedCustomerAsync(context);
        var order = await SeedOrderAsync(context, customer.Id, branchId);
        var service = CreateService(context, year: 2026);

        var firstNumber = await service.AssignSampleNumberAsync(branchId);
        context.Samples.Add(new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = await SeedInvestigationTypeAsync(context),
            Number = firstNumber,
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Registered
        });

        var secondNumber = await service.AssignSampleNumberAsync(branchId);

        Assert.NotEqual(firstNumber, secondNumber);
        Assert.Equal("ZHY-2026-00002", secondNumber);
    }

    private static NumberingService CreateService(ApplicationDbContext context, int year) =>
        new(context, new FixedDateTimeProvider(new DateTime(year, 5, 27, 12, 0, 0, DateTimeKind.Utc)));

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedBranchAsync(ApplicationDbContext context, Guid branchId)
    {
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир",
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext context)
    {
        var customer = new Customer { FullName = "Тест" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    private static async Task<Order> SeedOrderAsync(ApplicationDbContext context, Guid customerId, Guid branchId)
    {
        var order = new Order
        {
            CustomerId = customerId,
            BranchId = branchId,
            ReferralNumber = "REF-1",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    private static async Task<Guid> SeedInvestigationTypeAsync(ApplicationDbContext context)
    {
        var investigationType = new InvestigationType
        {
            Code = "TST",
            NameUk = "Тест",
            SortOrder = 1
        };
        context.InvestigationTypes.Add(investigationType);
        await context.SaveChangesAsync();
        return investigationType.Id;
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public FixedDateTimeProvider(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
