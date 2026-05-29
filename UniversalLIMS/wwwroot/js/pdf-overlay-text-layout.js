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
     * Рамка сегмента (anchor) без text-offset і без baseline — як box у Map.
     * @returns {{ left: number, top: number, width: number, height: number }}
     */
    const getSegmentBoxBounds = (positionX, positionY, width, height, coordScale) => {
        const scale = toNumber(coordScale, 1);
        return {
            left: toNumber(positionX) * scale,
            top: toNumber(positionY) * scale,
            width: toNumber(width, 120) * scale,
            height: toNumber(height, 28) * scale
        };
    };

    /**
     * Зсув тексту всередині рамки (offset + baseline), як getCalibrationInputRect у Map.
     */
    const getTextTranslateInBox = (textOffsetX, textOffsetY, coordScale) => {
        const scale = toNumber(coordScale, 1);
        return {
            x: toNumber(textOffsetX) * scale,
            y: (toNumber(textOffsetY) + BASELINE_OFFSET_PX) * scale
        };
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

    const mapTextAlign = (horizontalAlignment, textAlignment) => {
        const raw = String(horizontalAlignment ?? textAlignment ?? "Left").toLowerCase();
        if (raw === "center") {
            return "center";
        }
        if (raw === "right") {
            return "right";
        }
        return "left";
    };

    /**
     * Застосовує типографіку до input/textarea у PdfWorkspace Fill (WYSIWYG як у Map).
     * @param {HTMLElement} fieldEl
     */
    const layoutTextInTextBox = (fieldEl, options) => {
        if (!fieldEl) {
            return;
        }

        const coordScale = toNumber(options?.coordScale, 1);
        const fontSizePx = resolveFontSizePx(options?.segmentFontSize, coordScale);
        const fontStack = options?.fontName
            ? `${options.fontName}, Arial, Helvetica, sans-serif`
            : '"Times New Roman", Times, serif';
        const lineHeightValue = toNumber(options?.lineHeight);
        const lineHeight = lineHeightValue > 0 ? lineHeightValue : 1.2;

        fieldEl.style.fontSize = `${fontSizePx}px`;
        fieldEl.style.fontFamily = fontStack;
        fieldEl.style.fontWeight = options?.fontBold ? "700" : "400";
        fieldEl.style.fontStyle = options?.fontItalic ? "italic" : "normal";
        fieldEl.style.textDecoration = options?.textUnderline ? "underline" : "none";
        fieldEl.style.lineHeight = String(lineHeight);
        fieldEl.style.textAlign = mapTextAlign(options?.horizontalAlignment, options?.textAlignment);

        if (options?.multiline) {
            fieldEl.style.whiteSpace = "pre-wrap";
            fieldEl.style.wordBreak = "break-word";
        } else {
            fieldEl.style.whiteSpace = "nowrap";
            fieldEl.style.wordBreak = "normal";
        }

        const boxHeightPx = toNumber(options?.containerHeightPx) > 0
            ? toNumber(options.containerHeightPx)
            : toNumber(options?.height, 28) * coordScale;
        const contentHeight = fontSizePx * lineHeight;
        const vAlign = String(options?.verticalAlignment ?? "Top").toLowerCase();
        let paddingTop = 0;
        if (boxHeightPx > 0 && contentHeight > 0) {
            if (vAlign.includes("middle")) {
                paddingTop = Math.max(0, (boxHeightPx - contentHeight) / 2);
            } else if (vAlign.includes("bottom")) {
                paddingTop = Math.max(0, boxHeightPx - contentHeight);
            }
        }

        fieldEl.style.paddingTop = `${paddingTop}px`;
        fieldEl.style.paddingRight = "0";
        fieldEl.style.paddingBottom = "0";
        fieldEl.style.paddingLeft = "0";

        const offsetX = toNumber(options?.textOffsetX) * coordScale;
        const offsetY = (toNumber(options?.textOffsetY) + BASELINE_OFFSET_PX) * coordScale;
        if (offsetX !== 0 || offsetY !== 0) {
            fieldEl.style.transform = `translate(${offsetX}px, ${offsetY}px)`;
            fieldEl.style.transformOrigin = "0 0";
        } else if (BASELINE_OFFSET_PX !== 0) {
            fieldEl.style.transform = `translate(0px, ${BASELINE_OFFSET_PX * coordScale}px)`;
            fieldEl.style.transformOrigin = "0 0";
        } else {
            fieldEl.style.transform = "";
            fieldEl.style.transformOrigin = "";
        }
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
        getSegmentBoxBounds,
        getTextTranslateInBox,
        getPreviewBounds,
        resolveFontSizePx,
        layoutTextInTextBox,
        drawText
    };
})();
