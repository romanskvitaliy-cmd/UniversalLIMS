using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
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

        EnsureDraftVersion(version);
    }

    public async Task ProcessFieldSegmentsAsync(
        Guid templateVersionId,
        Guid fieldId,
        IReadOnlyList<TemplateFieldSegmentLayoutUpdate> segmentUpdates,
        IDictionary<TemplateFieldSegment, string> clientReferenceBySegment,
        CancellationToken cancellationToken = default)
    {
        if (segmentUpdates.Count == 0)
        {
            return;
        }

        var field = await _context.TemplateFields
            .Include(templateField => templateField.Segments)
            .FirstOrDefaultAsync(
                templateField => templateField.Id == fieldId
                    && templateField.TemplateVersionId == templateVersionId,
                cancellationToken);

        if (field is null)
        {
            throw new InvalidOperationException($"Поле шаблону {fieldId} не знайдено у версії {templateVersionId}.");
        }

        // Upsert only the segments that belong to this field.
        var existingSegmentsById = field.Segments.ToDictionary(segment => segment.Id);
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
            segment.TextAlignment = Enum.TryParse<TextAlignment>(segmentUpdate.TextAlignment, true, out var alignment)
                ? alignment
                : TextAlignment.Left;
            retainedSegments.Add(segment);
            clientReferenceBySegment[segment] = clientReferenceId;
        }

        var segmentsToAnnul = field.Segments
            .Where(item => !retainedSegments.Contains(item))
            .ToList();

        ReleaseActiveSequenceSlots(segmentsToAnnul, AnnulledSequenceStagingBase);
        foreach (var segment in segmentsToAnnul)
        {
            field.Segments.Remove(segment);
            AnnulSegment(segment);
        }

        if (segmentsToAnnul.Count > 0)
        {
            ReleaseActiveSequenceSlots(retainedSegments, RetainedSequenceStagingBase);
        }

        // Waterfall order is scoped to the current field only.
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
            .Where(segment => segment.TemplateField.TemplateVersionId == templateVersionId)
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
                segment.RowVersion))
            .ToList();

        var mapping = savedSegments
            .Where(segment => !string.IsNullOrWhiteSpace(segment.ClientReferenceId))
            .ToDictionary(
                segment => segment.ClientReferenceId,
                segment => new TemplateFieldSegmentIdentity(segment.Id, segment.RowVersion));

        return new TemplateFieldSegmentLayoutSaveResult(mapping, savedSegments);
    }

    private void AnnulSegment(TemplateFieldSegment segment)
    {
        segment.AnnulmentReason = "Видалено в PDF overlay designer.";
        _context.Remove(segment);
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
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePermissionsAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FieldAccessLevel>> accessLevelsByFieldId,
        CancellationToken cancellationToken = default)
    {
        var version = await _context.TemplateVersions
            .Include(templateVersion => templateVersion.Fields)
                .ThenInclude(field => field.Permissions)
            .FirstOrDefaultAsync(templateVersion => templateVersion.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            throw new InvalidOperationException("Версію шаблону не знайдено.");
        }

        EnsureDraftVersion(version);
        var validRoles = LimsRoles.All.ToHashSet(StringComparer.Ordinal);

        foreach (var field in version.Fields)
        {
            if (!accessLevelsByFieldId.TryGetValue(field.Id, out var accessLevelsByRole))
            {
                continue;
            }

            foreach (var roleAccess in accessLevelsByRole.Where(roleAccess => validRoles.Contains(roleAccess.Key)))
            {
                var permission = field.Permissions
                    .FirstOrDefault(existingPermission => existingPermission.RoleName == roleAccess.Key);

                if (permission is null)
                {
                    field.Permissions.Add(new TemplateFieldPermission
                    {
                        RoleName = roleAccess.Key,
                        AccessLevel = roleAccess.Value
                    });

                    continue;
                }

                permission.AccessLevel = roleAccess.Value;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
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

        EnsureDraftVersion(field.TemplateVersion);

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
                            PageNumber = 1,
                            PositionX = 24 + (existingCount % 3) * 240m,
                            PositionY = 24 + (existingCount / 3) * 42m,
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

        EnsureDraftVersion(version);
        return version;
    }

    private async Task AutoMapNewFieldAsync(TemplateField field, CancellationToken cancellationToken)
    {
        var dataField = await _context.DataFields
            .FirstOrDefaultAsync(item => item.Key == field.Tag && item.IsActive, cancellationToken);

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

    private static void EnsureDraftVersion(TemplateVersion version)
    {
        if (version.Status is not TemplateVersionStatus.Draft and not TemplateVersionStatus.ReadyForPublication)
        {
            throw new InvalidOperationException("Опубліковану версію шаблону змінювати заборонено.");
        }
    }
}
