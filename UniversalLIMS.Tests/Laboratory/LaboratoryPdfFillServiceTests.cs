using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Laboratory;

public sealed class LaboratoryPdfFillServiceTests
{
    [Fact]
    public async Task GetFillTargetsAsync_ReturnsSentToLabOrderDocuments()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        SeedBranchCustomerOrderSample(context, branchId, orderId, sampleId);
        SeedPdfTemplate(context, templateId, versionId, "Протокол");

        context.OrderDocuments.Add(new OrderDocument
        {
            Id = documentId,
            OrderId = orderId,
            SampleId = sampleId,
            TemplateId = templateId,
            TemplateVersionId = versionId,
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.SentToLab
        });

        await context.SaveChangesAsync();

        var service = new LaboratoryPdfFillService(context, new FixedCurrentUser(branchId));
        var targets = await service.GetFillTargetsAsync(sampleId);

        Assert.Single(targets);
        Assert.Equal(versionId, targets[0].TemplateVersionId);
        Assert.Equal(orderId, targets[0].OrderId);
        Assert.Equal(documentId, targets[0].OrderDocumentId);
    }

    [Fact]
    public async Task GetFillTargetsAsync_IgnoresPendingDocuments()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();

        SeedBranchCustomerOrderSample(context, branchId, orderId, sampleId, investigationTypeId);
        SeedPdfTemplate(context, templateId, versionId, "Протокол", investigationTypeId);

        context.OrderDocuments.Add(new OrderDocument
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            SampleId = sampleId,
            TemplateId = templateId,
            TemplateVersionId = versionId,
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.Pending
        });

        await context.SaveChangesAsync();

        var service = new LaboratoryPdfFillService(context, new FixedCurrentUser(branchId));
        var targets = await service.GetFillTargetsAsync(sampleId);

        Assert.Single(targets);
        Assert.Null(targets[0].OrderDocumentId);
        Assert.Equal(versionId, targets[0].TemplateVersionId);
    }

    private static void SeedBranchCustomerOrderSample(
        ApplicationDbContext context,
        Guid branchId,
        Guid orderId,
        Guid sampleId,
        Guid? investigationTypeId = null)
    {
        investigationTypeId ??= Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-LAB",
            Name = "Lab branch",
            City = "Zhytomyr",
            IsActive = true
        });

        var customerId = Guid.NewGuid();
        context.Customers.Add(new Customer
        {
            Id = customerId,
            Kind = CustomerKind.Individual,
            FullName = "Test"
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId.Value,
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
            InvestigationTypeId = investigationTypeId.Value,
            Number = "ZHY-TEST-001",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Routed
        });
    }

    private static void SeedPdfTemplate(
        ApplicationDbContext context,
        Guid templateId,
        Guid versionId,
        string nameUk,
        Guid? investigationTypeId = null)
    {
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "TPL-1",
            NameUk = nameUk
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
            Sha256Hash = new string('d', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        if (investigationTypeId.HasValue)
        {
            context.InvestigationTypeTemplates.Add(new InvestigationTypeTemplate
            {
                InvestigationTypeId = investigationTypeId.Value,
                TemplateId = templateId,
                IsActive = true
            });
        }
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FixedCurrentUser : ICurrentUserService
    {
        public FixedCurrentUser(Guid? branchId) => BranchId = branchId;

        public string? UserId => null;

        public string? UserName => "test";

        public string? UserFullName => "Test User";

        public Guid? BranchId { get; }

        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;

        public bool IsAuthenticated => true;
    }
}
