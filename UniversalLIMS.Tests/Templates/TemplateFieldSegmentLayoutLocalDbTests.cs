using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Persistence.Interceptors;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplateFieldSegmentLayoutLocalDbTests
{
    [Fact]
    public async Task SaveFieldSegments_LocalDb_SingleSegmentCoordinateUpdate_Succeeds()
    {
        await using var context = CreateContext();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var existingSegmentId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-SEG-LDB-3",
            NameUk = "Segment LocalDB test 3",
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
            Sha256Hash = new string('c', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "Pressure",
                    Title = "Тиск",
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

        var service = CreateService(context);
        await SaveFieldSegmentsAsync(
            service,
            context,
            versionId,
            fieldId,
            new List<TemplateFieldSegmentLayoutUpdate>
            {
                new(existingSegmentId, fieldId, "client-seg-single", 1, 1, 15, 25, 100, 20, true, "Left")
            });

        var segment = await context.TemplateFieldSegments.SingleAsync(item => item.Id == existingSegmentId);
        Assert.Equal(15, segment.PositionX);
        Assert.Equal(25, segment.PositionY);
    }

    [Fact]
    public async Task SaveFieldSegments_LocalDb_AddOneNewSegment_Succeeds()
    {
        await using var context = CreateContext();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var existingSegmentId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-SEG-LDB-4",
            NameUk = "Segment LocalDB test 4",
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
            Sha256Hash = new string('d', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "Temperature",
                    Title = "Температура",
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

        var service = CreateService(context);
        await SaveFieldSegmentsAsync(
            service,
            context,
            versionId,
            fieldId,
            new List<TemplateFieldSegmentLayoutUpdate>
            {
                new(existingSegmentId, fieldId, "client-seg-add-1", 1, 1, 10, 20, 100, 20, true, "Left"),
                new(null, fieldId, "client-seg-add-2", 2, 1, 10, 45, 100, 20, false, "Left")
            });

        var segments = await context.TemplateFieldSegments
            .Where(segment => segment.TemplateFieldId == fieldId)
            .OrderBy(segment => segment.Sequence)
            .ToListAsync();

        Assert.Equal(2, segments.Count);
    }

    [Fact]
    public async Task SaveFieldSegments_LocalDb_SecondSaveWithReturnedIds_UpdatesLayouts()
    {
        await using var context = CreateContext();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var existingSegmentId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-SEG-LDB",
            NameUk = "Segment LocalDB test",
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

        var service = CreateService(context);
        var firstClientReferenceIds = new[]
        {
            "client-seg-ldb-1",
            "client-seg-ldb-2",
            "client-seg-ldb-3"
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
            "client-seg-ldb-1b",
            "client-seg-ldb-2b",
            "client-seg-ldb-3b"
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

    [Fact]
    public async Task SaveFieldSegments_LocalDb_AddThenShiftSecondSave_UpdatesLayouts()
    {
        await using var context = CreateContext();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var existingSegmentId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-SEG-LDB-2",
            NameUk = "Segment LocalDB test 2",
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

        var service = CreateService(context);
        var firstSave = await SaveFieldSegmentsAsync(
            service,
            context,
            versionId,
            fieldId,
            new List<TemplateFieldSegmentLayoutUpdate>
            {
                new(existingSegmentId, fieldId, "client-seg-shift-1", 1, 1, 10, 20, 100, 20, true, "Left"),
                new(null, fieldId, "client-seg-shift-2", 2, 1, 10, 45, 100, 20, false, "Left")
            });

        var savedIds = firstSave.Segments.Select(segment => segment.Id).ToArray();
        Assert.Equal(2, savedIds.Length);

        context.ChangeTracker.Clear();

        var secondSave = await SaveFieldSegmentsAsync(
            service,
            context,
            versionId,
            fieldId,
            new List<TemplateFieldSegmentLayoutUpdate>
            {
                new(savedIds[0], fieldId, "client-seg-shift-1b", 1, 1, 14, 24, 100, 20, true, "Left"),
                new(savedIds[1], fieldId, "client-seg-shift-2b", 2, 1, 14, 49, 100, 20, false, "Left")
            });

        var segments = await context.TemplateFieldSegments
            .Where(segment => segment.TemplateFieldId == fieldId)
            .OrderBy(segment => segment.Sequence)
            .ToListAsync();

        Assert.Equal(2, segments.Count);
        Assert.Equal(14, segments[0].PositionX);
        Assert.Equal(49, segments[1].PositionY);
        Assert.Equal(2, secondSave.Segments.Count);
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

    private static TemplateFieldMappingService CreateService(ApplicationDbContext context) =>
        new(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow));

    private static ApplicationDbContext CreateContext()
    {
        var databaseName = $"UniversalLIMS-SegTests-{Guid.NewGuid():N}";
        var connectionString =
            $"Server=(localdb)\\mssqllocaldb;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new SoftAnnulmentSaveChangesInterceptor(
                new TestCurrentUserService(),
                new TestDateTimeProvider(DateTime.UtcNow)))
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.Migrate();
        return context;
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
