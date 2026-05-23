namespace UniversalLIMS.Application.Registration.Abstractions;

/// <summary>WYSIWYG preview калібрування: повний стан з браузера.</summary>
public sealed class PreviewCalibrationRequest
{
    public Guid TemplateVersionId { get; set; }

    public bool IsCalibrationPreview { get; set; } = true;

    public List<PreviewCalibrationFieldRequest> Fields { get; set; } = [];
}

public sealed class PreviewCalibrationFieldRequest
{
    public Guid? TemplateFieldId { get; set; }

    public string Text { get; set; } = string.Empty;

    public decimal OffsetX { get; set; }

    public decimal OffsetY { get; set; }

    public int Page { get; set; } = 1;

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public decimal? FontSize { get; set; }

    public string? FontName { get; set; }

    public string? Alignment { get; set; }

    public string? VerticalAlignment { get; set; }
}
