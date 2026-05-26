(function () {
    const mapping = window.__orderFieldMapping;
    if (!mapping?.templates?.length) {
        return;
    }

    const templatesHost = document.getElementById("fieldMappingTemplates");
    const groupsHost = document.getElementById("fieldMappingGroups");
    const sharedHost = document.getElementById("fieldMappingSharedValues");
    const jsonInput = document.getElementById("fieldMappingJson");
    const form = document.getElementById("orderFieldMappingForm");

    /** @type {{ label: string, members: { templateVersionId: string, templateFieldId: string, tag: string, title: string }[] }[]} */
    let groups = [];

    function fieldLabel(field) {
        const title = field.title?.trim();
        return title ? `${field.tag} — ${title}` : field.tag;
    }

    function renderTemplates() {
        if (!templatesHost) {
            return;
        }

        templatesHost.innerHTML = mapping.templates
            .map((template) => {
                const fieldsHtml = template.fields
                    .filter((field) => field.canRead)
                    .map((field) => {
                        const disabled = !field.canWrite ? " disabled" : "";
                        const hint = !field.canWrite ? ' title="Немає права на запис"' : "";
                        return `<div class="form-check">
                            <input class="form-check-input field-map-check" type="checkbox"
                                   data-version-id="${template.templateVersionId}"
                                   data-field-id="${field.templateFieldId}"
                                   data-tag="${field.tag}"
                                   data-title="${field.title ?? ""}"${disabled}${hint}
                                   id="fld_${field.templateFieldId}" />
                            <label class="form-check-label" for="fld_${field.templateFieldId}">
                                <code class="small">${field.tag}</code>
                                ${field.title ? `<span class="text-muted"> — ${field.title}</span>` : ""}
                            </label>
                        </div>`;
                    })
                    .join("");

                return `<div class="mb-3">
                    <div class="fw-semibold small">${template.templateNameUk} (v${template.versionNumber})</div>
                    <div class="ms-2">${fieldsHtml || '<span class="text-muted small">Немає доступних полів</span>'}</div>
                </div>`;
            })
            .join("");
    }

    function renderGroups() {
        if (!groupsHost) {
            return;
        }

        if (!groups.length) {
            groupsHost.innerHTML = '<p class="text-muted mb-0">Ще немає груп. Оберіть поля та натисніть «Об’єднати вибрані».</p>';
            renderSharedValues();
            syncJson();
            return;
        }

        groupsHost.innerHTML = groups
            .map((group, index) => {
                const members = group.members
                    .map((m) => `<li><code>${m.tag}</code>${m.title ? ` — ${m.title}` : ""}</li>`)
                    .join("");
                return `<div class="border rounded p-2 mb-2" data-group-index="${index}">
                    <div class="d-flex justify-content-between align-items-start gap-2">
                        <strong class="small">Група ${index + 1}</strong>
                        <button type="button" class="btn btn-link btn-sm text-danger p-0 btn-remove-group" data-group-index="${index}">Видалити</button>
                    </div>
                    <ul class="mb-0 ps-3">${members}</ul>
                </div>`;
            })
            .join("");

        groupsHost.querySelectorAll(".btn-remove-group").forEach((btn) => {
            btn.addEventListener("click", () => {
                const idx = Number(btn.getAttribute("data-group-index"));
                groups = groups.filter((_, i) => i !== idx);
                renderGroups();
            });
        });

        renderSharedValues();
        syncJson();
    }

    function renderSharedValues() {
        if (!sharedHost) {
            return;
        }

        if (!groups.length) {
            sharedHost.innerHTML = '<p class="text-muted small mb-0">Спочатку створіть хоча б одну групу.</p>';
            return;
        }

        sharedHost.innerHTML = groups
            .map((group, index) => {
                const label = group.members.map((m) => m.tag).join(" + ");
                return `<div class="mb-2">
                    <label class="form-label small mb-0" for="shared_${index}">Група ${index + 1}: ${label}</label>
                    <textarea class="form-control form-control-sm shared-value-input" id="shared_${index}"
                              data-group-index="${index}" rows="2" placeholder="Спільне значення"></textarea>
                </div>`;
            })
            .join("");
    }

    function syncJson() {
        if (!jsonInput) {
            return;
        }

        const sharedValues = [];
        document.querySelectorAll(".shared-value-input").forEach((el) => {
            const index = Number(el.getAttribute("data-group-index"));
            const value = el.value?.trim();
            if (value) {
                sharedValues.push({ groupIndex: index, value });
            }
        });

        const payload = {
            groups: groups.map((group) => ({
                label: group.label,
                members: group.members.map((m) => ({
                    templateVersionId: m.templateVersionId,
                    templateFieldId: m.templateFieldId
                }))
            })),
            sharedValues
        };

        jsonInput.value = JSON.stringify(payload);
    }

    function getSelectedChecks() {
        return Array.from(document.querySelectorAll(".field-map-check:checked"));
    }

    function fieldAlreadyGrouped(fieldId) {
        return groups.some((group) => group.members.some((m) => m.templateFieldId === fieldId));
    }

    document.getElementById("btnMergeSelected")?.addEventListener("click", () => {
        const selected = getSelectedChecks();
        if (selected.length < 2) {
            window.alert("Оберіть щонайменше два поля з різних або одного шаблону.");
            return;
        }

        const members = [];
        for (const check of selected) {
            const fieldId = check.getAttribute("data-field-id");
            if (fieldAlreadyGrouped(fieldId)) {
                window.alert(`Поле ${check.getAttribute("data-tag")} уже в іншій групі.`);
                return;
            }

            members.push({
                templateVersionId: check.getAttribute("data-version-id"),
                templateFieldId: fieldId,
                tag: check.getAttribute("data-tag") ?? "",
                title: check.getAttribute("data-title") ?? ""
            });
        }

        groups.push({
            label: members.map((m) => m.tag).join(" / "),
            members
        });

        selected.forEach((check) => {
            check.checked = false;
        });

        renderGroups();
    });

    document.getElementById("btnClearSelection")?.addEventListener("click", () => {
        document.querySelectorAll(".field-map-check:checked").forEach((check) => {
            check.checked = false;
        });
    });

    form?.addEventListener("submit", () => {
        syncJson();
    });

    sharedHost?.addEventListener("input", (event) => {
        if (event.target.classList.contains("shared-value-input")) {
            syncJson();
        }
    });

    renderTemplates();
    renderGroups();
})();
