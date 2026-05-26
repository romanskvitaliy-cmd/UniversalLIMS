using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Templates.Abstractions;

public interface ITemplateFieldPermissionService
{
    /// <summary>
    /// Ефективний рівень доступу до кожного поля версії шаблону для активної ролі користувача.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, FieldAccessLevel>> GetFieldAccessLevelsForVersionAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default);
}
