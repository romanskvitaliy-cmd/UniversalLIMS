(() => {
    const storageKey = "lims.workspace.sendConfirm.enabled";

    const isEnabled = () => {
        const value = localStorage.getItem(storageKey);
        if (value === null) {
            return true;
        }

        return value === "1";
    };

    const setEnabled = (enabled) => {
        localStorage.setItem(storageKey, enabled ? "1" : "0");
    };

    const bindToggle = (inputEl) => {
        if (!inputEl) {
            return;
        }

        inputEl.checked = isEnabled();
        inputEl.addEventListener("change", () => {
            setEnabled(inputEl.checked);
        });
    };

    const confirmSend = (message) => {
        if (!isEnabled()) {
            return true;
        }

        return window.confirm(message);
    };

    window.LimsSendConfirm = {
        storageKey,
        isEnabled,
        setEnabled,
        bindToggle,
        confirmSend
    };
})();
