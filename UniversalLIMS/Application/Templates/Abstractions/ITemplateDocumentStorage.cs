namespace UniversalLIMS.Application.Templates.Abstractions;

public interface ITemplateDocumentStorage
{
    Task<StoredTemplateDocument> SaveAsync(
        Guid templateId,
        Guid templateVersionId,
        string originalFileName,
        string contentType,
        Stream documentStream,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}
