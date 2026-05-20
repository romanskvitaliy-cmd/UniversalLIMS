/**
 * Live preview для overlay-тексту на PDF (конструктор).
 * Формули збігаються з UniversalLIMS.Application.Registration.PdfOverlayTextLayout.
 */
(() => {
    const CONSTRUCTOR_PREVIEW_SCALE = 1.35;
    const BASELINE_OFFSET_PX = 3;

    const toNumber = (value, fallback = 0) => {
        const raw = Number.parseFloat(String(value));
        return Number.isFinite(raw) ? raw : fallback;
    };

    /**
     * @returns {{ left: number, top: number, width: number, height: number }}
     */
    const getPreviewBounds = (
        positionX,
        positionY,
        width,
        height,
        textOffsetX,
        textOffsetY,
        coordScale) => {
        const scale = toNumber(coordScale, 1);
        const ox = toNumber(textOffsetX);
        const oy = toNumber(textOffsetY);
        return {
            left: (toNumber(positionX) + ox) * scale,
            top: (toNumber(positionY) + oy + BASELINE_OFFSET_PX) * scale,
            width: toNumber(width, 120) * scale,
            height: toNumber(height, 28) * scale
        };
    };

    const resolveFontSizePx = (segmentFontSize, coordScale) => {
        const base = toNumber(segmentFontSize) > 0 ? toNumber(segmentFontSize) : 10;
        return base * toNumber(coordScale, 1);
    };

    /**
     * @param {CanvasRenderingContext2D} ctx
     */
    const drawText = (ctx, options) => {
        const text = String(options.text ?? "").trim();
        if (!text) {
            return;
        }

        const bounds = options.bounds;
        const hAlign = String(options.horizontalAlignment ?? "Left").toLowerCase();
        const vAlign = String(options.verticalAlignment ?? "Top").toLowerCase();
        const fontFamily = options.fontFamily || "Arial, sans-serif";
        let fontSizePx = toNumber(options.fontSizePx, 10);

        ctx.fillStyle = options.color || "#000000";
        ctx.textBaseline = "alphabetic";

        const fitWidth = () => {
            while (fontSizePx > 6 && ctx.measureText(text).width > bounds.width - 4) {
                fontSizePx -= 0.5;
            }
        };

        ctx.font = `${options.fontWeight === "bold" ? "bold " : ""}${fontSizePx}px ${fontFamily}`;
        fitWidth();

        let x = bounds.left + 2;
        if (hAlign === "center") {
            x = bounds.left + bounds.width / 2;
            ctx.textAlign = "center";
        } else if (hAlign === "right") {
            x = bounds.left + bounds.width - 2;
            ctx.textAlign = "right";
        } else {
            ctx.textAlign = "left";
        }

        let y = bounds.top + fontSizePx * 0.85;
        if (vAlign === "middle") {
            y = bounds.top + bounds.height / 2 + fontSizePx * 0.35;
        } else if (vAlign === "bottom") {
            y = bounds.top + bounds.height - 4;
        }

        ctx.fillText(text, x, y);
        ctx.textAlign = "left";
    };

    window.PdfOverlayTextLayout = {
        CONSTRUCTOR_PREVIEW_SCALE,
        BASELINE_OFFSET_PX,
        getPreviewBounds,
        resolveFontSizePx,
        drawText
    };
})();
