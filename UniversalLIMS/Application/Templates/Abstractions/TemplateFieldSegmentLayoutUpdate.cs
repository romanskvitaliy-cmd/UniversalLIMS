namespace UniversalLIMS.Application.Templates.Abstractions;

public sealed record TemplateFieldSegmentLayoutUpdate(
    Guid? SegmentId,
    Guid FieldId,
    string ClientReferenceId,
    int Sequence,
    int PageNumber,
    decimal PositionX,
    decimal PositionY,
    decimal Width,
    decimal Height,
    bool IsPrimary,
    string TextAlignment,
    byte[]? RowVersion = null);
