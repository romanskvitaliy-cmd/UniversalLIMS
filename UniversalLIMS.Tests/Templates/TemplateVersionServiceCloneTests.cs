using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Templates;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplateVersionServiceCloneTests
{
    [Fact]
    public async Task CreateNewVersionAsync_ThrowsWhenSourceVersionHasNoFields()
    {
        var templateId = Guid.NewGuid();
        var sourceVersionId = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-EMPTY",
            NameUk = "Empty source",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = sourceVersionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/empty.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('b', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new TemplateVersionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow),
            new TestDocxContentControlReader(),
            new TestTemplateDocumentStorage(),
            new TestWordToPdfDocumentConverter(),
            new TestTemplatePublicationValidator());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateNewVersionAsync(sourceVersionId, "Clone empty"));

        Assert.Contains("немає полів", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CopyFieldsFromVersionAsync_CopiesGeometryOntoEmptyUploadedVersion()
    {
        var templateId = Guid.NewGuid();
        var sourceVersionId = Guid.NewGuid();
        var targetVersionId = Guid.NewGuid();
        var sourceFieldId = Guid.NewGuid();
        var sourceSegmentId = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-IMPORT",
            NameUk = "Import test",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = sourceVersionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "source.pdf",
            StorageKey = "templates/source.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 128,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = sourceFieldId,
                    TemplateVersionId = sourceVersionId,
                    Tag = "SamplingDate",
                    NormalizedTag = "SAMPLINGDATE",
                    Title = "Sampling date",
                    SortOrder = 1,
                    DetectedAtUtc = DateTime.UtcNow,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = sourceSegmentId,
                            TemplateFieldId = sourceFieldId,
                            Sequence = 1,
                            PageNumber = 2,
                            PositionX = 40,
                            PositionY = 50,
                            Width = 120,
                            Height = 32,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = targetVersionId,
            TemplateId = templateId,
            VersionNumber = 2,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "revised.pdf",
            StorageKey = "templates/revised.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 256,
            Sha256Hash = new string('c', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new TemplateVersionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow),
            new TestDocxContentControlReader(),
            new TestTemplateDocumentStorage(),
            new TestWordToPdfDocumentConverter(),
            new TestTemplatePublicationValidator());

        await service.CopyFieldsFromVersionAsync(targetVersionId, sourceVersionId);

        var targetVersion = await context.TemplateVersions
            .Include(version => version.Fields)
                .ThenInclude(field => field.Segments)
            .SingleAsync(version => version.Id == targetVersionId);

        var copiedField = Assert.Single(targetVersion.Fields);
        var copiedSegment = Assert.Single(copiedField.Segments);
        Assert.Equal(sourceVersionId, targetVersion.BasedOnTemplateVersionId);
        Assert.NotEqual(sourceFieldId, copiedField.Id);
        Assert.NotEqual(sourceSegmentId, copiedSegment.Id);
        Assert.Equal(2, copiedSegment.PageNumber);
        Assert.Equal(40, copiedSegment.PositionX);
        Assert.Equal(50, copiedSegment.PositionY);
        Assert.Equal(120, copiedSegment.Width);
        Assert.Equal(32, copiedSegment.Height);
    }

    [Fact]
    public async Task CreateNewVersionAsync_CopiesFieldsAndSegmentsWithNewIdentifiers()
    {
        var templateId = Guid.NewGuid();
        var sourceVersionId = Guid.NewGuid();
        var sourceFieldId = Guid.NewGuid();
        var sourceSegmentOneId = Guid.NewGuid();
        var sourceSegmentTwoId = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-CLONE",
            NameUk = "Clone test",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = sourceVersionId,
            TemplateId = templateId,
            VersionNumber = 4,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/shared.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 128,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = sourceFieldId,
                    TemplateVersionId = sourceVersionId,
                    Tag = "CLIENT_NAME",
                    NormalizedTag = "CLIENT_NAME",
                    Title = "Client",
                    SortOrder = 1,
                    DetectedAtUtc = DateTime.UtcNow,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = sourceSegmentOneId,
                            TemplateFieldId = sourceFieldId,
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 10,
                            PositionY = 20,
                            Width = 100,
                            Height = 30,
                            IsPrimary = true
                        },
                        new TemplateFieldSegment
                        {
                            Id = sourceSegmentTwoId,
                            TemplateFieldId = sourceFieldId,
                            Sequence = 2,
                            PageNumber = 2,
                            PositionX = 40,
                            PositionY = 50,
                            Width = 120,
                            Height = 32,
                            IsPrimary = false
                        }
                    ]
                }
            ]
        });

        await context.SaveChangesAsync();

        var service = new TemplateVersionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow),
            new TestDocxContentControlReader(),
            new TestTemplateDocumentStorage(),
            new TestWordToPdfDocumentConverter(),
            new TestTemplatePublicationValidator());

        var clonedVersion = await service.CreateNewVersionAsync(sourceVersionId, "Clone for v5");

        Assert.Equal(5, clonedVersion.VersionNumber);
        Assert.Equal(sourceVersionId, clonedVersion.BasedOnTemplateVersionId);
        Assert.Equal("templates/shared.pdf", clonedVersion.StorageKey);

        var persistedClone = await context.TemplateVersions
            .Include(version => version.Fields)
                .ThenInclude(field => field.Segments)
            .SingleAsync(version => version.Id == clonedVersion.Id);

        var clonedField = Assert.Single(persistedClone.Fields);
        Assert.NotEqual(sourceFieldId, clonedField.Id);
        Assert.Equal("CLIENT_NAME", clonedField.Tag);

        Assert.Equal(2, clonedField.Segments.Count);
        Assert.DoesNotContain(clonedField.Segments, segment => segment.Id == sourceSegmentOneId);
        Assert.DoesNotContain(clonedField.Segments, segment => segment.Id == sourceSegmentTwoId);
        Assert.Contains(clonedField.Segments, segment => segment.Sequence == 1 && segment.PageNumber == 1);
        Assert.Contains(clonedField.Segments, segment => segment.Sequence == 2 && segment.PageNumber == 2);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
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

    private sealed class TestDocxContentControlReader : IDocxContentControlReader
    {
        public Task<IReadOnlyCollection<DocxContentControlInfo>> ReadContentControlsAsync(
            Stream documentStream,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<DocxContentControlInfo>>([]);
    }

    private sealed class TestWordToPdfDocumentConverter : IWordToPdfDocumentConverter
    {
        public async Task<MemoryStream> ConvertAsync(
            Stream wordDocumentStream,
            string extension,
            CancellationToken cancellationToken = default)
        {
            var pdfStream = new MemoryStream();
            await wordDocumentStream.CopyToAsync(pdfStream, cancellationToken);
            pdfStream.Position = 0;
            return pdfStream;
        }
    }

    private sealed class TestTemplateDocumentStorage : ITemplateDocumentStorage
    {
        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task<StoredTemplateDocument> SaveAsync(
            Guid templateId,
            Guid templateVersionId,
            string originalFileName,
            string contentType,
            Stream documentStream,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestTemplatePublicationValidator : ITemplatePublicationValidator
    {
        public Task<PublicationValidationResult> ValidateAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PublicationValidationResult());
    }
}
