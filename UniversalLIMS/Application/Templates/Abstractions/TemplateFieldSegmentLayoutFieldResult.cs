namespace UniversalLIMS.Application.Templates.Abstractions;

public sealed record TemplateFieldSegmentLayoutFieldResult(
    Guid FieldId,
    IReadOnlyList<TemplateFieldSegmentLayoutState> Segments);
