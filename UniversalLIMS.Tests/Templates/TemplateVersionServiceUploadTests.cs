using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Templates;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplateVersionServiceUploadTests
{
    [Fact]
    public async Task CreateDraftVersionAsync_DocUpload_SkipsDocxContentControlReader()
    {
        await using var context = CreateContext();
        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-DOC",
            NameUk = "DOC upload",
            Status = TemplateStatus.Draft
        });
        await context.SaveChangesAsync();

        var reader = new TrackingDocxContentControlReader();
        var converter = new TestWordToPdfDocumentConverter();
        var storage = new TestTemplateDocumentStorage();
        var service = new TemplateVersionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow),
            reader,
            storage,
            converter,
            new TestTemplatePublicationValidator());

        await using var document = new MemoryStream([0xD0, 0xCF, 0x11, 0xE0, 0x01, 0x02, 0x03, 0x04]);
        var versionId = await service.CreateDraftVersionAsync(
            templateId,
            "template.doc",
            "application/msword",
            document);

        var version = await context.TemplateVersions.SingleAsync(item => item.Id == versionId);
        Assert.Equal(TemplateDocumentFormat.Pdf, version.DocumentFormat);
        Assert.Equal("application/pdf", version.ContentType);
        Assert.Equal("template.pdf", version.OriginalFileName);
        Assert.Equal(0, reader.CallCount);
        Assert.Equal(1, converter.CallCount);
        Assert.Equal(".doc", converter.LastExtension);
    }

    [Fact]
    public async Task CreateDraftVersionAsync_DocxUpload_ReadsControlsThenConvertsFromStart()
    {
        await using var context = CreateContext();
        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-DOCX",
            NameUk = "DOCX upload",
            Status = TemplateStatus.Draft
        });
        await context.SaveChangesAsync();

        var reader = new TrackingDocxContentControlReader();
        var converter = new TestWordToPdfDocumentConverter();
        var storage = new TestTemplateDocumentStorage();
        var service = new TemplateVersionService(
            context,
            new TestCurrentUserService(),
            new TestDateTimeProvider(DateTime.UtcNow),
            reader,
            storage,
            converter,
            new TestTemplatePublicationValidator());

        await using var document = new MemoryStream([0x50, 0x4B, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
        var versionId = await service.CreateDraftVersionAsync(
            templateId,
            "template.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            document);

        var version = await context.TemplateVersions.SingleAsync(item => item.Id == versionId);
        Assert.Equal(TemplateDocumentFormat.Pdf, version.DocumentFormat);
        Assert.Equal("template.pdf", version.OriginalFileName);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(1, converter.CallCount);
        Assert.Equal(".docx", converter.LastExtension);
        Assert.Equal(8, converter.LastInputLength);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TrackingDocxContentControlReader : IDocxContentControlReader
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyCollection<DocxContentControlInfo>> ReadContentControlsAsync(
            Stream documentStream,
            CancellationToken cancellationToken = default)
        {
            CallCount += 1;
            _ = documentStream.ReadByte();
            return Task.FromResult<IReadOnlyCollection<DocxContentControlInfo>>([]);
        }
    }

    private sealed class TestWordToPdfDocumentConverter : IWordToPdfDocumentConverter
    {
        public int CallCount { get; private set; }

        public string? LastExtension { get; private set; }

        public int LastInputLength { get; private set; }

        public async Task<MemoryStream> ConvertAsync(
            Stream wordDocumentStream,
            string extension,
            CancellationToken cancellationToken = default)
        {
            CallCount += 1;
            LastExtension = extension;

            var pdfStream = new MemoryStream();
            await wordDocumentStream.CopyToAsync(pdfStream, cancellationToken);
            LastInputLength = (int)pdfStream.Length;
            pdfStream.Position = 0;
            return pdfStream;
        }
    }

    private sealed class TestTemplateDocumentStorage : ITemplateDocumentStorage
    {
        public Task<StoredTemplateDocument> SaveAsync(
            Guid templateId,
            Guid templateVersionId,
            string originalFileName,
            string contentType,
            Stream documentStream,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StoredTemplateDocument(
                "templates/test.pdf",
                originalFileName,
                contentType,
                documentStream.Length,
                new string('a', 64)));
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
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
