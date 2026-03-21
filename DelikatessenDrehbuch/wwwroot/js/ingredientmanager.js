(() => {
    const fallbackUnits = [
        { de: "Stk.", en: "pcs.", esp: "uds.", prt: "un." },
        { de: "EL.", en: "tbsp.", esp: "cda.", prt: "colh. sopa" },
        { de: "TL.", en: "tsp.", esp: "cdta.", prt: "colh. cha" },
        { de: "Pkg.", en: "pack", esp: "paq.", prt: "pac." },
        { de: "Prise", en: "pinch", esp: "pizca", prt: "pitada" },
        { de: "g.", en: "g.", esp: "g.", prt: "g." },
        { de: "Kg", en: "kg", esp: "kg", prt: "kg" },
        { de: "Bund", en: "bunch", esp: "manojo", prt: "molho" },
        { de: "Dose", en: "can", esp: "lata", prt: "lata" },
        { de: "etwas", en: "some", esp: "algo", prt: "um pouco" },
        { de: "spritzer", en: "dash", esp: "chorrito", prt: "golpe" },
        { de: "Becher", en: "cup", esp: "taza", prt: "copo" },
        { de: "Scheiben", en: "slices", esp: "rebanadas", prt: "fatias" },
        { de: "Blatt", en: "leaf", esp: "hoja", prt: "folha" },
        { de: "Blätter", en: "leaves", esp: "hojas", prt: "folhas" },
        { de: "Handvoll", en: "handful", esp: "puñado", prt: "punhado" },
        { de: "cm", en: "cm", esp: "cm", prt: "cm" },
        { de: "Zweig", en: "sprig", esp: "rama", prt: "ramo" },
        { de: "ml", en: "ml", esp: "ml", prt: "ml" },
        { de: "Gekocht", en: "cooked", esp: "cocido", prt: "cozido" },
        { de: "Portionen", en: "portions", esp: "porciones", prt: "porcoes" },
        { de: "l", en: "l", esp: "l", prt: "l" },
        { de: "Glas", en: "glass", esp: "vaso", prt: "copo" },
        { de: "kcal", en: "kcal", esp: "kcal", prt: "kcal" },
        { de: "mg", en: "mg", esp: "mg", prt: "mg" },
        { de: "µg", en: "ug", esp: "ug", prt: "ug" }
    ];

    let cachedUnits = null;

    var resolveLangKey = window.CreatePostingUtils.resolveLangKey;
    var escapeHtml = window.CreatePostingUtils.escapeHtml;

    function normalizeUnit(unit) {
        const de = (unit?.de || "").toString().trim();
        if (!de) return null;

        return {
            de,
            en: (unit?.en || de).toString(),
            esp: (unit?.esp || unit?.es || de).toString(),
            prt: (unit?.prt || unit?.pt || de).toString(),
            id: (unit?.id || de).toString(),
            ms: (unit?.ms || de).toString(),
            nl: (unit?.nl || de).toString(),
            sv: (unit?.sv || de).toString(),
            da: (unit?.da || de).toString(),
            no: (unit?.no || de).toString()
        };
    }

    function buildUnitMap() {
        const map = new Map();

        fallbackUnits.forEach((unit) => {
            const normalized = normalizeUnit(unit);
            if (!normalized) return;
            map.set(normalized.de.toLowerCase(), normalized);
        });

        document.querySelectorAll(".js-db-unit option").forEach((opt) => {
            const value = (opt.value || "").toString().trim();
            const label = (opt.textContent || value).toString().trim();
            if (!value) return;

            const key = value.toLowerCase();
            const current = map.get(key);
            if (current) {
                current.en = current.en || label;
                current.esp = current.esp || label;
                current.prt = current.prt || label;
                return;
            }

            map.set(key, normalizeUnit({ de: value, en: label, esp: label, prt: label }));
        });

        return map;
    }

    function collectUnitsFromDom() {
        return Array.from(buildUnitMap().values()).filter(Boolean);
    }

    function getUnits() {
        if (!cachedUnits || !cachedUnits.length) {
            cachedUnits = collectUnitsFromDom();
        }
        return cachedUnits;
    }

    function refreshUnits() {
        cachedUnits = collectUnitsFromDom();
        return cachedUnits;
    }

    function findUnitByDe(unitDe) {
        const needle = (unitDe || "").toString().trim().toLowerCase();
        if (!needle) return null;
        return getUnits().find((unit) => (unit?.de || "").toString().trim().toLowerCase() === needle) || null;
    }

    function getDefaultUnitDe() {
        return getUnits()[0]?.de || "g.";
    }

    function getUnitLabel(unitObj, langKey) {
        if (!unitObj || typeof unitObj !== "object") return "";
        const lang = resolveLangKey(langKey || "de");
        return (unitObj[lang] || unitObj.de || "").toString();
    }

    function buildUnitOptionsHtml(selectedDe, langKey) {
        const selectedValue = (selectedDe || "").toString().trim();
        const selectedKey = selectedValue.toLowerCase();
        const units = refreshUnits();

        const hasSelected = selectedKey && units.some((unit) => (unit?.de || "").toString().trim().toLowerCase() === selectedKey);
        if (selectedValue && !hasSelected) {
            units.push(normalizeUnit({ de: selectedValue }));
        }

        return units.map((unit) => {
            const de = (unit?.de || "").toString();
            const label = getUnitLabel(unit, langKey) || de;
            const isSelected = de.toLowerCase() === selectedKey;
            return `<option value="${escapeHtml(de)}" ${isSelected ? "selected" : ""}>${escapeHtml(label)}</option>`;
        }).join("");
    }

    window.IngredientManager = {
        getUnits,
        refreshUnits,
        findUnitByDe,
        getDefaultUnitDe,
        getUnitLabel,
        buildUnitOptionsHtml
    };

    document.addEventListener("DOMContentLoaded", () => {
        refreshUnits();
    });
})();
