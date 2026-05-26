namespace UniversalLIMS.Application.Registration;

/// <summary>Збереження позиції/оформлення поля в макет поточної версії шаблону (не значення замовлення).</summary>
public sealed class PdfWorkspaceFillLayoutSaveRequest
{
    public List<PdfWorkspaceFillLayoutFieldUpdate> Fields { get; set; } = [];
}

public sealed class PdfWorkspaceFillLayoutFieldUpdate
{
    public Guid TemplateFieldId { get; set; }

    public Guid SegmentId { get; set; }

    public decimal TextOffsetX { get; set; }

    public decimal TextOffsetY { get; set; }

    public int PageNumber { get; set; }

    public decimal PositionX { get; set; }

    public decimal PositionY { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public bool IsPrimary { get; set; } = true;

    public string TextAlignment { get; set; } = "Left";

    public decimal? FontSize { get; set; }

    public string? FontName { get; set; }

    public decimal? LineHeight { get; set; }

    public string? HorizontalAlignment { get; set; }

    public string? VerticalAlignment { get; set; }

    public string? SvgPathData { get; set; }

    public string? RowVersionBase64 { get; set; }
}

public sealed class PdfWorkspaceFillLayoutSaveResult
{
    public int Saved { get; init; }

    public string Message { get; init; } = string.Empty;
}
