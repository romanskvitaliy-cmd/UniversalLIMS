using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class ResultFieldPermissionService : IResultFieldPermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveLimsRoleService _activeRole;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ResultFieldPermissionService(
        ApplicationDbContext context,
        IActiveLimsRoleService activeRole,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _activeRole = activeRole;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> CanWriteAsync(
        Guid sampleId,
        Guid dataFieldId,
        CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var role = _activeRole.ResolveActiveRole(user);
        if (string.Equals(role, LimsRoles.SystemAdministrator, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(role, LimsRoles.LaboratoryTechnician, StringComparison.Ordinal))
        {
            return false;
        }

        var investigationTypeId = await _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Id == sampleId && !sample.IsAnnulled)
            .Select(sample => sample.InvestigationTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (investigationTypeId == Guid.Empty)
        {
            return false;
        }

        var hasPublishedTemplates = await (
            from link in _context.InvestigationTypeTemplates.AsNoTracking()
            join version in _context.TemplateVersions.AsNoTracking()
                on link.TemplateId equals version.TemplateId
            where link.InvestigationTypeId == investigationTypeId
                  && link.IsActive
                  && version.Status == TemplateVersionStatus.Published
                  && !version.IsAnnulled
            select version.Id)
            .AnyAsync(cancellationToken);

        var templateFieldIds = await (
            from link in _context.InvestigationTypeTemplates.AsNoTracking()
            join version in _context.TemplateVersions.AsNoTracking()
                on link.TemplateId equals version.TemplateId
            join field in _context.TemplateFields.AsNoTracking()
                on version.Id equals field.TemplateVersionId
            where link.InvestigationTypeId == investigationTypeId
                  && link.IsActive
                  && version.Status == TemplateVersionStatus.Published
                  && !version.IsAnnulled
                  && !field.IsAnnulled
                  && field.DataFieldId == dataFieldId
            select field.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (templateFieldIds.Count == 0)
        {
            // Backward-compatible fallback: allow writes when no published templates
            // are linked for the investigation type yet.
            return !hasPublishedTemplates;
        }

        return await _context.TemplateFieldPermissions
            .AsNoTracking()
            .AnyAsync(
                permission =>
                    templateFieldIds.Contains(permission.TemplateFieldId)
                    && permission.RoleName == role
                    && !permission.IsAnnulled
                    && permission.AccessLevel >= FieldAccessLevel.Write,
                cancellationToken);
    }
}
