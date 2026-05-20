(() => {
    "use strict";

    const normalize = (value) => String(value ?? "").trim().toLowerCase();

    function initSearchableCombo(container, config) {
        if (!container) return null;

        const items = Array.isArray(config?.items) ? config.items : [];
        const placeholder = config?.placeholder || "Оберіть…";
        const emptyText = config?.emptyText || "Нічого не знайдено";
        const disabled = config?.disabled === true;

        container.classList.add("searchable-combo");
        container.innerHTML = `
            <button type="button" class="searchable-combo-trigger form-control form-control-sm" aria-haspopup="listbox" aria-expanded="false">
                <span class="searchable-combo-icon" aria-hidden="true">🔍</span>
                <span class="searchable-combo-label"></span>
                <span class="searchable-combo-caret" aria-hidden="true">▾</span>
            </button>
            <div class="searchable-combo-menu" hidden>
                <input type="text" class="searchable-combo-search form-control form-control-sm" placeholder="Пошук тегу..." autocomplete="off" />
                <ul class="searchable-combo-list" role="listbox"></ul>
            </div>`;

        const trigger = container.querySelector(".searchable-combo-trigger");
        const labelEl = container.querySelector(".searchable-combo-label");
        const menu = container.querySelector(".searchable-combo-menu");
        const searchInput = container.querySelector(".searchable-combo-search");
        const listEl = container.querySelector(".searchable-combo-list");

        let isOpen = false;
        let activeIndex = -1;
        let filtered = items;
        let currentValue = config?.value ?? null;

        const getItemId = (item) => item?.id ?? item?.Id ?? item?.key ?? item?.Key ?? null;
        const getItemKey = (item) => item?.key ?? item?.Key ?? "";
        const getItemDisplay = (item) => item?.displayName ?? item?.DisplayNameUk ?? item?.displayNameUk ?? getItemKey(item);
        const getItemGroup = (item) => item?.groupName ?? item?.GroupName ?? "";

        const findItem = (value) => {
            if (value === null || value === undefined || value === "") return null;
            const key = String(value);
            return items.find((item) => String(getItemId(item)) === key) ?? null;
        };

        const renderLabel = () => {
            const selected = findItem(currentValue);
            labelEl.textContent = selected ? getItemDisplay(selected) : placeholder;
            labelEl.classList.toggle("text-muted", !selected);
        };

        const renderList = () => {
            listEl.innerHTML = "";
            if (filtered.length === 0) {
                const empty = document.createElement("li");
                empty.className = "searchable-combo-empty";
                empty.textContent = emptyText;
                listEl.appendChild(empty);
                activeIndex = -1;
                return;
            }

            filtered.forEach((item, index) => {
                const li = document.createElement("li");
                li.className = "searchable-combo-option";
                li.setAttribute("role", "option");
                li.dataset.value = String(getItemId(item));
                const selected = String(getItemId(item)) === String(currentValue ?? "");
                li.setAttribute("aria-selected", selected ? "true" : "false");
                li.classList.toggle("is-selected", selected);
                li.classList.toggle("is-active", index === activeIndex);

                const group = getItemGroup(item);
                if (group) {
                    const groupEl = document.createElement("span");
                    groupEl.className = "searchable-combo-option-group";
                    groupEl.textContent = group;
                    li.appendChild(groupEl);
                }

                const title = document.createElement("span");
                title.className = "searchable-combo-option-title";
                title.textContent = getItemDisplay(item);
                li.appendChild(title);

                const key = document.createElement("span");
                key.className = "searchable-combo-option-key";
                key.textContent = getItemKey(item);
                li.appendChild(key);

                li.addEventListener("mousedown", (event) => event.preventDefault());
                li.addEventListener("click", () => selectItem(item));
                listEl.appendChild(li);
            });
        };

        const applyFilter = (query) => {
            const q = normalize(query);
            filtered = !q
                ? items
                : items.filter((item) => {
                    const haystack = [
                        getItemDisplay(item),
                        getItemKey(item),
                        getItemGroup(item)
                    ].join(" ");
                    return normalize(haystack).includes(q);
                });
            activeIndex = filtered.length ? 0 : -1;
            renderList();
        };

        const openMenu = () => {
            if (disabled || isOpen) return;
            isOpen = true;
            menu.hidden = false;
            trigger.setAttribute("aria-expanded", "true");
            container.classList.add("is-open");
            searchInput.value = "";
            applyFilter("");
            window.setTimeout(() => searchInput.focus(), 0);
        };

        const closeMenu = () => {
            if (!isOpen) return;
            isOpen = false;
            menu.hidden = true;
            trigger.setAttribute("aria-expanded", "false");
            container.classList.remove("is-open");
            activeIndex = -1;
        };

        const notifyPreview = (item) => {
            if (typeof config?.onPreview === "function") {
                config.onPreview(item ?? null);
            }
        };

        const selectItem = (item) => {
            currentValue = getItemId(item);
            renderLabel();
            renderList();
            closeMenu();
            notifyPreview(item);
            if (typeof config?.onChange === "function") {
                config.onChange(currentValue, item);
            }
        };

        const moveActive = (delta) => {
            if (!filtered.length) return;
            if (activeIndex < 0) activeIndex = 0;
            else activeIndex = Math.max(0, Math.min(filtered.length - 1, activeIndex + delta));
            renderList();
            const active = listEl.querySelector(".is-active");
            active?.scrollIntoView({ block: "nearest" });
            notifyPreview(filtered[activeIndex] ?? null);
        };

        trigger.addEventListener("click", () => (isOpen ? closeMenu() : openMenu()));
        searchInput.addEventListener("input", () => applyFilter(searchInput.value));
        searchInput.addEventListener("keydown", (event) => {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                moveActive(1);
                return;
            }
            if (event.key === "ArrowUp") {
                event.preventDefault();
                moveActive(-1);
                return;
            }
            if (event.key === "Enter") {
                event.preventDefault();
                if (activeIndex >= 0 && filtered[activeIndex]) selectItem(filtered[activeIndex]);
                return;
            }
            if (event.key === "Escape") {
                event.preventDefault();
                closeMenu();
            }
        });

        document.addEventListener("mousedown", (event) => {
            if (!container.contains(event.target)) closeMenu();
        });

        const api = {
            setValue(value) {
                currentValue = value;
                renderLabel();
                if (isOpen) renderList();
            },
            getValue() {
                return currentValue;
            },
            getSelectedItem() {
                return findItem(currentValue);
            },
            setDisabled(next) {
                trigger.disabled = next;
                if (next) closeMenu();
            },
            setItems(nextItems) {
                items.length = 0;
                items.push(...(nextItems || []));
                applyFilter(searchInput.value);
                renderLabel();
            }
        };

        api.setDisabled(disabled);
        renderLabel();
        return api;
    }

    window.initSearchableCombo = initSearchableCombo;
})();
