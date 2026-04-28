// WorldMiniApp Client-Side Localization Bridge
// Lädt lokalisierte Strings vom Server und stellt sie JavaScript zur Verfügung

(function () {
    if (window.WorldMiniAppI18n) return;

    let strings = {};
    let currentLang = 'de';

    // Get current language from ASP.NET Culture Cookie
    function getLang() {
        const cultureCookie = document.cookie
            .split('; ')
            .find(row => row.startsWith('.AspNetCore.Culture='));

        if (cultureCookie) {
            const match = cultureCookie.match(/c=([a-z]{2})/);
            if (match) return match[1];
        }

        // Fallback zu deli-lang Cookie
        const deliCookie = document.cookie
            .split('; ')
            .find(row => row.startsWith('deli-lang='));

        if (deliCookie) {
            const lang = decodeURIComponent(deliCookie.split('=')[1]);
            return normalizeLang(lang);
        }

        return 'de';
    }

    // Normalize legacy cookie codes
    function normalizeLang(lang) {
        const normalized = (lang || 'de').toLowerCase();
        if (normalized === 'esp') return 'es';
        if (normalized === 'prt') return 'pt';
        if (normalized === 'no') return 'nb';
        return normalized;
    }

    // Load strings from server endpoint
    async function loadStrings() {
        try {
            const response = await fetch(`/WorldMiniApp/Home/GetLocalizedStrings?culture=${currentLang}`);
            if (!response.ok) throw new Error('Failed to load localized strings');
            strings = await response.json();
        } catch (error) {
            console.error('WorldMiniAppI18n: Failed to load strings', error);
            // Fallback to empty object, keys will be returned as-is
            strings = {};
        }
    }

    // Translate a key with optional placeholders
    // Usage: t('Toast.RecipeAdded', 'Lasagne')
    function t(key, ...args) {
        let text = strings[key] || key;

        // Replace {0}, {1}, etc. with arguments
        args.forEach((arg, index) => {
            text = text.replace(`{${index}}`, arg);
        });

        return text;
    }

    // Public API
    window.WorldMiniAppI18n = {
        t,
        getLang,
        normalizeLang,
        ready: async () => {
            currentLang = getLang();
            await loadStrings();
        },
        // Legacy alias for compatibility with existing code
        resolveLangKey: normalizeLang
    };
})();
