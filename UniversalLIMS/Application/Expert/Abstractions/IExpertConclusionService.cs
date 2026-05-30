using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Application.Expert.Abstractions;

public interface IExpertConclusionService
{
    Task<ExpertConclusionReview?> GetReviewAsync(Guid sampleId, CancellationToken cancellationToken = default);

    Task MarkInProgressAsync(Guid sampleId, CancellationToken cancellationToken = default);

    Task<bool> ReturnToPendingReviewAsync(Guid sampleId, CancellationToken cancellationToken = default);

    Task<bool> ApproveAsync(
        Guid sampleId,
        string? notesUk,
        CancellationToken cancellationToken = default);

    Task<bool> ReturnForReworkAsync(
        Guid sampleId,
        string reworkReasonUk,
        CancellationToken cancellationToken = default);
}
