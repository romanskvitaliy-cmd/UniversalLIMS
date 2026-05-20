namespace UniversalLIMS.Application.Registration;

/// <summary>
/// Єдині правила позиціонування тексту overlay на PDF (preview конструктора → координати PDF).
/// Використовується ReferralPdfOverlayRenderer, live-калібруванням і PdfWorkspaceFill.
/// </summary>
public static class PdfOverlayTextLayout
{
    /// <summary>Масштаб preview у конструкторі; координати в БД збережені в цих пікселях.</summary>
    public const decimal ConstructorPreviewScale = 1.35m;

    /// <summary>Базовий зсув по Y у пікселях preview (еквівалент baseline у PDF).</summary>
    public const decimal BaselineYOffsetPx = 3m;

    public sealed record PreviewBounds(
        decimal X,
        decimal Y,
        decimal Width,
        decimal Height);

    /// <summary>Прямокутник тексту в координатах preview (до ділення на ConstructorPreviewScale).</summary>
    public static PreviewBounds GetPreviewBounds(
        decimal positionX,
        decimal positionY,
        decimal width,
        decimal height,
        decimal textOffsetX = 0m,
        decimal textOffsetY = 0m) =>
        new(
            positionX + textOffsetX,
            positionY + textOffsetY + BaselineYOffsetPx,
            width,
            height);

    /// <summary>Перетворення preview → PDF points (Syncfusion).</summary>
    public static (float X, float Y, float Width, float Height) ToPdfPoints(PreviewBounds bounds)
    {
        var scale = 1f / (float)ConstructorPreviewScale;
        return (
            (float)bounds.X * scale,
            (float)bounds.Y * scale,
            (float)bounds.Width * scale,
            (float)bounds.Height * scale);
    }

    public static float ResolveFontSizePt(decimal? segmentFontSizePt, float maxWidthPt, string text)
    {
        const float defaultFontSize = 10f;
        const float minFontSize = 6f;
        const float step = 0.5f;

        var current = segmentFontSizePt is > 0 ? (float)segmentFontSizePt.Value : defaultFontSize;
        // Ширина оцінюється грубо для auto-shrink; точне вимірювання — у PDF renderer.
        var estimatedCharWidth = current * 0.55f;
        var estimatedWidth = text.Length * estimatedCharWidth;
        while (estimatedWidth > maxWidthPt && current > minFontSize)
        {
            current -= step;
            estimatedCharWidth = current * 0.55f;
            estimatedWidth = text.Length * estimatedCharWidth;
        }

        return MathF.Max(minFontSize, current);
    }
}
