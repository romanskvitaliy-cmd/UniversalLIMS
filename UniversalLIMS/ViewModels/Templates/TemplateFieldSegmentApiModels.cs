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

public sealed class CalibrationPreviewPdfRequest
{
    /// <summary>WYSIWYG: координати й текст з поточного стану Map (пріоритет над Samples).</summary>
    public List<CalibrationPreviewOverlayRequestDto> Overlays { get; set; } = [];

    /// <summary>Застарілий формат: лише fieldId + text (fallback).</summary>
    public List<CalibrationPreviewSampleDto> Samples { get; set; } = [];
}

public sealed class CalibrationPreviewOverlayRequestDto
{
    public Guid? FieldId { get; set; }

    public string? Tag { get; set; }

    public string Text { get; set; } = string.Empty;

    public int Page { get; set; } = 1;

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public decimal TextOffsetX { get; set; }

    public decimal TextOffsetY { get; set; }

    public decimal? FontSize { get; set; }

    public string? FontName { get; set; }

    public string? HorizontalAlignment { get; set; }

    public string? VerticalAlignment { get; set; }
}

public sealed class CalibrationPreviewSampleDto
{
    public Guid FieldId { get; set; }

    public string Text { get; set; } = string.Empty;
}
