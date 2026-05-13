namespace UniversalLIMS.Application.Templates.Abstractions;

public sealed record TemplateFieldSegmentLayoutSaveResult(
    IReadOnlyDictionary<string, TemplateFieldSegmentIdentity> Mapping,
    IReadOnlyList<TemplateFieldSegmentSavedState> Segments);

public sealed record TemplateFieldSegmentIdentity(
    Guid Id,
    byte[] RowVersion);

public sealed record TemplateFieldSegmentSavedState(
    string ClientReferenceId,
    Guid Id,
    Guid FieldId,
    int Sequence,
    int PageNumber,
    decimal PositionX,
    decimal PositionY,
    decimal Width,
    decimal Height,
    bool IsPrimary,
    string TextAlignment,
    byte[] RowVersion);
