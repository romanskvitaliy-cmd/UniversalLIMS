using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class SampleDeliveryServiceTests
{
    [Fact]
    public async Task GetQueueAsync_ReturnsReadyForPickup_WhenApprovedAndNotIssued()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateContextAsync(sampleId, SampleDeliveryStatus.ReadyForPickup);

        var service = new SampleDeliveryService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var result = await service.GetQueueAsync(new SampleDeliveryQueueFilter());

        Assert.Single(result.Items);
        Assert.Equal(sampleId, result.Items[0].SampleId);
    }

    [Fact]
    public async Task MarkIssuedAsync_SetsIssuedStatus()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateContextAsync(sampleId, SampleDeliveryStatus.ReadyForPickup);
        var utc = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

        var service = new SampleDeliveryService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(utc));

        var issued = await service.MarkIssuedAsync(sampleId);

        Assert.True(issued);

        var sample = await context.Samples.SingleAsync(item => item.Id == sampleId);
        Assert.Equal(SampleDeliveryStatus.Issued, sample.DeliveryStatus);
        Assert.Equal(utc, sample.IssuedAtUtc);
    }

    [Fact]
    public async Task MarkIssuedAsync_ReturnsFalse_WhenNotReadyForPickup()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateContextAsync(sampleId, SampleDeliveryStatus.None);

        var service = new SampleDeliveryService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        Assert.False(await service.MarkIssuedAsync(sampleId));
    }

    private static async Task<ApplicationDbContext> CreateContextAsync(
        Guid sampleId,
        SampleDeliveryStatus deliveryStatus)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            FullName = "Тестовий Клієнт"
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "T1",
            NameUk = "Тест",
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "TPL",
            NameUk = "Шаблон",
            Status = TemplateStatus.Active
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            StorageKey = "test.pdf"
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
            RegisteredAt = DateTime.UtcNow,
            InvestigationTypeId = investigationTypeId,
            Status = SampleStatus.ResultsEntered,
            DeliveryStatus = deliveryStatus,
            ReadyForPickupAtUtc = deliveryStatus == SampleDeliveryStatus.ReadyForPickup
                ? DateTime.UtcNow
                : null
        });

        context.OrderDocuments.Add(new OrderDocument
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            SampleId = sampleId,
            TemplateId = templateId,
            TemplateVersionId = versionId,
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.ResultsEntered
        });

        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.Approved,
            ApprovedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return context;
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string? UserId => "registrar-user";

        public string? UserName => "registrar";

        public string? UserFullName => "Registrar User";

        public Guid? BranchId => null;

        public string? IpAddress => "127.0.0.1";

        public string? UserAgent => "tests";

        public string? CorrelationId => "test";

        public bool IsAuthenticated => true;
    }

    private sealed class TestDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
