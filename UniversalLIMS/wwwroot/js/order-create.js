(function () {
    const templateOptions = window.__orderCreateTemplateOptions || [];
    const branches = window.__orderCreateBranches || [];
    const initialSamples = window.__orderCreateInitialSamples || [];
    const defaultBranchId = window.__orderCreateDefaultBranchId || "";

    const modeExisting = document.getElementById("customerModeExisting");
    const modeNew = document.getElementById("customerModeNew");
    const existingPanel = document.getElementById("existingCustomerPanel");
    const newPanel = document.getElementById("newCustomerPanel");
    const searchInput = document.getElementById("customerSearch");
    const resultsBox = document.getElementById("customerSearchResults");
    const selectedIdInput = document.getElementById("selectedCustomerId");
    const selectedLabelInput = document.getElementById("selectedCustomerLabel");
    const selectedSummary = document.getElementById("selectedCustomerSummary");
    const samplesList = document.getElementById("orderSamplesList");
    const samplesPlaceholder = document.getElementById("orderSamplesPlaceholder");
    const btnAddSample = document.getElementById("btnAddOrderSample");
    const legacyInputs = document.getElementById("legacyOrderCreateInputs");
    const form = document.getElementById("orderCreateForm");
    const btnPrepareMapping = document.getElementById("btnPrepareFieldMapping");
    const fieldMappingHint = document.getElementById("fieldMappingHint");

    let searchTimer = null;

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function optionLabel(option) {
        const version = option.versionNumber ? ` (v${option.versionNumber})` : "";
        const marker = option.isDefault ? " *" : "";
        return `${option.templateNameUk}${version}${marker}`;
    }

    function getOptionByTemplateVersionId(templateVersionId) {
        return templateOptions.find((option) => option.templateVersionId === templateVersionId) || null;
    }

    function getOptionsForInvestigation(investigationTypeId) {
        return templateOptions.filter((option) => option.investigationTypeId === investigationTypeId);
    }

    function activeTemplateOptionsHtml(selectedTemplateVersionId) {
        const emptySelected = selectedTemplateVersionId ? "" : " selected";
        const options = [`<option value=""${emptySelected}>— оберіть —</option>`];
        for (const option of templateOptions) {
            const selected = option.templateVersionId === selectedTemplateVersionId ? " selected" : "";
            options.push(
                `<option value="${option.templateVersionId}" data-investigation-type-id="${option.investigationTypeId}"${selected}>${escapeHtml(optionLabel(option))}</option>`
            );
        }

        return options.join("");
    }

    function pdfOptionsHtml(investigationTypeId, selectedTemplateVersionId) {
        const optionsForType = getOptionsForInvestigation(investigationTypeId);
        const emptySelected = selectedTemplateVersionId ? "" : " selected";
        const options = [`<option value=""${emptySelected}>— оберіть —</option>`];
        for (const option of optionsForType) {
            const selected = option.templateVersionId === selectedTemplateVersionId ? " selected" : "";
            options.push(
                `<option value="${option.templateVersionId}"${selected}>${escapeHtml(optionLabel(option))}</option>`
            );
        }

        return options.join("");
    }

    function syncFieldMappingActions() {
        const selectedCount = getSampleRows()
            .filter((row) => row.querySelector(".sample-pdf-select")?.value)
            .length;
        const multi = selectedCount >= 2;
        btnPrepareMapping?.classList.toggle("d-none", !multi);
        fieldMappingHint?.classList.toggle("d-none", !multi);
    }

    function syncCustomerMode() {
        const isNew = modeNew?.checked;
        existingPanel?.classList.toggle("d-none", isNew);
        newPanel?.classList.toggle("d-none", !isNew);
    }

    function renderSelectedCustomer() {
        const label = selectedLabelInput?.value?.trim();
        if (!selectedSummary) {
            return;
        }
        selectedSummary.textContent = label ? `Обрано: ${label}` : "";
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
        return Array.from(samplesList?.querySelectorAll(".order-sample-row") || []);
    }

    function createSampleSeed(sample) {
        const selectedTemplateVersionId =
            sample?.selectedTemplateVersionIds?.[0]
            || sample?.templateVersionId
            || "";
        const selectedOption = getOptionByTemplateVersionId(selectedTemplateVersionId);
        const fallbackOption = sample?.investigationTypeId
            ? getOptionsForInvestigation(sample.investigationTypeId)[0]
            : templateOptions[0];
        const option = selectedOption || fallbackOption || null;

        return {
            investigationTypeId: sample?.investigationTypeId || option?.investigationTypeId || "",
            templateVersionId: selectedTemplateVersionId || option?.templateVersionId || "",
            branchId: sample?.documentTargetBranchIds?.[0] || defaultBranchId || branches[0]?.id || ""
        };
    }

    function addSampleRow(sample) {
        if (!samplesList) {
            return;
        }

        const seed = createSampleSeed(sample);
        const row = document.createElement("div");
        row.className = "row g-2 align-items-end border-top py-2 order-sample-row";
        row.innerHTML = `
            <div class="col-md-4">
                <label class="form-label small text-muted d-md-none">Активний шаблон / тип дослідження</label>
                <select class="form-select form-select-sm sample-active-template-select">
                    ${activeTemplateOptionsHtml(seed.templateVersionId)}
                </select>
            </div>
            <div class="col-md-4">
                <label class="form-label small text-muted d-md-none">PDF-бланк</label>
                <select class="form-select form-select-sm sample-pdf-select">
                    ${pdfOptionsHtml(seed.investigationTypeId, seed.templateVersionId)}
                </select>
            </div>
            <div class="col-md-3">
                <label class="form-label small text-muted d-md-none">Лабораторія-виконавець</label>
                <select class="form-select form-select-sm sample-branch-select">
                    ${branchOptionsHtml(seed.branchId)}
                </select>
            </div>
            <div class="col-md-1 text-md-end">
                <button type="button" class="btn btn-sm btn-outline-danger sample-remove-button" title="Видалити рядок">×</button>
            </div>`;

        samplesList.appendChild(row);
        bindSampleRow(row);
        syncSampleRows();
    }

    function bindSampleRow(row) {
        const activeSelect = row.querySelector(".sample-active-template-select");
        const pdfSelect = row.querySelector(".sample-pdf-select");
        const removeButton = row.querySelector(".sample-remove-button");

        activeSelect?.addEventListener("change", () => {
            const selectedOption = getOptionByTemplateVersionId(activeSelect.value);
            const investigationTypeId = selectedOption?.investigationTypeId || "";
            pdfSelect.innerHTML = pdfOptionsHtml(investigationTypeId, selectedOption?.templateVersionId || "");
            syncSampleRows();
        });

        pdfSelect?.addEventListener("change", syncSampleRows);
        row.querySelector(".sample-branch-select")?.addEventListener("change", syncSampleRows);
        removeButton?.addEventListener("click", () => {
            row.remove();
            syncSampleRows();
        });
    }

    function syncSampleRows() {
        const rows = getSampleRows();
        samplesPlaceholder?.classList.toggle("d-none", rows.length > 0);

        rows.forEach((row, index) => {
            const activeSelect = row.querySelector(".sample-active-template-select");
            const pdfSelect = row.querySelector(".sample-pdf-select");
            const branchSelect = row.querySelector(".sample-branch-select");
            const activeOption = getOptionByTemplateVersionId(activeSelect?.value);
            const pdfOption = getOptionByTemplateVersionId(pdfSelect?.value);
            const investigationTypeId = activeOption?.investigationTypeId || pdfOption?.investigationTypeId || "";
            const templateVersionId = pdfOption?.templateVersionId || activeOption?.templateVersionId || "";

            setHidden(row, "investigation", `Input.Samples[${index}].InvestigationTypeId`, investigationTypeId);
            setHidden(row, "template", `Input.Samples[${index}].TemplateVersionId`, templateVersionId);
            setHidden(row, "document", `Input.Samples[${index}].SelectedTemplateVersionIds[0]`, templateVersionId);
            setHidden(row, "branch", `Input.Samples[${index}].DocumentTargetBranchIds[0]`, branchSelect?.value || defaultBranchId);
        });

        syncLegacyInputs(rows);
        syncFieldMappingActions();
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

    function syncLegacyInputs(rows) {
        if (!legacyInputs) {
            return;
        }

        legacyInputs.innerHTML = "";
        const samples = rows
            .map((row) => {
                const activeOption = getOptionByTemplateVersionId(row.querySelector(".sample-active-template-select")?.value);
                const pdfOption = getOptionByTemplateVersionId(row.querySelector(".sample-pdf-select")?.value);
                return {
                    investigationTypeId: activeOption?.investigationTypeId || pdfOption?.investigationTypeId || "",
                    templateVersionId: pdfOption?.templateVersionId || activeOption?.templateVersionId || "",
                    branchId: row.querySelector(".sample-branch-select")?.value || defaultBranchId
                };
            })
            .filter((sample) => sample.investigationTypeId || sample.templateVersionId);

        const first = samples[0];
        if (first) {
            appendLegacyHidden("Input.InvestigationTypeId", first.investigationTypeId);
            appendLegacyHidden("Input.TemplateVersionId", first.templateVersionId);
        }

        samples.forEach((sample, index) => {
            appendLegacyHidden(`Input.SelectedTemplateVersionIds[${index}]`, sample.templateVersionId);
            appendLegacyHidden(`Input.DocumentTargetBranchIds[${index}]`, sample.branchId);
        });
    }

    function appendLegacyHidden(name, value) {
        const input = document.createElement("input");
        input.type = "hidden";
        input.name = name;
        input.value = value || "";
        legacyInputs.appendChild(input);
    }

    async function searchCustomers(query) {
        const url = `/api/customers/search?q=${encodeURIComponent(query)}&take=15`;
        const response = await fetch(url, { headers: { Accept: "application/json" } });
        if (!response.ok) {
            return [];
        }
        return response.json();
    }

    function renderSearchResults(items) {
        if (!resultsBox) {
            return;
        }
        resultsBox.innerHTML = "";
        if (!items.length) {
            resultsBox.classList.add("d-none");
            return;
        }

        for (const item of items) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "list-group-item list-group-item-action py-2 small";
            const org = item.organizationName ? ` · ${item.organizationName}` : "";
            const phone = item.contactPhone ? ` · ${item.contactPhone}` : "";
            button.textContent = `${item.fullName}${org}${phone}`;
            button.addEventListener("click", () => {
                selectedIdInput.value = item.id;
                selectedLabelInput.value = button.textContent;
                searchInput.value = item.fullName;
                resultsBox.classList.add("d-none");
                renderSelectedCustomer();
            });
            resultsBox.appendChild(button);
        }

        resultsBox.classList.remove("d-none");
    }

    form?.addEventListener("submit", () => {
        syncSampleRows();
    });

    modeExisting?.addEventListener("change", syncCustomerMode);
    modeNew?.addEventListener("change", syncCustomerMode);
    btnAddSample?.addEventListener("click", () => addSampleRow());

    searchInput?.addEventListener("input", () => {
        clearTimeout(searchTimer);
        const query = searchInput.value.trim();
        if (query.length < 2) {
            resultsBox?.classList.add("d-none");
            return;
        }

        searchTimer = setTimeout(async () => {
            const items = await searchCustomers(query);
            renderSearchResults(items);
        }, 250);
    });

    syncCustomerMode();
    if (initialSamples.length > 0) {
        initialSamples.forEach((sample) => addSampleRow(sample));
    } else {
        addSampleRow();
    }
    renderSelectedCustomer();
})();
