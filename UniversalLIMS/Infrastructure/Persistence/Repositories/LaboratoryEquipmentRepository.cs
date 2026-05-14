using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Infrastructure.Persistence.Repositories;

public sealed class LaboratoryEquipmentRepository : ILaboratoryEquipmentRepository
{
    private readonly ApplicationDbContext _context;

    public LaboratoryEquipmentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Equipment?> GetByIdIncludingAnnulledAsync(
        Guid equipmentId,
        CancellationToken cancellationToken = default)
    {
        return _context.Equipment
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(equipment => equipment.Id == equipmentId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Equipment>> GetByIdsIncludingAnnulledAsync(
        IReadOnlyCollection<Guid> equipmentIds,
        CancellationToken cancellationToken = default)
    {
        if (equipmentIds.Count == 0)
        {
            return new Dictionary<Guid, Equipment>();
        }

        var equipment = await _context.Equipment
            .IgnoreQueryFilters()
            .Where(item => equipmentIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        return equipment.ToDictionary(item => item.Id);
    }
}
