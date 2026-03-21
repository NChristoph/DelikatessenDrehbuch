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
    }
};
