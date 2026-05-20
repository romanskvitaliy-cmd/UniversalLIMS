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
    decimal? FontSize = null,
    string? HorizontalAlignment = null,
    string? VerticalAlignment = null,
    byte[]? RowVersion = null);
