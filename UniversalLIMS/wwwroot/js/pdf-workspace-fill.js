(() => {
    const CONSTRUCTOR_PREVIEW_SCALE = 1.35;
    const BLUR_AUTOSAVE_MS = 500;

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
    let blurSaveTimer = null;
    let isSaving = false;

    const segmentsByTemplateFieldId = new Map();
    segments.forEach((segment) => {
        const id = String(segment.templateFieldId || "");
        if (!id) {
            return;
        }

        if (!segmentsByTemplateFieldId.has(id)) {
            segmentsByTemplateFieldId.set(id, []);
        }

        segmentsByTemplateFieldId.get(id).push(segment);
    });

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

        if (previewFinalButton) {
            previewFinalButton.href = buildFinalPdfUrl(orderId, false);
            previewFinalButton.classList.remove("d-none");
        }

        if (downloadFinalButton) {
            downloadFinalButton.href = buildFinalPdfUrl(orderId, true);
            downloadFinalButton.classList.remove("d-none");
        }
    };

    const resolveSavedValueForSegment = (segment) => {
        const templateFieldId = String(segment.templateFieldId || "");
        const tag = segment.tag || "";
        const sequence = segment.sequence ?? 0;
        const fieldKey = segment.dataFieldKey || "";
        const siblings = segmentsByTemplateFieldId.get(templateFieldId) || [];
        const isMultiSegment = siblings.length > 1;

        if (isMultiSegment) {
            const lineKey = tag ? `${tag}#${sequence}` : "";
            if (lineKey && savedByKey[lineKey] !== undefined && savedByKey[lineKey] !== null) {
                return String(savedByKey[lineKey]);
            }

            const full = savedByKey[templateFieldId];
            if (full !== undefined && full !== null) {
                const lines = String(full).split("\n");
                const ordered = [...siblings].sort(
                    (left, right) => (left.sequence ?? 0) - (right.sequence ?? 0)
                );
                const index = ordered.findIndex((item) => String(item.segmentId) === String(segment.segmentId));
                return index >= 0 && index < lines.length ? lines[index] : "";
            }
        }

        const saved = savedByKey[templateFieldId]
            ?? (tag ? savedByKey[tag] : undefined)
            ?? (fieldKey ? savedByKey[fieldKey] : undefined);

        return saved === undefined || saved === null ? "" : String(saved);
    };

    const buildInitialValuesMap = () => {
        const values = new Map();
        segments.forEach((segment) => {
            const segmentId = String(segment.segmentId || "");
            if (!segmentId) {
                return;
            }

            values.set(segmentId, resolveSavedValueForSegment(segment));
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
                console.warn("[PdfWorkspace Fill] input without templateFieldId", input);
                return;
            }

            const value = input.value ?? "";
            if (byTemplateFieldId.has(templateFieldId)) {
                byTemplateFieldId.set(
                    templateFieldId,
                    `${byTemplateFieldId.get(templateFieldId)}\n${value}`
                );
            } else {
                byTemplateFieldId.set(templateFieldId, value);
            }
        });

        return [...byTemplateFieldId.entries()].map(([templateFieldId, value]) => ({
            templateFieldId,
            value: value.trim()
        }));
    };

    const normalizePixel = (value, fallback = 0) => {
        const raw = Number.parseFloat(String(value));
        return Number.isFinite(raw) ? Math.round(raw) : fallback;
    };

    const getOutputScale = () => Math.min(window.devicePixelRatio || 1, 3);

    class PdfFillEngine {
        constructor() {
            this.pdf = null;
            this.pageLayers = new Map();
            this.renderGeneration = 0;
            this.zoomDebounceId = null;
            this.focusedInput = null;
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
                    values.set(segmentId, input.value ?? "");
                }
            });
            return values;
        }

        async init() {
            if (!window.pdfjsLib || !pdfUrl) {
                showPreviewError("Не вдалося ініціалізувати PDF preview.");
                return;
            }

            window.pdfjsLib.GlobalWorkerOptions.workerSrc =
                "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";

            this.pdf = await window.pdfjsLib.getDocument(pdfUrl).promise;
            await this.refreshView(buildInitialValuesMap());
            updateFinalPdfActions(currentOrderId);
            this.bindUi();
        }

        async refreshView(preservedValues = null) {
            if (this.focusedInput) {
                return;
            }

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
                layer.className = "position-absolute top-0 start-0 pdf-fill-overlay-layer";
                layer.style.width = `${cssWidth}px`;
                layer.style.height = `${cssHeight}px`;
                layer.dataset.page = String(pageNumber);
                wrapper.appendChild(layer);
                this.pageLayers.set(pageNumber, layer);

                pagesHost.appendChild(wrapper);

                const ctx = canvas.getContext("2d", { alpha: false });
                if (ctx) {
                    const transform = outputScale !== 1 ? [outputScale, 0, 0, outputScale, 0, 0] : null;
                    await page.render({ canvasContext: ctx, viewport, transform }).promise;
                }
            }
        }

        renderFillFields(preservedValues = new Map()) {
            const coordScale = this.getCoordinateScale();

            segments.forEach((segment) => {
                const templateFieldId = String(segment.templateFieldId || "");
                if (!templateFieldId) {
                    console.warn("[PdfWorkspace Fill] segment without templateFieldId", segment);
                    return;
                }

                const page = normalizePixel(segment.page, 1);
                const layer = this.pageLayers.get(page) ?? this.pageLayers.get(1);
                if (!layer) {
                    return;
                }

                const width = Math.max(24, normalizePixel(segment.width * coordScale, 120));
                const height = Math.max(16, normalizePixel(segment.height * coordScale, 28));
                const layerWidth = layer.clientWidth || Number.MAX_SAFE_INTEGER;
                const layerHeight = layer.clientHeight || Number.MAX_SAFE_INTEGER;
                const left = Math.min(normalizePixel(segment.x * coordScale), Math.max(0, layerWidth - width));
                const top = Math.min(normalizePixel(segment.y * coordScale), Math.max(0, layerHeight - height));

                const box = document.createElement("div");
                box.className = "pdf-fill-field-box";
                box.style.left = `${left}px`;
                box.style.top = `${top}px`;
                box.style.width = `${width}px`;
                box.style.height = `${height}px`;

                const useMultiline = segment.allowMultiline === true;
                const segmentId = String(segment.segmentId || "");
                const input = document.createElement(useMultiline ? "textarea" : "input");
                input.className = "pdf-fill-input";
                input.dataset.segmentId = segmentId;
                input.dataset.templateFieldId = templateFieldId;
                input.dataset.sequence = String(segment.sequence ?? 0);

                if (!useMultiline) {
                    input.type = "text";
                    input.autocomplete = "off";
                } else {
                    input.rows = Math.max(2, Math.floor(height / 14));
                    input.wrap = "soft";
                }

                const preserved = preservedValues.get(segmentId);
                input.value = preserved !== undefined ? preserved : resolveSavedValueForSegment(segment);

                input.addEventListener("focus", () => {
                    this.focusedInput = input;
                    box.classList.add("pdf-fill-field-box--active");
                });

                input.addEventListener("blur", () => {
                    if (this.focusedInput === input) {
                        this.focusedInput = null;
                    }
                    box.classList.remove("pdf-fill-field-box--active");
                    this.scheduleBlurSave();
                });

                input.addEventListener("input", () => {
                    input.style.color = input.value ? "#111" : "transparent";
                });

                input.addEventListener("keydown", (event) => {
                    if (!useMultiline && event.key === "Enter") {
                        event.preventDefault();
                        input.blur();
                    }
                });

                box.addEventListener("mousedown", (event) => {
                    if (event.target === input) {
                        return;
                    }

                    event.preventDefault();
                    input.focus();
                });

                input.style.color = input.value ? "#111" : "transparent";
                box.appendChild(input);
                layer.appendChild(box);
            });
        }

        scheduleBlurSave() {
            window.clearTimeout(blurSaveTimer);
            blurSaveTimer = window.setTimeout(() => {
                if (isSaving || this.focusedInput) {
                    return;
                }

                this.saveValues({ silent: true, reason: "blur" });
            }, BLUR_AUTOSAVE_MS);
        }

        scheduleZoomRefresh() {
            if (this.focusedInput) {
                return;
            }

            window.clearTimeout(this.zoomDebounceId);
            this.zoomDebounceId = window.setTimeout(() => {
                this.refreshView(this.captureFieldValues()).catch((error) => {
                    console.error(error);
                    showPreviewError("Помилка перемасштабування PDF.");
                });
            }, 150);
        }

        async saveValues(options = {}) {
            const { silent = false, reason = "button" } = options;
            const payload = collectFilledValues();

            if (!window.__pdfFillSaveUrl) {
                if (!silent) {
                    showStatus("URL збереження не налаштовано.", "danger");
                }
                return;
            }

            if (isSaving) {
                return;
            }

            isSaving = true;
            if (saveButton) {
                saveButton.disabled = true;
            }

            const requestBody = { orderId: currentOrderId, values: payload };
            console.log(`[PdfWorkspace Fill] save (${reason}):`, JSON.parse(JSON.stringify(requestBody)));

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
                            : body.message || `Помилка збереження (${response.status}).`
                    );
                }

                currentOrderId = body.orderId ?? body.OrderId ?? currentOrderId;
                window.__pdfFillOrderId = currentOrderId;

                const received = body.received ?? body.Received ?? payload.length;
                const saved = body.saved ?? body.Saved ?? 0;
                const skippedUnmapped = body.skippedUnmapped ?? body.SkippedUnmapped ?? 0;
                const failedFields = body.failedFields ?? body.FailedFields ?? [];

                let message = body.message
                    || `Прийнято: ${received}, збережено: ${saved}, помилок: ${failedFields.length}.`;

                if (failedFields.length > 0) {
                    const details = failedFields
                        .slice(0, 5)
                        .map((item) => `${item.templateFieldId || item.TemplateFieldId}: ${item.reason || item.Reason}`)
                        .join("; ");
                    message = `${message} Не збережено: ${details}${failedFields.length > 5 ? "…" : ""}.`;
                }

                const ok = saved > 0 && failedFields.length === 0 && skippedUnmapped === 0;
                if (!silent || !ok) {
                    showStatus(message, ok ? "success" : "warning");
                }

                if (currentOrderId) {
                    updateFinalPdfActions(currentOrderId);
                    const url = new URL(window.location.href);
                    url.searchParams.set("orderId", currentOrderId);
                    window.history.replaceState(null, "", url.toString());
                }

                console.log("[PdfWorkspace Fill] saved", body);
            } catch (error) {
                console.error(error);
                if (!silent) {
                    showStatus(error.message || "Не вдалося зберегти значення.", "danger");
                }
            } finally {
                isSaving = false;
                if (saveButton) {
                    saveButton.disabled = false;
                }
            }
        }

        bindUi() {
            zoomSlider?.addEventListener("input", () => this.scheduleZoomRefresh());
            saveButton?.addEventListener("click", () => {
                window.clearTimeout(blurSaveTimer);
                this.saveValues({ reason: "button" });
            });
        }
    }

    new PdfFillEngine().init().catch((error) => {
        console.error(error);
        showPreviewError("Помилка завантаження PDF.");
    });
})();
