using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class LocalTemplateDocumentStorageTests : IDisposable
{
    private readonly string _contentRootPath = Path.Combine(Path.GetTempPath(), $"UniversalLIMS.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_RejectsNonPdfFile()
    {
        var storage = new LocalTemplateDocumentStorage(new TestWebHostEnvironment(_contentRootPath));
        await using var document = new MemoryStream("docx bytes"u8.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "template.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            document));
    }

    [Fact]
    public async Task SaveAsync_PersistsPdfAndReturnsMetadata()
    {
        var storage = new LocalTemplateDocumentStorage(new TestWebHostEnvironment(_contentRootPath));
        await using var document = new MemoryStream("%PDF-1.7"u8.ToArray());

        var metadata = await storage.SaveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "template.pdf",
            "application/pdf",
            document);

        Assert.Equal("template.pdf", metadata.OriginalFileName);
        Assert.Equal(document.Length, metadata.FileSizeBytes);
        Assert.True(await storage.ExistsAsync(metadata.StorageKey));
    }

    [Fact]
    public async Task SaveAsync_RejectsUnsupportedFile()
    {
        var storage = new LocalTemplateDocumentStorage(new TestWebHostEnvironment(_contentRootPath));
        await using var document = new MemoryStream("text"u8.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "template.txt",
            "text/plain",
            document));
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRootPath))
        {
            Directory.Delete(_contentRootPath, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
            WebRootFileProvider = new NullFileProvider();
        }

        public string ApplicationName { get; set; } = "UniversalLIMS.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }
    }
}
