using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Repositories;

public sealed class LaboratoryDataFieldRepository : ILaboratoryDataFieldRepository
{
    private readonly ApplicationDbContext _context;

    public LaboratoryDataFieldRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<DataField?> GetByIdIncludingAnnulledAsync(
        Guid dataFieldId,
        CancellationToken cancellationToken = default)
    {
        return _context.DataFields
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(dataField => dataField.Id == dataFieldId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, DataField>> GetByIdsIncludingAnnulledAsync(
        IReadOnlyCollection<Guid> dataFieldIds,
        CancellationToken cancellationToken = default)
    {
        if (dataFieldIds.Count == 0)
        {
            return new Dictionary<Guid, DataField>();
        }

        var fields = await _context.DataFields
            .IgnoreQueryFilters()
            .Where(dataField => dataFieldIds.Contains(dataField.Id))
            .ToListAsync(cancellationToken);

        return fields.ToDictionary(dataField => dataField.Id);
    }
}
