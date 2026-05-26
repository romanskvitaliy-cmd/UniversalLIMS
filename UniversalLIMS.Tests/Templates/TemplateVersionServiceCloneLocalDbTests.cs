using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Templates;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplateVersionServiceCloneLocalDbTests
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=aspnet-UniversalLIMS-702cc72f-35ea-4eea-8b01-ef5113ae799a;Trusted_Connection=True;MultipleActiveResultSets=true";

    private static readonly Guid SourceVersionId = Guid.Parse("1C392339-8F67-4DB9-9A0C-1042744D5FFB");

    [Fact]
    public async Task CreateNewVersionAsync_LocalDb_ClonesFieldsFromVersionThree()
    {
        await using var context = CreateContext();
        var service = new TemplateVersionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow),
            new TestDocxContentControlReader(),
            new TestTemplateDocumentStorage(),
            new TestWordToPdfDocumentConverter(),
            new TestTemplatePublicationValidator());

        var clonedVersion = await service.CreateNewVersionAsync(
            SourceVersionId,
            "Automated Smart Clone verification");

        var persistedClone = await context.TemplateVersions
            .Include(version => version.Fields)
                .ThenInclude(field => field.Segments)
            .SingleAsync(version => version.Id == clonedVersion.Id);

        Assert.Equal(SourceVersionId, persistedClone.BasedOnTemplateVersionId);
        Assert.Equal(2, persistedClone.Fields.Count);
        Assert.Equal(2, persistedClone.Fields.Sum(field => field.Segments.Count));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
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

        public Task<PublicationValidationResult> ValidateRepublishAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PublicationValidationResult());
    }
}
