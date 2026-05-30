(() => {
    const host = document.querySelector("[data-lims-route-notify=\"1\"]");
    if (!host) {
        return;
    }

    const activeRole = String(host.dataset.limsActiveRole || "").toLowerCase();
    const pollConfigByRole = {
        laboratorytechnician: {
            endpoint: "/api/laboratory/notifications/incoming",
            journalNavSelector: 'a.nav-link[href*="/Laboratory"]',
            journalUrlFallback: "/Laboratory",
            singleTitle: "Нова проба від реєстратури",
            batchTitle: "Нові проби від реєстратури",
            batchMessageSuffix: "проб надійшло в лабораторію",
            voiceSinglePrefix: "Нова проба",
            voiceBatchPrefix: "нові проби в лабораторії"
        },
        specialist: {
            endpoint: "/api/expert/notifications/incoming",
            journalNavSelector: 'a.nav-link[href*="/Expert"]',
            journalUrlFallback: "/Expert",
            singleTitle: "Нова проба від лабораторії",
            batchTitle: "Нові проби від лабораторії",
            batchMessageSuffix: "проб готові до розгляду експертом",
            voiceSinglePrefix: "Нова проба для експертизи",
            voiceBatchPrefix: "нові проби для експертизи"
        },
        registrar: {
            endpoint: "/api/registration/notifications/ready-for-pickup",
            journalNavSelector: 'a.nav-link[href*="/Issuance"]',
            journalUrlFallback: "/Issuance",
            singleTitle: "Готово до видачі клієнту",
            batchTitle: "Готові до видачі",
            batchMessageSuffix: "проб очікують видачі",
            voiceSinglePrefix: "Готово до видачі",
            voiceBatchPrefix: "проб готові до видачі"
        },
        systemadministrator: {
            endpoint: "/api/laboratory/notifications/incoming",
            journalNavSelector: 'a.nav-link[href*="/Laboratory"]',
            journalUrlFallback: "/Laboratory",
            singleTitle: "Нова проба від реєстратури",
            batchTitle: "Нові проби від реєстратури",
            batchMessageSuffix: "проб надійшло в лабораторію",
            voiceSinglePrefix: "Нова проба",
            voiceBatchPrefix: "нові проби в лабораторії"
        }
    };

    const pollConfig = pollConfigByRole[activeRole] || null;
    const shouldPoll = pollConfig !== null;

    const storageKeys = {
        visual: "lims.routeNotify.visual",
        voice: "lims.routeNotify.voice",
        lastSeenUtc: `lims.routeNotify.lastSeenUtc.${activeRole || "default"}`,
        seenIds: `lims.routeNotify.seenIds.${activeRole || "default"}`
    };

    const pollIntervalMs = 45000;
    const initialLookbackMs = 24 * 60 * 60 * 1000;
    const maxSeenIds = 120;
    const toastContainerId = "limsRouteNotifyToasts";

    const readBool = (key, defaultValue) => {
        const value = localStorage.getItem(key);
        if (value === null) {
            return defaultValue;
        }
        return value === "1";
    };

    const writeBool = (key, enabled) => {
        localStorage.setItem(key, enabled ? "1" : "0");
    };

    let visualEnabled = readBool(storageKeys.visual, true);
    let voiceEnabled = readBool(storageKeys.voice, false);
    let pollTimer = null;
    let pollInFlight = false;

    const getToastContainer = () => {
        let container = document.getElementById(toastContainerId);
        if (!container) {
            container = document.createElement("div");
            container.id = toastContainerId;
            container.className = "toast-container position-fixed bottom-0 end-0 p-3 lims-route-notify-toasts";
            container.setAttribute("aria-live", "polite");
            container.setAttribute("aria-atomic", "false");
            document.body.appendChild(container);
        }
        return container;
    };

    const speak = (text) => {
        if (!voiceEnabled || !window.speechSynthesis || !text) {
            return;
        }

        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = "uk-UA";
        utterance.rate = 1.05;
        utterance.pitch = 1;

        const voices = window.speechSynthesis.getVoices();
        const ukrainianVoice = voices.find((voice) => voice.lang.toLowerCase().startsWith("uk"));
        if (ukrainianVoice) {
            utterance.voice = ukrainianVoice;
        }

        window.speechSynthesis.speak(utterance);
    };

    const showToast = ({ title, message, linkHref, variant = "success" }) => {
        if (!visualEnabled) {
            return;
        }

        const container = getToastContainer();
        const toastEl = document.createElement("div");
        toastEl.className = `toast lims-route-notify-toast align-items-center text-bg-${variant} border-0`;
        toastEl.setAttribute("role", "alert");
        toastEl.setAttribute("aria-live", "assertive");
        toastEl.setAttribute("aria-atomic", "true");

        const bodyHtml = linkHref
            ? `<a class="lims-route-notify-toast__link stretched-link" href="${linkHref}">${message}</a>`
            : `<span>${message}</span>`;

        toastEl.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    <div class="lims-route-notify-toast__title">${title}</div>
                    ${bodyHtml}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Закрити"></button>
            </div>`;

        container.appendChild(toastEl);
        const toast = window.bootstrap?.Toast
            ? new window.bootstrap.Toast(toastEl, { delay: 7000, autohide: true })
            : null;
        toast?.show();
        toastEl.addEventListener("hidden.bs.toast", () => toastEl.remove());
    };

    const loadSeenIds = () => {
        try {
            const raw = sessionStorage.getItem(storageKeys.seenIds);
            const parsed = raw ? JSON.parse(raw) : [];
            return Array.isArray(parsed) ? parsed.map(String) : [];
        } catch {
            return [];
        }
    };

    const saveSeenIds = (ids) => {
        sessionStorage.setItem(storageKeys.seenIds, JSON.stringify(ids.slice(-maxSeenIds)));
    };

    const getLastSeenUtc = () => {
        const raw = localStorage.getItem(storageKeys.lastSeenUtc);
        if (raw) {
            const parsed = Date.parse(raw);
            if (Number.isFinite(parsed)) {
                return new Date(parsed).toISOString();
            }
        }

        // First poll for this role/browser: look back instead of "now" (C6).
        // Otherwise samples routed before the first page open are never notified.
        return new Date(Date.now() - initialLookbackMs).toISOString();
    };

    const setLastSeenUtc = (isoValue) => {
        if (isoValue) {
            localStorage.setItem(storageKeys.lastSeenUtc, isoValue);
        }
    };

    const updateJournalBadge = (count) => {
        if (!pollConfig) {
            return;
        }

        const navLink = document.querySelector(pollConfig.journalNavSelector);
        if (!navLink) {
            return;
        }

        let badge = navLink.querySelector(".lims-route-notify-badge");
        if (count <= 0) {
            badge?.remove();
            return;
        }

        if (!badge) {
            badge = document.createElement("span");
            badge.className = "lims-route-notify-badge";
            badge.setAttribute("aria-label", "Нові проби");
            navLink.appendChild(badge);
        }

        badge.textContent = count > 9 ? "9+" : String(count);
    };

    const notifyIncomingBatch = (items, config) => {
        const effectiveConfig = config || pollConfig;
        if (!items.length || !effectiveConfig) {
            return;
        }

        const journalUrl = document.querySelector(effectiveConfig.journalNavSelector)?.getAttribute("href")
            || effectiveConfig.journalUrlFallback;
        const count = items.length;
        const variant = effectiveConfig.toastVariant || "primary";

        if (count === 1) {
            const item = items[0];
            const message = `${item.sampleNumber} · ${item.customerFullName}`;
            showToast({
                title: effectiveConfig.singleTitle,
                message,
                linkHref: journalUrl,
                variant
            });
            speak(`${effectiveConfig.voiceSinglePrefix} ${item.sampleNumber}`);
        } else {
            showToast({
                title: effectiveConfig.batchTitle,
                message: `${count} ${effectiveConfig.batchMessageSuffix}`,
                linkHref: journalUrl,
                variant
            });
            speak(`${count} ${effectiveConfig.voiceBatchPrefix}`);
        }

        if (!config || config === pollConfig) {
            updateJournalBadge(count);
        }
    };

    const laboratoryReworkNotifyConfig = {
        journalNavSelector: 'a.nav-link[href*="/Laboratory"]',
        journalUrlFallback: "/Laboratory",
        singleTitle: "Повернення на доопрацювання",
        batchTitle: "Повернення з експертизи",
        batchMessageSuffix: "проб повернуто в лабораторію",
        voiceSinglePrefix: "Повернення на доопрацювання",
        voiceBatchPrefix: "проб на доопрацюванні",
        toastVariant: "warning"
    };

    const pollIncoming = async () => {
        if (!shouldPoll || pollInFlight || document.hidden || !pollConfig) {
            return;
        }

        pollInFlight = true;
        try {
            const since = encodeURIComponent(getLastSeenUtc());
            const response = await fetch(`${pollConfig.endpoint}?since=${since}`, {
                headers: { Accept: "application/json" },
                credentials: "same-origin"
            });

            if (!response.ok) {
                return;
            }

            const payload = await response.json();
            const serverTimeUtc = payload?.serverTimeUtc;
            const items = Array.isArray(payload?.items) ? payload.items : [];
            const reworkItems = Array.isArray(payload?.reworkItems) ? payload.reworkItems : [];
            const seenIds = new Set(loadSeenIds());
            const freshItems = items.filter((item) => item?.sampleId && !seenIds.has(String(item.sampleId)));

            if (freshItems.length > 0) {
                freshItems.forEach((item) => seenIds.add(String(item.sampleId)));
                saveSeenIds([...seenIds]);
                notifyIncomingBatch(freshItems);
            }

            if (reworkItems.length > 0
                && (activeRole === "laboratorytechnician" || activeRole === "systemadministrator")) {
                const freshRework = reworkItems.filter((item) => {
                    if (!item?.sampleId) {
                        return false;
                    }
                    const key = `rework:${item.sampleId}`;
                    return !seenIds.has(key);
                });
                if (freshRework.length > 0) {
                    freshRework.forEach((item) => seenIds.add(`rework:${item.sampleId}`));
                    saveSeenIds([...seenIds]);
                    notifyIncomingBatch(freshRework, laboratoryReworkNotifyConfig);
                }
            }

            if (serverTimeUtc) {
                setLastSeenUtc(serverTimeUtc);
            }
        } catch {
            // Quiet network failures during background polling.
        } finally {
            pollInFlight = false;
        }
    };

    const startPolling = () => {
        if (!shouldPoll) {
            return;
        }

        window.clearInterval(pollTimer);
        pollTimer = window.setInterval(pollIncoming, pollIntervalMs);
        pollIncoming();
    };

    const stopPolling = () => {
        window.clearInterval(pollTimer);
        pollTimer = null;
    };

    const syncToggleUi = () => {
        const visualToggle = document.getElementById("limsRouteNotifyVisual");
        const voiceToggle = document.getElementById("limsRouteNotifyVoice");
        const trigger = document.querySelector("[data-lims-route-notify-trigger]");

        if (visualToggle) {
            visualToggle.checked = visualEnabled;
        }
        if (voiceToggle) {
            voiceToggle.checked = voiceEnabled;
        }
        if (trigger) {
            const anyEnabled = visualEnabled || voiceEnabled;
            trigger.classList.toggle("is-active", anyEnabled && shouldPoll);
            trigger.title = anyEnabled
                ? "Сповіщення про нові проби увімкнено"
                : "Сповіщення про нові проби вимкнено";
        }
    };

    document.getElementById("limsRouteNotifyVisual")?.addEventListener("change", (event) => {
        visualEnabled = event.target.checked;
        writeBool(storageKeys.visual, visualEnabled);
        syncToggleUi();
    });

    document.getElementById("limsRouteNotifyVoice")?.addEventListener("change", (event) => {
        voiceEnabled = event.target.checked;
        writeBool(storageKeys.voice, voiceEnabled);
        syncToggleUi();
        if (voiceEnabled) {
            speak("Голосові сповіщення увімкнено");
        }
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) {
            stopPolling();
            return;
        }
        startPolling();
    });

    const flashSuccess = host.dataset.limsFlashSuccess;
    if (flashSuccess) {
        showToast({
            title: "Реєстратура",
            message: flashSuccess,
            variant: "success"
        });
        if (voiceEnabled) {
            speak("Відправлено в лабораторію");
        }
        delete host.dataset.limsFlashSuccess;
    }

    syncToggleUi();
    if (shouldPoll) {
        startPolling();
    }

    if (window.speechSynthesis) {
        window.speechSynthesis.getVoices();
        window.speechSynthesis.addEventListener("voiceschanged", () => {
            window.speechSynthesis.getVoices();
        });
    }
})();
