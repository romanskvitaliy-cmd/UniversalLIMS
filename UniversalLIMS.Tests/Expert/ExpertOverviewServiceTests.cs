using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Expert;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Expert;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Expert;

public sealed class ExpertOverviewServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_CountsQueuePerExpertBranch_WithoutCurrentUserFilter()
    {
        var expertBranchA = Guid.NewGuid();
        var expertBranchB = Guid.NewGuid();
        var labBranchId = Guid.NewGuid();
        var sampleInA = Guid.NewGuid();
        var sampleInB = Guid.NewGuid();

        await using var context = await CreateSeededContextAsync(
            expertBranchA,
            expertBranchB,
            labBranchId,
            sampleInA,
            sampleInB);

        var service = new ExpertOverviewService(context);

        var overview = await service.GetOverviewAsync();

        Assert.Equal(2, overview.TotalQueueSampleCount);
        Assert.Equal(2, overview.Branches.Count);

        var branchA = Assert.Single(overview.Branches, branch => branch.BranchId == expertBranchA);
        Assert.Equal(1, branchA.QueueSampleCount);

        var branchB = Assert.Single(overview.Branches, branch => branch.BranchId == expertBranchB);
        Assert.Equal(1, branchB.QueueSampleCount);
    }

    [Fact]
    public async Task GetOverviewAsync_ExcludesApprovedAndReworkSamples()
    {
        var expertBranchId = Guid.NewGuid();
        var labBranchId = Guid.NewGuid();
        var approvedSampleId = Guid.NewGuid();
        var reworkSampleId = Guid.NewGuid();
        var activeSampleId = Guid.NewGuid();

        await using var context = await CreateSeededContextAsync(
            expertBranchId,
            expertBranchId,
            labBranchId,
            approvedSampleId,
            reworkSampleId,
            activeSampleId);

        context.ExpertConclusionReviews.AddRange(
            new ExpertConclusionReview
            {
                SampleId = approvedSampleId,
                Status = ExpertConclusionStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow
            },
            new ExpertConclusionReview
            {
                SampleId = reworkSampleId,
                Status = ExpertConclusionStatus.ReturnedForRework,
                ReviewStartedAtUtc = DateTime.UtcNow
            },
            new ExpertConclusionReview
            {
                SampleId = activeSampleId,
                Status = ExpertConclusionStatus.InProgress,
                ReviewStartedAtUtc = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var service = new ExpertOverviewService(context);

        var overview = await service.GetOverviewAsync();

        Assert.Equal(1, overview.TotalQueueSampleCount);
        Assert.Equal(1, overview.TotalInProgressSampleCount);
        Assert.Equal(1, overview.Branches[0].QueueSampleCount);
        Assert.Equal(1, overview.Branches[0].InProgressSampleCount);
    }

    [Fact]
    public async Task GetOverviewAsync_IncludesSample_WhenLabBranchMapsToExpertBranch()
    {
        var expertBranchId = Guid.NewGuid();
        var labBranchId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();

        await using var context = await CreateSeededContextAsync(
            expertBranchId,
            expertBranchId,
            labBranchId,
            [sampleId],
            labExpertBranchId: expertBranchId);

        var service = new ExpertOverviewService(context);

        var overview = await service.GetOverviewAsync();

        Assert.Equal(1, overview.TotalQueueSampleCount);
    }

    private static async Task<ApplicationDbContext> CreateSeededContextAsync(
        Guid expertBranchAId,
        Guid expertBranchBId,
        Guid labBranchId,
        params Guid[] sampleIds)
    {
        return await CreateSeededContextAsync(
            expertBranchAId,
            expertBranchBId,
            labBranchId,
            sampleIds,
            labExpertBranchId: null);
    }

    private static async Task<ApplicationDbContext> CreateSeededContextAsync(
        Guid expertBranchAId,
        Guid expertBranchBId,
        Guid labBranchId,
        Guid[] sampleIds,
        Guid? labExpertBranchId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);

        if (expertBranchAId == expertBranchBId)
        {
            context.Branches.Add(new Branch
            {
                Id = expertBranchAId,
                Code = "EXP-ZHY",
                Name = "Експертиза Житомир",
                City = "Житомир",
                Kind = BranchKind.Expert,
                IsActive = true
            });
        }
        else
        {
            context.Branches.AddRange(
                new Branch
                {
                    Id = expertBranchAId,
                    Code = "MIX-BER",
                    Name = "Бердичів",
                    City = "Бердичів",
                    Kind = BranchKind.Mixed,
                    IsActive = true
                },
                new Branch
                {
                    Id = expertBranchBId,
                    Code = "MIX-KOR",
                    Name = "Корosten",
                    City = "Корosten",
                    Kind = BranchKind.Mixed,
                    IsActive = true
                });
        }

        context.Branches.Add(new Branch
        {
            Id = labBranchId,
            Code = "LAB-ZHY",
            Name = "Лабораторія",
            City = "Житомир",
            Kind = BranchKind.Laboratory,
            ExpertBranchId = labExpertBranchId,
            IsActive = true
        });

        var customerId = Guid.NewGuid();
        context.Customers.Add(new Customer
        {
            Id = customerId,
            FullName = "Тестовий замовник"
        });

        var investigationTypeId = Guid.NewGuid();
        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "FOOD",
            NameUk = "Харчові продукти",
            IsActive = true
        });

        var templateId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "F343",
            NameUk = "Протокол",
            Status = TemplateStatus.Active,
            CurrentPublishedVersionId = templateVersionId
        });
        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = templateVersionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf
        });

        for (var index = 0; index < sampleIds.Length; index++)
        {
            var sampleId = sampleIds[index];
            var targetBranchId = expertBranchAId == expertBranchBId
                ? (labExpertBranchId ?? expertBranchAId)
                : (index == 0 ? expertBranchAId : expertBranchBId);

            var orderId = Guid.NewGuid();
            context.Orders.Add(new Order
            {
                Id = orderId,
                CustomerId = customerId,
                BranchId = expertBranchAId,
                ReferralNumber = $"REF-{index + 1}",
                Status = OrderStatus.Registered,
                RegisteredAtUtc = DateTime.UtcNow
            });
            context.Samples.Add(new Sample
            {
                Id = sampleId,
                OrderId = orderId,
                InvestigationTypeId = investigationTypeId,
                Number = $"S-{index + 1}",
                Status = SampleStatus.ResultsEntered,
                RegisteredAt = DateTime.UtcNow,
                ResultsEnteredAtUtc = DateTime.UtcNow
            });
            context.OrderDocuments.Add(new OrderDocument
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                SampleId = sampleId,
                TemplateVersionId = templateVersionId,
                TargetBranchId = targetBranchId,
                Status = OrderDocumentStatus.ResultsEntered
            });
        }

        await context.SaveChangesAsync();
        return context;
    }
}
