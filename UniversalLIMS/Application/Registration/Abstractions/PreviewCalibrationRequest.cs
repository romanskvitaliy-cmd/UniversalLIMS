using System.Text.Json.Serialization;

namespace UniversalLIMS.Application.Registration.Abstractions;

/// <summary>WYSIWYG preview калібрування: повний стан з браузера.</summary>
public sealed class PreviewCalibrationRequest
{
    [JsonPropertyName("templateVersionId")]
    public Guid TemplateVersionId { get; set; }

    [JsonPropertyName("isCalibrationPreview")]
    public bool IsCalibrationPreview { get; set; } = true;

    [JsonPropertyName("fields")]
    public List<PreviewCalibrationFieldRequest> Fields { get; set; } = [];
}

public sealed class PreviewCalibrationFieldRequest
{
    [JsonPropertyName("templateFieldId")]
    public Guid? TemplateFieldId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Альтернативне ім'я з клієнта (деякі збірки payload використовують textToDraw).</summary>
    [JsonPropertyName("textToDraw")]
    public string? TextToDraw { get; set; }

    [JsonPropertyName("offsetX")]
    public decimal OffsetX { get; set; }

    [JsonPropertyName("offsetY")]
    public decimal OffsetY { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("x")]
    public decimal X { get; set; }

    [JsonPropertyName("y")]
    public decimal Y { get; set; }

    [JsonPropertyName("width")]
    public decimal Width { get; set; }

    [JsonPropertyName("height")]
    public decimal Height { get; set; }

    [JsonPropertyName("fontSize")]
    public decimal? FontSize { get; set; }

    [JsonPropertyName("fontName")]
    public string? FontName { get; set; }

    [JsonPropertyName("alignment")]
    public string? Alignment { get; set; }

    [JsonPropertyName("verticalAlignment")]
    public string? VerticalAlignment { get; set; }

    /// <summary>Текст для малювання: <see cref="Text"/> або <see cref="TextToDraw"/>.</summary>
    public string ResolveDrawableText()
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            return Text.Trim();
        }

        return string.IsNullOrWhiteSpace(TextToDraw) ? string.Empty : TextToDraw.Trim();
    }

    public void NormalizeDrawableText()
    {
        var resolved = ResolveDrawableText();
        Text = resolved;
        if (!string.IsNullOrEmpty(resolved))
        {
            TextToDraw = resolved;
        }
    }
}
