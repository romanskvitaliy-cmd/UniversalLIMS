using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class PdfWorkspaceFillServiceTests
{
    [Fact]
    public async Task SaveValuesAsync_PersistsByDataFieldKeyWhenMapped()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-1",
            Name = "Test branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            Kind = CustomerKind.Individual,
            FullName = "Test customer"
        });

        context.DataFields.Add(new DataField
        {
            Id = dataFieldId,
            Key = "Sample.SamplingLocation",
            DisplayNameUk = "Місце відбору",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsActive = true
        });

        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-SAVE",
            NameUk = "PDF save test",
            Status = TemplateStatus.Draft
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = Guid.NewGuid(),
            Code = "INV-1",
            NameUk = "Test investigation",
            SortOrder = 1,
            IsActive = true
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = Guid.NewGuid(),
                    TemplateVersionId = versionId,
                    Tag = "SamplingLocation",
                    Title = "Місце відбору",
                    DataFieldId = dataFieldId,
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 10,
                            PositionY = 20,
                            Width = 100,
                            Height = 20,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance);

        var templateField = await context.TemplateFields.SingleAsync(field => field.TemplateVersionId == versionId);

        var result = await service.SaveValuesAsync(
            versionId,
            null,
            [new PdfWorkspaceFieldValueDto { TemplateFieldId = templateField.Id, Value = "м. Житомир" }]);

        Assert.Equal(1, result.SavedCount);
        Assert.Equal("Збережено 1 з 1 полів", result.Message);

        var stored = await context.OrderFieldValues
            .Include(fieldValue => fieldValue.DataField)
            .SingleAsync(fieldValue => fieldValue.OrderId == result.OrderId);

        Assert.Equal("Sample.SamplingLocation", stored.DataField.Key);
        Assert.Equal("м. Житомир", stored.StoredValue);
    }

    [Fact]
    public async Task SaveValuesAsync_CreatesSeparateRecordsPerTemplateFieldTag()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-2",
            Name = "Branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Kind = CustomerKind.Individual,
            FullName = "Customer"
        });

        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-MULTI",
            NameUk = "Multi field",
            Status = TemplateStatus.Draft
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = Guid.NewGuid(),
            Code = "INV-2",
            NameUk = "Investigation",
            SortOrder = 1,
            IsActive = true
        });

        var tags = new[] { "Global.DocNumber", "ProtocolNumber", "Global.FacilityName", "Air.Temperature" };
        var templateFieldIds = tags.Select(_ => Guid.NewGuid()).ToArray();
        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('c', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields = tags.Select((tag, index) => new TemplateField
            {
                Id = templateFieldIds[index],
                TemplateVersionId = versionId,
                Tag = tag,
                Title = tag,
                SortOrder = index + 1,
                Segments =
                [
                    new TemplateFieldSegment
                    {
                        Id = Guid.NewGuid(),
                        Sequence = 1,
                        PageNumber = 1,
                        PositionX = 10,
                        PositionY = 20 + index * 30,
                        Width = 100,
                        Height = 20,
                        IsPrimary = true
                    }
                ]
            }).ToList()
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance);

        var submissions = templateFieldIds
            .Select((id, index) => new PdfWorkspaceFieldValueDto
            {
                TemplateFieldId = id,
                Value = $"value-{index + 1}"
            })
            .ToList();

        var result = await service.SaveValuesAsync(versionId, null, submissions);

        Assert.Equal(tags.Length, result.SavedCount);
        Assert.Empty(result.UnmatchedFields);

        var storedKeys = await context.OrderFieldValues
            .Include(fieldValue => fieldValue.DataField)
            .Where(fieldValue => fieldValue.OrderId == result.OrderId)
            .Select(fieldValue => fieldValue.DataField.Key)
            .ToListAsync();

        Assert.Equal(tags.Length, storedKeys.Count);
        foreach (var tag in tags)
        {
            Assert.Contains(tag, storedKeys, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeTemplateDocumentStorage : Application.Templates.Abstractions.ITemplateDocumentStorage
    {
        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task<Application.Templates.Abstractions.StoredTemplateDocument> SaveAsync(
            Guid templateId,
            Guid templateVersionId,
            string originalFileName,
            string contentType,
            Stream documentStream,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Application.Templates.Abstractions.StoredTemplateDocument(
                "templates/test.pdf",
                originalFileName,
                contentType,
                1,
                new string('b', 64)));
    }
}
