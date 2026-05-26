(function () {
    const selectAll = document.getElementById("selectAllDocuments");
    const checkboxes = [...document.querySelectorAll(".document-send-checkbox")];
    const sendButton = document.getElementById("sendDocumentsButton");
    const sendForm = document.getElementById("orderDocumentsForm");

    function syncSelectAll() {
        if (!selectAll || checkboxes.length === 0) {
            return;
        }

        const checkedCount = checkboxes.filter((item) => item.checked).length;
        selectAll.checked = checkedCount === checkboxes.length;
        selectAll.indeterminate = checkedCount > 0 && checkedCount < checkboxes.length;
    }

    selectAll?.addEventListener("change", () => {
        for (const checkbox of checkboxes) {
            checkbox.checked = selectAll.checked;
        }
        syncSelectAll();
    });

    for (const checkbox of checkboxes) {
        checkbox.addEventListener("change", syncSelectAll);
    }

    sendForm?.addEventListener("submit", (event) => {
        const selected = checkboxes.filter((item) => item.checked);
        if (selected.length === 0) {
            event.preventDefault();
            window.alert("Оберіть хоча б один документ для відправки в лабораторію.");
            return;
        }

        if (sendButton) {
            sendButton.disabled = true;
        }
    });

    document.querySelectorAll(".routing-form").forEach((routingForm) => {
        routingForm.addEventListener("submit", (event) => {
            event.stopPropagation();
        });
    });

    syncSelectAll();
})();
