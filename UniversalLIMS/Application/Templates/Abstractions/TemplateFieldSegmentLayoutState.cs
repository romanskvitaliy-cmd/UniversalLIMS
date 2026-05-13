namespace UniversalLIMS.Application.Templates.Abstractions;

public sealed record TemplateFieldSegmentLayoutState(
    Guid SegmentId,
    int Sequence,
    int PageNumber,
    decimal PositionX,
    decimal PositionY,
    decimal Width,
    decimal Height,
    bool IsPrimary,
    string TextAlignment,
    byte[] RowVersion);
