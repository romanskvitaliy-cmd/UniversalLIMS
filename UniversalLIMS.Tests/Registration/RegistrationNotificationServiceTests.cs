using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class RegistrationNotificationServiceTests
{
    [Fact]
    public async Task GetReadyForPickupSinceAsync_ReturnsApprovedReadySamples()
    {
        var sampleId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var readyAt = new DateTime(2026, 5, 30, 14, 0, 0, DateTimeKind.Utc);
        await using var context = await CreateContextAsync(sampleId, branchId, readyAt);

        var service = new RegistrationNotificationService(
            context,
            new TestCurrentUserService(branchId));

        var items = await service.GetReadyForPickupSinceAsync(readyAt.AddMinutes(-5));

        Assert.Single(items);
        Assert.Equal(sampleId, items[0].SampleId);
    }

    private static async Task<ApplicationDbContext> CreateContextAsync(
        Guid sampleId,
        Guid branchId,
        DateTime readyAt)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);

        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();

        context.Branches.Add(new Branch { Id = branchId, Code = "REG-ZHY", Name = "Реєстратура", IsActive = true });
        context.Customers.Add(new Customer { Id = customerId, FullName = "Клієнт" });
        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "T1",
            NameUk = "Тест",
            IsActive = true
        });
        context.Orders.Add(new Order
        {
            Id = orderId,
            BranchId = branchId,
            CustomerId = customerId,
            Status = OrderStatus.Registered
        });
        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            Number = "SMP-001",
            RegisteredAt = readyAt.AddDays(-1),
            InvestigationTypeId = investigationTypeId,
            DeliveryStatus = SampleDeliveryStatus.ReadyForPickup,
            ReadyForPickupAtUtc = readyAt
        });
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.Approved,
            ApprovedAtUtc = readyAt
        });

        await context.SaveChangesAsync();
        return context;
    }

    private sealed class TestCurrentUserService(Guid branchId) : ICurrentUserService
    {
        public string? UserId => "registrar-user";

        public string? UserName => "registrar";

        public string? UserFullName => "Registrar";

        public Guid? BranchId => branchId;

        public string? IpAddress => "127.0.0.1";

        public string? UserAgent => "tests";

        public string? CorrelationId => "test";

        public bool IsAuthenticated => true;
    }
}
