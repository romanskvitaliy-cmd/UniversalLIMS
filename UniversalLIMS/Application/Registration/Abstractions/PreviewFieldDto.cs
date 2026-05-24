using System.Text.Json.Serialization;

namespace UniversalLIMS.Application.Registration.Abstractions;

/// <summary>Текст і layout з UI для Preview PDF (калібрування).</summary>
public sealed class PreviewFieldDto
{
    [JsonPropertyName("templateFieldId")]
    public Guid? TemplateFieldId { get; set; }

    [JsonPropertyName("segmentSequence")]
    public int SegmentSequence { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("x")]
    public decimal X { get; set; }

    [JsonPropertyName("y")]
    public decimal Y { get; set; }

    [JsonPropertyName("width")]
    public decimal Width { get; set; }

    [JsonPropertyName("height")]
    public decimal Height { get; set; }

    [JsonPropertyName("offsetX")]
    public decimal OffsetX { get; set; }

    [JsonPropertyName("offsetY")]
    public decimal OffsetY { get; set; }

    [JsonPropertyName("fontSize")]
    public decimal? FontSize { get; set; }

    [JsonPropertyName("fontName")]
    public string? FontName { get; set; }

    [JsonPropertyName("alignment")]
    public string? Alignment { get; set; }

    [JsonPropertyName("verticalAlignment")]
    public string? VerticalAlignment { get; set; }
}
