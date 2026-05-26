using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IFieldTextLibraryService
{
    Task<FieldTextLibraryListResult> ListForFieldAsync(
        Guid templateVersionId,
        Guid templateFieldId,
        Guid? orderId,
        CancellationToken cancellationToken = default);

    Task<FieldTextLibraryMutationResult> UpsertAsync(
        Guid templateVersionId,
        FieldTextLibraryUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<FieldTextLibraryMutationResult> UpdateAsync(
        Guid templateVersionId,
        Guid entryId,
        FieldTextLibraryUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task AnnulAsync(
        Guid templateVersionId,
        Guid entryId,
        Guid templateFieldId,
        Guid? orderId,
        CancellationToken cancellationToken = default);

    Task RecordUsageAsync(
        Guid templateVersionId,
        Guid entryId,
        Guid templateFieldId,
        Guid? orderId,
        CancellationToken cancellationToken = default);
}
