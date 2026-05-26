(() => {
    /** Має збігатися з PdfOverlayTextLayout.ConstructorPreviewScale і конструктором Map. */
    const PREVIEW_SCALE = 1.35;
    const BASELINE_OFFSET_PX = 3;
    const TEXT_STYLE_PREFIX = "ULIMS_TEXT_STYLE:";
    const LAYOUT_OFFSET_MIN = -50;
    const LAYOUT_OFFSET_MAX = 50;

    const getOutputScale = () => {
        const ratio = window.devicePixelRatio || 1;
        return Math.min(Math.max(ratio * 1.5, 2), 4);
    };

    const pagesHost = document.getElementById("pdfFillPagesHost");
    const workspaceRoot = document.getElementById("pdfFillWorkspaceRoot");
    const zoomContainer = document.getElementById("pdfFillZoomContainer");
    const previewError = document.getElementById("pdfFillPreviewError");
    const saveButton = document.getElementById("pdfFillSaveButton");
    const previewFinalButton = document.getElementById("pdfFillPreviewFinalButton");
    const downloadFinalButton = document.getElementById("pdfFillDownloadFinalButton");
    const statusBox = document.getElementById("pdfFillStatus");
    const zoomSlider = document.getElementById("pdfFillZoomSlider");
    const zoomValue = document.getElementById("pdfFillZoomValue");
    const panelEditing = document.getElementById("pdfFillPanelEditing");
    const panelHint = document.getElementById("pdfFillPanelHint");
    const panelValue = document.getElementById("pdfFillPanelValue");
    const panelReadonlyNote = document.getElementById("pdfFillPanelReadonlyNote");
    const panelCharCount = document.getElementById("pdfFillPanelCharCount");
    const panelSaveBadge = document.getElementById("pdfFillSaveBadge");
    const panelSaveButton = document.getElementById("pdfFillPanelSaveButton");
    const panelNextField = document.getElementById("pdfFillPanelNextField");
    const panelFieldSearch = document.getElementById("pdfFillFieldSearch");
    const panelFieldList = document.getElementById("pdfFillFieldList");
    const autoSaveToggle = document.getElementById("pdfFillAutoSaveToggle");
    const layoutBadge = document.getElementById("pdfFillLayoutBadge");
    const saveLayoutButton = document.getElementById("pdfFillSaveLayoutButton");
    const fillFontFamily = document.getElementById("pdfFillFontFamily");
    const fillFontSize = document.getElementById("pdfFillFontSize");
    const fillFontSizeMinus = document.getElementById("pdfFillFontSizeMinus");
    const fillFontSizePlus = document.getElementById("pdfFillFontSizePlus");
    const fillBold = document.getElementById("pdfFillBold");
    const fillItalic = document.getElementById("pdfFillItalic");
    const fillUnderline = document.getElementById("pdfFillUnderline");
    const fillLineHeight = document.getElementById("pdfFillLineHeight");
    const fillOffsetX = document.getElementById("pdfFillOffsetX");
    const fillOffsetY = document.getElementById("pdfFillOffsetY");

    if (!pagesHost) {
        return;
    }

    const loadClientConfig = () => {
        const configNode = document.getElementById("pdfFillConfig");
        if (configNode?.textContent?.trim()) {
            try {
                return JSON.parse(configNode.textContent);
            } catch (error) {
                console.error("[PdfWorkspace Fill] config JSON parse error:", error);
            }
        }

        return {
            pdfJsWorkerSrc: window.__pdfJsWorkerSrc,
            pdfPreviewUrl: window.__pdfPreviewUrl,
            orderId: window.__pdfFillOrderId,
            saveUrl: window.__pdfFillSaveUrl,
            finalPdfUrlTemplate: window.__pdfFillFinalPdfUrlTemplate,
            savedValues: window.__pdfFillSavedValues,
            segments: window.__pdfFillSegments,
            expectedSegmentCount: window.__pdfFillSegments?.length ?? 0,
            firstFieldPage: 1
        };
    };

    const clientConfig = loadClientConfig();
    const segments = Array.isArray(clientConfig.segments) ? clientConfig.segments : [];
    const expectedSegmentCount = Number.parseInt(clientConfig.expectedSegmentCount ?? "0", 10) || 0;
    const firstFieldPage = Number.parseInt(clientConfig.firstFieldPage ?? "1", 10) || 1;
    const pdfUrl = clientConfig.pdfPreviewUrl || workspaceRoot?.dataset?.pdfPreviewUrl || null;
    const savedByKey = clientConfig.savedValues && typeof clientConfig.savedValues === "object"
        ? clientConfig.savedValues
        : {};

    let currentOrderId = clientConfig.orderId || null;
    let isSaving = false;
    let focusSegmentId = null;
    let selectedSegmentId = null;
    let isDirty = false;
    let lastSavedSignature = "";
    let autoSaveTimer = null;
    let panelSyncing = false;
    let layoutPanelSyncing = false;
    let isLayoutDirty = false;
    let isSavingLayout = false;
    const AUTO_SAVE_DELAY_MS = Number.parseInt(clientConfig.autoSaveDelayMs ?? "2500", 10) || 2500;
    const layoutSaveUrl = clientConfig.layoutSaveUrl || "";
    const segmentLayoutOverrides = new Map();
    const layoutDirtySegmentIds = new Set();
    let pdfDoc = null;
    const pageLayers = new Map();
    const pageMetrics = new Map();
    const pageViewports = new Map();

    const tagByTemplateFieldId = new Map();
    const segmentsByTemplateFieldId = new Map();
    const segmentById = new Map();

    const accessLevelRank = (segment) => {
        const raw = segment.accessLevel ?? segment.AccessLevel ?? "";
        if (typeof raw === "number") {
            return raw;
        }
        const normalized = String(raw).trim().toLowerCase();
        if (normalized === "approve") {
            return 3;
        }
        if (normalized === "write") {
            return 2;
        }
        if (normalized === "read") {
            return 1;
        }
        return 0;
    };

    const canWriteSegment = (segment) => {
        if (segment.canWrite === true || segment.CanWrite === true) {
            return true;
        }
        return accessLevelRank(segment) >= 2;
    };

    segments.forEach((segment) => {
        const fieldId = String(segment.templateFieldId || "");
        const segmentId = String(segment.segmentId || "");
        if (segmentId) {
            segmentById.set(segmentId, segment);
        }
        if (!fieldId) {
            console.warn("[PdfWorkspace Fill] segment without templateFieldId:", segment);
            return;
        }

        tagByTemplateFieldId.set(fieldId, segment.tag || segment.title || fieldId);
        if (!segmentsByTemplateFieldId.has(fieldId)) {
            segmentsByTemplateFieldId.set(fieldId, []);
        }
        segmentsByTemplateFieldId.get(fieldId).push(segment);
    });

    const hasWritableFields = segments.some((segment) => canWriteSegment(segment));

    const clampLayoutOffset = (value) => {
        const n = Number.parseFloat(String(value));
        if (!Number.isFinite(n)) {
            return 0;
        }
        return Math.min(LAYOUT_OFFSET_MAX, Math.max(LAYOUT_OFFSET_MIN, Math.round(n * 10) / 10));
    };

    const parseExtraTextStyle = (svgPathData) => {
        const raw = String(svgPathData ?? "").trim();
        if (!raw.startsWith(TEXT_STYLE_PREFIX)) {
            return {};
        }
        try {
            const parsed = JSON.parse(raw.slice(TEXT_STYLE_PREFIX.length));
            return {
                fontBold: parsed?.b === true || parsed?.bold === true,
                fontItalic: parsed?.i === true || parsed?.italic === true,
                textUnderline: parsed?.u === true || parsed?.underline === true
            };
        } catch {
            return {};
        }
    };

    const buildExtraTextStylePayload = (layout) => {
        const hasExtra = layout.fontBold === true
            || layout.fontItalic === true
            || layout.textUnderline === true;
        if (!hasExtra) {
            const legacy = String(layout.svgPathData ?? "").trim();
            return legacy.startsWith(TEXT_STYLE_PREFIX) ? null : (legacy || null);
        }
        return `${TEXT_STYLE_PREFIX}${JSON.stringify({
            b: layout.fontBold === true,
            i: layout.fontItalic === true,
            u: layout.textUnderline === true
        })}`;
    };

    const getEffectiveSegment = (baseSegment) => {
        const segmentId = String(baseSegment.segmentId || "");
        const override = segmentLayoutOverrides.get(segmentId);
        if (!override) {
            return baseSegment;
        }
        return { ...baseSegment, ...override };
    };

    const readLayoutState = (baseSegment) => {
        const effective = getEffectiveSegment(baseSegment);
        const extra = parseExtraTextStyle(effective.svgPathData);
        return {
            textOffsetX: num(effective.textOffsetX, 0),
            textOffsetY: num(effective.textOffsetY, 0),
            fontSize: effective.fontSize ?? null,
            fontName: effective.fontName ?? null,
            lineHeight: effective.lineHeight ?? null,
            horizontalAlignment: effective.horizontalAlignment || effective.textAlignment || "Left",
            verticalAlignment: effective.verticalAlignment || "Top",
            textAlignment: effective.textAlignment || effective.horizontalAlignment || "Left",
            svgPathData: effective.svgPathData ?? null,
            fontBold: extra.fontBold === true,
            fontItalic: extra.fontItalic === true,
            textUnderline: extra.textUnderline === true
        };
    };

    const applyLayoutPatch = (segmentId, patch) => {
        const base = segmentById.get(String(segmentId || ""));
        if (!base) {
            return;
        }
        const current = readLayoutState(base);
        const next = { ...current, ...patch };
        next.svgPathData = buildExtraTextStylePayload(next);
        segmentLayoutOverrides.set(String(segmentId), {
            textOffsetX: next.textOffsetX,
            textOffsetY: next.textOffsetY,
            fontSize: next.fontSize,
            fontName: next.fontName,
            lineHeight: next.lineHeight,
            horizontalAlignment: next.horizontalAlignment,
            verticalAlignment: next.verticalAlignment,
            textAlignment: next.textAlignment,
            svgPathData: next.svgPathData
        });
        layoutDirtySegmentIds.add(String(segmentId));
        isLayoutDirty = true;
        updateLayoutBadge("dirty");
        mountOverlays(captureValues(), segmentId);
        if (selectedSegmentId === segmentId) {
            syncLayoutControlsFromSegment(base);
        }
    };

    const updateLayoutBadge = (state) => {
        if (!layoutBadge) {
            return;
        }
        layoutBadge.classList.remove(
            "pdf-fill-panel__save-badge--saved",
            "pdf-fill-panel__save-badge--dirty",
            "pdf-fill-panel__save-badge--saving"
        );
        if (state === "saving") {
            layoutBadge.textContent = "Збереження макету…";
            layoutBadge.classList.add("pdf-fill-panel__save-badge--saving");
        } else if (state === "dirty") {
            layoutBadge.textContent = "Макет: є зміни";
            layoutBadge.classList.add("pdf-fill-panel__save-badge--dirty");
        } else {
            layoutBadge.textContent = "Макет збережено";
            layoutBadge.classList.add("pdf-fill-panel__save-badge--saved");
        }
    };

    const setLayoutControlsEnabled = (enabled) => {
        const controls = [
            fillFontFamily,
            fillFontSize,
            fillFontSizeMinus,
            fillFontSizePlus,
            fillBold,
            fillItalic,
            fillUnderline,
            fillLineHeight,
            fillOffsetX,
            fillOffsetY,
            saveLayoutButton,
            ...document.querySelectorAll(".pdf-fill-offset-step, .pdf-fill-shift, .pdf-fill-h-align, .pdf-fill-v-align")
        ];
        controls.forEach((control) => {
            if (control) {
                control.disabled = !enabled;
            }
        });
    };

    const syncLayoutControlsFromSegment = (baseSegment) => {
        if (!baseSegment) {
            setLayoutControlsEnabled(false);
            return;
        }
        const layout = readLayoutState(baseSegment);
        layoutPanelSyncing = true;
        setLayoutControlsEnabled(true);
        if (fillFontFamily) {
            fillFontFamily.value = layout.fontName || "";
        }
        if (fillFontSize) {
            fillFontSize.value = layout.fontSize != null ? String(layout.fontSize) : "";
        }
        if (fillLineHeight) {
            fillLineHeight.value = layout.lineHeight != null ? String(layout.lineHeight) : "";
        }
        if (fillOffsetX) {
            fillOffsetX.value = String(layout.textOffsetX);
        }
        if (fillOffsetY) {
            fillOffsetY.value = String(layout.textOffsetY);
        }
        if (fillBold) {
            fillBold.setAttribute("aria-pressed", layout.fontBold ? "true" : "false");
            fillBold.classList.toggle("active", layout.fontBold);
        }
        if (fillItalic) {
            fillItalic.setAttribute("aria-pressed", layout.fontItalic ? "true" : "false");
            fillItalic.classList.toggle("active", layout.fontItalic);
        }
        if (fillUnderline) {
            fillUnderline.setAttribute("aria-pressed", layout.textUnderline ? "true" : "false");
            fillUnderline.classList.toggle("active", layout.textUnderline);
        }
        document.querySelectorAll(".pdf-fill-h-align").forEach((button) => {
            const align = button.dataset.align || "";
            button.classList.toggle("active", align === layout.horizontalAlignment);
        });
        document.querySelectorAll(".pdf-fill-v-align").forEach((button) => {
            const align = button.dataset.align || "";
            button.classList.toggle("active", align === layout.verticalAlignment);
        });
        layoutPanelSyncing = false;
    };

    const buildLayoutFieldPayload = (segmentId) => {
        const base = segmentById.get(String(segmentId || ""));
        if (!base) {
            return null;
        }
        const effective = getEffectiveSegment(base);
        const layout = readLayoutState(base);
        return {
            templateFieldId: String(base.templateFieldId || ""),
            segmentId: String(base.segmentId || ""),
            textOffsetX: layout.textOffsetX,
            textOffsetY: layout.textOffsetY,
            pageNumber: px(base.page, 1),
            positionX: num(base.x, 0),
            positionY: num(base.y, 0),
            width: num(base.width, 120),
            height: num(base.height, 28),
            isPrimary: base.isPrimary !== false,
            textAlignment: layout.textAlignment || "Left",
            fontSize: layout.fontSize,
            fontName: layout.fontName || null,
            lineHeight: layout.lineHeight,
            horizontalAlignment: layout.horizontalAlignment,
            verticalAlignment: layout.verticalAlignment,
            svgPathData: layout.svgPathData,
            rowVersionBase64: effective.rowVersionBase64 || base.rowVersionBase64 || null
        };
    };

    const commitLayoutOverridesToSegments = () => {
        layoutDirtySegmentIds.forEach((segmentId) => {
            const base = segmentById.get(segmentId);
            const override = segmentLayoutOverrides.get(segmentId);
            if (!base || !override) {
                return;
            }
            Object.assign(base, override);
        });
        segmentLayoutOverrides.clear();
        layoutDirtySegmentIds.clear();
        isLayoutDirty = false;
        updateLayoutBadge("saved");
    };

    const saveTemplateLayout = async () => {
        if (!layoutSaveUrl) {
            showStatus("URL збереження макету не налаштовано.", "danger");
            return;
        }
        if (isSavingLayout) {
            return;
        }

        const targetIds = layoutDirtySegmentIds.size > 0
            ? [...layoutDirtySegmentIds]
            : (selectedSegmentId ? [selectedSegmentId] : []);

        const fields = targetIds
            .map((segmentId) => buildLayoutFieldPayload(segmentId))
            .filter((item) => item != null);

        if (fields.length === 0) {
            showStatus("Немає змін макету для збереження в шаблон.", "warning");
            return;
        }

        isSavingLayout = true;
        updateLayoutBadge("saving");
        if (saveLayoutButton) {
            saveLayoutButton.disabled = true;
        }

        try {
            const response = await fetch(layoutSaveUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    RequestVerificationToken: getAntiforgeryToken()
                },
                body: JSON.stringify({ fields })
            });
            const body = await response.json().catch(() => ({}));
            if (!response.ok) {
                throw new Error(body.message || `Помилка збереження макету (${response.status}).`);
            }
            commitLayoutOverridesToSegments();
            showStatus(body.message || "Макет шаблону збережено.", "success");
        } catch (error) {
            console.error("[PdfWorkspace Fill] layout save error:", error);
            showStatus(error.message || "Не вдалося зберегти макет.", "danger");
            updateLayoutBadge("dirty");
        } finally {
            isSavingLayout = false;
            if (saveLayoutButton) {
                saveLayoutButton.disabled = false;
            }
        }
    };

    const sortedSegmentsForNav = () =>
        [...segments].sort((a, b) => {
            const pageDiff = (px(a.page, 1) - px(b.page, 1));
            if (pageDiff !== 0) {
                return pageDiff;
            }
            return (a.sequence ?? 0) - (b.sequence ?? 0);
        });

    const formatSegmentLabel = (segment) =>
        segment.title || segment.tag || segment.dataFieldKey || "Поле";

    const buildValuesSignature = (payload) => {
        const list = payload ?? collectFilledValues();
        const normalized = [...list]
            .map((item) => ({
                templateFieldId: String(item.templateFieldId || ""),
                value: String(item.value ?? "")
            }))
            .sort((a, b) => a.templateFieldId.localeCompare(b.templateFieldId));
        return JSON.stringify(normalized);
    };

    const captureSignatureFromDom = () => buildValuesSignature(collectFilledValues());

    const updateSaveBadge = (state) => {
        if (!panelSaveBadge) {
            return;
        }
        panelSaveBadge.classList.remove(
            "pdf-fill-panel__save-badge--saved",
            "pdf-fill-panel__save-badge--dirty",
            "pdf-fill-panel__save-badge--saving"
        );
        if (state === "saving") {
            panelSaveBadge.textContent = "Збереження…";
            panelSaveBadge.classList.add("pdf-fill-panel__save-badge--saving");
        } else if (state === "dirty") {
            panelSaveBadge.textContent = "Є незбережені зміни";
            panelSaveBadge.classList.add("pdf-fill-panel__save-badge--dirty");
        } else {
            panelSaveBadge.textContent = "Збережено";
            panelSaveBadge.classList.add("pdf-fill-panel__save-badge--saved");
        }
    };

    const setDirty = (dirty) => {
        isDirty = dirty;
        updateSaveBadge(dirty ? "dirty" : "saved");
        if (dirty && autoSaveToggle?.checked !== false) {
            scheduleAutoSave();
        }
    };

    const markDirtyFromDom = () => {
        const signature = captureSignatureFromDom();
        setDirty(signature !== lastSavedSignature);
    };

    const scheduleAutoSave = () => {
        if (!hasWritableFields || autoSaveToggle?.checked === false) {
            return;
        }
        if (autoSaveTimer) {
            clearTimeout(autoSaveTimer);
        }
        autoSaveTimer = setTimeout(() => {
            autoSaveTimer = null;
            if (isDirty && !isSaving) {
                saveValues({ silent: true, source: "auto" });
            }
        }, AUTO_SAVE_DELAY_MS);
    };

    const clearPanelSelectionHighlight = () => {
        document.querySelectorAll(".pdf-fill-field-box.is-panel-selected").forEach((box) => {
            box.classList.remove("is-panel-selected");
        });
        panelFieldList?.querySelectorAll(".pdf-fill-panel__field-item.is-active").forEach((item) => {
            item.classList.remove("is-active");
        });
    };

    const getFieldInputBySegmentId = (segmentId) =>
        document.querySelector(`.pdf-field-input[data-segment-id="${segmentId}"]`);

    const updatePanelCharCount = (text) => {
        if (!panelCharCount) {
            return;
        }
        const length = (text || "").length;
        panelCharCount.textContent = length > 0 ? `Символів: ${length}` : "";
    };

    const updateNextFieldButton = () => {
        if (!panelNextField) {
            return;
        }
        const nav = sortedSegmentsForNav().filter((segment) => canWriteSegment(segment));
        if (nav.length < 2 || !selectedSegmentId) {
            panelNextField.disabled = true;
            return;
        }
        const index = nav.findIndex((segment) => String(segment.segmentId) === selectedSegmentId);
        panelNextField.disabled = index < 0 || index >= nav.length - 1;
    };

    const selectField = (segmentId, options = {}) => {
        const { focusPanel = false, focusPdf = true, switchTab = false } = options;
        const segment = segmentById.get(String(segmentId || ""));
        const fieldEl = getFieldInputBySegmentId(segmentId);
        if (!segment || !fieldEl) {
            return;
        }

        selectedSegmentId = String(segmentId);
        focusSegmentId = selectedSegmentId;
        clearPanelSelectionHighlight();

        const box = fieldEl.closest(".pdf-fill-field-box");
        box?.classList.add("is-panel-selected");
        panelFieldList
            ?.querySelector(`[data-fill-segment-id="${CSS.escape(selectedSegmentId)}"]`)
            ?.classList.add("is-active");

        const writable = fieldEl.dataset.canWrite === "true";
        const label = formatSegmentLabel(segment);
        const tag = segment.tag ? ` · ${segment.tag}` : "";

        if (panelEditing) {
            panelEditing.textContent = writable ? `${label}${tag}` : `${label}${tag} (перегляд)`;
        }
        if (panelHint) {
            panelHint.classList.add("is-hidden");
        }

        if (panelValue) {
            panelSyncing = true;
            panelValue.value = readFieldText(fieldEl);
            panelValue.disabled = !writable;
            panelValue.placeholder = writable ? "Введіть текст для цього поля…" : "Лише перегляд";
            panelSyncing = false;
        }
        if (panelReadonlyNote) {
            panelReadonlyNote.classList.toggle("d-none", writable);
        }
        updatePanelCharCount(readFieldText(fieldEl));
        updateNextFieldButton();
        syncLayoutControlsFromSegment(segment);

        if (switchTab) {
            activateFillTab("text");
        }

        if (focusPdf && !focusPanel) {
            fieldEl.focus();
            if (typeof fieldEl.select === "function" && fieldEl instanceof HTMLInputElement) {
                fieldEl.select();
            }
        } else if (focusPanel && panelValue && writable) {
            panelValue.focus();
        }

        if (focusPdf && box && workspaceRoot) {
            const rootRect = workspaceRoot.getBoundingClientRect();
            const boxRect = box.getBoundingClientRect();
            if (boxRect.top < rootRect.top + 24 || boxRect.bottom > rootRect.bottom - 24) {
                workspaceRoot.scrollTop += boxRect.top - rootRect.top - 48;
            }
        }
    };

    const activateFillTab = (tabName) => {
        document.querySelectorAll(".pdf-fill-panel__tab").forEach((tab) => {
            tab.classList.toggle("active", tab.dataset.fillTab === tabName);
        });
        document.querySelectorAll(".pdf-fill-panel__pane").forEach((pane) => {
            pane.classList.toggle("active", pane.dataset.fillPane === tabName);
        });
    };

    const renderFieldList = (filterText = "") => {
        if (!panelFieldList) {
            return;
        }
        const query = (filterText || "").trim().toLowerCase();
        panelFieldList.innerHTML = "";

        sortedSegmentsForNav().forEach((segment) => {
            const segmentId = String(segment.segmentId || "");
            if (!segmentId) {
                return;
            }
            const label = formatSegmentLabel(segment);
            const tag = (segment.tag || "").toLowerCase();
            if (query && !label.toLowerCase().includes(query) && !tag.includes(query)) {
                return;
            }

            const fieldEl = getFieldInputBySegmentId(segmentId);
            const filled = fieldEl ? readFieldText(fieldEl).trim().length > 0 : false;
            const writable = canWriteSegment(segment);

            const button = document.createElement("button");
            button.type = "button";
            button.className = "pdf-fill-panel__field-item"
                + (filled ? " is-filled" : "")
                + (selectedSegmentId === segmentId ? " is-active" : "");
            button.dataset.fillSegmentId = segmentId;
            button.setAttribute("role", "listitem");
            button.innerHTML =
                `<span class="pdf-fill-panel__field-dot" aria-hidden="true"></span>`
                + `${label}`
                + (writable ? "" : " <span class=\"text-muted\">(перегляд)</span>");
            button.addEventListener("click", () => {
                selectField(segmentId, { focusPdf: true, switchTab: true });
            });
            panelFieldList.appendChild(button);
        });

        if (!panelFieldList.children.length) {
            const empty = document.createElement("p");
            empty.className = "small text-muted mb-0 p-2";
            empty.textContent = query ? "Нічого не знайдено." : "Поки немає полів.";
            panelFieldList.appendChild(empty);
        }
    };

    const initFillPanel = () => {
        if (!panelValue) {
            return;
        }

        document.querySelectorAll(".pdf-fill-panel__tab").forEach((tab) => {
            tab.addEventListener("click", () => {
                activateFillTab(tab.dataset.fillTab || "text");
            });
        });

        panelValue.addEventListener("input", () => {
            if (panelSyncing || !selectedSegmentId) {
                return;
            }
            const fieldEl = getFieldInputBySegmentId(selectedSegmentId);
            if (!fieldEl || fieldEl.dataset.canWrite !== "true") {
                return;
            }
            writeFieldText(fieldEl, panelValue.value);
            fieldEl.classList.toggle("is-empty", panelValue.value.length === 0);
            updatePanelCharCount(panelValue.value);
            renderFieldList(panelFieldSearch?.value || "");
            markDirtyFromDom();
        });

        panelFieldSearch?.addEventListener("input", () => {
            renderFieldList(panelFieldSearch.value);
        });

        panelSaveButton?.addEventListener("click", () => saveValues({ source: "panel" }));
        panelNextField?.addEventListener("click", () => {
            const nav = sortedSegmentsForNav().filter((segment) => canWriteSegment(segment));
            const index = nav.findIndex((segment) => String(segment.segmentId) === selectedSegmentId);
            if (index >= 0 && index < nav.length - 1) {
                selectField(nav[index + 1].segmentId, { focusPdf: true, switchTab: true });
            }
        });

        autoSaveToggle?.addEventListener("change", () => {
            if (autoSaveToggle.checked && isDirty) {
                scheduleAutoSave();
            } else if (autoSaveTimer) {
                clearTimeout(autoSaveTimer);
                autoSaveTimer = null;
            }
        });

        renderFieldList();

        const onLayoutControlChange = () => {
            if (layoutPanelSyncing || !selectedSegmentId) {
                return;
            }
            const patch = {
                fontName: fillFontFamily?.value || null,
                fontSize: fillFontSize?.value ? Number.parseFloat(fillFontSize.value) : null,
                lineHeight: fillLineHeight?.value ? Number.parseFloat(fillLineHeight.value) : null,
                textOffsetX: clampLayoutOffset(fillOffsetX?.value ?? 0),
                textOffsetY: clampLayoutOffset(fillOffsetY?.value ?? 0),
                fontBold: fillBold?.getAttribute("aria-pressed") === "true",
                fontItalic: fillItalic?.getAttribute("aria-pressed") === "true",
                textUnderline: fillUnderline?.getAttribute("aria-pressed") === "true"
            };
            applyLayoutPatch(selectedSegmentId, patch);
        };

        fillFontFamily?.addEventListener("change", onLayoutControlChange);
        fillFontSize?.addEventListener("change", onLayoutControlChange);
        fillLineHeight?.addEventListener("change", onLayoutControlChange);
        fillOffsetX?.addEventListener("change", onLayoutControlChange);
        fillOffsetY?.addEventListener("change", onLayoutControlChange);

        fillFontSizeMinus?.addEventListener("click", () => {
            if (!fillFontSize || layoutPanelSyncing) {
                return;
            }
            const current = Number.parseFloat(fillFontSize.value || "12") || 12;
            fillFontSize.value = String(Math.max(6, current - 0.5));
            onLayoutControlChange();
        });
        fillFontSizePlus?.addEventListener("click", () => {
            if (!fillFontSize || layoutPanelSyncing) {
                return;
            }
            const current = Number.parseFloat(fillFontSize.value || "12") || 12;
            fillFontSize.value = String(Math.min(72, current + 0.5));
            onLayoutControlChange();
        });

        [fillBold, fillItalic, fillUnderline].forEach((button) => {
            button?.addEventListener("click", () => {
                if (!button || layoutPanelSyncing || !selectedSegmentId) {
                    return;
                }
                const pressed = button.getAttribute("aria-pressed") === "true";
                button.setAttribute("aria-pressed", pressed ? "false" : "true");
                button.classList.toggle("active", !pressed);
                onLayoutControlChange();
            });
        });

        document.querySelectorAll(".pdf-fill-offset-step").forEach((button) => {
            button.addEventListener("click", () => {
                if (layoutPanelSyncing || !selectedSegmentId) {
                    return;
                }
                const axis = button.dataset.axis;
                const delta = Number.parseFloat(button.dataset.delta || "0");
                const input = axis === "y" ? fillOffsetY : fillOffsetX;
                if (!input) {
                    return;
                }
                input.value = String(clampLayoutOffset(Number.parseFloat(input.value || "0") + delta));
                onLayoutControlChange();
            });
        });

        document.querySelectorAll(".pdf-fill-shift").forEach((button) => {
            button.addEventListener("click", () => {
                if (layoutPanelSyncing || !selectedSegmentId) {
                    return;
                }
                const axis = button.dataset.axis;
                const delta = Number.parseFloat(button.dataset.delta || "0");
                const input = axis === "y" ? fillOffsetY : fillOffsetX;
                if (!input) {
                    return;
                }
                input.value = String(clampLayoutOffset(Number.parseFloat(input.value || "0") + delta));
                onLayoutControlChange();
            });
        });

        document.querySelectorAll(".pdf-fill-h-align").forEach((button) => {
            button.addEventListener("click", () => {
                if (layoutPanelSyncing || !selectedSegmentId) {
                    return;
                }
                applyLayoutPatch(selectedSegmentId, {
                    horizontalAlignment: button.dataset.align || "Left",
                    textAlignment: button.dataset.align || "Left"
                });
            });
        });

        document.querySelectorAll(".pdf-fill-v-align").forEach((button) => {
            button.addEventListener("click", () => {
                if (layoutPanelSyncing || !selectedSegmentId) {
                    return;
                }
                applyLayoutPatch(selectedSegmentId, {
                    verticalAlignment: button.dataset.align || "Top"
                });
            });
        });

        saveLayoutButton?.addEventListener("click", () => saveTemplateLayout());
        updateLayoutBadge("saved");
        setLayoutControlsEnabled(false);
    };

    console.info(
        `[PdfWorkspace Fill] init: segments=${segments.length}, fields=${segmentsByTemplateFieldId.size}, orderId=${currentOrderId || "(new)"}`
    );

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
        const template = clientConfig.finalPdfUrlTemplate || window.__pdfFillFinalPdfUrlTemplate || "";
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

    const readFieldText = (fieldEl) => {
        if (!fieldEl) {
            return "";
        }
        if (fieldEl instanceof HTMLInputElement || fieldEl instanceof HTMLTextAreaElement) {
            return (fieldEl.value || "").replace(/\r/g, "");
        }
        return (fieldEl.innerText || fieldEl.textContent || "").replace(/\r/g, "");
    };

    const writeFieldText = (fieldEl, text) => {
        if (!fieldEl) {
            return;
        }
        const normalized = text ?? "";
        if (fieldEl instanceof HTMLInputElement || fieldEl instanceof HTMLTextAreaElement) {
            fieldEl.value = normalized;
            return;
        }
        fieldEl.textContent = normalized;
    };

    const collectFilledValues = () => {
        const fields = [...document.querySelectorAll(".pdf-fill-field-box .pdf-field-input")];
        fields.sort((a, b) =>
            (Number.parseInt(a.dataset.sequence || "0", 10) || 0)
            - (Number.parseInt(b.dataset.sequence || "0", 10) || 0));

        const byTemplateFieldId = new Map();
        const missingIds = [];

        fields.forEach((fieldEl) => {
            const templateFieldId = (fieldEl.dataset.templateFieldId || "").trim();
            if (!templateFieldId) {
                missingIds.push(fieldEl.dataset.segmentId || "?");
                return;
            }
            if (fieldEl.dataset.canWrite !== "true") {
                return;
            }
            const value = readFieldText(fieldEl);
            if (byTemplateFieldId.has(templateFieldId)) {
                byTemplateFieldId.set(templateFieldId, `${byTemplateFieldId.get(templateFieldId)}\n${value}`);
            } else {
                byTemplateFieldId.set(templateFieldId, value);
            }
        });

        if (missingIds.length > 0) {
            console.warn("[PdfWorkspace Fill] fields without templateFieldId:", missingIds);
        }

        return [...byTemplateFieldId.entries()].map(([templateFieldId, value]) => ({
            templateFieldId,
            value
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

    const applyZoomTransform = () => {
        const percent = getZoomPercent();
        if (zoomContainer) {
            // CSS zoom зберігає hit-area полів; transform: scale() зміщує кліки.
            zoomContainer.style.transform = "";
            zoomContainer.style.transformOrigin = "";
            zoomContainer.style.zoom = `${percent / 100}`;
        }
        if (zoomValue) {
            zoomValue.textContent = `${percent}%`;
        }
    };

    const captureValues = () => {
        const values = new Map();
        document.querySelectorAll(".pdf-fill-field-box .pdf-field-input").forEach((fieldEl) => {
            const segmentId = fieldEl.dataset.segmentId;
            if (segmentId) {
                values.set(segmentId, readFieldText(fieldEl));
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

    const mapTextAlign = (segment) => {
        const raw = (segment.horizontalAlignment || segment.textAlignment || "left").toString().toLowerCase();
        if (raw.includes("center")) {
            return "center";
        }
        if (raw.includes("right")) {
            return "right";
        }
        return "left";
    };

    const mapVerticalAlign = (segment) => {
        const raw = (segment.verticalAlignment || "top").toString().toLowerCase();
        if (raw.includes("middle") || raw.includes("center")) {
            return "center";
        }
        if (raw.includes("bottom")) {
            return "flex-end";
        }
        return "flex-start";
    };

    const createEditableField = (segment, segmentId, templateFieldId, coordScale, writable) => {
        const layout = readLayoutState(segment);
        const multiline = segment.allowMultiline === true;
        const fieldEl = document.createElement(multiline ? "textarea" : "input");
        if (!multiline) {
            fieldEl.type = "text";
        }
        fieldEl.className = multiline
            ? "pdf-field pdf-field-input pdf-field--multiline"
            : "pdf-field pdf-field-input";
        fieldEl.spellcheck = false;
        fieldEl.autocomplete = "off";
        fieldEl.setAttribute("aria-label", segment.title || segment.tag || "Поле PDF");
        fieldEl.dataset.segmentId = segmentId;
        fieldEl.dataset.templateFieldId = templateFieldId;
        fieldEl.dataset.sequence = String(segment.sequence ?? 0);
        fieldEl.dataset.canWrite = writable ? "true" : "false";

        if (!writable) {
            fieldEl.readOnly = true;
            fieldEl.setAttribute("aria-readonly", "true");
            fieldEl.tabIndex = -1;
        }

        const fontSize = num(layout.fontSize) > 0 ? num(layout.fontSize) * coordScale : 12 * coordScale;
        const lineHeight = num(layout.lineHeight) > 0 ? num(layout.lineHeight) : 1.2;
        fieldEl.style.fontSize = `${fontSize}px`;
        fieldEl.style.lineHeight = String(lineHeight);
        fieldEl.style.textAlign = mapTextAlign(layout);
        fieldEl.style.fontWeight = layout.fontBold ? "700" : "400";
        fieldEl.style.fontStyle = layout.fontItalic ? "italic" : "normal";
        fieldEl.style.textDecoration = layout.textUnderline ? "underline" : "none";

        if (layout.fontName) {
            fieldEl.style.fontFamily = `${layout.fontName}, Arial, sans-serif`;
        }

        fieldEl.addEventListener("mousedown", (event) => event.stopPropagation());
        fieldEl.addEventListener("click", (event) => event.stopPropagation());

        if (writable) {
            fieldEl.addEventListener("keydown", (event) => {
                if (!multiline && event.key === "Enter") {
                    event.preventDefault();
                    fieldEl.blur();
                }
            });

            fieldEl.addEventListener("focus", () => {
                focusSegmentId = segmentId;
                fieldEl.closest(".pdf-fill-field-box")?.classList.add("is-focused");
                selectField(segmentId);
            });

            fieldEl.addEventListener("input", () => {
                fieldEl.classList.toggle("is-empty", readFieldText(fieldEl).length === 0);
                if (selectedSegmentId === segmentId && panelValue && !panelSyncing) {
                    panelSyncing = true;
                    panelValue.value = readFieldText(fieldEl);
                    panelSyncing = false;
                    updatePanelCharCount(panelValue.value);
                }
                renderFieldList(panelFieldSearch?.value || "");
                markDirtyFromDom();
            });
        } else {
            fieldEl.addEventListener("focus", () => {
                focusSegmentId = segmentId;
                selectField(segmentId);
            });
            fieldEl.addEventListener("keydown", (event) => event.preventDefault());
            fieldEl.addEventListener("beforeinput", (event) => event.preventDefault());
            fieldEl.addEventListener("paste", (event) => event.preventDefault());
        }

        fieldEl.addEventListener("blur", () => {
            fieldEl.closest(".pdf-fill-field-box")?.classList.remove("is-focused");
            if (focusSegmentId === segmentId) {
                focusSegmentId = null;
            }
        });

        return fieldEl;
    };

    const resolveSegmentBounds = (segment, coordScale) => {
        const effective = getEffectiveSegment(segment);
        const layout = window.PdfOverlayTextLayout;
        if (layout?.getPreviewBounds) {
            return layout.getPreviewBounds(
                num(effective.x),
                num(effective.y),
                num(effective.width, 120),
                num(effective.height, 28),
                num(effective.textOffsetX, 0),
                num(effective.textOffsetY, 0),
                coordScale
            );
        }

        return {
            left: num(effective.x) * coordScale + num(effective.textOffsetX, 0),
            top: num(effective.y) * coordScale + num(effective.textOffsetY, 0) + BASELINE_OFFSET_PX,
            width: num(segment.width, 120) * coordScale,
            height: num(segment.height, 28) * coordScale
        };
    };

    const fitsLayerRect = (rect, layerWidth, layerHeight) =>
        Number.isFinite(rect.left)
        && Number.isFinite(rect.top)
        && rect.width >= 8
        && rect.height >= 8
        && rect.left >= -4
        && rect.top >= -4
        && rect.left + rect.width <= layerWidth + 4
        && rect.top + rect.height <= layerHeight + 4;

    /** Як у конструкторі Map: координати в БД можуть бути в PDF- або preview-просторі. */
    const toViewportRect = (bounds, page) => {
        const rawRect = {
            left: px(bounds.left),
            top: px(bounds.top),
            width: px(bounds.width, 120),
            height: px(bounds.height, 28)
        };

        const pageNumber = px(page, 1);
        const viewport = pageViewports.get(pageNumber) ?? pageViewports.get(1);
        const layer = pageLayers.get(pageNumber) ?? pageLayers.get(1);
        if (!viewport || !layer) {
            return rawRect;
        }

        const metrics = pageMetrics.get(pageNumber) ?? pageMetrics.get(1);
        const layerWidth = metrics?.width || layer.clientWidth || rawRect.width;
        const layerHeight = metrics?.height || layer.clientHeight || rawRect.height;

        if (fitsLayerRect(rawRect, layerWidth, layerHeight)) {
            return rawRect;
        }

        const [vx1, vy1] = viewport.convertToViewportPoint(rawRect.left, rawRect.top);
        const [vx2, vy2] = viewport.convertToViewportPoint(
            rawRect.left + rawRect.width,
            rawRect.top + rawRect.height
        );
        const converted = {
            left: Math.min(vx1, vx2),
            top: Math.min(vy1, vy2),
            width: Math.abs(vx2 - vx1),
            height: Math.abs(vy2 - vy1)
        };

        return fitsLayerRect(converted, layerWidth, layerHeight) ? converted : rawRect;
    };

    const renderPages = async () => {
        pagesHost.innerHTML = "";
        pageLayers.clear();
        pageMetrics.clear();
        pageViewports.clear();

        const viewportScale = PREVIEW_SCALE;
        const deviceScale = getOutputScale();

        for (let pageNumber = 1; pageNumber <= pdfDoc.numPages; pageNumber++) {
            const page = await pdfDoc.getPage(pageNumber);
            const viewport = page.getViewport({ scale: viewportScale });
            pageViewports.set(pageNumber, viewport);
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
            layer.className = "pdf-fill-overlay-layer position-absolute top-0 start-0";
            layer.style.position = "absolute";
            layer.style.top = "0";
            layer.style.left = "0";
            layer.style.width = `${cssWidth}px`;
            layer.style.height = `${cssHeight}px`;
            layer.style.zIndex = "10";
            layer.dataset.page = String(pageNumber);
            wrapper.appendChild(layer);
            pageLayers.set(pageNumber, layer);
            pageMetrics.set(pageNumber, { width: cssWidth, height: cssHeight });

            pagesHost.appendChild(wrapper);

            const ctx = canvas.getContext("2d", { alpha: false });
            if (ctx) {
                ctx.imageSmoothingEnabled = true;
                ctx.imageSmoothingQuality = "high";
                const transform = deviceScale !== 1 ? [deviceScale, 0, 0, deviceScale, 0, 0] : null;
                await page.render({ canvasContext: ctx, viewport, transform, intent: "display" }).promise;
            }
        }
    };

    const renderOverlayFields = (values, restoreFocusSegmentId = null) => {
        const coordScale = 1;

        segments.forEach((baseSegment) => {
            const segment = getEffectiveSegment(baseSegment);
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

            const bounds = resolveSegmentBounds(segment, coordScale);
            const viewportRect = toViewportRect(bounds, page);
            const width = Math.max(24, px(viewportRect.width, 120));
            const height = Math.max(14, px(viewportRect.height, 28));
            const metrics = pageMetrics.get(page) ?? pageMetrics.get(1);
            const layerWidth = metrics?.width || layer.clientWidth || width;
            const layerHeight = metrics?.height || layer.clientHeight || height;
            const left = Math.min(px(viewportRect.left), Math.max(0, layerWidth - width));
            const top = Math.min(px(viewportRect.top), Math.max(0, layerHeight - height));

            const writable = canWriteSegment(baseSegment);

            const box = document.createElement("div");
            box.className = writable ? "pdf-fill-field-box" : "pdf-fill-field-box is-readonly";
            box.style.left = `${left}px`;
            box.style.top = `${top}px`;
            box.style.width = `${width}px`;
            box.style.height = `${height}px`;
            box.style.alignItems = mapVerticalAlign(segment);
            box.style.zIndex = "20";
            box.title = segment.title || segment.tag || "";

            const fieldEl = createEditableField(segment, segmentId, templateFieldId, coordScale, writable);
            const initialValue = values.has(segmentId)
                ? values.get(segmentId)
                : resolveSavedValueForSegment(segment);
            writeFieldText(fieldEl, initialValue);
            fieldEl.classList.toggle("is-empty", initialValue.length === 0);

            if (writable) {
                box.addEventListener("mousedown", (event) => {
                    if (!fieldEl.contains(event.target)) {
                        event.preventDefault();
                        fieldEl.focus();
                        if (typeof fieldEl.select === "function") {
                            fieldEl.select();
                        }
                    }
                });
            }

            box.appendChild(fieldEl);
            layer.appendChild(box);
        });

        if (restoreFocusSegmentId) {
            const fieldEl = document.querySelector(
                `.pdf-field-input[data-segment-id="${restoreFocusSegmentId}"]`
            );
            if (fieldEl) {
                fieldEl.focus();
                if (typeof fieldEl.select === "function") {
                    fieldEl.select();
                }
            }
        }
    };

    const mountOverlays = (preservedValues = null, restoreFocus = null) => {
        pageLayers.forEach((layer) => {
            layer.querySelectorAll(".pdf-fill-field-box").forEach((box) => box.remove());
        });

        const values = preservedValues ?? captureValues();
        if (!values.size) {
            buildInitialValues().forEach((v, k) => values.set(k, v));
        }

        renderOverlayFields(values, restoreFocus ?? focusSegmentId ?? selectedSegmentId);
        if (selectedSegmentId) {
            selectField(selectedSegmentId, { focusPdf: false });
            renderFieldList(panelFieldSearch?.value || "");
        }
    };

    const saveValues = async (options = {}) => {
        const { silent = false, source = "manual" } = options;
        const saveUrl = clientConfig.saveUrl || window.__pdfFillSaveUrl;
        if (!saveUrl) {
            if (!silent) {
                showStatus("URL збереження не налаштовано.", "danger");
            }
            return;
        }
        if (isSaving) {
            return;
        }

        const payload = collectFilledValues();
        const nonEmpty = payload.filter((item) => item.value.trim().length > 0).length;

        if (payload.length === 0) {
            if (!silent) {
                showStatus("Немає полів для збереження. Перевірте, чи шаблон має розміщені сегменти.", "danger");
            }
            console.error("[PdfWorkspace Fill] save aborted: empty payload");
            return;
        }

        if (!isDirty && lastSavedSignature && source === "auto") {
            return;
        }

        isSaving = true;
        updateSaveBadge("saving");
        if (saveButton) {
            saveButton.disabled = true;
        }
        if (panelSaveButton) {
            panelSaveButton.disabled = true;
        }

        const requestBody = { orderId: currentOrderId, values: payload };
        console.info("[PdfWorkspace Fill] save payload:", {
            orderId: currentOrderId,
            fields: payload.length,
            nonEmpty,
            values: payload
        });

        try {
            const response = await fetch(saveUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    RequestVerificationToken: getAntiforgeryToken()
                },
                body: JSON.stringify(requestBody)
            });

            const body = await response.json().catch(() => ({}));
            console.info("[PdfWorkspace Fill] save response:", response.status, body);

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
            const skippedEmpty = body.skippedEmpty ?? body.SkippedEmpty ?? 0;
            const failedFields = body.failedFields ?? body.FailedFields ?? [];
            const failedText = formatFailedFields(failedFields);

            let message = body.message || `Прийнято: ${received}, збережено: ${saved}.`;
            if (skippedEmpty > 0) {
                message += ` Порожніх: ${skippedEmpty}.`;
            }
            if (skippedUnmapped > 0) {
                message += ` Пропущено (без мапінгу): ${skippedUnmapped}.`;
            }
            if (failedText) {
                message += ` Не збережено: ${failedText}.`;
            }

            const hasFailures = failedFields.length > 0 || skippedUnmapped > 0;
            const ok = !hasFailures && (saved > 0 || (nonEmpty === 0 && received > 0));

            if (!silent || !ok || hasFailures) {
                showStatus(message, ok ? "success" : (saved > 0 ? "warning" : "danger"));
            } else if (silent && ok) {
                statusBox?.classList.add("d-none");
            }

            if (ok && !hasFailures) {
                lastSavedSignature = buildValuesSignature(payload);
                isDirty = false;
                updateSaveBadge("saved");
            } else if (!silent) {
                updateSaveBadge(isDirty ? "dirty" : "saved");
            }

            if (currentOrderId) {
                updateFinalPdfActions(currentOrderId);
                const url = new URL(window.location.href);
                url.searchParams.set("orderId", currentOrderId);
                window.history.replaceState(null, "", url.toString());
            }
        } catch (error) {
            console.error("[PdfWorkspace Fill] save error:", error);
            if (!silent) {
                showStatus(error.message || "Не вдалося зберегти значення.", "danger");
            }
            updateSaveBadge("dirty");
        } finally {
            isSaving = false;
            if (saveButton && hasWritableFields) {
                saveButton.disabled = false;
            }
            if (panelSaveButton && hasWritableFields) {
                panelSaveButton.disabled = false;
            }
        }
    };

    const scrollWorkspaceToFirstField = () => {
        const targetPage = document.querySelector(`.pdf-fill-page[data-page="${firstFieldPage}"]`)
            ?? document.querySelector(".pdf-fill-page");
        if (!workspaceRoot || !targetPage) {
            return;
        }

        const rootRect = workspaceRoot.getBoundingClientRect();
        const pageRect = targetPage.getBoundingClientRect();
        workspaceRoot.scrollTop += pageRect.top - rootRect.top - 8;
    };

    const resolvePdfJsWorkerSrc = () => {
        if (clientConfig.pdfJsWorkerSrc) {
            return clientConfig.pdfJsWorkerSrc;
        }

        if (window.__pdfJsWorkerSrc) {
            return window.__pdfJsWorkerSrc;
        }

        const script = document.querySelector('script[src*="pdf.min.js"]');
        if (!script?.src) {
            return null;
        }

        return script.src.replace("pdf.min.js", "pdf.worker.min.js");
    };

    const init = async () => {
        if (expectedSegmentCount > 0 && segments.length === 0) {
            showPreviewError(
                "Не вдалося завантажити дані полів (зламаний JSON на сторінці). "
                + "Перезавантажте Ctrl+F5 або відкрийте заповнення зі списку версій."
            );
            console.error(
                "[PdfWorkspace Fill] segments missing: expected=%s, loaded=%s",
                expectedSegmentCount,
                segments.length
            );
            return;
        }

        if (!pdfUrl) {
            showPreviewError("Не задано URL PDF-файлу. Відкрийте сторінку знову з реєстру замовлення.");
            return;
        }

        if (!window.pdfjsLib) {
            showPreviewError(
                "Бібліотека PDF.js не завантажилась. Перезавантажте сторінку або зверніться до адміністратора."
            );
            return;
        }

        const workerSrc = resolvePdfJsWorkerSrc();
        if (!workerSrc) {
            showPreviewError("Не знайдено worker PDF.js (pdf.worker.min.js).");
            return;
        }

        window.pdfjsLib.GlobalWorkerOptions.workerSrc = workerSrc;

        const documentUrl = pdfUrl.startsWith("http") ? pdfUrl : new URL(pdfUrl, window.location.origin).href;
        pdfDoc = await window.pdfjsLib.getDocument({ url: documentUrl, withCredentials: true }).promise;
        await renderPages();
        requestAnimationFrame(() => {
            mountOverlays(buildInitialValues());
            applyZoomTransform();
            updateFinalPdfActions(currentOrderId);
            scrollWorkspaceToFirstField();
            initFillPanel();
            lastSavedSignature = captureSignatureFromDom();
            isDirty = false;
            updateSaveBadge("saved");

            const nav = sortedSegmentsForNav();
            const firstTarget =
                nav.find((segment) => canWriteSegment(segment)) ?? nav[0];
            if (firstTarget?.segmentId) {
                selectField(firstTarget.segmentId, { focusPdf: false });
            }

            const mounted = document.querySelectorAll(".pdf-fill-field-box").length;
            if (segments.length > 0 && mounted === 0) {
                showPreviewError(
                    "Поля шаблону не відобразились на PDF. Спробуйте оновити сторінку (Ctrl+F5)."
                );
                console.error("[PdfWorkspace Fill] overlay mount failed: segments=%s, boxes=%s", segments.length, mounted);
            }
        });

        zoomSlider?.addEventListener("input", () => {
            applyZoomTransform();
            requestAnimationFrame(() => mountOverlays(captureValues(), selectedSegmentId));
        });

        saveButton?.addEventListener("click", () => saveValues({ source: "toolbar" }));

        document.addEventListener("keydown", (event) => {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
                event.preventDefault();
                saveValues({ source: "shortcut" });
            }
        });

        window.addEventListener("beforeunload", (event) => {
            if (isDirty || isLayoutDirty) {
                event.preventDefault();
                event.returnValue = "";
            }
        });
    };

    init().catch((error) => {
        console.error("[PdfWorkspace Fill] init error:", error);
        const message = error?.message?.includes("404") || error?.name === "MissingPDFException"
            ? "PDF-файл не знайдено у сховищі. Перевірте, що оригінал шаблону завантажено (кнопка «Оригінал PDF»)."
            : "Помилка завантаження PDF. Спробуйте «Оригінал PDF» у новій вкладці.";
        showPreviewError(message);
    });
})();
