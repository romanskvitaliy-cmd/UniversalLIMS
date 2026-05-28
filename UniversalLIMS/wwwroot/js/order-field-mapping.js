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
    const autoMergePreviewButton = document.getElementById("btnAutoMergePreview");
    const autoMergeConfirmButton = document.getElementById("btnAutoMergeConfirm");
    const autoMergeStatus = document.getElementById("autoMergeStatus");

    /** @type {{ label: string, members: { templateVersionId: string, templateFieldId: string, tag: string, title: string }[] }[]} */
    let groups = [];

    /** @type {{ groupIndex: number, value: string }[]} */
    let pendingSharedValues = [];
    /** @type {{ label: string, members: { templateVersionId: string, templateFieldId: string, tag: string, title: string }[] }[]} */
    let pendingAutoMergeGroups = [];

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
                    .map((m, memberIndex) => `<li class="d-flex justify-content-between align-items-center gap-2">
                        <span><code>${m.tag}</code>${m.title ? ` — ${m.title}` : ""}</span>
                        <button type="button"
                                class="btn btn-link btn-sm text-warning p-0 btn-remove-member"
                                data-group-index="${index}"
                                data-member-index="${memberIndex}">
                            Роз’єднати
                        </button>
                    </li>`)
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
                resetPendingAutoMergeStatus();
                renderGroups();
            });
        });

        groupsHost.querySelectorAll(".btn-remove-member").forEach((btn) => {
            btn.addEventListener("click", () => {
                const groupIndex = Number(btn.getAttribute("data-group-index"));
                const memberIndex = Number(btn.getAttribute("data-member-index"));
                const group = groups[groupIndex];
                if (!group) {
                    return;
                }

                const nextMembers = group.members.filter((_, i) => i !== memberIndex);
                if (nextMembers.length < 2) {
                    groups = groups.filter((_, i) => i !== groupIndex);
                } else {
                    groups[groupIndex] = {
                        ...group,
                        members: nextMembers,
                        label: nextMembers.map((m) => m.tag).join(" / ")
                    };
                }

                resetPendingAutoMergeStatus();
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

        pendingSharedValues.forEach((item) => {
            const el = document.getElementById(`shared_${item.groupIndex}`);
            if (el) {
                el.value = item.value ?? "";
            }
        });
        pendingSharedValues = [];
        syncJson();
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

    function normalizeTag(tag) {
        return String(tag ?? "").trim().toLowerCase();
    }

    function computeAutoMergeGroupsByExactTag() {
        const checks = Array.from(document.querySelectorAll(".field-map-check"))
            .filter((check) => !check.disabled);
        const bucketByTag = new Map();

        checks.forEach((check) => {
            const fieldId = check.getAttribute("data-field-id");
            if (!fieldId || fieldAlreadyGrouped(fieldId)) {
                return;
            }

            const tag = check.getAttribute("data-tag") ?? "";
            const normalizedTag = normalizeTag(tag);
            if (!normalizedTag) {
                return;
            }

            const member = {
                templateVersionId: check.getAttribute("data-version-id"),
                templateFieldId: fieldId,
                tag,
                title: check.getAttribute("data-title") ?? ""
            };

            if (!bucketByTag.has(normalizedTag)) {
                bucketByTag.set(normalizedTag, []);
            }
            bucketByTag.get(normalizedTag).push(member);
        });

        const createdGroups = [];
        bucketByTag.forEach((members) => {
            if (members.length < 2) {
                return;
            }

            const uniqueTemplateIds = new Set(members.map((member) => member.templateVersionId));
            if (uniqueTemplateIds.size < 2) {
                return;
            }

            createdGroups.push({
                label: members[0].tag,
                members
            });
        });

        return createdGroups;
    }

    function resetPendingAutoMergeStatus() {
        pendingAutoMergeGroups = [];
        if (autoMergeConfirmButton) {
            autoMergeConfirmButton.classList.add("d-none");
        }
        if (autoMergeStatus) {
            autoMergeStatus.classList.add("d-none");
            autoMergeStatus.textContent = "";
            autoMergeStatus.classList.remove("text-success", "text-warning");
            autoMergeStatus.classList.add("text-muted");
        }
    }

    function previewAutoMergeByExactTag() {
        pendingAutoMergeGroups = computeAutoMergeGroupsByExactTag();
        if (pendingAutoMergeGroups.length === 0) {
            if (autoMergeStatus) {
                autoMergeStatus.textContent = "Збігів для автооб’єднання не знайдено.";
                autoMergeStatus.classList.remove("d-none", "text-success", "text-muted");
                autoMergeStatus.classList.add("text-warning");
            }
            if (autoMergeConfirmButton) {
                autoMergeConfirmButton.classList.add("d-none");
            }
            return;
        }

        if (autoMergeStatus) {
            autoMergeStatus.textContent =
                `Знайдено груп для автооб’єднання: ${pendingAutoMergeGroups.length}. Перевірте і натисніть «Підтвердити автооб’єднання».`;
            autoMergeStatus.classList.remove("d-none", "text-warning", "text-muted");
            autoMergeStatus.classList.add("text-success");
        }
        if (autoMergeConfirmButton) {
            autoMergeConfirmButton.classList.remove("d-none");
        }
    }

    function confirmAutoMergeByExactTag() {
        if (!pendingAutoMergeGroups.length) {
            previewAutoMergeByExactTag();
            return;
        }

        pendingAutoMergeGroups.forEach((group) => groups.push(group));
        const created = pendingAutoMergeGroups.length;
        renderGroups();
        resetPendingAutoMergeStatus();
        window.alert(`Створено груп: ${created}. Автооб’єднання виконано за точним збігом тегу.`);
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

        resetPendingAutoMergeStatus();
        renderGroups();
    });

    document.getElementById("btnClearSelection")?.addEventListener("click", () => {
        document.querySelectorAll(".field-map-check:checked").forEach((check) => {
            check.checked = false;
        });
    });

    autoMergePreviewButton?.addEventListener("click", () => {
        previewAutoMergeByExactTag();
    });

    autoMergeConfirmButton?.addEventListener("click", () => {
        confirmAutoMergeByExactTag();
    });

    function getSelectedTemplateVersionIds() {
        return Array.from(
            form?.querySelectorAll('input[name*=".SelectedTemplateVersionIds"]') ?? []
        )
            .map((input) => input.value)
            .filter(Boolean);
    }

    function applyAdaptedMapping(result) {
        groups = (result.groups ?? []).map((group) => ({
            label: group.label ?? group.members?.map((m) => m.tag).join(" / ") ?? "",
            members: (group.members ?? []).map((m) => ({
                templateVersionId: m.templateVersionId,
                templateFieldId: m.templateFieldId,
                tag: m.tag ?? "",
                title: m.title ?? ""
            }))
        }));

        pendingSharedValues = (result.sharedValues ?? []).map((item) => ({
            groupIndex: item.groupIndex,
            value: item.value ?? ""
        }));

        resetPendingAutoMergeStatus();
        renderGroups();
    }

    document.getElementById("btnCopyFieldMapping")?.addEventListener("click", async () => {
        const sourceOrderId = document.getElementById("copyMappingSourceOrderId")?.value;
        const statusEl = document.getElementById("copyMappingStatus");
        const adaptUrl = window.__orderFieldMappingAdaptUrl;

        if (!sourceOrderId) {
            window.alert("Оберіть замовлення, з якого копіювати мапінг.");
            return;
        }

        const versionIds = getSelectedTemplateVersionIds();
        if (versionIds.length < 2) {
            window.alert("Потрібно щонайменше два шаблони в замовленні.");
            return;
        }

        if (!adaptUrl) {
            return;
        }

        const params = new URLSearchParams();
        params.set("sourceOrderId", sourceOrderId);
        versionIds.forEach((id) => params.append("templateVersionIds", id));

        const btn = document.getElementById("btnCopyFieldMapping");
        btn?.setAttribute("disabled", "disabled");

        try {
            const response = await fetch(`${adaptUrl}?${params.toString()}`, {
                headers: { Accept: "application/json" }
            });
            const payload = await response.json();

            if (!response.ok) {
                const message = payload?.message ?? "Не вдалося скопіювати мапінг.";
                window.alert(message);
                return;
            }

            if (!payload.groups?.length) {
                if (statusEl) {
                    statusEl.textContent = payload.infoMessage ?? "Групи не перенесено.";
                    statusEl.classList.remove("d-none", "text-success");
                    statusEl.classList.add("text-warning");
                }
                return;
            }

            if (
                groups.length > 0 &&
                !window.confirm("Поточні групи буде замінено скопійованим мапінгом. Продовжити?")
            ) {
                return;
            }

            applyAdaptedMapping(payload);

            if (statusEl) {
                statusEl.textContent = payload.infoMessage ?? `Перенесено груп: ${payload.groups.length}.`;
                statusEl.classList.remove("d-none", "text-warning");
                statusEl.classList.add("text-success");
            }
        } catch {
            window.alert("Помилка мережі під час копіювання мапінгу.");
        } finally {
            btn?.removeAttribute("disabled");
        }
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
    resetPendingAutoMergeStatus();
})();
