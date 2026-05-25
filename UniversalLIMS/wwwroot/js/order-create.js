(function () {
    const templateOptions = window.__orderCreateTemplateOptions || [];
    const modeExisting = document.getElementById('customerModeExisting');
    const modeNew = document.getElementById('customerModeNew');
    const existingPanel = document.getElementById('existingCustomerPanel');
    const newPanel = document.getElementById('newCustomerPanel');
    const searchInput = document.getElementById('customerSearch');
    const resultsBox = document.getElementById('customerSearchResults');
    const selectedIdInput = document.getElementById('selectedCustomerId');
    const selectedLabelInput = document.getElementById('selectedCustomerLabel');
    const selectedSummary = document.getElementById('selectedCustomerSummary');
    const investigationSelect = document.getElementById('investigationTypeId');
    const templateSelect = document.getElementById('templateVersionId');

    let searchTimer = null;

    function syncCustomerMode() {
        const isNew = modeNew?.checked;
        existingPanel?.classList.toggle('d-none', isNew);
        newPanel?.classList.toggle('d-none', !isNew);
    }

    function renderSelectedCustomer() {
        const label = selectedLabelInput?.value?.trim();
        if (!label) {
            selectedSummary.textContent = '';
            return;
        }

        selectedSummary.textContent = `Обрано: ${label}`;
    }

    function bindTemplateOptions() {
        const typeId = investigationSelect?.value;
        const current = templateSelect?.value;
        templateSelect.innerHTML = '<option value="">— за замовчуванням —</option>';

        if (!typeId) {
            return;
        }

        const options = templateOptions.filter(item => item.investigationTypeId === typeId);
        for (const option of options) {
            const el = document.createElement('option');
            el.value = option.templateVersionId;
            el.textContent = `${option.templateNameUk} (v${option.versionNumber})${option.isDefault ? ' ★' : ''}`;
            if (option.isDefault && !current) {
                el.selected = true;
            }

            templateSelect.appendChild(el);
        }

        if (current) {
            templateSelect.value = current;
        }
    }

    async function searchCustomers(query) {
        const url = `/api/customers/search?q=${encodeURIComponent(query)}&take=15`;
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        if (!response.ok) {
            return [];
        }

        return response.json();
    }

    function renderSearchResults(items) {
        resultsBox.innerHTML = '';
        if (!items.length) {
            resultsBox.classList.add('d-none');
            return;
        }

        for (const item of items) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'list-group-item list-group-item-action py-2 small';
            const org = item.organizationName ? ` · ${item.organizationName}` : '';
            const phone = item.contactPhone ? ` · ${item.contactPhone}` : '';
            button.textContent = `${item.fullName}${org}${phone}`;
            button.addEventListener('click', () => {
                selectedIdInput.value = item.id;
                selectedLabelInput.value = button.textContent;
                searchInput.value = item.fullName;
                resultsBox.classList.add('d-none');
                renderSelectedCustomer();
            });
            resultsBox.appendChild(button);
        }

        resultsBox.classList.remove('d-none');
    }

    modeExisting?.addEventListener('change', syncCustomerMode);
    modeNew?.addEventListener('change', syncCustomerMode);
    investigationSelect?.addEventListener('change', bindTemplateOptions);

    searchInput?.addEventListener('input', () => {
        clearTimeout(searchTimer);
        const query = searchInput.value.trim();
        if (query.length < 2) {
            resultsBox.classList.add('d-none');
            return;
        }

        searchTimer = setTimeout(async () => {
            const items = await searchCustomers(query);
            renderSearchResults(items);
        }, 250);
    });

    syncCustomerMode();
    bindTemplateOptions();
    renderSelectedCustomer();
})();
