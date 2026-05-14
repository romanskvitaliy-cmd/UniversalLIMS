using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

/// <summary>
/// Resolves dictionary <see cref="DataField"/> definitions for result entry (SSOT field metadata).
/// </summary>
public interface ILaboratoryDataFieldRepository
{
    Task<DataField?> GetByIdIncludingAnnulledAsync(Guid dataFieldId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, DataField>> GetByIdsIncludingAnnulledAsync(
        IReadOnlyCollection<Guid> dataFieldIds,
        CancellationToken cancellationToken = default);
}
