(function () {
    const templateOptions = window.__orderAppendTemplateOptions || [];
    const investigationTypes = window.__orderAppendInvestigationTypes || [];
    const branches = window.__orderAppendBranches || [];
    const initialSamples = window.__orderAppendInitialSamples || [];
    const defaultBranchId = window.__orderAppendDefaultBranchId || "";
    const panelOpenInitially = window.__orderAppendPanelOpen === true;

    const templateCatalog = window.OrderTemplateField.createCatalog(templateOptions, investigationTypes);

    const appendRoot = document.getElementById("orderDetailAppend");
    const appendToggle = document.getElementById("orderDetailAppendToggle");
    const appendBody = document.getElementById("orderDetailAppendBody");
    const samplesList = document.getElementById("orderAppendSamplesList");
    const samplesPlaceholder = document.getElementById("orderAppendSamplesPlaceholder");
    const btnAddSample = document.getElementById("btnAddAppendSample");
    const appendForm = document.getElementById("appendSamplesForm");
    const submitButton = document.getElementById("btnSubmitAppendSamples");

    if (!appendRoot || !samplesList) {
        return;
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function branchOptionsHtml(selectedId) {
        return branches
            .map((branch) => {
                const selected = branch.id === selectedId ? " selected" : "";
                return `<option value="${branch.id}"${selected}>${escapeHtml(branch.name)}</option>`;
            })
            .join("");
    }

    function getSampleRows() {
        return Array.from(samplesList.querySelectorAll(".order-sample-row"));
    }

    function createSampleSeed(sample) {
        const selectedTemplateVersionId =
            sample?.selectedTemplateVersionIds?.[0]
            || sample?.templateVersionId
            || "";
        const selectedOption = templateCatalog.getOptionByTemplateVersionId(selectedTemplateVersionId);
        const fallbackOption = templateOptions[0] || null;
        const option = selectedOption || fallbackOption || null;

        return {
            templateVersionId: selectedTemplateVersionId || option?.templateVersionId || "",
            branchId: sample?.documentTargetBranchIds?.[0] || sample?.branchId || defaultBranchId || branches[0]?.id || ""
        };
    }

    function addSampleRow(sample) {
        const seed = createSampleSeed(sample);
        const row = document.createElement("div");
        row.className = "row g-2 align-items-end border-top py-2 order-sample-row order-detail-append__row";
        row.innerHTML = `
            <div class="col-md-6">
                <label class="form-label small text-muted d-md-none">Бланк для проби</label>
                <div class="order-sample-template-cell"></div>
            </div>
            <div class="col-md-5">
                <label class="form-label small text-muted d-md-none">Філія</label>
                <select class="form-select form-select-sm sample-branch-select">
                    ${branchOptionsHtml(seed.branchId)}
                </select>
            </div>
            <div class="col-md-1 text-md-end">
                <button type="button" class="btn btn-sm btn-outline-danger sample-remove-button" title="Прибрати рядок">×</button>
            </div>`;

        const cell = row.querySelector(".order-sample-template-cell");
        cell.innerHTML = window.OrderTemplateField.renderPickerHtml(templateCatalog, seed.templateVersionId, {
            showPreview: false
        });

        const fieldRoot = cell.querySelector(".order-template-field");
        row.templatePicker = window.OrderTemplateField.bindPicker(fieldRoot, templateCatalog, {
            onChange: syncSampleRows
        });

        samplesList.appendChild(row);
        bindSampleRow(row);
        syncSampleRows();
    }

    function bindSampleRow(row) {
        const removeButton = row.querySelector(".sample-remove-button");

        row.querySelector(".sample-branch-select")?.addEventListener("change", syncSampleRows);
        removeButton?.addEventListener("click", () => {
            window.OrderTemplateField.closeOpenMenu();
            row.remove();
            syncSampleRows();
        });
    }

    function syncSampleRows() {
        const rows = getSampleRows();
        samplesPlaceholder?.classList.toggle("d-none", rows.length > 0);

        rows.forEach((row, index) => {
            const branchSelect = row.querySelector(".sample-branch-select");
            const option = row.templatePicker?.getSelectedOption();
            const investigationTypeId = option?.investigationTypeId || "";
            const templateVersionId = option?.templateVersionId || "";

            setHidden(row, "investigation", `AppendSamples.Samples[${index}].InvestigationTypeId`, investigationTypeId);
            setHidden(row, "template", `AppendSamples.Samples[${index}].TemplateVersionId`, templateVersionId);
            setHidden(row, "document", `AppendSamples.Samples[${index}].SelectedTemplateVersionIds[0]`, templateVersionId);
            setHidden(row, "branch", `AppendSamples.Samples[${index}].DocumentTargetBranchIds[0]`, branchSelect?.value || defaultBranchId);
        });
    }

    function setHidden(row, key, name, value) {
        let input = row.querySelector(`input[data-sample-hidden="${key}"]`);
        if (!input) {
            input = document.createElement("input");
            input.type = "hidden";
            input.dataset.sampleHidden = key;
            row.appendChild(input);
        }

        input.name = name;
        input.value = value || "";
    }

    function setPanelOpen(isOpen) {
        appendRoot.classList.toggle("order-detail-append--open", isOpen);
        appendToggle?.setAttribute("aria-expanded", isOpen ? "true" : "false");
        if (appendBody) {
            if (isOpen) {
                appendBody.removeAttribute("hidden");
            } else {
                appendBody.setAttribute("hidden", "hidden");
            }
        }
    }

    function openPanelAndAddRow(seed) {
        setPanelOpen(true);
        addSampleRow(seed);
        appendBody?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    appendToggle?.addEventListener("click", () => {
        const isOpen = appendRoot.classList.contains("order-detail-append--open");
        setPanelOpen(!isOpen);
        if (!isOpen && getSampleRows().length === 0) {
            addSampleRow(null);
        }
    });

    btnAddSample?.addEventListener("click", () => {
        setPanelOpen(true);
        addSampleRow(null);
    });

    document.querySelectorAll(".order-detail-sample__clone").forEach((button) => {
        button.addEventListener("click", () => {
            openPanelAndAddRow({
                investigationTypeId: button.dataset.investigationTypeId,
                templateVersionId: button.dataset.templateVersionId,
                documentTargetBranchIds: [button.dataset.branchId]
            });
        });
    });

    appendForm?.addEventListener("submit", (event) => {
        syncSampleRows();
        const rows = getSampleRows();
        if (rows.length === 0) {
            event.preventDefault();
            window.alert("Додайте хоча б одну пробу з бланком PDF.");
            setPanelOpen(true);
            return;
        }

        const hasBlank = rows.some((row) => !row.templatePicker?.getValue());

        if (hasBlank) {
            event.preventDefault();
            window.alert("У кожному рядку оберіть бланк для проби.");
            return;
        }

        if (submitButton) {
            submitButton.disabled = true;
        }
    });

    if (initialSamples.length > 0) {
        for (const sample of initialSamples) {
            addSampleRow(sample);
        }
        setPanelOpen(true);
    } else if (panelOpenInitially) {
        setPanelOpen(true);
        if (getSampleRows().length === 0) {
            addSampleRow(null);
        }
    } else {
        setPanelOpen(false);
    }
})();
