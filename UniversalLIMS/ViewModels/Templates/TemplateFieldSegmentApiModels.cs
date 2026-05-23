namespace UniversalLIMS.ViewModels.Templates;

public sealed class SaveTemplateFieldSegmentsRequest
{
    public List<FieldSegmentsUpdateDto> Fields { get; set; } = [];

    public List<Guid> DeletedFieldIds { get; set; } = [];
}

public sealed class FieldSegmentsUpdateDto
{
    public Guid FieldId { get; set; }

    public decimal? TextOffsetX { get; set; }

    public decimal? TextOffsetY { get; set; }

    public List<TemplateFieldSegmentDto> Segments { get; set; } = [];
}

public sealed class SaveTemplateFieldTextOffsetsRequest
{
    public List<TemplateFieldTextOffsetDto> Fields { get; set; } = [];
}

public sealed class TemplateFieldTextOffsetDto
{
    public Guid FieldId { get; set; }

    public decimal TextOffsetX { get; set; }

    public decimal TextOffsetY { get; set; }
}
