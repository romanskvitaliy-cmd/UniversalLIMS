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
            .Select(type => new { type.Id, type.Code, type.NameUk })
            .ToListAsync(cancellationToken);

        if (investigationTypeRows.Count == 0)
        {
            return;
        }

        var investigationTypes = investigationTypeRows
            .Select(type => new InvestigationTypeLinkTarget(type.Id, type.Code, type.NameUk))
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

        var scoredMatches = investigationTypes
            .Select(type => new
            {
                type.Id,
                Score = GetTemplateTypeMatchScore(normalizedTemplateName, type.Code, type.NameUk)
            })
            .Where(type => type.Score > 0)
            .ToList();

        if (scoredMatches.Count > 0)
        {
            var bestScore = scoredMatches.Max(type => type.Score);
            return scoredMatches
                .Where(type => type.Score == bestScore)
                .Select(type => type.Id)
                .ToList();
        }

        return investigationTypes.Count == 1
            ? [investigationTypes[0].Id]
            : [];
    }

    private sealed record InvestigationTypeLinkTarget(Guid Id, string Code, string NameUk);

    private static int GetTemplateTypeMatchScore(
        string normalizedTemplateName,
        string investigationTypeCode,
        string investigationTypeNameUk)
    {
        var typeName = NormalizeName(investigationTypeNameUk);

        if (normalizedTemplateName == typeName)
        {
            return 100;
        }

        if (normalizedTemplateName.Contains(typeName, StringComparison.Ordinal)
            || typeName.Contains(normalizedTemplateName, StringComparison.Ordinal))
        {
            return 80;
        }

        var score = investigationTypeCode.ToUpperInvariant() switch
        {
            "WATER" when normalizedTemplateName.Contains("вод", StringComparison.Ordinal) => 60,
            "FOOD" when normalizedTemplateName.Contains("харч", StringComparison.Ordinal)
                || normalizedTemplateName.Contains("страв", StringComparison.Ordinal)
                || normalizedTemplateName.Contains("напівфабрикат", StringComparison.Ordinal) => 60,
            "INDOOR_AIR" when normalizedTemplateName.Contains("повітр", StringComparison.Ordinal) => 60,
            _ => 0
        };

        var typeTokens = typeName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 3)
            .ToHashSet(StringComparer.Ordinal);
        var commonTokenCount = normalizedTemplateName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(token => typeTokens.Contains(token));

        return score + commonTokenCount;
    }

    private static string NormalizeName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
}
