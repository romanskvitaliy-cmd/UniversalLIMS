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

        var entries = await QueryForKey(fieldContext.BranchId, fieldContext.DataFieldId, fieldContext.NormalizedTag)
            .OrderByDescending(entry => entry.UsageCount)
            .ThenByDescending(entry => entry.UpdatedAtUtc ?? entry.CreatedAtUtc)
            .ThenBy(entry => entry.SortOrder)
            .Take(FieldTextLibraryNormalizer.MaxEntriesPerKey)
            .ToListAsync(cancellationToken);

        return new FieldTextLibraryListResult
        {
            Entries = entries.Select(MapToDto).ToList(),
            TotalCount = entries.Count
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

        var existing = await FindActiveByHashTrackedAsync(
            fieldContext.BranchId,
            fieldContext.DataFieldId,
            fieldContext.NormalizedTag,
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

        var activeCount = await CountActiveForKeyAsync(
            fieldContext.BranchId,
            fieldContext.DataFieldId,
            fieldContext.NormalizedTag,
            cancellationToken);

        if (activeCount >= FieldTextLibraryNormalizer.MaxEntriesPerKey)
        {
            throw new InvalidOperationException(
                $"Досягнуто ліміт {FieldTextLibraryNormalizer.MaxEntriesPerKey} записів для цього поля у філії.");
        }

        var entry = new FieldTextLibraryEntry
        {
            Id = Guid.NewGuid(),
            BranchId = fieldContext.BranchId,
            DataFieldId = fieldContext.DataFieldId,
            NormalizedTag = fieldContext.DataFieldId.HasValue ? null : fieldContext.NormalizedTag,
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

        EnsureEntryMatchesFieldKey(entry, fieldContext);

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
            fieldContext.BranchId,
            fieldContext.DataFieldId,
            fieldContext.NormalizedTag,
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

        EnsureEntryMatchesFieldKey(entry, fieldContext);

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

        EnsureEntryMatchesFieldKey(entry, fieldContext);
        entry.UsageCount += 1;
        entry.UpdatedAtUtc = _dateTimeProvider.UtcNow;
        entry.UpdatedByUserId = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<FieldTextLibraryEntry> QueryForKey(
        Guid branchId,
        Guid? dataFieldId,
        string? normalizedTag)
    {
        if (dataFieldId.HasValue)
        {
            return _context.FieldTextLibraryEntries
                .AsNoTracking()
                .Where(entry => entry.BranchId == branchId && entry.DataFieldId == dataFieldId);
        }

        return _context.FieldTextLibraryEntries
            .AsNoTracking()
            .Where(entry =>
                entry.BranchId == branchId
                && entry.DataFieldId == null
                && entry.NormalizedTag == normalizedTag);
    }

    private async Task<FieldTextLibraryEntry?> FindActiveByHashAsync(
        Guid branchId,
        Guid? dataFieldId,
        string? normalizedTag,
        string bodyHash,
        CancellationToken cancellationToken)
    {
        return await QueryForKey(branchId, dataFieldId, normalizedTag)
            .FirstOrDefaultAsync(entry => entry.NormalizedBodyHash == bodyHash, cancellationToken);
    }

    private async Task<FieldTextLibraryEntry?> FindActiveByHashTrackedAsync(
        Guid branchId,
        Guid? dataFieldId,
        string? normalizedTag,
        string bodyHash,
        CancellationToken cancellationToken)
    {
        if (dataFieldId.HasValue)
        {
            return await _context.FieldTextLibraryEntries
                .FirstOrDefaultAsync(
                    entry =>
                        entry.BranchId == branchId
                        && entry.DataFieldId == dataFieldId
                        && entry.NormalizedBodyHash == bodyHash,
                    cancellationToken);
        }

        return await _context.FieldTextLibraryEntries
            .FirstOrDefaultAsync(
                entry =>
                    entry.BranchId == branchId
                    && entry.DataFieldId == null
                    && entry.NormalizedTag == normalizedTag
                    && entry.NormalizedBodyHash == bodyHash,
                cancellationToken);
    }

    private async Task<int> CountActiveForKeyAsync(
        Guid branchId,
        Guid? dataFieldId,
        string? normalizedTag,
        CancellationToken cancellationToken)
    {
        return await QueryForKey(branchId, dataFieldId, normalizedTag).CountAsync(cancellationToken);
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
        var normalizedTag = FieldTextLibraryNormalizer.NormalizeTag(field.Tag);

        return new FieldLibraryFieldContext(
            branchId,
            field.DataFieldId,
            field.DataFieldId.HasValue ? null : normalizedTag);
    }

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
        FieldLibraryFieldContext fieldContext)
    {
        if (fieldContext.DataFieldId.HasValue)
        {
            if (entry.DataFieldId != fieldContext.DataFieldId)
            {
                throw new InvalidOperationException("Запис не належить до цього поля.");
            }

            return;
        }

        if (!string.Equals(entry.NormalizedTag, fieldContext.NormalizedTag, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Запис не належить до цього поля.");
        }
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
                : string.Empty
        };

    private sealed record FieldLibraryFieldContext(
        Guid BranchId,
        Guid? DataFieldId,
        string? NormalizedTag);
}
