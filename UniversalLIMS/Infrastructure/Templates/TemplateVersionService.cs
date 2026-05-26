using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Templates;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class TemplateVersionService : ITemplateVersionService
{
    /// <summary>
    /// Sequences at or above this value are temporary staging slots used while saving overlay layout.
    /// They must not be copied into a new template version.
    /// </summary>
    private const int SegmentSequenceStagingThreshold = 100_000;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDocxContentControlReader _docxContentControlReader;
    private readonly ITemplateDocumentStorage _documentStorage;
    private readonly IWordToPdfDocumentConverter _wordToPdfDocumentConverter;
    private readonly ITemplatePublicationValidator _publicationValidator;

    public TemplateVersionService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IDocxContentControlReader docxContentControlReader,
        ITemplateDocumentStorage documentStorage,
        IWordToPdfDocumentConverter wordToPdfDocumentConverter,
        ITemplatePublicationValidator publicationValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _docxContentControlReader = docxContentControlReader;
        _documentStorage = documentStorage;
        _wordToPdfDocumentConverter = wordToPdfDocumentConverter;
        _publicationValidator = publicationValidator;
    }

    public async Task<Guid> CreateDraftVersionAsync(
        Guid templateId,
        string originalFileName,
        string contentType,
        Stream documentStream,
        Guid? copyFieldsFromVersionId = null,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(item => item.Id == templateId, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException("Шаблон не знайдено.");
        }

        await using var bufferedDocument = new MemoryStream();
        await documentStream.CopyToAsync(bufferedDocument, cancellationToken);

        var fileExtension = ResolveUploadExtension(originalFileName, contentType);
        if (!IsSupportedUploadExtension(fileExtension))
        {
            throw new InvalidOperationException("Дозволено завантажувати тільки файли .pdf, .docx або .doc.");
        }

        IReadOnlyCollection<DocxContentControlInfo> detectedFields = [];
        var persistedFileName = Path.GetFileName(originalFileName);
        const string persistedContentType = "application/pdf";
        MemoryStream? convertedPdfStream = null;
        Stream persistenceStream = bufferedDocument;

        if (IsWordExtension(fileExtension))
        {
            if (IsDocxExtension(fileExtension))
            {
                bufferedDocument.Position = 0;
                detectedFields = await _docxContentControlReader.ReadContentControlsAsync(
                    bufferedDocument,
                    cancellationToken);
            }

            bufferedDocument.Position = 0;
            try
            {
                convertedPdfStream = await _wordToPdfDocumentConverter.ConvertAsync(
                    bufferedDocument,
                    fileExtension,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidDataException("Файл Word пошкоджений або його не вдалося конвертувати в PDF.", exception);
            }

            persistenceStream = convertedPdfStream;
            persistedFileName = BuildPersistedPdfFileName(originalFileName);
        }
        else
        {
            bufferedDocument.Position = 0;
        }

        var version = new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = await GetNextVersionNumberAsync(template.Id, cancellationToken),
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            UploadedAtUtc = _dateTimeProvider.UtcNow,
            UploadedByUserId = _currentUserService.UserId
        };

        try
        {
            persistenceStream.Position = 0;
            var storedDocument = await _documentStorage.SaveAsync(
                template.Id,
                version.Id,
                persistedFileName,
                persistedContentType,
                persistenceStream,
                cancellationToken);

            version.OriginalFileName = storedDocument.OriginalFileName;
            version.StorageKey = storedDocument.StorageKey;
            version.ContentType = storedDocument.ContentType;
            version.FileSizeBytes = storedDocument.FileSizeBytes;
            version.Sha256Hash = storedDocument.Sha256Hash;
        }
        finally
        {
            if (convertedPdfStream is not null)
            {
                await convertedPdfStream.DisposeAsync();
            }
        }

        version.Fields = CreateFields(detectedFields);
        await AutoMapFieldsAsync(version.Fields, cancellationToken);

        _context.TemplateVersions.Add(version);
        await _context.SaveChangesAsync(cancellationToken);

        if (copyFieldsFromVersionId.HasValue)
        {
            await CopyFieldsFromVersionAsync(version.Id, copyFieldsFromVersionId.Value, cancellationToken);
        }

        return version.Id;
    }

    public async Task CopyFieldsFromVersionAsync(
        Guid targetVersionId,
        Guid sourceVersionId,
        CancellationToken cancellationToken = default)
    {
        if (targetVersionId == sourceVersionId)
        {
            throw new InvalidOperationException("Неможливо копіювати теги з тієї самої версії.");
        }

        var targetVersion = await _context.TemplateVersions
            .FirstOrDefaultAsync(version => version.Id == targetVersionId, cancellationToken);

        if (targetVersion is null)
        {
            throw new InvalidOperationException("Цільову версію шаблону не знайдено.");
        }

        EnsureDraftVersion(targetVersion);

        var targetHasFields = await _context.TemplateFields
            .IgnoreQueryFilters()
            .AnyAsync(field => field.TemplateVersionId == targetVersionId && !field.IsAnnulled, cancellationToken);

        if (targetHasFields)
        {
            throw new InvalidOperationException("У цій версії вже є теги. Імпорт доступний лише для чистої версії.");
        }

        var sourceVersion = await _context.TemplateVersions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(version => version.Id == sourceVersionId)
            .Select(version => new
            {
                version.Id,
                version.TemplateId,
                version.DocumentFormat
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sourceVersion is null)
        {
            throw new InvalidOperationException("Вихідну версію шаблону не знайдено.");
        }

        if (sourceVersion.TemplateId != targetVersion.TemplateId)
        {
            throw new InvalidOperationException("Копіювати теги можна лише між версіями одного шаблону.");
        }

        if (sourceVersion.DocumentFormat != targetVersion.DocumentFormat)
        {
            throw new InvalidOperationException("Копіювання тегів доступне лише між версіями одного формату документа.");
        }

        var sourceFields = await LoadSourceFieldsAsync(sourceVersionId, cancellationToken);
        if (sourceFields.Count == 0)
        {
            throw new InvalidOperationException(
                "У вибраній версії немає тегів для копіювання. Оберіть іншу версію або почніть з чистого макету.");
        }

        _context.TemplateFields.AddRange(BuildClonedFields(targetVersion.Id, sourceFields));
        targetVersion.BasedOnTemplateVersionId = sourceVersion.Id;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RescanFieldsAsync(Guid templateVersionId, CancellationToken cancellationToken = default)
    {
        var version = await _context.TemplateVersions
            .Include(templateVersion => templateVersion.Fields)
            .FirstOrDefaultAsync(templateVersion => templateVersion.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            throw new InvalidOperationException("Версію шаблону не знайдено.");
        }

        EnsureDraftVersion(version);
        EnsureDocxLegacyVersion(version);

        await using var documentStream = await _documentStorage.OpenReadAsync(version.StorageKey, cancellationToken);
        var detectedFields = await _docxContentControlReader.ReadContentControlsAsync(documentStream, cancellationToken);
        var detectedByTag = detectedFields.ToDictionary(
            field => NormalizeTag(field.Tag),
            field => field,
            StringComparer.OrdinalIgnoreCase);

        foreach (var existingField in version.Fields)
        {
            if (!detectedByTag.ContainsKey(existingField.NormalizedTag))
            {
                existingField.IsAnnulled = true;
                existingField.AnnulledAtUtc = _dateTimeProvider.UtcNow;
                existingField.AnnulledByUserId = _currentUserService.UserId;
                existingField.AnnulmentReason = "Content Control відсутній після повторного сканування .docx.";
            }
        }

        var existingTags = version.Fields
            .Select(field => field.NormalizedTag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var detectedField in detectedFields)
        {
            var normalizedTag = NormalizeTag(detectedField.Tag);
            if (existingTags.Contains(normalizedTag))
            {
                continue;
            }

            version.Fields.Add(CreateField(detectedField));
        }

        await AutoMapFieldsAsync(version.Fields, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PublicationValidationResult> PublishAsync(
        Guid templateVersionId,
        string? publicationNotesUk,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _publicationValidator.ValidateAsync(templateVersionId, cancellationToken);
        if (!validationResult.IsValid)
        {
            return validationResult;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var version = await _context.TemplateVersions
            .Include(templateVersion => templateVersion.Template)
            .FirstAsync(templateVersion => templateVersion.Id == templateVersionId, cancellationToken);

        var previousPublishedVersions = await _context.TemplateVersions
            .Where(templateVersion =>
                templateVersion.TemplateId == version.TemplateId &&
                templateVersion.Status == TemplateVersionStatus.Published &&
                templateVersion.Id != version.Id)
            .ToListAsync(cancellationToken);

        foreach (var previousVersion in previousPublishedVersions)
        {
            previousVersion.Status = TemplateVersionStatus.Superseded;
        }

        version.Status = TemplateVersionStatus.Published;
        version.PublishedAtUtc = _dateTimeProvider.UtcNow;
        version.PublishedByUserId = _currentUserService.UserId;
        version.PublicationNotesUk = publicationNotesUk;

        version.Template.Status = TemplateStatus.Active;
        version.Template.CurrentPublishedVersionId = version.Id;

        await InvestigationTypeTemplateLinker.EnsureLinksForTemplateAsync(
            _context,
            version.TemplateId,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PublicationValidationResult();
    }

    /// <summary>
    /// Creates a new draft template version by cloning the field markup and segment geometry
    /// from a previous version while reusing the same stored PDF document.
    /// </summary>
    /// <param name="previousVersionId">The source version whose markup is copied.</param>
    /// <param name="changeReason">A human-readable reason stored in publication notes for audit traceability.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The newly created draft <see cref="TemplateVersion"/> with regenerated field and segment identifiers.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the source version does not exist or the change reason is empty.
    /// </exception>
    public async Task<TemplateVersion> CreateNewVersionAsync(
        Guid previousVersionId,
        string changeReason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(changeReason))
        {
            throw new InvalidOperationException("Change reason is required when creating a new template version.");
        }

        var sourceVersion = await _context.TemplateVersions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(version => version.Id == previousVersionId, cancellationToken);

        if (sourceVersion is null)
        {
            throw new InvalidOperationException("Source template version was not found.");
        }

        var sourceFields = await LoadSourceFieldsAsync(previousVersionId, cancellationToken);
        if (sourceFields.Count == 0)
        {
            throw new InvalidOperationException(
                "У вихідній версії немає полів для копіювання. Оберіть версію з налаштованими тегами або спочатку збережіть overlay у Map.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var clonedVersion = new TemplateVersion
        {
            TemplateId = sourceVersion.TemplateId,
            BasedOnTemplateVersionId = sourceVersion.Id,
            VersionNumber = await GetNextVersionNumberAsync(sourceVersion.TemplateId, cancellationToken),
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = sourceVersion.DocumentFormat,
            OriginalFileName = sourceVersion.OriginalFileName,
            StorageKey = sourceVersion.StorageKey,
            ContentType = sourceVersion.ContentType,
            FileSizeBytes = sourceVersion.FileSizeBytes,
            Sha256Hash = sourceVersion.Sha256Hash,
            UploadedAtUtc = _dateTimeProvider.UtcNow,
            UploadedByUserId = _currentUserService.UserId,
            PublicationNotesUk = changeReason.Trim()
        };

        foreach (var clonedField in BuildClonedFields(clonedVersion.Id, sourceFields))
        {
            clonedVersion.Fields.Add(clonedField);
        }

        _context.TemplateVersions.Add(clonedVersion);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return clonedVersion;
    }

    public async Task AnnulAsync(
        Guid templateVersionId,
        string annulmentReason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(annulmentReason))
        {
            throw new InvalidOperationException("Причина анулювання версії шаблону обов'язкова.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var version = await _context.TemplateVersions
            .Include(templateVersion => templateVersion.Template)
            .FirstOrDefaultAsync(templateVersion => templateVersion.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            throw new InvalidOperationException("Версію шаблону не знайдено.");
        }

        if (version.Status == TemplateVersionStatus.Published &&
            version.Template.CurrentPublishedVersionId == version.Id)
        {
            version.Template.CurrentPublishedVersionId = null;
            version.Template.Status = TemplateStatus.Draft;
        }

        version.Status = TemplateVersionStatus.Annulled;
        version.AnnulmentReason = annulmentReason.Trim();
        _context.TemplateVersions.Remove(version);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<List<TemplateField>> LoadSourceFieldsAsync(
        Guid sourceVersionId,
        CancellationToken cancellationToken)
    {
        return await _context.TemplateFields
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(field => field.TemplateVersionId == sourceVersionId && !field.IsAnnulled)
            .AsSplitQuery()
            .Include(field => field.Segments)
            .Include(field => field.Permissions)
            .OrderBy(field => field.SortOrder)
            .ToListAsync(cancellationToken);
    }

    private static IEnumerable<TemplateField> BuildClonedFields(
        Guid targetVersionId,
        IEnumerable<TemplateField> sourceFields)
    {
        foreach (var sourceField in sourceFields)
        {
            var clonedFieldId = Guid.NewGuid();
            var clonedSegments = new List<TemplateFieldSegment>();

            var sourceSegments = sourceField.Segments
                .Where(segment => segment.Sequence < SegmentSequenceStagingThreshold)
                .OrderBy(segment => segment.Sequence)
                .ThenBy(segment => segment.Id)
                .ToList();

            for (var segmentIndex = 0; segmentIndex < sourceSegments.Count; segmentIndex++)
            {
                var sourceSegment = sourceSegments[segmentIndex];
                clonedSegments.Add(new TemplateFieldSegment
                {
                    Id = Guid.NewGuid(),
                    TemplateFieldId = clonedFieldId,
                    Sequence = segmentIndex + 1,
                    PageNumber = sourceSegment.PageNumber,
                    PositionX = sourceSegment.PositionX,
                    PositionY = sourceSegment.PositionY,
                    Width = sourceSegment.Width,
                    Height = sourceSegment.Height,
                    IsPrimary = sourceSegment.IsPrimary,
                    TextAlignment = sourceSegment.TextAlignment,
                    HorizontalAlignment = sourceSegment.HorizontalAlignment,
                    VerticalAlignment = sourceSegment.VerticalAlignment,
                    FontName = sourceSegment.FontName,
                    FontSize = sourceSegment.FontSize,
                    LineHeight = sourceSegment.LineHeight,
                    SvgPathData = sourceSegment.SvgPathData
                });
            }


            if (clonedSegments.Count == 0)
            {
                clonedSegments.Add(new TemplateFieldSegment
                {
                    Id = Guid.NewGuid(),
                    TemplateFieldId = clonedFieldId,
                    Sequence = 1,
                    PageNumber = 1,
                    PositionX = 24,
                    PositionY = 24,
                    Width = 220,
                    Height = 28,
                    IsPrimary = true
                });
            }

            yield return new TemplateField
            {
                Id = clonedFieldId,
                TemplateVersionId = targetVersionId,
                Tag = sourceField.Tag,
                NormalizedTag = sourceField.NormalizedTag,
                Title = sourceField.Title,
                WordControlType = sourceField.WordControlType,
                FieldType = sourceField.FieldType,
                Status = sourceField.Status,
                DataFieldId = sourceField.DataFieldId,
                IsRequired = sourceField.IsRequired,
                SortOrder = sourceField.SortOrder,
                EstimatedCapacityChars = sourceField.EstimatedCapacityChars,
                MaxLines = sourceField.MaxLines,
                AllowMultiline = sourceField.AllowMultiline,
                OverflowPolicy = sourceField.OverflowPolicy,
                DetectedAtUtc = sourceField.DetectedAtUtc,
                LastMappedAtUtc = sourceField.LastMappedAtUtc,
                LastMappedByUserId = sourceField.LastMappedByUserId,
                Segments = clonedSegments,
                Permissions = sourceField.Permissions
                    .Where(permission => !permission.IsAnnulled)
                    .GroupBy(permission => permission.RoleName)
                    .Select(group => group.First())
                    .Select(permission => new TemplateFieldPermission
                    {
                        Id = Guid.NewGuid(),
                        TemplateFieldId = clonedFieldId,
                        RoleName = permission.RoleName,
                        AccessLevel = permission.AccessLevel
                    })
                    .ToList()
            };
        }
    }

    private async Task<int> GetNextVersionNumberAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var latestVersionNumber = await _context.TemplateVersions
            .IgnoreQueryFilters()
            .Where(version => version.TemplateId == templateId)
            .MaxAsync(version => (int?)version.VersionNumber, cancellationToken);

        return (latestVersionNumber ?? 0) + 1;
    }

    private ICollection<TemplateField> CreateFields(IReadOnlyCollection<DocxContentControlInfo> fieldInfos)
    {
        return fieldInfos
            .OrderBy(field => field.SortOrder)
            .Select(CreateField)
            .ToList();
    }

    private async Task AutoMapFieldsAsync(ICollection<TemplateField> fields, CancellationToken cancellationToken)
    {
        var tags = fields
            .Select(field => field.Tag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dataFields = await _context.DataFields
            .Where(dataField => tags.Contains(dataField.Key) && dataField.IsActive)
            .ToDictionaryAsync(dataField => dataField.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var field in fields)
        {
            if (field.DataFieldId is not null)
            {
                continue;
            }

            if (!dataFields.TryGetValue(field.Tag, out var dataField))
            {
                continue;
            }

            field.DataFieldId = dataField.Id;
            field.Status = TemplateFieldStatus.Mapped;
            field.EstimatedCapacityChars ??= dataField.MaxLength;
        }
    }

    private TemplateField CreateField(DocxContentControlInfo fieldInfo)
    {
        return new TemplateField
        {
            Tag = fieldInfo.Tag,
            NormalizedTag = NormalizeTag(fieldInfo.Tag),
            Title = fieldInfo.Title,
            WordControlType = fieldInfo.ControlType,
            Status = TemplateFieldStatus.NewTag,
            SortOrder = fieldInfo.SortOrder,
            DetectedAtUtc = _dateTimeProvider.UtcNow,
            IsRequired = true,
            EstimatedCapacityChars = fieldInfo.EstimatedCapacityChars,
            MaxLines = fieldInfo.AllowMultiline ? null : 1,
            AllowMultiline = fieldInfo.AllowMultiline,
            OverflowPolicy = FieldOverflowPolicy.Block
        };
    }

    private static void EnsureDraftVersion(TemplateVersion version)
    {
        if (version.Status is not TemplateVersionStatus.Draft and not TemplateVersionStatus.ReadyForPublication)
        {
            throw new InvalidOperationException("Опубліковану версію шаблону змінювати заборонено.");
        }
    }

    private static void EnsureDocxLegacyVersion(TemplateVersion version)
    {
        if (version.DocumentFormat != TemplateDocumentFormat.DocxLegacy)
        {
            throw new InvalidOperationException("Повторне читання полів доступне тільки для legacy .docx версій.");
        }
    }

    private static string NormalizeTag(string tag)
    {
        return tag.Trim().ToUpperInvariant();
    }

    private static string ResolveUploadExtension(string originalFileName, string contentType)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ".pdf";
        }

        if (string.Equals(
                contentType,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                StringComparison.OrdinalIgnoreCase))
        {
            return ".docx";
        }

        if (string.Equals(contentType, "application/msword", StringComparison.OrdinalIgnoreCase))
        {
            return ".doc";
        }

        return string.Empty;
    }

    private static bool IsSupportedUploadExtension(string extension)
    {
        return extension is ".pdf" or ".docx" or ".doc";
    }

    private static bool IsWordExtension(string extension)
    {
        return extension is ".docx" or ".doc";
    }

    private static bool IsDocxExtension(string extension)
    {
        return extension is ".docx";
    }

    private static string BuildPersistedPdfFileName(string originalFileName)
    {
        var safeOriginalFileName = Path.GetFileName(originalFileName);
        return Path.ChangeExtension(safeOriginalFileName, ".pdf") ?? "template.pdf";
    }
}
