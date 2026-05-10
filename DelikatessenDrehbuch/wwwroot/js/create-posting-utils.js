window.CreatePostingUtils = {
    escapeHtml: function (value) {
        return (value ?? "")
            .toString()
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    },
    resolveLangKey: function (value) {
        var normalized = (value || "de").toString().toLowerCase();
        if (normalized === "es") return "esp";
        if (normalized === "pt") return "prt";
        if (normalized === "se") return "sv";
        if (normalized === "dk") return "da";
        return normalized;
    },
    getDataUrl: function (key, options) {
        var dataUrls = (window.CreatePostingDataUrls || {});
        var baseUrl = dataUrls[key] || "";

        // Support language-specific master_steps
        if (key === 'masterSteps' && options && options.language) {
            var lang = this.resolveLangKey(options.language);
            var url = "/data/master_steps." + lang + ".json";
            console.log("[CreatePostingUtils] Loading masterSteps for language:", options.language, "->", lang, "URL:", url);
            return url;
        }

        return baseUrl;
    },
    fetchJson: function (key, options) {
        var url = this.getDataUrl(key, options);
        if (!url) {
            return Promise.reject(new Error("Missing CreatePosting data URL for key: " + key));
        }
        var fetchOptions = options && options.fetchOptions ? options.fetchOptions : {};
        return fetch(url, fetchOptions).then(function (response) {
            if (!response.ok) {
                throw new Error("Failed to load JSON from " + url + " (" + response.status + ")");
            }
            return response.json();
        });
    },
    showTransientMessage: function (message, options) {
        var settings = options || {};
        var selector = settings.selector || "#creatorToast";
        var duration = typeof settings.duration === "number" ? settings.duration : 2200;
        var className = settings.className || "show";
        var fallbackPrefix = settings.fallbackPrefix || "CreatePosting";
        var element = document.querySelector(selector);

        if (!element) {
            console.warn("[" + fallbackPrefix + "] " + (message || ""));
            return;
        }

        element.textContent = message || "";
        element.classList.add(className);

        if (element._hideTimer) {
            window.clearTimeout(element._hideTimer);
        }

        element._hideTimer = window.setTimeout(function () {
            element.classList.remove(className);
            element._hideTimer = null;
        }, duration);
    },
    reportError: function (message, error, options) {
        var settings = options || {};
        var prefix = settings.prefix || "CreatePosting";
        var fallbackMessage = settings.fallbackMessage || "Es ist ein Fehler aufgetreten.";
        var details = error && error.message ? error.message : "";
        var fullMessage = message || fallbackMessage;

        console.error("[" + prefix + "] " + fullMessage, error);
        this.showTransientMessage(fullMessage + (details ? ": " + details : ""), {
            selector: settings.selector || "#creatorToast",
            duration: settings.duration || 3600,
            fallbackPrefix: prefix
        });
    }
};
