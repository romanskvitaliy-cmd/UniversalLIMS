using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplateFillLayoutRefinementTests
{
    [Fact]
    public async Task SaveFillLayoutRefinementAsync_PublishedVersion_UpdatesOffsets()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var segmentId = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-FILL-LAYOUT",
            NameUk = "Fill layout",
            Status = TemplateStatus.Active,
            CurrentPublishedVersionId = versionId
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 49,
            Status = TemplateVersionStatus.Published,
            PublishedAtUtc = DateTime.UtcNow.AddDays(-1),
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('b', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "SamplingDate",
                    Title = "Дата відбору",
                    SortOrder = 1,
                    TextOffsetX = 0,
                    TextOffsetY = 0,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = segmentId,
                            TemplateFieldId = fieldId,
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 50,
                            PositionY = 100,
                            Width = 120,
                            Height = 18,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = new TemplateFieldMappingService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var result = await service.SaveFillLayoutRefinementAsync(
            versionId,
            [
                new PdfWorkspaceFillLayoutFieldUpdate
                {
                    TemplateFieldId = fieldId,
                    SegmentId = segmentId,
                    PageNumber = 1,
                    PositionX = 50,
                    PositionY = 100,
                    Width = 120,
                    Height = 18,
                    TextOffsetX = 2.5m,
                    TextOffsetY = -4m,
                    IsPrimary = true,
                    TextAlignment = "Left"
                }
            ]);

        Assert.Equal(1, result.Saved);
        Assert.Contains("v49", result.Message, StringComparison.Ordinal);

        var field = await context.TemplateFields
            .AsNoTracking()
            .SingleAsync(item => item.Id == fieldId);

        Assert.Equal(2.5m, field.TextOffsetX);
        Assert.Equal(-4m, field.TextOffsetY);
    }

    [Fact]
    public async Task SaveFillLayoutRefinementAsync_SupersededVersion_Throws()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-SUP",
            NameUk = "Superseded",
            Status = TemplateStatus.Active
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 5,
            Status = TemplateVersionStatus.Superseded,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('c', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new TemplateFieldMappingService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveFillLayoutRefinementAsync(
                versionId,
                [
                    new PdfWorkspaceFillLayoutFieldUpdate
                    {
                        TemplateFieldId = Guid.NewGuid(),
                        SegmentId = Guid.NewGuid(),
                        PageNumber = 1,
                        PositionX = 1,
                        PositionY = 1,
                        Width = 50,
                        Height = 20
                    }
                ]));
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
}
