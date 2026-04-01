// master-steps-ui.js
// Erwartet:
// - /data/master_steps.json (wwwroot/data/master_steps.json)
// - <div id="insertContainer"></div>  (Liste der Step-Buttons)
// - <div id="MasterText"></div>       (Aktueller Step + Inline-Editor darunter)
// Optional:
// - <select id="LangSelect"></select>

(() => {
    const DEFAULT_LANG = "de";
    const VISIBLE_RANKED_OPTIONS = 3;

    // -----------------------------
    // STATE
    // -----------------------------
    let doc = null;
    let variableCatalog = { variables: {} };
    let steps = [];
    let currentLang = DEFAULT_LANG;

    // active step
    let activeStep = null; // { master_id, title, templateRaw, values:{} }
    let activeToken = null; // { varName, tokenId }

    // Filter state
    let currentSearchQuery = '';
    let currentPhaseFilter = 'all';

    // -----------------------------
    // DOM
    // -----------------------------
    const $ = (s) => document.querySelector(s);
    const insertContainer = () => $("#sc2MasterTemplateCards");
    const masterText = () => $("#MasterText");
    const langSelect = () => $("#LangSelect");

    // -----------------------------
    // UTIL
    // -----------------------------
    var escapeHtml = window.CreatePostingUtils.escapeHtml;

    function encodeAttr(str) {
        return encodeURIComponent(str ?? "");
    }
    function decodeAttr(str) {
        try { return decodeURIComponent(str ?? ""); } catch { return str ?? ""; }
    }

    function uid(prefix = "id") {
        return `${prefix}_${Math.random().toString(16).slice(2)}_${Date.now()}`;
    }

    // Mehrsprachige Anzeigenamen für Platzhalter-Variablen
    const VAR_DISPLAY_NAMES = {
        ingredient:  { de: "Zutat",        en: "ingredient",   esp: "ingrediente",  prt: "ingrediente",  nl: "ingrediënt",   sv: "ingrediens",   da: "ingrediens",   no: "ingrediens",   id: "bahan",      ms: "bahan"     },
        ingredient2: { de: "Zutat 2",      en: "ingredient 2", esp: "ingrediente 2",prt: "ingrediente 2",nl: "ingrediënt 2", sv: "ingrediens 2", da: "ingrediens 2", no: "ingrediens 2", id: "bahan 2",    ms: "bahan 2"   },
        ingredients: { de: "Zutaten",      en: "ingredients",  esp: "ingredientes", prt: "ingredientes", nl: "ingrediënten", sv: "ingredienser", da: "ingredienser", no: "ingredienser", id: "bahan-bahan",ms: "bahan-bahan"},
        state:       { de: "Zustand",      en: "state",        esp: "estado",       prt: "estado",       nl: "toestand",     sv: "tillstånd",    da: "tilstand",     no: "tilstand",     id: "keadaan",    ms: "keadaan"   },
        equipment:   { de: "Gerät",        en: "equipment",    esp: "equipo",       prt: "equipamento",  nl: "apparaat",     sv: "redskap",      da: "redskab",      no: "redskap",      id: "peralatan",  ms: "peralatan" },
        tool:        { de: "Werkzeug",     en: "tool",         esp: "herramienta",  prt: "ferramenta",   nl: "gereedschap",  sv: "verktyg",      da: "værktøj",      no: "verktøy",      id: "alat",       ms: "alat"      },
        duration:    { de: "Dauer",        en: "duration",     esp: "duración",     prt: "duração",      nl: "duur",         sv: "tid",          da: "varighed",     no: "varighet",     id: "durasi",     ms: "tempoh"    },
        temp:        { de: "Temperatur",   en: "temperature",  esp: "temperatura",  prt: "temperatura",  nl: "temperatuur",  sv: "temperatur",   da: "temperatur",   no: "temperatur",   id: "suhu",       ms: "suhu"      },
        shape:       { de: "Form",         en: "shape",        esp: "forma",        prt: "forma",        nl: "vorm",         sv: "form",         da: "form",         no: "form",         id: "bentuk",     ms: "bentuk"    },
        grind_size:  { de: "Größe",        en: "size",         esp: "tamaño",       prt: "tamanho",      nl: "grootte",      sv: "storlek",      da: "størrelse",    no: "størrelse",    id: "ukuran",     ms: "saiz"      },
        pronoun:     { de: "Pronomen",     en: "pronoun",      esp: "pronombre",    prt: "pronome",      nl: "voornaamwoord",sv: "pronomen",     da: "pronomen",     no: "pronomen",     id: "kata ganti", ms: "kata ganti"},
        pronoun2:    { de: "Pronomen 2",   en: "pronoun 2",    esp: "pronombre 2",  prt: "pronome 2",    nl: "voornaamwoord 2",sv: "pronomen 2", da: "pronomen 2", no: "pronomen 2", id: "kata ganti 2", ms: "kata ganti 2"},
        action:      { de: "Aktion",       en: "action",       esp: "acción",       prt: "ação",         nl: "actie",        sv: "åtgärd",       da: "handling",     no: "handling",     id: "tindakan",   ms: "tindakan"  },
        liquid:      { de: "Flüssigkeit",  en: "liquid",       esp: "líquido",      prt: "líquido",      nl: "vloeistof",    sv: "vätska",       da: "væske",        no: "væske",        id: "cairan",     ms: "cecair"    },
        fat:         { de: "Fett",         en: "fat",          esp: "grasa",        prt: "gordura",      nl: "vet",          sv: "fett",         da: "fedt",         no: "fett",         id: "lemak",      ms: "lemak"     },
        base:        { de: "Basis",        en: "base",         esp: "base",         prt: "base",         nl: "basis",        sv: "bas",          da: "base",         no: "base",         id: "dasar",      ms: "asas"      },
        marinade:    { de: "Marinade",     en: "marinade",     esp: "marinada",     prt: "marinada",     nl: "marinade",     sv: "marinad",      da: "marinade",     no: "marinade",     id: "bumbu rendam", ms: "perapan" },
        method:      { de: "Methode",      en: "method",       esp: "método",       prt: "método",       nl: "methode",      sv: "metod",        da: "metode",       no: "metode",       id: "metode",     ms: "kaedah"    },
        finish:      { de: "Abschluss",    en: "finish",       esp: "acabado",      prt: "acabamento",   nl: "afwerking",    sv: "finish",       da: "finish",       no: "finish",       id: "akhiran",    ms: "kemasan"   },
        seasonings:  { de: "Gewürze",      en: "seasonings",   esp: "condimentos",  prt: "temperos",     nl: "kruiden",      sv: "kryddor",      da: "krydderier",   no: "krydder",      id: "bumbu",      ms: "perisa"    },
        thickener:   { de: "Bindemittel",  en: "thickener",    esp: "espesante",    prt: "espessante",   nl: "bindmiddel",   sv: "förtjockningsmedel", da: "fortykningsmiddel", no: "fortykningsmiddel", id: "pengental", ms: "pemekat" },
        count:       { de: "Anzahl",       en: "count",        esp: "cantidad",     prt: "quantidade",   nl: "aantal",       sv: "antal",        da: "antal",        no: "antall",       id: "jumlah",     ms: "bilangan"  },
        mode:        { de: "Modus",        en: "mode",         esp: "modo",         prt: "modo",         nl: "modus",        sv: "läge",         da: "tilstand",     no: "modus",        id: "mode",       ms: "mod"       },
        components:  { de: "Komponenten",  en: "components",   esp: "componentes",  prt: "componentes",  nl: "componenten",  sv: "komponenter",  da: "komponenter",  no: "komponenter",  id: "komponen",   ms: "komponen"  },
        dough:       { de: "Teig",         en: "dough",        esp: "masa",         prt: "massa",        nl: "deeg",         sv: "deg",          da: "dej",          no: "deig",         id: "adonan",     ms: "doh"       },
        surface:     { de: "Oberfläche",   en: "surface",      esp: "superficie",   prt: "superfície",   nl: "oppervlak",    sv: "yta",          da: "overflade",    no: "overflate",    id: "permukaan",  ms: "permukaan" },
        heat:        { de: "Hitze",        en: "heat",         esp: "calor",        prt: "calor",        nl: "hitte",        sv: "värme",        da: "varme",        no: "varme",        id: "panas",      ms: "panas"     },
        extra:       { de: "Extra",        en: "extra",        esp: "extra",        prt: "extra",        nl: "extra",        sv: "extra",        da: "ekstra",       no: "ekstra",       id: "ekstra",     ms: "ekstra"    },
        item:        { de: "Element",      en: "item",         esp: "elemento",     prt: "elemento",     nl: "element",      sv: "objekt",       da: "element",      no: "element",      id: "item",       ms: "item"      },
        position:    { de: "Position",     en: "position",     esp: "posición",     prt: "posição",      nl: "positie",      sv: "position",     da: "position",     no: "posisjon",     id: "posisi",     ms: "posisi"    },
        reason:      { de: "Grund",        en: "reason",       esp: "razón",        prt: "razão",        nl: "reden",        sv: "anledning",    da: "grund",        no: "grunn",        id: "alasan",     ms: "sebab"     },
        goal:        { de: "Ziel",         en: "goal",         esp: "objetivo",     prt: "objetivo",     nl: "doel",         sv: "mål",          da: "mål",          no: "mål",          id: "tujuan",     ms: "matlamat"  },
        balance:     { de: "Balance",      en: "balance",      esp: "equilibrio",   prt: "equilíbrio",   nl: "balans",       sv: "balans",       da: "balance",      no: "balanse",      id: "keseimbangan",ms: "keseimbangan"},
        keep:        { de: "Beibehalten",  en: "keep",         esp: "mantener",     prt: "manter",       nl: "bewaren",      sv: "behåll",       da: "behold",       no: "behold",       id: "simpan",     ms: "simpan"    }
    };

    function getVarDisplayName(varName) {
        const key = (varName || "").toString().trim();
        const entry = VAR_DISPLAY_NAMES[key] || VAR_DISPLAY_NAMES[key.toLowerCase()];
        if (!entry) return key;
        return entry[currentLang] ?? entry[DEFAULT_LANG] ?? entry.de ?? key;
    }

    // -----------------------------
    // FRACTION HELPERS
    // -----------------------------
    function getFractionOptions() {
        const frDef = variableCatalog?.variables?.ingredient_fractions;
        if (!frDef || !Array.isArray(frDef.options)) return [];
        const lang = currentLang || DEFAULT_LANG;
        return frDef.options.map(opt => ({
            key: opt.key,
            numerator: opt.numerator,
            denominator: opt.denominator,
            label: (opt.labels && (opt.labels[lang] || opt.labels[DEFAULT_LANG] || opt.labels.de)) || opt.key,
            remainderLabel: opt.remainder_labels
                ? (opt.remainder_labels[lang] || opt.remainder_labels[DEFAULT_LANG] || opt.remainder_labels.de || "")
                : ""
        }));
    }

    function composeFractionText(fractionDef, ingredientName, article) {
        if (!fractionDef || !ingredientName) return ingredientName || "";

        // Convert nominative articles to genitive for fraction constructions
        // "die Hälfte DES Zuckers" (not "die Hälfte der Zucker")
        let genitiveArticle = article;
        if (article && article !== "ohne") {
            const lowerArticle = article.toLowerCase();

            // Check if article is already genitive - DON'T convert!
            const alreadyGenitive = ["des", "eines"].includes(lowerArticle);

            if (!alreadyGenitive) {
                const genitiveMap = {
                    "die": "der",  // feminine nominative → genitive
                    "das": "des",  // neuter nominative → genitive
                    "den": "des",  // masculine accusative → genitive
                    "dem": "des",  // masculine/neuter dative → genitive
                    "einen": "eines",  // indefinite masculine accusative → genitive
                    "einem": "eines",  // indefinite masculine/neuter dative → genitive
                    // NOTE: "der" can be:
                    //   - masculine nominative → should become "des"
                    //   - feminine genitive → should stay "der"
                    // We assume if user selects "der" with fraction, they mean genitive (feminine)
                    // So we DON'T convert "der" anymore!
                };
                genitiveArticle = genitiveMap[lowerArticle] || article;
            }
        }

        const art = (genitiveArticle && genitiveArticle !== "ohne") ? ` ${genitiveArticle}` : "";
        return `${fractionDef.label}${art} ${ingredientName}`.trim();
    }

    function findMatchingFractionOption(numerator, denominator, fractionOptions) {
        if (!numerator || !denominator) return null;
        const opts = fractionOptions || getFractionOptions();
        return opts.find(o => o.numerator * denominator === numerator * o.denominator) || null;
    }

    function getRemainderChipsFromAcceptedSteps() {
        const stepRows = document.querySelectorAll('#selectedSteps .step-row[data-ingredient-fractions]');
        if (!stepRows.length) return { remainderChips: [], fullyUsedNames: [] };

        const usageMap = {};
        stepRows.forEach(row => {
            let frData;
            try { frData = JSON.parse(row.dataset.ingredientFractions || "null"); } catch { return; }
            if (!frData) return;

            // Handle both old format (single object) and new format (array)
            const fractionItems = Array.isArray(frData) ? frData : (frData.ingredientName ? [frData] : []);

            fractionItems.forEach(item => {
                if (!item || !item.ingredientName) return;
                const key = item.ingredientName.toLowerCase();
                if (!usageMap[key]) usageMap[key] = { name: item.ingredientName, used: 0 };
                usageMap[key].used += (item.numerator || 0) / (item.denominator || 1);
            });
        });

        const frOpts = getFractionOptions();
        const remainderChips = [];
        const fullyUsedNames = [];

        Object.values(usageMap).forEach(entry => {
            const remaining = 1 - entry.used;
            if (remaining <= 0.001) {
                fullyUsedNames.push(entry.name.toLowerCase());
                return;
            }
            const matchedFraction = findMatchingFractionOption(
                Math.round(remaining * 12), 12, frOpts
            );
            if (matchedFraction) {
                remainderChips.push({
                    name: composeFractionText(matchedFraction, entry.name),
                    icon: "",
                    isRemainder: true,
                    originalName: entry.name,
                    remainingNumerator: matchedFraction.numerator,
                    remainingDenominator: matchedFraction.denominator
                });
            } else {
                const fallback = frOpts.find(o => o.remainderLabel);
                const label = fallback ? fallback.remainderLabel : "rest";
                remainderChips.push({
                    name: `${label} ${entry.name}`.trim(),
                    icon: "",
                    isRemainder: true,
                    originalName: entry.name,
                    remainingNumerator: Math.round(remaining * 12),
                    remainingDenominator: 12
                });
            }
        });

        return { remainderChips, fullyUsedNames };
    }

    function renderFractionPickerHtml(fractionOptions, useClassBasedIds = false) {
        const opts = fractionOptions || getFractionOptions();
        if (!opts.length) return "";
        const wholeBtnLabel = { de: "Ganzes", en: "Whole", esp: "Entero", prt: "Inteiro", id: "Seluruh", nl: "Geheel", sv: "Hel", da: "Hel", no: "Hel", ms: "Keseluruhan" };
        const wholeLabel = wholeBtnLabel[currentLang] || wholeBtnLabel[DEFAULT_LANG] || "Ganzes";

        // Add js-fraction-picker-row class for Probability Area
        const rowClass = useClassBasedIds ? "fraction-picker-row js-fraction-picker-row d-flex flex-wrap gap-2 mb-2" : "fraction-picker-row d-flex flex-wrap gap-2 mb-2";
        const rowId = useClassBasedIds ? "" : ' id="FractionPickerRow"';

        let html = `<div class="small text-muted mt-2 mb-1">Menge</div><div class="${rowClass}"${rowId}>`;
        html += `<button type="button" class="fraction-pick active" data-pick-mode="fraction" data-fraction-key="" data-fraction-num="0" data-fraction-den="1">${escapeHtml(wholeLabel)}</button>`;
        opts.forEach(o => {
            html += `<button type="button" class="fraction-pick" data-pick-mode="fraction" data-fraction-key="${escapeHtml(o.key)}" data-fraction-num="${o.numerator}" data-fraction-den="${o.denominator}">${escapeHtml(o.key)} ${escapeHtml(o.label)}</button>`;
        });
        html += `</div>`;
        return html;
    }

    // -----------------------------
    // LOAD
    // -----------------------------
    async function loadJson() {
        return await window.CreatePostingDataStore.load("masterSteps");
    }

    async function loadVariableCatalog() {
        try {
            const data = await window.CreatePostingDataStore.load("masterStepVariables");
            return data && typeof data === "object" ? data : { variables: {} };
        } catch {
            return { variables: {} };
        }
    }

    // -----------------------------
    // OPTION LOOKUP (NUR JSON!)
    // -----------------------------
    function normalizeVarKey(value) {
        const raw = (value || "").toString().trim();
        const normalized = raw.toLowerCase().replace(/[_\-\s]+/g, "");
        const aliasMap = { pronomen: "pronoun" };
        return {
            key: aliasMap[normalized] || raw,
            normalizedKey: (aliasMap[normalized] || raw).toString().toLowerCase().replace(/[_\-\s]+/g, "")
        };
    }

    function normalizeOptionValue(value) {
        return (value || "").toString().trim().toLowerCase()
            .replace(/[\u{1F300}-\u{1F9FF}\u{2600}-\u{27BF}\u{FE00}-\u{FE0F}\u{200D}\u{20E3}\u{E0020}-\u{E007F}]/gu, "")
            .trim();
    }

    function isSemanticTag(tag) {
        const value = (tag || "").toString().trim().toLowerCase();
        return !!value && !value.startsWith("variable:") && !value.startsWith("value:");
    }

    function getStepById(masterId) {
        if (!masterId) return null;
        return steps.find(s => (s?.master_id || "") === masterId) || null;
    }

    function getStepSelectionTags(masterId) {
        const step = getStepById(masterId);
        const tags = Array.isArray(step?.selection_tags) ? [...step.selection_tags] : [];
        const explicitTags = [];
        if (Array.isArray(step?.tags)) {
            explicitTags.push(...step.tags);
        }
        if (step?.Tag) {
            explicitTags.push(step.Tag);
        }
        explicitTags
            .map(tag => (tag || "").toString().trim())
            .filter(Boolean)
            .forEach(tag => {
                if (!tags.includes(tag)) tags.push(tag);
            });
        return tags;
    }

    function getComparableTags(rawTags) {
        const variants = new Set();
        (Array.isArray(rawTags) ? rawTags : []).forEach(rawTag => {
            const tag = (rawTag || "").toString().trim().toLowerCase();
            if (!tag) return;
            variants.add(tag);
            const parts = tag.split(":").filter(Boolean);
            if (parts.length > 1) {
                variants.add(parts[parts.length - 1]);
            }
        });
        return variants;
    }

    function getVariableCatalogEntry(varName) {
        const { key, normalizedKey } = normalizeVarKey(varName);
        const variables = variableCatalog && variableCatalog.variables;
        if (!variables || typeof variables !== "object") return null;

        if (variables[key]) return variables[key];

        const match = Object.entries(variables)
            .find(([candidateKey]) => normalizeVarKey(candidateKey).normalizedKey === normalizedKey);
        return match ? match[1] : null;
    }

    function getLocalizedOptionLabel(option) {
        if (!option || !option.labels || typeof option.labels !== "object") return "";
        const langKey = (currentLang || DEFAULT_LANG || "de").toLowerCase();
        return option.labels[currentLang]
            ?? option.labels[langKey]
            ?? option.labels[DEFAULT_LANG]
            ?? option.labels.de
            ?? option.labels.en
            ?? "";
    }

    function getCatalogOptionEntries(varName) {
        const entry = getVariableCatalogEntry(varName);
        const options = entry && Array.isArray(entry.options) ? entry.options : [];
        return options
            .map(option => ({
                value: (getLocalizedOptionLabel(option) || "").toString().trim(),
                tags: Array.isArray(option?.tags) ? option.tags : []
            }))
            .filter(option => option.value);
    }

    function filterOptionEntriesByStepTags(optionEntries, masterId) {
        const entries = Array.isArray(optionEntries) ? optionEntries : [];
        if (!entries.length || !masterId) return entries;

        const stepTags = getComparableTags(getStepSelectionTags(masterId).filter(isSemanticTag));
        if (!stepTags.size) return entries;

        const taggedEntries = entries.filter(option => Array.isArray(option.tags) && option.tags.some(isSemanticTag));
        if (!taggedEntries.length) return entries;

        const matches = entries.filter(option =>
            Array.isArray(option.tags) &&
            option.tags.some(tag => {
                if (!isSemanticTag(tag)) return false;
                const comparable = getComparableTags([tag]);
                return Array.from(comparable).some(value => stepTags.has(value));
            })
        );

        return matches.length ? matches : entries;
    }

    function getStepAction(masterId) {
        const step = getStepById(masterId);
        return step ? (step.action || "").toString().trim() : "";
    }

    function getOptionRulesContext(varName, masterId, context) {
        const ctx = context || {};
        const action = ctx.action || getStepAction(masterId);
        const ingredientFamily = ctx.ingredientFamily || "";
        const lang = currentLang || DEFAULT_LANG;
        const renderer = window.MasterStepRenderer;
        const actionRules = renderer && renderer.getOptionRulesForAction
            ? renderer.getOptionRulesForAction(action, varName, lang) : { preferred: [], blocked: [] };
        const familyRules = renderer && renderer.getOptionRulesForFamily
            ? renderer.getOptionRulesForFamily(ingredientFamily, varName, lang) : { preferred: [], blocked: [] };
        const stepRules = renderer && renderer.getOptionRulesForStep
            ? renderer.getOptionRulesForStep(masterId, varName, lang) : { preferred: [], blocked: [] };
        return { action, ingredientFamily, actionRules, familyRules, stepRules };
    }

    function buildCurrentScoringContext() {
        const masterId = (activeStep?.master_id || "").toString().trim();
        const action = getStepAction(masterId);
        // Try to detect ingredient family from selected ingredients
        const ingredientFamily = detectIngredientFamily();
        return { action, ingredientFamily };
    }

    function detectIngredientFamily() {
        const items = getSelectedIngredientsFromPage();
        if (!items.length) return "";
        const names = items.map(x => (x.name || "").toString().toLowerCase());

        const eggTerms = ["eier", "eggs", "eigelb", "eiweiß", "yolk"];
        const eggExact = ["ei", "egg"];
        const meatTerms = ["fleisch", "meat", "rind", "beef", "schwein", "pork", "lamm", "lamb", "huhn", "hähn", "chicken", "kalb", "veal", "ente", "duck", "wildschwein", "wildfleisch", "hirsch", "reh", "venison", "hack", "steak", "filet", "schnitzel", "braten", "gulasch", "schinken", "speck", "bacon", "wurst", "sausage"];
        const fishTerms = ["fisch", "fish", "lachs", "salmon", "thunfisch", "tuna", "garnele", "shrimp", "forelle", "trout", "kabeljau", "cod", "pangasius", "dorade", "zander", "hering", "sardine", "muschel", "tintenfisch", "calamari", "krebs", "crab", "hummer", "lobster"];
        const vegTerms = ["zwiebel", "onion", "karotte", "carrot", "tomate", "tomato", "paprika", "pepper", "brokkoli", "broccoli", "zucchini", "aubergine", "sellerie", "celery", "lauch", "leek", "knoblauch", "garlic", "salat", "gemüse", "spinat", "spinach", "kürbis", "pumpkin", "bohne", "bean", "erbse", "pea", "mais", "corn", "gurke", "cucumber", "radieschen", "radish", "blumenkohl", "cauliflower", "rosenkohl", "kohlrabi", "fenchel", "pilz", "mushroom", "champignon"];
        const doughTerms = ["teig", "dough", "mehl", "flour", "hefe", "yeast"];
        const liquidTerms = ["brühe", "broth", "stock", "milch", "milk", "sahne", "cream", "wasser", "water", "wein", "wine", "saft", "juice", "buttermilch", "kokosmilch"];
        const fatTerms = ["öl", "oil", "schmalz", "lard", "margarine", "ghee"];
        const fatExact = ["butter", "fett"];
        const seasoningTerms = ["salz", "salt", "pfeffer", "pepper", "gewürz", "spice", "zucker", "sugar", "zimt", "cinnamon", "paprikapulver", "kümmel", "muskat", "oregano", "thymian", "rosmarin", "basilikum", "curry", "kurkuma"];

        const hasAny = (terms) => names.some(n => terms.some(t => n.includes(t)));
        const hasExact = (terms) => names.some(n => terms.includes(n));

        if (hasAny(eggTerms) || hasExact(eggExact)) return "egg";
        if (hasAny(meatTerms)) return "meat";
        if (hasAny(fishTerms)) return "fish";
        if (hasAny(doughTerms)) return "dough";
        if (hasAny(liquidTerms)) return "liquid";
        if (hasAny(fatTerms) || hasExact(fatExact)) return "fat";
        if (hasAny(seasoningTerms)) return "seasoning";
        if (hasAny(vegTerms)) return "vegetable";
        return "";
    }

    function getRankedVarOptionEntries(varName, contextMasterId, context) {
        if (!doc) return [];

        const { key, normalizedKey } = normalizeVarKey(varName);
        let optionEntries = getCatalogOptionEntries(key);
        if (!optionEntries.length && normalizedKey === "equipment" && doc.equipment) {
            optionEntries = Object.values(doc.equipment)
                .map(value => ({ value: (value || "").toString().trim(), tags: [] }))
                .filter(option => option.value);
        }

        // SPECIAL: Always include "Würfel" for "shape" variable (most commonly used)
        if (normalizedKey === "shape" && optionEntries.length > 0) {
            const hasWuerfel = optionEntries.some(opt =>
                (opt.value || "").toLowerCase().includes("würfel") ||
                (opt.value || "").toLowerCase().includes("wuerfel")
            );
            if (!hasWuerfel) {
                optionEntries.unshift({ value: "Würfel", tags: [] });
            }
        }

        const uniqueEntries = [];
        const seen = new Set();
        optionEntries.forEach((entry, index) => {
            const value = (entry && entry.value ? entry.value : "").toString().trim();
            const normalizedValue = normalizeOptionValue(value);
            if (!normalizedValue || seen.has(normalizedValue)) return;
            seen.add(normalizedValue);
            uniqueEntries.push({
                value,
                tags: Array.isArray(entry?.tags) ? entry.tags : [],
                originalIndex: index
            });
        });

        const masterId = (contextMasterId || activeStep?.master_id || "").toString().trim();
        const stepTags = getComparableTags(getStepSelectionTags(masterId).filter(isSemanticTag));
        const hasSemanticStepTags = stepTags.size > 0;

        // Load option rules context for multi-level scoring
        const rulesCtx = getOptionRulesContext(key, masterId, context);
        const actionPreferred = new Set((rulesCtx.actionRules.preferred || []).map(normalizeOptionValue));
        const actionBlocked = new Set((rulesCtx.actionRules.blocked || []).map(normalizeOptionValue));
        const familyPreferred = new Set((rulesCtx.familyRules.preferred || []).map(normalizeOptionValue));
        const familyBlocked = new Set((rulesCtx.familyRules.blocked || []).map(normalizeOptionValue));
        const stepPreferred = new Set((rulesCtx.stepRules.preferred || []).map(normalizeOptionValue));
        const stepBlocked = new Set((rulesCtx.stepRules.blocked || []).map(normalizeOptionValue));

        const ranked = uniqueEntries
            .map(entry => {
                const semanticTags = (entry.tags || []).filter(isSemanticTag);
                const matchedTags = semanticTags.filter(tag => {
                    const comparable = getComparableTags([tag]);
                    return Array.from(comparable).some(value => stepTags.has(value));
                });
                let score = 0;
                const normalizedValue = normalizeOptionValue(entry.value);

                // --- Tag Match: +30 per semantic tag match ---
                if (matchedTags.length) {
                    score += matchedTags.length * 30;
                } else if (hasSemanticStepTags && semanticTags.length) {
                    score -= 15;
                }

                if (!semanticTags.length) {
                    score += 5;
                }

                // --- Step-specific rules (highest priority): +60 preferred, -120 blocked ---
                if (stepPreferred.has(normalizedValue)) {
                    score += 60;
                }
                if (stepBlocked.has(normalizedValue)) {
                    score -= 120;
                }

                // --- Action rules: +50 preferred, -100 blocked ---
                if (actionPreferred.has(normalizedValue)) {
                    score += 50;
                }
                if (actionBlocked.has(normalizedValue)) {
                    score -= 100;
                }

                // --- Ingredient Family rules: +40 preferred, -80 blocked ---
                if (familyPreferred.has(normalizedValue)) {
                    score += 40;
                }
                if (familyBlocked.has(normalizedValue)) {
                    score -= 80;
                }

                // --- Position decay ---
                score -= entry.originalIndex * 0.01;

                return {
                    value: entry.value,
                    score,
                    matchedTagsCount: matchedTags.length,
                    originalIndex: entry.originalIndex
                };
            })
            .sort((a, b) => {
                if (b.score !== a.score) return b.score - a.score;
                if (b.matchedTagsCount !== a.matchedTagsCount) return b.matchedTagsCount - a.matchedTagsCount;
                return a.originalIndex - b.originalIndex;
            });

        const finalRanked = ranked;

        if (normalizedKey === "shape") {
            const blocked = new Set(["fein", "feine", "grob", "grobe"]);
            const filtered = finalRanked.filter(entry => {
                const normalized = normalizeOptionValue(entry.value);
                return normalized && !blocked.has(normalized);
            });
            const hasGehackt = filtered.some(entry => normalizeOptionValue(entry.value) === "gehackt");
            if (!hasGehackt) {
                filtered.unshift({
                    value: "gehackt",
                    score: Number.MAX_SAFE_INTEGER,
                    matchedTagsCount: 0,
                    originalIndex: -1
                });
            }
            return filtered;
        }

        return finalRanked;
    }

    // liefert ALLE Rohoptionen ohne Filterung (für "Weitere anzeigen" Fallback)
    function getRawVarOptions(varName, contextMasterId, context) {
        return getRankedVarOptionEntries(varName, contextMasterId, context).map(entry => entry.value);
    }

    // liefert Liste von Optionen für eine Variable (Buttons)
    function getVarOptions(varName, contextMasterId, context) {
        const entries = getRankedVarOptionEntries(varName, contextMasterId, context);
        // For pronouns, show ALL options directly (no limit)
        const isPronoun = (varName || '').toLowerCase() === 'pronoun';
        const limit = isPronoun ? entries.length : VISIBLE_RANKED_OPTIONS;
        const result = entries.slice(0, limit).map(entry => entry.value);
        return result;
    }
    function getArticleOptions() {
        const list = getCatalogOptionEntries("articles").map(option => option.value);
        const cleaned = Array.isArray(list) ? list : [];
        const filtered = cleaned.filter(x => (x ?? "").toString().trim().length > 0);

        // Ensure "des" is included
        if (!filtered.includes("des")) {
            filtered.push("des");
        }

        return ["ohne", ...filtered];
    }

    function getDurationUnits() {
        // duration_units: { minute:{de:"Minuten"...}, hour:{de:"Stunden"...}, per_package:{de:"laut Packungsanweisung"...}}
        const du = doc?.duration_units;
        if (!du) return [
            { key: "minute", label: "Minute(n)" },
            { key: "hour", label: "Stunde(n)" },
            { key: "short", label: "kurz" },
            { key: "per_package", label: "laut Packungsanweisung" }
        ];

        const minuteLabel = du.minute?.[currentLang] ?? du.minute?.de ?? "Minuten";
        const hourLabel = du.hour?.[currentLang] ?? du.hour?.de ?? "Stunden";
        const shortLabel = du.short?.[currentLang] ?? du.short?.de ?? "kurz";
        const perPackageLabel = du.per_package?.[currentLang] ?? du.per_package?.de ?? "laut Packungsanweisung";
        return [
            { key: "minute", label: minuteLabel },
            { key: "hour", label: hourLabel },
            { key: "short", label: shortLabel },
            { key: "per_package", label: perPackageLabel }
        ];
    }

    // -----------------------------
    // TEMPLATE RENDER (klickbare Tokens)
    // -----------------------------
        function getTemplateVariables(templateFragment) {
        const found = [];
        (templateFragment || "").replace(/{{\s*([^}]+?)\s*}}/g, (_m, varRaw) => {
            const varName = (varRaw ?? "").trim();
            if (varName && !found.includes(varName)) found.push(varName);
            return _m;
        });
        return found;
    }

    function resolveOptionalTemplateSegments(templateRaw, values) {
        let output = (templateRaw || "").toString();
        const optionalPattern = /\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]/g;
        let previous = null;

        while (output !== previous) {
            previous = output;
            output = output.replace(optionalPattern, (_match, parenInner, bracketInner) => {
                const inner = (parenInner ?? bracketInner ?? "").toString();
                const varsInGroup = getTemplateVariables(inner);
                if (!varsInGroup.length) return inner;

                const hasAnyValue = varsInGroup.some(varName => ((values?.[varName] ?? "").toString().trim().length > 0));
                return hasAnyValue ? inner : "";
            });
        }

        return output;
    }

    function buildOptionalSegmentButtons(templateRaw, stepId, values) {
        const source = (templateRaw || "").toString();
        const optionalPattern = /\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]/g;
        const seen = new Set();
        const buttons = [];
        let match;

        while ((match = optionalPattern.exec(source)) !== null) {
            const inner = (match[1] ?? match[2] ?? "").toString();
            const varsInGroup = getTemplateVariables(inner);
            if (!varsInGroup.length) continue;

            const hasAnyValue = varsInGroup.some(varName => ((values?.[varName] ?? "").toString().trim().length > 0));
            if (hasAnyValue) continue;

            const firstVar = varsInGroup[0];
            const dedupeKey = `${stepId}__${varsInGroup.join("__")}`;
            if (seen.has(dedupeKey)) continue;
            seen.add(dedupeKey);

            const label = varsInGroup.map(v => getVarDisplayName(v)).join(" / ");
            const tokenId = `${stepId}_optional_${varsInGroup.join("_")}`;
            buttons.push(`
        <button type="button"
                class="btn btn-sm btn-outline-secondary js-optional-var-add"
                data-optional-var="${escapeHtml(firstVar)}"
                data-token-id="${escapeHtml(tokenId)}">
          + ${escapeHtml(label)}
        </button>
      `);
        }

        return buttons.join("");
    }

    function renderTemplateTokens(templateRaw, stepId, values) {
        let index = 0;

        return (templateRaw || "").replace(/{{\s*([^}]+?)\s*}}/g, (_m, varRaw) => {
            const varName = (varRaw ?? "").trim();
            const tokenId = `${stepId}_${varName}_${index++}`;
            const val = (values[varName] ?? "").toString();
            const display = val.trim().length > 0 ? val : varName;

            // Show Plus button for ingredient variables when value is set
            const varKey = varName.toLowerCase();
            const isIngredient = varKey === "ingredient" || varKey === "ingredient2" || varKey === "ingredients" ||
                                 varKey === "liquid" || varKey === "fat" || varKey === "seasonings" ||
                                 varKey === "marinade" || varKey === "thickener" || varKey === "components" || varKey === "extra";

            // ✅ ENTFERNT (2026-03-28): Plus-Button nicht mehr nötig - alles wird im Overlay bearbeitet!
            // Vorher: showPlusBtn für ingredient-Variablen mit Wert
            // console.log(`[renderTemplateTokens] varName=${varName}, isIngredient=${isIngredient}, val="${val}", hasValue=${hasValue}, showPlusBtn=${showPlusBtn}`);

            return `
      <span class="token-highlight placeholder-token template-var"
            draggable="false"
            data-var="${escapeHtml(varName)}"
            data-token-id="${escapeHtml(tokenId)}"
            data-has-value="${val.trim().length > 0 ? "1" : "0"}">
        ${escapeHtml(display)}
      </span>
      <button type="button"
              class="placeholder-reset"
              data-token-id="${escapeHtml(tokenId)}"
              data-var="${escapeHtml(varName)}"
              title="Zurücksetzen"
              aria-label="Zurücksetzen">
        <i class="bi bi-arrow-counterclockwise" aria-hidden="true"></i>
      </button>
    `;
        });
    }

    // Renders optional [{{var}}] segments as clickable inline pills (if unfilled)
    // or as their inner content with tokens (if any var is filled).
    // Required {{var}} tokens are rendered as the usual token-highlight spans.
    function renderTemplate(templateRaw, stepId, values) {
        if (!templateRaw) return "";
        values = values || {};

        const optionalPattern = /\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]/g;
        const seen = new Set();
        // Sentinel chars that won't appear in normal template text
        const PILL_START = "\x01";
        const PILL_SEP   = "\x02";
        const PILL_END   = "\x03";

        // Pass 1: replace optional segments
        let processed = templateRaw.replace(optionalPattern, (fullMatch, parenInner, bracketInner) => {
            const inner = (parenInner ?? bracketInner ?? "").toString();
            const varsInGroup = getTemplateVariables(inner);
            if (!varsInGroup.length) return inner;

            const hasAnyValue = varsInGroup.some(v => ((values?.[v] ?? "").toString().trim().length > 0));
            if (hasAnyValue) return inner; // filled → render content normally

            const dedupeKey = `${stepId}__${varsInGroup.join("__")}`;
            if (seen.has(dedupeKey)) return "";
            seen.add(dedupeKey);

            const firstVar   = varsInGroup[0];
            const tokenId    = `${stepId}_optional_${varsInGroup.join("_")}`;
            const label      = inner.replace(/{{\s*([^}]+?)\s*}}/g, (_m, v) => getVarDisplayName(v.trim()));
            // Encode as sentinel-delimited marker (safe from {{}} regex)
            return `${PILL_START}${firstVar}${PILL_SEP}${tokenId}${PILL_SEP}${label}${PILL_END}`;
        });

        // Pass 2: render regular {{var}} tokens
        let tokenIndex = 0;
        processed = processed.replace(/{{\s*([^}]+?)\s*}}/g, (_m, varRaw, offset) => {
            const varName = (varRaw ?? "").trim();
            const tid     = `${stepId}_${varName}_${tokenIndex++}`;
            const val     = (values[varName] ?? "").toString();
            // Capitalize value when token is at sentence start
            const isAtSentenceStart = offset === 0 || /^[\s]*$/.test(processed.slice(0, offset)) || /[.!?]\s*$/.test(processed.slice(0, offset));
            let display = val.trim().length > 0 ? val : getVarDisplayName(varName);
            if (isAtSentenceStart && display.length > 0) {
                display = display.charAt(0).toUpperCase() + display.slice(1);
            }

            // Check if this is an ingredient variable
            const varKey = varName.toLowerCase();
            const isIngredient = varKey === "ingredient" || varKey === "ingredient2" || varKey === "ingredients" ||
                                 varKey === "liquid" || varKey === "fat" || varKey === "seasonings" ||
                                 varKey === "marinade" || varKey === "thickener" || varKey === "components" || varKey === "extra";

            // ✅ ENTFERNT (2026-03-28): Plus-Button nicht mehr nötig - alles wird im Overlay bearbeitet!
            // Vorher: showPlusBtn für ingredient-Variablen mit Wert
            // const showPlusBtn = isIngredient && hasValue;
            // const plusButton = showPlusBtn ? `<button ...>+</button>` : "";

            return `<span class="token-highlight placeholder-token template-var" draggable="false" data-var="${escapeHtml(varName)}" data-token-id="${escapeHtml(tid)}" data-has-value="${val.trim().length > 0 ? "1" : "0"}" data-sentence-start="${isAtSentenceStart ? "1" : "0"}">${escapeHtml(display)}</span><button type="button" class="placeholder-reset" data-token-id="${escapeHtml(tid)}" data-var="${escapeHtml(varName)}" title="Zurücksetzen" aria-label="Zurücksetzen"><i class="bi bi-arrow-counterclockwise" aria-hidden="true"></i></button>`;
        });

        // Pass 3: replace pill markers with inline pill HTML
        processed = processed.replace(
            new RegExp(`\\x01([^\\x02]*)\\x02([^\\x02]*)\\x02([^\\x03]*)\\x03`, "g"),
            (_m, firstVar, tokenId, label) =>
                `<span class="optional-inline-pill js-optional-var-add" role="button" tabindex="0" data-optional-var="${escapeHtml(firstVar)}" data-token-id="${escapeHtml(tokenId)}" title="Optional hinzufügen: ${escapeHtml(label)}"><i class="bi bi-plus-circle-dotted" aria-hidden="true"></i><span class="optional-pill-label">${escapeHtml(label)}</span></span>`
        );

        return processed;
    }


    function renderTemplateWithConfig(templateRaw, stepId, values, config) {
        if (!templateRaw) return "";
        values = values || {};
        config = config || {};

        const optionalPattern = /\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]/g;
        const seen = new Set();
        const optionalValues = config.optionalValues || values;
        const tokenClass = ["token-highlight placeholder-token template-var", config.tokenExtraClasses || ""].filter(Boolean).join(" ");
        const pillClass = ["optional-inline-pill", config.pillExtraClasses || ""].filter(Boolean).join(" ");
        const tokenWrapClass = (config.tokenWrapClass || "").toString().trim();
        const tokenVarKeyAttr = config.includeVarKey ? ` data-var-key="{{VAR_NAME}}"` : "";
        const pillVarKeyAttr = config.includeVarKey ? ` data-var-key="{{VAR_NAME}}"` : "";
        const optionalVarAttrName = (config.optionalVarAttrName || "data-optional-var").toString();
        const pillTitlePrefix = (config.pillTitlePrefix || "Optional").toString();
        const enablePlusButtons = config.enablePlusButtons || false;
        const multiIngredientsData = config.multiIngredientsData || {}; // {varName: [{name, fraction, article}, ...]}
        const PILL_START = "\x01";
        const PILL_SEP = "\x02";
        const PILL_END = "\x03";

        let processed = templateRaw.replace(optionalPattern, (_match, parenInner, bracketInner) => {
            const inner = (parenInner ?? bracketInner ?? "").toString();
            const varsInGroup = getTemplateVariables(inner);
            if (!varsInGroup.length) return inner;

            const hasAnyValue = varsInGroup.some(varName => ((optionalValues?.[varName] ?? "").toString().trim().length > 0));
            if (hasAnyValue) return inner;

            const dedupeKey = `${stepId}__${varsInGroup.join("__")}`;
            if (seen.has(dedupeKey)) return "";
            seen.add(dedupeKey);

            const firstVar = varsInGroup[0];
            const tokenId = `${stepId}_optional_${varsInGroup.join("_")}`;
            const label = inner.replace(/{{\s*([^}]+?)\s*}}/g, (_m, v) => getVarDisplayName(v.trim()));
            return `${PILL_START}${firstVar}${PILL_SEP}${tokenId}${PILL_SEP}${label}${PILL_END}`;
        });

        let tokenIndex = 0;
        processed = processed.replace(/{{\s*([^}]+?)\s*}}/g, (_match, varRaw, offset) => {
            const varName = (varRaw ?? "").trim();
            const tokenId = `${stepId}_${varName}_${tokenIndex++}`;
            const value = (values[varName] ?? "").toString();
            const isAtSentenceStart = offset === 0 || /^[\s]*$/.test(processed.slice(0, offset)) || /[.!?]\s*$/.test(processed.slice(0, offset));
            let display = value.trim().length > 0 ? value : getVarDisplayName(varName);
            if (isAtSentenceStart && display.length > 0) {
                display = display.charAt(0).toUpperCase() + display.slice(1);
            }
            const varKeyAttr = tokenVarKeyAttr.replaceAll("{{VAR_NAME}}", escapeHtml(varName));

            // ✅ ENTFERNT (2026-03-28): Plus-Button nicht mehr nötig - alles wird im Overlay bearbeitet!
            // Vorher: plusButtonHtml für ingredient-Variablen mit Wert
            // let plusButtonHtml = "";
            // if (enablePlusButtons && isIngredientVariable(varName) && value.trim().length > 0) {
            //     plusButtonHtml = `<button ...>+</button>`;
            // }

            const tokenHtml = `<span class="${tokenClass}" draggable="false" data-var="${escapeHtml(varName)}"${varKeyAttr} data-token-id="${escapeHtml(tokenId)}" data-has-value="${value.trim().length > 0 ? "1" : "0"}" data-sentence-start="${isAtSentenceStart ? "1" : "0"}">${escapeHtml(display)}</span><button type="button" class="placeholder-reset" data-token-id="${escapeHtml(tokenId)}" data-var="${escapeHtml(varName)}" title="Zurücksetzen" aria-label="Zurücksetzen"><i class="bi bi-arrow-counterclockwise" aria-hidden="true"></i></button>`;
            if (!tokenWrapClass) return tokenHtml;
            return `<span class="${escapeHtml(tokenWrapClass)}">${tokenHtml}</span>`;
        });

        processed = processed.replace(
            new RegExp(`\\x01([^\\x02]*)\\x02([^\\x02]*)\\x02([^\\x03]*)\\x03`, "g"),
            (_match, firstVar, tokenId, label) => {
                const varKeyAttr = pillVarKeyAttr.replaceAll("{{VAR_NAME}}", escapeHtml(firstVar));
                return `<span class="${pillClass}" role="button" tabindex="0" ${optionalVarAttrName}="${escapeHtml(firstVar)}"${varKeyAttr} data-token-id="${escapeHtml(tokenId)}" title="${escapeHtml(pillTitlePrefix)}: ${escapeHtml(label)}"><i class="bi bi-plus-circle-dotted" aria-hidden="true"></i><span class="optional-pill-label">${escapeHtml(label)}</span></span>`;
            }
        );

        return processed;
    }

    // Shared placeholder renderer for other creator UIs that need the same
    // clickable token/reset structure for a single template preview.
    function renderAssignedPlaceholderTemplate(templateRaw, values, assignments, config) {
        const template = (templateRaw || "").toString();
        if (!template) return "";

        const opts = config || {};
        const activeTokenId = (opts.activeTokenId || "").toString();
        const tokenKeyAttr = (opts.tokenKeyAttr || "data-placeholder-key").toString();
        const tokenIdAttr = (opts.tokenIdAttr || "data-placeholder-token-id").toString();
        const wrapAttr = (opts.wrapAttr || tokenIdAttr).toString();
        let placeholderIndex = 0;

        return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, function (_match, rawKey) {
            const key = (rawKey || "").toString().trim();
            const tokenId = `${key}__${placeholderIndex++}`;
            const fallback = values && values[key] != null ? String(values[key]).trim() : key;
            const assigned = assignments && assignments[tokenId] != null ? String(assignments[tokenId]).trim() : "";
            const value = assigned || fallback || key;
            const activeClass = activeTokenId === tokenId ? " token-active" : "";
            const safeKey = escapeHtml(key);
            const safeTokenId = escapeHtml(tokenId);
            const safeValue = escapeHtml(value);
            const safeFallback = escapeHtml(fallback || key);

            return `<span class="placeholder-wrap" ${wrapAttr}="${safeTokenId}">
                    <span class="token-highlight placeholder-token${activeClass}" draggable="false" ${tokenKeyAttr}="${safeKey}" ${tokenIdAttr}="${safeTokenId}">${safeValue}</span>
                    <button type="button" class="placeholder-reset" ${tokenIdAttr}="${safeTokenId}" data-default-value="${safeFallback}" title="Zurücksetzen">&#8630;</button>
                </span>`;
        });
    }

    function splitLeadingArticleByOptions(value, articleOptions) {
        const raw = (value || "").toString().trim();
        if (!raw) return { article: "", noun: "" };
        const options = Array.isArray(articleOptions) ? articleOptions : [];
        const lower = raw.toLowerCase();
        const found = options.find(option => {
            const candidate = (option || "").toString().trim();
            return candidate && lower.startsWith(`${candidate.toLowerCase()} `);
        });
        if (!found) return { article: "", noun: raw };
        return { article: found, noun: raw.substring(found.length).trim() };
    }

    function composeArticleAndNoun(article, noun) {
        const art = (article || "").toString().trim();
        const n = (noun || "").toString().trim();
        if (!n) return "";
        return art && art !== "ohne" ? `${art} ${n}` : n;
    }

    function getNounOptionsFromValues(options, articleOptions) {
        const nouns = (options || [])
            .map(option => splitLeadingArticleByOptions(option, articleOptions).noun)
            .filter(Boolean);
        return Array.from(new Set(nouns));
    }

    function splitEditorPrefillValue(rawValue, config) {
        const opts = config || {};
        const rawCurrentVal = (rawValue || "").toString().trim();
        const articleOptions = Array.isArray(opts.articleOptions) ? opts.articleOptions : [];
        const pronounOptions = Array.isArray(opts.pronounOptions) ? opts.pronounOptions : [];
        const stateVar = opts.stateVar === true;
        const ingredientVar = opts.ingredientVar === true;
        const noArticleVar = opts.noArticleVar === true;
        const suppressPronounButtons = opts.suppressPronounButtons === true;

        let prefilledArticle = "";
        let prefilledPronoun = "";
        let prefilledValue = rawCurrentVal;

        if (stateVar && !suppressPronounButtons && rawCurrentVal) {
            const pronounMatch = pronounOptions.find(option => {
                const value = (option || "").toString().trim();
                if (!value) return false;
                const lowerCurrent = rawCurrentVal.toLowerCase();
                const lowerOption = value.toLowerCase();
                return lowerCurrent === lowerOption || lowerCurrent.startsWith(`${lowerOption} `);
            });

            if (pronounMatch) {
                prefilledPronoun = pronounMatch;
                prefilledValue = rawCurrentVal.slice(pronounMatch.length).trim();
            }
        } else if (!ingredientVar && !noArticleVar && rawCurrentVal) {
            const parts = splitLeadingArticleByOptions(rawCurrentVal, articleOptions);
            prefilledArticle = parts.article || "";
            prefilledValue = parts.noun || rawCurrentVal;
        }

        return {
            article: prefilledArticle,
            pronoun: prefilledPronoun,
            value: prefilledValue
        };
    }

    // Renders a read-only template preview for the step-card list.
    // Optional [{{var}}] segments appear as small faded badges instead of raw [brackets].
    function renderSnippetTokens(text) {
        return (text || "").toString().replace(/{{\s*([^}]+?)\s*}}/g, (_match, varName) => {
            const label = escapeHtml(getVarDisplayName((varName || "").toString().trim()));
            return `<span class="template-snippet-var">${label}</span>`;
        }).split("\n").map(part => part).join("\n");
    }

    function snippetPreview(templateRaw) {
        if (!templateRaw) return "";
        const optionalPattern = /\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]/g;
        const parts = [];
        let lastIndex = 0;
        let match;
        while ((match = optionalPattern.exec(templateRaw)) !== null) {
            const textBefore = templateRaw.slice(lastIndex, match.index);
            parts.push(renderSnippetTokens(escapeHtml(textBefore)));
            const inner = (match[1] ?? match[2] ?? "").toString();
            const label = inner.replace(/{{\s*([^}]+?)\s*}}/g, (_m, v) => getVarDisplayName(v.trim()));
            parts.push(`<span class="opt-snippet-badge"><i class="bi bi-plus-circle-dotted" aria-hidden="true"></i> ${renderSnippetTokens(escapeHtml(label))}</span>`);
            lastIndex = match.index + match[0].length;
        }
        const remaining = templateRaw.slice(lastIndex);
        parts.push(renderSnippetTokens(escapeHtml(remaining)));
        return parts.join("");
    }


    function getPhaseMeta(phase) {
        const p = (phase ?? 0).toString();
        const meta = doc?.phases?.[p];
        const label =
            meta?.label?.[currentLang] ??
            meta?.label?.[DEFAULT_LANG] ??
            (typeof meta === "string" ? meta : p);

        const icon = meta?.icon ?? "?";
        return { label, icon };
    }

    function getSubGroupMeta(subGroupKey) {
        const meta = doc?.sub_groups?.[subGroupKey];
        if (!meta) return { label: subGroupKey, icon: "", description: "" };
        const label =
            meta?.label?.[currentLang] ??
            meta?.label?.[DEFAULT_LANG] ??
            subGroupKey;
        const icon = meta?.icon ?? "";
        const description =
            meta?.description?.[currentLang] ??
            meta?.description?.[DEFAULT_LANG] ??
            "";
        return { label, icon, description };
    }

    // -----------------------------
    // FILTER FUNCTIONS
    // -----------------------------
    function matchesSearch(step, query) {
        if (!query) return true;
        const q = query.toLowerCase();
        const searchText = [
            step.description || '',
            step.master_id || '',
            step.action || '',
            ...(step.selection_tags || [])
        ].join(' ').toLowerCase();
        return searchText.includes(q);
    }

    function filterSteps(stepsToFilter) {
        return stepsToFilter.filter(step => {
            // Phase filter
            if (currentPhaseFilter !== 'all') {
                const phaseNum = parseInt(currentPhaseFilter, 10);
                if ((step.phase ?? 0) !== phaseNum) {
                    return false;
                }
            }
            // Search filter
            if (!matchesSearch(step, currentSearchQuery)) {
                return false;
            }
            return true;
        });
    }

    function setSearchQuery(query) {
        currentSearchQuery = query || '';
        renderStepButtons();
    }

    function setPhaseFilter(phase) {
        currentPhaseFilter = phase || 'all';
        renderStepButtons();
    }

    function renderStepButtons() {
        const container = insertContainer();
        if (!container) return;

        container.innerHTML = "";

        // Apply filters first
        const filteredSteps = filterSteps(steps);

        // Show message if no results after filtering
        if (!filteredSteps.length && steps.length > 0) {
            container.innerHTML = '<div class="small text-white-50 text-center py-4">Keine Steps für diesen Filter gefunden.</div>';
            return;
        }

        // sortieren: phase -> sub_group -> master_id
        const sorted = [...filteredSteps].sort((a, b) => {
            const pa = a.phase ?? 0;
            const pb = b.phase ?? 0;
            if (pa !== pb) return pa - pb;
            const sga = a.sub_group ?? "";
            const sgb = b.sub_group ?? "";
            if (sga !== sgb) return sga.localeCompare(sgb);
            return (a.master_id ?? "").localeCompare(b.master_id ?? "");
        });

        // gruppieren: phase -> sub_group -> steps
        const byPhase = new Map();
        for (const step of sorted) {
            const p = (step.phase ?? 0).toString();
            if (!byPhase.has(p)) byPhase.set(p, new Map());
            const bySg = byPhase.get(p);
            const sg = step.sub_group ?? "_none";
            if (!bySg.has(sg)) bySg.set(sg, []);
            bySg.get(sg).push(step);
        }

        const theme = window.CreatePostingCurrentTheme || document.querySelector('.smart-step-creator')?.getAttribute('data-theme') || 'gold';

        // render pro Phase → Sub-Group
        for (const [phaseKey, subGroups] of byPhase.entries()) {
            const meta = getPhaseMeta(phaseKey);

            // Header für Phase
            container.insertAdjacentHTML("beforeend", `
              <div class="phase-header mt-3 mb-2">
                <div class="d-flex align-items-center gap-2">
                  <span class="phase-icon">${escapeHtml(meta.icon)}</span>
                  <span class="phase-title">${escapeHtml(meta.label)}</span>
                </div>
              </div>
            `);

            // Sub-Groups innerhalb der Phase
            for (const [sgKey, sgSteps] of subGroups.entries()) {
                const sgMeta = getSubGroupMeta(sgKey);

                // Sub-Group Header
                const sgDesc = sgMeta.description
                    ? `<div class="sub-group-desc">${escapeHtml(sgMeta.description)}</div>`
                    : "";
                container.insertAdjacentHTML("beforeend", `
                  <div class="sub-group-header mt-3 mb-1">
                    <span class="sub-group-icon">${escapeHtml(sgMeta.icon)}</span>
                    <span class="sub-group-title">${escapeHtml(sgMeta.label)}</span>
                    ${sgDesc}
                  </div>
                `);

                // Steps in Sub-Group
                sgSteps.forEach(step => {
                    const title = step.description ?? "";
                    const templateRaw = step.templates?.[currentLang] ?? "";

                    container.insertAdjacentHTML("beforeend", `
                      <button type="button"
                              class="template-card w-100 mb-2"
                              data-theme="${theme}"
                              data-step-id="${escapeHtml(step.master_id ?? "")}"
                              data-title="${escapeHtml(title)}"
                              data-template-raw="${encodeAttr(templateRaw)}">
                        <div class="template-title">
                          ${escapeHtml(title)}
                        </div>
                        <div class="template-snippet">
                          ${snippetPreview(templateRaw)}
                        </div>
                      </button>
                    `);
                });
            }
        }
    }

    // -----------------------------
    // RENDER: MasterText (aktueller Step + Editor Slot)
    // -----------------------------
        function renderMasterText() {
        const target = masterText();
        if (!target) return;

        if (!activeStep) {
            target.innerHTML = "Wähle eine Template.";
            return;
        }

        const rendered = renderTemplate(activeStep.templateRaw, activeStep.master_id, activeStep.values);

        // Editor placeholder (wird beim Token-Klick gefüllt)
        target.innerHTML = `
      <div class="current-step-wrap">
        <div class="current-step-header d-flex justify-content-between align-items-center">
          <div class="preview-step-title mb-0">AKTUELLER STEP</div>
          <button type="button" class="btn btn-sm creator-cta-primary" id="btnAcceptStep">Step akzeptieren</button>
        </div>

        <div class="preview-step-text mt-2" id="CurrentStepText">
          ${rendered}
        </div>

        <div class="mt-3" id="InlineVarEditorHost"></div>
      </div>
    `;
    }


    // -----------------------------
    // INLINE EDITOR (unter dem Step)
    // -----------------------------
    function preserveWindowScroll(work) {
        const scrollX = window.scrollX || window.pageXOffset || 0;
        const scrollY = window.scrollY || window.pageYOffset || 0;
        if (typeof work === "function") work();
        window.requestAnimationFrame(() => window.scrollTo(scrollX, scrollY));
    }
    // ══════════════════════════════════════════════════════════════
    // UNIVERSAL EDITOR OVERLAY SYSTEM (used by both areas)
    // ══════════════════════════════════════════════════════════════

    /**
     * Opens a universal fullscreen editor overlay.
     * Used by both Smart Step Creator and Probability Area.
     *
     * @param {Object} config - Configuration object
     * @param {string} config.type - 'step' or 'probability'
     * @param {string} config.varName - Variable name being edited
     * @param {string} config.currentVal - Current value
     * @param {string} config.previewHtml - HTML for the preview section (top)
     * @param {Array} config.multiIngredients - Multi-ingredient array
     * @param {string} config.masterId - Master step ID (for probability area)
     * @param {Function} config.onApply - Callback when applying value
     * @param {Function} config.onClose - Callback when closing editor
     * @param {Object} config.eventContext - Context for event handlers (activeStep, activeToken, etc.)
     */
    function openUniversalEditorOverlay(config) {
        const {
            type = 'step',
            varName,
            currentVal = '',
            previewHtml = '',
            multiIngredients = [],
            masterId = '',
            onApply,
            onClose,
            eventContext = {}
        } = config;

        console.log("[Universal Overlay] Opening:", { type, varName, masterId });

        // Generate editor HTML using shared function
        const useClassBasedIds = type === 'probability';
        const result = _generateEditorHtml(varName, currentVal, {
            multiIngredients,
            masterId,
            selectedIngredientValues: null,
            suppressPronounButtons: false,
            useClassBasedIds
        });

        // Split editor HTML into content and buttons
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = result.html;
        const buttonRow = tempDiv.querySelector('.d-flex.gap-2.align-items-center');
        const buttonsHtml = buttonRow ? buttonRow.outerHTML : '';
        if (buttonRow) buttonRow.remove();
        const editorContentHtml = tempDiv.innerHTML;

        // Get theme
        const creatorEl = document.querySelector('.smart-step-creator');
        const theme = creatorEl ? creatorEl.dataset.theme || 'dark' : 'dark';

        // Create fullscreen overlay HTML
        const overlayHtml = `
            <div id="universalEditorOverlay"
                 class="smart-step-creator"
                 data-theme="${theme}"
                 data-overlay-owner="step"
                 data-editor-type="${type}"
                 data-var-name="${escapeHtml(varName)}"
                 data-master-id="${escapeHtml(masterId)}"
                 style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 9999; display: flex; flex-direction: column;">

                <!-- Fixed Header: Preview -->
                <div class="creator-preview-canvas" style="flex-shrink: 0; padding: 16px; border-bottom: 1px solid rgba(255,255,255,0.1); background: rgba(0,0,0,0.2); min-height: auto;">
                    <div style="max-width: 800px; margin: 0 auto;" class="universal-overlay-preview">
                        ${previewHtml}
                    </div>
                </div>

                <!-- Scrollable Middle: Editor Content -->
                <div id="universalEditorContent" style="flex: 1; overflow-y: auto; overflow-x: hidden; padding: 24px 16px; background: var(--creator-sheet, rgba(255,255,255,0.05));">
                    <div style="max-width: 800px; margin: 0 auto;">
                        ${editorContentHtml}
                    </div>
                </div>

                <!-- Fixed Footer: Buttons -->
                <div style="flex-shrink: 0; padding: 16px; border-top: 1px solid rgba(255,255,255,0.1); background: rgba(0,0,0,0.3);">
                    <div style="max-width: 800px; margin: 0 auto;">
                        ${buttonsHtml}
                    </div>
                </div>
            </div>
        `;

        // Remove existing overlay
        const existing = document.getElementById('universalEditorOverlay');
        if (existing) existing.remove();

        // Append to body
        document.body.insertAdjacentHTML('beforeend', overlayHtml);

        // Set data attributes on editor element
        const editorEl = document.querySelector('#universalEditorOverlay .duration-editor, #universalEditorOverlay .prob-inline-editor');
        if (editorEl) {
            Object.keys(result.dataAttributes).forEach(key => {
                editorEl.dataset[key] = result.dataAttributes[key];
            });
        }

        // Prevent body scroll
        document.body.style.overflow = 'hidden';

        // Bind universal event handlers
        _bindUniversalOverlayEvents(type, varName, masterId, onApply, onClose, eventContext);

        console.log("[Universal Overlay] Opened successfully");
    }

    /**
     * Closes the universal editor overlay
     */
    function closeUniversalEditorOverlay() {
        const overlay = document.getElementById('universalEditorOverlay');
        if (overlay) {
            console.log("[Universal Overlay] Closing");
            overlay.remove();
        }

        // Restore body scroll
        document.body.style.overflow = '';
    }

    /**
     * Binds event handlers to the universal overlay
     */
    function _bindUniversalOverlayEvents(type, varName, masterId, onApply, onClose, eventContext) {
        const $overlay = $('#universalEditorOverlay');
        const editorEl = document.querySelector('#universalEditorOverlay .duration-editor, #universalEditorOverlay .prob-inline-editor');
        const useClassBasedIds = type === 'probability';

        if (!editorEl) {
            console.error("[Universal Overlay] Editor element not found!");
            return;
        }

        // IMPORTANT: Remove ALL previous handlers (from both areas!)
        $overlay.off('.universal').off('.probinline');

        // Close button
        $overlay.on('click.universal', '.js-prob-inline-close, #BtnCloseVar', function() {
            closeUniversalEditorOverlay();
            if (typeof onClose === 'function') onClose();
        });

        // Apply button
        $overlay.on('click.universal', '.js-prob-inline-apply, #BtnApplyVar', function() {
            if (typeof onApply === 'function') {
                const value = applyEditorValue(editorEl);
                onApply(value, editorEl);
            }
        });

        // Pick-mode buttons (article, ingredient, fraction, etc.)
        $overlay.on('click.universal', 'button[data-pick-mode]', function(e) {
            // Only handle if this is OUR overlay
            const overlayEl = document.getElementById('universalEditorOverlay');
            if (!overlayEl || overlayEl.dataset.overlayOwner !== 'step') return;

            // Stop other handlers from executing
            e.stopImmediatePropagation();

            // Only handle if editorEl still exists and is valid
            if (!editorEl || !editorEl.isConnected) return;

            _handlePickModeClick(this, editorEl, useClassBasedIds);
        });

        // Duration unit buttons
        $overlay.on('click.universal', 'button[data-duration-unit]', function(e) {
            const overlayEl = document.getElementById('universalEditorOverlay');
            if (!overlayEl || overlayEl.dataset.overlayOwner !== 'step') return;

            e.stopImmediatePropagation();

            if (!editorEl || !editorEl.isConnected) return;
            _handleDurationUnitClick(this, editorEl);
        });

        // Remove multi-ingredient chip
        $overlay.on('click.universal', 'button[data-remove-multi-ingredient]', function(e) {
            e.stopPropagation();
            e.preventDefault();

            const idx = parseInt(this.dataset.removeMultiIngredient, 10);
            if (isNaN(idx)) return;

            console.log("[Universal Overlay] Remove chip at index:", idx);

            if (type === 'step') {
                // Smart Step Creator logic
                if (!activeStep || !activeToken) return;

                const multiIngredientsObj = activeStep._multiIngredients || {};
                const existingList = multiIngredientsObj[activeToken.varName] || [];
                if (idx < 0 || idx >= existingList.length) return;

                existingList.splice(idx, 1);
                if (!activeStep._multiIngredients) activeStep._multiIngredients = {};
                activeStep._multiIngredients[activeToken.varName] = existingList;

                const composed = formatSelectedIngredientList(existingList, currentLang);
                activeStep.values[activeToken.varName] = composed;

                renderMasterText();

                // Update overlay preview
                const currentStepWrap = document.querySelector(".current-step-wrap");
                const previewSection = document.querySelector('#universalEditorOverlay .universal-overlay-preview');
                if (currentStepWrap && previewSection) {
                    previewSection.innerHTML = currentStepWrap.innerHTML;
                }

                // Re-open overlay with updated data
                openInlineEditor(activeToken.varName, activeToken.tokenId);

            } else if (type === 'probability') {
                // Probability Area logic
                const existingList = (window.ProbabilityMultiIngredients[masterId] || {})[varName] || [];
                if (idx < 0 || idx >= existingList.length) return;

                existingList.splice(idx, 1);
                if (!window.ProbabilityMultiIngredients[masterId]) {
                    window.ProbabilityMultiIngredients[masterId] = {};
                }
                window.ProbabilityMultiIngredients[masterId][varName] = existingList;

                const helpers = window.MasterStepCreatorHelpers;
                const composed = helpers && helpers.formatSelectedIngredientList
                    ? helpers.formatSelectedIngredientList(existingList, currentLang || 'de')
                    : '';

                // Update template token
                const templateWrap = document.querySelector(`.probability-template-wrap[data-master-id="${masterId}"]`);
                const token = templateWrap ? templateWrap.querySelector(`.js-probability-var[data-var="${varName}"]`) : null;

                if (token) {
                    token.textContent = composed || varName;
                    token.dataset.hasValue = composed ? '1' : '0';
                }

                // Update overlay preview
                if (templateWrap) {
                    const templateCard = templateWrap.querySelector('.probability-template-card');
                    const previewSection = document.querySelector('#universalEditorOverlay .universal-overlay-preview');
                    if (templateCard && previewSection) {
                        previewSection.innerHTML = templateCard.innerHTML;
                    }
                }

                // Re-open overlay (call the probability function)
                if (typeof eventContext.reopenEditor === 'function') {
                    eventContext.reopenEditor(varName, composed);
                }
            }
        });

        console.log("[Universal Overlay] Events bound for type:", type);
    }

    // Old closeInlineEditor - now calls universal function
    function closeInlineEditor() {
        activeToken = null;
        closeUniversalEditorOverlay();

        // Clear old inline host (backward compatibility)
        const host = $("#InlineVarEditorHost");
        if (host) host.innerHTML = "";
    }

    function isIngredientVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase();
        return key === "ingredient" || key === "ingredient2" || key === "ingredients" || key === "liquid" || key === "fat" || key === "seasonings" || key === "marinade" || key === "thickener" || key === "components" || key === "extra";
    }

    function isHybridIngredientVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase();
        return key === "extra";
    }

    // Filters ingredient items based on the variable type (e.g. {{liquid}} → only liquids)
    // and optionally by step context (e.g. PREP_CUT_01 → only isHard).
    // Returns { filtered, rest } where filtered are primary matches and rest are remaining items.
    function filterIngredientsByVarType(items, varName, masterId) {
        const key = (varName || "").toString().trim().toLowerCase();
        const step = (masterId || "").toString().trim().toUpperCase();
        let filterFn = null;

        if (key === "liquid") {
            filterFn = item => item.isLiquid;
        } else if (key === "fat") {
            filterFn = item => item.isFat;
        } else if (key === "hard") {
            filterFn = item => item.isHard;
        } else if (key === "soft") {
            filterFn = item => item.isSoft;
        } else if (key === "seasonings") {
            filterFn = item => item.groupId === "5"; // Gewürze
        } else if (key === "thickener") {
            filterFn = item => item.groupId === "8"; // Grundnahrungsmittel
        } else if (key === "ingredient" || key === "ingredients") {
            // Step-spezifische Filter für generische ingredient-Variable
            if (step === "PREP_CUT_01" || step === "PREP_GRATE_01" || step === "PREP_MINCE_01" || step === "PREP_PEEL_01") {
                filterFn = item => item.isHard || item.isSoft;
            } else if (step === "PREP_TENDERIZE_01") {
                // Klopfen/Plattieren: nur harte Zutaten (Fleisch, Schnitzel)
                filterFn = item => item.isHard;
            }
        }

        if (!filterFn) return { filtered: items, rest: [] };

        const filtered = items.filter(filterFn);
        const rest = items.filter(item => !filterFn(item));
        // Return filtered items (empty if no matches)
        return { filtered, rest };
    }

    function isGrindSizeVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "grindsize" || key.includes("grindsize");
    }

    function isNoArticleVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "state" || key === "duration" || key === "count" || key === "mode" || key === "component" || key === "pronoun" || key === "pronoun2" || key === "pronomen" || key === "shape" || key === "finish" || key === "marinade" || key === "method" || key === "thickener" || key === "action" || isGrindSizeVariable(varName);
    }
    function isStateVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "state";
    }

    

    function isCompactSpecialVariable(varName) {
        const key = (varName || '').toString().trim().toLowerCase();
        return key === 'duration' || key === 'temp' || key === 'count';
    }

    function getSelectedIngredientNamesFromPage() {
        const rows = Array.from(document.querySelectorAll("#selectedIngredients .ingredient-row .ingredient-name-text"));
        return rows
            .map(x => (x?.textContent || "").toString().trim())
            .filter(Boolean);
    }

    // Wie getSelectedIngredientNamesFromPage, aber mit Icon aus data-group-icon
    function getSpecialTransformChipsForStep(masterId) {
        if (!window.ingredientTransforms) return [];
        var transforms = window.ingredientTransforms.special_transforms || [];
        var lang = (currentLang || "de").toLowerCase();
        var chips = [];
        transforms.forEach(function (t) {
            if (t.trigger_step !== masterId) return;
            (t.outputs || []).forEach(function (o) {
                var name = (o.names && (o.names[lang] || o.names.de)) || "";
                if (name && !chips.some(function (c) { return c.name.toLowerCase() === name.toLowerCase(); })) {
                    chips.push({ name: name, icon: o.icon || "🔄" });
                }
            });
        });
        return chips;
    }

    function hasMatchingIngredientForTransform(items, transformDef) {
        var lang = (currentLang || "de").toLowerCase();
        var matchTerms = (transformDef.match && (transformDef.match[lang] || transformDef.match.de)) || [];
        return items.some(function (x) {
            var name = (x.name || "").toLowerCase();
            return matchTerms.some(function (t) { return name.includes(t); });
        });
    }

    function getDerivedChipsFromAcceptedSteps() {
        if (!window.ingredientTransforms) return [];
        var transforms = window.ingredientTransforms.special_transforms || [];
        var lang = (currentLang || "de").toLowerCase();
        var result = [];

        transforms.forEach(function (transformDef) {
            var stepRows = document.querySelectorAll(
                '#selectedSteps .step-row[data-master-template-id="' + transformDef.trigger_step + '"]');
            if (!stepRows.length) return;

            var matchTerms = (transformDef.match && (transformDef.match[lang] || transformDef.match.de)) || [];
            stepRows.forEach(function (row) {
                var refJson = (row.dataset.stepReferenceJson || "").toLowerCase();
                var ingName = (row.dataset.ingredientName || "").toLowerCase();
                var hasMatch = matchTerms.some(function (t) { return ingName.includes(t) || refJson.includes(t); });
                if (hasMatch) {
                    (transformDef.outputs || []).forEach(function (output) {
                        var name = (output.names && (output.names[lang] || output.names.de)) || "";
                        if (name && !result.some(function (r) { return r.name.toLowerCase() === name.toLowerCase(); })) {
                            result.push({ name: name, icon: output.icon || "🔄" });
                        }
                    });
                }
            });
        });
        return result;
    }

    function getSelectedIngredientsFromPage() {
        if (window.CreatePostingIngredientHelpers && typeof window.CreatePostingIngredientHelpers.getSelectedIngredientsForSandbox === "function") {
            return window.CreatePostingIngredientHelpers.getSelectedIngredientsForSandbox(currentLang).map(item => ({
                name: (item?.name || "").toString().trim(),
                icon: (item?.iconHtml || "").toString().trim(),
                namesByLang: item?.namesByLang || {},
                genusByLang: item?.genusByLang || {},
                id: (item?.id || "").toString(),
                groupId: (item?.groupId || "").toString(),
                isLiquid: !!item?.isLiquid,
                isFat: !!item?.isFat,
                isHard: !!item?.isHard,
                isSoft: !!item?.isSoft
            })).filter(x => x.name);
        }

        const rows = Array.from(document.querySelectorAll("#selectedIngredients .ingredient-row"));
        let items = rows.map(row => {
            const name = (row.querySelector(".ingredient-name-text")?.textContent || "").trim();
            const icon = (row.dataset.groupIcon || row.querySelector(".ingredient-group-icon")?.innerHTML || "").trim();
            return { name, icon };
        }).filter(x => x.name);

        const addActiveChips = activeStep && activeStep.master_id
            && (window.ingredientTransforms?.special_transforms || []).some(function (t) {
                return t.trigger_step === activeStep.master_id && hasMatchingIngredientForTransform(items, t);
            });
        const derivedChips = addActiveChips
            ? getSpecialTransformChipsForStep(activeStep.master_id)
            : getDerivedChipsFromAcceptedSteps();

        if (derivedChips.length) {
            const existing = new Set(items.map(x => x.name.toLowerCase()));
            const toInsert = derivedChips.filter(ep => !existing.has(ep.name.toLowerCase()));
            if (toInsert.length) {
                items.unshift(...toInsert);
            }
        }

        const { remainderChips, fullyUsedNames } = getRemainderChipsFromAcceptedSteps();
        if (fullyUsedNames.length) {
            items = items.filter(x => !fullyUsedNames.includes(x.name.toLowerCase()));
        }
        if (remainderChips.length) {
            remainderChips.forEach(rc => {
                const origIdx = items.findIndex(x => x.name.toLowerCase() === rc.originalName.toLowerCase());
                if (origIdx >= 0) {
                    items[origIdx] = { ...items[origIdx], ...rc, icon: items[origIdx].icon || rc.icon };
                } else {
                    items.push(rc);
                }
            });
        }

        return items;
    }

    

    function parseSelectedIngredientValues(rawValue, options) {
        const list = (options || []).map(x => (x || "").toString().trim()).filter(Boolean);
        const byLower = new Map(list.map(x => [x.toLowerCase(), x]));
        const tokens = (rawValue || "")
            .toString()
            .replace(/\s+und\s+/gi, ",")
            .replace(/\s+and\s+/gi, ",")
            .replace(/\s+y\s+/gi, ",")
            .replace(/\s+e\s+/gi, ",")
            .split(",")
            .map(x => x.trim())
            .filter(Boolean);

        const selected = [];
        tokens.forEach(token => {
            const key = token.toLowerCase();
            if (byLower.has(key) && !selected.find(s => (s.name || s).toLowerCase() === key)) {
                selected.push({ name: byLower.get(key), fraction: "" });
            }
        });
        return selected;
    }

    // Helper: Get ingredient name from new or old format
    function getIngredientName(item) {
        return typeof item === 'string' ? item : (item?.name || "");
    }

    // Helper: Get ingredient fraction from new format (returns "" for old format)
    function getIngredientFraction(item) {
        return (typeof item === 'object' && item?.fraction) ? item.fraction : "";
    }

    // Helper: Normalize selectedIngredientValues to new format
    function normalizeIngredientValues(values) {
        if (!Array.isArray(values)) return [];
        return values.map(item => {
            if (typeof item === 'string') {
                return { name: item, fraction: "", article: "" };
            }
            return {
                name: item.name || "",
                fraction: item.fraction || "",
                article: item.article || ""
            };
        });
    }

    function formatSelectedIngredientList(names, langKey) {
        const lang = langKey || currentLang;

        // New: Handle array of objects with fractions and per-ingredient articles
        if (Array.isArray(names) && names.length > 0 && typeof names[0] === 'object' && names[0].name) {
            console.log('[formatSelectedIngredientList] Processing objects:', names);
            const allIngredients = getSelectedIngredientsFromPage();
            console.log('[formatSelectedIngredientList] All ingredients:', allIngredients);
            const frOpts = getFractionOptions();

            const composed = names.map(item => {
                const itemName = getIngredientName(item);
                const itemFraction = getIngredientFraction(item);
                const itemArticle = item.article || ""; // Per-ingredient article
                console.log(`[formatSelectedIngredientList] Item: ${itemName}, Fraction: ${itemFraction}, Article: ${itemArticle}`);

                // Determine final article: use per-ingredient if set, otherwise auto-detect from genus
                let article = "";
                if (itemArticle && itemArticle !== "ohne") {
                    article = itemArticle;
                    console.log(`[formatSelectedIngredientList] Using per-ingredient article: "${article}"`);
                } else {
                    // Find full ingredient data to get genus/article
                    const fullIngredient = allIngredients.find(ing => ing.name === itemName);
                    article = fullIngredient?.genusByLang?.[lang] || "";
                    console.log(`[formatSelectedIngredientList] Auto-detected genus: "${article}"`);
                }

                // If no fraction, return with article if present
                if (!itemFraction) {
                    if (article && article !== "ohne") {
                        return `${article} ${itemName}`;
                    }
                    return itemName;
                }

                // Find fraction definition
                const fractionDef = frOpts.find(f => f.key === itemFraction);
                if (!fractionDef) {
                    console.log(`[formatSelectedIngredientList] No fraction def found for ${itemFraction}`);
                    return itemName;
                }
                console.log('[formatSelectedIngredientList] Fraction def:', fractionDef);

                // Compose with fraction
                const result = composeFractionText(fractionDef, itemName, article);
                console.log(`[formatSelectedIngredientList] Composed: ${result}`);
                return result;
            });

            // Format as list
            if (!composed.length) return "";
            if (composed.length === 1) return composed[0];
            if (composed.length === 2) return `${composed[0]} und ${composed[1]}`;
            const head = composed.slice(0, -1).join(", ");
            const tail = composed[composed.length - 1];
            return `${head} und ${tail}`;
        }

        // Old: Handle array of strings (backward compatibility)
        if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.formatIngredientList === "function") {
            return window.MasterStepCreatorHelpers.formatIngredientList(names || [], lang);
        }
        const list = (names || []).map(x => (x || "").toString().trim()).filter(Boolean);
        if (!list.length) return "";
        if (list.length === 1) return list[0];
        if (list.length === 2) return `${list[0]} und ${list[1]}`;
        const head = list.slice(0, -1).join(", ");
        const tail = list[list.length - 1];
        return `${head} und ${tail}`;
    }


    // ── Shared HTML Generator for Both Editors ──
    // Generates editor HTML for both Smart Step Creator and Probability Area
    // Returns: { html: string, dataAttributes: object }
    function _generateEditorHtml(varName, currentVal, opts = {}) {
        const {
            multiIngredients = [],
            masterId = "",
            selectedIngredientValues: preSelectedValues = null,
            suppressPronounButtons = false,
            useClassBasedIds = false // true for Probability Area (uses js-* classes instead of IDs)
        } = opts;

        const scoringContext = buildCurrentScoringContext();
        const compactSpecialVar = isCompactSpecialVariable(varName);

        // Special variables (duration/temp/count) - simplified editor
        if (compactSpecialVar) {
            const specialBlockCompact = renderSpecialEditor(varName, currentVal);
            return {
                html: `<div class="duration-editor mt-2${useClassBasedIds ? ' prob-inline-editor' : ''}" data-editor-for="${escapeHtml(varName)}">
                    <div class="small text-muted mb-1"><strong>${escapeHtml(getVarDisplayName(varName))}</strong> auswählen</div>
                    ${specialBlockCompact}
                </div>`,
                dataAttributes: {
                    selectedArticle: "",
                    selectedPronoun: "",
                    selectedValue: "",
                    selectedIngredientValues: "[]",
                    selectedFraction: ""
                }
            };
        }

        // Regular variables
        const ingredientVar = isIngredientVariable(varName);
        const noArticleVar = isNoArticleVariable(varName);
        const stateVar = isStateVariable(varName);
        const pronounVar = (varName || '').toLowerCase() === 'pronoun';
        const isPronounOrState = pronounVar || stateVar;

        // Check if template has BOTH pronoun AND state tokens
        const templateRaw = activeStep?.templateRaw || "";
        const hasPronounToken = /\{\{\s*pronoun\s*\}\}/i.test(templateRaw);
        const hasStateToken = /\{\{\s*state\s*\}\}/i.test(templateRaw);
        const hasBothTokens = hasPronounToken && hasStateToken;

        // Only show combined editor if BOTH tokens exist
        const showCombinedEditor = isPronounOrState && hasBothTokens;
        const showOnlyPronoun = pronounVar && !hasStateToken;

        // Article buttons
        const articleButtons = noArticleVar ? "" : renderPillButtons(getArticleOptions(), "article", null);

        // Pronoun buttons (for pronoun/state variables)
        const pronounButtons = ((showCombinedEditor || showOnlyPronoun) && !suppressPronounButtons)
            ? renderPillButtons(getVarOptions("pronoun", masterId, scoringContext), "pronoun", null)
            : "";

        // Get ingredient items
        const allIngredientItemsList = ingredientVar ? getSelectedIngredientsFromPage() : [];
        const { filtered: ingredientItems, rest: restIngredientItems } = ingredientVar
            ? filterIngredientsByVarType(allIngredientItemsList, varName, masterId)
            : { filtered: allIngredientItemsList, rest: [] };

        // Options for non-ingredient variables
        const options = ingredientVar
            ? ingredientItems.map(x => x.name)
            : (showCombinedEditor ? getVarOptions('state', masterId, scoringContext)
               : (showOnlyPronoun ? [] // No value buttons for pronoun-only
                  : getVarOptions(varName, masterId, scoringContext)));

        // Parse selected ingredient values
        let selectedIngredientValues = [];
        if (ingredientVar) {
            if (preSelectedValues) {
                selectedIngredientValues = normalizeIngredientValues(preSelectedValues);
            } else {
                selectedIngredientValues = parseSelectedIngredientValues(currentVal, options.concat(restIngredientItems.map(x => x.name)));
            }
        }

        // Render ingredient chips with disabled state for already-added ingredients
        const renderIngChip = ({ name, icon, isRemainder, dimmed }) => {
            const value = name.trim();
            const selectedItem = selectedIngredientValues.find(s => getIngredientName(s) === value);
            const active = selectedItem ? " active" : "";
            const remainderCls = isRemainder ? " fraction-remainder" : "";
            const dimCls = dimmed ? " ingredient-chip-dimmed" : "";

            // Check if already added to multi-ingredients list
            const alreadyAdded = multiIngredients.some(item => getIngredientName(item) === value);
            const disabledCls = alreadyAdded ? " ingredient-chip-disabled" : "";
            const disabled = alreadyAdded ? " disabled" : "";

            const safe = escapeHtml(value);
            const iconPart = icon ? `<span class="chip-icon" aria-hidden="true">${icon}</span> ` : "";

            return `<button type="button" class="ingredient-chip${active}${remainderCls}${dimCls}${disabledCls}"${disabled} data-pick-mode="ingredient-value" data-pick-value="${safe}">${iconPart}${safe}</button>`;
        };

        const valueButtons = ingredientVar
            ? ingredientItems.map(item => renderIngChip(item)).join("")
                + (restIngredientItems.length ? `<span class="ingredient-chip-divider"></span>` + restIngredientItems.map(item => renderIngChip({ ...item, dimmed: true })).join("") : "")
            : renderPillButtons(options, "value", currentVal);

        // Hybrid variable support
        const hybridVar = isHybridIngredientVariable(varName);
        const hybridOptions = hybridVar ? getVarOptions(varName, masterId, scoringContext) : [];
        const hybridButtons = hybridVar ? renderPillButtons(hybridOptions, "value", currentVal) : "";

        // Fraction picker for ingredient variables
        const fractionOptions = getFractionOptions();
        const fractionPickerHtml = ingredientVar ? renderFractionPickerHtml(fractionOptions, useClassBasedIds) : "";

        // Special editor blocks (duration/temp inputs)
        const specialBlock = renderSpecialEditor(varName, currentVal);

        // Multi-ingredients chips (already added ingredients with remove buttons)
        const frOpts = getFractionOptions();
        const multiIngredientsChips = multiIngredients.length > 0
            ? multiIngredients.map((item, idx) => {
                const frDef = frOpts.find(f => f.key === item.fraction);
                const displayText = item.fraction && frDef
                    ? composeFractionText(frDef, item.name, item.article)
                    : (item.article && item.article !== "ohne" ? `${item.article} ${item.name}` : item.name);
                return `<span class="badge bg-primary me-1 mb-1" style="font-size: 0.85rem; padding: 0.4rem 0.6rem;">
                    ${escapeHtml(displayText)}
                    <button type="button" class="btn-close btn-close-white ms-1" data-remove-multi-ingredient="${idx}" style="font-size: 0.6rem; padding: 0;" aria-label="Entfernen"></button>
                </span>`;
            }).join("")
            : "";

        const multiIngredientsSection = multiIngredientsChips
            ? `<div class="mb-2 pb-2 border-bottom">
                <div class="small text-muted mb-1"><strong>Bereits hinzugefügt:</strong></div>
                <div>${multiIngredientsChips}</div>
               </div>`
            : "";

        // ID/class selectors (different for Probability Area vs Step Creator)
        const articleRowId = useClassBasedIds ? 'class="d-flex flex-wrap gap-2 mb-2 js-article-btn-row"' : 'id="ArticleBtnRow" class="d-flex flex-wrap gap-2 mb-2"';
        const pronounRowId = useClassBasedIds ? 'class="d-flex flex-wrap gap-2 mb-2 js-pronoun-btn-row"' : 'id="PronounBtnRow" class="d-flex flex-wrap gap-2 mb-2"';
        const valueRowId = useClassBasedIds ? 'class="d-flex flex-wrap gap-2 js-value-btn-row"' : 'id="ValueBtnRow" class="d-flex flex-wrap gap-2"';
        const hybridRowId = useClassBasedIds ? 'class="d-flex flex-wrap gap-2 js-hybrid-options-row"' : 'id="HybridOptionsBtnRow" class="d-flex flex-wrap gap-2"';
        const applyBtnClass = useClassBasedIds ? 'js-prob-inline-apply' : '';
        const applyBtnId = useClassBasedIds ? '' : 'id="BtnApplyVar"';
        const closeBtnClass = useClassBasedIds ? 'js-prob-inline-close' : '';
        const closeBtnId = useClassBasedIds ? '' : 'id="BtnCloseVar"';

        // Extract prefill values from currentVal
        const prefillConfig = {
            articleOptions: getArticleOptions(),
            pronounOptions: getVarOptions("pronoun", masterId, scoringContext),
            stateVar: stateVar,
            ingredientVar: ingredientVar,
            noArticleVar: noArticleVar,
            suppressPronounButtons: suppressPronounButtons
        };
        const prefilled = splitEditorPrefillValue(currentVal, prefillConfig);

        // Build HTML
        const html = `<div class="duration-editor mt-2${useClassBasedIds ? ' prob-inline-editor' : ''}" data-editor-for="${escapeHtml(varName)}" data-hybrid="${hybridVar ? "1" : "0"}">
            <div class="small text-muted mb-1"><strong>${escapeHtml(getVarDisplayName(varName))}</strong> auswählen</div>

            ${multiIngredientsSection}

            ${specialBlock}

            ${fractionPickerHtml}

            ${noArticleVar ? "" : `
            <div class="small text-muted mt-2 mb-1">Artikel</div>
            <div ${articleRowId}>
              ${articleButtons}
            </div>
            `}

            ${(showCombinedEditor || showOnlyPronoun) ? `
            <div class="small text-muted mt-2 mb-1">Pronomen</div>
            <div ${pronounRowId}>
              ${pronounButtons}
            </div>
            ` : ""}

            ${showCombinedEditor ? `<div class="small text-muted mb-1">Zustand</div>` : (showOnlyPronoun ? "" : `<div class="small text-muted mb-1">${escapeHtml(getVarDisplayName(varName))} einsetzen</div>`)}
            <div ${valueRowId}>
              ${valueButtons || (ingredientVar
                ? `<div class="text-muted small">Keine Zutaten ausgewählt.</div>`
                : `<div class="text-muted small">Keine Optionen im JSON gefunden: master_step_variables.${escapeHtml(varName)}.${escapeHtml(currentLang)}</div>`)}
            </div>

            ${hybridVar && hybridButtons ? `
            <div class="small text-muted mt-2 mb-1">Optionen</div>
            <div ${hybridRowId}>
              ${hybridButtons}
            </div>
            ` : ""}

            ${ingredientVar && !hybridVar ? "" : (!ingredientVar && !showOnlyPronoun ? renderFallbackSection(showCombinedEditor ? 'state' : varName, hybridVar ? hybridOptions : options, masterId, scoringContext) : "")}

            <div class="d-flex gap-2 align-items-center mt-3 js-editor-action-row">
              <button type="button" class="btn btn-sm creator-cta-primary ${applyBtnClass}" ${applyBtnId}>Einsetzen</button>
              <button type="button" class="btn btn-sm btn-outline-secondary ${closeBtnClass}" ${closeBtnId}>Schließen</button>
            </div>
          </div>`;

        return {
            html,
            dataAttributes: {
                selectedArticle: prefilled.article || "",
                selectedPronoun: prefilled.pronoun || "",
                selectedValue: prefilled.value || "",
                selectedIngredientValues: JSON.stringify(selectedIngredientValues),
                selectedFraction: ""
            }
        };
    }

    // ── UNIFIED: Multi-Ingredients Management ──
    // Manages multi-ingredients for both Step Creator and Probability Area
    function getMultiIngredientsForContext(context, varName) {
        if (!context || !varName) return [];

        if (context.type === 'step') {
            // Smart Step Creator: stored in activeStep._multiIngredients
            return activeStep?._multiIngredients?.[varName] || [];
        } else if (context.type === 'probability') {
            // Probability Area: stored in window.ProbabilityMultiIngredients
            const masterId = context.probabilityMasterId || context.masterId;
            return window.ProbabilityMultiIngredients?.[masterId]?.[varName] || [];
        }
        return [];
    }

    function saveMultiIngredientsForContext(context, varName, values) {
        if (!context || !varName) return;

        if (context.type === 'step') {
            // Smart Step Creator
            if (!activeStep) return;
            if (!activeStep._multiIngredients) activeStep._multiIngredients = {};
            activeStep._multiIngredients[varName] = values;
        } else if (context.type === 'probability') {
            // Probability Area
            const masterId = context.probabilityMasterId || context.masterId;
            if (!masterId) return;
            if (!window.ProbabilityMultiIngredients) window.ProbabilityMultiIngredients = {};
            if (!window.ProbabilityMultiIngredients[masterId]) window.ProbabilityMultiIngredients[masterId] = {};
            window.ProbabilityMultiIngredients[masterId][varName] = values;
        }
    }

    // ── Shared Duration-Unit Click Handler ──
    function _handleDurationUnitClick(btn, host) {
        if (!btn || !host) return;

        host.dataset.durationUnit = btn.dataset.durationUnit;

        // Mark active — deactivate all duration-unit buttons in the editor
        host.querySelectorAll("button[data-duration-unit]")?.forEach(b => b.classList.remove("active"));
        btn.classList.add("active");

        // per_package/short: hide number fields; minute/hour: show
        const hideNum = btn.dataset.durationUnit === "per_package" || btn.dataset.durationUnit === "short";
        const numInput = host.querySelector("#DurationValueInput");
        const numInputTo = host.querySelector("#DurationValueToInput");
        const dashSep = numInput?.nextElementSibling;
        if (numInput) numInput.style.display = hideNum ? "none" : "";
        if (numInputTo) numInputTo.style.display = hideNum ? "none" : "";
        if (dashSep && dashSep.tagName === "SPAN") dashSep.style.display = hideNum ? "none" : "";
    }

    // ── Shared Pick-Mode Click Handler ──
    function _handlePickModeClick(pickBtn, host, useClassBasedIds = false) {
        if (!pickBtn || !host) return;

        const mode = pickBtn.dataset.pickMode;
        const val = pickBtn.dataset.pickValue ?? "";

        console.log("[_handlePickModeClick]", { mode, val, useClassBasedIds, pickBtn, host });

        // Handle fraction mode
        if (mode === "fraction") {
            const frRowId = useClassBasedIds ? ".js-fraction-picker-row" : "#FractionPickerRow";
            const frRow = typeof frRowId === "string" && frRowId.startsWith(".")
                ? host.querySelector(frRowId)
                : host.querySelector(frRowId);

            console.log("[Fraction Mode]", { frRowId, frRow, fractionKey: pickBtn.dataset.fractionKey });

            if (frRow) {
                frRow.querySelectorAll(".fraction-pick").forEach(b => b.classList.remove("active"));
                pickBtn.classList.add("active");
            }
            host.dataset.selectedFraction = pickBtn.dataset.fractionKey || "";
            console.log("[Fraction Mode] Set dataset.selectedFraction =", host.dataset.selectedFraction);
            return;
        }

        // Handle ingredient-value mode (multi-select chips)
        if (mode === "ingredient-value") {
            const selected = JSON.parse(host.dataset.selectedIngredientValues || "[]");
            let list = normalizeIngredientValues(selected);
            const idx = list.findIndex(item => getIngredientName(item) === val);

            console.log("[Ingredient-Value Mode]", { val, selected, list, idx });

            if (idx >= 0) {
                list.splice(idx, 1);
                pickBtn.classList.remove("active");
            } else {
                list.push({ name: val, fraction: "", article: "" });
                pickBtn.classList.add("active");
            }
            host.dataset.selectedIngredientValues = JSON.stringify(list);
            host.dataset.selectedValue = list.map(item => getIngredientName(item)).join(", ");

            console.log("[Ingredient-Value Mode] Updated:", { list, selectedIngredientValues: host.dataset.selectedIngredientValues });
            return;
        }

        // Hybrid: clicking an option pill deselects all ingredient chips
        if (mode === "value" && host.dataset.hybrid === "1") {
            host.dataset.selectedIngredientValues = "[]";
            host.querySelectorAll(".ingredient-chip.active").forEach(b => b.classList.remove("active"));
        }

        // Toggle active style for article/pronoun/value modes
        let row;
        if (mode === "article") {
            row = useClassBasedIds ? host.querySelector(".js-article-btn-row") : $("#ArticleBtnRow");
        } else if (mode === "pronoun") {
            row = useClassBasedIds ? host.querySelector(".js-pronoun-btn-row") : $("#PronounBtnRow");
        } else if (mode === "value") {
            row = useClassBasedIds ? host.querySelector(".js-value-btn-row") : $("#ValueBtnRow");
        }

        if (row) {
            row.querySelectorAll("button[data-pick-mode]").forEach(b => {
                if (b.dataset.pickMode === mode) b.classList.remove("active");
            });
        }

        // Also toggle in HybridOptionsBtnRow for value mode
        if (mode === "value") {
            const hybridRowSel = useClassBasedIds ? ".js-hybrid-options-row" : "#HybridOptionsBtnRow";
            const hybridRow = host.querySelector(hybridRowSel);
            if (hybridRow) {
                hybridRow.querySelectorAll("button[data-pick-mode='value']").forEach(b => b.classList.remove("active"));
            }
        }

        pickBtn.classList.add("active");

        // Set dataset values
        if (mode === "article") {
            host.dataset.selectedArticle = val;
            console.log("[Article Mode] Set dataset.selectedArticle =", val);
        }
        if (mode === "pronoun") {
            host.dataset.selectedPronoun = val;
            console.log("[Pronoun Mode] Set dataset.selectedPronoun =", val);
        }
        if (mode === "value") {
            host.dataset.selectedValue = val;
            console.log("[Value Mode] Set dataset.selectedValue =", val);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // ── UNIFIED OVERLAY SYSTEM (for both Step Creator & Probability Area) ──
    // ══════════════════════════════════════════════════════════════════════════════

    /**
     * Opens a unified variable editor overlay that works for both Step Creator and Probability Area.
     * @param {Object} config - Configuration object
     * @param {string} config.varName - Variable name (e.g., "item", "state", "duration")
     * @param {string} config.currentVal - Current value for pre-filling
     * @param {string} config.masterId - Master step ID (for scoring/options)
     * @param {Object} config.context - Context object distinguishing between areas
     * @param {string} config.context.type - Either 'step' or 'probability'
     * @param {Function} config.onApply - Callback when value is applied (value, extras) => void
     * @param {Function} config.onClose - Callback when editor is closed
     */
    function openUniversalVariableEditor(config) {
        const {
            varName,
            currentVal = "",
            masterId = "",
            context,
            onApply,
            onClose
        } = config;

        if (!varName || !context) {
            console.error("[openUniversalVariableEditor] Missing required params:", { varName, context });
            return;
        }

        // Get multi-ingredients for this context
        const multiIngredients = getMultiIngredientsForContext(context, varName);

        // Generate editor HTML (unified for both areas)
        const { html, dataAttributes } = _generateEditorHtml(varName, currentVal, {
            masterId: masterId,
            multiIngredients: multiIngredients,
            useClassBasedIds: false, // Unified: always use IDs
            suppressPronounButtons: false
        });

        // Create overlay HTML (different layouts for different contexts)
        const overlayHtml = createUnifiedOverlayHtml(html, context);

        // Remove existing overlay if any
        const existing = document.getElementById('universalEditorOverlay');
        if (existing) existing.remove();

        // Insert overlay into DOM
        document.body.insertAdjacentHTML('beforeend', overlayHtml);
        const overlayEl = document.getElementById('universalEditorOverlay');
        if (!overlayEl) {
            console.error("[Unified Overlay] Failed to create overlay element!");
            return;
        }

        // Prevent body scroll
        document.body.style.overflow = 'hidden';

        // Get editor element and set data attributes
        const editorEl = overlayEl.querySelector('.duration-editor');
        if (!editorEl) {
            console.error("[Unified Overlay] Editor element not found!");
            return;
        }

        // Set data attributes for pre-filled values
        Object.keys(dataAttributes).forEach(key => {
            editorEl.dataset[key] = dataAttributes[key];
        });

        // Bind unified event handlers
        bindUnifiedOverlayEventHandlers(overlayEl, editorEl, {
            varName,
            masterId,
            context,
            onApply,
            onClose
        });
    }

    /**
     * Creates the overlay HTML structure (3-section layout: Header Preview | Scrollable Content | Fixed Footer Buttons)
     */
    function createUnifiedOverlayHtml(editorHtml, context) {
        const theme = document.querySelector('.smart-step-creator')?.dataset?.theme || 'dark';
        const ownerType = context.type || 'step';

        // Get preview HTML based on context (LIVE from DOM for current values!)
        let previewHtml = '';
        if (context.type === 'step') {
            const currentStepWrap = document.querySelector(".current-step-wrap");
            previewHtml = currentStepWrap ? currentStepWrap.innerHTML : "";
        } else if (context.type === 'probability') {
            // LIVE aus DOM holen (wie bei Step Creator!) → zeigt immer aktuelle Werte
            const masterId = context.probabilityMasterId || context.masterId;
            if (masterId) {
                const templateCard = document.querySelector(`.probability-template-wrap[data-master-id="${CSS.escape(masterId)}"] .probability-template-card`);
                previewHtml = templateCard ? templateCard.innerHTML : "";
            }
            if (!previewHtml) {
                previewHtml = context.templatePreviewHtml || ""; // Fallback
            }
        }

        // Split editor HTML into content and buttons
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = editorHtml;
        // ✅ FIX: Only select button row with .js-editor-action-row, not ALL .d-flex!
        // Duration inputs also have .d-flex.gap-2.align-items-center, so we need to be specific
        const buttonRow = tempDiv.querySelector('.js-editor-action-row');
        const buttonsHtml = buttonRow ? buttonRow.outerHTML : '';
        if (buttonRow) buttonRow.remove(); // Remove from editor content
        const editorContentHtml = tempDiv.innerHTML;

        // 3-SECTION LAYOUT: Fixed Header (Preview) | Scrollable Middle (Editor) | Fixed Footer (Buttons)
        return `
            <div id="universalEditorOverlay" class="smart-step-creator" data-theme="${theme}" data-overlay-owner="${ownerType}"
                 style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 9999; display: flex; flex-direction: column;">
                <!-- Fixed Header: Preview -->
                <div class="creator-preview-canvas" style="flex-shrink: 0; padding: 16px; border-bottom: 1px solid rgba(255,255,255,0.1); background: rgba(0,0,0,0.2); min-height: auto;">
                    <div style="max-width: 800px; margin: 0 auto;">
                        ${previewHtml}
                    </div>
                </div>

                <!-- Scrollable Middle: Editor Content (without buttons) -->
                <div id="universalEditorContent" style="flex: 1; overflow-y: auto; overflow-x: hidden; padding: 24px 16px; background: var(--creator-sheet, rgba(255,255,255,0.05));">
                    <div style="max-width: 800px; margin: 0 auto;">
                        ${editorContentHtml}
                    </div>
                </div>

                <!-- Fixed Footer: Buttons -->
                <div style="flex-shrink: 0; padding: 16px; border-top: 1px solid rgba(255,255,255,0.1); background: rgba(0,0,0,0.3);">
                    <div style="max-width: 800px; margin: 0 auto;">
                        ${buttonsHtml}
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Binds unified event handlers to the overlay (works for both contexts)
     */
    function bindUnifiedOverlayEventHandlers(overlayEl, editorEl, config) {
        const { varName, masterId, context, onApply, onClose } = config;

        const $overlay = window.$(overlayEl);  // Use jQuery, not local $ helper

        // Clean up previous handlers
        $overlay.off('.universal');

        // Close button
        $overlay.on('click.universal', '#BtnCloseVar, #BtnCloseVarTop', function(e) {
            e.stopPropagation();  // Prevent old document-level handler from firing
            closeUnifiedOverlay();
            if (typeof onClose === 'function') onClose();
        });

        // Apply button
        $overlay.on('click.universal', '#BtnApplyVar', function(e) {
            e.stopPropagation();  // Prevent old document-level handler from firing
            // ✅ Get fresh editor element (important after variable switch!)
            const currentEditorEl = overlayEl.querySelector('.duration-editor');
            applyUnifiedEditorValue(currentEditorEl, config);
        });

        // Pick-mode buttons (article, pronoun, value, ingredient, fraction)
        $overlay.on('click.universal', 'button[data-pick-mode]', function(e) {
            e.stopImmediatePropagation();
            // ✅ Get fresh editor element
            const currentEditorEl = overlayEl.querySelector('.duration-editor');
            if (!currentEditorEl || !currentEditorEl.isConnected) return;
            _handlePickModeClick(this, currentEditorEl, false);
        });

        // Duration unit buttons
        $overlay.on('click.universal', 'button[data-duration-unit]', function(e) {
            e.stopImmediatePropagation();
            // ✅ Get fresh editor element
            const currentEditorEl = overlayEl.querySelector('.duration-editor');
            _handleDurationUnitClick(this, currentEditorEl);
        });

        // Quick apply buttons for special editors
        $overlay.on('click.universal', '#BtnPickDurationQuick, #BtnPickTempQuick, #BtnPickCountQuick', function(e) {
            e.stopPropagation();  // Prevent old document-level handler from firing
            // ✅ Get fresh editor element
            const currentEditorEl = overlayEl.querySelector('.duration-editor');
            applyUnifiedEditorValue(currentEditorEl, config);
        });

        // Plus button for multi-ingredients
        $overlay.on('click.universal', '.js-add-ingredient-to-list', function(e) {
            e.stopPropagation();
            // ✅ Get fresh editor element
            const currentEditorEl = overlayEl.querySelector('.duration-editor');
            handleUnifiedPlusButtonClick(currentEditorEl, config);
        });

        // Remove multi-ingredient chip
        $overlay.on('click.universal', '[data-remove-multi-ingredient]', function(e) {
            e.stopPropagation();
            const idx = parseInt(this.dataset.removeMultiIngredient, 10);
            if (isNaN(idx)) return;

            // ✅ Use current varName from config (important after variable switch!)
            const currentVarName = config.varName;

            const multiIngredients = getMultiIngredientsForContext(context, currentVarName);
            multiIngredients.splice(idx, 1);
            saveMultiIngredientsForContext(context, currentVarName, multiIngredients);

            // Format updated list
            const composed = formatSelectedIngredientList(multiIngredients, currentLang || 'de');

            // UPDATE CONTEXT (unified) - wichtig: Token/Step aktualisieren!
            updateContextValue(context, currentVarName, composed, {});

            // ✅ NEU (2026-03-28): Preview aktualisieren nach Chip-Entfernung
            updateOverlayPreview(context);

            // ✅ NEU (2026-03-28): Switch editor statt Close+Reopen (Overlay bleibt offen!)
            config.currentVal = composed;
            switchEditorVariable(currentVarName, config);
        });

        // ✅ NEU (2026-03-28): Token-Clicks im Preview zum Variable-Wechsel
        // User kann Tokens im Preview klicken, um andere Variablen zu bearbeiten
        $overlay.on('click.universal', '.creator-preview-canvas .template-var[data-var], .creator-preview-canvas .js-probability-var[data-var]', function(e) {
            e.stopPropagation();
            e.preventDefault();

            const clickedToken = this;
            const newVarName = clickedToken.dataset.var || clickedToken.dataset.varKey;

            if (!newVarName) {
                console.warn('[Preview Token Click] No var name found!');
                return;
            }

            // Don't switch if it's the same variable
            if (newVarName === config.varName) {
                console.log('[Preview Token Click] Already editing this variable');
                return;
            }

            console.log('[Preview Token Click] Switching to variable:', newVarName);
            switchEditorVariable(newVarName, config);
        });

        // Close overlay when clicking outside
        $overlay.on('click.universal', function(e) {
            if (e.target === overlayEl) {
                closeUnifiedOverlay();
                if (typeof onClose === 'function') onClose();
            }
        });

        // ESC key to close
        window.$(document).on('keydown.universal', function(e) {  // Use jQuery
            if (e.key === 'Escape') {
                closeUnifiedOverlay();
                if (typeof onClose === 'function') onClose();
            }
        });
    }

    /**
     * Updates the value in the appropriate context (Step or Probability) - UNIFIED Object-based
     */
    function updateContextValue(context, varName, value, extras) {
        if (context.type === 'step') {
            // Smart Step Creator: Update activeStep and re-render
            if (!activeStep) {
                console.error('[updateContextValue] No activeStep available!');
                return;
            }

            // Save value to activeStep
            activeStep.values[varName] = value;

            // Handle extras (e.g., pronoun for state variables)
            if (extras && extras.pronoun) {
                activeStep.values['pronoun'] = extras.pronoun;
            }

            // Re-render step text
            preserveWindowScroll(() => {
                renderMasterText();
            });

        } else if (context.type === 'probability') {
            // Probability Area: Update probability state object and re-render (UNIFIED!)
            const masterId = context.probabilityMasterId || context.masterId;

            // Get probability state from global dictionary (in CreatePostingPage.js)
            if (!window.probabilityStates || !window.probabilityStates[masterId]) {
                console.error('[updateContextValue] No probability state found for:', masterId);
                return;
            }

            const prob = window.probabilityStates[masterId];

            // Save value to probability state (wie activeStep!)
            prob.values[varName] = value;

            // Handle extras (e.g., pronoun for state variables)
            if (extras && extras.pronoun) {
                prob.values['pronoun'] = extras.pronoun;
            }

            // Re-render probability template (wie renderMasterText!)
            if (typeof window.renderProbabilityTemplate === 'function') {
                window.renderProbabilityTemplate(masterId);
            } else {
                console.error('[updateContextValue] renderProbabilityTemplate not available!');
            }
        }
    }

    /**
     * Updates the preview in the overlay header with fresh content from DOM
     * NEU (2026-03-28): Preview soll nach Wert-Auswahl aktualisiert werden!
     */
    function updateOverlayPreview(context) {
        const overlay = document.getElementById('universalEditorOverlay');
        if (!overlay) {
            return; // Overlay nicht offen
        }

        const previewSection = overlay.querySelector('.creator-preview-canvas > div');
        if (!previewSection) {
            console.warn('[updateOverlayPreview] Preview section not found in overlay');
            return;
        }

        let sourcePreviewHtml = '';

        if (context.type === 'step') {
            // Get updated preview from Step Creator DOM
            const currentStepWrap = document.querySelector(".current-step-wrap");
            if (currentStepWrap) {
                sourcePreviewHtml = currentStepWrap.innerHTML;
            }
        } else if (context.type === 'probability') {
            // Get updated preview from Probability Area DOM
            const masterId = context.probabilityMasterId || context.masterId;
            if (masterId) {
                const templateCard = document.querySelector(`.probability-template-wrap[data-master-id="${CSS.escape(masterId)}"] .probability-template-card`);
                if (templateCard) {
                    sourcePreviewHtml = templateCard.innerHTML;
                }
            }
        }

        if (sourcePreviewHtml) {
            previewSection.innerHTML = sourcePreviewHtml;
            console.log('[updateOverlayPreview] Preview updated for context:', context.type);
        } else {
            console.warn('[updateOverlayPreview] No source preview HTML found');
        }
    }

    /**
     * Applies the current editor value (unified for both contexts)
     */
    function applyUnifiedEditorValue(editorEl, config) {
        const { varName, masterId, context, onApply } = config;

        if (!editorEl) {
            console.error("[Unified Apply] Editor element not found!");
            return;
        }

        // Extract value using shared helper
        let value = applyEditorValue(editorEl);

        // Extract extras (pronoun for state vars, etc.)
        const extras = applyEditorExtras(editorEl);

        // Handle multi-ingredients with special "add to list" workflow
        if (isIngredientVariable(varName)) {
            const existingMulti = getMultiIngredientsForContext(context, varName);
            const selectedValues = JSON.parse(editorEl.dataset.selectedIngredientValues || '[]');

            console.log('[Unified Apply] Multi-ingredient workflow:', {
                existingMulti: existingMulti.length,
                selectedValues: selectedValues.length
            });

            // If we already have ingredients in the list
            if (existingMulti && existingMulti.length > 0) {
                // User selected NEW ingredients to add
                if (selectedValues && selectedValues.length > 0) {
                    console.log('[Unified Apply] Adding new ingredients to existing list');

                    const article = editorEl.dataset.selectedArticle || '';
                    const fraction = editorEl.dataset.selectedFraction || '';

                    const newNormalized = normalizeIngredientValues(selectedValues);
                    newNormalized.forEach(item => {
                        if (!item.article) item.article = article;
                        if (!item.fraction) item.fraction = fraction;
                    });

                    // Combine existing + new
                    const combinedList = [...existingMulti, ...newNormalized];
                    saveMultiIngredientsForContext(context, varName, combinedList);

                    const ingredientComposed = formatSelectedIngredientList(combinedList, currentLang || 'de');

                    // UPDATE CONTEXT (Step or Probability Token)
                    updateContextValue(context, varName, ingredientComposed, extras);

                    // ✅ NEU (2026-03-28): Preview aktualisieren nach Multi-Ingredient-Hinzufügen
                    updateOverlayPreview(context);

                    // Call onApply callback for custom logic (optional)
                    if (typeof onApply === 'function') {
                        onApply(ingredientComposed, extras);
                    }

                    // ✅ NEU (2026-03-28): Switch editor statt Close+Reopen (Overlay bleibt offen!)
                    config.currentVal = ingredientComposed;
                    switchEditorVariable(varName, config);
                    return;  // ← Important: Overlay bleibt offen!
                } else {
                    // No new selection - just use existing list and CLOSE
                    console.log('[Unified Apply] No new selection, closing with existing list');
                    const normalized = normalizeIngredientValues(existingMulti);
                    value = formatSelectedIngredientList(normalized, currentLang || 'de');
                    // Continue to normal context update + close below
                }
            } else {
                // First time - store initial selection
                if (selectedValues && selectedValues.length > 0) {
                    const article = editorEl.dataset.selectedArticle || '';
                    const fraction = editorEl.dataset.selectedFraction || '';

                    const normalized = normalizeIngredientValues(selectedValues);
                    normalized.forEach(item => {
                        if (!item.article || item.article === '') item.article = article;
                        if (!item.fraction || item.fraction === '') item.fraction = fraction;
                    });

                    saveMultiIngredientsForContext(context, varName, normalized);
                    value = formatSelectedIngredientList(normalized, currentLang || 'de');
                    console.log("[Unified Apply] Stored initial multi-ingredients:", normalized);
                }
            }
        }

        // UPDATE CONTEXT (Step or Probability Token) - UNIFIED for all cases
        updateContextValue(context, varName, value, extras);

        // ✅ NEU (2026-03-28): Preview im Overlay-Header aktualisieren!
        // Nach updateContextValue() wurde renderMasterText/renderProbabilityTemplate aufgerufen
        // → DOM wurde aktualisiert → Preview aus DOM in Overlay kopieren
        updateOverlayPreview(context);

        // Call onApply callback for custom extra logic (optional)
        if (typeof onApply === 'function') {
            onApply(value, extras);
        }

        // ✅ NEU (2026-03-28): Overlay bleibt OFFEN!
        // User kann jetzt andere Tokens im Preview klicken, um weitere Variablen zu bearbeiten
        // "Schließen"-Button zum manuellen Beenden
        // NOTE: onClose callback wird NICHT aufgerufen, da Overlay offen bleibt
    }

    /**
     * Handles plus button click for multi-ingredients (unified)
     */
    function handleUnifiedPlusButtonClick(editorEl, config) {
        const { varName, context } = config;

        const selectedValues = JSON.parse(editorEl.dataset.selectedIngredientValues || '[]');
        if (!selectedValues || !selectedValues.length) {
            return;
        }

        const article = editorEl.dataset.selectedArticle || '';
        const fraction = editorEl.dataset.selectedFraction || '';

        const normalized = normalizeIngredientValues(selectedValues);
        normalized.forEach(item => {
            if (!item.article || item.article === '') item.article = article;
            if (!item.fraction || item.fraction === '') item.fraction = fraction;
        });

        // Get existing list and add new items
        const existingList = getMultiIngredientsForContext(context, varName);
        const combinedList = [...existingList, ...normalized];
        saveMultiIngredientsForContext(context, varName, combinedList);

        const composed = formatSelectedIngredientList(combinedList, currentLang || 'de');

        console.log("[Unified Plus] Added ingredients:", { normalized, combinedList, composed });

        // UPDATE CONTEXT (unified)
        updateContextValue(context, varName, composed, {});

        // ✅ NEU (2026-03-28): Preview aktualisieren nach Multi-Ingredient-Änderung
        updateOverlayPreview(context);

        // ✅ NEU (2026-03-28): Switch editor statt Close+Reopen (Overlay bleibt offen!)
        config.currentVal = composed;
        switchEditorVariable(varName, config);
    }

    /**
     * Switches to a different variable in the overlay without closing
     * NEU (2026-03-28): Tokens im Preview sind klickbar!
     */
    function switchEditorVariable(newVarName, config) {
        const overlayEl = document.getElementById('universalEditorOverlay');
        if (!overlayEl) {
            console.error('[Switch Variable] Overlay not found!');
            return;
        }

        const { context, masterId } = config;

        // Get current value for new variable
        let newVal = '';
        if (context.type === 'step' && activeStep) {
            newVal = activeStep.values[newVarName] || '';
        } else if (context.type === 'probability') {
            const prob = window.probabilityStates[masterId];
            if (prob && prob.values) {
                newVal = prob.values[newVarName] || '';
            }
        }

        // Get multi-ingredients for new variable
        const multiIngredients = getMultiIngredientsForContext(context, newVarName);

        // Generate new editor HTML
        const { html, dataAttributes } = _generateEditorHtml(newVarName, newVal, {
            masterId: masterId,
            multiIngredients: multiIngredients,
            useClassBasedIds: false,
            suppressPronounButtons: false
        });

        // Find editor container
        const editorContainer = overlayEl.querySelector('#universalEditorContent');
        if (!editorContainer) {
            console.error('[Switch Variable] Editor container not found!');
            return;
        }

        // Split editor HTML (remove buttons, they stay in footer)
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = html;
        const buttonRow = tempDiv.querySelector('.js-editor-action-row');
        if (buttonRow) buttonRow.remove();
        const editorContentHtml = tempDiv.innerHTML;

        // Replace editor content
        const innerDiv = editorContainer.querySelector(':scope > div');
        if (innerDiv) {
            innerDiv.innerHTML = editorContentHtml;
        }

        // Get new editor element and set data attributes
        const newEditorEl = editorContainer.querySelector('.duration-editor');
        if (newEditorEl) {
            Object.keys(dataAttributes).forEach(key => {
                newEditorEl.dataset[key] = dataAttributes[key];
            });
        }

        // Update config
        config.varName = newVarName;
        config.currentVal = newVal;

        console.log('[Switch Variable] Switched to:', newVarName);
    }

    /**
     * Closes the unified overlay
     */
    function closeUnifiedOverlay() {
        const overlay = document.getElementById('universalEditorOverlay');
        if (overlay) overlay.remove();

        // Restore body scroll
        document.body.style.overflow = '';

        // Remove ESC key handler
        window.$(document).off('keydown.universal');  // Use jQuery
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // ── END UNIFIED OVERLAY SYSTEM ──
    // ══════════════════════════════════════════════════════════════════════════════

    // ── Open Inline Editor (Step Creator) ── PHASE 5: Migrated to Unified System
    function openInlineEditor(varName, tokenId) {
        if (!activeStep) {
            console.warn("[openInlineEditor] No activeStep available");
            return;
        }

        // Set activeToken for backward compatibility (used by old code)
        activeToken = { varName, tokenId };

        const currentVal = activeStep.values[varName] ?? "";
        const masterId = activeStep.master_id || "";

        // Call unified overlay system
        openUniversalVariableEditor({
            varName: varName,
            currentVal: currentVal,
            masterId: masterId,
            context: {
                type: 'step',
                tokenId: tokenId
            },
            onApply: function(newVal, extras) {
                // Context update is handled by updateContextValue() in unified apply
                // Editor closing is handled by unified apply (closeUnifiedOverlay)
                // This callback is only for custom extra logic if needed
            },
            onClose: function() {
                // Clean up activeToken when editor is closed
                activeToken = null;
            }
        });
    }


    function renderFallbackSection(varName, filteredOptions, contextMasterId, context) {
        if (isIngredientVariable(varName) || isCompactSpecialVariable(varName)) return "";
        const optionsMasterId = (contextMasterId || activeStep?.master_id || "").toString();
        const allOptions = getRawVarOptions(varName, optionsMasterId, context);
        const filteredSet = new Set((filteredOptions || []).map(normalizeOptionValue));
        const extras = allOptions.filter(o => !filteredSet.has(normalizeOptionValue(o)));
        if (!extras.length) return "";
        const extraButtons = renderPillButtons(extras, "value", null);
        return `
        <div class="mt-2">
          <button type="button" class="btn btn-sm btn-outline-secondary js-toggle-fallback-options" data-expanded="0">Weitere anzeigen ▼</button>
          <div class="d-flex flex-wrap gap-2 mt-2 js-fallback-options-wrap" style="display:none !important;">
            ${extraButtons}
          </div>
        </div>`;
    }

    function renderPillButtons(list, mode, currentVal) {
        if (!Array.isArray(list) || list.length === 0) return "";

        return list.map(val => {
            const v = (val ?? "").toString();
            const isActive = currentVal && v === currentVal;
            // Style: wie dein "Einsetzen"-Button Look -> machst du über CSS Klasse "pill-like"
            return `
        <button type="button"
                class="btn btn-sm btn-outline-light pill-like ${isActive ? "active" : ""}"
                data-pick-mode="${escapeHtml(mode)}"
                data-pick-value="${escapeHtml(v)}">
          ${escapeHtml(v === "" ? "ohne" : v)}
        </button>
      `;
        }).join("");
    }

    function renderSpecialEditor(varName, currentVal) {
        // duration -> Input + unit buttons (minute/hour) oder dropdown
        if (varName === "duration") {
            const units = getDurationUnits();
            const timeUnits = units.filter(u => u.key !== 'per_package');
            const perPackage = units.find(u => u.key === 'per_package');
            const unitBtns = timeUnits.map(u => `
        <button type="button"
                class="btn btn-sm btn-outline-light pill-like"
                data-duration-unit="${escapeHtml(u.key)}">
          ${escapeHtml(u.label)}
        </button>
      `).join("");

            const perPackageBtn = perPackage ? `
        <div class="mt-2">
          <button type="button"
                  class="btn btn-sm btn-outline-warning pill-like js-duration-per-package"
                  data-duration-unit="per_package">
            ${escapeHtml(perPackage.label)}
          </button>
        </div>` : '';

            const rangeMatch = (currentVal || '').match(/^(\d+)\s*[-–]\s*(\d+)/);
            const fromVal = rangeMatch ? rangeMatch[1] : (extractLeadingNumber(currentVal) || "10");
            const toVal = rangeMatch ? rangeMatch[2] : "";

            return `
        <div class="d-flex gap-2 align-items-center">
          <input type="number" min="1" step="1" id="DurationValueInput"
                 class="form-control form-control-sm"
                 style="max-width:80px; background:#ffffff !important; color:#111827 !important; -webkit-text-fill-color:#111827 !important; caret-color:#111827 !important; text-shadow:none !important;"
                 value="${escapeHtml(fromVal)}" placeholder="Von" />
          <span class="text-muted small">–</span>
          <input type="number" min="1" step="1" id="DurationValueToInput"
                 class="form-control form-control-sm"
                 style="max-width:80px; background:#ffffff !important; color:#111827 !important; -webkit-text-fill-color:#111827 !important; caret-color:#111827 !important; text-shadow:none !important;"
                 value="${escapeHtml(toVal)}" placeholder="Bis" />
        </div>

        <div class="d-flex flex-wrap gap-2 mt-2">
          ${unitBtns}
          ${perPackage ? `<button type="button"
                  class="btn btn-sm btn-outline-warning pill-like js-duration-per-package"
                  data-duration-unit="per_package">
            ${escapeHtml(perPackage.label)}
          </button>` : ''}
        </div>

        <div class="d-flex gap-2 align-items-center mt-2 js-editor-action-row">
          <button type="button" class="btn btn-sm btn-outline-light" id="BtnPickDurationQuick">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVarTop">Schließen</button>
        </div>
      `;
        }

        // temp -> Input + Unit (?C/?F) (du wolltest dropdown)
        if (varName === "temp") {
            return `
        <div class="d-flex gap-2 align-items-center flex-wrap">
          <input type="number" min="0" step="1" id="TempValueInput"
                 class="form-control form-control-sm"
                 style="flex:1 1 180px; min-width:180px; background:#ffffff !important; color:#111827 !important; -webkit-text-fill-color:#111827 !important; caret-color:#111827 !important; text-shadow:none !important;"
                 value="${escapeHtml(extractLeadingNumber(currentVal) || "180")}" />

          <select id="TempUnitSelect" class="form-select form-select-sm" style="flex:0 0 160px; min-width:140px; background:#ffffff !important; color:#111827 !important; -webkit-text-fill-color:#111827 !important; text-shadow:none !important;">
            <option value="C">C</option>
            <option value="F">F</option>
          </select>
        </div>

        <div class="d-flex gap-2 align-items-center mt-2 js-editor-action-row">
          <button type="button" class="btn btn-sm btn-outline-light" id="BtnPickTempQuick">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVarTop">Schließen</button>
        </div>
      `;
        }


        if (varName === "count") {
            return `
        <div class="d-flex gap-2 align-items-center js-editor-action-row">
          <input type="number" min="1" step="1" id="CountValueInput"
                 class="form-control form-control-sm"
                 style="max-width:110px; background:#ffffff !important; color:#111827 !important; -webkit-text-fill-color:#111827 !important; caret-color:#111827 !important; text-shadow:none !important;"
                 value="${escapeHtml(extractLeadingNumber(currentVal) || "1")}" />
          <button type="button" class="btn btn-sm btn-outline-light" id="BtnPickCountQuick">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVarTop">Schließen</button>
        </div>
      `;
        }
        // default: einfache Textbox (falls keine options existieren)
        return `
   
    `;
    }

    function extractLeadingNumber(text) {
        const m = (text ?? "").toString().match(/\d+/);
        return m ? m[0] : "";
    }

    // -----------------------------
    // APPLY VALUE
    // -----------------------------
    function applyCurrentEditorSelection() {
        if (!activeStep || !activeToken) return;

        // Get editor element from fullscreen overlay (or fallback to inline host)
        let host = document.querySelector('#universalEditorOverlay .duration-editor');
        if (!host) host = $("#InlineVarEditorHost");
        if (!host) return;

        const varName = activeToken.varName;

        // Spezialfälle zuerst
        if (varName === "duration") {
            const unit = host.dataset.durationUnit || "minute";
            const labels = getDurationUnits();
            if (unit === "per_package") {
                const perPackageLabel = labels.find(x => x.key === "per_package")?.label ?? "laut Packungsanweisung";
                activeStep.values[varName] = perPackageLabel;
            } else {
                const n = $("#DurationValueInput")?.value?.trim() || "";
                const nTo = $("#DurationValueToInput")?.value?.trim() || "";
                const unitLabel = labels.find(x => x.key === unit)?.label ?? unit;
                const numPart = (n && nTo && nTo !== n) ? `${n}-${nTo}` : n;
                const composed = numPart ? `${numPart} ${unitLabel}` : "";
                if (composed) activeStep.values[varName] = composed;
            }
            rerenderAfterValueSet();
            return;
        }

        if (varName === "temp") {
            const n = $("#TempValueInput")?.value?.trim() || "";
            const u = $("#TempUnitSelect")?.value || "C";
            const composed = n ? `${n} ${u}` : "";
            if (composed) activeStep.values[varName] = composed;
            rerenderAfterValueSet();
            return;
        }


        if (varName === "count") {
            const n = $("#CountValueInput")?.value?.trim() || "1";
            const normalized = /^\d+$/.test(n) ? n : "1";
            activeStep.values[varName] = normalized;
            rerenderAfterValueSet();
            return;
        }
        // Normalfall: Artikel + Wert
        const article = host.dataset.selectedArticle ?? "";
        const pronoun = host.dataset.selectedPronoun ?? "";
        let value = host.dataset.selectedValue ?? "";
        const noArticleVar = isNoArticleVariable(varName);
        const stateVar = isStateVariable(varName);

        if (isIngredientVariable(varName)) {
            // Check if we have multi-ingredients from the "Hinzufügen" workflow (per variable)
            const multiIngredientsObj = activeStep._multiIngredients || {};
            const multiIngredients = multiIngredientsObj[varName] || [];
            const article = host.dataset.selectedArticle ?? "";
            const fraction = host.dataset.selectedFraction ?? "";
            const selectedValues = JSON.parse(host.dataset.selectedIngredientValues || "[]");

            console.log('[applyCurrentEditorSelection] multiIngredients:', multiIngredients, 'selectedValues:', selectedValues);

            if (multiIngredients && multiIngredients.length > 0) {
                // Check if user selected NEW ingredients to add
                if (selectedValues && selectedValues.length > 0) {
                    // User selected new ingredients - ADD to existing list
                    console.log('[applyCurrentEditorSelection] Adding new ingredients to existing list');

                    const newNormalized = normalizeIngredientValues(selectedValues);
                    newNormalized.forEach(item => {
                        if (!item.article) item.article = article;
                        if (!item.fraction) item.fraction = fraction;
                    });

                    // Combine existing + new
                    const combinedList = [...multiIngredients, ...newNormalized];
                    console.log('[applyCurrentEditorSelection] combinedList:', combinedList);

                    // Format the combined list
                    let ingredientComposed = formatSelectedIngredientList(combinedList, currentLang);
                    console.log('[applyCurrentEditorSelection] ingredientComposed:', ingredientComposed);

                    // Store fraction data
                    const hasFractions = combinedList.some(item => item.fraction && item.fraction !== "");
                    if (hasFractions) {
                        const frOpts = getFractionOptions();
                        activeStep._fractionData = combinedList
                            .filter(item => item.fraction)
                            .map(item => {
                                const fr = frOpts.find(f => f.key === item.fraction);
                                return {
                                    ingredientName: item.name,
                                    fractionKey: item.fraction,
                                    numerator: fr?.numerator || 1,
                                    denominator: fr?.denominator || 1
                                };
                            });
                    } else {
                        activeStep._fractionData = null;
                    }

                    activeStep.values[varName] = ingredientComposed;
                    // Store per variable
                    if (!activeStep._multiIngredients) activeStep._multiIngredients = {};
                    activeStep._multiIngredients[varName] = combinedList;
                    console.log('[applyCurrentEditorSelection] Updated _multiIngredients for', varName, ':', activeStep._multiIngredients[varName]);

                    // Re-open editor to show updated chips (don't close!)
                    renderMasterText();
                    openInlineEditor(varName, activeToken.tokenId);
                    return;
                } else {
                    // No new selection - just use existing list and CLOSE editor
                    console.log('[applyCurrentEditorSelection] No new selection, closing editor with existing list');
                    const normalized = normalizeIngredientValues(multiIngredients);
                    let ingredientComposed = formatSelectedIngredientList(normalized, currentLang);

                    const hasFractions = normalized.some(item => item.fraction && item.fraction !== "");
                    if (hasFractions) {
                        const frOpts = getFractionOptions();
                        activeStep._fractionData = normalized
                            .filter(item => item.fraction)
                            .map(item => {
                                const fr = frOpts.find(f => f.key === item.fraction);
                                return {
                                    ingredientName: item.name,
                                    fractionKey: item.fraction,
                                    numerator: fr?.numerator || 1,
                                    denominator: fr?.denominator || 1
                                };
                            });
                    } else {
                        activeStep._fractionData = null;
                    }

                    activeStep.values[varName] = ingredientComposed;
                    // Store per variable
                    if (!activeStep._multiIngredients) activeStep._multiIngredients = {};
                    activeStep._multiIngredients[varName] = normalized;

                    rerenderAfterValueSet();
                    return;
                }
            }

            // Fallback: Single-ingredient selection (old workflow)
            // article, fraction, selectedValues already declared above
            console.log('[applyCurrentEditorSelection - Fallback] selectedValues:', selectedValues);

            // Hybrid: if no ingredient chips selected, fall through to regular value logic
            if (!Array.isArray(selectedValues) || !selectedValues.length) {
                if (!isHybridIngredientVariable(varName)) return;
                // fall through to normal value handling below
            } else {
                // Single ingredient with article/fraction
                const normalized = normalizeIngredientValues(selectedValues);
                normalized.forEach(item => {
                    if (!item.article) item.article = article;
                    if (!item.fraction) item.fraction = fraction;
                });

                console.log('[applyCurrentEditorSelection] normalized:', normalized);

                // Check if we should ADD to existing multi-ingredients or create new (per variable)
                const multiIngredientsObj = activeStep._multiIngredients || {};
                const existingMulti = multiIngredientsObj[varName] || [];
                let combinedList = existingMulti.length > 0 ? [...existingMulti, ...normalized] : normalized;
                console.log('[applyCurrentEditorSelection] existingMulti for', varName, ':', existingMulti, 'combinedList:', combinedList);

                // Format the ingredient list
                let ingredientComposed = formatSelectedIngredientList(combinedList, currentLang);
                console.log('[applyCurrentEditorSelection] ingredientComposed:', ingredientComposed);

                // Store fraction data
                const hasFractions = combinedList.some(item => item.fraction && item.fraction !== "");
                if (hasFractions) {
                    const frOpts = getFractionOptions();
                    activeStep._fractionData = combinedList
                        .filter(item => item.fraction)
                        .map(item => {
                            const fr = frOpts.find(f => f.key === item.fraction);
                            return {
                                ingredientName: item.name,
                                fractionKey: item.fraction,
                                numerator: fr?.numerator || 1,
                                denominator: fr?.denominator || 1
                            };
                        });
                } else {
                    activeStep._fractionData = null;
                }

                activeStep.values[varName] = ingredientComposed;

                // Store combined list in _multiIngredients for further additions via Plus button (per variable)
                if (!activeStep._multiIngredients) activeStep._multiIngredients = {};
                activeStep._multiIngredients[varName] = combinedList;
                console.log('[applyCurrentEditorSelection - Fallback] Stored in _multiIngredients for', varName, ':', activeStep._multiIngredients[varName]);

                rerenderAfterValueSet();
                return;
            }
        }

        let composed = value;

        // Artikel nur wenn nicht "ohne"
        if (!noArticleVar && article && article !== "ohne") {
            composed = `${article} ${value}`.trim();
        }

        const hasSepPronoun = /\{\{\s*pronoun\s*\}\}/i.test(activeStep?.templateRaw || "");
        const pronounVar = (varName || '').toLowerCase() === 'pronoun';

        // If editing pronoun variable directly
        if (pronounVar) {
            // Check if there's a separate {{state}} token in the template
            const hasSepState = /\{\{\s*state\s*\}\}/i.test(activeStep?.templateRaw || "");

            // Save pronoun
            if (pronoun) {
                activeStep.values["pronoun"] = pronoun;
            }

            // If state selected AND there's a separate {{state}} token, save it there too
            if (value && hasSepState) {
                activeStep.values["state"] = value;
            }
            // If state selected but NO separate {{state}} token, combine them
            else if (value && !hasSepState) {
                const combined = `${pronoun} ${value}`.trim();
                activeStep.values["pronoun"] = combined;
            }

            rerenderAfterValueSet();
            return;
        }

        // Combined pronoun+state editor: handle both selections
        if (stateVar) {
            // Always save pronoun if selected (even without state)
            if (pronoun) {
                activeStep.values["pronoun"] = pronoun;
            }

            // If both pronoun and state selected, compose them
            if (pronoun && value) {
                if (!hasSepPronoun) {
                    composed = `${pronoun} ${value}`.trim();
                }
            }

            // If no value selected, just save pronoun and return
            if (!value) {
                rerenderAfterValueSet();
                return;
            }
        }

        // wenn nur artikel geklickt aber kein value -> nix setzen (außer pronoun wurde schon gesetzt)
        if (!value) return;

        // Sequential editing DISABLED: User can fill pronoun and state independently
        // const isPronounToken = varName === "pronoun";
        // const templateRaw = activeStep?.templateRaw || "";
        // const stateUnfilled = !((activeStep.values["state"] || "").toString().trim());
        // const shouldAutoState = isPronounToken &&
        //     /\{\{\s*state\s*\}\}/i.test(templateRaw) &&
        //     stateUnfilled;

        activeStep.values[varName] = composed;

        // Auch {pronoun}-Token setzen falls im Template vorhanden
        if (stateVar && pronoun) {
            activeStep.values["pronoun"] = pronoun;
        }

        rerenderAfterValueSet();

        // if (shouldAutoState) {
        //     const stateToken = document.querySelector("#CurrentStepText .placeholder-token[data-var='state']");
        //     if (stateToken) {
        //         openInlineEditor("state", stateToken.dataset.tokenId);
        //     }
        // }
    }

    function rerenderAfterValueSet() {
        preserveWindowScroll(() => {
            // Step-Text neu rendern (Tokens die gesetzt sind werden zu Text)
            renderMasterText();

            // Editor schließen
            closeInlineEditor();
        });
    }

    function getRenderedTextForLang(step, lang) {
        const langKey = (lang || DEFAULT_LANG).toLowerCase();
        const templateRaw = step?.templates?.[langKey] ?? step?.templates?.[DEFAULT_LANG] ?? activeStep?.templateRaw ?? "";
        const values = { ...(activeStep?.values || {}) };
        if (window.CreatePostingIngredientHelpers && typeof window.CreatePostingIngredientHelpers.localizeIngredientValueForSandbox === "function") {
            ["ingredient", "ingredient2", "ingredients", "liquid", "fat"].forEach(function (key) {
                if (!values[key]) return;
                const localized = window.CreatePostingIngredientHelpers.localizeIngredientValueForSandbox(values[key], langKey, currentLang);
                if (localized) values[key] = localized;
            });
        }
        const rendered = renderTemplate(templateRaw, activeStep?.master_id || "step", values);
        const temp = document.createElement("div");
        temp.innerHTML = rendered || "";
        temp.querySelectorAll(".placeholder-reset, .optional-inline-pill").forEach(el => el.remove());
        return (temp.textContent || temp.innerText || "").replace(/\s+/g, " ").trim();
    }

    function resolveAcceptedIngredientName() {
        if (activeStep?.master_id === "PREP_SEPARATE_01") {
            const parts = [activeStep?.values?.ingredient, activeStep?.values?.ingredient2].map(x => (x || "").toString().trim()).filter(Boolean);
            if (parts.length) return parts.join(", ");
        }
        const fromValues =
            activeStep?.values?.ingredient ||
            activeStep?.values?.ingredients ||
            activeStep?.values?.liquid ||
            activeStep?.values?.fat ||
            "";
        const text = (fromValues || "").toString().trim();
        if (text) return text;
        return formatSelectedIngredientList(getSelectedIngredientNamesFromPage(), currentLang);
    }

    function slugifyStableKey(value) {
        return (value || "")
            .toString()
            .trim()
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/[^a-z0-9]+/g, "_")
            .replace(/^_+|_+$/g, "");
    }

    function normalizeToArray(value) {
        if (Array.isArray(value)) return value.filter(Boolean).map(x => x.toString());
        if (value == null) return [];
        const single = value.toString().trim();
        return single ? [single] : [];
    }

    function getStableOptionReference(varName, rawValue) {
        const value = (rawValue || "").toString().trim();
        if (!value) return null;

        const options = getVarOptions(varName);
        if (Array.isArray(options) && options.length) {
            const index = options.findIndex(x => (x || "").toString().trim().toLowerCase() === value.toLowerCase());
            if (index >= 0) {
                return {
                    source: "json_option",
                    key: `${slugifyStableKey(varName)}:${index}`
                };
            }
        }

        if (varName === "pronoun" || varName === "articles") {
            return {
                source: "grammar",
                key: `${slugifyStableKey(varName)}:${slugifyStableKey(value)}`
            };
        }

        if (varName === "count") {
            return {
                source: "numeric",
                key: `count:${value}`
            };
        }

        if (varName === "duration" || varName === "temp") {
            return {
                source: "normalized_scalar",
                key: `${slugifyStableKey(varName)}:${slugifyStableKey(value)}`
            };
        }

        return null;
    }

    function buildStableStepReference(step) {
        const stableTaxonomy = step?.stable_taxonomy_keys || {};
        const reference = {
            schema_version: "2.0.0",
            master_step_key: (step?.master_id || activeStep?.master_id || "").toString(),
            action_key: (step?.action || "").toString(),
            step_intent_key: (step?.step_intent || "").toString(),
            phase_key: (step?.phase ?? 0).toString(),
            equipment_key: (step?.equipment ?? 0).toString(),
            taxonomy: {
                category_keys: normalizeToArray(stableTaxonomy.category_keys),
                diet_keys: normalizeToArray(stableTaxonomy.diet_keys),
                ingredient_family_keys: normalizeToArray(stableTaxonomy.ingredient_family_keys),
                keyword_keys: normalizeToArray(stableTaxonomy.keyword_keys),
                quality_rule_keys: normalizeToArray(step?.quality_rule_keys)
            },
            variables: {}
        };

        const variableTypes = step?.variable_types || {};
        const values = activeStep?.values || {};
        Object.keys(values).forEach(function (varName) {
            const displayValue = (values[varName] || "").toString().trim();
            if (!displayValue) return;

            const stableOption = getStableOptionReference(varName, displayValue);
            reference.variables[varName] = {
                type_key: (variableTypes[varName] || "").toString(),
                display_value: displayValue,
                reference_key: stableOption?.key || null,
                reference_source: stableOption?.source || "display_only"
            };
        });

        return reference;
    }

    /**
     * Saves current editor value before accepting a step (shared for Step Creator & Probability)
     * NEU (2026-03-28): Gemeinsame Funktion für beide Accept-Flows
     */
    function saveCurrentEditorValueBeforeAccept(contextType, contextId) {
        const overlay = document.getElementById('universalEditorOverlay');
        if (!overlay || overlay.dataset.overlayOwner !== contextType) return false;

        const currentEditorEl = overlay.querySelector('.duration-editor');
        if (!currentEditorEl) return false;

        const varName = currentEditorEl.dataset.editorFor;
        if (!varName) return false;

        // Extract current editor value
        const value = applyEditorValue(currentEditorEl);
        const extras = applyEditorExtras(currentEditorEl);

        // Save based on context type
        if (contextType === 'step') {
            if (!activeStep) return false;
            activeStep.values[varName] = value;
            if (extras && extras.pronoun) {
                activeStep.values['pronoun'] = extras.pronoun;
            }
            preserveWindowScroll(() => {
                renderMasterText();
            });
        } else if (contextType === 'probability') {
            const masterId = contextId;
            if (!window.probabilityStates || !window.probabilityStates[masterId]) return false;
            window.probabilityStates[masterId].values[varName] = value;
            if (extras && extras.pronoun) {
                window.probabilityStates[masterId].values['pronoun'] = extras.pronoun;
            }
            if (typeof window.renderProbabilityTemplate === 'function') {
                window.renderProbabilityTemplate(masterId);
            }
        }

        return true; // Value was saved
    }

    function acceptActiveStep() {
        if (!activeStep) return;

        // ✅ NEU (2026-03-28): Gemeinsame Funktion für Editor-Wert speichern
        const wasSaved = saveCurrentEditorValueBeforeAccept('step', null);
        if (wasSaved) {
            closeUnifiedOverlay();
        }

        const step = steps.find(s => (s?.master_id || "") === (activeStep.master_id || ""));
        const payload = {
            de: getRenderedTextForLang(step, "de"),
            en: getRenderedTextForLang(step, "en"),
            esp: getRenderedTextForLang(step, "esp"),
            prt: getRenderedTextForLang(step, "prt"),
            phase: parseInt(step?.phase ?? 0, 10) || 0,
            equipment: parseInt(step?.equipment ?? 0, 10) || 0,
            masterTemplateId: (step?.master_id || activeStep.master_id || "").toString(),
            stableReference: buildStableStepReference(step)
        };
        const textCurrent = payload[currentLang] || payload.de || payload.en || "";
        const ingredientName = resolveAcceptedIngredientName();

        if (typeof window.addStep === "function") {
            const generatedId =
                typeof window.createFallbackStepId === "function"
                    ? window.createFallbackStepId()
                    : uid("smart_step");
            window.addStep(String(generatedId), null, textCurrent || `Schritt ${generatedId}`, {
                skipRender: true,
                ingredientName,
                masterTemplateId: payload.masterTemplateId,
                stepData: payload,
                fractionData: activeStep._fractionData || null
            });
            if (typeof window.updateStepIndices === "function") {
                window.updateStepIndices();
            } else if (typeof window.updateStoryProgress === "function") {
                window.updateStoryProgress();
            }

            // ✅ NEU (2026-03-28): Transformationen nach Step-Accept anwenden (z.B. Eiweiß → Eischnee)
            if (typeof window.refreshMasterTemplateBuilder === "function") {
                window.refreshMasterTemplateBuilder();
            }

            return;
        }

        const selected = document.querySelector("#selectedSteps");
        if (!selected) return;
        selected.insertAdjacentHTML(
            "beforeend",
            `<div class="dynamic-item d-flex align-items-center step-row"><div class="small flex-grow-1"><span class="step-text-content">${escapeHtml(textCurrent || "Schritt")}</span></div></div>`
        );
    }

    // -----------------------------
    // EVENTS
    // -----------------------------
    function onStepCardClick(btn) {
        const stepId = btn.dataset.stepId || "";
        const title = btn.dataset.title || "";
        const templateRaw = decodeAttr(btn.dataset.templateRaw || "");

        if (!stepId || !templateRaw) return;

        activeStep = {
            master_id: stepId,
            title,
            templateRaw,
            values: {}
        };

        closeInlineEditor();
        renderMasterText();
        setActiveButton(stepId);
    }

    function setActiveButton(stepId) {
        document.querySelectorAll(".template-card.active").forEach(x => x.classList.remove("active"));
        const btn = document.querySelector(`.template-card[data-step-id="${CSS.escape(stepId)}"]`);
        if (btn) btn.classList.add("active");
    }

    function wireEvents() {
        document.addEventListener("click", (e) => {
            // Step Card
            const card = e.target.closest(".template-card");
            if (card) {
                onStepCardClick(card);
                return;
            }

                        // Token in MasterText
            const token = e.target.closest(".template-var");
            if (token) {
                if (!activeStep) return;
                const varName = token.dataset.var || token.dataset.placeholderKey || token.dataset.var;
                const tokenId = token.dataset.tokenId || token.dataset.placeholderTokenId || token.dataset.tokenId;
                // Sequential editing: {state} clicked with separate unfilled {pronoun} → open pronoun first
                if (isStateVariable(varName) && /\{\{\s*pronoun\s*\}\}/i.test(activeStep.templateRaw || "")) {
                    const pronounToken = document.querySelector("#CurrentStepText .placeholder-token[data-var='pronoun']");
                    if (pronounToken && !((activeStep.values["pronoun"] || "").toString().trim())) {
                        openInlineEditor("pronoun", pronounToken.dataset.tokenId);
                        return;
                    }
                }
                openInlineEditor(varName, tokenId);
                return;
            }

            const optionalAdd = e.target.closest(".js-optional-var-add");
            if (optionalAdd) {
                if (!activeStep) return;
                const varName = (optionalAdd.dataset.optionalVar || "").toString();
                const tokenId = (optionalAdd.dataset.tokenId || uid("optional")).toString();
                if (!varName) return;
                openInlineEditor(varName, tokenId);
                return;
            }

            // Reset button near token
            const reset = e.target.closest(".placeholder-reset");
            if (reset && activeStep) {
                const varName = (reset.dataset.var || "").toString().trim();
                if (varName) {
                    delete activeStep.values[varName];
                    renderMasterText();
                    closeInlineEditor();
                }
                return;
            }

            // "Weitere anzeigen" toggle
            const fallbackToggle = e.target.closest(".js-toggle-fallback-options");
            if (fallbackToggle) {
                const wrap = fallbackToggle.parentElement?.querySelector(".js-fallback-options-wrap");
                if (wrap) {
                    const expanded = fallbackToggle.dataset.expanded === "1";
                    wrap.style.cssText = expanded ? "display:none !important;" : "display:flex !important; flex-wrap:wrap;";
                    fallbackToggle.dataset.expanded = expanded ? "0" : "1";
                    fallbackToggle.textContent = expanded ? "Weitere anzeigen \u25BC" : "Weitere ausblenden \u25B2";
                }
                return;
            }

            if (e.target.id === "btnAcceptStep") {
                acceptActiveStep();
                return;
            }

            // Editor Close Buttons
            if (e.target.id === "BtnCloseVar" || e.target.id === "BtnCloseVarTop") {
                closeInlineEditor();
                return;
            }

            // Apply Button
            if (e.target.id === "BtnApplyVar") {
                applyCurrentEditorSelection();
                return;
            }

            // ✅ ENTFERNT (2026-03-28): Plus-Button Event-Handler nicht mehr nötig
            // Alle Multi-Ingredient-Bearbeitung läuft jetzt über das Unified Overlay
            // mit dem grünen Plus-Button im Overlay-Footer
            /*
            // ── Plus Button: Open Editor or Add Ingredient ──
            const plusBtn = e.target.closest(".ingredient-plus-btn");
            if (plusBtn) {
                // ... (alter Code für Plus-Button)
                return;
            }
            */

            // ── Remove Multi-Ingredient Chip ──
            const removeChip = e.target.closest("button[data-remove-multi-ingredient]");
            if (removeChip) {
                e.stopPropagation();
                e.preventDefault();

                console.log("[Remove Chip] Clicked!", removeChip);
                const idx = parseInt(removeChip.dataset.removeMultiIngredient, 10);
                console.log("[Remove Chip] idx:", idx, "activeStep:", activeStep, "activeToken:", activeToken);
                if (!activeStep || !activeToken || isNaN(idx)) {
                    console.log("[Remove Chip] Validation failed");
                    return;
                }

                // Get list for current variable
                const multiIngredientsObj = activeStep._multiIngredients || {};
                const existingList = multiIngredientsObj[activeToken.varName] || [];
                if (idx < 0 || idx >= existingList.length) return;

                // Remove item at index
                existingList.splice(idx, 1);
                if (!activeStep._multiIngredients) activeStep._multiIngredients = {};
                activeStep._multiIngredients[activeToken.varName] = existingList;

                // Update preview text
                const composed = formatSelectedIngredientList(existingList, currentLang);
                activeStep.values[activeToken.varName] = composed;

                console.log("[Remove Chip] Updated value:", composed);

                renderMasterText();

                // Update overlay preview if it exists
                const overlay = document.getElementById('universalEditorOverlay');
                if (overlay) {
                    const currentStepWrap = document.querySelector(".current-step-wrap");
                    const previewSection = overlay.querySelector('.creator-preview-canvas > div');
                    if (currentStepWrap && previewSection) {
                        previewSection.innerHTML = currentStepWrap.innerHTML;
                        console.log("[Remove Chip] Updated overlay preview");
                    }
                }

                // Re-render editor to show updated list
                openInlineEditor(activeToken.varName, activeToken.tokenId);

                return;
            }

            // Artikel/Wert Button Picks
            const pickBtn = e.target.closest("button[data-pick-mode]");
            if (pickBtn) {
                // Check fullscreen overlay first, then inline host
                let host = document.querySelector('#universalEditorOverlay .duration-editor');
                if (!host) host = $("#InlineVarEditorHost");
                if (!host) return;
                _handlePickModeClick(pickBtn, host, false); // false = use IDs (Step Creator)
                return;
            }

            // duration unit pick
            const du = e.target.closest("button[data-duration-unit]");
            if (du) {
                let host = document.querySelector('#universalEditorOverlay .duration-editor');
                if (!host) host = $("#InlineVarEditorHost");
                if (!host) return;
                _handleDurationUnitClick(du, host);
                return;
            }

            // quick duration apply
            if (e.target.id === "BtnPickDurationQuick") {
                let host = document.querySelector('#universalEditorOverlay .duration-editor');
                if (!host) host = $("#InlineVarEditorHost");
                if (!host) return;
                if (!host.dataset.durationUnit) host.dataset.durationUnit = "minute";
                const labels = getDurationUnits();
                let composed;
                if (host.dataset.durationUnit === "per_package") {
                    composed = labels.find(x => x.key === "per_package")?.label ?? "laut Packungsanweisung";
                } else if (host.dataset.durationUnit === "short") {
                    composed = labels.find(x => x.key === "short")?.label ?? "kurz";
                } else {
                    const n = $("#DurationValueInput")?.value?.trim() || "";
                    const nTo = $("#DurationValueToInput")?.value?.trim() || "";
                    const unitLabel = labels.find(x => x.key === host.dataset.durationUnit)?.label ?? host.dataset.durationUnit;
                    const numPart = (n && nTo && nTo !== n) ? `${n}-${nTo}` : n;
                    composed = numPart ? `${numPart} ${unitLabel}` : "";
                }

                // direkt übernehmen:
                if (composed) activeStep.values["duration"] = composed;
                rerenderAfterValueSet();
                return;
            }

            if (e.target.id === "BtnPickCountQuick") {
                const n = $("#CountValueInput")?.value?.trim() || "1";
                const normalized = /^\d+$/.test(n) ? n : "1";
                activeStep.values["count"] = normalized;
                rerenderAfterValueSet();
                return;
            }
            // quick temp apply
            if (e.target.id === "BtnPickTempQuick") {
                const n = $("#TempValueInput")?.value?.trim() || "";
                const u = $("#TempUnitSelect")?.value || "C";
                const composed = n ? `${n} ${u}` : "";

                activeStep.values["temp"] = composed;
                rerenderAfterValueSet();
                return;
            }
        });

        // language dropdown
        const ls = langSelect();
        if (ls) {
            ls.addEventListener("change", () => {
                currentLang = (ls.value || DEFAULT_LANG).toLowerCase();

                renderStepButtons();

                // wenn aktiv, template neu holen, aber values behalten (du kannst später language-values bauen)
                if (activeStep) {
                    const step = steps.find(s => s.master_id === activeStep.master_id);
                    activeStep.templateRaw = step?.templates?.[currentLang] ?? "";
                    closeInlineEditor();
                    renderMasterText();
                    setActiveButton(activeStep.master_id);
                }
            });
        }
    }

    // -----------------------------
    // INIT
    // -----------------------------
    async function init() {
        try {
            const loaded = await Promise.all([loadJson(), loadVariableCatalog()]);
            doc = loaded[0];
            variableCatalog = loaded[1] || { variables: {} };
            steps = doc.master_steps || [];

            renderStepButtons();
            renderMasterText();
            wireEvents();
        } catch (err) {
            console.error(err);
            const target = masterText();
            if (target) target.innerHTML = "Fehler beim Laden der Master Templates. Bitte Console prüfen.";
        }
    }

    // ── Inline-editor helpers exposed for the probability area ──────────────────────────────────────
    // buildInlineEditorHtml: same logic as openInlineEditor but returns HTML.
    // Uses class-based rows so multiple host containers don't conflict.
    // ── Build Inline Editor HTML (Probability Area) ──
    // Now uses shared _generateEditorHtml() function for consistency
    function buildInlineEditorHtml(varName, currentVal, opts) {
        const suppressPronounButtons = !!(opts && opts.suppressPronounButtons);
        const optionsMasterId = (opts && opts.masterId) || activeStep?.master_id || "";
        const multiIngredients = (opts && opts.multiIngredients) || [];

        // Generate HTML using shared function with class-based IDs for Probability Area
        const result = _generateEditorHtml(varName, currentVal, {
            multiIngredients,
            masterId: optionsMasterId,
            selectedIngredientValues: null, // Will be parsed from currentVal
            suppressPronounButtons,
            useClassBasedIds: true // Use js-* classes for Probability Area
        });

        // Build data attributes string
        const dataAttrs = Object.keys(result.dataAttributes)
            .map(key => `data-${key.replace(/([A-Z])/g, '-$1').toLowerCase()}="${escapeHtml(result.dataAttributes[key])}"`)
            .join(' ');

        // Return HTML with data attributes embedded - use regex to properly insert into opening div tag
        return result.html.replace(/^(<div[^>]+)(>)/, `$1 ${dataAttrs}$2`);
    }

    function applyEditorValue(editorEl) {
        if (!editorEl) return null;
        const varName = (editorEl.dataset.editorFor || '').trim();

        if (varName === 'duration') {
            const unit = editorEl.dataset.durationUnit || 'minute';
            const units = getDurationUnits();
            if (unit === 'per_package') {
                return (units.find(x => x.key === 'per_package') || {}).label || 'laut Packungsanweisung';
            }
            if (unit === 'short') {
                return (units.find(x => x.key === 'short') || {}).label || 'kurz';
            }
            const n = (editorEl.querySelector('#DurationValueInput') || {}).value?.trim() || '';
            const nTo = (editorEl.querySelector('#DurationValueToInput') || {}).value?.trim() || '';
            const unitLabel = (units.find(x => x.key === unit) || {}).label || unit;
            const numPart = (n && nTo && nTo !== n) ? `${n}-${nTo}` : n;
            return numPart ? `${numPart} ${unitLabel}` : null;
        }
        if (varName === 'temp') {
            const n = (editorEl.querySelector('#TempValueInput') || {}).value?.trim() || '';
            const u = (editorEl.querySelector('#TempUnitSelect') || {}).value || 'C';
            return n ? `${n} ${u}` : null;
        }
        if (varName === 'count') {
            const n = ((editorEl.querySelector('#CountValueInput') || {}).value || '1').trim();
            return /^\d+$/.test(n) ? n : '1';
        }
        if (isIngredientVariable(varName)) {
            const selectedValues = JSON.parse(editorEl.dataset.selectedIngredientValues || '[]');
            console.log("[applyEditorValue] Ingredient variable:", { varName, selectedValues });

            // Hybrid: if no ingredient chips selected, fall through to regular value logic
            if (!Array.isArray(selectedValues) || !selectedValues.length) {
                if (!isHybridIngredientVariable(varName)) return null;
                // fall through
            } else {
                // Normalize to new format (includes per-ingredient articles)
                const normalized = normalizeIngredientValues(selectedValues);

                // Apply global fraction and article to each ingredient (if not already set)
                const globalArticle = editorEl.dataset.selectedArticle || '';
                const globalFraction = editorEl.dataset.selectedFraction || '';

                console.log("[applyEditorValue] Global values:", { globalArticle, globalFraction });
                console.log("[applyEditorValue] Before applying global:", JSON.parse(JSON.stringify(normalized)));

                normalized.forEach(item => {
                    if (!item.article || item.article === '') item.article = globalArticle;
                    if (!item.fraction || item.fraction === '') item.fraction = globalFraction;
                });

                console.log("[applyEditorValue] After applying global:", JSON.parse(JSON.stringify(normalized)));

                // Format the ingredient list (handles per-ingredient fractions and articles)
                const ingredientResult = formatSelectedIngredientList(normalized, currentLang);

                console.log("[applyEditorValue] Final result:", ingredientResult);

                return ingredientResult;
            }
        }
        const article = editorEl.dataset.selectedArticle || '';
        const value = editorEl.dataset.selectedValue || '';
        if (!value) return null;
        return composeArticleAndNoun(article, value);
    }

    // applyEditorExtras: returns companion variable values (e.g. pronoun for state vars).
    // Use alongside applyEditorValue when the template may have both {state} and {pronoun} tokens.
    function applyEditorExtras(editorEl) {
        if (!editorEl) return null;
        const varName = (editorEl.dataset.editorFor || '').trim();
        const varNameLower = varName.toLowerCase();

        // Return pronoun extras for BOTH state AND pronoun variables
        if (isStateVariable(varName) || varNameLower === 'pronoun' || varNameLower === 'pronoun2') {
            const pronoun = (editorEl.dataset.selectedPronoun || '').trim();
            return pronoun ? { pronoun } : null;
        }

        return null;
    }

    // Checks if sequential pronoun-before-state editing should be triggered.
    // config: { placeholderType, containerSelector, assignments, onTriggered(pronounTokenId) }
    // Returns true (and calls onTriggered) if triggered, false otherwise.
    function triggerPronounBeforeState(config) {
        if (!config || config.placeholderType !== 'state') return false;
        const pronounEl = document.querySelector((config.containerSelector || '') + ' .placeholder-token[data-placeholder-key="pronoun"]');
        if (!pronounEl) return false;
        const pronounTokenId = pronounEl.dataset.placeholderTokenId;
        if ((config.assignments[pronounTokenId] || '').toString().trim()) return false;
        if (config.onTriggered) config.onTriggered(pronounTokenId);
        return true;
    }

    // Merge into MasterStepCreatorHelpers (second IIFE adds formatIngredientList etc.)
    window.MasterStepCreatorHelpers = Object.assign(window.MasterStepCreatorHelpers || {}, {
        renderTemplate,  // ← Export für renderProbabilityTemplate
        renderTemplateWithConfig,
        renderAssignedPlaceholderTemplate,
        splitLeadingArticleByOptions,
        composeArticleAndNoun,
        getNounOptionsFromValues,
        splitEditorPrefillValue,
        buildInlineEditorHtml,
        applyEditorValue,
        applyEditorExtras,
        saveCurrentEditorValueBeforeAccept,  // ← NEU (2026-03-28): Gemeinsame Accept-Logik
        triggerPronounBeforeState,
        getVarDisplayName,
        handlePickModeClick: _handlePickModeClick,
        handleDurationUnitClick: _handleDurationUnitClick,
        normalizeIngredientValues,
        formatSelectedIngredientList,
        isIngredientVariable,
        // Unified Overlay System (NEW)
        openUniversalVariableEditor,
        closeUnifiedOverlay,
        getMultiIngredientsForContext,
        saveMultiIngredientsForContext,
        // Filter functions (Phase 1)
        setSearchQuery,
        setPhaseFilter,
        renderStepButtons
    });


    document.addEventListener("DOMContentLoaded", init);
})();
(() => {
    var resolveLangKey = window.CreatePostingUtils.resolveLangKey;
    var escapeHtml = window.CreatePostingUtils.escapeHtml;

    function formatIngredientList(names, langKey = "de") {
        const list = (names || []).map(x => (x || "").toString().trim()).filter(Boolean);
        if (!list.length) return "";
        if (list.length === 1) return list[0];

        const lang = resolveLangKey(langKey);
        const conjunctions = { de: "und", en: "and", esp: "y", prt: "e", nl: "en" };
        const conj = conjunctions[lang] || "and";

        if (list.length === 2) {
            return `${list[0]} ${conj} ${list[1]}`;
        }

        const head = list.slice(0, -1).join(", ");
        const tail = list[list.length - 1];

        if (lang === "de") {
            return `${head}, ${conj} ${tail}`;
        }
        return `${head} ${conj} ${tail}`;
    }


    function buildIngredientChipsHtml(ingredients, selectedIds) {
        const selected = (selectedIds || []).map(x => (x || "").toString());
        const list = Array.isArray(ingredients) ? ingredients : [];

        if (!list.length) {
            return '<span class="small text-white-50">Keine Zutaten ausgewählt</span>';
        }

        return list.map(ing => {
            const name = (ing?.name || "").toString();
            const ingId = (ing?.id || "").toString();
            const isActive = selected.includes(ingId);
            const btnClass = isActive ? "btn-light text-dark active" : "btn-outline-light";
            const safe = escapeHtml(name);
            const safeId = escapeHtml(ingId);
            const iconHtml = (ing?.iconHtml || "").toString();
            const label = `${iconHtml ? `${iconHtml} ` : ''}${safe}`;
            return `<button type="button" class="btn btn-sm ${btnClass} inline-equipment-opt js-prob-ingredient-chip" data-value="${safe}" data-id="${safeId}">${label}</button>`;
        }).join("");
    }

    function resolveIngredientInsertValue(selectedNames, langKey, fallbackValue) {
        const fromSelection = formatIngredientList(selectedNames || [], langKey);
        return fromSelection || (fallbackValue || "").toString();
    }

    // Merge the format/chip helpers into MasterStepCreatorHelpers (buildInlineEditorHtml
    // and applyEditorValue are already set by the first IIFE above).
    Object.assign(window.MasterStepCreatorHelpers = window.MasterStepCreatorHelpers || {}, {
        formatIngredientList,
        buildIngredientChipsHtml,
        resolveIngredientInsertValue
    });
})();










