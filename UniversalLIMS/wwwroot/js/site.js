document.addEventListener("DOMContentLoaded", () => {
    const toggle = document.querySelector("[data-calendar-toggle]");
    const overlay = document.querySelector("[data-calendar-overlay]");
    const grid = document.querySelector("[data-calendar-grid]");
    const monthEl = document.querySelector("[data-calendar-month]");
    const yearEl = document.querySelector("[data-calendar-year]");
    if (!toggle || !overlay || !grid || !monthEl || !yearEl) {
        return;
    }

    // Keep the overlay outside local stacking contexts so cards/headers never cover it.
    if (overlay.parentElement !== document.body) {
        document.body.appendChild(overlay);
    }

    const storageKey = "lims.workspace.calendarOverlay.enabled";
    const monthNames = [
        "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень",
        "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень"
    ];

    const now = new Date();
    const year = now.getFullYear();
    const month = now.getMonth();
    monthEl.textContent = monthNames[month];
    yearEl.textContent = String(year);

    renderCalendarGrid(now, grid);

    const setEnabled = (enabled) => {
        overlay.hidden = !enabled;
        overlay.setAttribute("aria-hidden", enabled ? "false" : "true");
        toggle.setAttribute("aria-pressed", enabled ? "true" : "false");
        toggle.classList.toggle("is-active", enabled);
        toggle.title = enabled ? "Вимкнути календар-фон" : "Увімкнути календар-фон";
        localStorage.setItem(storageKey, enabled ? "1" : "0");
    };

    const saved = localStorage.getItem(storageKey);
    setEnabled(saved === "1");

    toggle.addEventListener("click", () => {
        const enabled = toggle.getAttribute("aria-pressed") !== "true";
        setEnabled(enabled);
    });
});

function renderCalendarGrid(now, host) {
    host.innerHTML = "";
    const year = now.getFullYear();
    const month = now.getMonth();
    const firstDay = new Date(year, month, 1);
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const startOffset = ((firstDay.getDay() + 6) % 7); // Monday-first
    const daysInPrevMonth = new Date(year, month, 0).getDate();

    for (let i = 0; i < startOffset; i++) {
        const el = document.createElement("span");
        el.className = "lims-workspace-calendar-day is-muted";
        el.textContent = String(daysInPrevMonth - startOffset + i + 1);
        host.appendChild(el);
    }

    for (let d = 1; d <= daysInMonth; d++) {
        const el = document.createElement("span");
        el.className = "lims-workspace-calendar-day";
        el.textContent = String(d);
        if (d === now.getDate()) {
            el.classList.add("is-today");
        }
        host.appendChild(el);
    }
}
