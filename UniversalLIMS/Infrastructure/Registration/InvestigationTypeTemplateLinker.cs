using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

/// <summary>
/// Ensures published templates are linked to investigation types for order registration.
/// </summary>
public static class InvestigationTypeTemplateLinker
{
    public static async Task EnsureLinksForTemplateAsync(
        ApplicationDbContext context,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await context.Templates
            .AsNoTracking()
            .Where(item => item.Id == templateId && item.CurrentPublishedVersionId != null && !item.IsAnnulled)
            .Select(item => new { item.Id, item.NameUk })
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
        {
            return;
        }

        var investigationTypeRows = await context.InvestigationTypes
            .AsNoTracking()
            .Where(type => type.IsActive && !type.IsAnnulled)
            .Select(type => new { type.Id, type.NameUk })
            .ToListAsync(cancellationToken);

        if (investigationTypeRows.Count == 0)
        {
            return;
        }

        var investigationTypes = investigationTypeRows
            .Select(type => new InvestigationTypeLinkTarget(type.Id, type.NameUk))
            .ToList();

        var targetTypeIds = ResolveTargetInvestigationTypeIds(template.NameUk, investigationTypes);
        var existingLinks = await context.InvestigationTypeTemplates
            .AsNoTracking()
            .Where(link => link.TemplateId == templateId && targetTypeIds.Contains(link.InvestigationTypeId))
            .Select(link => link.InvestigationTypeId)
            .ToListAsync(cancellationToken);

        var maxSortByType = await context.InvestigationTypeTemplates
            .AsNoTracking()
            .Where(link => targetTypeIds.Contains(link.InvestigationTypeId))
            .GroupBy(link => link.InvestigationTypeId)
            .Select(group => new { InvestigationTypeId = group.Key, MaxSortOrder = group.Max(link => link.SortOrder) })
            .ToDictionaryAsync(item => item.InvestigationTypeId, item => item.MaxSortOrder, cancellationToken);

        foreach (var investigationTypeId in targetTypeIds)
        {
            if (existingLinks.Contains(investigationTypeId))
            {
                continue;
            }

            var nextSortOrder = maxSortByType.GetValueOrDefault(investigationTypeId) + 1;
            context.InvestigationTypeTemplates.Add(new InvestigationTypeTemplate
            {
                InvestigationTypeId = investigationTypeId,
                TemplateId = templateId,
                SortOrder = nextSortOrder,
                IsActive = true
            });
            maxSortByType[investigationTypeId] = nextSortOrder;
        }
    }

    public static async Task EnsureLinksForAllPublishedTemplatesAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var publishedTemplateIds = await context.Templates
            .AsNoTracking()
            .Where(template => template.CurrentPublishedVersionId != null && !template.IsAnnulled)
            .Select(template => template.Id)
            .ToListAsync(cancellationToken);

        foreach (var templateId in publishedTemplateIds)
        {
            await EnsureLinksForTemplateAsync(context, templateId, cancellationToken);
        }
    }

    private static List<Guid> ResolveTargetInvestigationTypeIds(
        string templateNameUk,
        IReadOnlyList<InvestigationTypeLinkTarget> investigationTypes)
    {
        var normalizedTemplateName = NormalizeName(templateNameUk);
        if (string.IsNullOrEmpty(normalizedTemplateName))
        {
            return investigationTypes.Select(type => type.Id).ToList();
        }

        var exactMatches = investigationTypes
            .Where(type => NormalizeName(type.NameUk) == normalizedTemplateName)
            .Select(type => type.Id)
            .ToList();

        if (exactMatches.Count > 0)
        {
            return exactMatches;
        }

        var partialMatches = investigationTypes
            .Where(type =>
            {
                var typeName = NormalizeName(type.NameUk);
                return typeName.Contains(normalizedTemplateName, StringComparison.Ordinal)
                    || normalizedTemplateName.Contains(typeName, StringComparison.Ordinal);
            })
            .Select(type => type.Id)
            .ToList();

        return partialMatches.Count > 0
            ? partialMatches
            : investigationTypes.Select(type => type.Id).ToList();
    }

    private sealed record InvestigationTypeLinkTarget(Guid Id, string NameUk);

    private static string NormalizeName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
}
