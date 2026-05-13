using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplateFieldSegmentLayoutUpdateTests
{
    [Fact(Skip = "InMemory provider does not reliably support row-versioned segment updates; covered by LocalDB integration tests.")]
    public async Task UpdateSegmentLayoutsAsync_UpsertsMultipleSegmentsIncludingNewOnes()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var existingSegmentId = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-SEG",
            NameUk = "Segment test",
            Status = TemplateStatus.Draft
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
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "Conclusion",
                    Title = "Висновок",
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = existingSegmentId,
                            TemplateFieldId = fieldId,
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
        context.ChangeTracker.Clear();

        var service = new TemplateFieldMappingService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var clientReferenceIds = new[]
        {
            "client-seg-1",
            "client-seg-2",
            "client-seg-3"
        };

        var updates = new List<TemplateFieldSegmentLayoutUpdate>
        {
            new(existingSegmentId, fieldId, clientReferenceIds[0], 1, 1, 10, 20, 100, 20, true, "Left"),
            new(null, fieldId, clientReferenceIds[1], 2, 1, 10, 45, 100, 20, false, "Left"),
            new(null, fieldId, clientReferenceIds[2], 3, 1, 10, 70, 100, 20, false, "Left")
        };

        var saveResult = await SaveFieldSegmentsAsync(service, context, versionId, fieldId, updates);

        var segments = await context.TemplateFieldSegments
            .Where(segment => segment.TemplateFieldId == fieldId)
            .OrderBy(segment => segment.Sequence)
            .ToListAsync();

        Assert.Equal(3, segments.Count);
        Assert.Equal([1, 2, 3], segments.Select(segment => segment.Sequence).ToArray());
        Assert.Equal(3, saveResult.Segments.Count);
        Assert.Equal(clientReferenceIds, saveResult.Segments.Select(segment => segment.ClientReferenceId).ToArray());
    }

    [Fact(Skip = "InMemory provider does not reliably support row-versioned segment updates; covered by LocalDB integration tests.")]
    public async Task UpdateSegmentLayoutsAsync_SecondSaveWithReturnedSegmentIds_UpdatesLayouts()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var existingSegmentId = Guid.NewGuid();

        await using var context = CreateContext();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-SEG-2",
            NameUk = "Segment test 2",
            Status = TemplateStatus.Draft
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
            Sha256Hash = new string('b', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "Humidity",
                    Title = "Відносна вологість",
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = existingSegmentId,
                            TemplateFieldId = fieldId,
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
        context.ChangeTracker.Clear();

        var service = new TemplateFieldMappingService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

        var firstClientReferenceIds = new[]
        {
            "client-seg-2-1",
            "client-seg-2-2",
            "client-seg-2-3"
        };

        var firstSave = await SaveFieldSegmentsAsync(
            service,
            context,
            versionId,
            fieldId,
            new List<TemplateFieldSegmentLayoutUpdate>
            {
                new(existingSegmentId, fieldId, firstClientReferenceIds[0], 1, 1, 10, 20, 100, 20, true, "Left"),
                new(null, fieldId, firstClientReferenceIds[1], 2, 1, 10, 45, 100, 20, false, "Left"),
                new(null, fieldId, firstClientReferenceIds[2], 3, 1, 10, 70, 100, 20, false, "Left")
            });

        var savedIds = firstSave.Segments.Select(segment => segment.Id).ToArray();
        Assert.Equal(3, savedIds.Length);

        context.ChangeTracker.Clear();

        var secondClientReferenceIds = new[]
        {
            "client-seg-2-1b",
            "client-seg-2-2b",
            "client-seg-2-3b"
        };

        var secondSave = await SaveFieldSegmentsAsync(
            service,
            context,
            versionId,
            fieldId,
            new List<TemplateFieldSegmentLayoutUpdate>
            {
                new(savedIds[0], fieldId, secondClientReferenceIds[0], 1, 1, 12, 22, 100, 20, true, "Left"),
                new(savedIds[1], fieldId, secondClientReferenceIds[1], 2, 1, 12, 47, 100, 20, false, "Left"),
                new(savedIds[2], fieldId, secondClientReferenceIds[2], 3, 1, 12, 72, 100, 20, false, "Left")
            });

        var segments = await context.TemplateFieldSegments
            .Where(segment => segment.TemplateFieldId == fieldId)
            .OrderBy(segment => segment.Sequence)
            .ToListAsync();

        Assert.Equal(3, segments.Count);
        Assert.Equal([1, 2, 3], segments.Select(segment => segment.Sequence).ToArray());
        Assert.Equal(12, segments[0].PositionX);
        Assert.Equal(3, secondSave.Segments.Count);
    }

    private static async Task<TemplateFieldSegmentLayoutSaveResult> SaveFieldSegmentsAsync(
        TemplateFieldMappingService service,
        ApplicationDbContext context,
        Guid versionId,
        Guid fieldId,
        IReadOnlyList<TemplateFieldSegmentLayoutUpdate> updates)
    {
        var clientReferenceBySegment = new Dictionary<TemplateFieldSegment, string>();
        await service.EnsureEditableTemplateVersionAsync(versionId);
        await service.ProcessFieldSegmentsAsync(versionId, fieldId, updates, clientReferenceBySegment);
        await context.SaveChangesAsync();
        return await service.BuildSegmentLayoutSaveResultAsync(versionId, clientReferenceBySegment);
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
}
