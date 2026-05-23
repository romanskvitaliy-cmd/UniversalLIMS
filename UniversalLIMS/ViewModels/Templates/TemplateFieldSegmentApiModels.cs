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

/// <summary>Preview PDF у режимі калібрування — повний стан з браузера (WYSIWYG).</summary>
public sealed class CalibrationPreviewPdfRequest
{
    public bool IsCalibrationPreview { get; set; } = true;

    /// <summary>Актуальний стан полів з Map (основний формат).</summary>
    public List<CalibrationPreviewFieldDto> Fields { get; set; } = [];

    /// <summary>Застарілий alias; використовується лише якщо <see cref="Fields"/> порожній.</summary>
    public List<CalibrationPreviewOverlayRequestDto> Overlays { get; set; } = [];
}

/// <summary>Один сегмент для WYSIWYG preview калібрування.</summary>
public sealed class CalibrationPreviewFieldDto
{
    public Guid? TemplateFieldId { get; set; }

    public string? DataFieldKey { get; set; }

    public string? Tag { get; set; }

    public string Text { get; set; } = string.Empty;

    public int Page { get; set; } = 1;

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public decimal OffsetX { get; set; }

    public decimal OffsetY { get; set; }

    public decimal? FontSize { get; set; }

    public string? FontName { get; set; }

    /// <summary>Горизонтальне вирівнювання (Left/Center/Right).</summary>
    public string? Alignment { get; set; }

    public string? HorizontalAlignment { get; set; }

    public string? VerticalAlignment { get; set; }
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
