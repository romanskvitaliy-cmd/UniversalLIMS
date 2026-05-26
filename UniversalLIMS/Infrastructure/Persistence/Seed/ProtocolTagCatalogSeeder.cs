using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Seed;

public static class ProtocolTagCatalogSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var existingKeys = await context.DataFields
            .IgnoreQueryFilters()
            .Select(dataField => dataField.Key)
            .ToListAsync(cancellationToken);

        var existingKeySet = existingKeys.ToHashSet(StringComparer.Ordinal);

        foreach (var tag in ProtocolTagCatalog.All)
        {
            if (existingKeySet.Contains(tag.Key))
            {
                continue;
            }

            context.DataFields.Add(new DataField
            {
                Key = tag.Key,
                DisplayNameUk = tag.DisplayNameUk,
                FieldType = tag.FieldType,
                Scope = tag.Scope,
                IsActive = true,
                IsRequired = false,
                IsSystem = false
            });

            existingKeySet.Add(tag.Key);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
