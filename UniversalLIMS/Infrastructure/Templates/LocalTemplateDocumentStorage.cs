using System.Security.Cryptography;
using UniversalLIMS.Application.Templates.Abstractions;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class LocalTemplateDocumentStorage : ITemplateDocumentStorage
{
    private const string RootFolderName = "App_Data";
    private const string TemplateDocumentsFolderName = "TemplateDocuments";

    private readonly IWebHostEnvironment _environment;

    public LocalTemplateDocumentStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<StoredTemplateDocument> SaveAsync(
        Guid templateId,
        Guid templateVersionId,
        string originalFileName,
        string contentType,
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        var safeOriginalFileName = Path.GetFileName(originalFileName);
        var extension = Path.GetExtension(safeOriginalFileName);
        var normalizedExtension = extension.ToLowerInvariant();
        if (normalizedExtension != ".pdf")
        {
            throw new InvalidOperationException("У сховищі дозволено зберігати тільки PDF-файли.");
        }

        var storageKey = Path.Combine(
            templateId.ToString("N"),
            templateVersionId.ToString("N"),
            $"{Guid.NewGuid():N}{normalizedExtension}");

        var absolutePath = GetAbsolutePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var output = File.Create(absolutePath);
        using var sha256 = SHA256.Create();
        await using var hashingStream = new CryptoStream(output, sha256, CryptoStreamMode.Write);
        await documentStream.CopyToAsync(hashingStream, cancellationToken);
        hashingStream.FlushFinalBlock();

        var fileInfo = new FileInfo(absolutePath);
        var hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

        return new StoredTemplateDocument(
            storageKey,
            safeOriginalFileName,
            string.IsNullOrWhiteSpace(contentType)
                ? ResolveDefaultContentType(normalizedExtension)
                : contentType,
            fileInfo.Length,
            hash);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absolutePath = GetAbsolutePath(storageKey);
        Stream stream = File.OpenRead(absolutePath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(File.Exists(GetAbsolutePath(storageKey)));
    }

    private string GetAbsolutePath(string storageKey)
    {
        return Path.Combine(_environment.ContentRootPath, RootFolderName, TemplateDocumentsFolderName, storageKey);
    }

    private static string ResolveDefaultContentType(string normalizedExtension)
    {
        return "application/pdf";
    }
}
