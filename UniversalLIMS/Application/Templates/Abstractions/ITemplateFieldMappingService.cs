using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Templates.Abstractions;

public interface ITemplateFieldMappingService
{
    Task UpdateMappingsAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, Guid?> dataFieldIdsByFieldId,
        CancellationToken cancellationToken = default);

    Task UpdateCapacityAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, TemplateFieldCapacityUpdate> capacityByFieldId,
        CancellationToken cancellationToken = default);

    Task UpdateLayoutAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, TemplateFieldLayoutUpdate> layoutByFieldId,
        CancellationToken cancellationToken = default);

    Task UpdateTextOffsetsAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, TemplateFieldTextOffsetUpdate> offsetsByFieldId,
        CancellationToken cancellationToken = default);

    Task EnsureEditableTemplateVersionAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default);

    Task ProcessFieldSegmentsAsync(
        Guid templateVersionId,
        Guid fieldId,
        IReadOnlyList<TemplateFieldSegmentLayoutUpdate> segmentUpdates,
        IDictionary<TemplateFieldSegment, string> clientReferenceBySegment,
        CancellationToken cancellationToken = default);

    Task<TemplateFieldSegmentLayoutSaveResult> BuildSegmentLayoutSaveResultAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<TemplateFieldSegment, string> clientReferenceBySegment,
        CancellationToken cancellationToken = default);

    Task DeleteFieldsAsync(
        Guid templateVersionId,
        IReadOnlyCollection<Guid> fieldIds,
        CancellationToken cancellationToken = default);

    Task UpdatePermissionsAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FieldAccessLevel>> accessLevelsByFieldId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateDataFieldFromTemplateFieldAsync(
        Guid templateFieldId,
        DataFieldType fieldType,
        DataFieldScope scope,
        string displayNameUk,
        string? descriptionUk,
        int? maxLength,
        CancellationToken cancellationToken = default);

    Task<Guid> CreatePdfFieldAsync(
        Guid templateVersionId,
        string tag,
        string? title,
        CancellationToken cancellationToken = default);
}
