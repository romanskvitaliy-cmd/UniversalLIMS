using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class TemplateFieldMappingService : ITemplateFieldMappingService
{
    private const int RetainedSequenceStagingBase = 100_000;
    private const int AnnulledSequenceStagingBase = 200_000;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TemplateFieldMappingService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task UpdateMappingsAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, Guid?> dataFieldIdsByFieldId,
        CancellationToken cancellationToken = default)
    {
        var version = await LoadEditableVersionAsync(templateVersionId, cancellationToken);

        var submittedDataFieldIds = dataFieldIdsByFieldId.Values
            .Where(dataFieldId => dataFieldId.HasValue)
            .Select(dataFieldId => dataFieldId!.Value)
            .ToHashSet();

        var activeDataFieldIds = await _context.DataFields
            .Where(dataField => submittedDataFieldIds.Contains(dataField.Id) && dataField.IsActive)
            .Select(dataField => dataField.Id)
            .ToListAsync(cancellationToken);

        if (activeDataFieldIds.Count != submittedDataFieldIds.Count)
        {
            throw new InvalidOperationException("Один або кілька DataField неактивні або анульовані.");
        }

        foreach (var field in version.Fields)
        {
            if (!dataFieldIdsByFieldId.TryGetValue(field.Id, out var dataFieldId))
            {
                continue;
            }

            field.DataFieldId = dataFieldId;
            field.Status = dataFieldId.HasValue ? TemplateFieldStatus.Mapped : TemplateFieldStatus.NewTag;
            field.LastMappedAtUtc = _dateTimeProvider.UtcNow;
            field.LastMappedByUserId = _currentUserService.UserId;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCapacityAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, TemplateFieldCapacityUpdate> capacityByFieldId,
        CancellationToken cancellationToken = default)
    {
        var version = await LoadEditableVersionAsync(templateVersionId, cancellationToken);

        foreach (var field in version.Fields)
        {
            if (!capacityByFieldId.TryGetValue(field.Id, out var capacity))
            {
                continue;
            }

            field.EstimatedCapacityChars = capacity.EstimatedCapacityChars;
            field.MaxLines = capacity.MaxLines;
            field.AllowMultiline = capacity.AllowMultiline;
            field.OverflowPolicy = capacity.OverflowPolicy;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLayoutAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, TemplateFieldLayoutUpdate> layoutByFieldId,
        CancellationToken cancellationToken = default)
    {
        var version = await LoadEditableVersionAsync(templateVersionId, cancellationToken);

        foreach (var field in version.Fields)
        {
            if (!layoutByFieldId.TryGetValue(field.Id, out var layout))
            {
                continue;
            }

            var segment = field.EnsurePrimarySegment();
            var nextPage = layout.PageNumber ?? segment.PageNumber;
            var nextX = layout.PositionX ?? segment.PositionX;
            var nextY = layout.PositionY ?? segment.PositionY;
            var nextWidth = layout.Width ?? segment.Width;
            var nextHeight = layout.Height ?? segment.Height;

            segment.PageNumber = Math.Max(1, nextPage);
            segment.PositionX = Math.Max(0, nextX);
            segment.PositionY = Math.Max(0, nextY);
            segment.Width = Math.Max(20, nextWidth);
            segment.Height = Math.Max(14, nextHeight);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTextOffsetsAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, TemplateFieldTextOffsetUpdate> offsetsByFieldId,
        CancellationToken cancellationToken = default)
    {
        if (offsetsByFieldId.Count == 0)
        {
            return;
        }

        var version = await LoadEditableVersionAsync(templateVersionId, cancellationToken);

        foreach (var field in version.Fields)
        {
            if (!offsetsByFieldId.TryGetValue(field.Id, out var offsets))
            {
                continue;
            }

            field.TextOffsetX = offsets.TextOffsetX;
            field.TextOffsetY = offsets.TextOffsetY;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureEditableTemplateVersionAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default)
    {
        var version = await _context.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(templateVersion => templateVersion.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            throw new InvalidOperationException("Версію шаблону не знайдено.");
        }

        EnsureWorkingVersion(version);
    }

    public async Task ProcessFieldSegmentsAsync(
        Guid templateVersionId,
        Guid fieldId,
        IReadOnlyList<TemplateFieldSegmentLayoutUpdate> segmentUpdates,
        IDictionary<TemplateFieldSegment, string> clientReferenceBySegment,
        CancellationToken cancellationToken = default)
    {
        var field = await _context.TemplateFields
            .IgnoreQueryFilters()
            .Include(templateField => templateField.Segments)
            .FirstOrDefaultAsync(templateField => templateField.Id == fieldId, cancellationToken);

        if (field is null)
        {
            throw new InvalidOperationException($"Поле шаблону {fieldId} не знайдено.");
        }

        if (field.IsAnnulled)
        {
            return;
        }

        if (field.TemplateVersionId != templateVersionId)
        {
            throw new InvalidOperationException(
                $"Поле шаблону {fieldId} належить іншій версії шаблону. Оновіть сторінку мапінгу (F5) і збережіть знову.");
        }

        var existingSegments = await _context.TemplateFieldSegments
            .Where(segment => segment.TemplateFieldId == fieldId)
            .ToListAsync(cancellationToken);

        if (segmentUpdates.Count == 0)
        {
            RemoveOrphanSegments(field, existingSegments);
            return;
        }

        var existingSegmentsById = existingSegments.ToDictionary(segment => segment.Id);
        var retainedSegments = new List<TemplateFieldSegment>(segmentUpdates.Count);
        var segmentsByClientReferenceId = new Dictionary<string, TemplateFieldSegment>(StringComparer.Ordinal);
        var newlyCreatedSegmentIds = new HashSet<Guid>();

        foreach (var segmentUpdate in segmentUpdates.OrderBy(segment => segment.Sequence))
        {
            if (string.IsNullOrWhiteSpace(segmentUpdate.ClientReferenceId))
            {
                throw new InvalidOperationException(
                    $"ClientReferenceId is required for segment sequence {segmentUpdate.Sequence} of field {field.Id}.");
            }

            var clientReferenceId = segmentUpdate.ClientReferenceId.Trim();
            TemplateFieldSegment? segment = null;
            if (segmentsByClientReferenceId.TryGetValue(clientReferenceId, out var resolvedByClientReference))
            {
                segment = resolvedByClientReference;
            }

            var createdSegment = false;
            if (segment is null)
            {
                segment = ResolveSegmentForUpdate(
                    segmentUpdate,
                    existingSegmentsById,
                    retainedSegments);
            }

            if (segment is null)
            {
                segment = new TemplateFieldSegment
                {
                    TemplateFieldId = field.Id
                };
                field.Segments.Add(segment);
                existingSegmentsById[segment.Id] = segment;
                createdSegment = true;
                newlyCreatedSegmentIds.Add(segment.Id);
            }

            segmentsByClientReferenceId[clientReferenceId] = segment;
            if (!createdSegment)
            {
                PrepareTrackedSegmentRowVersion(segment);
            }

            segment.PageNumber = Math.Max(1, segmentUpdate.PageNumber);
            segment.PositionX = Math.Max(0, segmentUpdate.PositionX);
            segment.PositionY = Math.Max(0, segmentUpdate.PositionY);
            segment.Width = Math.Max(20, segmentUpdate.Width);
            segment.Height = Math.Max(14, segmentUpdate.Height);
            segment.IsPrimary = segmentUpdate.IsPrimary;
            ApplySegmentTypography(segment, segmentUpdate);
            retainedSegments.Add(segment);
            clientReferenceBySegment[segment] = clientReferenceId;
        }

        var retainedSegmentIds = retainedSegments
            .Select(segment => segment.Id)
            .ToHashSet();
        var orphanSegments = existingSegments
            .Where(segment => !retainedSegmentIds.Contains(segment.Id))
            .ToList();

        RemoveOrphanSegments(field, orphanSegments);

        if (orphanSegments.Count > 0)
        {
            ReleaseActiveSequenceSlots(retainedSegments, RetainedSequenceStagingBase);
        }

        FinalizeWaterfallSequences(retainedSegments);
        FinalizePrimarySegment(field, retainedSegments);

        foreach (var segment in retainedSegments.Where(item => newlyCreatedSegmentIds.Contains(item.Id)))
        {
            _context.Entry(segment).State = EntityState.Added;
        }
    }

    public async Task<TemplateFieldSegmentLayoutSaveResult> BuildSegmentLayoutSaveResultAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<TemplateFieldSegment, string> clientReferenceBySegment,
        CancellationToken cancellationToken = default)
    {
        var clientReferenceBySegmentId = clientReferenceBySegment
            .ToDictionary(
                pair => pair.Key.Id,
                pair => pair.Value.Trim());

        var freshSegments = await _context.TemplateFieldSegments
            .AsNoTracking()
            .Where(segment =>
                segment.TemplateField.TemplateVersionId == templateVersionId
                && !segment.TemplateField.IsAnnulled)
            .OrderBy(segment => segment.TemplateField.SortOrder)
            .ThenBy(segment => segment.Sequence)
            .ToListAsync(cancellationToken);

        var savedSegments = freshSegments
            .Select(segment => new TemplateFieldSegmentSavedState(
                clientReferenceBySegmentId.TryGetValue(segment.Id, out var clientReferenceId)
                    ? clientReferenceId
                    : string.Empty,
                segment.Id,
                segment.TemplateFieldId,
                segment.Sequence,
                segment.PageNumber,
                segment.PositionX,
                segment.PositionY,
                segment.Width,
                segment.Height,
                segment.IsPrimary,
                segment.TextAlignment.ToString(),
                segment.FontSize,
                segment.FontName,
                segment.LineHeight,
                segment.HorizontalAlignment,
                segment.VerticalAlignment,
                segment.SvgPathData,
                segment.RowVersion))
            .ToList();

        var mapping = savedSegments
            .Where(segment => !string.IsNullOrWhiteSpace(segment.ClientReferenceId))
            .ToDictionary(
                segment => segment.ClientReferenceId,
                segment => new TemplateFieldSegmentIdentity(segment.Id, segment.RowVersion));

        return new TemplateFieldSegmentLayoutSaveResult(mapping, savedSegments);
    }

    private void RemoveOrphanSegments(TemplateField field, IReadOnlyList<TemplateFieldSegment> orphanSegments)
    {
        if (orphanSegments.Count == 0)
        {
            return;
        }

        ReleaseActiveSequenceSlots(orphanSegments, AnnulledSequenceStagingBase);
        foreach (var segment in orphanSegments)
        {
            field.Segments.Remove(segment);
            segment.AnnulmentReason = "Видалено в PDF overlay designer.";
        }

        _context.TemplateFieldSegments.RemoveRange(orphanSegments);
    }

    private static void ReleaseActiveSequenceSlots(
        IReadOnlyList<TemplateFieldSegment> segments,
        int stagingBase)
    {
        for (var index = 0; index < segments.Count; index++)
        {
            segments[index].Sequence = stagingBase + index + 1;
        }
    }

    private static void FinalizeWaterfallSequences(IReadOnlyList<TemplateFieldSegment> retainedSegments)
    {
        for (var index = 0; index < retainedSegments.Count; index++)
        {
            retainedSegments[index].Sequence = index + 1;
        }
    }

    private static void FinalizePrimarySegment(TemplateField field, IReadOnlyList<TemplateFieldSegment> retainedSegments)
    {
        if (retainedSegments.Count == 0)
        {
            return;
        }

        if (!retainedSegments.Any(segment => segment.IsPrimary))
        {
            retainedSegments[0].IsPrimary = true;
            return;
        }

        if (retainedSegments.Count(segment => segment.IsPrimary) <= 1)
        {
            return;
        }

        var primarySegment = retainedSegments.First(segment => segment.IsPrimary);
        foreach (var segment in retainedSegments)
        {
            segment.IsPrimary = segment.Id == primarySegment.Id;
        }
    }

    private static TemplateFieldSegment? ResolveSegmentForUpdate(
        TemplateFieldSegmentLayoutUpdate segmentUpdate,
        IReadOnlyDictionary<Guid, TemplateFieldSegment> existingSegmentsById,
        IReadOnlyCollection<TemplateFieldSegment> retainedSegments)
    {
        var unmatchedSegments = existingSegmentsById.Values
            .Where(segment => !retainedSegments.Contains(segment))
            .OrderBy(segment => segment.Sequence)
            .ThenBy(segment => segment.Id)
            .ToList();

        if (segmentUpdate.SegmentId is Guid segmentId &&
            segmentId != Guid.Empty &&
            existingSegmentsById.TryGetValue(segmentId, out var existingSegment) &&
            !retainedSegments.Contains(existingSegment))
        {
            return existingSegment;
        }

        if (segmentUpdate.Sequence > 0 && segmentUpdate.Sequence <= unmatchedSegments.Count)
        {
            return unmatchedSegments[segmentUpdate.Sequence - 1];
        }

        if (unmatchedSegments.Count == 0)
        {
            return null;
        }

        return unmatchedSegments
            .FirstOrDefault(segment => segment.Sequence == segmentUpdate.Sequence);
    }

    private void PrepareTrackedSegmentRowVersion(TemplateFieldSegment segment)
    {
        var entry = _context.Entry(segment);
        if (entry.State is EntityState.Detached or EntityState.Added)
        {
            return;
        }

        var rowVersion = segment.RowVersion;
        if (rowVersion is not { Length: > 0 })
        {
            return;
        }

        entry.Property(item => item.RowVersion).OriginalValue = rowVersion;
    }

    public async Task DeleteFieldsAsync(
        Guid templateVersionId,
        IReadOnlyCollection<Guid> fieldIds,
        CancellationToken cancellationToken = default)
    {
        if (fieldIds.Count == 0)
        {
            return;
        }

        var version = await LoadEditableVersionAsync(templateVersionId, cancellationToken);
        var ids = fieldIds.ToHashSet();

        foreach (var field in version.Fields.Where(item => ids.Contains(item.Id)))
        {
            field.IsAnnulled = true;
            field.AnnulledAtUtc = _dateTimeProvider.UtcNow;
            field.AnnulledByUserId = _currentUserService.UserId;
            field.AnnulmentReason = "Видалено в PDF overlay designer.";

            var activeSegments = field.Segments.ToList();
            if (activeSegments.Count > 0)
            {
                RemoveOrphanSegments(field, activeSegments);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePermissionsAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FieldAccessLevel>> accessLevelsByFieldId,
        CancellationToken cancellationToken = default)
    {
        var version = await _context.TemplateVersions
            .AsNoTracking()
            .Where(item => item.Id == templateVersionId)
            .Select(item => new { item.Status, FieldIds = item.Fields.Select(field => field.Id).ToList() })
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            throw new InvalidOperationException("Версію шаблону не знайдено.");
        }

        if (!CanUpdatePermissions(version.Status))
        {
            throw new InvalidOperationException(
                version.Status == TemplateVersionStatus.Superseded
                    ? "Замінену версію шаблону змінювати заборонено."
                    : "Права доступу для цієї версії шаблону змінювати заборонено.");
        }

        var validRoles = LimsRoles.All.ToHashSet(StringComparer.Ordinal);
        var timestampUtc = _dateTimeProvider.UtcNow;
        var userId = _currentUserService.UserId;
        var pendingInserts = new List<TemplateFieldPermission>();

        await AnnulDuplicateActivePermissionsAsync(version.FieldIds, cancellationToken);

        foreach (var fieldId in version.FieldIds)
        {
            if (!accessLevelsByFieldId.TryGetValue(fieldId, out var accessLevelsByRole))
            {
                continue;
            }

            foreach (var roleAccess in accessLevelsByRole.Where(roleAccess => validRoles.Contains(roleAccess.Key)))
            {
                var roleName = roleAccess.Key;
                var accessLevel = roleAccess.Value;

                var activeUpdated = await _context.TemplateFieldPermissions
                    .IgnoreQueryFilters()
                    .Where(permission =>
                        permission.TemplateFieldId == fieldId &&
                        permission.RoleName == roleName &&
                        !permission.IsAnnulled)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(permission => permission.AccessLevel, accessLevel)
                            .SetProperty(permission => permission.UpdatedAtUtc, timestampUtc)
                            .SetProperty(permission => permission.UpdatedByUserId, userId),
                        cancellationToken);

                if (activeUpdated > 0)
                {
                    continue;
                }

                var reactivated = await _context.TemplateFieldPermissions
                    .IgnoreQueryFilters()
                    .Where(permission =>
                        permission.TemplateFieldId == fieldId &&
                        permission.RoleName == roleName &&
                        permission.IsAnnulled)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(permission => permission.IsAnnulled, false)
                            .SetProperty(permission => permission.AnnulledAtUtc, (DateTime?)null)
                            .SetProperty(permission => permission.AnnulledByUserId, (string?)null)
                            .SetProperty(permission => permission.AnnulmentReason, (string?)null)
                            .SetProperty(permission => permission.AccessLevel, accessLevel)
                            .SetProperty(permission => permission.UpdatedAtUtc, timestampUtc)
                            .SetProperty(permission => permission.UpdatedByUserId, userId),
                        cancellationToken);

                if (reactivated > 0)
                {
                    continue;
                }

                pendingInserts.Add(new TemplateFieldPermission
                {
                    TemplateFieldId = fieldId,
                    RoleName = roleName,
                    AccessLevel = accessLevel
                });
            }
        }

        if (pendingInserts.Count == 0)
        {
            return;
        }

        _context.TemplateFieldPermissions.AddRange(pendingInserts);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task AnnulDuplicateActivePermissionsAsync(
        IReadOnlyCollection<Guid> fieldIds,
        CancellationToken cancellationToken)
    {
        if (fieldIds.Count == 0)
        {
            return;
        }

        var activePermissions = await _context.TemplateFieldPermissions
            .IgnoreQueryFilters()
            .Where(permission => fieldIds.Contains(permission.TemplateFieldId) && !permission.IsAnnulled)
            .Select(permission => new { permission.Id, permission.TemplateFieldId, permission.RoleName, permission.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var duplicateIds = activePermissions
            .GroupBy(permission => (permission.TemplateFieldId, permission.RoleName))
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.OrderBy(permission => permission.CreatedAtUtc).Skip(1).Select(permission => permission.Id))
            .ToList();

        if (duplicateIds.Count == 0)
        {
            return;
        }

        var annulledAtUtc = _dateTimeProvider.UtcNow;
        var annulledByUserId = _currentUserService.UserId;
        const string annulmentReason = "Дублікат права доступу до поля шаблону.";

        await _context.TemplateFieldPermissions
            .IgnoreQueryFilters()
            .Where(permission => duplicateIds.Contains(permission.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(permission => permission.IsAnnulled, true)
                    .SetProperty(permission => permission.AnnulledAtUtc, annulledAtUtc)
                    .SetProperty(permission => permission.AnnulledByUserId, annulledByUserId)
                    .SetProperty(permission => permission.AnnulmentReason, annulmentReason),
                cancellationToken);
    }

    public async Task<Guid> CreateDataFieldFromTemplateFieldAsync(
        Guid templateFieldId,
        DataFieldType fieldType,
        DataFieldScope scope,
        string displayNameUk,
        string? descriptionUk,
        int? maxLength,
        CancellationToken cancellationToken = default)
    {
        var field = await _context.TemplateFields
            .Include(templateField => templateField.TemplateVersion)
            .FirstOrDefaultAsync(templateField => templateField.Id == templateFieldId, cancellationToken);

        if (field is null)
        {
            throw new InvalidOperationException("Поле шаблону не знайдено.");
        }

        EnsureWorkingVersion(field.TemplateVersion);

        var key = field.Tag.Trim();
        var existingDataField = await _context.DataFields
            .FirstOrDefaultAsync(dataField => dataField.Key == key, cancellationToken);

        if (existingDataField is not null)
        {
            field.DataFieldId = existingDataField.Id;
            field.Status = TemplateFieldStatus.Mapped;
            await _context.SaveChangesAsync(cancellationToken);
            return existingDataField.Id;
        }

        var dataField = new DataField
        {
            Key = key,
            DisplayNameUk = string.IsNullOrWhiteSpace(displayNameUk) ? field.Tag : displayNameUk.Trim(),
            DescriptionUk = descriptionUk?.Trim(),
            FieldType = fieldType,
            Scope = scope,
            MaxLength = maxLength,
            IsRequired = field.IsRequired,
            IsSystem = false,
            IsActive = true
        };

        _context.DataFields.Add(dataField);
        field.DataField = dataField;
        field.Status = TemplateFieldStatus.Mapped;
        field.LastMappedAtUtc = _dateTimeProvider.UtcNow;
        field.LastMappedByUserId = _currentUserService.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        return dataField.Id;
    }

    public async Task<Guid> CreatePdfFieldAsync(
        Guid templateVersionId,
        string tag,
        string? title,
        int? pageNumber = null,
        decimal? positionX = null,
        decimal? positionY = null,
        CancellationToken cancellationToken = default)
    {
        var trimmedTag = tag.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTag))
            throw new InvalidOperationException("Tag обов'язковий.");

        var normalizedTag = trimmedTag.ToUpperInvariant();
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? trimmedTag : title.Trim();

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                // Перевірка версії
                var version = await _context.TemplateVersions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == templateVersionId, cancellationToken);

                if (version == null)
                    throw new InvalidOperationException("Версію шаблону не знайдено.");

                if (version.Status is not (TemplateVersionStatus.Draft or TemplateVersionStatus.ReadyForPublication))
                    throw new InvalidOperationException("Неможливо змінювати опубліковану версію.");

                // Idempotency — перевірка дубліката
                var duplicate = await _context.TemplateFields
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(f =>
                        f.TemplateVersionId == templateVersionId &&
                        !f.IsAnnulled &&
                        f.NormalizedTag == normalizedTag, cancellationToken);

                if (duplicate != null)
                    return duplicate.Id;

                // Кількість існуючих полів для розрахунку позиції
                var existingCount = await _context.TemplateFields
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .CountAsync(f => f.TemplateVersionId == templateVersionId && !f.IsAnnulled, cancellationToken);

                var useViewportPlacement = pageNumber is > 0 && positionX is >= 0 && positionY is >= 0;
                var segmentPage = useViewportPlacement ? pageNumber!.Value : 1;
                var segmentX = useViewportPlacement
                    ? positionX!.Value
                    : 24 + (existingCount % 3) * 240m;
                var segmentY = useViewportPlacement
                    ? positionY!.Value
                    : 24 + (existingCount / 3) * 42m;

                var field = new TemplateField
                {
                    TemplateVersionId = templateVersionId,
                    Tag = trimmedTag,
                    NormalizedTag = normalizedTag,
                    Title = normalizedTitle,
                    WordControlType = WordContentControlType.Unknown,
                    FieldType = FieldType.Text,
                    Status = TemplateFieldStatus.NewTag,
                    SortOrder = existingCount + 1,
                    IsRequired = true,
                    OverflowPolicy = FieldOverflowPolicy.Block,
                    DetectedAtUtc = _dateTimeProvider.UtcNow,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Sequence = 1,
                            PageNumber = segmentPage,
                            PositionX = segmentX,
                            PositionY = segmentY,
                            Width = 220,
                            Height = 28,
                            IsPrimary = true,
                            TextAlignment = TextAlignment.Left
                        }
                    ]
                };

                await AutoMapNewFieldAsync(field, cancellationToken);

                _context.TemplateFields.Add(field);
                await _context.SaveChangesAsync(cancellationToken);

                return field.Id;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                await Task.Delay(60 * attempt, cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < 3 &&
                   (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true))
            {
                // Race condition — поле вже створилося іншим запитом
                var racedId = await _context.TemplateFields
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(f => f.TemplateVersionId == templateVersionId &&
                               !f.IsAnnulled &&
                               f.NormalizedTag == normalizedTag)
                    .Select(f => f.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (racedId != Guid.Empty)
                    return racedId;
            }
        }

        throw new InvalidOperationException("Не вдалося додати PDF-поле після 3 спроб. Спробуйте ще раз.");
    }

    private async Task<TemplateVersion> LoadEditableVersionAsync(Guid templateVersionId, CancellationToken cancellationToken)
    {
        var version = await _context.TemplateVersions
            .Include(templateVersion => templateVersion.Fields)
                .ThenInclude(field => field.Segments)
            .FirstOrDefaultAsync(templateVersion => templateVersion.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            throw new InvalidOperationException("Версію шаблону не знайдено.");
        }

        EnsureWorkingVersion(version);
        return version;
    }

    private async Task AutoMapNewFieldAsync(TemplateField field, CancellationToken cancellationToken)
    {
        var lookupKey = ResolveSemanticDataFieldKey(field.Tag);
        var dataField = await _context.DataFields
            .FirstOrDefaultAsync(item => item.Key == lookupKey && item.IsActive, cancellationToken);

        if (dataField is null)
        {
            return;
        }

        field.DataFieldId = dataField.Id;
        field.Status = TemplateFieldStatus.Mapped;
        field.LastMappedAtUtc = _dateTimeProvider.UtcNow;
        field.LastMappedByUserId = _currentUserService.UserId;
        field.EstimatedCapacityChars ??= dataField.MaxLength;
    }

    /// <summary>Legacy Global.* теги конструктора → семантичні ключі DataField.</summary>
    private static string ResolveSemanticDataFieldKey(string tag) =>
        tag.Trim() switch
        {
            "Global.SamplePoint" => "Sample.SamplingLocation",
            _ => tag.Trim()
        };

    private static bool CanUpdatePermissions(TemplateVersionStatus status) =>
        status is TemplateVersionStatus.Draft
            or TemplateVersionStatus.ReadyForPublication
            or TemplateVersionStatus.Published;

    private static void EnsureWorkingVersion(TemplateVersion version)
    {
        if (version.Status is not TemplateVersionStatus.Draft
            and not TemplateVersionStatus.ReadyForPublication
            and not TemplateVersionStatus.Published)
        {
            throw new InvalidOperationException("Редагувати можна лише чернетку, готову до публікації або активну версію шаблону.");
        }
    }

    private static void ApplySegmentTypography(
        TemplateFieldSegment segment,
        TemplateFieldSegmentLayoutUpdate segmentUpdate)
    {
        var horizontal = ResolveHorizontalAlignment(
            segmentUpdate.HorizontalAlignment,
            segmentUpdate.TextAlignment);

        segment.HorizontalAlignment = horizontal;
        segment.TextAlignment = Enum.TryParse<TextAlignment>(horizontal, true, out var alignment)
            ? alignment
            : TextAlignment.Left;
        segment.VerticalAlignment = NormalizeVerticalAlignment(segmentUpdate.VerticalAlignment);
        segment.FontSize = segmentUpdate.FontSize is > 0 ? segmentUpdate.FontSize : null;
        segment.FontName = string.IsNullOrWhiteSpace(segmentUpdate.FontName)
            ? null
            : segmentUpdate.FontName.Trim();
        segment.LineHeight = segmentUpdate.LineHeight is > 0 ? segmentUpdate.LineHeight : null;
        segment.SvgPathData = string.IsNullOrWhiteSpace(segmentUpdate.SvgPathData)
            ? null
            : segmentUpdate.SvgPathData.Trim();
    }

    private static string ResolveHorizontalAlignment(string? horizontalAlignment, string textAlignment)
    {
        if (!string.IsNullOrWhiteSpace(horizontalAlignment))
        {
            return horizontalAlignment.Trim();
        }

        return string.IsNullOrWhiteSpace(textAlignment) ? "Left" : textAlignment.Trim();
    }

    private static string? NormalizeVerticalAlignment(string? verticalAlignment)
    {
        if (string.IsNullOrWhiteSpace(verticalAlignment))
        {
            return null;
        }

        return verticalAlignment.Trim() switch
        {
            "Top" => "Top",
            "Middle" => "Middle",
            "Bottom" => "Bottom",
            _ => null
        };
    }

    public async Task<PdfWorkspaceFillLayoutSaveResult> SaveFillLayoutRefinementAsync(
        Guid templateVersionId,
        IReadOnlyList<PdfWorkspaceFillLayoutFieldUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0)
        {
            return new PdfWorkspaceFillLayoutSaveResult
            {
                Saved = 0,
                Message = "Немає змін макету для збереження."
            };
        }

        var version = await _context.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            throw new InvalidOperationException("Версію шаблону не знайдено.");
        }

        EnsureLayoutRefinableVersion(version);

        var saved = 0;

        foreach (var update in updates)
        {
            if (update.SegmentId == Guid.Empty || update.TemplateFieldId == Guid.Empty)
            {
                continue;
            }

            var segment = await _context.TemplateFieldSegments
                .Include(item => item.TemplateField)
                .FirstOrDefaultAsync(
                    item => item.Id == update.SegmentId
                            && item.TemplateFieldId == update.TemplateFieldId
                            && !item.IsAnnulled,
                    cancellationToken);

            if (segment is null
                || segment.TemplateField.IsAnnulled
                || segment.TemplateField.TemplateVersionId != templateVersionId)
            {
                continue;
            }

            PrepareTrackedSegmentRowVersion(segment);

            segment.PageNumber = Math.Max(1, update.PageNumber);
            segment.PositionX = Math.Max(0, update.PositionX);
            segment.PositionY = Math.Max(0, update.PositionY);
            segment.Width = Math.Max(20, update.Width);
            segment.Height = Math.Max(14, update.Height);
            segment.IsPrimary = update.IsPrimary;

            var layoutUpdate = new TemplateFieldSegmentLayoutUpdate(
                segment.Id,
                segment.TemplateFieldId,
                $"fill-{segment.Id:N}",
                segment.Sequence,
                segment.PageNumber,
                segment.PositionX,
                segment.PositionY,
                segment.Width,
                segment.Height,
                segment.IsPrimary,
                update.TextAlignment,
                update.FontSize,
                update.FontName,
                update.LineHeight,
                update.HorizontalAlignment,
                update.VerticalAlignment,
                update.SvgPathData,
                ResolveRowVersionFromBase64(update.RowVersionBase64));

            ApplySegmentTypography(segment, layoutUpdate);

            segment.TemplateField.TextOffsetX = update.TextOffsetX;
            segment.TemplateField.TextOffsetY = update.TextOffsetY;
            segment.UpdatedAtUtc = _dateTimeProvider.UtcNow;
            saved++;
        }

        if (saved == 0)
        {
            return new PdfWorkspaceFillLayoutSaveResult
            {
                Saved = 0,
                Message = "Жодне поле не оновлено (перевірте segmentId / templateFieldId)."
            };
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new PdfWorkspaceFillLayoutSaveResult
        {
            Saved = saved,
            Message = $"Макет збережено для версії v{version.VersionNumber}: полів {saved}. Наступні замовлення отримають ці правки."
        };
    }

    private static void EnsureLayoutRefinableVersion(TemplateVersion version)
    {
        if (version.IsAnnulled)
        {
            throw new InvalidOperationException("Анульовану версію шаблону змінювати заборонено.");
        }

        if (version.Status is not TemplateVersionStatus.Draft
            and not TemplateVersionStatus.ReadyForPublication
            and not TemplateVersionStatus.Published)
        {
            throw new InvalidOperationException(
                "Макет можна уточнювати лише для чернетки, готової до публікації або опублікованої версії.");
        }
    }

    private static byte[]? ResolveRowVersionFromBase64(string? rowVersionBase64)
    {
        if (string.IsNullOrWhiteSpace(rowVersionBase64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(rowVersionBase64.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
