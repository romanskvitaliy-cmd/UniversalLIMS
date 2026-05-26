using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class TemplateFieldPermissionService : ITemplateFieldPermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveLimsRoleService _activeRole;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TemplateFieldPermissionService(
        ApplicationDbContext context,
        IActiveLimsRoleService activeRole,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _activeRole = activeRole;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyDictionary<Guid, FieldAccessLevel>> GetFieldAccessLevelsForVersionAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default)
    {
        var fieldIds = await _context.TemplateFields
            .AsNoTracking()
            .Where(field => field.TemplateVersionId == templateVersionId && !field.IsAnnulled)
            .Select(field => field.Id)
            .ToListAsync(cancellationToken);

        if (fieldIds.Count == 0)
        {
            return new Dictionary<Guid, FieldAccessLevel>();
        }

        var role = ResolveRole();
        if (string.IsNullOrWhiteSpace(role))
        {
            return fieldIds.ToDictionary(id => id, _ => FieldAccessLevel.None);
        }

        if (string.Equals(role, LimsRoles.SystemAdministrator, StringComparison.Ordinal))
        {
            return fieldIds.ToDictionary(id => id, _ => FieldAccessLevel.Write);
        }

        var versionHasConfiguredPermissions = await _context.TemplateFieldPermissions
            .AsNoTracking()
            .AnyAsync(
                permission =>
                    permission.TemplateField.TemplateVersionId == templateVersionId
                    && !permission.IsAnnulled,
                cancellationToken);

        if (!versionHasConfiguredPermissions)
        {
            return fieldIds.ToDictionary(id => id, _ => FieldAccessLevel.Write);
        }

        var permissionsByFieldId = await _context.TemplateFieldPermissions
            .AsNoTracking()
            .Where(
                permission =>
                    permission.TemplateField.TemplateVersionId == templateVersionId
                    && permission.RoleName == role
                    && !permission.IsAnnulled)
            .Select(permission => new { permission.TemplateFieldId, permission.AccessLevel })
            .ToDictionaryAsync(item => item.TemplateFieldId, item => item.AccessLevel, cancellationToken);

        return fieldIds.ToDictionary(
            id => id,
            id => permissionsByFieldId.GetValueOrDefault(id, FieldAccessLevel.None));
    }

    private string? ResolveRole()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return _activeRole.ResolveActiveRole(user);
    }
}
