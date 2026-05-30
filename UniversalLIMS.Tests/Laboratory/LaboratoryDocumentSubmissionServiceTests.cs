using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Laboratory;

public sealed class LaboratoryDocumentSubmissionServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SendDocumentToExpertAsync_MarksDocumentAndSampleReady_WhenSingleDocument()
    {
        var branchId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        await using var context = CreateContext();
        SeedSampleWithDocument(context, branchId, orderId, sampleId, documentId, OrderDocumentStatus.InProgress);
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);
        var result = await service.SendDocumentToExpertAsync(documentId);

        var document = await context.OrderDocuments.SingleAsync(item => item.Id == documentId);
        var sample = await context.Samples.SingleAsync(item => item.Id == sampleId);

        Assert.Equal(OrderDocumentStatus.ResultsEntered, document.Status);
        Assert.Equal(FixedNow, document.ResultsEnteredAtUtc);
        Assert.Equal(SampleStatus.ResultsEntered, sample.Status);
        Assert.Equal(FixedNow, sample.ResultsEnteredAtUtc);
        Assert.True(result.SampleReadyForExpert);
    }

    [Fact]
    public async Task SendDocumentToExpertAsync_KeepsSampleInProgress_UntilAllDocumentsSent()
    {
        var branchId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        await using var context = CreateContext();
        SeedSampleWithDocument(context, branchId, orderId, sampleId, firstDocumentId, OrderDocumentStatus.InProgress);
        context.OrderDocuments.Add(new OrderDocument
        {
            Id = secondDocumentId,
            OrderId = orderId,
            SampleId = sampleId,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.InProgress
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);
        var firstResult = await service.SendDocumentToExpertAsync(firstDocumentId);

        var sampleAfterFirst = await context.Samples.SingleAsync(item => item.Id == sampleId);
        Assert.Equal(SampleStatus.InProgress, sampleAfterFirst.Status);
        Assert.Null(sampleAfterFirst.ResultsEnteredAtUtc);
        Assert.False(firstResult.SampleReadyForExpert);

        var secondResult = await service.SendDocumentToExpertAsync(secondDocumentId);
        var sampleAfterSecond = await context.Samples.SingleAsync(item => item.Id == sampleId);
        Assert.Equal(SampleStatus.ResultsEntered, sampleAfterSecond.Status);
        Assert.NotNull(sampleAfterSecond.ResultsEnteredAtUtc);
        Assert.True(secondResult.SampleReadyForExpert);
    }

    [Fact]
    public async Task SendDocumentToExpertAsync_RejectsSentToLabBeforePdfSave()
    {
        var branchId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        await using var context = CreateContext();
        SeedSampleWithDocument(context, branchId, orderId, sampleId, documentId, OrderDocumentStatus.SentToLab);
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendDocumentToExpertAsync(documentId));

        Assert.Contains("В роботі", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendDocumentToExpertAsync_RejectsAlreadySentDocument()
    {
        var branchId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        await using var context = CreateContext();
        SeedSampleWithDocument(context, branchId, orderId, sampleId, documentId, OrderDocumentStatus.ResultsEntered);
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendDocumentToExpertAsync(documentId));

        Assert.Contains("вже відправлено", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LaboratoryDocumentSubmissionService CreateService(
        ApplicationDbContext context,
        Guid branchId) =>
        new(
            context,
            new FixedLaboratoryBranchContext(branchId),
            new FixedDateTimeProvider(FixedNow),
            new FixedCurrentUser("lab-user"));

    private static void SeedSampleWithDocument(
        ApplicationDbContext context,
        Guid branchId,
        Guid orderId,
        Guid sampleId,
        Guid documentId,
        OrderDocumentStatus documentStatus)
    {
        var customerId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-LAB",
            Name = "Lab branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            Kind = CustomerKind.Individual,
            FullName = "Тестовий замовник"
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "AIR",
            NameUk = "Повітря",
            SortOrder = 1,
            IsActive = true
        });

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = branchId,
            Status = OrderStatus.Registered,
            ReferralNumber = "REF-1"
        });

        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            InvestigationTypeId = investigationTypeId,
            Number = "ZHY-TEST-001",
            RegisteredAt = FixedNow.AddDays(-1),
            Status = SampleStatus.Routed
        });

        context.OrderDocuments.Add(new OrderDocument
        {
            Id = documentId,
            OrderId = orderId,
            SampleId = sampleId,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = branchId,
            Status = documentStatus
        });
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class FixedLaboratoryBranchContext(Guid branchId) : ILaboratoryBranchContext
    {
        public Task<LaboratoryBranchContextState> GetStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LaboratoryBranchContextState
            {
                ActiveBranchId = branchId,
                CanSelectBranch = false,
                Branches = []
            });

        public Task SetSelectedBranchAsync(Guid? branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class FixedCurrentUser(string userId) : ICurrentUserService
    {
        public string? UserId => userId;

        public string? UserName => userId;

        public string? UserFullName => userId;

        public Guid? BranchId => null;

        public string? IpAddress => "127.0.0.1";

        public string? UserAgent => "tests";

        public string? CorrelationId => "test";

        public bool IsAuthenticated => true;
    }
}
