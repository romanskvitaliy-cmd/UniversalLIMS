namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ILaboratoryDocumentSubmissionService
{
    Task<SendDocumentToExpertResult> SendDocumentToExpertAsync(
        Guid orderDocumentId,
        CancellationToken cancellationToken = default);
}
