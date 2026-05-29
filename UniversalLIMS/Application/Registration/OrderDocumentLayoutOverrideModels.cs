using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniversalLIMS.Application.Registration;

public enum PdfWorkspaceFillLayoutSaveScope
{
    OrderDocument = 0,
    Template = 1
}

/// <summary>Уточнення макету (offset, шрифт) лише для одного документа замовлення.</summary>
public sealed class OrderSegmentLayoutOverride
{
    public decimal TextOffsetX { get; set; }

    public decimal TextOffsetY { get; set; }

    public decimal? FontSize { get; set; }

    public string? FontName { get; set; }

    public decimal? LineHeight { get; set; }

    public string? HorizontalAlignment { get; set; }

    public string? VerticalAlignment { get; set; }

    public string? TextAlignment { get; set; }

    public string? SvgPathData { get; set; }
}

public static class OrderDocumentLayoutOverridesJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Dictionary<Guid, OrderSegmentLayoutOverride> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<Guid, OrderSegmentLayoutOverride>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<Guid, OrderSegmentLayoutOverride>>(json, SerializerOptions);
            return parsed ?? new Dictionary<Guid, OrderSegmentLayoutOverride>();
        }
        catch (JsonException)
        {
            return new Dictionary<Guid, OrderSegmentLayoutOverride>();
        }
    }

    public static string Serialize(IReadOnlyDictionary<Guid, OrderSegmentLayoutOverride> overrides) =>
        JsonSerializer.Serialize(overrides, SerializerOptions);

    public static OrderSegmentLayoutOverride FromFieldUpdate(PdfWorkspaceFillLayoutFieldUpdate update) =>
        new()
        {
            TextOffsetX = update.TextOffsetX,
            TextOffsetY = update.TextOffsetY,
            FontSize = update.FontSize,
            FontName = update.FontName,
            LineHeight = update.LineHeight,
            HorizontalAlignment = update.HorizontalAlignment,
            VerticalAlignment = update.VerticalAlignment,
            TextAlignment = update.TextAlignment,
            SvgPathData = update.SvgPathData
        };

    public static PdfWorkspaceFillSegmentDto Apply(PdfWorkspaceFillSegmentDto segment, OrderSegmentLayoutOverride? ov)
    {
        if (ov is null)
        {
            return segment;
        }

        return new PdfWorkspaceFillSegmentDto
        {
            SegmentId = segment.SegmentId,
            TemplateFieldId = segment.TemplateFieldId,
            Tag = segment.Tag,
            Title = segment.Title,
            DataFieldId = segment.DataFieldId,
            DataFieldKey = segment.DataFieldKey,
            Sequence = segment.Sequence,
            PageNumber = segment.PageNumber,
            PositionX = segment.PositionX,
            PositionY = segment.PositionY,
            Width = segment.Width,
            Height = segment.Height,
            AllowMultiline = segment.AllowMultiline,
            TextOffsetX = ov.TextOffsetX,
            TextOffsetY = ov.TextOffsetY,
            FontSize = ov.FontSize ?? segment.FontSize,
            FontName = ov.FontName ?? segment.FontName,
            HorizontalAlignment = ov.HorizontalAlignment ?? segment.HorizontalAlignment,
            VerticalAlignment = ov.VerticalAlignment ?? segment.VerticalAlignment,
            TextAlignment = ov.TextAlignment ?? segment.TextAlignment,
            LineHeight = ov.LineHeight ?? segment.LineHeight,
            SvgPathData = ov.SvgPathData ?? segment.SvgPathData,
            IsPrimary = segment.IsPrimary,
            SegmentRowVersion = segment.SegmentRowVersion,
            AccessLevel = segment.AccessLevel
        };
    }
}
