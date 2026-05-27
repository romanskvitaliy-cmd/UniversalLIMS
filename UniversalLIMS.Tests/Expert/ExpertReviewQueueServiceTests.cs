using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Expert;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Expert;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Expert;

public sealed class ExpertReviewQueueServiceTests
{
    [Fact]
    public async Task GetQueueAsync_IncludesSample_WhenAllSentDocumentsHaveResultsEntered()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateSeededContextAsync(sampleId, OrderDocumentStatus.ResultsEntered);

        var service = new ExpertReviewQueueService(context);
        var result = await service.GetQueueAsync(new ExpertReviewQueueFilter());

        Assert.Contains(result.Items, item => item.SampleId == sampleId);
    }

    [Fact]
    public async Task GetQueueAsync_ExcludesSample_WhenAnySentDocumentStillInProgress()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateSeededContextAsync(
            sampleId,
            OrderDocumentStatus.ResultsEntered,
            secondDocumentStatus: OrderDocumentStatus.InProgress);

        var service = new ExpertReviewQueueService(context);
        var result = await service.GetQueueAsync(new ExpertReviewQueueFilter());

        Assert.DoesNotContain(result.Items, item => item.SampleId == sampleId);
    }

    [Fact]
    public async Task GetQueueAsync_ExcludesApprovedSample_FromDefaultQueue()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateSeededContextAsync(sampleId, OrderDocumentStatus.ResultsEntered);
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.Approved,
            ApprovedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new ExpertReviewQueueService(context);
        var result = await service.GetQueueAsync(new ExpertReviewQueueFilter());

        Assert.DoesNotContain(result.Items, item => item.SampleId == sampleId);
    }

    [Fact]
    public async Task GetQueueAsync_IncludesApprovedSample_WhenFilterRequestsApproved()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateSeededContextAsync(sampleId, OrderDocumentStatus.ResultsEntered);
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.Approved,
            ApprovedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new ExpertReviewQueueService(context);
        var result = await service.GetQueueAsync(new ExpertReviewQueueFilter
        {
            ReviewStatus = ExpertConclusionStatus.Approved
        });

        Assert.Contains(result.Items, item => item.SampleId == sampleId);
    }

    [Fact]
    public async Task GetQueueAsync_FiltersByNotesContainsUk_WhenProvided()
    {
        var sampleId = Guid.NewGuid();
        await using var context = await CreateSeededContextAsync(sampleId, OrderDocumentStatus.ResultsEntered);
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleId,
            Status = ExpertConclusionStatus.Approved,
            ApprovedAtUtc = DateTime.UtcNow,
            NotesUk = "Потребує контрольної перевірки"
        });
        await context.SaveChangesAsync();

        var service = new ExpertReviewQueueService(context);
        var result = await service.GetQueueAsync(new ExpertReviewQueueFilter
        {
            ReviewStatus = ExpertConclusionStatus.Approved,
            NotesContainsUk = "контрольної"
        });

        Assert.Contains(result.Items, item => item.SampleId == sampleId);
    }

    private static async Task<ApplicationDbContext> CreateSeededContextAsync(
        Guid sampleId,
        OrderDocumentStatus primaryDocumentStatus,
        OrderDocumentStatus? secondDocumentStatus = null)
    {
        var context = CreateContext();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();
        var doc1Id = Guid.NewGuid();
        var doc2Id = Guid.NewGuid();

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
            FullName = "Тестовий клієнт"
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "FOOD",
            NameUk = "Харчові продукти",
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-FOOD",
            NameUk = "Бланк",
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
            StorageKey = "templates/t.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = branchId,
            ReferralNumber = "REF-1",
            Status = OrderStatus.Registered
        });

        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            Number = "S-001",
            RegisteredAt = DateTime.UtcNow,
            InvestigationTypeId = investigationTypeId,
            Status = SampleStatus.ResultsEntered
        });

        context.OrderDocuments.Add(new OrderDocument
        {
            Id = doc1Id,
            OrderId = orderId,
            SampleId = sampleId,
            TemplateId = templateId,
            TemplateVersionId = versionId,
            TargetBranchId = branchId,
            Status = primaryDocumentStatus
        });

        if (secondDocumentStatus.HasValue)
        {
            context.OrderDocuments.Add(new OrderDocument
            {
                Id = doc2Id,
                OrderId = orderId,
                SampleId = sampleId,
                TemplateId = templateId,
                TemplateVersionId = versionId,
                TargetBranchId = branchId,
                Status = secondDocumentStatus.Value
            });
        }

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
}
