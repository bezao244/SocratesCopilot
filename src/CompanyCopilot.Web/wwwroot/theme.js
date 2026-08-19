(function () {
    const defaultTheme = "light";
    const root = document.documentElement;

    function normalizeTheme(value) {
        return value === "dark" ? "dark" : "light";
    }

    function resolveStorageKey() {
        const visitorId = root.getAttribute("data-theme-visitor-id");
        const normalizedVisitorId = typeof visitorId === "string" && visitorId.length > 0
            ? visitorId
            : "anonymous";
        return `theme_preference_${normalizedVisitorId}`;
    }

    function readTheme() {
        const storageKey = resolveStorageKey();
        try {
            return window.localStorage.getItem(storageKey);
        } catch (error) {
            console.warn("Nao foi possivel ler preferencia de tema no localStorage.", error);
            return null;
        }
    }

    function persistTheme(theme) {
        const storageKey = resolveStorageKey();
        try {
            window.localStorage.setItem(storageKey, theme);
        } catch (error) {
            console.warn("Nao foi possivel persistir preferencia de tema no localStorage.", error);
        }
    }

    function syncControls(theme) {
        document.querySelectorAll("[data-theme-select]").forEach((element) => {
            if (element instanceof HTMLSelectElement && element.value !== theme) {
                element.value = theme;
            }
        });
    }

    function bindControls() {
        document.querySelectorAll("[data-theme-select]").forEach((element) => {
            if (!(element instanceof HTMLSelectElement) || element.dataset.themeBound === "true") {
                return;
            }

            element.addEventListener("change", (event) => {
                if (!(event.currentTarget instanceof HTMLSelectElement)) {
                    return;
                }

                const normalized = applyTheme(event.currentTarget.value);
                persistTheme(normalized);
            });

            element.dataset.themeBound = "true";
        });
    }

    function applyTheme(theme) {
        const normalized = normalizeTheme(theme);
        const storageKey = resolveStorageKey();
        if (root.getAttribute("data-theme") !== normalized) {
            root.setAttribute("data-theme", normalized);
        }
        if (root.getAttribute("data-theme-storage-key") !== storageKey) {
            root.setAttribute("data-theme-storage-key", storageKey);
        }
        syncControls(normalized);
        return normalized;
    }

    function restoreTheme() {
        const savedTheme = readTheme();
        const normalized = applyTheme(savedTheme ?? defaultTheme);
        if (savedTheme !== normalized) {
            persistTheme(normalized);
        }
        return normalized;
    }

    restoreTheme();

    window.themePreference = {
        getTheme: function () {
            return normalizeTheme(root.getAttribute("data-theme") ?? defaultTheme);
        },
        setTheme: function (theme) {
            const normalized = applyTheme(theme);
            persistTheme(normalized);
            return normalized;
        },
        getStorageKey: function () {
            return resolveStorageKey();
        },
        sync: function () {
            syncControls(this.getTheme());
        }
    };

    let syncScheduled = false;
    const scheduleRefresh = function () {
        if (syncScheduled) {
            return;
        }

        syncScheduled = true;
        window.requestAnimationFrame(function () {
            syncScheduled = false;
            restoreTheme();
            bindControls();
            window.themePreference.sync();
        });
    };

    const refreshAfterNavigation = function () {
        scheduleRefresh();
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            refreshAfterNavigation();
        }, { once: true });
    } else {
        refreshAfterNavigation();
    }

    document.addEventListener("enhancedload", refreshAfterNavigation);

    const htmlAttributeObserver = new MutationObserver(refreshAfterNavigation);
    htmlAttributeObserver.observe(root, {
        attributes: true,
        attributeFilter: ["data-theme", "data-theme-visitor-id"]
    });

    const bindBodyObserver = function () {
        if (!document.body) {
            return;
        }

        const bodyObserver = new MutationObserver(refreshAfterNavigation);
        bodyObserver.observe(document.body, { childList: true, subtree: true });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", bindBodyObserver, { once: true });
    } else {
        bindBodyObserver();
    }
})();
