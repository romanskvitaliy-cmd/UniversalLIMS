using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
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

    private static readonly ConcurrentDictionary<string, PdfBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<ReferralPdfOverlayRenderer>? _logger;

    public ReferralPdfOverlayRenderer(ILogger<ReferralPdfOverlayRenderer>? logger = null)
    {
        _logger = logger;
    }

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
        IReadOnlyDictionary<Guid, string?> valuesByDataFieldId,
        bool skipEmptyText = true,
        IReadOnlyDictionary<string, string>? textByUiField = null,
        IReadOnlyDictionary<Guid, string>? textByTemplateFieldId = null,
        IReadOnlyDictionary<string, string>? textById = null)
    {
        ArgumentNullException.ThrowIfNull(originalPdfStream);

        // Calibration preview: skipEmptyText=false → малюємо всі сегменти з payload (без IsVisible/permissions).
        var debugDrawAllIncomingSegments = !skipEmptyText;

        var drawn = 0;
        var skippedEmpty = 0;
        var skippedPage = 0;
        var segmentIndex = 0;

        using var loadedDocument = new PdfLoadedDocument(originalPdfStream);
        var pageCount = loadedDocument.Pages.Count;

        _logger?.LogInformation(
            "RenderWithStats start: segments={SegmentCount}, pdfPages={PageCount}, skipEmptyText={SkipEmpty}, debugDrawAll={DebugAll}",
            segments.Count,
            pageCount,
            skipEmptyText,
            debugDrawAllIncomingSegments);

        var useSimpleTextById = textById is { Count: > 0 };

        foreach (var overlaySegment in segments)
        {
            segmentIndex++;
            var id = overlaySegment.TemplateFieldId?.ToString("D") ?? "no-id";
            var key = id;

            // Пріоритет: текст на сегменті (WYSIWYG preview / waterfall), потім словники.
            var value = ResolveSegmentDrawableText(overlaySegment);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (useSimpleTextById
                    && overlaySegment.TemplateFieldId is Guid templateFieldId
                    && textById!.TryGetValue(templateFieldId.ToString("D"), out var uiText))
                {
                    value = uiText;
                }
                else if (!useSimpleTextById)
                {
                    value = ResolveOverlayText(
                        overlaySegment,
                        valuesByDataFieldId,
                        textByUiField,
                        textByTemplateFieldId) ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                var textToDraw = overlaySegment.TextToDraw ?? string.Empty;
                var segmentPayloadText = overlaySegment.Text ?? string.Empty;
                value = !string.IsNullOrWhiteSpace(textToDraw)
                    ? textToDraw.Trim()
                    : segmentPayloadText.Trim();
            }

            var logText = value.Length <= 80 ? value : $"{value[..80]}…";
            _logger?.LogInformation(
                "Drawing segment {TemplateFieldId} with text: '{Text}'",
                key,
                logText);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (!useSimpleTextById && skipEmptyText)
                {
                    LogSkipped(id, "empty-text (skipEmptyText=true)", segmentIndex, overlaySegment);
                }

                skippedEmpty++;
                continue;
            }

            if (overlaySegment.PageNumber < 1 || overlaySegment.PageNumber > pageCount)
            {
                LogSkipped(
                    id,
                    $"bad-page (page={overlaySegment.PageNumber}, pdfPages={pageCount})",
                    segmentIndex,
                    overlaySegment);
                skippedPage++;
                continue;
            }

            var page = loadedDocument.Pages[overlaySegment.PageNumber - 1];
            var pageSize = page.Size;
            var rawBounds = ToPdfRectangle(overlaySegment);
            var bounds = FitBoundsToPage(rawBounds, pageSize);

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                LogSkipped(
                    id,
                    $"zero-bounds (w={bounds.Width}, h={bounds.Height}, rawX={rawBounds.X}, rawY={rawBounds.Y})",
                    segmentIndex,
                    overlaySegment);
                skippedEmpty++;
                continue;
            }

            var format = CreateStringFormat(overlaySegment);
            var font = ResolveFontToFitWidth(value, overlaySegment, bounds.Width, format);

            _logger?.LogInformation(
                "DRAWN Field {Id}: seq={Seq} page={Page} textLen={TextLen}",
                id,
                overlaySegment.SegmentSequence,
                overlaySegment.PageNumber,
                value.Length);

            DrawOverlayText(page, value, font, bounds, format, overlaySegment);
            drawn++;
        }

        _logger?.LogInformation(
            "RenderWithStats end: drawn={Drawn}, skippedEmpty={SkippedEmpty}, skippedPage={SkippedPage}, total={Total}",
            drawn,
            skippedEmpty,
            skippedPage,
            segments.Count);

        using var output = new MemoryStream();
        loadedDocument.Save(output);
        return new PdfOverlayRenderStats(output.ToArray(), drawn, skippedEmpty, skippedPage, pageCount);
    }

    private void LogSkipped(string id, string reason, int segmentIndex, ReferralOverlaySegment segment)
    {
        _logger?.LogInformation("SKIPPED Field {Id}: {Reason}", id, reason);
    }

    private void DrawOverlayText(
        PdfPageBase page,
        string value,
        PdfTrueTypeFont font,
        RectangleF bounds,
        PdfStringFormat format,
        ReferralOverlaySegment segment)
    {
        var idLabel = segment.TemplateFieldId?.ToString("D") ?? "no-id";
        _logger?.LogInformation(
            "DRAWING: Field {Id} | Text: '{Text}' | X: {X}, Y: {Y}, Width: {W}, Height: {H}",
            idLabel,
            value,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height);

        var brush = ResolveTextBrush(segment.TextColor);
        var (drawX, drawY) = ResolveDrawPoint(value, font, bounds, format);
        page.Graphics.DrawString(value, font, brush, drawX, drawY);
    }

    private static (float X, float Y) ResolveDrawPoint(
        string value,
        PdfTrueTypeFont font,
        RectangleF bounds,
        PdfStringFormat format)
    {
        var measureFormat = new PdfStringFormat
        {
            WordWrap = PdfWordWrapType.None,
            Alignment = PdfTextAlignment.Left,
            LineAlignment = PdfVerticalAlignment.Top
        };
        var size = font.MeasureString(value, measureFormat);

        var x = bounds.X;
        if (format.Alignment == PdfTextAlignment.Center)
        {
            x = bounds.X + Math.Max(0f, (bounds.Width - size.Width) / 2f);
        }
        else if (format.Alignment == PdfTextAlignment.Right)
        {
            x = bounds.X + Math.Max(0f, bounds.Width - size.Width);
        }

        var y = bounds.Y;
        if (format.LineAlignment == PdfVerticalAlignment.Middle)
        {
            y = bounds.Y + Math.Max(0f, (bounds.Height - size.Height) / 2f);
        }
        else if (format.LineAlignment == PdfVerticalAlignment.Bottom)
        {
            y = bounds.Y + Math.Max(0f, bounds.Height - size.Height);
        }

        return (x, y);
    }

    private static PdfBrush ResolveTextBrush(string? textColor)
    {
        var key = string.IsNullOrWhiteSpace(textColor) ? "#111111" : textColor.Trim();
        return BrushCache.GetOrAdd(key, static colorKey =>
        {
            if (TryParseHexColor(colorKey, out var color))
            {
                return new PdfSolidBrush(color);
            }

            return new PdfSolidBrush(Color.FromArgb(255, 17, 17, 17));
        });
    }

    private static bool TryParseHexColor(string? value, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length != 6
            || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    internal static string BuildUiTextLookupKey(Guid templateFieldId, int segmentSequence, decimal? positionX = null, decimal? positionY = null)
    {
        var sequence = segmentSequence > 0 ? segmentSequence : 1;
        if (positionX is not null && positionY is not null)
        {
            var x = (int)Math.Round(positionX.Value, MidpointRounding.AwayFromZero);
            var y = (int)Math.Round(positionY.Value, MidpointRounding.AwayFromZero);
            return $"{templateFieldId:D}|{sequence}|{x}|{y}";
        }

        return $"{templateFieldId:D}|{sequence}";
    }

    private static string? ResolveOverlayText(
        ReferralOverlaySegment segment,
        IReadOnlyDictionary<Guid, string?> valuesByDataFieldId,
        IReadOnlyDictionary<string, string>? textByUiField = null,
        IReadOnlyDictionary<Guid, string>? textByTemplateFieldId = null)
    {
        // WYSIWYG preview: текст із payload сегмента (кожен overlay-box) — пріоритет над словниками.
        var segmentText = ResolveSegmentDrawableText(segment);
        if (!string.IsNullOrWhiteSpace(segmentText))
        {
            return segmentText;
        }

        if (segment.TemplateFieldId is Guid templateFieldId)
        {
            var sequence = segment.SegmentSequence > 0 ? segment.SegmentSequence : 1;

            if (textByUiField is not null)
            {
                var positionKey = BuildUiTextLookupKey(
                    templateFieldId,
                    sequence,
                    segment.PositionX,
                    segment.PositionY);
                if (textByUiField.TryGetValue(positionKey, out var uiText)
                    && !string.IsNullOrWhiteSpace(uiText))
                {
                    return uiText;
                }

                var sequenceKey = BuildUiTextLookupKey(templateFieldId, sequence);
                if (textByUiField.TryGetValue(sequenceKey, out uiText)
                    && !string.IsNullOrWhiteSpace(uiText))
                {
                    return uiText;
                }

                if (sequence != 1)
                {
                    var primaryKey = BuildUiTextLookupKey(templateFieldId, 1);
                    if (textByUiField.TryGetValue(primaryKey, out uiText)
                        && !string.IsNullOrWhiteSpace(uiText))
                    {
                        return uiText;
                    }
                }
            }

            if (textByTemplateFieldId is not null
                && textByTemplateFieldId.TryGetValue(templateFieldId, out var uiTextByField)
                && !string.IsNullOrWhiteSpace(uiTextByField))
            {
                return uiTextByField;
            }
        }

        if (segment.DataFieldId is null ||
            !valuesByDataFieldId.TryGetValue(segment.DataFieldId.Value, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>Пріоритет: Text, потім TextToDraw (калібрування / UI).</summary>
    private static string ResolveSegmentDrawableText(ReferralOverlaySegment segment) =>
        !string.IsNullOrWhiteSpace(segment.Text)
            ? segment.Text.Trim()
            : !string.IsNullOrWhiteSpace(segment.TextToDraw)
                ? segment.TextToDraw.Trim()
                : string.Empty;

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

        // Явний розмір з конструктора / реєстратури — не зменшуємо під ширину (WYSIWYG).
        if (segment.FontSize is > 0)
        {
            return GetUnicodeFont(currentFontSize);
        }

        while (true)
        {
            var font = GetUnicodeFont(currentFontSize);
            var measureFormat = new PdfStringFormat
            {
                WordWrap = PdfWordWrapType.None,
                Alignment = PdfTextAlignment.Left,
                LineAlignment = PdfVerticalAlignment.Top
            };
            var measuredWidth = font.MeasureString(text, measureFormat).Width;
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

    public Guid? TemplateFieldId { get; init; }

    public int SegmentSequence { get; init; }

    public string? StorageKey { get; init; }

    /// <summary>Готовий текст для сегмента (наприклад, один рядок з waterfall).</summary>
    public string? Text { get; init; }

    /// <summary>Текст з UI (калібрування), якщо <see cref="Text"/> порожній.</summary>
    public string? TextToDraw { get; init; }

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

    /// <summary>Колір тексту (#RRGGBB) з калібрування.</summary>
    public string? TextColor { get; init; }
}
