(function (global) {
    const MAX_SUGGESTIONS = 8;
    let openMenuRoot = null;

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function normalizeSearchText(value) {
        return String(value ?? "")
            .trim()
            .toLowerCase()
            .replace(/[’'`]/g, "")
            .replace(/\s+/g, " ");
    }

    function createCatalog(templateOptions, investigationTypes) {
        const investigationTypeNameById = new Map(
            (investigationTypes || []).map((type) => [type.id, type.nameUk || ""])
        );
        const investigationTypeCodeById = new Map(
            (investigationTypes || []).map((type) => [type.id, type.code || ""])
        );

        function investigationTypeLabel(option) {
            return investigationTypeNameById.get(option.investigationTypeId) || "";
        }

        function optionLabel(option) {
            if (!option) {
                return "";
            }

            const version = option.versionNumber ? ` (v${option.versionNumber})` : "";
            return `${option.templateNameUk}${version}`;
        }

        function buildOptionSearchText(option) {
            const typeName = investigationTypeNameById.get(option.investigationTypeId) || "";
            const typeCode = investigationTypeCodeById.get(option.investigationTypeId) || "";
            return normalizeSearchText(
                `${option.templateNameUk} v${option.versionNumber} ${typeName} ${typeCode}`
            );
        }

        function getOptionByTemplateVersionId(templateVersionId) {
            return templateOptions.find((option) => option.templateVersionId === templateVersionId) || null;
        }

        function getFilterTokens(filterQuery) {
            return normalizeSearchText(filterQuery).split(" ").filter(Boolean);
        }

        function optionMatchesFilter(option, tokens, selectedTemplateVersionId) {
            if (tokens.length === 0) {
                return true;
            }

            if (selectedTemplateVersionId && option.templateVersionId === selectedTemplateVersionId) {
                return true;
            }

            const haystack = buildOptionSearchText(option);
            return tokens.every((token) => haystack.includes(token));
        }

        function getMatchingOptions(filterQuery, selectedTemplateVersionId, limit) {
            const tokens = getFilterTokens(filterQuery);
            const matches = [];

            for (const option of templateOptions) {
                if (!optionMatchesFilter(option, tokens, selectedTemplateVersionId)) {
                    continue;
                }

                matches.push(option);
                if (limit && matches.length >= limit) {
                    break;
                }
            }

            return { matches, tokens, totalMatches: countMatches(filterQuery, selectedTemplateVersionId) };
        }

        function countMatches(filterQuery, selectedTemplateVersionId) {
            const tokens = getFilterTokens(filterQuery);
            return templateOptions.filter((option) =>
                optionMatchesFilter(option, tokens, selectedTemplateVersionId)
            ).length;
        }

        function buildSelectOptionsHtml(selectedTemplateVersionId, filterQuery) {
            const tokens = getFilterTokens(filterQuery);
            const emptySelected = selectedTemplateVersionId ? "" : " selected";
            const parts = [`<option value=""${emptySelected}>— оберіть бланк —</option>`];

            for (const option of templateOptions) {
                if (!optionMatchesFilter(option, tokens, selectedTemplateVersionId)) {
                    continue;
                }

                const selected = option.templateVersionId === selectedTemplateVersionId ? " selected" : "";
                parts.push(
                    `<option value="${option.templateVersionId}" data-investigation-type-id="${option.investigationTypeId}"${selected}>${escapeHtml(optionLabel(option))}</option>`
                );
            }

            return parts.join("");
        }

        return {
            templateOptions,
            optionLabel,
            investigationTypeLabel,
            getOptionByTemplateVersionId,
            getMatchingOptions,
            buildSelectOptionsHtml,
            countMatches
        };
    }

    function renderPickerHtml(catalog, selectedTemplateVersionId, options) {
        const showPreview = options?.showPreview !== false;
        const selectHtml = catalog.buildSelectOptionsHtml(selectedTemplateVersionId, "");
        const previewButton = showPreview
            ? `<button type="button"
                       class="btn btn-sm btn-outline-secondary order-template-field__preview"
                       title="Переглянути оригінал PDF"
                       aria-label="Переглянути оригінал PDF"
                       disabled>
                   <i class="bi bi-eye" aria-hidden="true"></i>
               </button>`
            : "";

        return `
            <div class="order-template-field">
                <div class="order-template-field__head">
                    <label class="order-template-field__search">
                        <i class="bi bi-search order-template-field__search-icon" aria-hidden="true"></i>
                        <input type="search"
                               class="order-template-field__search-input"
                               placeholder="Ф327, 205, вода…"
                               autocomplete="off"
                               spellcheck="false"
                               aria-label="Пошук бланка" />
                    </label>
                    ${previewButton}
                </div>
                <div class="order-template-field__body">
                    <ul class="order-template-field__menu list-group list-group-flush" role="listbox" hidden></ul>
                    <select class="form-select form-select-sm sample-template-select">
                        ${selectHtml}
                    </select>
                    <p class="order-template-field__hint small text-muted mb-0" hidden></p>
                </div>
            </div>`;
    }

    function closeOpenMenu() {
        if (!openMenuRoot) {
            return;
        }

        const menu = openMenuRoot.querySelector(".order-template-field__menu");
        menu?.setAttribute("hidden", "hidden");
        openMenuRoot.classList.remove("order-template-field--menu-open");
        openMenuRoot = null;
    }

    function bindPicker(root, catalog, callbacks) {
        const searchInput = root.querySelector(".order-template-field__search-input");
        const select = root.querySelector(".sample-template-select");
        const menu = root.querySelector(".order-template-field__menu");
        const hint = root.querySelector(".order-template-field__hint");
        const previewButton = root.querySelector(".order-template-field__preview");
        let filterQuery = "";
        let searchTimer = null;
        let activeSuggestionIndex = -1;

        function syncPreviewButton() {
            if (!previewButton) {
                return;
            }

            const option = catalog.getOptionByTemplateVersionId(select.value);
            const previewUrl = option?.previewUrl || "";
            previewButton.disabled = !previewUrl;
            previewButton.dataset.previewUrl = previewUrl;
            previewButton.dataset.previewTitle = option ? catalog.optionLabel(option) : "";
        }

        function refreshSelect() {
            const selectedId = select.value;
            select.innerHTML = catalog.buildSelectOptionsHtml(selectedId, filterQuery);

            if (selectedId && !select.querySelector(`option[value="${CSS.escape(selectedId)}"]`)) {
                select.value = "";
            } else if (selectedId) {
                select.value = selectedId;
            }

            syncPreviewButton();
        }

        function renderMenu() {
            const tokens = catalog.getMatchingOptions(filterQuery, select.value, 0).tokens;
            const { matches, totalMatches } = catalog.getMatchingOptions(
                filterQuery,
                select.value,
                MAX_SUGGESTIONS
            );

            if (!filterQuery.trim() || tokens.length === 0) {
                menu.setAttribute("hidden", "hidden");
                root.classList.remove("order-template-field--menu-open");
                if (openMenuRoot === root) {
                    openMenuRoot = null;
                }

                hint.setAttribute("hidden", "hidden");
                return;
            }

            activeSuggestionIndex = -1;

            if (matches.length === 0) {
                menu.innerHTML = `
                    <li class="list-group-item order-template-field__menu-empty small text-muted">
                        Нічого не знайдено. Спробуйте інші слова або оберіть у списку нижче.
                    </li>`;
                hint.textContent = "0 збігів";
                hint.removeAttribute("hidden");
            } else {
                menu.innerHTML = matches
                    .map((option, index) => {
                        const typeLabel = catalog.investigationTypeLabel(option);
                        const meta = typeLabel
                            ? `<span class="order-template-field__menu-meta">${escapeHtml(typeLabel)}</span>`
                            : "";
                        return `
                            <li>
                                <button type="button"
                                        class="list-group-item list-group-item-action order-template-field__menu-item py-2"
                                        role="option"
                                        data-template-version-id="${option.templateVersionId}"
                                        data-suggestion-index="${index}">
                                    <span class="order-template-field__menu-title">${escapeHtml(catalog.optionLabel(option))}</span>
                                    ${meta}
                                </button>
                            </li>`;
                    })
                    .join("");

                const more = totalMatches > matches.length ? ` · ще ${totalMatches - matches.length} у списку` : "";
                hint.textContent =
                    totalMatches === 1
                        ? "1 збіг — Enter або клік"
                        : `${totalMatches} збігів${more}`;
                hint.removeAttribute("hidden");
            }

            menu.removeAttribute("hidden");
            root.classList.add("order-template-field--menu-open");
            openMenuRoot = root;
        }

        function selectTemplateVersionId(templateVersionId) {
            select.value = templateVersionId || "";
            filterQuery = "";
            searchInput.value = "";
            refreshSelect();
            closeOpenMenu();
            syncPreviewButton();
            callbacks?.onChange?.();
        }

        function highlightSuggestion(index) {
            const items = menu.querySelectorAll(".order-template-field__menu-item");
            items.forEach((item, itemIndex) => {
                item.classList.toggle("active", itemIndex === index);
            });
            activeSuggestionIndex = index;
        }

        function pickHighlightedSuggestion() {
            const items = menu.querySelectorAll(".order-template-field__menu-item");
            if (activeSuggestionIndex >= 0 && activeSuggestionIndex < items.length) {
                selectTemplateVersionId(items[activeSuggestionIndex].dataset.templateVersionId);
                return true;
            }

            if (items.length === 1) {
                selectTemplateVersionId(items[0].dataset.templateVersionId);
                return true;
            }

            return false;
        }

        searchInput.addEventListener("input", () => {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(() => {
                filterQuery = searchInput.value;
                refreshSelect();
                renderMenu();
            }, 80);
        });

        searchInput.addEventListener("focus", () => {
            if (filterQuery.trim()) {
                renderMenu();
            }
        });

        searchInput.addEventListener("keydown", (event) => {
            const items = menu.querySelectorAll(".order-template-field__menu-item");
            if (event.key === "ArrowDown") {
                event.preventDefault();
                if (menu.hasAttribute("hidden")) {
                    renderMenu();
                }

                const nextIndex =
                    activeSuggestionIndex < items.length - 1 ? activeSuggestionIndex + 1 : 0;
                highlightSuggestion(nextIndex);
                items[nextIndex]?.scrollIntoView({ block: "nearest" });
                return;
            }

            if (event.key === "ArrowUp") {
                event.preventDefault();
                const prevIndex =
                    activeSuggestionIndex > 0 ? activeSuggestionIndex - 1 : items.length - 1;
                highlightSuggestion(prevIndex);
                items[prevIndex]?.scrollIntoView({ block: "nearest" });
                return;
            }

            if (event.key === "Enter") {
                if (!menu.hasAttribute("hidden") && pickHighlightedSuggestion()) {
                    event.preventDefault();
                }
                return;
            }

            if (event.key === "Escape") {
                closeOpenMenu();
                searchInput.blur();
            }
        });

        menu.addEventListener("click", (event) => {
            const button = event.target.closest(".order-template-field__menu-item");
            if (!button) {
                return;
            }

            selectTemplateVersionId(button.dataset.templateVersionId);
        });

        select.addEventListener("change", () => {
            filterQuery = "";
            searchInput.value = "";
            refreshSelect();
            closeOpenMenu();
            callbacks?.onChange?.();
        });

        previewButton?.addEventListener("click", () => {
            const option = catalog.getOptionByTemplateVersionId(select.value);
            const previewUrl = option?.previewUrl || previewButton.dataset.previewUrl || "";
            callbacks?.onPreview?.(previewUrl, option ? catalog.optionLabel(option) : "");
        });

        refreshSelect();

        return {
            getValue() {
                return select.value;
            },
            getSelectedOption() {
                return catalog.getOptionByTemplateVersionId(select.value);
            },
            setValue(templateVersionId) {
                select.value = templateVersionId || "";
                filterQuery = "";
                searchInput.value = "";
                refreshSelect();
            },
            syncPreviewButton
        };
    }

    document.addEventListener("click", (event) => {
        if (!openMenuRoot || openMenuRoot.contains(event.target)) {
            return;
        }

        closeOpenMenu();
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeOpenMenu();
        }
    });

    global.OrderTemplateField = {
        createCatalog,
        renderPickerHtml,
        bindPicker,
        closeOpenMenu
    };
})(window);
