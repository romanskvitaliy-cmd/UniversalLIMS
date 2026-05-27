(function () {
    const selectAll = document.getElementById("selectAllDocuments");
    const checkboxes = [...document.querySelectorAll(".document-send-checkbox")];
    const sendButton = document.getElementById("sendDocumentsButton");
    const sendForm = document.getElementById("orderDocumentsForm");
    const customerForm = document.getElementById("orderCustomerForm");
    const customerFullNameInput = customerForm?.querySelector("input[name='CustomerEdit.FullName']");
    const customerFullNameError = document.getElementById("customerFullNameError");

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

    function normalizeText(value) {
        return String(value ?? "").trim();
    }

    function setCustomerNameError(message) {
        if (!customerFullNameInput) {
            return;
        }
        const hasError = Boolean(message);
        customerFullNameInput.classList.toggle("is-invalid", hasError);
        customerFullNameInput.setAttribute("aria-invalid", hasError ? "true" : "false");
        if (customerFullNameError) {
            customerFullNameError.textContent = message || "";
        }
    }

    function validateCustomerForm() {
        if (!customerFullNameInput) {
            return true;
        }
        const value = normalizeText(customerFullNameInput.value);
        if (value.length === 0) {
            setCustomerNameError(
                customerFullNameInput.dataset.requiredMessage
                || "ПІБ або назва замовника є обов'язковими."
            );
            return false;
        }
        setCustomerNameError("");
        return true;
    }

    customerFullNameInput?.addEventListener("input", () => {
        if (customerFullNameInput.classList.contains("is-invalid")) {
            validateCustomerForm();
        }
    });

    customerForm?.addEventListener("submit", (event) => {
        if (!validateCustomerForm()) {
            event.preventDefault();
            customerFullNameInput?.focus();
        }
    });

    syncSelectAll();
})();
