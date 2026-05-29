(function () {
    const RULES = {
        edrpou: {
            test(value) {
                const trimmed = String(value ?? "").trim();
                if (!trimmed) {
                    return "";
                }

                const digits = trimmed.replace(/\D/g, "");
                return digits.length === 8
                    ? ""
                    : "ЄДРПОУ — 8 цифр. Можна зберегти й так.";
            }
        },
        rnokpp: {
            test(value) {
                const trimmed = String(value ?? "").trim();
                if (!trimmed) {
                    return "";
                }

                const digits = trimmed.replace(/\D/g, "");
                return digits.length === 10
                    ? ""
                    : "РНОКПП — 10 цифр. Можна зберегти й так.";
            }
        },
        email: {
            test(value) {
                const trimmed = String(value ?? "").trim();
                if (!trimmed) {
                    return "";
                }

                return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)
                    ? ""
                    : "Перевірте формат email. Можна зберегти й так.";
            }
        },
        phone: {
            test(value) {
                const trimmed = String(value ?? "").trim();
                if (!trimmed) {
                    return "";
                }

                const digits = trimmed.replace(/\D/g, "");
                return digits.length >= 9 && digits.length <= 13
                    ? ""
                    : "Телефон виглядає некоректно. Можна зберегти й так.";
            }
        }
    };

    function normalizeText(value) {
        return String(value ?? "").trim();
    }

    function ensureFeedbackElement(input) {
        const existing = input.parentElement?.querySelector(`[data-lims-soft-feedback="${input.name}"]`);
        if (existing) {
            return existing;
        }

        const feedback = document.createElement("div");
        feedback.className = "invalid-feedback lims-soft-invalid-feedback";
        feedback.dataset.limsSoftFeedback = input.name;
        input.insertAdjacentElement("afterend", feedback);
        return feedback;
    }

    function setSoftHint(input, message) {
        const feedback = ensureFeedbackElement(input);
        const hasHint = Boolean(message);
        input.classList.toggle("is-invalid", hasHint);
        input.classList.toggle("lims-soft-invalid", hasHint);
        input.setAttribute("aria-invalid", hasHint ? "true" : "false");
        feedback.textContent = message || "";
    }

    function validateField(input) {
        const ruleKey = input.dataset.limsSoftFormat;
        const rule = RULES[ruleKey];
        if (!rule) {
            return "";
        }

        const message = rule.test(input.value);
        setSoftHint(input, message);
        return message;
    }

    function bindForm(form) {
        const fields = [...form.querySelectorAll("[data-lims-soft-format]")];
        if (fields.length === 0) {
            return;
        }

        for (const input of fields) {
            input.addEventListener("input", () => validateField(input));
            input.addEventListener("blur", () => validateField(input));
            if (normalizeText(input.value)) {
                validateField(input);
            }
        }

        form.addEventListener("submit", () => {
            for (const input of fields) {
                validateField(input);
            }
        });
    }

    function init() {
        document.querySelectorAll("form[data-lims-soft-validate]").forEach(bindForm);
    }

    window.LimsCustomerFormatHints = {
        bindForm,
        validateField
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
