(() => {
    const PREVIEW_SCALE = 1.35;

    const pagesHost = document.getElementById("pdfFillPagesHost");
    const previewError = document.getElementById("pdfFillPreviewError");
    const saveButton = document.getElementById("pdfFillSaveButton");
    const previewFinalButton = document.getElementById("pdfFillPreviewFinalButton");
    const downloadFinalButton = document.getElementById("pdfFillDownloadFinalButton");
    const statusBox = document.getElementById("pdfFillStatus");
    const zoomSlider = document.getElementById("pdfFillZoomSlider");
    const zoomValue = document.getElementById("pdfFillZoomValue");

    if (!pagesHost) {
        return;
    }

    const segments = Array.isArray(window.__pdfFillSegments) ? window.__pdfFillSegments : [];
    const pdfUrl = window.__pdfPreviewUrl;
    const savedByKey = window.__pdfFillSavedValues && typeof window.__pdfFillSavedValues === "object"
        ? window.__pdfFillSavedValues
        : {};

    let currentOrderId = window.__pdfFillOrderId || null;
    let isSaving = false;
    let zoomDebounceId = null;

    const tagByTemplateFieldId = new Map();
    const segmentsByTemplateFieldId = new Map();
    segments.forEach((segment) => {
        const fieldId = String(segment.templateFieldId || "");
        if (!fieldId) {
            return;
        }

        tagByTemplateFieldId.set(fieldId, segment.tag || segment.title || fieldId);
        if (!segmentsByTemplateFieldId.has(fieldId)) {
            segmentsByTemplateFieldId.set(fieldId, []);
        }
        segmentsByTemplateFieldId.get(fieldId).push(segment);
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

        if (siblings.length > 1) {
            const lineKey = tag ? `${tag}#${sequence}` : "";
            if (lineKey && savedByKey[lineKey] != null) {
                return String(savedByKey[lineKey]);
            }
            const full = savedByKey[templateFieldId];
            if (full != null) {
                const lines = String(full).split("\n");
                const ordered = [...siblings].sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0));
                const index = ordered.findIndex((item) => String(item.segmentId) === String(segment.segmentId));
                return index >= 0 && index < lines.length ? lines[index] : "";
            }
        }

        const saved = savedByKey[templateFieldId]
            ?? (tag ? savedByKey[tag] : undefined)
            ?? (fieldKey ? savedByKey[fieldKey] : undefined);
        return saved == null ? "" : String(saved);
    };

    const collectFilledValues = () => {
        const inputs = [...document.querySelectorAll(".pdf-fill-input")];
        inputs.sort((a, b) =>
            (Number.parseInt(a.dataset.sequence || "0", 10) || 0)
            - (Number.parseInt(b.dataset.sequence || "0", 10) || 0));

        const byTemplateFieldId = new Map();
        inputs.forEach((input) => {
            const templateFieldId = (input.dataset.templateFieldId || "").trim();
            if (!templateFieldId) {
                return;
            }
            const value = input.value ?? "";
            if (byTemplateFieldId.has(templateFieldId)) {
                byTemplateFieldId.set(templateFieldId, `${byTemplateFieldId.get(templateFieldId)}\n${value}`);
            } else {
                byTemplateFieldId.set(templateFieldId, value);
            }
        });

        return [...byTemplateFieldId.entries()].map(([templateFieldId, value]) => ({
            templateFieldId,
            value: value.trim()
        }));
    };

    const formatFailedFields = (failedFields) => {
        if (!Array.isArray(failedFields) || failedFields.length === 0) {
            return "";
        }
        return failedFields
            .map((item) => {
                const id = String(item.templateFieldId || item.TemplateFieldId || "");
                const tag = tagByTemplateFieldId.get(id) || id || "?";
                const reason = item.reason || item.Reason || "невідома причина";
                return `${tag}: ${reason}`;
            })
            .join("; ");
    };

    const px = (value, fallback = 0) => {
        const n = Number.parseFloat(String(value));
        return Number.isFinite(n) ? Math.round(n) : fallback;
    };

    const num = (value, fallback = 0) => {
        const n = Number.parseFloat(String(value));
        return Number.isFinite(n) ? n : fallback;
    };

    const getZoomPercent = () => Number.parseInt(zoomSlider?.value ?? "100", 10) || 100;
    const getCoordScale = () => (PREVIEW_SCALE * (getZoomPercent() / 100)) / PREVIEW_SCALE;

    const captureValues = () => {
        const values = new Map();
        document.querySelectorAll(".pdf-fill-input").forEach((input) => {
            const segmentId = input.dataset.segmentId;
            if (segmentId) {
                values.set(segmentId, input.value ?? "");
            }
        });
        return values;
    };

    const buildInitialValues = () => {
        const values = new Map();
        segments.forEach((segment) => {
            const segmentId = String(segment.segmentId || "");
            if (segmentId) {
                values.set(segmentId, resolveSavedValueForSegment(segment));
            }
        });
        return values;
    };

    let pdfDoc = null;
    const pageLayers = new Map();
    let renderGeneration = 0;
    let focusSegmentId = null;

    const renderPages = async () => {
        pagesHost.innerHTML = "";
        pageLayers.clear();

        const viewportScale = PREVIEW_SCALE * (getZoomPercent() / 100);
        const deviceScale = Math.min(window.devicePixelRatio || 1, 3);

        for (let pageNumber = 1; pageNumber <= pdfDoc.numPages; pageNumber++) {
            const page = await pdfDoc.getPage(pageNumber);
            const viewport = page.getViewport({ scale: viewportScale });
            const cssWidth = Math.floor(viewport.width);
            const cssHeight = Math.floor(viewport.height);

            const wrapper = document.createElement("div");
            wrapper.className = "position-relative border bg-white mb-3 mx-auto pdf-fill-page";
            wrapper.style.width = `${cssWidth}px`;
            wrapper.style.height = `${cssHeight}px`;
            wrapper.dataset.page = String(pageNumber);

            const canvas = document.createElement("canvas");
            canvas.className = "pdf-fill-page-canvas";
            canvas.width = Math.floor(cssWidth * deviceScale);
            canvas.height = Math.floor(cssHeight * deviceScale);
            canvas.style.width = `${cssWidth}px`;
            canvas.style.height = `${cssHeight}px`;
            wrapper.appendChild(canvas);

            const layer = document.createElement("div");
            layer.className = "pdf-fill-overlay-layer";
            layer.style.width = `${cssWidth}px`;
            layer.style.height = `${cssHeight}px`;
            layer.dataset.page = String(pageNumber);
            wrapper.appendChild(layer);
            pageLayers.set(pageNumber, layer);

            pagesHost.appendChild(wrapper);

            const ctx = canvas.getContext("2d", { alpha: false });
            if (ctx) {
                const transform = deviceScale !== 1 ? [deviceScale, 0, 0, deviceScale, 0, 0] : null;
                await page.render({ canvasContext: ctx, viewport, transform }).promise;
            }
        }
    };

    const renderOverlayFields = (values, restoreFocusSegmentId = null) => {
        const coordScale = getCoordScale();

        segments.forEach((segment) => {
            const templateFieldId = String(segment.templateFieldId || "");
            const segmentId = String(segment.segmentId || "");
            if (!templateFieldId || !segmentId) {
                return;
            }

            const page = px(segment.page, 1);
            const layer = pageLayers.get(page) ?? pageLayers.get(1);
            if (!layer) {
                return;
            }

            const width = Math.max(24, px(num(segment.width) * coordScale, 120));
            const height = Math.max(14, px(num(segment.height) * coordScale, 28));
            const layerWidth = layer.clientWidth || width;
            const layerHeight = layer.clientHeight || height;
            const left = Math.min(px(num(segment.x) * coordScale), Math.max(0, layerWidth - width));
            const top = Math.min(px(num(segment.y) * coordScale), Math.max(0, layerHeight - height));

            const box = document.createElement("div");
            box.className = "pdf-fill-field-box";
            box.style.left = `${left}px`;
            box.style.top = `${top}px`;
            box.style.width = `${width}px`;
            box.style.height = `${height}px`;

            const multiline = segment.allowMultiline === true;
            const input = document.createElement(multiline ? "textarea" : "input");
            input.className = "pdf-fill-input";
            input.dataset.segmentId = segmentId;
            input.dataset.templateFieldId = templateFieldId;
            input.dataset.sequence = String(segment.sequence ?? 0);
            input.spellcheck = false;

            if (!multiline) {
                input.type = "text";
                input.autocomplete = "off";
            } else {
                input.rows = Math.max(2, Math.floor(height / 14));
            }

            const fontSize = num(segment.fontSize) > 0 ? num(segment.fontSize) * coordScale : 12;
            input.style.fontSize = `${fontSize}px`;
            if (segment.fontName) {
                input.style.fontFamily = `${segment.fontName}, Arial, sans-serif`;
            }

            input.value = values.has(segmentId) ? values.get(segmentId) : resolveSavedValueForSegment(segment);

            input.addEventListener("focus", () => {
                focusSegmentId = segmentId;
                box.classList.add("pdf-fill-field-box--active");
            });
            input.addEventListener("blur", () => {
                if (focusSegmentId === segmentId) {
                    focusSegmentId = null;
                }
                box.classList.remove("pdf-fill-field-box--active");
            });

            box.addEventListener("mousedown", (event) => {
                if (event.target !== input) {
                    event.preventDefault();
                    input.focus();
                }
            });

            box.appendChild(input);
            layer.appendChild(box);
        });

        if (restoreFocusSegmentId) {
            const input = document.querySelector(
                `.pdf-fill-input[data-segment-id="${restoreFocusSegmentId}"]`
            );
            input?.focus();
        }
    };

    const refreshView = async (preservedValues = null, restoreFocus = null) => {
        const values = preservedValues ?? captureValues();
        if (!values.size) {
            buildInitialValues().forEach((v, k) => values.set(k, v));
        }

        const generation = ++renderGeneration;
        await renderPages();
        if (generation !== renderGeneration) {
            return;
        }

        renderOverlayFields(values, restoreFocus ?? focusSegmentId);
        if (zoomValue) {
            zoomValue.textContent = `${getZoomPercent()}%`;
        }
    };

    const saveValues = async () => {
        if (!window.__pdfFillSaveUrl) {
            showStatus("URL збереження не налаштовано.", "danger");
            return;
        }
        if (isSaving) {
            return;
        }

        const payload = collectFilledValues();
        isSaving = true;
        if (saveButton) {
            saveButton.disabled = true;
        }

        const requestBody = { orderId: currentOrderId, values: payload };
        console.log("[PdfWorkspace Fill] save:", requestBody);

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
            const failedText = formatFailedFields(failedFields);

            let message = body.message || `Прийнято: ${received}, збережено: ${saved}.`;
            if (skippedUnmapped > 0) {
                message += ` Пропущено (без мапінгу): ${skippedUnmapped}.`;
            }
            if (failedText) {
                message += ` Не збережено: ${failedText}.`;
            }

            const ok = failedFields.length === 0 && skippedUnmapped === 0 && saved > 0;
            showStatus(message, ok ? "success" : (saved > 0 ? "warning" : "danger"));

            if (currentOrderId) {
                updateFinalPdfActions(currentOrderId);
                const url = new URL(window.location.href);
                url.searchParams.set("orderId", currentOrderId);
                window.history.replaceState(null, "", url.toString());
            }
        } catch (error) {
            console.error(error);
            showStatus(error.message || "Не вдалося зберегти значення.", "danger");
        } finally {
            isSaving = false;
            if (saveButton) {
                saveButton.disabled = false;
            }
        }
    };

    const init = async () => {
        if (!window.pdfjsLib || !pdfUrl) {
            showPreviewError("Не вдалося ініціалізувати PDF preview.");
            return;
        }

        window.pdfjsLib.GlobalWorkerOptions.workerSrc =
            "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";

        pdfDoc = await window.pdfjsLib.getDocument(pdfUrl).promise;
        await refreshView(buildInitialValues());
        updateFinalPdfActions(currentOrderId);

        zoomSlider?.addEventListener("input", () => {
            window.clearTimeout(zoomDebounceId);
            zoomDebounceId = window.setTimeout(() => {
                refreshView(captureValues(), focusSegmentId).catch((error) => {
                    console.error(error);
                    showPreviewError("Помилка перемасштабування PDF.");
                });
            }, 150);
        });

        saveButton?.addEventListener("click", () => saveValues());
    };

    init().catch((error) => {
        console.error(error);
        showPreviewError("Помилка завантаження PDF.");
    });
})();
