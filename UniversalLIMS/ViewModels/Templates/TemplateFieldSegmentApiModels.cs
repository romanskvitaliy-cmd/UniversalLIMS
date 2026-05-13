namespace UniversalLIMS.ViewModels.Templates;

public sealed class SaveTemplateFieldSegmentsRequest
{
    public List<FieldSegmentsUpdateDto> Fields { get; set; } = [];
}

public sealed class FieldSegmentsUpdateDto
{
    public Guid FieldId { get; set; }

    public List<TemplateFieldSegmentDto> Segments { get; set; } = [];
}
