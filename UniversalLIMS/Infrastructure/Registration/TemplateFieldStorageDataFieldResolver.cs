using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

/// <summary>Визначає DataFieldId для збереження значення поля (семантичний ключ або workspace-GUID).</summary>
internal static class TemplateFieldStorageDataFieldResolver
{
    public static string WorkspaceDataFieldKey(Guid templateFieldId) =>
        templateFieldId.ToString("D");

    public static bool IsWorkspaceDataFieldKey(string dataFieldKey, Guid templateFieldId) =>
        string.Equals(dataFieldKey, WorkspaceDataFieldKey(templateFieldId), StringComparison.Ordinal);

    public static Guid? ResolveStorageDataFieldId(
        Guid templateFieldId,
        Guid? dataFieldId,
        string? dataFieldKey,
        IReadOnlyDictionary<string, Guid> workspaceDataFieldIdsByKey)
    {
        if (dataFieldId.HasValue
            && !string.IsNullOrWhiteSpace(dataFieldKey)
            && !IsWorkspaceDataFieldKey(dataFieldKey, templateFieldId))
        {
            return dataFieldId;
        }

        return workspaceDataFieldIdsByKey.TryGetValue(WorkspaceDataFieldKey(templateFieldId), out var workspaceId)
            ? workspaceId
            : dataFieldId;
    }

    public static async Task<Dictionary<Guid, Guid>> ResolveOrCreateStorageDataFieldIdsAsync(
        ApplicationDbContext context,
        IEnumerable<TemplateField> fields,
        CancellationToken cancellationToken)
    {
        var fieldList = fields.ToList();
        if (fieldList.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var mappedDataFieldIds = fieldList
            .Where(field => field.DataFieldId.HasValue)
            .Select(field => field.DataFieldId!.Value)
            .Distinct()
            .ToList();

        var dataFieldKeysById = mappedDataFieldIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.DataFields
                .Where(dataField => mappedDataFieldIds.Contains(dataField.Id) && dataField.IsActive)
                .ToDictionaryAsync(dataField => dataField.Id, dataField => dataField.Key, cancellationToken);

        var workspaceKeys = fieldList.Select(field => WorkspaceDataFieldKey(field.Id)).ToList();
        var existingWorkspaceByKey = await context.DataFields
            .Where(dataField => workspaceKeys.Contains(dataField.Key) && dataField.IsActive)
            .ToDictionaryAsync(dataField => dataField.Key, cancellationToken);

        var result = new Dictionary<Guid, Guid>();

        foreach (var field in fieldList)
        {
            if (field.DataFieldId.HasValue
                && dataFieldKeysById.TryGetValue(field.DataFieldId.Value, out var mappedKey)
                && !IsWorkspaceDataFieldKey(mappedKey, field.Id))
            {
                result[field.Id] = field.DataFieldId.Value;
                continue;
            }

            var workspaceKey = WorkspaceDataFieldKey(field.Id);
            if (existingWorkspaceByKey.TryGetValue(workspaceKey, out var existingWorkspace))
            {
                result[field.Id] = existingWorkspace.Id;
                continue;
            }

            var dataField = new DataField
            {
                Key = workspaceKey,
                DisplayNameUk = string.IsNullOrWhiteSpace(field.Title) ? field.Tag : field.Title.Trim(),
                FieldType = DataFieldType.Text,
                Scope = DataFieldScope.Registration,
                IsActive = true,
                IsRequired = field.IsRequired
            };

            context.DataFields.Add(dataField);
            existingWorkspaceByKey[workspaceKey] = dataField;
            result[field.Id] = dataField.Id;
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>Лише читання — не створює workspace DataField.</summary>
    public static async Task<Dictionary<Guid, Guid>> ResolveStorageDataFieldIdsReadOnlyAsync(
        ApplicationDbContext context,
        IEnumerable<TemplateField> fields,
        CancellationToken cancellationToken)
    {
        var fieldList = fields.ToList();
        if (fieldList.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var mappedDataFieldIds = fieldList
            .Where(field => field.DataFieldId.HasValue)
            .Select(field => field.DataFieldId!.Value)
            .Distinct()
            .ToList();

        var dataFieldKeysById = mappedDataFieldIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.DataFields
                .AsNoTracking()
                .Where(dataField => mappedDataFieldIds.Contains(dataField.Id) && dataField.IsActive)
                .ToDictionaryAsync(dataField => dataField.Id, dataField => dataField.Key, cancellationToken);

        var workspaceKeys = fieldList.Select(field => WorkspaceDataFieldKey(field.Id)).ToList();
        var existingWorkspaceByKey = await context.DataFields
            .AsNoTracking()
            .Where(dataField => workspaceKeys.Contains(dataField.Key) && dataField.IsActive)
            .ToDictionaryAsync(dataField => dataField.Key, dataField => dataField.Id, cancellationToken);

        var result = new Dictionary<Guid, Guid>();

        foreach (var field in fieldList)
        {
            if (field.DataFieldId.HasValue
                && dataFieldKeysById.TryGetValue(field.DataFieldId.Value, out var mappedKey)
                && !IsWorkspaceDataFieldKey(mappedKey, field.Id))
            {
                result[field.Id] = field.DataFieldId.Value;
                continue;
            }

            var workspaceKey = WorkspaceDataFieldKey(field.Id);
            if (existingWorkspaceByKey.TryGetValue(workspaceKey, out var workspaceId))
            {
                result[field.Id] = workspaceId;
            }
        }

        return result;
    }
}
