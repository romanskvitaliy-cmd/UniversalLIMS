using System.Collections.Concurrent;
using Syncfusion.Drawing;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class ReferralPdfOverlayRenderer
{
    public const decimal ConstructorPreviewScale = PdfOverlayTextLayout.ConstructorPreviewScale;

    private const float DefaultFontSize = 10f;
    private const float MinFontSize = 6f;
    private const float FontSizeStep = 0.5f;

    private static readonly ConcurrentDictionary<float, PdfTrueTypeFont> FontCache = new();

    public byte[] Render(
        Stream originalPdfStream,
        IReadOnlyList<ReferralOverlaySegment> segments,
        IReadOnlyDictionary<Guid, string?> valuesByDataFieldId)
    {
        ArgumentNullException.ThrowIfNull(originalPdfStream);

        using var loadedDocument = new PdfLoadedDocument(originalPdfStream);

        foreach (var overlaySegment in segments)
        {
            var value = ResolveOverlayText(overlaySegment, valuesByDataFieldId);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (overlaySegment.PageNumber < 1 || overlaySegment.PageNumber > loadedDocument.Pages.Count)
            {
                continue;
            }

            var page = loadedDocument.Pages[overlaySegment.PageNumber - 1];
            var bounds = ToPdfRectangle(overlaySegment);
            var format = CreateStringFormat(overlaySegment);
            var font = ResolveFontToFitWidth(value, overlaySegment, bounds.Width, format);

            page.Graphics.DrawString(value, font, PdfBrushes.Black, bounds, format);
        }

        using var output = new MemoryStream();
        loadedDocument.Save(output);
        return output.ToArray();
    }

    private static string? ResolveOverlayText(
        ReferralOverlaySegment segment,
        IReadOnlyDictionary<Guid, string?> valuesByDataFieldId)
    {
        if (!string.IsNullOrWhiteSpace(segment.Text))
        {
            return segment.Text;
        }

        if (segment.DataFieldId is null ||
            !valuesByDataFieldId.TryGetValue(segment.DataFieldId.Value, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }

    private static RectangleF ToPdfRectangle(ReferralOverlaySegment segment)
    {
        var preview = PdfOverlayTextLayout.GetPreviewBounds(
            segment.PositionX,
            segment.PositionY,
            segment.Width,
            segment.Height,
            segment.TextOffsetX,
            segment.TextOffsetY);
        var pdf = PdfOverlayTextLayout.ToPdfPoints(preview);
        return new RectangleF(pdf.X, pdf.Y, pdf.Width, pdf.Height);
    }

    private static PdfTrueTypeFont ResolveFontToFitWidth(
        string text,
        ReferralOverlaySegment segment,
        float maxWidth,
        PdfStringFormat format)
    {
        var currentFontSize = segment.FontSize is > 0 ? (float)segment.FontSize.Value : DefaultFontSize;

        while (true)
        {
            var font = GetUnicodeFont(currentFontSize);
            var measuredWidth = font.MeasureString(text, format).Width;
            if (measuredWidth <= maxWidth || currentFontSize <= MinFontSize)
            {
                return font;
            }

            currentFontSize -= FontSizeStep;
        }
    }

    private static PdfTrueTypeFont GetUnicodeFont(float fontSize)
    {
        var normalizedSize = MathF.Round(fontSize * 2f, MidpointRounding.AwayFromZero) / 2f;
        return FontCache.GetOrAdd(normalizedSize, size =>
        {
            var fontPath = PdfCyrillicFontProvider.ResolveFontPath();
            return new PdfTrueTypeFont(fontPath, size, PdfFontStyle.Regular);
        });
    }

    private static PdfStringFormat CreateStringFormat(ReferralOverlaySegment segment)
    {
        var format = new PdfStringFormat
        {
            WordWrap = PdfWordWrapType.None
        };

        var horizontal = segment.HorizontalAlignment ?? segment.TextAlignment.ToString();
        format.Alignment = horizontal switch
        {
            "Center" => PdfTextAlignment.Center,
            "Right" => PdfTextAlignment.Right,
            _ => PdfTextAlignment.Left
        };

        // За замовчуванням Top: bounds уже зміщені через TextOffset + baseline (PdfOverlayTextLayout).
        var vertical = segment.VerticalAlignment ?? "Top";
        format.LineAlignment = vertical switch
        {
            "Bottom" => PdfVerticalAlignment.Bottom,
            "Middle" => PdfVerticalAlignment.Middle,
            _ => PdfVerticalAlignment.Top
        };

        return format;
    }
}

public sealed class ReferralOverlaySegment
{
    public Guid? DataFieldId { get; init; }

    public string? StorageKey { get; init; }

    /// <summary>Готовий текст для сегмента (наприклад, один рядок з waterfall).</summary>
    public string? Text { get; init; }

    public int PageNumber { get; init; }

    public decimal PositionX { get; init; }

    public decimal PositionY { get; init; }

    public decimal Width { get; init; }

    public decimal Height { get; init; }

    public TextAlignment TextAlignment { get; init; }

    public string? HorizontalAlignment { get; init; }

    public string? VerticalAlignment { get; init; }

    public string? FontName { get; init; }

    public decimal? FontSize { get; init; }

    public decimal TextOffsetX { get; init; }

    public decimal TextOffsetY { get; init; }
}
