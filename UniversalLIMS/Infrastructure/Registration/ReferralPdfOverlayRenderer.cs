using System.Collections.Concurrent;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
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
    private const float MinBoundsWidthPt = 12f;
    private const float MinBoundsHeightPt = 8f;

    private static readonly ConcurrentDictionary<float, PdfTrueTypeFont> FontCache = new();

    public byte[] Render(
        Stream originalPdfStream,
        IReadOnlyList<ReferralOverlaySegment> segments,
        IReadOnlyDictionary<Guid, string?> valuesByDataFieldId) =>
        RenderWithStats(originalPdfStream, segments, valuesByDataFieldId).PdfBytes;

    public PdfOverlayRenderStats RenderWithStats(
        Stream originalPdfStream,
        IReadOnlyList<ReferralOverlaySegment> segments,
        IReadOnlyDictionary<Guid, string?> valuesByDataFieldId)
    {
        ArgumentNullException.ThrowIfNull(originalPdfStream);

        var drawn = 0;
        var skippedEmpty = 0;
        var skippedPage = 0;

        using var loadedDocument = new PdfLoadedDocument(originalPdfStream);
        var pageCount = loadedDocument.Pages.Count;

        foreach (var overlaySegment in segments)
        {
            var value = ResolveOverlayText(overlaySegment, valuesByDataFieldId);
            if (string.IsNullOrWhiteSpace(value))
            {
                skippedEmpty++;
                continue;
            }

            if (overlaySegment.PageNumber < 1 || overlaySegment.PageNumber > pageCount)
            {
                skippedPage++;
                continue;
            }

            var page = loadedDocument.Pages[overlaySegment.PageNumber - 1];
            var pageSize = page.Size;
            var bounds = FitBoundsToPage(ToPdfRectangle(overlaySegment), pageSize);
            var format = CreateStringFormat(overlaySegment);
            var font = ResolveFontToFitWidth(value, overlaySegment, bounds.Width, format);

            DrawOverlayText(page, value, font, bounds, format);
            drawn++;
        }

        using var output = new MemoryStream();
        loadedDocument.Save(output);
        return new PdfOverlayRenderStats(output.ToArray(), drawn, skippedEmpty, skippedPage, pageCount);
    }

    private static void DrawOverlayText(
        PdfPageBase page,
        string value,
        PdfTrueTypeFont font,
        RectangleF bounds,
        PdfStringFormat format)
    {
        // Пряме позиціонування для Left+Top — збігається з live preview у конструкторі.
        if (format.Alignment == PdfTextAlignment.Left && format.LineAlignment == PdfVerticalAlignment.Top)
        {
            page.Graphics.DrawString(value, font, PdfBrushes.Black, bounds.X, bounds.Y);
            return;
        }

        page.Graphics.DrawString(value, font, PdfBrushes.Black, bounds, format);
    }

    private static string? ResolveOverlayText(
        ReferralOverlaySegment segment,
        IReadOnlyDictionary<Guid, string?> valuesByDataFieldId)
    {
        if (!string.IsNullOrWhiteSpace(segment.Text))
        {
            return segment.Text.Trim();
        }

        if (segment.DataFieldId is null ||
            !valuesByDataFieldId.TryGetValue(segment.DataFieldId.Value, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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

    private static RectangleF FitBoundsToPage(RectangleF bounds, SizeF pageSize)
    {
        var width = Math.Max(MinBoundsWidthPt, bounds.Width);
        var height = Math.Max(MinBoundsHeightPt, bounds.Height);

        if (pageSize.Width > 0)
        {
            width = Math.Min(width, pageSize.Width);
        }

        if (pageSize.Height > 0)
        {
            height = Math.Min(height, pageSize.Height);
        }

        var maxX = Math.Max(0f, pageSize.Width - width);
        var maxY = Math.Max(0f, pageSize.Height - height);
        var x = Math.Clamp(bounds.X, 0f, maxX);
        var y = Math.Clamp(bounds.Y, 0f, maxY);

        return new RectangleF(x, y, width, height);
    }

    private static PdfTrueTypeFont ResolveFontToFitWidth(
        string text,
        ReferralOverlaySegment segment,
        float maxWidth,
        PdfStringFormat format)
    {
        var currentFontSize = ResolveFontSizePt(segment);

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

    private static float ResolveFontSizePt(ReferralOverlaySegment segment)
    {
        if (segment.FontSize is not > 0)
        {
            return DefaultFontSize;
        }

        // Розмір з конструктора в preview-пікселях → PDF points.
        var scaled = (float)segment.FontSize.Value / (float)ConstructorPreviewScale;
        return MathF.Max(MinFontSize, scaled);
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

public sealed record PdfOverlayRenderStats(
    byte[] PdfBytes,
    int SegmentsDrawn,
    int SegmentsSkippedEmpty,
    int SegmentsSkippedPage,
    int PdfPageCount);

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
