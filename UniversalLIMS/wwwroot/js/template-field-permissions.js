(() => {
    const root = document.getElementById("templatePermissionsRoot");
    const matrixBody = document.getElementById("templatePermissionsMatrixBody");
    const prevFieldButton = document.getElementById("templatePermissionsPrevField");
    const nextFieldButton = document.getElementById("templatePermissionsNextField");
    const activeTagLabel = document.getElementById("templatePermissionsActiveTag");
    const bulkRoleSelect = document.getElementById("templatePermissionsBulkRole");

    if (!root) {
        return;
    }

    let selectedFieldId = null;
    let permissionsPreview = null;

    const normalizeGuid = (value) => String(value ?? "").trim().toLowerCase();

    const getRows = () => Array.from(matrixBody?.querySelectorAll(".template-permissions-row") ?? []);

    const updateActiveTagLabel = (row) => {
        if (!activeTagLabel) {
            return;
        }

        if (!row) {
            activeTagLabel.textContent = "";
            return;
        }

        const title = row.querySelector(".template-permissions-field-title")?.textContent?.trim() || "—";
        const tag = row.dataset.fieldTag || "";
        activeTagLabel.textContent = tag ? `${title} · ${tag}` : title;
    };

    const setSelectedField = (fieldId, options = {}) => {
        const { scrollRow = true, scrollPreview = scrollRow } = options;
        const normalized = normalizeGuid(fieldId);
        selectedFieldId = normalized || null;

        getRows().forEach((row) => {
            const isActive = normalizeGuid(row.dataset.fieldId) === normalized;
            row.classList.toggle("is-active", isActive);
            if (isActive && scrollRow) {
                row.scrollIntoView({ block: "nearest", behavior: "smooth" });
                updateActiveTagLabel(row);
            }
        });

        permissionsPreview?.selectField(normalized, { scrollPreview });
    };

    const navigateField = (direction) => {
        const rows = getRows();
        if (rows.length === 0) {
            return;
        }

        const currentIndex = rows.findIndex((row) => normalizeGuid(row.dataset.fieldId) === selectedFieldId);
        const nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + direction + rows.length) % rows.length;
        const nextRow = rows[nextIndex];
        if (!nextRow?.dataset.fieldId) {
            return;
        }

        setSelectedField(nextRow.dataset.fieldId);
    };

    const bindMatrixRows = () => {
        getRows().forEach((row) => {
            row.addEventListener("click", (event) => {
                if (event.target.closest("select, option, button, a, input, label")) {
                    return;
                }

                setSelectedField(row.dataset.fieldId);
            });
        });
    };

    const bindPreviewControls = () => {
        prevFieldButton?.addEventListener("click", () => navigateField(-1));
        nextFieldButton?.addEventListener("click", () => navigateField(1));
    };

    const bindBulkActions = () => {
        root.querySelectorAll("[data-bulk-level]").forEach((button) => {
            button.addEventListener("click", () => {
                const level = button.dataset.bulkLevel;
                const scope = button.dataset.bulkScope || "role";
                if (!level) {
                    return;
                }

                const selects = scope === "all"
                    ? root.querySelectorAll("select.template-permissions-access-select")
                    : root.querySelectorAll(`select.template-permissions-access-select[data-role="${bulkRoleSelect?.value ?? ""}"]`);

                selects.forEach((select) => {
                    if (select.disabled) {
                        return;
                    }

                    select.value = level;
                });
            });
        });
    };

    bindMatrixRows();
    bindPreviewControls();
    bindBulkActions();

    const previewRoot = document.getElementById("templatePermissionsPreviewRoot");
    const pagesHost = document.getElementById("templatePermissionsPagesHost");
    const versionId = root.dataset.templateVersionId;
    const pdfUrl = root.dataset.pdfPreviewUrl;
    const hasMapPreview = root.dataset.hasMapPreview === "true";

    if (hasMapPreview && previewRoot && pagesHost && versionId && pdfUrl && typeof window.initTemplatePermissionsPreview === "function") {
        window.initTemplatePermissionsPreview({
            scrollRoot: previewRoot,
            pagesHost,
            versionId,
            pdfUrl,
            zoomSlider: document.getElementById("templatePermissionsZoomSlider"),
            zoomValue: document.getElementById("templatePermissionsZoomValue"),
            onFieldSelect: (fieldId) => setSelectedField(fieldId, { scrollRow: true, scrollPreview: false })
        }).then((preview) => {
            permissionsPreview = preview;
            if (selectedFieldId) {
                permissionsPreview?.selectField(selectedFieldId, { scrollPreview: false });
            } else {
                const firstRow = getRows()[0];
                if (firstRow?.dataset.fieldId) {
                    setSelectedField(firstRow.dataset.fieldId, { scrollRow: false });
                }
            }
        });
    } else {
        const firstRow = getRows()[0];
        if (firstRow?.dataset.fieldId) {
            setSelectedField(firstRow.dataset.fieldId, { scrollRow: false });
        }
    }
})();
