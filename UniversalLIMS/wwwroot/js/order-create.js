(function () {
    const templateOptions = window.__orderCreateTemplateOptions || [];
    const branches = window.__orderCreateBranches || [];
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
    const investigationSelect = document.getElementById("investigationTypeId");
    const documentsList = document.getElementById("templateDocumentsList");
    const documentsPlaceholder = document.getElementById("templateDocumentsPlaceholder");
    const form = document.getElementById("orderCreateForm");
    const btnPrepareMapping = document.getElementById("btnPrepareFieldMapping");
    const fieldMappingHint = document.getElementById("fieldMappingHint");

    let searchTimer = null;

    function syncFieldMappingActions() {
        const checkedCount = documentsList?.querySelectorAll(".template-version-checkbox:checked").length ?? 0;
        const multi = checkedCount >= 2;
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
                return `<option value="${branch.id}"${selected}>${branch.name}</option>`;
            })
            .join("");
    }

    function renderTemplateDocuments() {
        const typeId = investigationSelect?.value;
        if (!documentsList || !documentsPlaceholder) {
            return;
        }

        documentsList.innerHTML = "";
        if (!typeId) {
            documentsPlaceholder.textContent = "Спочатку оберіть тип дослідження.";
            documentsPlaceholder.classList.remove("d-none");
            documentsList.classList.add("d-none");
            return;
        }

        const options = templateOptions.filter((item) => item.investigationTypeId === typeId);
        if (!options.length) {
            documentsPlaceholder.textContent = "Для цього типу немає опублікованих PDF-шаблонів.";
            documentsPlaceholder.classList.remove("d-none");
            documentsList.classList.add("d-none");
            return;
        }

        documentsPlaceholder.classList.add("d-none");
        documentsList.classList.remove("d-none");

        for (const option of options) {
            const row = document.createElement("div");
            row.className = "row g-2 align-items-center border-bottom py-2 template-document-row";
            row.dataset.templateVersionId = option.templateVersionId;

            const defaultSelected = option.isDefault ? " checked" : "";
            const branchDefault = defaultBranchId || (branches[0]?.id ?? "");

            row.innerHTML = `
                <div class="col-md-6">
                    <div class="form-check">
                        <input class="form-check-input template-version-checkbox" type="checkbox"
                               name="Input.SelectedTemplateVersionIds" value="${option.templateVersionId}" id="tpl-${option.templateVersionId}"${defaultSelected} />
                        <label class="form-check-label" for="tpl-${option.templateVersionId}">
                            ${option.templateNameUk} <span class="text-muted">(v${option.versionNumber})</span>
                            ${option.isDefault ? '<span class="text-warning" title="За замовчуванням">★</span>' : ""}
                        </label>
                    </div>
                </div>
                <div class="col-md-6">
                    <label class="form-label small text-muted mb-0">Філія лабораторії</label>
                    <select class="form-select form-select-sm document-branch-select" name="Input.DocumentTargetBranchIds" disabled>
                        ${branchOptionsHtml(branchDefault)}
                    </select>
                </div>`;

            documentsList.appendChild(row);
        }

        bindTemplateRowHandlers();
    }

    function bindTemplateRowHandlers() {
        documentsList?.querySelectorAll(".template-document-row").forEach((row) => {
            const checkbox = row.querySelector(".template-version-checkbox");
            const branchSelect = row.querySelector(".document-branch-select");
            if (!checkbox || !branchSelect) {
                return;
            }

            const syncBranchEnabled = () => {
                branchSelect.disabled = !checkbox.checked;
                if (!checkbox.checked) {
                    branchSelect.removeAttribute("name");
                } else {
                    branchSelect.setAttribute("name", "Input.DocumentTargetBranchIds");
                }
            };

            syncBranchEnabled();
            checkbox.addEventListener("change", () => {
                syncBranchEnabled();
                syncFieldMappingActions();
            });
        });

        syncFieldMappingActions();
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
        documentsList?.querySelectorAll(".template-document-row").forEach((row) => {
            const checkbox = row.querySelector(".template-version-checkbox");
            const branchSelect = row.querySelector(".document-branch-select");
            if (!checkbox?.checked && branchSelect) {
                branchSelect.disabled = true;
                branchSelect.removeAttribute("name");
            }
        });
    });

    modeExisting?.addEventListener("change", syncCustomerMode);
    modeNew?.addEventListener("change", syncCustomerMode);
    investigationSelect?.addEventListener("change", renderTemplateDocuments);

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
    renderTemplateDocuments();
    renderSelectedCustomer();
})();
