using Syncfusion.Drawing;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class ReferralPdfOverlayRenderer
{
    public const decimal ConstructorPreviewScale = 1.35m;

    public byte[] Render(
        Stream originalPdfStream,
        IReadOnlyList<ReferralOverlaySegment> segments,
        IReadOnlyDictionary<Guid, string?> valuesByDataFieldId)
    {
        ArgumentNullException.ThrowIfNull(originalPdfStream);

        using var loadedDocument = new PdfLoadedDocument(originalPdfStream);

        foreach (var overlaySegment in segments)
        {
            if (overlaySegment.DataFieldId is null)
            {
                continue;
            }

            if (!valuesByDataFieldId.TryGetValue(overlaySegment.DataFieldId.Value, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (overlaySegment.PageNumber < 1 || overlaySegment.PageNumber > loadedDocument.Pages.Count)
            {
                continue;
            }

            var page = loadedDocument.Pages[overlaySegment.PageNumber - 1];
            var bounds = ToPdfRectangle(overlaySegment);
            var font = CreateFont(overlaySegment);
            var format = CreateStringFormat(overlaySegment.TextAlignment);

            page.Graphics.DrawString(value, font, PdfBrushes.Black, bounds, format);
        }

        using var output = new MemoryStream();
        loadedDocument.Save(output);
        return output.ToArray();
    }

    private static RectangleF ToPdfRectangle(ReferralOverlaySegment segment)
    {
        var scale = 1f / (float)ConstructorPreviewScale;
        return new RectangleF(
            (float)segment.PositionX * scale,
            (float)segment.PositionY * scale,
            (float)segment.Width * scale,
            (float)segment.Height * scale);
    }

    private static PdfFont CreateFont(ReferralOverlaySegment segment)
    {
        var fontFamily = ResolveFontFamily(segment.FontName);
        var fontSize = segment.FontSize is > 0 ? (float)segment.FontSize.Value : 10f;
        return new PdfStandardFont(fontFamily, fontSize);
    }

    private static PdfFontFamily ResolveFontFamily(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return PdfFontFamily.Helvetica;
        }

        return fontName.Trim().ToLowerInvariant() switch
        {
            "times" or "times new roman" or "timesnewroman" => PdfFontFamily.TimesRoman,
            "courier" or "courier new" or "couriernew" => PdfFontFamily.Courier,
            _ => PdfFontFamily.Helvetica
        };
    }

    private static PdfStringFormat CreateStringFormat(TextAlignment alignment)
    {
        var format = new PdfStringFormat
        {
            LineAlignment = PdfVerticalAlignment.Top,
            WordWrap = PdfWordWrapType.Word
        };

        format.Alignment = alignment switch
        {
            TextAlignment.Center => PdfTextAlignment.Center,
            TextAlignment.Right => PdfTextAlignment.Right,
            _ => PdfTextAlignment.Left
        };

        return format;
    }
}

public sealed class ReferralOverlaySegment
{
    public Guid? DataFieldId { get; init; }

    public int PageNumber { get; init; }

    public decimal PositionX { get; init; }

    public decimal PositionY { get; init; }

    public decimal Width { get; init; }

    public decimal Height { get; init; }

    public TextAlignment TextAlignment { get; init; }

    public string? FontName { get; init; }

    public decimal? FontSize { get; init; }
}
