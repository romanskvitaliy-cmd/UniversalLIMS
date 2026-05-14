using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

/// <summary>
/// Resolves laboratory equipment used for ISO 17025 traceability on result entry.
/// </summary>
public interface ILaboratoryEquipmentRepository
{
    Task<Equipment?> GetByIdIncludingAnnulledAsync(Guid equipmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, Equipment>> GetByIdsIncludingAnnulledAsync(
        IReadOnlyCollection<Guid> equipmentIds,
        CancellationToken cancellationToken = default);
}
