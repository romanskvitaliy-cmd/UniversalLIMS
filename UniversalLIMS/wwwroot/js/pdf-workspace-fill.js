(() => {
    /** Координати полів у БД збережені в пікселях preview з цим масштабом (як у конструкторі). */
    const CONSTRUCTOR_PREVIEW_SCALE = 1.35;

    const root = document.getElementById("pdfFillWorkspaceRoot");
    const pagesHost = document.getElementById("pdfFillPagesHost");
    const previewError = document.getElementById("pdfFillPreviewError");
    const saveButton = document.getElementById("pdfFillSaveButton");
    const previewFinalButton = document.getElementById("pdfFillPreviewFinalButton");
    const downloadFinalButton = document.getElementById("pdfFillDownloadFinalButton");
    const statusBox = document.getElementById("pdfFillStatus");
    const zoomSlider = document.getElementById("pdfFillZoomSlider");
    const zoomValue = document.getElementById("pdfFillZoomValue");

    if (!root || !pagesHost) {
        return;
    }

    const segments = Array.isArray(window.__pdfFillSegments) ? window.__pdfFillSegments : [];
    const pdfUrl = window.__pdfPreviewUrl;
    const savedByKey = window.__pdfFillSavedValues && typeof window.__pdfFillSavedValues === "object"
        ? window.__pdfFillSavedValues
        : {};

    let currentOrderId = window.__pdfFillOrderId || null;

    const showPreviewError = (message) => {
        if (!previewError) {
            return;
        }

        previewError.textContent = message;
        previewError.style.display = "block";
    };

    const showStatus = (message, type = "success") => {
        if (!statusBox) {
            return;
        }

        statusBox.textContent = message;
        statusBox.className = `alert alert-${type} mb-2`;
        statusBox.classList.remove("d-none");
    };

    const getAntiforgeryToken = () =>
        document.querySelector('#pdfFillAntiforgeryForm input[name="__RequestVerificationToken"]')?.value ?? "";

    const buildFinalPdfUrl = (orderId, download) => {
        const template = window.__pdfFillFinalPdfUrlTemplate || "";
        const url = new URL(template.replace("{orderId}", orderId), window.location.origin);
        if (download) {
            url.searchParams.set("download", "true");
        } else {
            url.searchParams.delete("download");
        }

        return url.toString();
    };

    const updateFinalPdfActions = (orderId) => {
        if (!orderId) {
            previewFinalButton?.classList.add("d-none");
            downloadFinalButton?.classList.add("d-none");
            return;
        }

        const previewUrl = buildFinalPdfUrl(orderId, false);
        const downloadUrl = buildFinalPdfUrl(orderId, true);

        if (previewFinalButton) {
            previewFinalButton.href = previewUrl;
            previewFinalButton.classList.remove("d-none");
        }

        if (downloadFinalButton) {
            downloadFinalButton.href = downloadUrl;
            downloadFinalButton.classList.remove("d-none");
        }
    };

    const buildInitialValuesMap = () => {
        const values = new Map();
        segments.forEach((segment) => {
            const templateFieldId = segment.templateFieldId ? String(segment.templateFieldId) : "";
            const fieldKey = segment.dataFieldKey || "";
            const sequence = segment.sequence ?? 0;
            const tag = segment.tag || "";
            const savedKey = tag && segments.filter((item) => item.tag === tag).length > 1
                ? `${tag}#${sequence}`
                : tag;
            const saved = savedByKey[templateFieldId]
                ?? savedByKey[savedKey]
                ?? savedByKey[tag]
                ?? (fieldKey ? savedByKey[fieldKey] : undefined);
            if (saved === undefined || saved === null) {
                return;
            }

            values.set(String(segment.segmentId || ""), String(saved));
        });
        return values;
    };

    const collectFilledValues = () => {
        const inputs = [...document.querySelectorAll(".pdf-fill-input")];
        inputs.sort((left, right) => {
            const leftSeq = Number.parseInt(left.dataset.sequence || "0", 10) || 0;
            const rightSeq = Number.parseInt(right.dataset.sequence || "0", 10) || 0;
            return leftSeq - rightSeq;
        });

        const byTemplateFieldId = new Map();
        inputs.forEach((input) => {
            const templateFieldId = (input.dataset.templateFieldId || "").trim();
            if (!templateFieldId) {
                return;
            }

            const segmentValue = input.value;
            if (byTemplateFieldId.has(templateFieldId)) {
                const merged = byTemplateFieldId.get(templateFieldId);
                byTemplateFieldId.set(templateFieldId, `${merged}\n${segmentValue}`);
            } else {
                byTemplateFieldId.set(templateFieldId, segmentValue);
            }
        });

        return [...byTemplateFieldId.entries()].map(([templateFieldId, value]) => ({
            templateFieldId,
            value: value.trim()
        }));
    };

    const normalizePixel = (value, fallback = 0) => {
        const raw = Number.parseFloat(String(value));
        if (!Number.isFinite(raw)) {
            return fallback;
        }

        return Math.round(raw);
    };

    const getOutputScale = () => Math.min(window.devicePixelRatio || 1, 3);

    class PdfFillEngine {
        constructor() {
            this.pdf = null;
            this.pageLayers = new Map();
            this.renderGeneration = 0;
            this.zoomDebounceId = null;
        }

        getZoomPercent() {
            return Number.parseInt(zoomSlider?.value ?? "100", 10) || 100;
        }

        getViewportScale() {
            return CONSTRUCTOR_PREVIEW_SCALE * (this.getZoomPercent() / 100);
        }

        getCoordinateScale() {
            return this.getViewportScale() / CONSTRUCTOR_PREVIEW_SCALE;
        }

        captureFieldValues() {
            const values = new Map();
            document.querySelectorAll(".pdf-fill-input").forEach((input) => {
                const segmentId = input.dataset.segmentId;
                if (segmentId) {
                    values.set(segmentId, input.value);
                }
            });
            return values;
        }

        async init() {
            if (!window.pdfjsLib || !pdfUrl) {
                showPreviewError("Не вдалося ініціалізувати PDF preview.");
                return;
            }

            const pdfjsLib = window.pdfjsLib;
            pdfjsLib.GlobalWorkerOptions.workerSrc =
                "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";

            this.pdf = await pdfjsLib.getDocument(pdfUrl).promise;
            await this.refreshView(buildInitialValuesMap());
            updateFinalPdfActions(currentOrderId);
            this.bindUi();
        }

        async refreshView(preservedValues = null) {
            const values = preservedValues ?? this.captureFieldValues();
            const generation = ++this.renderGeneration;

            await this.renderPages();
            if (generation !== this.renderGeneration) {
                return;
            }

            this.renderFillFields(values);
            if (zoomValue) {
                zoomValue.textContent = `${this.getZoomPercent()}%`;
            }
        }

        async renderPages() {
            pagesHost.innerHTML = "";
            this.pageLayers.clear();

            const viewportScale = this.getViewportScale();
            const outputScale = getOutputScale();

            for (let pageNumber = 1; pageNumber <= this.pdf.numPages; pageNumber++) {
                const page = await this.pdf.getPage(pageNumber);
                const viewport = page.getViewport({ scale: viewportScale });

                const cssWidth = Math.floor(viewport.width);
                const cssHeight = Math.floor(viewport.height);

                const wrapper = document.createElement("div");
                wrapper.className = "position-relative border bg-white mb-3 mx-auto pdf-fill-page";
                wrapper.style.width = `${cssWidth}px`;
                wrapper.style.height = `${cssHeight}px`;
                wrapper.dataset.page = String(pageNumber);

                const canvas = document.createElement("canvas");
                canvas.width = Math.floor(cssWidth * outputScale);
                canvas.height = Math.floor(cssHeight * outputScale);
                canvas.style.width = `${cssWidth}px`;
                canvas.style.height = `${cssHeight}px`;
                wrapper.appendChild(canvas);

                const layer = document.createElement("div");
                layer.className = "position-absolute top-0 start-0 field-overlay-layer";
                layer.style.width = `${cssWidth}px`;
                layer.style.height = `${cssHeight}px`;
                layer.style.pointerEvents = "none";
                layer.style.zIndex = "30";
                layer.dataset.page = String(pageNumber);
                wrapper.appendChild(layer);
                this.pageLayers.set(pageNumber, layer);

                pagesHost.appendChild(wrapper);

                const ctx = canvas.getContext("2d", { alpha: false });
                if (ctx) {
                    ctx.imageSmoothingEnabled = true;
                    ctx.imageSmoothingQuality = "high";
                    const transform = outputScale !== 1 ? [outputScale, 0, 0, outputScale, 0, 0] : null;
                    await page.render({
                        canvasContext: ctx,
                        viewport,
                        transform
                    }).promise;
                }
            }
        }

        renderFillFields(preservedValues = new Map()) {
            const coordScale = this.getCoordinateScale();

            segments.forEach((segment) => {
                const page = normalizePixel(segment.page, 1);
                const layer = this.pageLayers.get(page) ?? this.pageLayers.get(1);
                if (!layer) {
                    return;
                }

                const width = Math.max(20, normalizePixel(segment.width * coordScale, 120));
                const height = Math.max(14, normalizePixel(segment.height * coordScale, 28));
                const layerWidth = layer.clientWidth || Number.MAX_SAFE_INTEGER;
                const layerHeight = layer.clientHeight || Number.MAX_SAFE_INTEGER;
                const maxLeft = Math.max(0, layerWidth - width);
                const maxTop = Math.max(0, layerHeight - height);
                const left = Math.min(normalizePixel(segment.x * coordScale), maxLeft);
                const top = Math.min(normalizePixel(segment.y * coordScale), maxTop);

                const box = document.createElement("div");
                box.className = "position-absolute pdf-fill-field-box";
                box.style.left = `${left}px`;
                box.style.top = `${top}px`;
                box.style.width = `${width}px`;
                box.style.height = `${height}px`;
                box.title = segment.title || segment.tag || segment.dataFieldKey || "";

                const fieldKey = segment.dataFieldKey || "";
                const displayLabel = segment.tag || fieldKey || "";
                const useTextarea = segment.allowMultiline === true;
                const segmentId = String(segment.segmentId || "");

                const input = document.createElement(useTextarea ? "textarea" : "input");
                input.className = "pdf-fill-input form-control form-control-sm";
                input.dataset.fieldKey = fieldKey;
                input.dataset.segmentId = segmentId;
                input.dataset.templateFieldId = String(segment.templateFieldId || "");
                input.dataset.tag = String(segment.tag || "");
                input.dataset.sequence = String(segment.sequence ?? 0);
                input.placeholder = displayLabel;
                input.style.pointerEvents = "auto";

                const preserved = preservedValues.get(segmentId);
                if (preserved !== undefined) {
                    input.value = preserved;
                } else {
                    const seq = Number.parseInt(input.dataset.sequence || "0", 10) || 0;
                    const tagKey = displayLabel && segments.filter((item) => item.tag === displayLabel).length > 1
                        ? `${displayLabel}#${seq}`
                        : displayLabel;
                    const templateFieldId = String(segment.templateFieldId || "");
                    const saved = savedByKey[templateFieldId]
                        ?? savedByKey[tagKey]
                        ?? savedByKey[displayLabel]
                        ?? savedByKey[fieldKey];
                    input.value = saved === undefined || saved === null ? "" : String(saved);
                }

                if (!useTextarea) {
                    input.type = "text";
                } else {
                    input.rows = Math.max(2, Math.floor(height / 16));
                }

                box.appendChild(input);
                layer.appendChild(box);
            });
        }

        scheduleZoomRefresh() {
            window.clearTimeout(this.zoomDebounceId);
            this.zoomDebounceId = window.setTimeout(() => {
                const values = this.captureFieldValues();
                this.refreshView(values).catch((error) => {
                    console.error(error);
                    showPreviewError("Помилка перемасштабування PDF.");
                });
            }, 120);
        }

        async saveValues() {
            const payload = collectFilledValues();
            if (payload.length === 0) {
                showStatus("Немає полів на PDF для збереження.", "warning");
                return;
            }

            if (!window.__pdfFillSaveUrl) {
                showStatus("URL збереження не налаштовано.", "danger");
                return;
            }

            saveButton.disabled = true;

            const requestBody = {
                orderId: currentOrderId,
                values: payload
            };
            console.log("[PdfWorkspace Fill] save payload:", JSON.parse(JSON.stringify(requestBody)));

            try {
                const response = await fetch(window.__pdfFillSaveUrl, {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json",
                        RequestVerificationToken: getAntiforgeryToken()
                    },
                    body: JSON.stringify(requestBody)
                });

                const body = await response.json().catch(() => ({}));
                if (!response.ok) {
                    const detail = body.detail || body.inner || "";
                    throw new Error(
                        detail
                            ? `${body.message || "Помилка збереження"}: ${detail}`
                            : body.message || `Помилка збереження (${response.status}).`);
                }

                currentOrderId = body.orderId ?? body.OrderId ?? currentOrderId;
                window.__pdfFillOrderId = currentOrderId;

                const received = body.received ?? body.Received ?? payload.length;
                const mapped = body.mapped ?? body.Mapped ?? 0;
                const saved = body.saved ?? body.Saved ?? 0;
                const skippedUnmapped = body.skippedUnmapped ?? body.SkippedUnmapped ?? 0;
                const skippedEmpty = body.skippedEmpty ?? body.SkippedEmpty ?? 0;

                const message = body.message
                    || `Прийнято: ${received}, збережено: ${saved}, без мапінгу: ${skippedUnmapped}, очищено: ${skippedEmpty}.`;

                const hasIssues = skippedUnmapped > 0 || (mapped > 0 && saved === 0 && skippedEmpty < mapped);
                showStatus(message, saved > 0 && !hasIssues ? "success" : "warning");
                if (currentOrderId) {
                    updateFinalPdfActions(currentOrderId);
                }

                if (currentOrderId && (saved > 0 || skippedEmpty > 0)) {
                    const url = new URL(window.location.href);
                    url.searchParams.set("orderId", currentOrderId);
                    window.history.replaceState(null, "", url.toString());
                }

                console.log("[PdfWorkspace Fill] saved", body);
            } catch (error) {
                console.error(error);
                showStatus(error.message || "Не вдалося зберегти значення.", "danger");
            } finally {
                saveButton.disabled = false;
            }
        }

        bindUi() {
            if (zoomSlider) {
                zoomSlider.addEventListener("input", () => this.scheduleZoomRefresh());
            }

            if (saveButton) {
                saveButton.addEventListener("click", () => {
                    this.saveValues();
                });
            }
        }
    }

    const engine = new PdfFillEngine();
    engine.init().catch((error) => {
        console.error(error);
        showPreviewError("Помилка завантаження PDF.");
    });
})();
