using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Expert;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Expert;

public sealed class ExpertConclusionServiceTests
{
    [Fact]
    public async Task ApproveAsync_CreatesApprovedReview_ForReadySample()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateReadySampleContextAsync(sampleId);
        var utc = new DateTime(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);

        var service = new ExpertConclusionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(utc));

        var approved = await service.ApproveAsync(sampleId, "Відповідає нормам");

        Assert.True(approved);

        var review = await context.ExpertConclusionReviews.SingleAsync(item => item.SampleId == sampleId);
        Assert.Equal(ExpertConclusionStatus.Approved, review.Status);
        Assert.Equal(utc, review.ApprovedAtUtc);
        Assert.Equal("Відповідає нормам", review.NotesUk);

        var sample = await context.Samples.SingleAsync(item => item.Id == sampleId);
        Assert.Equal(SampleDeliveryStatus.ReadyForPickup, sample.DeliveryStatus);
        Assert.Equal(utc, sample.ReadyForPickupAtUtc);
    }

    [Fact]
    public async Task ApproveAsync_ReturnsFalse_WhenSampleNotReady()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateReadySampleContextAsync(
            sampleId,
            OrderDocumentStatus.InProgress);

        var service = new ExpertConclusionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var approved = await service.ApproveAsync(sampleId, null);

        Assert.False(approved);
        Assert.False(await context.ExpertConclusionReviews.AnyAsync());
    }

    [Fact]
    public async Task ApproveAsync_TrimsAndTruncatesNotes_ToConfiguredLimit()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateReadySampleContextAsync(sampleId);

        var service = new ExpertConclusionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var rawNotes = $"  {new string('Н', 2100)}  ";
        var approved = await service.ApproveAsync(sampleId, rawNotes);

        Assert.True(approved);

        var review = await context.ExpertConclusionReviews.SingleAsync(item => item.SampleId == sampleId);
        Assert.NotNull(review.NotesUk);
        Assert.Equal(2000, review.NotesUk!.Length);
        Assert.Equal('Н', review.NotesUk[0]);
    }

    [Fact]
    public async Task ReturnToPendingReviewAsync_SetsPendingStatus_WhenReviewInProgress()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateReadySampleContextAsync(sampleId);
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.InProgress,
            ReviewStartedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "expert-user"
        });
        await context.SaveChangesAsync();

        var service = new ExpertConclusionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var moved = await service.ReturnToPendingReviewAsync(sampleId);

        Assert.True(moved);
        var review = await context.ExpertConclusionReviews.SingleAsync(item => item.SampleId == sampleId);
        Assert.Equal(ExpertConclusionStatus.PendingReview, review.Status);
    }

    [Fact]
    public async Task ReturnToPendingReviewAsync_ReturnsFalse_WhenReviewAlreadyApproved()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateReadySampleContextAsync(sampleId);
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.Approved,
            ApprovedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "expert-user"
        });
        await context.SaveChangesAsync();

        var service = new ExpertConclusionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var moved = await service.ReturnToPendingReviewAsync(sampleId);

        Assert.False(moved);
        var review = await context.ExpertConclusionReviews.SingleAsync(item => item.SampleId == sampleId);
        Assert.Equal(ExpertConclusionStatus.Approved, review.Status);
    }

    [Fact]
    public async Task ReturnForReworkAsync_ResetsLabWorkflow_WhenReviewInProgress()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateReadySampleContextAsync(sampleId);
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.InProgress,
            ReviewStartedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "expert-user"
        });
        await context.SaveChangesAsync();

        var service = new ExpertConclusionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var returned = await service.ReturnForReworkAsync(sampleId, "Виправити формулювання");

        Assert.True(returned);

        var review = await context.ExpertConclusionReviews.SingleAsync(item => item.SampleId == sampleId);
        Assert.Equal(ExpertConclusionStatus.ReturnedForRework, review.Status);
        Assert.Equal("Виправити формулювання", review.ReworkReasonUk);

        var sample = await context.Samples.SingleAsync(item => item.Id == sampleId);
        Assert.Equal(SampleStatus.InProgress, sample.Status);
        Assert.Equal(SampleDeliveryStatus.None, sample.DeliveryStatus);

        var document = await context.OrderDocuments.SingleAsync(item => item.SampleId == sampleId);
        Assert.Equal(OrderDocumentStatus.InProgress, document.Status);
    }

    [Fact]
    public async Task ReturnToPendingReviewAsync_ReturnsTrue_WhenAlreadyPending()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateReadySampleContextAsync(sampleId);
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.PendingReview,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "expert-user"
        });
        await context.SaveChangesAsync();

        var service = new ExpertConclusionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var moved = await service.ReturnToPendingReviewAsync(sampleId);

        Assert.True(moved);
        var review = await context.ExpertConclusionReviews.SingleAsync(item => item.SampleId == sampleId);
        Assert.Equal(ExpertConclusionStatus.PendingReview, review.Status);
    }

    private static async Task<ApplicationDbContext> CreateReadySampleContextAsync(
        Guid sampleId,
        OrderDocumentStatus documentStatus = OrderDocumentStatus.ResultsEntered)
    {
        var context = CreateContext();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();

        context.Branches.Add(new Branch { Id = branchId, Code = "ZHY", Name = "Житомир", IsActive = true });
        context.Customers.Add(new Customer { Id = customerId, FullName = "Клієнт" });
        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "FOOD",
            NameUk = "Харчові",
            IsActive = true
        });
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T",
            NameUk = "Шаблон",
            Status = TemplateStatus.Active,
            CurrentPublishedVersionId = versionId
        });
        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "t.pdf",
            StorageKey = "k",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('b', 64),
            UploadedAtUtc = DateTime.UtcNow
        });
        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = branchId,
            Status = OrderStatus.Registered
        });
        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            Number = "S-1",
            RegisteredAt = DateTime.UtcNow,
            InvestigationTypeId = investigationTypeId,
            Status = SampleStatus.ResultsEntered
        });
        context.OrderDocuments.Add(new OrderDocument
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            SampleId = sampleId,
            TemplateId = templateId,
            TemplateVersionId = versionId,
            TargetBranchId = branchId,
            Status = documentStatus
        });

        await context.SaveChangesAsync();
        return context;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string? UserId => "expert-user";

        public string? UserName => "expert";

        public string? UserFullName => "Expert User";

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
