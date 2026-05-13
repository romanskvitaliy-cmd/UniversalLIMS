namespace UniversalLIMS.Application.Templates.Abstractions;

public sealed record TemplateFieldLayoutUpdate(
    int? PageNumber,
    decimal? PositionX,
    decimal? PositionY,
    decimal? Width,
    decimal? Height);
