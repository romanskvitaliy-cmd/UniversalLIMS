/**
 * Read-only PDF preview with field overlays for Template Permissions.
 * Coordinate logic mirrors Map FieldEngine (PREVIEW_SCALE 1.35, same API).
 */
(() => {
    const PREVIEW_SCALE = 1.35;
    const ZOOM_MIN = 75;
    const ZOOM_MAX = 300;

    const normalizeGuid = (value) => String(value ?? "").trim().toLowerCase();

    const parsePixel = (value, fallback = 0) => {
        const raw = Number.parseFloat(String(value));
        return Number.isFinite(raw) ? raw : fallback;
    };

    const clampInt = (value, min, max, fallback) => {
        const parsed = Number.parseInt(String(value), 10);
        if (!Number.isFinite(parsed)) {
            return fallback;
        }
        return Math.min(max, Math.max(min, parsed));
    };

    class PermissionsPdfPreview {
        constructor(options) {
            this.scrollRoot = options.scrollRoot;
            this.pagesHost = options.pagesHost;
            this.versionId = options.versionId;
            this.pdfUrl = options.pdfUrl;
            this.onFieldSelect = options.onFieldSelect;
            this.pdf = null;
            this.pageLayers = new Map();
            this.pageViewports = new Map();
            this.boxesByFieldId = new Map();
            this.overlaysByFieldId = new Map();
            this.activeFieldId = null;
            this.previewScale = PREVIEW_SCALE;
            this.zoomPercent = 100;
            this.zoomDebounceId = null;
            this.apiFieldsCache = [];
            this.zoomSlider = options.zoomSlider ?? null;
            this.zoomValue = options.zoomValue ?? null;
        }

        getCoordinateScale() {
            return this.zoomPercent / 100;
        }

        getViewportScale() {
            return PREVIEW_SCALE * this.getCoordinateScale();
        }

        getOutputScale() {
            return Math.min(window.devicePixelRatio || 1, 3);
        }

        normalizeGuidKey(value) {
            return normalizeGuid(value);
        }

        readSegmentSequence(segment, fallback) {
            const parsed = Number.parseInt(String(segment?.sequence ?? segment?.Sequence ?? fallback), 10);
            return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
        }

        resolveApiSegments(field) {
            const source = field ?? {};
            const rawSegments = Array.isArray(source.segments)
                ? source.segments
                : Array.isArray(source.Segments)
                    ? source.Segments
                    : [];
            if (rawSegments.length > 0) {
                return [...rawSegments]
                    .sort((left, right) => {
                        const leftSequence = Number(left?.sequence ?? left?.Sequence ?? 0);
                        const rightSequence = Number(right?.sequence ?? right?.Sequence ?? 0);
                        return leftSequence - rightSequence;
                    })
                    .map((segment, index) => ({
                        ...segment,
                        sequence: index + 1
                    }));
            }

            return [{
                sequence: 1,
                page: source.page ?? source.pageNumber ?? source.PageNumber ?? 1,
                x: source.x ?? source.positionX ?? source.PositionX ?? 24,
                y: source.y ?? source.positionY ?? source.PositionY ?? 24,
                width: source.width ?? source.Width ?? 220,
                height: source.height ?? source.Height ?? 28,
                isPrimary: true
            }];
        }

        coerceSegmentField(field, segment) {
            const source = field ?? {};
            const segmentSource = segment ?? {};
            const fieldId = this.normalizeGuidKey(
                source.id ?? source.fieldId ?? source.templateFieldId ?? source.Id ?? source.FieldId);
            if (!fieldId) {
                return null;
            }

            const segmentSequence = this.readSegmentSequence(segmentSource, 1);
            return {
                fieldId,
                segmentSequence,
                tag: source.tag ?? source.Tag ?? "",
                title: source.title ?? source.Title ?? "",
                page: segmentSource.page ?? segmentSource.pageNumber ?? segmentSource.PageNumber ?? 1,
                x: segmentSource.x ?? segmentSource.positionX ?? segmentSource.PositionX ?? 24,
                y: segmentSource.y ?? segmentSource.positionY ?? segmentSource.PositionY ?? 24,
                width: segmentSource.width ?? segmentSource.Width ?? 220,
                height: segmentSource.height ?? segmentSource.Height ?? 28,
                textOffsetX: parsePixel(source.textOffsetX ?? source.TextOffsetX ?? 0),
                textOffsetY: parsePixel(source.textOffsetY ?? source.TextOffsetY ?? 0)
            };
        }

        expandApiFieldsToOverlayFields(fields) {
            const overlayFields = [];
            (fields ?? []).forEach((field) => {
                this.resolveApiSegments(field).forEach((segment) => {
                    const coerced = this.coerceSegmentField(field, segment);
                    if (!coerced) {
                        return;
                    }

                    overlayFields.push(this.normalizeField(coerced));
                });
            });
            return overlayFields;
        }

        toViewportRect(field, page) {
            const viewport = this.pageViewports.get(page);
            const coordScale = this.getCoordinateScale();
            const rawX = Math.max(0, (Number(field.x) || 0) * coordScale);
            const rawY = Math.max(0, (Number(field.y) || 0) * coordScale);
            const rawW = Math.max(20, (Number(field.width) || 220) * coordScale);
            const rawH = Math.max(14, (Number(field.height) || 28) * coordScale);
            const rawRect = { left: rawX, top: rawY, width: rawW, height: rawH };
            if (!viewport) {
                return rawRect;
            }

            const layer = this.pageLayers.get(page);
            const layerWidth = layer?.clientWidth ?? viewport.width;
            const layerHeight = layer?.clientHeight ?? viewport.height;
            const fitsLayer = (rect) =>
                Number.isFinite(rect.left) &&
                Number.isFinite(rect.top) &&
                rect.width >= 8 &&
                rect.height >= 8 &&
                rect.left >= -4 &&
                rect.top >= -4 &&
                rect.left + rect.width <= layerWidth + 4 &&
                rect.top + rect.height <= layerHeight + 4;

            if (fitsLayer(rawRect)) {
                return rawRect;
            }

            const [vx1, vy1] = viewport.convertToViewportPoint(rawX, rawY);
            const [vx2, vy2] = viewport.convertToViewportPoint(rawX + rawW, rawY + rawH);
            const converted = {
                left: Math.min(vx1, vx2),
                top: Math.min(vy1, vy2),
                width: Math.abs(vx2 - vx1),
                height: Math.abs(vy2 - vy1)
            };

            if (fitsLayer(converted)) {
                return converted;
            }

            return rawRect;
        }

        normalizeField(field) {
            const fallbackPage = 1;
            const page = this.clampInt(field.page, 1, this.pdf?.numPages ?? 1, fallbackPage);
            const rect = this.toViewportRect(field, page);
            return {
                fieldId: field.fieldId,
                segmentSequence: field.segmentSequence ?? 1,
                tag: field.tag || "",
                title: (field.title || "").trim(),
                page,
                x: Math.max(0, rect.left),
                y: Math.max(0, rect.top),
                width: Math.max(20, rect.width),
                height: Math.max(14, rect.height)
            };
        }

        clampInt(value, min, max, fallback) {
            return clampInt(value, min, max, fallback);
        }

        async renderPages() {
            this.pagesHost.innerHTML = "";
            this.pageLayers.clear();
            this.pageViewports.clear();
            const viewportScale = this.getViewportScale();
            const outputScale = this.getOutputScale();
            this.previewScale = viewportScale;

            for (let pageNumber = 1; pageNumber <= this.pdf.numPages; pageNumber++) {
                const page = await this.pdf.getPage(pageNumber);
                const viewport = page.getViewport({ scale: viewportScale });
                this.pageViewports.set(pageNumber, viewport);

                const cssWidth = Math.floor(viewport.width);
                const cssHeight = Math.floor(viewport.height);

                const wrapper = document.createElement("div");
                wrapper.className = "template-permissions-pdf-page position-relative border bg-white mb-3";
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
                layer.className = "position-absolute top-0 start-0 template-permissions-overlay-layer";
                layer.style.width = `${cssWidth}px`;
                layer.style.height = `${cssHeight}px`;
                layer.dataset.page = String(pageNumber);
                wrapper.appendChild(layer);
                this.pageLayers.set(pageNumber, layer);

                this.pagesHost.appendChild(wrapper);
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

        clearOverlays() {
            this.boxesByFieldId.clear();
            this.overlaysByFieldId.clear();
            this.pageLayers.forEach((layer) => {
                layer.innerHTML = "";
            });
        }

        applyBoxVisualState(box, isActive) {
            const label = box.querySelector(".template-permissions-overlay-label");
            const palette = isActive
                ? { border: "#e11d48", bg: "rgba(225, 29, 72, 0.28)", label: "rgba(225, 29, 72, 0.95)" }
                : { border: "#94a3b8", bg: "rgba(148, 163, 184, 0.2)", label: "rgba(100, 116, 139, 0.92)" };

            box.classList.toggle("is-active", isActive);
            box.style.border = `${isActive ? 3 : 2}px solid ${palette.border}`;
            box.style.background = palette.bg;
            box.style.zIndex = isActive ? "48" : "20";
            box.style.opacity = isActive ? "1" : "0.82";

            if (label) {
                label.style.background = palette.label;
                label.style.display = isActive ? "block" : "none";
            }
        }

        renderFieldBox(field) {
            const layer = this.pageLayers.get(field.page) ?? this.pageLayers.get(1);
            if (!layer) {
                return;
            }

            const maxLeft = Math.max(0, layer.clientWidth - field.width);
            const maxTop = Math.max(0, layer.clientHeight - field.height);
            const x = Math.min(field.x, maxLeft);
            const y = Math.min(field.y, maxTop);

            const box = document.createElement("button");
            box.type = "button";
            box.className = "position-absolute template-permissions-overlay-box";
            box.dataset.fieldId = field.fieldId;
            box.dataset.page = String(field.page);
            box.dataset.segmentSequence = String(field.segmentSequence ?? 1);
            box.style.left = `${x}px`;
            box.style.top = `${y}px`;
            box.style.width = `${field.width}px`;
            box.style.height = `${field.height}px`;
            box.title = field.title || field.tag || "Поле";

            const label = document.createElement("span");
            label.className = "template-permissions-overlay-label small px-1 text-truncate";
            label.textContent = field.title || field.tag || "Поле";
            box.appendChild(label);

            box.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();
                if (typeof this.onFieldSelect === "function") {
                    this.onFieldSelect(field.fieldId);
                }
            });

            layer.appendChild(box);

            const list = this.overlaysByFieldId.get(field.fieldId) ?? [];
            list.push(box);
            this.overlaysByFieldId.set(field.fieldId, list);

            const primary = !this.boxesByFieldId.has(field.fieldId)
                || field.segmentSequence === 1;
            if (primary) {
                this.boxesByFieldId.set(field.fieldId, box);
            }

            this.applyBoxVisualState(box, normalizeGuid(field.fieldId) === this.activeFieldId);
        }

        renderAllFields(fields) {
            this.clearOverlays();
            fields.forEach((field) => this.renderFieldBox(field));
            this.updateAllBoxVisualStates();
        }

        updateAllBoxVisualStates() {
            this.overlaysByFieldId.forEach((boxes, fieldId) => {
                const isActive = normalizeGuid(fieldId) === this.activeFieldId;
                boxes.forEach((box) => this.applyBoxVisualState(box, isActive));
            });
        }

        async loadFieldsFromApi() {
            const response = await fetch(`/api/template-fields/${this.versionId}`, { credentials: "same-origin" });
            if (!response.ok) {
                return [];
            }

            const payload = await response.json();
            return Array.isArray(payload) ? payload : [];
        }

        selectField(fieldId, options = {}) {
            const { scrollPreview = true } = options;
            this.activeFieldId = normalizeGuid(fieldId) || null;
            this.updateAllBoxVisualStates();

            if (!scrollPreview || !this.activeFieldId) {
                return;
            }

            const anchor = this.boxesByFieldId.get(this.activeFieldId)
                ?? this.overlaysByFieldId.get(this.activeFieldId)?.[0];
            if (!anchor || !this.scrollRoot) {
                return;
            }

            const rootRect = this.scrollRoot.getBoundingClientRect();
            const boxRect = anchor.getBoundingClientRect();
            const deltaTop = boxRect.top - rootRect.top - rootRect.height / 2 + boxRect.height / 2;
            const deltaLeft = boxRect.left - rootRect.left - rootRect.width / 2 + boxRect.width / 2;
            this.scrollRoot.scrollBy({ top: deltaTop, left: deltaLeft, behavior: "smooth" });
        }

        updateZoomLabel() {
            if (this.zoomValue) {
                this.zoomValue.textContent = `${this.zoomPercent}%`;
            }
        }

        bindZoomControls() {
            if (!this.zoomSlider) {
                return;
            }

            this.zoomSlider.value = String(this.zoomPercent);
            this.updateZoomLabel();

            this.zoomSlider.addEventListener("input", () => {
                const parsed = Number.parseInt(this.zoomSlider.value, 10);
                if (Number.isFinite(parsed)) {
                    this.zoomPercent = Math.max(ZOOM_MIN, Math.min(ZOOM_MAX, parsed));
                    this.updateZoomLabel();
                    this.schedulePdfZoomRefresh(this.zoomPercent);
                }
            });
        }

        schedulePdfZoomRefresh(newZoomPercent) {
            window.clearTimeout(this.zoomDebounceId);
            this.zoomDebounceId = window.setTimeout(() => {
                this.refreshPdfZoom(newZoomPercent).catch((error) => {
                    console.error("Permissions PDF zoom refresh failed", error);
                });
            }, 120);
        }

        async refreshPdfZoom(newZoomPercent) {
            if (!this.pdf) {
                return;
            }

            const previousFieldId = this.activeFieldId;
            this.zoomPercent = Math.max(ZOOM_MIN, Math.min(ZOOM_MAX, newZoomPercent));
            this.previewScale = this.getViewportScale();

            await this.renderPages();

            const prepared = this.expandApiFieldsToOverlayFields(this.apiFieldsCache);
            this.renderAllFields(prepared);

            if (previousFieldId) {
                this.selectField(previousFieldId, { scrollPreview: false });
            }
        }

        async init() {
            if (!window.pdfjsLib || !this.pdfUrl || !this.pagesHost) {
                return false;
            }

            const pdfjsLib = window.pdfjsLib;
            pdfjsLib.GlobalWorkerOptions.workerSrc = window.__pdfJsWorkerSrc
                || document.querySelector('script[src*="pdf.min.js"]')?.src?.replace("pdf.min.js", "pdf.worker.min.js")
                || "";

            this.pdf = await pdfjsLib.getDocument(this.pdfUrl).promise;
            this.bindZoomControls();
            await this.renderPages();

            this.apiFieldsCache = await this.loadFieldsFromApi();
            const prepared = this.expandApiFieldsToOverlayFields(this.apiFieldsCache);
            this.renderAllFields(prepared);
            return true;
        }
    }

    window.initTemplatePermissionsPreview = (options) => {
        const preview = new PermissionsPdfPreview(options);
        return preview.init().then((ready) => (ready ? preview : null));
    };
})();
