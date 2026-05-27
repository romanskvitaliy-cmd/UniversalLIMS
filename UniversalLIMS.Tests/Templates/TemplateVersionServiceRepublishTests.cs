using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Templates;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplateVersionServiceRepublishTests
{
    private static readonly DateTime FirstPublishUtc = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RepublishUtc = new(2026, 5, 26, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RepublishAsync_SupersededVersion_BecomesCurrent_PreservesFirstDate_SetsRepublished()
    {
        var templateId = Guid.NewGuid();
        var v5Id = Guid.NewGuid();
        var v7Id = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-REP",
            NameUk = "Republish test",
            Status = TemplateStatus.Active,
            CurrentPublishedVersionId = v7Id
        });

        context.TemplateVersions.AddRange(
            new TemplateVersion
            {
                Id = v5Id,
                TemplateId = templateId,
                VersionNumber = 5,
                Status = TemplateVersionStatus.Superseded,
                DocumentFormat = TemplateDocumentFormat.Pdf,
                OriginalFileName = "v5.pdf",
                StorageKey = "templates/v5.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 1,
                Sha256Hash = new string('a', 64),
                UploadedAtUtc = FirstPublishUtc.AddDays(-1),
                FirstPublishedAtUtc = FirstPublishUtc,
                PublishedAtUtc = FirstPublishUtc
            },
            new TemplateVersion
            {
                Id = v7Id,
                TemplateId = templateId,
                VersionNumber = 7,
                Status = TemplateVersionStatus.Published,
                DocumentFormat = TemplateDocumentFormat.Pdf,
                OriginalFileName = "v7.pdf",
                StorageKey = "templates/v7.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 1,
                Sha256Hash = new string('b', 64),
                UploadedAtUtc = RepublishUtc.AddDays(-2),
                FirstPublishedAtUtc = RepublishUtc.AddDays(-1),
                PublishedAtUtc = RepublishUtc.AddDays(-1)
            });

        await context.SaveChangesAsync();

        var service = CreateService(context, RepublishUtc);
        var result = await service.RepublishAsync(v5Id, "Повернення v5 в роботу");

        Assert.True(result.IsValid);

        var template = await context.Templates.AsNoTracking().SingleAsync(item => item.Id == templateId);
        Assert.Equal(v5Id, template.CurrentPublishedVersionId);

        var v5 = await context.TemplateVersions.AsNoTracking().SingleAsync(item => item.Id == v5Id);
        var v7 = await context.TemplateVersions.AsNoTracking().SingleAsync(item => item.Id == v7Id);

        Assert.Equal(TemplateVersionStatus.Published, v5.Status);
        Assert.Equal(FirstPublishUtc, v5.FirstPublishedAtUtc);
        Assert.Equal(RepublishUtc, v5.RepublishedAtUtc);
        Assert.Equal(RepublishUtc, v5.PublishedAtUtc);

        Assert.Equal(TemplateVersionStatus.Superseded, v7.Status);
    }

    [Fact]
    public async Task PublishAsync_FirstPublication_SetsFirstDateOnly()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var publishUtc = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-NEW",
            NameUk = "First publish",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "t.pdf",
            StorageKey = "templates/t.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('c', 64),
            UploadedAtUtc = publishUtc.AddHours(-1),
            Fields =
            [
                new TemplateField
                {
                    Id = Guid.NewGuid(),
                    TemplateVersionId = versionId,
                    Tag = "Field1",
                    Title = "Field",
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            TemplateFieldId = Guid.Empty,
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 1,
                            PositionY = 1,
                            Width = 50,
                            Height = 20,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        await context.SaveChangesAsync();

        var field = await context.TemplateFields.SingleAsync();
        var segment = await context.TemplateFieldSegments.SingleAsync();
        segment.TemplateFieldId = field.Id;

        foreach (var role in UniversalLIMS.Application.Security.LimsRoles.All)
        {
            context.TemplateFieldPermissions.Add(new TemplateFieldPermission
            {
                TemplateFieldId = field.Id,
                RoleName = role,
                AccessLevel = FieldAccessLevel.Write
            });
        }

        await context.SaveChangesAsync();

        var service = CreateService(context, publishUtc);
        var result = await service.PublishAsync(versionId, "Перша публікація");

        Assert.True(result.IsValid);

        var version = await context.TemplateVersions.AsNoTracking().SingleAsync(item => item.Id == versionId);
        Assert.Equal(publishUtc, version.FirstPublishedAtUtc);
        Assert.Equal(publishUtc, version.PublishedAtUtc);
        Assert.Null(version.RepublishedAtUtc);
    }

    [Fact]
    public async Task RepublishAsync_LegacyVersionWithoutFirstPublished_BackfillsFromPublishedAtUtc()
    {
        var templateId = Guid.NewGuid();
        var v5Id = Guid.NewGuid();
        var legacyPublishedUtc = new DateTime(2024, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var republishUtc = new DateTime(2026, 5, 27, 6, 0, 0, DateTimeKind.Utc);

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-LEG",
            NameUk = "Legacy dates",
            Status = TemplateStatus.Active,
            CurrentPublishedVersionId = Guid.NewGuid()
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = v5Id,
            TemplateId = templateId,
            VersionNumber = 5,
            Status = TemplateVersionStatus.Superseded,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "v5.pdf",
            StorageKey = "templates/v5.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('d', 64),
            UploadedAtUtc = legacyPublishedUtc.AddDays(-1),
            FirstPublishedAtUtc = null,
            PublishedAtUtc = legacyPublishedUtc
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, republishUtc);
        var result = await service.RepublishAsync(v5Id, "Повторна активація legacy");

        Assert.True(result.IsValid);

        var version = await context.TemplateVersions.AsNoTracking().SingleAsync(item => item.Id == v5Id);
        Assert.Equal(legacyPublishedUtc, version.FirstPublishedAtUtc);
        Assert.Equal(republishUtc, version.RepublishedAtUtc);
        Assert.Equal(republishUtc, version.PublishedAtUtc);
    }

    [Fact]
    public async Task RepublishAsync_WhenValidationFails_LeavesCurrentPublishedUnchanged()
    {
        var templateId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var supersededId = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-BLOCK",
            NameUk = "Blocked republish",
            Status = TemplateStatus.Active,
            CurrentPublishedVersionId = currentId
        });

        context.TemplateVersions.AddRange(
            new TemplateVersion
            {
                Id = supersededId,
                TemplateId = templateId,
                VersionNumber = 5,
                Status = TemplateVersionStatus.Superseded,
                DocumentFormat = TemplateDocumentFormat.Pdf,
                OriginalFileName = "v5.pdf",
                StorageKey = "templates/v5.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 1,
                Sha256Hash = new string('e', 64),
                UploadedAtUtc = DateTime.UtcNow
            },
            new TemplateVersion
            {
                Id = currentId,
                TemplateId = templateId,
                VersionNumber = 7,
                Status = TemplateVersionStatus.Published,
                DocumentFormat = TemplateDocumentFormat.Pdf,
                OriginalFileName = "v7.pdf",
                StorageKey = "templates/v7.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 1,
                Sha256Hash = new string('f', 64),
                UploadedAtUtc = DateTime.UtcNow,
                PublishedAtUtc = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var service = new TemplateVersionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow),
            new TestDocxContentControlReader(),
            new TestTemplateDocumentStorage(),
            new TestWordToPdfDocumentConverter(),
            new FailingTemplatePublicationValidator());

        var result = await service.RepublishAsync(supersededId, "blocked");

        Assert.False(result.IsValid);

        var template = await context.Templates.AsNoTracking().SingleAsync(item => item.Id == templateId);
        Assert.Equal(currentId, template.CurrentPublishedVersionId);

        var superseded = await context.TemplateVersions.AsNoTracking().SingleAsync(item => item.Id == supersededId);
        Assert.Equal(TemplateVersionStatus.Superseded, superseded.Status);
    }

    private static TemplateVersionService CreateService(ApplicationDbContext context, DateTime utcNow) =>
        new(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(utcNow),
            new TestDocxContentControlReader(),
            new TestTemplateDocumentStorage(),
            new TestWordToPdfDocumentConverter(),
            new TestTemplatePublicationValidator());

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

    private sealed class TestTemplatePublicationValidator : ITemplatePublicationValidator
    {
        public Task<PublicationValidationResult> ValidateAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PublicationValidationResult());

        public Task<PublicationValidationResult> ValidateRepublishAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PublicationValidationResult());
    }

    private sealed class FailingTemplatePublicationValidator : ITemplatePublicationValidator
    {
        public Task<PublicationValidationResult> ValidateAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PublicationValidationResult());

        public Task<PublicationValidationResult> ValidateRepublishAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PublicationValidationResult
            {
                Errors = ["Republish blocked for test."]
            });
    }
}
