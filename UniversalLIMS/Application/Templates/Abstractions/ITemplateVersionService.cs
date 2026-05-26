using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Templates.Abstractions;

public interface ITemplateVersionService
{
    Task<Guid> CreateDraftVersionAsync(
        Guid templateId,
        string originalFileName,
        string contentType,
        Stream documentStream,
        Guid? copyFieldsFromVersionId = null,
        CancellationToken cancellationToken = default);

    Task CopyFieldsFromVersionAsync(
        Guid targetVersionId,
        Guid sourceVersionId,
        CancellationToken cancellationToken = default);

    Task RescanFieldsAsync(Guid templateVersionId, CancellationToken cancellationToken = default);

    Task<PublicationValidationResult> PublishAsync(
        Guid templateVersionId,
        string? publicationNotesUk,
        CancellationToken cancellationToken = default);

    /// <summary>Знову зробити поточною опублікованою версію, що була замінена (Superseded).</summary>
    Task<PublicationValidationResult> RepublishAsync(
        Guid templateVersionId,
        string? publicationNotesUk,
        CancellationToken cancellationToken = default);

    Task AnnulAsync(
        Guid templateVersionId,
        string annulmentReason,
        CancellationToken cancellationToken = default);

    Task<TemplateVersion> CreateNewVersionAsync(
        Guid previousVersionId,
        string changeReason,
        CancellationToken cancellationToken = default);
}
