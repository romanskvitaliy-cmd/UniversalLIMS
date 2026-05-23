namespace UniversalLIMS.ViewModels.Templates;

public sealed class TemplateFieldSegmentDto
{
    public Guid? Id { get; set; }

    public Guid FieldId { get; set; }

    public string ClientReferenceId { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public int Page { get; set; }

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public bool IsPrimary { get; set; }

    public string TextAlignment { get; set; } = "Left";

    public decimal? FontSize { get; set; }

    public string? FontName { get; set; }

    public decimal? LineHeight { get; set; }

    public string? HorizontalAlignment { get; set; }

    public string? VerticalAlignment { get; set; }

    public string? SvgPathData { get; set; }

    public byte[]? RowVersion { get; set; }

    public string? RowVersionBase64 { get; set; }
}
