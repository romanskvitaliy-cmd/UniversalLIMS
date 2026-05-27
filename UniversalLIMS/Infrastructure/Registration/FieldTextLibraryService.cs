using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class FieldTextLibraryService : IFieldTextLibraryService
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateFieldPermissionService _fieldPermissions;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public FieldTextLibraryService(
        ApplicationDbContext context,
        ITemplateFieldPermissionService fieldPermissions,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _fieldPermissions = fieldPermissions;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<FieldTextLibraryListResult> ListForFieldAsync(
        Guid templateVersionId,
        Guid templateFieldId,
        Guid? orderId,
        CancellationToken cancellationToken = default)
    {
        var fieldContext = await ResolveFieldContextAsync(
            templateVersionId,
            templateFieldId,
            orderId,
            requireWrite: false,
            cancellationToken);

        var entries = await QueryForList(fieldContext, templateVersionId)
            .OrderByDescending(entry => entry.UsageCount)
            .ThenByDescending(entry => entry.UpdatedAtUtc ?? entry.CreatedAtUtc)
            .ThenBy(entry => entry.SortOrder)
            .Take(FieldTextLibraryNormalizer.MaxEntriesPerKey)
            .ToListAsync(cancellationToken);

        return new FieldTextLibraryListResult
        {
            Entries = entries.Select(MapToDto).ToList(),
            TotalCount = entries.Count,
            FieldTag = fieldContext.FieldTag
        };
    }

    public async Task<FieldTextLibraryMutationResult> UpsertAsync(
        Guid templateVersionId,
        FieldTextLibraryUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var fieldContext = await ResolveFieldContextAsync(
            templateVersionId,
            request.TemplateFieldId,
            request.OrderId,
            requireWrite: true,
            cancellationToken);

        var normalizedBody = FieldTextLibraryNormalizer.NormalizeBody(request.Body);
        if (normalizedBody.Length == 0)
        {
            throw new InvalidOperationException("Текст бібліотеки не може бути порожнім.");
        }

        if (normalizedBody.Length > FieldTextLibraryNormalizer.MaxBodyLength)
        {
            throw new InvalidOperationException(
                $"Текст перевищує {FieldTextLibraryNormalizer.MaxBodyLength} символів.");
        }

        var bodyHash = FieldTextLibraryNormalizer.ComputeBodyHash(normalizedBody);
        var shortLabel = BuildShortLabel(request.ShortLabel, normalizedBody);
        var timestampUtc = _dateTimeProvider.UtcNow;
        var userId = _currentUser.UserId;

        var scopeVersionId = request.ScopeToTemplateVersion ? templateVersionId : (Guid?)null;

        var existing = await FindActiveByHashTrackedAsync(
            fieldContext,
            scopeVersionId,
            bodyHash,
            cancellationToken);

        if (existing is not null)
        {
            existing.UsageCount += 1;
            existing.UpdatedAtUtc = timestampUtc;
            existing.UpdatedByUserId = userId;
            if (!string.IsNullOrWhiteSpace(shortLabel))
            {
                existing.ShortLabel = shortLabel;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new FieldTextLibraryMutationResult
            {
                Entry = MapToDto(existing),
                Created = false,
                Message = "Такий текст уже є в бібліотеці — оновлено лічильник використання."
            };
        }

        var activeCount = await CountActiveForScopeAsync(
            fieldContext,
            scopeVersionId,
            cancellationToken);

        if (activeCount >= FieldTextLibraryNormalizer.MaxEntriesPerKey)
        {
            throw new InvalidOperationException(
                $"Досягнуто ліміт {FieldTextLibraryNormalizer.MaxEntriesPerKey} записів для цього поля у філії.");
        }

        var (storageDataFieldId, storageNormalizedTag) = ResolveEntryStorageKeys(fieldContext);
        var entry = new FieldTextLibraryEntry
        {
            Id = Guid.NewGuid(),
            BranchId = fieldContext.BranchId,
            DataFieldId = storageDataFieldId,
            TemplateVersionId = scopeVersionId,
            NormalizedTag = storageNormalizedTag,
            Body = normalizedBody,
            NormalizedBodyHash = bodyHash,
            ShortLabel = shortLabel,
            UsageCount = 1,
            SortOrder = activeCount + 1,
            CreatedAtUtc = timestampUtc,
            CreatedByUserId = userId,
            UpdatedAtUtc = timestampUtc,
            UpdatedByUserId = userId
        };

        _context.FieldTextLibraryEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);

        return new FieldTextLibraryMutationResult
        {
            Entry = MapToDto(entry),
            Created = true,
            Message = "Додано до бібліотеки."
        };
    }

    public async Task<FieldTextLibraryMutationResult> UpdateAsync(
        Guid templateVersionId,
        Guid entryId,
        FieldTextLibraryUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var fieldContext = await ResolveFieldContextAsync(
            templateVersionId,
            request.TemplateFieldId,
            request.OrderId,
            requireWrite: true,
            cancellationToken);

        var entry = await _context.FieldTextLibraryEntries
            .FirstOrDefaultAsync(
                item => item.Id == entryId && item.BranchId == fieldContext.BranchId,
                cancellationToken)
            ?? throw new InvalidOperationException("Запис бібліотеки не знайдено.");

        EnsureEntryMatchesFieldKey(entry, fieldContext, templateVersionId);

        var normalizedBody = FieldTextLibraryNormalizer.NormalizeBody(request.Body);
        if (normalizedBody.Length == 0)
        {
            throw new InvalidOperationException("Текст бібліотеки не може бути порожнім.");
        }

        if (normalizedBody.Length > FieldTextLibraryNormalizer.MaxBodyLength)
        {
            throw new InvalidOperationException(
                $"Текст перевищує {FieldTextLibraryNormalizer.MaxBodyLength} символів.");
        }

        var bodyHash = FieldTextLibraryNormalizer.ComputeBodyHash(normalizedBody);
        var duplicate = await FindActiveByHashAsync(
            fieldContext,
            entry.TemplateVersionId,
            bodyHash,
            cancellationToken);

        if (duplicate is not null && duplicate.Id != entry.Id)
        {
            throw new InvalidOperationException("Такий текст уже є в бібліотеці для цього поля.");
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersionBase64))
        {
            ApplyRowVersion(entry, request.RowVersionBase64);
        }

        entry.Body = normalizedBody;
        entry.NormalizedBodyHash = bodyHash;
        entry.ShortLabel = BuildShortLabel(request.ShortLabel, normalizedBody);
        entry.UpdatedAtUtc = _dateTimeProvider.UtcNow;
        entry.UpdatedByUserId = _currentUser.UserId;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Запис змінив інший користувач. Оновіть список бібліотеки та спробуйте знову.");
        }

        return new FieldTextLibraryMutationResult
        {
            Entry = MapToDto(entry),
            Created = false,
            Message = "Запис бібліотеки оновлено."
        };
    }

    public async Task AnnulAsync(
        Guid templateVersionId,
        Guid entryId,
        Guid templateFieldId,
        Guid? orderId,
        CancellationToken cancellationToken = default)
    {
        var fieldContext = await ResolveFieldContextAsync(
            templateVersionId,
            templateFieldId,
            orderId,
            requireWrite: true,
            cancellationToken);

        var entry = await _context.FieldTextLibraryEntries
            .FirstOrDefaultAsync(
                item => item.Id == entryId && item.BranchId == fieldContext.BranchId,
                cancellationToken)
            ?? throw new InvalidOperationException("Запис бібліотеки не знайдено.");

        EnsureEntryMatchesFieldKey(entry, fieldContext, templateVersionId);

        var timestampUtc = _dateTimeProvider.UtcNow;
        entry.IsAnnulled = true;
        entry.AnnulledAtUtc = timestampUtc;
        entry.AnnulledByUserId = _currentUser.UserId;
        entry.AnnulmentReason = "Removed from fill panel library.";
        entry.UpdatedAtUtc = timestampUtc;
        entry.UpdatedByUserId = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordUsageAsync(
        Guid templateVersionId,
        Guid entryId,
        Guid templateFieldId,
        Guid? orderId,
        CancellationToken cancellationToken = default)
    {
        var fieldContext = await ResolveFieldContextAsync(
            templateVersionId,
            templateFieldId,
            orderId,
            requireWrite: false,
            cancellationToken);

        var entry = await _context.FieldTextLibraryEntries
            .FirstOrDefaultAsync(
                item => item.Id == entryId && item.BranchId == fieldContext.BranchId,
                cancellationToken);

        if (entry is null)
        {
            return;
        }

        EnsureEntryMatchesFieldKey(entry, fieldContext, templateVersionId);
        entry.UsageCount += 1;
        entry.UpdatedAtUtc = _dateTimeProvider.UtcNow;
        entry.UpdatedByUserId = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<FieldTextLibraryEntry> ApplyFieldKeyFilter(
        IQueryable<FieldTextLibraryEntry> query,
        FieldLibraryFieldContext fieldContext)
    {
        if (!string.IsNullOrEmpty(fieldContext.NormalizedTag))
        {
            var tag = fieldContext.NormalizedTag;
            var legacyDataFieldId = fieldContext.LegacyDataFieldId;

            return query.Where(entry =>
                entry.NormalizedTag == tag
                || (legacyDataFieldId.HasValue
                    && entry.DataFieldId == legacyDataFieldId
                    && entry.NormalizedTag == null));
        }

        if (fieldContext.LegacyDataFieldId.HasValue)
        {
            return query.Where(entry => entry.DataFieldId == fieldContext.LegacyDataFieldId);
        }

        return query.Where(_ => false);
    }

    private IQueryable<FieldTextLibraryEntry> QueryForList(
        FieldLibraryFieldContext fieldContext,
        Guid templateVersionId)
    {
        var query = _context.FieldTextLibraryEntries
            .AsNoTracking()
            .Where(entry =>
                entry.BranchId == fieldContext.BranchId
                && (entry.TemplateVersionId == null || entry.TemplateVersionId == templateVersionId));

        return ApplyFieldKeyFilter(query, fieldContext);
    }

    private IQueryable<FieldTextLibraryEntry> QueryForScope(
        FieldLibraryFieldContext fieldContext,
        Guid? scopeTemplateVersionId,
        bool asNoTracking)
    {
        var query = asNoTracking
            ? _context.FieldTextLibraryEntries.AsNoTracking()
            : _context.FieldTextLibraryEntries.AsQueryable();

        query = query.Where(entry =>
            entry.BranchId == fieldContext.BranchId
            && entry.TemplateVersionId == scopeTemplateVersionId);

        return ApplyFieldKeyFilter(query, fieldContext);
    }

    private async Task<FieldTextLibraryEntry?> FindActiveByHashAsync(
        FieldLibraryFieldContext fieldContext,
        Guid? scopeTemplateVersionId,
        string bodyHash,
        CancellationToken cancellationToken)
    {
        return await QueryForScope(fieldContext, scopeTemplateVersionId, asNoTracking: true)
            .FirstOrDefaultAsync(entry => entry.NormalizedBodyHash == bodyHash, cancellationToken);
    }

    private Task<FieldTextLibraryEntry?> FindActiveByHashTrackedAsync(
        FieldLibraryFieldContext fieldContext,
        Guid? scopeTemplateVersionId,
        string bodyHash,
        CancellationToken cancellationToken)
    {
        return QueryForScope(fieldContext, scopeTemplateVersionId, asNoTracking: false)
            .FirstOrDefaultAsync(entry => entry.NormalizedBodyHash == bodyHash, cancellationToken);
    }

    private async Task<int> CountActiveForScopeAsync(
        FieldLibraryFieldContext fieldContext,
        Guid? scopeTemplateVersionId,
        CancellationToken cancellationToken)
    {
        return await QueryForScope(fieldContext, scopeTemplateVersionId, asNoTracking: true)
            .CountAsync(cancellationToken);
    }

    private async Task<FieldLibraryFieldContext> ResolveFieldContextAsync(
        Guid templateVersionId,
        Guid templateFieldId,
        Guid? orderId,
        bool requireWrite,
        CancellationToken cancellationToken)
    {
        var field = await _context.TemplateFields
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == templateFieldId
                        && item.TemplateVersionId == templateVersionId
                        && !item.IsAnnulled,
                cancellationToken)
            ?? throw new InvalidOperationException("Поле шаблону не знайдено.");

        var accessByFieldId = await _fieldPermissions.GetFieldAccessLevelsForVersionAsync(
            templateVersionId,
            cancellationToken);

        if (!accessByFieldId.TryGetValue(field.Id, out var accessLevel))
        {
            accessLevel = FieldAccessLevel.None;
        }

        var required = requireWrite ? FieldAccessLevel.Write : FieldAccessLevel.Read;
        if (accessLevel < required)
        {
            throw new InvalidOperationException(
                requireWrite
                    ? "Немає права Write для цього поля."
                    : "Немає доступу до цього поля.");
        }

        var branchId = await ResolveBranchIdAsync(orderId, cancellationToken);
        var normalizedTag = ResolveLibraryNormalizedTag(field);
        var fieldTag = field.Tag.Trim();

        return new FieldLibraryFieldContext(
            branchId,
            normalizedTag,
            field.DataFieldId,
            fieldTag);
    }

    private static string? ResolveLibraryNormalizedTag(TemplateField field)
    {
        if (!string.IsNullOrWhiteSpace(field.Tag))
        {
            return FieldTextLibraryNormalizer.NormalizeTag(field.Tag);
        }

        if (!string.IsNullOrWhiteSpace(field.NormalizedTag))
        {
            return field.NormalizedTag.Trim().ToUpperInvariant();
        }

        return null;
    }

    private static (Guid? DataFieldId, string? NormalizedTag) ResolveEntryStorageKeys(
        FieldLibraryFieldContext fieldContext) =>
        string.IsNullOrEmpty(fieldContext.NormalizedTag)
            ? (fieldContext.LegacyDataFieldId, null)
            : (null, fieldContext.NormalizedTag);

    private async Task<Guid> ResolveBranchIdAsync(Guid? orderId, CancellationToken cancellationToken)
    {
        if (orderId.HasValue)
        {
            var orderBranchId = await _context.Orders
                .AsNoTracking()
                .Where(order => order.Id == orderId.Value)
                .Select(order => (Guid?)order.BranchId)
                .FirstOrDefaultAsync(cancellationToken);

            if (orderBranchId.HasValue)
            {
                return orderBranchId.Value;
            }
        }

        if (_currentUser.BranchId is Guid userBranchId)
        {
            return userBranchId;
        }

        throw new InvalidOperationException("Не вдалося визначити філію для бібліотеки текстів.");
    }

    private static void EnsureEntryMatchesFieldKey(
        FieldTextLibraryEntry entry,
        FieldLibraryFieldContext fieldContext,
        Guid templateVersionId)
    {
        if (!EntryMatchesFieldKey(entry, fieldContext))
        {
            throw new InvalidOperationException("Запис не належить до цього поля.");
        }

        if (entry.TemplateVersionId.HasValue && entry.TemplateVersionId != templateVersionId)
        {
            throw new InvalidOperationException("Запис належить до іншої версії шаблону.");
        }
    }

    private static bool EntryMatchesFieldKey(
        FieldTextLibraryEntry entry,
        FieldLibraryFieldContext fieldContext)
    {
        if (!string.IsNullOrEmpty(fieldContext.NormalizedTag))
        {
            if (string.Equals(entry.NormalizedTag, fieldContext.NormalizedTag, StringComparison.Ordinal))
            {
                return true;
            }

            return fieldContext.LegacyDataFieldId.HasValue
                && entry.DataFieldId == fieldContext.LegacyDataFieldId
                && entry.NormalizedTag is null;
        }

        return fieldContext.LegacyDataFieldId.HasValue
            && entry.DataFieldId == fieldContext.LegacyDataFieldId;
    }

    private static string BuildShortLabel(string? requested, string normalizedBody)
    {
        var trimmed = requested?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed.Length > FieldTextLibraryNormalizer.MaxShortLabelLength
                ? trimmed[..FieldTextLibraryNormalizer.MaxShortLabelLength]
                : trimmed;
        }

        return FieldTextLibraryNormalizer.BuildDefaultShortLabel(normalizedBody);
    }

    private static void ApplyRowVersion(FieldTextLibraryEntry entry, string rowVersionBase64)
    {
        try
        {
            entry.RowVersion = Convert.FromBase64String(rowVersionBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Невалідна версія запису (RowVersion).");
        }
    }

    private static FieldTextLibraryEntryDto MapToDto(FieldTextLibraryEntry entry) =>
        new()
        {
            Id = entry.Id,
            Body = entry.Body,
            ShortLabel = entry.ShortLabel,
            UsageCount = entry.UsageCount,
            RowVersionBase64 = entry.RowVersion.Length > 0
                ? Convert.ToBase64String(entry.RowVersion)
                : string.Empty,
            ScopeToTemplateVersion = entry.TemplateVersionId.HasValue
        };

    private sealed record FieldLibraryFieldContext(
        Guid BranchId,
        string? NormalizedTag,
        Guid? LegacyDataFieldId,
        string FieldTag);
}
