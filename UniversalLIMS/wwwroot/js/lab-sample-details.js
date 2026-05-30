(() => {
    const selectAll = document.getElementById("labSelectAllExpertDocuments");
    const checkboxes = [...document.querySelectorAll(".lab-expert-send-checkbox")];
    const sendButton = document.getElementById("labSendExpertDocumentsButton");
    const confirmSwitch = document.getElementById("labSendConfirmSwitch");

    window.LimsSendConfirm?.bindToggle(confirmSwitch);

    const getToken = () =>
        document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    const getSelected = () => checkboxes.filter((item) => item.checked);

    const syncSendButton = () => {
        if (!sendButton) {
            return;
        }

        const selectedCount = getSelected().length;
        const totalSendable = checkboxes.length;
        sendButton.disabled = totalSendable === 0;

        if (selectedCount > 0) {
            sendButton.textContent = `Відправити обрані (${selectedCount})`;
            return;
        }

        sendButton.textContent = `Відправити готові (${totalSendable})`;
    };

    const syncSelectAll = () => {
        if (!selectAll || checkboxes.length === 0) {
            syncSendButton();
            return;
        }

        const checkedCount = getSelected().length;
        selectAll.checked = checkedCount === checkboxes.length;
        selectAll.indeterminate = checkedCount > 0 && checkedCount < checkboxes.length;
        syncSendButton();
    };

    const resolveSelection = () => {
        let selected = getSelected();
        if (selected.length === 0) {
            for (const checkbox of checkboxes) {
                checkbox.checked = true;
            }
            selected = [...checkboxes];
            syncSelectAll();
        }

        return selected;
    };

    selectAll?.addEventListener("change", () => {
        for (const checkbox of checkboxes) {
            checkbox.checked = selectAll.checked;
        }
        syncSelectAll();
    });

    for (const checkbox of checkboxes) {
        checkbox.addEventListener("change", syncSelectAll);
    }

    sendButton?.addEventListener("click", async () => {
        const selected = resolveSelection();
        if (selected.length === 0) {
            window.alert("Немає документів, готових до відправки експерту.");
            return;
        }

        const labels = selected.map((checkbox) => {
            const row = checkbox.closest("tr");
            const name = row?.querySelector(".order-detail-doc__name")?.textContent?.trim();
            return name || "шаблон";
        });

        const listText = labels.map((label) => `• ${label}`).join("\n");
        const confirmed = window.LimsSendConfirm?.confirmSend(
            `Відправити експерту ${selected.length} шаблон(ів)?\n\n${listText}\n\nПереконайтесь, що PDF збережено.`
        ) ?? window.confirm(
            `Відправити експерту ${selected.length} шаблон(ів)?\n\n${listText}\n\nПереконайтесь, що PDF збережено.`
        );
        if (!confirmed) {
            return;
        }

        sendButton.disabled = true;
        const errors = [];

        for (const checkbox of selected) {
            const documentId = checkbox.value;
            try {
                const response = await fetch(`/api/laboratory/documents/${documentId}/send-to-expert`, {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        Accept: "application/json",
                        RequestVerificationToken: getToken()
                    }
                });
                const body = await response.json().catch(() => ({}));
                if (!response.ok) {
                    throw new Error(body.message || "Не вдалося відправити.");
                }
            } catch (error) {
                errors.push(error.message || "Помилка відправки.");
            }
        }

        if (errors.length > 0) {
            window.alert(errors.join("\n"));
            syncSelectAll();
            return;
        }

        window.location.reload();
    });

    syncSelectAll();
})();
