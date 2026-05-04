/**
 * ════════════════════════════════════════════════════════════════════════════
 * CreatePostingSmartStepCreator.js
 * ════════════════════════════════════════════════════════════════════════════
 *
 * Smart Step Creator & Probability Area für Rezept-Erstellung
 *
 * VERANTWORTLICHKEITEN:
 * - Manuelle Step-Auswahl (unterer Bereich: Smart Step Creator)
 * - Automatische Step-Vorschläge (oberer Bereich: Probability Area)
 * - Template-Rendering mit Variablen-Substitution
 * - Ingredient-Matching basierend auf JSON-Regeln
 * - Universeller Variable-Editor Overlay (geteilt von beiden Bereichen)
 * - Draft-Management Integration
 *
 * ABHÄNGIGKEITEN:
 * - window.CreatePostingUtils (escapeHtml)
 * - window.CreatePostingTemplateDrafts (draft engine)
 * - window.MasterStepRenderer (step metadata)
 *
 * DATEN-DATEIEN:
 * - /data/master_steps.json (step templates)
 * - /data/master_step_variables.json (variable catalog)
 * - /data/ingredient_match_rules.json (step-variable rules)
 *
 * DOM-REQUIREMENTS:
 * - #sc2MasterTemplateCards (Step-Cards Container)
 * - #MasterText (Aktueller Step Preview)
 * - #LangSelect (optional)
 *
 * PUBLIC API:
 * - window.MasterStepCreatorHelpers.buildInlineEditorHtml
 * - window.MasterStepCreatorHelpers.applyEditorValue
 * - window.MasterStepCreatorHelpers.formatIngredientList
 * - window.MasterStepCreatorHelpers.buildIngredientChipsHtml
 * - window.MasterStepCreatorHelpers.resolveIngredientInsertValue
 * - window.MasterStepCreatorHelpers.filterIngredientsByVarType
 *
 * @version 3.0.0
 * @date 2026-05-04
 */

(() => {
    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 1: CONFIGURATION & STATE
    // ════════════════════════════════════════════════════════════════════════════
    // Konstanten, globale State-Variablen, DOM-Selektoren

    const DEFAULT_LANG = "de";
    const VISIBLE_RANKED_OPTIONS = 3;
    const DEBUG_ENABLED = false; // Set to true for console logging
    const draftEngine = window.CreatePostingTemplateDrafts || null;

    // Scoring & Ranking Constants
    const SCORE = {
        // Tag Matching
        TAG_MATCH: 30,                // Points per semantic tag match
        TAG_MISMATCH_PENALTY: -15,    // Penalty when step has tags but option doesn't
        NO_TAGS_BONUS: 5,             // Small bonus when no semantic filtering active

        // Rule-based Scoring (descending priority)
        STEP_PREFERRED: 60,           // Step-specific preferred option
        STEP_BLOCKED: -120,           // Step-specific blocked option
        ACTION_PREFERRED: 50,         // Action-based preferred option
        ACTION_BLOCKED: -100,         // Action-based blocked option
        FAMILY_PREFERRED: 40,         // Ingredient-family preferred option
        FAMILY_BLOCKED: -80,          // Ingredient-family blocked option

        // Group Matching
        GROUP_MATCH_BASE: 100,        // Base score for preferred group match
        GROUP_MATCH_DECAY: 10,        // Score decay per group position
        GROUP_DEPRIORITIZE_BASE: -40, // Base penalty for deprioritized groups
        GROUP_DEPRIORITIZE_DECAY: -5, // Additional penalty per position

        // Flag Matching
        FLAG_BONUS: 8,                // Bonus for matching preferred flags

        // Defaults
        IS_DEFAULT_BONUS: 20          // Bonus for default options
    };

    // Unit Conversion Constants
    const UNIT_CONVERSION = {
        KG_TO_GRAMS: 1000,
        LITERS_TO_ML: 1000
    };

    // CSS Class Selectors (prevents typos, enables IDE autocomplete)
    const CSS_CLASS = {
        // Cards & Templates
        TEMPLATE_CARD: 'template-card',
        TEMPLATE_VAR: 'template-var',
        TOKEN_GREEN: 'token-green',

        // Editor Components
        DURATION_EDITOR: 'duration-editor',
        SMART_STEP_CREATOR: 'smart-step-creator',

        // Action Buttons (js- prefixed for JavaScript-only classes)
        JS_EDITOR_REMOVE_OPTIONAL: 'js-editor-remove-optional',
        JS_EDITOR_ACTION_ROW: 'js-editor-action-row',
        JS_VALUE_BTN_ROW: 'js-value-btn-row',
        JS_PRONOUN_BTN_ROW: 'js-pronoun-btn-row',
        JS_ARTICLE_BTN_ROW: 'js-article-btn-row',
        JS_OPTIONAL_VAR_ADD: 'js-optional-var-add',
        JS_FRACTION_PICKER_ROW: 'js-fraction-picker-row',
        JS_HYBRID_OPTIONS_ROW: 'js-hybrid-options-row',
        JS_TOGGLE_FALLBACK_OPTIONS: 'js-toggle-fallback-options',
        JS_ADD_INGREDIENT_TO_LIST: 'js-add-ingredient-to-list',

        // Other
        PLACEHOLDER_RESET: 'placeholder-reset',
        INGREDIENT_CHIP_ACTIVE: 'ingredient-chip.active',
        FRACTION_PICK: 'fraction-pick',
        OPTIONAL_INLINE_PILL: 'optional-inline-pill'
    };

    // DOM Element IDs
    const DOM_ID = {
        // Main Containers
        INLINE_VAR_EDITOR_HOST: 'InlineVarEditorHost',
        UNIVERSAL_EDITOR_CONTENT: 'universalEditorContent',
        UNIVERSAL_EDITOR_OVERLAY: 'universalEditorOverlay',

        // Input Fields
        DURATION_VALUE_INPUT: 'DurationValueInput',
        DURATION_VALUE_TO_INPUT: 'DurationValueToInput',
        TEMP_VALUE_INPUT: 'TempValueInput',
        TEMP_UNIT_SELECT: 'TempUnitSelect',
        COUNT_VALUE_INPUT: 'CountValueInput',

        // Buttons
        BTN_APPLY_VAR: 'BtnApplyVar',
        BTN_ACCEPT_STEP_OVERLAY: 'BtnAcceptStepOverlay',
        BTN_ACCEPT_PROBABILITY_STEP: 'BtnAcceptProbabilityStep'
    };

    // State-Variablen
    let doc = null;
    let variableCatalog = { variables: {} };
    let ingredientMatchRules = { variable_rules: {}, step_variable_rules: {} };
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
    // Safe DOM query helpers
    const $ = (s) => document.querySelector(s);
    const $$ = (s) => document.querySelectorAll(s);

    /**
     * Safe querySelector with error logging.
     * @param {string} selector - CSS selector
     * @param {string} context - Context for error logging (e.g., "renderStepButtons")
     * @returns {Element|null} Found element or null
     */
    const $safe = (selector, context = "unknown") => {
        const el = document.querySelector(selector);
        if (!el) {
            DEBUG("RENDER", `DOM element not found: ${selector}`, { context });
        }
        return el;
    };

    const insertContainer = () => $("#sc2MasterTemplateCards");
    const masterText = () => $("#MasterText");
    const langSelect = () => $("#LangSelect");

    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 2: UTILITY FUNCTIONS
    // ════════════════════════════════════════════════════════════════════════════
    // String-Utilities, Fraction-Helpers, Genus-Konvertierung, Display-Namen

    /**
     * Centralized debug logging function.
     * @param {string} category - Log category (e.g., "RENDER", "EVENT", "DATA")
     * @param {string} message - Log message
     * @param {...any} args - Additional arguments to log
     */
    function DEBUG(category, message, ...args) {
        if (!DEBUG_ENABLED) return;
        const timestamp = new Date().toISOString().split('T')[1].slice(0, -1);
        console.log(`[${timestamp}] [${category}] ${message}`, ...args);
    }

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
        seasoning:   { de: "Gewürz/Öl",    en: "seasoning",    esp: "condimento",   prt: "tempero",      nl: "kruiden",      sv: "krydda",       da: "krydderi",     no: "krydder",      id: "bumbu",      ms: "perisa"    },
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
    // GENUS TO ARTICLE CONVERSION
    // -----------------------------
    function genusToArticle(genus, lang = 'de') {
        const g = (genus || '').toString().trim().toLowerCase();
        if (lang === 'de') {
            if (g === 'pl' || g === 'plural') return 'die';
            if (g === 'f' || g === 'fem' || g === 'feminine') return 'die';
            if (g === 'n' || g === 'neut' || g === 'neuter') return 'das';
            if (g === 'm' || g === 'masc' || g === 'masculine') return 'der';
        }
        // Für andere Sprachen: meistens "the" oder leer
        if (lang === 'en') return 'the';
        return '';
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

    function composeFractionText(fractionDef, ingredientName, article, genus) {
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
                };

                // ✅ FIX (2026-04-02): "der" richtig konvertieren basierend auf Genus
                if (lowerArticle === "der") {
                    // "der" kann maskulin Nominativ ODER feminin Genitiv sein
                    // Prüfe Genus um zu entscheiden
                    const genusLower = (genus || '').toString().toLowerCase();
                    if (genusLower === 'm' || genusLower === 'masc' || genusLower === 'masculine') {
                        // Maskulin Nominativ → Genitiv "des"
                        genitiveArticle = "des";
                    } else {
                        // Feminin (oder unbekannt) → bleibt "der" (feminin Genitiv)
                        genitiveArticle = "der";
                    }
                } else {
                    genitiveArticle = genitiveMap[lowerArticle] || article;
                }
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

        const pillStyle = `background:white;color:var(--cp-ink-deep,#14391f);border:1px solid var(--cp-ink-line,rgba(20,57,31,0.12));border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;cursor:pointer;transition:all 0.15s;`;
        const activeStyle = `background:var(--cp-avocado,#5fa052);color:white;border:1px solid var(--cp-avocado,#5fa052);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;cursor:pointer;`;

        let html = `<div class="small text-muted mt-2 mb-1">Menge</div><div class="${rowClass}"${rowId}>`;
        html += `<button type="button" class="${CSS_CLASS.FRACTION_PICK} active" style="${activeStyle}" data-pick-mode="fraction" data-fraction-key="" data-fraction-num="0" data-fraction-den="1">${escapeHtml(wholeLabel)}</button>`;
        opts.forEach(o => {
            html += `<button type="button" class="${CSS_CLASS.FRACTION_PICK}" style="${pillStyle}" data-pick-mode="fraction" data-fraction-key="${escapeHtml(o.key)}" data-fraction-num="${o.numerator}" data-fraction-den="${o.denominator}">${escapeHtml(o.key)} ${escapeHtml(o.label)}</button>`;
        });
        html += `</div>`;
        return html;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 3: DATA LOADING & CACHING
    // ════════════════════════════════════════════════════════════════════════════
    // JSON-Daten laden, Caching-Logik

    /**
     * Loads master steps JSON data from server.
     * @returns {Promise<Array>} Master steps data
     */
    async function loadJson() {
        return await window.CreatePostingDataStore.load("masterSteps");
    }

    /**
     * Loads variable catalog from server.
     * @returns {Promise<object>} Variable catalog with variables definitions
     * @throws {Error} If loading fails critically (re-thrown for upstream handling)
     */
    async function loadVariableCatalog() {
        try {
            const data = await window.CreatePostingDataStore.load("masterStepVariables");
            if (!data || typeof data !== "object") {
                DEBUG("DATA", "Variable catalog loaded but invalid format, using defaults");
                return { variables: {} };
            }
            DEBUG("DATA", "Variable catalog loaded successfully", { variableCount: Object.keys(data.variables || {}).length });
            return data;
        } catch (error) {
            DEBUG("DATA", "Failed to load variable catalog", { error: error.message, stack: error.stack });
            // Return fallback to allow app to continue with limited functionality
            return { variables: {} };
        }
    }

    /**
     * Loads ingredient match rules from server.
     * @returns {Promise<object>} Match rules with variable_rules and step_variable_rules
     * @throws {Error} If loading fails critically (re-thrown for upstream handling)
     */
    async function loadIngredientMatchRules() {
        try {
            const data = await window.CreatePostingDataStore.load("ingredientMatchRules");
            if (!data || typeof data !== "object") {
                DEBUG("DATA", "Ingredient match rules loaded but invalid format, using defaults");
                return { variable_rules: {}, step_variable_rules: {} };
            }
            DEBUG("DATA", "Ingredient match rules loaded successfully", {
                variableRules: Object.keys(data.variable_rules || {}).length,
                stepRules: Object.keys(data.step_variable_rules || {}).length
            });
            return data;
        } catch (error) {
            DEBUG("DATA", "Failed to load ingredient match rules", { error: error.message, stack: error.stack });
            // Return fallback to allow app to continue with limited functionality
            return { variable_rules: {}, step_variable_rules: {} };
        }
    }

    // -----------------------------
    // OPTION LOOKUP (NUR JSON!)
    // -----------------------------
    function normalizeVarKey(value) {
        const raw = (value || "").toString().trim();
        const normalized = raw.toLowerCase().replace(/[_\-\s]+/g, "");
        const aliasMap = {
            pronomen: "pronoun",
            basis: "base"  // ✅ NEU (2026-05-04): "basis" als Alias für "base"
        };
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

    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 4: OPTION & RULE SYSTEM
    // ════════════════════════════════════════════════════════════════════════════
    // Variable-Catalog-Access, Option-Ranking, Step-Rules, Ingredient-Family-Detection

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

    /**
     * Ranks and filters variable options based on step rules, ingredient family, and context.
     * This is the core option-scoring algorithm that powers the variable editor dropdowns.
     *
     * Scoring factors:
     * - Step-specific rules (master_step_option_rules.json)
     * - Ingredient family detection (e.g., "chicken" → poultry family → heat="hoch")
     * - Action-based defaults (e.g., "braten" action → heat="hoch")
     * - Semantic tags (variable: state, value: golden_brown)
     * - Quantity mode filtering (prefer_smallest, exclude_largest, etc.)
     *
     * @param {string} varName - Variable name (e.g., "state", "shape", "heat")
     * @param {string} contextMasterId - Current step master ID (e.g., "PREP_CUT_01")
     * @param {object} context - Scoring context
     * @param {Array} context.selectedIngredients - Array of ingredient objects with id, groupId, name
     * @param {string} context.action - Current step action (e.g., "braten", "kochen")
     * @param {string} context.varName - Variable name (redundant with first param, for compatibility)
     * @returns {Array<object>} Sorted array of options [{value, label, score, isDefault, tags}, ...]
     *
     * @example
     * getRankedVarOptionEntries("shape", "PREP_CUT_01", {
     *   selectedIngredients: [{ id: 1, name: "Kartoffel", groupId: 2 }],
     *   action: "schneiden"
     * })
     * // Returns: [{ value: "würfel", label: "Würfel", score: 15, isDefault: true }, ...]
     */
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

                // --- Tag Match: +TAG_MATCH per semantic tag match ---
                if (matchedTags.length) {
                    score += matchedTags.length * SCORE.TAG_MATCH;
                } else if (hasSemanticStepTags && semanticTags.length) {
                    score += SCORE.TAG_MISMATCH_PENALTY;
                }

                if (!semanticTags.length) {
                    score += SCORE.NO_TAGS_BONUS;
                }

                // --- Step-specific rules (highest priority) ---
                if (stepPreferred.has(normalizedValue)) {
                    score += SCORE.STEP_PREFERRED;
                }
                if (stepBlocked.has(normalizedValue)) {
                    score += SCORE.STEP_BLOCKED;
                }

                // --- Action rules ---
                if (actionPreferred.has(normalizedValue)) {
                    score += SCORE.ACTION_PREFERRED;
                }
                if (actionBlocked.has(normalizedValue)) {
                    score += SCORE.ACTION_BLOCKED;
                }

                // --- Ingredient Family rules ---
                if (familyPreferred.has(normalizedValue)) {
                    score += SCORE.FAMILY_PREFERRED;
                }
                if (familyBlocked.has(normalizedValue)) {
                    score += SCORE.FAMILY_BLOCKED;
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

            // Add "Würfel" as first option (most common)
            const hasWuerfel = filtered.some(entry => {
                const normalized = normalizeOptionValue(entry.value);
                return normalized === "würfel" || normalized === "wuerfel";
            });
            if (!hasWuerfel) {
                filtered.unshift({
                    value: "Würfel",
                    score: Number.MAX_SAFE_INTEGER + 1,
                    matchedTagsCount: 0,
                    originalIndex: -1
                });
            } else {
                // Move Würfel to first position
                const wuerfelIndex = filtered.findIndex(entry => {
                    const normalized = normalizeOptionValue(entry.value);
                    return normalized === "würfel" || normalized === "wuerfel";
                });
                if (wuerfelIndex > 0) {
                    const wuerfelEntry = filtered.splice(wuerfelIndex, 1)[0];
                    wuerfelEntry.score = Number.MAX_SAFE_INTEGER + 1;
                    filtered.unshift(wuerfelEntry);
                }
            }

            // Add "gehackt" as fallback option
            const hasGehackt = filtered.some(entry => normalizeOptionValue(entry.value) === "gehackt");
            if (!hasGehackt) {
                filtered.splice(1, 0, {
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

    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 5: TEMPLATE RENDERING SYSTEM
    // ════════════════════════════════════════════════════════════════════════════
    // Template-Parsing, Token-Replacement, Optional Segments, Multi-Variable Fallback

    /**
     * Extracts all variable names from a template fragment.
     * @param {string} templateFragment - Template with {{var}} syntax
     * @returns {Array<string>} Array of unique variable names
     */
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

        // Fallback text: {~text~varName~} → show text only when varName is empty
        output = output.replace(/\{~([^~]+)~([^~]+)~\}/g, (_m, fallback, varName) => {
            const hasAnyValue = (varName || "")
                .split("|")
                .map(x => x.trim())
                .filter(Boolean)
                .some(name => ((values?.[name] ?? "").toString().trim().length > 0));
            return hasAnyValue ? "" : fallback;
        });

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
                class="cp-pill ${CSS_CLASS.JS_OPTIONAL_VAR_ADD}"
                style="background:white;color:var(--cp-ink-deep);border:1px solid var(--cp-ink-line);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;"
                data-optional-var="${escapeHtml(firstVar)}"
                data-token-id="${escapeHtml(tokenId)}">
          <i class="bi bi-plus-circle me-1"></i>${escapeHtml(label)}
        </button>
      `);
        }

        return buttons.join("");
    }

    /**
     * Base template rendering engine (consolidates all 3 render functions).
     * Replaces {{var}} tokens and handles optional [...] segments.
     *
     * @param {string} templateRaw - Raw template with {{var}} and [...] syntax
     * @param {string} stepId - Step identifier for token IDs
     * @param {object} values - Variable values {varName: value}
     * @param {object} config - Rendering configuration
     * @param {boolean} config.enableOptionalSegments - Process optional [...] and (...) segments
     * @param {boolean} config.enableFallbackText - Process {~text~var~} fallback syntax
     * @param {boolean} config.enableSentenceStart - Capitalize tokens at sentence start
     * @param {object} config.optionalValues - Values for optional segment detection (defaults to values)
     * @param {string} config.tokenClass - CSS classes for tokens
     * @param {string} config.pillClass - CSS classes for optional pills
     * @param {string} config.tokenWrapClass - Wrap tokens in additional span with this class
     * @param {boolean} config.includeVarKey - Include data-var-key attribute
     * @param {string} config.optionalVarAttrName - Attribute name for optional vars (default: data-optional-var)
     * @param {string} config.pillTitlePrefix - Title prefix for pills (default: "Optional")
     * @returns {string} Rendered HTML
     */
    function _renderTemplateBase(templateRaw, stepId, values, config = {}) {
        if (!templateRaw) return "";
        values = values || {};

        const {
            enableOptionalSegments = true,
            enableFallbackText = true,
            enableSentenceStart = true,
            optionalValues = values,
            tokenClass = "token-highlight placeholder-token template-var token-green",
            pillClass = "optional-inline-pill js-optional-var-add",
            tokenWrapClass = "",
            includeVarKey = false,
            optionalVarAttrName = "data-optional-var",
            pillTitlePrefix = "Optional"
        } = config;

        const PILL_START = "\x01";
        const PILL_SEP = "\x02";
        const PILL_END = "\x03";

        let processed = templateRaw;

        // ========== PASS 1: Optional Segments ==========
        if (enableOptionalSegments) {
            const optionalPattern = /\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]/g;
            const seen = new Set();

            processed = processed.replace(optionalPattern, (_match, parenInner, bracketInner) => {
                const inner = (parenInner ?? bracketInner ?? "").toString();
                const varsInGroup = getTemplateVariables(inner);
                if (!varsInGroup.length) return inner;

                const hasAnyValue = varsInGroup.some(varName =>
                    ((optionalValues?.[varName] ?? "").toString().trim().length > 0)
                );
                if (hasAnyValue) return inner; // Filled → render content normally

                const dedupeKey = `${stepId}__${varsInGroup.join("__")}`;
                if (seen.has(dedupeKey)) return "";
                seen.add(dedupeKey);

                const firstVar = varsInGroup[0];
                const tokenId = `${stepId}_optional_${varsInGroup.join("_")}`;
                const label = inner.replace(/{{\s*([^}]+?)\s*}}/g, (_m, v) => getVarDisplayName(v.trim()));

                // Encode as sentinel-delimited marker
                return `${PILL_START}${firstVar}${PILL_SEP}${tokenId}${PILL_SEP}${label}${PILL_END}`;
            });
        }

        // ========== PASS 2: Fallback Text ==========
        if (enableFallbackText) {
            processed = processed.replace(/\{~([^~]+)~([^~]+)~\}/g, (_m, fallback, varName) => {
                const hasAnyValue = (varName || "")
                    .split("|")
                    .map(x => x.trim())
                    .filter(Boolean)
                    .some(name => ((optionalValues?.[name] ?? "").toString().trim().length > 0));
                return hasAnyValue ? "" : fallback;
            });
        }

        // ========== PASS 3: Render {{var}} Tokens ==========
        let tokenIndex = 0;
        const tokenVarKeyAttr = includeVarKey ? ` data-var-key="{{VAR_NAME}}"` : "";

        processed = processed.replace(/{{\s*([^}]+?)\s*}}/g, (_match, varRaw, offset) => {
            const varName = (varRaw ?? "").trim();
            const tokenId = `${stepId}_${varName}_${tokenIndex++}`;

            // ✅ FIX: Objekt zu String konvertieren
            let rawValue = values[varName] ?? "";
            if (typeof rawValue === 'object' && rawValue !== null) {
                rawValue = rawValue.name || rawValue.value || rawValue.displayName || String(rawValue);
            }
            const value = String(rawValue);

            let display = value.trim().length > 0 ? value : getVarDisplayName(varName);

            // Sentence-start capitalization
            if (enableSentenceStart) {
                const isAtSentenceStart = offset === 0 ||
                    /^[\s]*$/.test(processed.slice(0, offset)) ||
                    /[.!?]\s*$/.test(processed.slice(0, offset));

                if (isAtSentenceStart && display.length > 0) {
                    display = display.charAt(0).toUpperCase() + display.slice(1);
                }
            }

            const varKeyAttr = tokenVarKeyAttr.replaceAll("{{VAR_NAME}}", escapeHtml(varName));
            const sentenceStartAttr = enableSentenceStart ?
                ` data-sentence-start="${(offset === 0 || /^[\s]*$/.test(processed.slice(0, offset)) || /[.!?]\s*$/.test(processed.slice(0, offset))) ? "1" : "0"}"` :
                "";

            const tokenHtml = `<span class="${tokenClass}" draggable="false" data-var="${escapeHtml(varName)}"${varKeyAttr} data-token-id="${escapeHtml(tokenId)}" data-has-value="${value.trim().length > 0 ? "1" : "0"}"${sentenceStartAttr}>${escapeHtml(display)}</span>`;

            if (!tokenWrapClass) return tokenHtml;
            return `<span class="${escapeHtml(tokenWrapClass)}">${tokenHtml}</span>`;
        });

        // ========== PASS 4: Render Pills ==========
        if (enableOptionalSegments) {
            const pillVarKeyAttr = includeVarKey ? ` data-var-key="{{VAR_NAME}}"` : "";

            processed = processed.replace(
                new RegExp(`\\x01([^\\x02]*)\\x02([^\\x02]*)\\x02([^\\x03]*)\\x03`, "g"),
                (_match, firstVar, tokenId, label) => {
                    const varKeyAttr = pillVarKeyAttr.replaceAll("{{VAR_NAME}}", escapeHtml(firstVar));
                    return `<span class="${pillClass}" role="button" tabindex="0" ${optionalVarAttrName}="${escapeHtml(firstVar)}"${varKeyAttr} data-token-id="${escapeHtml(tokenId)}" title="${escapeHtml(pillTitlePrefix)}: ${escapeHtml(label)}"><i class="bi bi-plus-circle-dotted" aria-hidden="true"></i><span class="optional-pill-label">${escapeHtml(label)}</span></span>`;
                }
            );
        }

        return processed;
    }

    /**
     * Simple token renderer without optional segments or capitalization.
     * @param {string} templateRaw - Template string
     * @param {string} stepId - Step ID
     * @param {object} values - Variable values
     * @returns {string} Rendered HTML
     */
    function renderTemplateTokens(templateRaw, stepId, values) {
        return _renderTemplateBase(templateRaw, stepId, values, {
            enableOptionalSegments: false,
            enableFallbackText: false,
            enableSentenceStart: false
        });
    }

    /**
     * Renders template with optional segments as pills and sentence-start capitalization.
     * @param {string} templateRaw - Template string
     * @param {string} stepId - Step ID
     * @param {object} values - Variable values
     * @returns {string} Rendered HTML
     */
    function renderTemplate(templateRaw, stepId, values) {
        return _renderTemplateBase(templateRaw, stepId, values, {
            enableOptionalSegments: true,
            enableFallbackText: true,
            enableSentenceStart: true,
            pillTitlePrefix: "Optional hinzufügen"
        });
    }


    /**
     * Renders template with full configurability (CSS classes, attributes, etc.).
     * @param {string} templateRaw - Template string
     * @param {string} stepId - Step ID
     * @param {object} values - Variable values
     * @param {object} config - Configuration options
     * @returns {string} Rendered HTML
     */
    function renderTemplateWithConfig(templateRaw, stepId, values, config) {
        config = config || {};

        // Map legacy config options to new format
        const baseConfig = {
            enableOptionalSegments: true,
            enableFallbackText: true,
            enableSentenceStart: true,
            optionalValues: config.optionalValues || values,
            tokenClass: ["token-highlight placeholder-token template-var token-green", config.tokenExtraClasses || ""].filter(Boolean).join(" "),
            pillClass: ["optional-inline-pill", config.pillExtraClasses || ""].filter(Boolean).join(" "),
            tokenWrapClass: (config.tokenWrapClass || "").toString().trim(),
            includeVarKey: config.includeVarKey || false,
            optionalVarAttrName: (config.optionalVarAttrName || "data-optional-var").toString(),
            pillTitlePrefix: (config.pillTitlePrefix || "Optional").toString()
        };

        return _renderTemplateBase(templateRaw, stepId, values, baseConfig);
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

            // ✅ FIX: Objekt zu String konvertieren
            let rawFallback = values && values[key] != null ? values[key] : key;
            if (typeof rawFallback === 'object' && rawFallback !== null) {
                rawFallback = rawFallback.name || rawFallback.value || rawFallback.displayName || String(rawFallback);
            }
            const fallback = String(rawFallback).trim();

            let rawAssigned = assignments && assignments[tokenId] != null ? assignments[tokenId] : "";
            if (typeof rawAssigned === 'object' && rawAssigned !== null) {
                rawAssigned = rawAssigned.name || rawAssigned.value || rawAssigned.displayName || String(rawAssigned);
            }
            const assigned = String(rawAssigned).trim();

            const value = assigned || fallback || key;
            const activeClass = activeTokenId === tokenId ? " token-active" : "";
            const safeKey = escapeHtml(key);
            const safeTokenId = escapeHtml(tokenId);
            const safeValue = escapeHtml(value);
            const safeFallback = escapeHtml(fallback || key);

            return `<span class="token-highlight placeholder-token token-green${activeClass}" draggable="false" ${tokenKeyAttr}="${safeKey}" ${tokenIdAttr}="${safeTokenId}">${safeValue}</span>`;
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
        const lang = currentLang || 'de';
        const searchText = [
            step.description || '',
            step.master_id || '',
            step.action || '',
            step.templates?.[lang] || '',
            step.templates?.de || '',
            ...(step.selection_tags || [])
        ].join(' ').toLowerCase();
        return searchText.includes(q);
    }

    function filterSteps(stepsToFilter, ingredientTagSet, ingredientGroupIds) {
        return stepsToFilter.filter(step => {
            // Phase filter
            if (currentPhaseFilter === 'best') {
                const score = window.MasterStepRenderer?.getStepGroupScore
                    ? window.MasterStepRenderer.getStepGroupScore(step.master_id, ingredientGroupIds || [])
                    : 0;
                if (score < 2) return false;
            } else if (currentPhaseFilter !== 'all') {
                const phaseNum = parseInt(currentPhaseFilter, 10);
                if ((step.phase ?? 0) !== phaseNum) {
                    return false;
                }
            }
            // Search filter
            if (!matchesSearch(step, currentSearchQuery)) {
                return false;
            }
            // Ingredient tag filter
            if (ingredientTagSet && ingredientTagSet.size && window.MasterStepRenderer?.shouldShowStep) {
                if (!window.MasterStepRenderer.shouldShowStep(step.master_id, ingredientTagSet)) {
                    return false;
                }
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

    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 7: SMART STEP CREATOR (Lower Area)
    // ════════════════════════════════════════════════════════════════════════════
    // Manuelle Step-Auswahl, Step-Card-Rendering, Filter-UI, Draft-Management

    function renderStepButtons() {
        const container = insertContainer();
        if (!container) return;

        container.innerHTML = "";

        // Collect unique ingredient groupIds for affinity scoring + tags for filtering
        const _pageIngredients = getSelectedIngredientsFromPage();
        const _ingredientGroupIds = [...new Set(_pageIngredients.map(i => (i.groupId || '').toString()).filter(Boolean))];
        const _ingredientTags = window.MasterStepRenderer?.collectIngredientTags
            ? window.MasterStepRenderer.collectIngredientTags(_pageIngredients)
            : new Set();

        // Apply filters first (including ingredient tag filter)
        const filteredSteps = filterSteps(steps, _ingredientTags, _ingredientGroupIds);

        // Show message if no results after filtering
        if (!filteredSteps.length && steps.length > 0) {
            if (currentPhaseFilter === 'best') {
                container.innerHTML = '<div class="small text-white-50 text-center py-4">Keine passenden Steps — zuerst Zutaten hinzufügen oder mehr Zutaten wählen.</div>';
            } else {
                container.innerHTML = '<div class="small text-white-50 text-center py-4">Keine Steps für diesen Filter gefunden.</div>';
            }
            return;
        }

        // sortieren: phase -> affinity score (desc) -> sub_group -> master_id
        const sorted = [...filteredSteps].sort((a, b) => {
            const pa = a.phase ?? 0;
            const pb = b.phase ?? 0;
            if (pa !== pb) return pa - pb;
            // Higher affinity score first within same phase
            const scoreA = window.MasterStepRenderer?.getStepGroupScore ? window.MasterStepRenderer.getStepGroupScore(a.master_id, _ingredientGroupIds) : 0;
            const scoreB = window.MasterStepRenderer?.getStepGroupScore ? window.MasterStepRenderer.getStepGroupScore(b.master_id, _ingredientGroupIds) : 0;
            if (scoreA !== scoreB) return scoreB - scoreA;
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

        const theme = window.CreatePostingCurrentTheme || document.querySelector(`.${CSS_CLASS.SMART_STEP_CREATOR}`)?.getAttribute('data-theme') || 'gold';

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

                    // Affinity-Score Badge
                    const affScore = window.MasterStepRenderer?.getStepGroupScore
                        ? MasterStepRenderer.getStepGroupScore(step.master_id, _ingredientGroupIds)
                        : 0;
                    const scoreBadge = affScore >= 3 ? '<span class="step-affinity-badge high" title="Sehr relevant">\u2605\u2605\u2605</span>'
                        : affScore === 2 ? '<span class="step-affinity-badge medium" title="Relevant">\u2605\u2605</span>'
                        : affScore === 1 ? '<span class="step-affinity-badge low" title="M\u00f6glich">\u2605</span>'
                        : '';

                    // Dynamisches Icon basierend auf dem Titel oder Theme (optional)
                    const icon = theme === 'vorbereitung' ? '🔪' : '🥕';

                    container.insertAdjacentHTML("beforeend", `
                                <button type="button" 
                                        class="template-card-modern template-card w-100 mb-2"

                                        data-step-id="${escapeHtml(step.master_id ?? "")}" 
                                        data-title="${escapeHtml(title)}" 
                                        data-template-raw="${encodeAttr(templateRaw)}">
            
                                    <!-- Linke Seite: Icon -->
                                    <div class="template-card-icon">
                                        ${icon}
                                    </div>
        
                                    <!-- Mitte: Textinhalt -->
                                    <div class="template-card-content">
                                        <div class="template-card-title">
                                            ${escapeHtml(title)}
                                        </div>
                                        <div class="template-card-snippet">
                                            ${snippetPreview(templateRaw)}
                                        </div>
                                    </div>
        
                                    <!-- Rechte Seite: Badge & Plus -->
                                    <div class="template-card-meta">
                                        <div class="template-card-score">
                                            ${scoreBadge} 
                                        </div>
                                        <span class="template-card-plus">
                                            <span>+</span>
                                        </span>
                                    </div>
                                </button>
                    `);
                });
            }
        }
    }

    function getCurrentStepDraft() {
        return (draftEngine && typeof draftEngine.getActiveStepDraft === 'function')
            ? draftEngine.getActiveStepDraft()
            : activeStep;
    }

    // ✅ Helper: Konvertiert Werte zu Strings (verhindert [object Object])
    function normalizeValueToString(value) {
        if (value == null) return '';
        if (typeof value === 'string') return value;
        if (typeof value === 'object') {
            return value.name || value.value || value.displayName || value.label || String(value);
        }
        return String(value);
    }

    function ensureProbabilityDraftForEditing(masterId) {
        const safeMasterId = (masterId || '').toString().trim();
        if (!safeMasterId) return null;

        if (typeof window.ensureCreatePostingProbabilityDraftState === 'function') {
            return window.ensureCreatePostingProbabilityDraftState(safeMasterId);
        }

        if (draftEngine && typeof draftEngine.ensureProbabilityDraft === 'function') {
            return draftEngine.ensureProbabilityDraft(safeMasterId, {
                masterId: safeMasterId,
                values: {},
                _multiIngredients: {}
            });
        }

        return window.probabilityStates?.[safeMasterId] || null;
    }

    function renderTemplateDraftHtml(draft, options = {}) {
        const mode = (options.mode || 'step').toString();
        if (!draft) return mode === 'step' ? "Wähle eine Template." : "";

        if (mode === 'probability') {
            const builder = window.CreatePostingProbability;
            const template = window.MasterStepRenderer?.findTemplate
                ? window.MasterStepRenderer.findTemplate(draft.masterId)
                : null;
            if (!builder || typeof builder.buildInlineTemplateText !== 'function' || !template) {
                return '';
            }

            const lang = (options.lang || currentLang || DEFAULT_LANG).toString();
            const renderVars = options.varsOverride || draft.values || {};
            const optionalValues = options.optionalValues || draft.values || {};
            return builder.buildInlineTemplateText(draft.masterId, template, lang, renderVars, optionalValues);
        }

        return renderTemplate(draft.templateRaw, draft.master_id || draft.masterId, draft.values || {});
    }

    function renderEditableStepPreview(draft, options = {}) {
        const mode = (options.mode || 'step').toString();
        const rendered = renderTemplateDraftHtml(draft, options);
        const title = (options.title || (mode === 'probability' ? 'Erkannter Step' : 'AKTUELLER STEP')).toString();
        const textId = (options.textId || '').toString().trim();
        const hostId = (options.hostId || '').toString().trim();
        const hostClass = (options.hostClass || '').toString().trim();
        const bodyClasses = (options.bodyClasses || 'preview-step-text mt-2').toString().trim();
        const actionHtml = (options.actionHtml || '').toString();
        const wrapperClass = (options.wrapperClass || (mode === 'probability' ? 'probability-preview-wrap' : 'current-step-wrap')).toString();

        const textIdAttr = textId ? ` id="${escapeHtml(textId)}"` : '';
        const hostIdAttr = hostId ? ` id="${escapeHtml(hostId)}"` : '';
        const hostClassAttr = hostClass ? ` class="${escapeHtml(hostClass)}"` : '';

        return `
      <div class="${escapeHtml(wrapperClass)}">
        <div class="current-step-header d-flex justify-content-between align-items-center">
          <div class="preview-step-title mb-0">${escapeHtml(title)}</div>
          ${actionHtml}
        </div>

        <div class="${escapeHtml(bodyClasses)}"${textIdAttr}>
          ${rendered}
        </div>

        <div${hostClassAttr}${hostIdAttr}></div>
      </div>
    `;
    }

    function renderOverlayPreviewForContext(context) {
        if (!context) return '';

        if (context.type === 'step') {
            const stepDraft = getCurrentStepDraft();
            if (!stepDraft) return '';
            return renderEditableStepPreview(stepDraft, {
                mode: 'step',
                title: 'AKTUELLER STEP',
                textId: 'CurrentStepText',
                hostId: 'DOM_ID.INLINE_VAR_EDITOR_HOST',
                hostClass: 'mt-3',
                bodyClasses: 'preview-step-text mt-2',
                wrapperClass: 'current-step-wrap',
                actionHtml: ''  // ✅ NEU (2026-05-04): Kein Accept-Button im Overlay-Header, nur im Haupt-Editor
            });
        }

        if (context.type === 'probability') {
            const masterId = (context.probabilityMasterId || context.masterId || '').toString().trim();
            const buildProbabilityPreviewDraft = window.buildCreatePostingProbabilityPreviewDraft;
            const probabilityPreviewDraft = typeof buildProbabilityPreviewDraft === 'function'
                ? buildProbabilityPreviewDraft(masterId)
                : ensureProbabilityDraftForEditing(masterId);
            if (!probabilityPreviewDraft) return '';

            return renderEditableStepPreview({
                masterId: masterId,
                templateRaw: probabilityPreviewDraft.templateRaw || '',
                values: probabilityPreviewDraft.values || {}
            }, {
                mode: 'step',
                title: 'Erkannter Step',
                bodyClasses: 'probability-template-text preview-step-text mt-2',
                wrapperClass: 'current-step-wrap probability-preview-wrap',
                actionHtml: masterId
                    ? `<button type="button" class="btn btn-sm creator-cta-primary" id="BtnAcceptProbabilityStep" data-master-id="${escapeHtml(masterId)}">Step akzeptieren</button>`
                    : ''
            });
        }

        return '';
    }

    // -----------------------------
    // RENDER: MasterText (aktueller Step + Editor Slot)
    // -----------------------------
    function renderMasterText() {
        const target = masterText();
        if (!target) return;

        const currentDraft = getCurrentStepDraft();
        if (!currentDraft) {
            target.innerHTML = "Wähle eine Template.";
            return;
        }

        target.innerHTML = renderEditableStepPreview(currentDraft, {
            mode: 'step',
            title: 'AKTUELLER STEP',
            textId: 'CurrentStepText',
            hostId: 'DOM_ID.INLINE_VAR_EDITOR_HOST',
            hostClass: 'mt-3',
            bodyClasses: 'preview-step-text mt-2',
            wrapperClass: 'current-step-wrap',
            actionHtml: ''  // ✅ NEU (2026-05-04): Button ins Overlay verschoben
        });
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

    function closeInlineEditor() {
        activeToken = null;
        closeUnifiedOverlay();

        // Clear old inline host (backward compatibility)
        const host = $("#DOM_ID.INLINE_VAR_EDITOR_HOST");
        if (host) host.innerHTML = "";
    }

    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 6: INGREDIENT MATCHING & FILTERING
    // ════════════════════════════════════════════════════════════════════════════
    // Ingredient-Match-Rules, Quantity-Mode, Display-Filtering, Special-Transforms

    function isIngredientVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase();
        return key === "ingredient" || key === "ingredient2" || key === "ingredients" || key === "base" || key === "basis" || key === "seasoning" || key === "liquid" || key === "fat" || key === "seasonings" || key === "marinade" || key === "thickener" || key === "components" || key === "extra";
    }

    function isHybridIngredientVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase();
        return key === "extra" || key === "base" || key === "basis" || key === "liquid";
    }

    function getEditorSelectionLabel(varName, hybridVar) {
        const key = (varName || "").toString().trim().toLowerCase();
        if (hybridVar && key === "base") {
            return "Zutat oder Basis";
        }
        return getVarDisplayName(varName);
    }

    function normalizeRuleArray(value) {
        if (!Array.isArray(value)) return [];
        return value
            .map(x => (x ?? "").toString().trim())
            .filter(Boolean);
    }

    function mergeRuleArrays(primary, fallback) {
        return [...new Set([
            ...normalizeRuleArray(primary),
            ...normalizeRuleArray(fallback)
        ])];
    }

    function getIngredientRuleValue(item, ruleKey) {
        const raw = (ruleKey || "").toString().trim();
        if (!raw) return false;
        if (Object.prototype.hasOwnProperty.call(item || {}, raw)) {
            return !!item[raw];
        }
        if (/^is[A-Z]/.test(raw)) {
            return !!item?.[raw];
        }
        const normalized = "is" + raw.charAt(0).toUpperCase() + raw.slice(1);
        return !!item?.[normalized];
    }

    function getIngredientCandidateNames(item) {
        const names = [
            (item?.name || "").toString().trim(),
            ...Object.values(item?.namesByLang || {}).map(x => (x || "").toString().trim())
        ]
            .map(x => x.toLowerCase())
            .filter(Boolean);
        return [...new Set(names)];
    }

    function matchesIngredientRule(item, rule) {
        if (!rule || typeof rule !== "object") return true;

        const anyOf = Array.isArray(rule.any_of) ? rule.any_of : [];
        if (anyOf.length && !anyOf.some(child => matchesIngredientRule(item, child))) {
            return false;
        }

        const allOf = Array.isArray(rule.all_of) ? rule.all_of : [];
        if (allOf.length && !allOf.every(child => matchesIngredientRule(item, child))) {
            return false;
        }

        const requireAll = normalizeRuleArray(rule.require_all);
        if (requireAll.length && !requireAll.every(key => getIngredientRuleValue(item, key))) {
            return false;
        }

        const requireAny = normalizeRuleArray(rule.require_any);
        if (requireAny.length && !requireAny.some(key => getIngredientRuleValue(item, key))) {
            return false;
        }

        const excludeAny = normalizeRuleArray(rule.exclude_any || rule.exclude_tags);
        if (excludeAny.some(key => getIngredientRuleValue(item, key))) {
            return false;
        }

        const allowGroups = normalizeRuleArray(rule.allow_groups);
        const itemGroup = (item?.groupId || "").toString();
        if (allowGroups.length && !allowGroups.includes(itemGroup)) {
            return false;
        }

        const excludeGroups = normalizeRuleArray(rule.exclude_groups);
        if (excludeGroups.includes(itemGroup)) {
            return false;
        }

        const candidateNames = getIngredientCandidateNames(item);
        const allowNamesExact = normalizeRuleArray(rule.allow_names_exact).map(x => x.toLowerCase());
        if (allowNamesExact.length && !candidateNames.some(name => allowNamesExact.includes(name))) {
            return false;
        }

        const excludeNamesExact = normalizeRuleArray(rule.exclude_names_exact).map(x => x.toLowerCase());
        if (candidateNames.some(name => excludeNamesExact.includes(name))) {
            return false;
        }

        const allowNameContains = normalizeRuleArray(rule.allow_name_contains).map(x => x.toLowerCase());
        if (allowNameContains.length && !allowNameContains.some(part => candidateNames.some(name => name.includes(part)))) {
            return false;
        }

        const excludeNameContains = normalizeRuleArray(rule.exclude_name_contains).map(x => x.toLowerCase());
        if (excludeNameContains.some(part => candidateNames.some(name => name.includes(part)))) {
            return false;
        }

        return true;
    }

    function getIngredientMatchRule(varName, masterId) {
        const key = (varName || "").toString().trim().toLowerCase();
        const step = (masterId || "").toString().trim().toUpperCase();
        const variableRule = ingredientMatchRules?.variable_rules?.[key] || null;
        const stepRule = ingredientMatchRules?.step_variable_rules?.[step]?.[key] || null;
        return { variableRule, stepRule };
    }

    function getIngredientDisplayConfig(varName, masterId) {
        const key = (varName || "").toString().trim().toLowerCase();
        const { variableRule, stepRule } = getIngredientMatchRule(varName, masterId);

        const limitRaw = stepRule?.display_limit ?? variableRule?.display_limit;
        const parsedLimit = Number(limitRaw);
        const defaultLimit = key === "ingredient" ? 2 : (key === "ingredients" ? 3 : null);

        return {
            limit: Number.isFinite(parsedLimit) && parsedLimit > 0 ? parsedLimit : defaultLimit,
            preferGroups: mergeRuleArrays(stepRule?.display_prefer_groups, variableRule?.display_prefer_groups),
            deprioritizeGroups: mergeRuleArrays(stepRule?.display_deprioritize_groups, variableRule?.display_deprioritize_groups),
            preferFlags: mergeRuleArrays(stepRule?.display_prefer_flags, variableRule?.display_prefer_flags),
            quantityMode: getIngredientQuantityMode(varName, masterId),
            excludeDerived: (stepRule?.display_exclude_derived ?? variableRule?.display_exclude_derived) === true,
            preferBaseSources: (stepRule?.display_prefer_base_sources ?? variableRule?.display_prefer_base_sources) !== false
        };
    }

    function getIngredientQuantityMode(varName, masterId) {
        const { variableRule, stepRule } = getIngredientMatchRule(varName, masterId);
        const legacyPreferSmaller = (stepRule?.display_prefer_smaller_quantity ?? variableRule?.display_prefer_smaller_quantity) === true
            ? "prefer_smallest"
            : "";
        return normalizeDisplayQuantityMode(
            stepRule?.quantity_mode
            ?? variableRule?.quantity_mode
            ?? stepRule?.display_quantity_mode
            ?? variableRule?.display_quantity_mode
            ?? legacyPreferSmaller
        );
    }

    function normalizeDisplayQuantityMode(value) {
        const raw = (value || "").toString().trim().toLowerCase();
        const aliases = {
            prefer_smallest: "prefer_smallest",
            smaller: "prefer_smallest",
            min: "prefer_smallest",
            prefer_min: "prefer_smallest",
            prefer_small: "prefer_smallest",
            prefer_largest: "prefer_largest",
            larger: "prefer_largest",
            max: "prefer_largest",
            prefer_max: "prefer_largest",
            prefer_large: "prefer_largest",
            exclude_largest: "exclude_largest",
            ignore_largest: "exclude_largest",
            exclude_max: "exclude_largest",
            ignore_max: "exclude_largest",
            exclude_biggest: "exclude_largest",
            exclude_smallest: "exclude_smallest",
            ignore_smallest: "exclude_smallest",
            exclude_min: "exclude_smallest",
            ignore_min: "exclude_smallest"
        };
        return aliases[raw] || "";
    }

    function parseComparableIngredientQuantity(item) {
        const rawQuantity = (item?.quantity || "").toString().trim().replace(',', '.');
        const value = Number(rawQuantity);
        if (!Number.isFinite(value) || value <= 0) {
            return null;
        }

        const normalizeUnit = (unit) => (unit || "").toString().toLowerCase().replace(/\s+/g, '').replace(/\./g, '');
        const unit = normalizeUnit(item?.unitDe);
        const aliases = {
            weight: ["g", "gr", "gramm", "gram", "kg", "kilogramm"],
            volume: ["ml", "milliliter", "millilitre", "l", "liter", "litre"],
            piece: ["stk", "stueck", "stück", "piece", "pieces", "pcs", "unit", "units"]
        };

        let family = unit || "raw";
        if (aliases.weight.includes(unit)) family = "weight";
        else if (aliases.volume.includes(unit)) family = "volume";
        else if (aliases.piece.includes(unit)) family = "piece";

        let normalizedValue = value;
        if (unit === "kg" || unit === "kilogramm") normalizedValue = value * UNIT_CONVERSION.KG_TO_GRAMS;
        if (unit === "l" || unit === "liter" || unit === "litre") normalizedValue = value * UNIT_CONVERSION.LITERS_TO_ML;

        return { family, value: normalizedValue };
    }

    function pickComparableQuantityFamily(entries) {
        const counts = new Map();
        (entries || []).forEach((entry, index) => {
            const family = entry?.comparableQuantity?.family;
            if (!family) return;
            if (!counts.has(family)) {
                counts.set(family, { family, count: 0, firstIndex: index });
            }
            counts.get(family).count += 1;
        });

        const rankedFamilies = [...counts.values()]
            .sort((a, b) => (b.count - a.count) || (a.firstIndex - b.firstIndex));

        return rankedFamilies[0]?.family || "";
    }

    function applyQuantityModeToItems(items, quantityMode) {
        const mode = normalizeDisplayQuantityMode(quantityMode);
        const list = Array.isArray(items) ? items.slice() : [];
        if (!mode || list.length < 2) {
            return { items: list, excluded: [] };
        }

        const entries = list.map((item, index) => ({
            item,
            index,
            comparableQuantity: parseComparableIngredientQuantity(item)
        }));

        const comparableFamily = pickComparableQuantityFamily(entries);
        if (!comparableFamily) {
            return { items: list, excluded: [] };
        }

        const comparableEntries = entries.filter(entry => entry?.comparableQuantity?.family === comparableFamily);
        if (comparableEntries.length < 2) {
            return { items: list, excluded: [] };
        }

        const quantitySorted = comparableEntries.slice()
            .sort((a, b) => (a.comparableQuantity.value - b.comparableQuantity.value) || (a.index - b.index));

        if (mode === "exclude_largest" || mode === "exclude_smallest") {
            const target = mode === "exclude_largest"
                ? quantitySorted[quantitySorted.length - 1]
                : quantitySorted[0];
            return {
                items: list.filter(item => item !== target.item),
                excluded: [target.item]
            };
        }

        const preferLargest = mode === "prefer_largest";
        const preferredComparable = quantitySorted
            .slice()
            .sort((a, b) => preferLargest
                ? ((b.comparableQuantity.value - a.comparableQuantity.value) || (a.index - b.index))
                : ((a.comparableQuantity.value - b.comparableQuantity.value) || (a.index - b.index)))
            .map(entry => entry.item);

        const comparableSet = new Set(preferredComparable);
        const rest = list.filter(item => !comparableSet.has(item));
        return { items: preferredComparable.concat(rest), excluded: [] };
    }

    function applyDisplayQuantityMode(entries, quantityMode) {
        const mode = normalizeDisplayQuantityMode(quantityMode);
        if (!mode || !Array.isArray(entries) || entries.length < 2) {
            return entries || [];
        }

        const comparableFamily = pickComparableQuantityFamily(entries);
        if (!comparableFamily) {
            return entries;
        }

        const comparableEntries = entries.filter(entry => entry?.comparableQuantity?.family === comparableFamily);
        if (comparableEntries.length < 2) {
            return entries;
        }

        const quantitySorted = comparableEntries.slice()
            .sort((a, b) => (a.comparableQuantity.value - b.comparableQuantity.value) || (a.index - b.index));

        if (mode === "exclude_largest" || mode === "exclude_smallest") {
            const target = mode === "exclude_largest"
                ? quantitySorted[quantitySorted.length - 1]
                : quantitySorted[0];
            return entries.filter(entry => entry !== target);
        }

        const direction = mode === "prefer_largest" ? -1 : 1;
        return entries.slice().sort((a, b) => {
            const aq = a?.comparableQuantity;
            const bq = b?.comparableQuantity;
            const aComparable = aq?.family === comparableFamily;
            const bComparable = bq?.family === comparableFamily;

            if (aComparable && bComparable && aq.value !== bq.value) {
                return (aq.value - bq.value) * direction;
            }
            if (aComparable && !bComparable) return -1;
            if (!aComparable && bComparable) return 1;
            return 0;
        });
    }

    function selectIngredientsForDisplay(items, varName, masterId) {
        const list = Array.isArray(items) ? items.slice() : [];
        if (!list.length) return [];

        const config = getIngredientDisplayConfig(varName, masterId);
        let candidates = list;

        if (config.excludeDerived) {
            const baseOnly = candidates.filter(item => {
                const itemId = (item?.id || "").toString();
                const sourceId = (item?.sourceBaseId || itemId).toString();
                return !itemId || !sourceId || itemId === sourceId;
            });
            if (baseOnly.length) {
                candidates = baseOnly;
            }
        }

        const rankedEntries = candidates
            .map((item, index) => {
                let score = 0;
                const groupId = (item?.groupId || "").toString();
                const itemId = (item?.id || "").toString();
                const sourceId = (item?.sourceBaseId || itemId).toString();

                if (config.preferBaseSources && itemId && sourceId && itemId === sourceId) {
                    score += 40;
                }

                const preferredGroupIndex = config.preferGroups.indexOf(groupId);
                if (preferredGroupIndex >= 0) {
                    score += SCORE.GROUP_MATCH_BASE - preferredGroupIndex * SCORE.GROUP_MATCH_DECAY;
                }

                const deprioritizedGroupIndex = config.deprioritizeGroups.indexOf(groupId);
                if (deprioritizedGroupIndex >= 0) {
                    score += SCORE.GROUP_DEPRIORITIZE_BASE + deprioritizedGroupIndex * SCORE.GROUP_DEPRIORITIZE_DECAY;
                }

                config.preferFlags.forEach(flag => {
                    if (getIngredientRuleValue(item, flag)) score += SCORE.FLAG_BONUS;
                });

                return {
                    item,
                    index,
                    score,
                    comparableQuantity: config.quantityMode ? parseComparableIngredientQuantity(item) : null
                };
            })
            .sort((a, b) => {
                const scoreDiff = (b.score - a.score);
                if (scoreDiff) return scoreDiff;

                if (config.quantityMode === "prefer_smallest") {
                    const aq = a.comparableQuantity;
                    const bq = b.comparableQuantity;
                    if (aq && bq && aq.family === bq.family && aq.value !== bq.value) {
                        return aq.value - bq.value;
                    }
                    if (aq && !bq) return -1;
                    if (!aq && bq) return 1;
                }

                return a.index - b.index;
            });

        const quantityAdjusted = applyDisplayQuantityMode(rankedEntries, config.quantityMode);
        const ranked = quantityAdjusted.map(entry => entry.item);

        if (!config.limit || ranked.length <= config.limit) {
            return ranked;
        }

        return ranked.slice(0, config.limit);
    }

    // Filters ingredient items based on the variable type (e.g. {{liquid}} → only liquids)
    /**
     * Filters ingredient list based on variable type and step-specific rules.
     * Uses ingredient_match_rules.json for declarative filtering.
     *
     * Filtering rules include:
     * - Boolean flags (is_peelable, is_cookable, etc.)
     * - Group IDs (groupId matching)
     * - Quantity mode (prefer_smallest, exclude_largest, etc.)
     *
     * @param {Array<object>} items - Ingredient objects with id, groupId, is_* flags
     * @param {string} varName - Variable name (e.g., "ingredient", "liquid", "fat")
     * @param {string} masterId - Step master ID (e.g., "PREP_PEEL_01")
     * @returns {object} { filtered: Array, rest: Array } - Matched and unmatched ingredients
     *
     * @example
     * filterIngredientsByVarType(
     *   [{id: 1, name: "Kartoffel", is_peelable: true}, {id: 2, name: "Tomate"}],
     *   "ingredient",
     *   "PREP_PEEL_01"
     * )
     * // Returns: { filtered: [{id: 1, ...}], rest: [{id: 2, ...}] }
     */
    function filterIngredientsByVarType(items, varName, masterId) {
        const { variableRule, stepRule } = getIngredientMatchRule(varName, masterId);
        if (!variableRule && !stepRule) return { filtered: items, rest: [] };

        const filterFn = item => matchesIngredientRule(item, variableRule) && matchesIngredientRule(item, stepRule);
        const filteredBase = (items || []).filter(filterFn);
        const quantityAdjusted = applyQuantityModeToItems(filteredBase, getIngredientQuantityMode(varName, masterId));
        const filtered = quantityAdjusted.items;
        const rest = (items || []).filter(item => !filterFn(item));
        // Return filtered items (empty if no matches)
        return { filtered, rest };
    }

    function isGrindSizeVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "grindsize" || key.includes("grindsize");
    }

    function isNoArticleVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "state" || key === "duration" || key === "count" || key === "mode" || key === "component" || key === "pronoun" || key === "pronoun2" || key === "pronomen" || key === "shape" || key === "finish" || key === "marinade" || key === "method" || key === "thickener" || key === "action" || key === "copula" || isGrindSizeVariable(varName);
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
                isSoft: !!item?.isSoft,
                isPeelable: !!item?.isPeelable,
                isCuttable: !!item?.isCuttable,
                isGrateable: !!item?.isGrateable,
                isFryable: !!item?.isFryable,
                isRoastable: !!item?.isRoastable,
                isGrillable: !!item?.isGrillable,
                isSteamable: !!item?.isSteamable,
                isBoilable: !!item?.isBoilable,
                isSearable: !!item?.isSearable,
                isPoachable: !!item?.isPoachable,
                isSmokable: !!item?.isSmokable,
                isFlambeable: !!item?.isFlambeable,
                isBlendable: !!item?.isBlendable,
                isPowder: !!item?.isPowder,
                quantity: (item?.quantity || "").toString().trim(),
                unitDe: (item?.unitDe || "").toString().trim()
            })).filter(x => x.name);
        }

        const rows = Array.from(document.querySelectorAll("#selectedIngredients .ingredient-row"));
        let items = rows.map(row => {
            const name = (row.querySelector(".ingredient-name-text")?.textContent || "").trim();
            const icon = (row.dataset.groupIcon || row.querySelector(".ingredient-group-icon")?.innerHTML || "").trim();
            const quantity = (row.querySelector(".ingredient-qty-hidden")?.value || "").toString().trim();
            const unitDe = (row.querySelector(".ingredient-unit-hidden")?.value || "").toString().trim();
            return { name, icon, quantity, unitDe };
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
            DEBUG("INGREDIENT", "formatSelectedIngredientList: Processing objects", { names, allIngredients: getSelectedIngredientsFromPage() });
            const allIngredients = getSelectedIngredientsFromPage();
            const frOpts = getFractionOptions();

            const composed = names.map(item => {
                const itemName = getIngredientName(item);
                const itemFraction = getIngredientFraction(item);
                const itemArticle = item.article || ""; // Per-ingredient article
                DEBUG("INGREDIENT", "formatSelectedIngredientList: Processing item", { itemName, itemFraction, itemArticle });

                // Determine final article: use per-ingredient if set, otherwise auto-detect from genus
                let article = "";
                if (itemArticle && itemArticle !== "ohne") {
                    article = itemArticle;
                    DEBUG("INGREDIENT", "formatSelectedIngredientList: Using per-ingredient article", { article });
                } else {
                    // Find full ingredient data to get genus/article
                    const fullIngredient = allIngredients.find(ing => ing.name === itemName);
                    article = fullIngredient?.genusByLang?.[lang] || "";
                    DEBUG("INGREDIENT", "formatSelectedIngredientList: Auto-detected genus", { article });
                }

                // If no fraction, return with article if present
                if (!itemFraction) {
                    // Adjektiv-Endung an Artikel anpassen (z.B. "geschnittene Zwiebel" + "den" → "geschnittenen Zwiebel")
                    const adjustedName = (typeof window.adjustAdjectiveEndingForArticle === 'function')
                        ? window.adjustAdjectiveEndingForArticle(itemName, article, lang)
                        : itemName;
                    if (article && article !== "ohne") {
                        return `${article} ${adjustedName}`;
                    }
                    return adjustedName;
                }

                // Find fraction definition
                const fractionDef = frOpts.find(f => f.key === itemFraction);
                if (!fractionDef) {
                    DEBUG("INGREDIENT", "formatSelectedIngredientList: No fraction def found", { itemFraction });
                    return itemName;
                }
                DEBUG("INGREDIENT", "formatSelectedIngredientList: Found fraction def", { fractionDef });

                // Compose with fraction
                // ✅ FIX (2026-04-02): Genus aus Katalog holen für korrekten Genitiv
                const fullIngredient = allIngredients.find(ing => ing.name === itemName);
                const genus = fullIngredient?.genusByLang?.[lang] || '';
                const result = composeFractionText(fractionDef, itemName, article, genus);
                DEBUG("INGREDIENT", "formatSelectedIngredientList: Composed text", { result });
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


    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 8: UNIFIED OVERLAY SYSTEM
    // ════════════════════════════════════════════════════════════════════════════
    // Universeller Variable-Editor, Overlay-HTML-Generation, Multi-Ingredient-Management

    /**
     * Generates editor HTML for both Smart Step Creator and Probability Area.
     * @param {string} varName - Variable name to edit
     * @param {string} currentVal - Current value
     * @param {object} opts - Options (multiIngredients, masterId, etc.)
     * @returns {object} {html: string, dataAttributes: object}
     */
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
            const isActive = !!selectedItem;
            const remainderCls = isRemainder ? " fraction-remainder" : "";
            const dimCls = dimmed ? " ingredient-chip-dimmed" : "";

            // Check if already added to multi-ingredients list
            const alreadyAdded = multiIngredients.some(item => getIngredientName(item) === value);
            const disabledCls = alreadyAdded ? " ingredient-chip-disabled" : "";
            const disabled = alreadyAdded ? " disabled" : "";

            // ✅ Avocado Pill Design
            let pillStyle = isActive
                ? `background:var(--cp-avocado,#5fa052);color:white;border:1px solid var(--cp-avocado,#5fa052);`
                : `background:white;color:var(--cp-ink-deep,#14391f);border:1px solid var(--cp-ink-line,rgba(20,57,31,0.12));`;

            if (alreadyAdded) {
                pillStyle = `background:#f0f0f0;color:#999;border:1px solid #ddd;opacity:0.6;cursor:not-allowed;`;
            } else if (dimmed) {
                pillStyle += `opacity:0.5;`;
            }

            const safe = escapeHtml(value);
            const iconPart = icon ? `<span class="chip-icon" aria-hidden="true">${icon}</span> ` : "";

            return `<button type="button" class="ingredient-chip${remainderCls}${dimCls}${disabledCls}"
                    style="${pillStyle}border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;cursor:pointer;transition:all 0.15s;"${disabled}
                    data-pick-mode="ingredient-value" data-pick-value="${safe}">${iconPart}${safe}</button>`;
        };

        const valueButtons = ingredientVar
            ? ingredientItems.map(item => renderIngChip(item)).join("")
                + (restIngredientItems.length ? `<span class="ingredient-chip-divider"></span>` + restIngredientItems.map(item => renderIngChip({ ...item, dimmed: true })).join("") : "")
            : renderPillButtons(options, "value", currentVal);

        // Hybrid variable support
        const hybridVar = isHybridIngredientVariable(varName);
        const selectionLabel = getEditorSelectionLabel(varName, hybridVar);
        const hybridOptions = hybridVar ? getVarOptions(varName, masterId, scoringContext) : [];
        const hybridButtons = hybridVar ? renderPillButtons(hybridOptions, "value", currentVal) : "";

        // Fraction picker for ingredient variables
        const fractionOptions = getFractionOptions();
        const fractionPickerHtml = ingredientVar ? renderFractionPickerHtml(fractionOptions, useClassBasedIds) : "";

        // Special editor blocks (duration/temp inputs)
        const specialBlock = renderSpecialEditor(varName, currentVal);

        // Multi-ingredients chips (already added ingredients with remove buttons)
        const frOpts = getFractionOptions();
        const allIngredients = getSelectedIngredientsFromPage(); // ✅ FIX (2026-04-02): Für Genus-Lookup
        const multiIngredientsChips = multiIngredients.length > 0
            ? multiIngredients.map((item, idx) => {
                const frDef = frOpts.find(f => f.key === item.fraction);
                // ✅ FIX (2026-04-02): Genus aus Katalog holen für korrekten Genitiv
                const fullIngredient = allIngredients.find(ing => ing.name === item.name);
                const genus = fullIngredient?.genusByLang?.[currentLang] || '';
                const displayText = item.fraction && frDef
                    ? composeFractionText(frDef, item.name, item.article, genus)
                    : (item.article && item.article !== "ohne" ? `${item.article} ${item.name}` : item.name);
                return `<span class="badge bg-primary me-1 mb-1" style="font-size: 0.85rem; padding: 0.4rem 0.6rem;">
                    ${escapeHtml(displayText)}
                    <button type="button" class="btn-close btn-close-white ms-1" data-remove-multi-ingredient="${idx}" style="font-size: 0.6rem; padding: 0;" aria-label="Entfernen"></button>
                </span>`;
            }).join("")
            : "";

        // ✅ FIX (2026-04-02): Immer Multi-Ingredient-Sektion zeigen (auch wenn leer)
        const multiIngredientsSection = ingredientVar
            ? `<div class="mb-2 pb-2 border-bottom">
                <div class="small text-muted mb-1"><strong>Ausgewählte Zutaten:</strong></div>
                <div>${multiIngredientsChips || '<span class="text-muted small">Noch keine Zutaten ausgewählt. Wähle unten mehrere Zutaten aus.</span>'}</div>
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
            <div class="small text-muted mb-1"><strong>${escapeHtml(selectionLabel)}</strong> auswählen</div>

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

            ${showCombinedEditor ? `<div class="small text-muted mb-1">Zustand</div>` : (showOnlyPronoun ? "" : `<div class="small text-muted mb-1">${escapeHtml(selectionLabel)} einsetzen</div>`)}
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

            ${ingredientVar && !hybridVar ? "" : (hybridVar ? renderFallbackSection(varName, hybridOptions, masterId, scoringContext) : (!ingredientVar && !showOnlyPronoun ? renderFallbackSection(showCombinedEditor ? 'state' : varName, options, masterId, scoringContext) : ""))}

            <div class="d-flex gap-2 align-items-center mt-3 ${CSS_CLASS.JS_EDITOR_ACTION_ROW}">
              <button type="button" class="btn btn-sm ${applyBtnClass}" ${applyBtnId}
                      style="background:var(--cp-gradient-coral, linear-gradient(135deg, #ff9a3c, #ff7849));color:white;border:none;border-radius:12px;padding:10px 18px;font-weight:700;box-shadow:var(--cp-shadow-coral-cta, 0 6px 20px rgba(255, 120, 73, 0.4));flex:1;">
                <i class="bi bi-check-lg me-1"></i>Einsetzen
              </button>
              <button type="button" class="btn btn-sm ${CSS_CLASS.JS_EDITOR_REMOVE_OPTIONAL}" style="display:none;background:transparent;color:var(--cp-coral, #ff7849);border:1px solid var(--cp-coral, #ff7849);border-radius:12px;padding:10px 18px;font-weight:600;" data-var="${escapeHtml(varName)}">
                <i class="bi bi-trash me-1"></i>Entfernen
              </button>
              <button type="button" class="btn btn-sm ${closeBtnClass}" ${closeBtnId}
                      style="background:white;color:var(--cp-ink-deep, #14391f);border:1px solid var(--cp-ink-line, rgba(20, 57, 31, 0.12));border-radius:12px;padding:10px 18px;font-weight:600;">
                <i class="bi bi-x-lg me-1"></i>Schließen
              </button>
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
            if (draftEngine && typeof draftEngine.getActiveStepDraft === 'function') {
                return draftEngine.getActiveStepDraft()?._multiIngredients?.[varName] || [];
            }
            return activeStep?._multiIngredients?.[varName] || [];
        } else if (context.type === 'probability') {
            const masterId = context.probabilityMasterId || context.masterId;
            if (draftEngine && typeof draftEngine.getProbabilityMultiIngredients === 'function') {
                return draftEngine.getProbabilityMultiIngredients(masterId, varName) || [];
            }
            return window.ProbabilityMultiIngredients?.[masterId]?.[varName] || [];
        }
        return [];
    }

    function saveMultiIngredientsForContext(context, varName, values) {
        if (!context || !varName) return;

        if (context.type === 'step') {
            // Smart Step Creator
            const stepDraft = draftEngine && typeof draftEngine.getActiveStepDraft === 'function'
                ? draftEngine.getActiveStepDraft()
                : activeStep;
            if (!stepDraft) return;
            if (!stepDraft._multiIngredients) stepDraft._multiIngredients = {};
            stepDraft._multiIngredients[varName] = values;
        } else if (context.type === 'probability') {
            const masterId = context.probabilityMasterId || context.masterId;
            if (!masterId) return;
            if (draftEngine && typeof draftEngine.setProbabilityMultiIngredients === 'function') {
                draftEngine.setProbabilityMultiIngredients(masterId, varName, values);
                return;
            }
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
        const numInput = host.querySelector(`#${DOM_ID.DURATION_VALUE_INPUT}`);
        const numInputTo = host.querySelector(`#${DOM_ID.DURATION_VALUE_TO_INPUT}`);
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

        console.log("🔵 _handlePickModeClick triggered", { mode, val });
        DEBUG("EVENT", "_handlePickModeClick triggered", { mode, val, useClassBasedIds, pickBtn, host });

        // Handle fraction mode
        if (mode === "fraction") {
            const frRowId = useClassBasedIds ? `.${CSS_CLASS.JS_FRACTION_PICKER_ROW}` : "#FractionPickerRow";
            const frRow = typeof frRowId === "string" && frRowId.startsWith(".")
                ? host.querySelector(frRowId)
                : host.querySelector(frRowId);

            DEBUG("EVENT", "Fraction Mode triggered", { frRowId, frRow, fractionKey: pickBtn.dataset.fractionKey });

            // ✅ FIX (2026-05-04): Inline-Styles für Fraction-Buttons ändern
            const inactiveStyle = `background:white;color:var(--cp-ink-deep,#14391f);border:1px solid var(--cp-ink-line,rgba(20,57,31,0.12));border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;cursor:pointer;transition:all 0.15s;`;
            const activeStyle = `background:var(--cp-avocado,#5fa052);color:white;border:1px solid var(--cp-avocado,#5fa052);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;cursor:pointer;`;

            if (frRow) {
                frRow.querySelectorAll(`.${CSS_CLASS.FRACTION_PICK}`).forEach(b => {
                    b.classList.remove("active");
                    b.style.cssText = inactiveStyle;
                });
                pickBtn.classList.add("active");
                pickBtn.style.cssText = activeStyle;
            }
            host.dataset.selectedFraction = pickBtn.dataset.fractionKey || "";
            DEBUG("EVENT", "Fraction Mode: Set dataset.selectedFraction", { selectedFraction: host.dataset.selectedFraction });
            return;
        }

        // Handle ingredient-value mode (multi-select chips)
        if (mode === "ingredient-value") {
            const selected = JSON.parse(host.dataset.selectedIngredientValues || "[]");
            let list = normalizeIngredientValues(selected);
            const idx = list.findIndex(item => getIngredientName(item) === val);

            DEBUG("EVENT", "Ingredient-Value Mode triggered", { val, selected, list, idx });

            // ✅ FIX (2026-05-04): Inline-Styles für Ingredient-Chips ändern
            const inactiveStyle = `background:white;color:var(--cp-ink-deep,#14391f);border:1px solid var(--cp-ink-line,rgba(20,57,31,0.12));border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;cursor:pointer;transition:all 0.15s;`;
            const activeStyle = `background:var(--cp-avocado,#5fa052);color:white;border:1px solid var(--cp-avocado,#5fa052);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;cursor:pointer;transition:all 0.15s;`;

            if (idx >= 0) {
                list.splice(idx, 1);
                pickBtn.classList.remove("active");
                pickBtn.style.cssText = inactiveStyle;
            } else {
                // ✅ FIX (2026-04-02): Artikel automatisch aus Katalog-Genus konvertieren
                const allIngredients = getSelectedIngredientsFromPage();
                const catalogItem = allIngredients.find(ing => ing.name === val);
                const genusFromCatalog = catalogItem?.genusByLang?.[currentLang] || '';
                const autoArticle = genusToArticle(genusFromCatalog, currentLang);

                list.push({ name: val, fraction: "", article: autoArticle });
                pickBtn.classList.add("active");
                pickBtn.style.cssText = activeStyle;

                DEBUG("INGREDIENT", "Ingredient-Value Mode: Auto-article detected", { genus: genusFromCatalog, article: autoArticle });
            }
            host.dataset.selectedIngredientValues = JSON.stringify(list);
            host.dataset.selectedValue = list.map(item => getIngredientName(item)).join(", ");

            DEBUG("INGREDIENT", "Ingredient-Value Mode: Updated values", { list, selectedIngredientValues: host.dataset.selectedIngredientValues });
            return;
        }

        // Hybrid: clicking an option pill deselects all ingredient chips
        if (mode === "value" && host.dataset.hybrid === "1") {
            host.dataset.selectedIngredientValues = "[]";
            host.querySelectorAll(`.${CSS_CLASS.INGREDIENT_CHIP_ACTIVE}`).forEach(b => b.classList.remove("active"));
        }

        // Toggle active style for article/pronoun/value modes
        let row;
        if (mode === "article") {
            row = useClassBasedIds ? host.querySelector(`.${CSS_CLASS.JS_ARTICLE_BTN_ROW}`) : document.getElementById("ArticleBtnRow");
        } else if (mode === "pronoun") {
            row = useClassBasedIds ? host.querySelector(`.${CSS_CLASS.JS_PRONOUN_BTN_ROW}`) : document.getElementById("PronounBtnRow");
        } else if (mode === "value") {
            row = useClassBasedIds ? host.querySelector(`.${CSS_CLASS.JS_VALUE_BTN_ROW}`) : document.getElementById("ValueBtnRow");
        }

        // ✅ FIX (2026-05-04): Inline-Styles ändern statt nur Klassen (Inline-Styles haben Vorrang!)
        const inactiveStyle = `background:white;color:var(--cp-ink-deep,#14391f);border:1px solid var(--cp-ink-line,rgba(20,57,31,0.12));border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;transition:all 0.15s;`;
        const activeStyle = `background:var(--cp-avocado,#5fa052);color:white;border:1px solid var(--cp-avocado,#5fa052);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;`;

        if (row) {
            row.querySelectorAll("button[data-pick-mode]").forEach(b => {
                if (b.dataset.pickMode === mode) {
                    b.classList.remove("active");
                    b.style.cssText = inactiveStyle;
                }
            });
        }

        // Also toggle in HybridOptionsBtnRow for value mode
        if (mode === "value") {
            const hybridRowSel = useClassBasedIds ? `.${CSS_CLASS.JS_HYBRID_OPTIONS_ROW}` : "#HybridOptionsBtnRow";
            const hybridRow = host.querySelector(hybridRowSel);
            if (hybridRow) {
                hybridRow.querySelectorAll("button[data-pick-mode='value']").forEach(b => {
                    b.classList.remove("active");
                    b.style.cssText = inactiveStyle;
                });
            }
        }

        pickBtn.classList.add("active");
        pickBtn.style.cssText = activeStyle;

        DEBUG("BUTTON", "Button activated with green style", { mode, val, activeStyle });

        // Set dataset values (ensure strings)
        if (mode === "article") {
            host.dataset.selectedArticle = normalizeValueToString(val);
            DEBUG("EVENT", "Article Mode: Set dataset.selectedArticle", { val: host.dataset.selectedArticle });
        }
        if (mode === "pronoun") {
            host.dataset.selectedPronoun = normalizeValueToString(val);
            DEBUG("EVENT", "Pronoun Mode: Set dataset.selectedPronoun", { val: host.dataset.selectedPronoun });
        }
        if (mode === "value") {
            host.dataset.selectedValue = normalizeValueToString(val);
            DEBUG("EVENT", "Value Mode: Set dataset.selectedValue", { val: host.dataset.selectedValue });
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
        let multiIngredients = getMultiIngredientsForContext(context, varName);

        // Fallback: Auto-matched ingredients aus buildVariablesForTemplate übernehmen
        // Nur beim allerersten Öffnen — wenn User schon editiert hat (Key existiert im Draft), nicht mehr
        if (!multiIngredients.length && isIngredientVariable(varName) && context.type === 'probability') {
            const existingDraft = draftEngine?.getProbabilityDraft?.(masterId);
            const hasUserEdited = existingDraft?._multiIngredients && (varName in existingDraft._multiIngredients);
            if (!hasUserEdited) {
                const autoMatched = window._autoMatchedIngredients?.[masterId]?.[varName];
                if (autoMatched?.length) {
                    multiIngredients = autoMatched;
                    saveMultiIngredientsForContext(context, varName, autoMatched);
                }
            }
        }

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
        const existing = document.getElementById(DOM_ID.UNIVERSAL_EDITOR_OVERLAY);
        if (existing) existing.remove();

        // Insert overlay into DOM
        document.body.insertAdjacentHTML('beforeend', overlayHtml);
        const overlayEl = document.getElementById(DOM_ID.UNIVERSAL_EDITOR_OVERLAY);
        if (!overlayEl) {
            console.error("[Unified Overlay] Failed to create overlay element!");
            return;
        }

        // Prevent body scroll
        document.body.style.overflow = 'hidden';

        // Get editor element and set data attributes
        const editorEl = overlayEl.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
        if (!editorEl) {
            console.error("[Unified Overlay] Editor element not found!");
            return;
        }

        // Set data attributes for pre-filled values
        Object.keys(dataAttributes).forEach(key => {
            editorEl.dataset[key] = dataAttributes[key];
        });

        // Show "Entfernen" button if variable is optional (appears inside brackets in template)
        const templateForOptionalCheck = context.templateRaw || activeStep?.templateRaw || "";
        const isVarInBrackets = new RegExp(`\\[[^\\]]*\\{\\{\\s*${varName}\\s*\\}\\}[^\\]]*\\]`).test(templateForOptionalCheck);
        if (isVarInBrackets) {
            const removeBtn = overlayEl.querySelector(`.${CSS_CLASS.JS_EDITOR_REMOVE_OPTIONAL}`);
            if (removeBtn) removeBtn.style.display = '';
        }

        // Bind unified event handlers
        bindUnifiedOverlayEventHandlers(overlayEl, editorEl, {
            varName,
            masterId,
            context,
            onApply,
            onClose
        });
    }

    function openStepDraftEditor(config) {
        const {
            source = 'manual',
            varName,
            currentVal = "",
            masterId = "",
            context,
            onApply,
            onClose
        } = config || {};

        if (!varName || !context) {
            console.error("[openStepDraftEditor] Missing required params:", { source, varName, context });
            return;
        }

        const normalizedContext = Object.assign({}, context, {
            source: (source || context.source || '').toString() || 'manual'
        });

        openUniversalVariableEditor({
            varName,
            currentVal,
            masterId,
            context: normalizedContext,
            onApply,
            onClose
        });
    }

    /**
     * Creates the overlay HTML structure (3-section layout: Header Preview | Scrollable Content | Fixed Footer Buttons)
     */
    function createUnifiedOverlayHtml(editorHtml, context) {
        const theme = document.querySelector(`.${CSS_CLASS.SMART_STEP_CREATOR}`)?.dataset?.theme || 'dark';
        const ownerType = context.type || 'step';

        const previewHtml = renderOverlayPreviewForContext(context) || context.templatePreviewHtml || '';

        // Split editor HTML into content and buttons
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = editorHtml;
        // ✅ FIX: Only select button row with .js-editor-action-row, not ALL .d-flex!
        // Duration inputs also have .d-flex.gap-2.align-items-center, so we need to be specific
        const buttonRow = tempDiv.querySelector(`.${CSS_CLASS.JS_EDITOR_ACTION_ROW}`);
        const buttonsHtml = buttonRow ? buttonRow.outerHTML : '';
        if (buttonRow) buttonRow.remove(); // Remove from editor content
        const editorContentHtml = tempDiv.innerHTML;

        // ✅ AVOCADO DESIGN (2026-05-04): 3-Section Layout
        return `
            <div id="${DOM_ID.UNIVERSAL_EDITOR_OVERLAY}" class="smart-step-creator" data-theme="${theme}" data-overlay-owner="${ownerType}"
                 style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 9999; background: rgba(20, 57, 31, 0.5); backdrop-filter: blur(8px); display: flex; align-items: center; justify-content: center; padding: 16px;">

                <div style="background: var(--cp-card, white); border-radius: var(--cp-radius-xl, 22px); box-shadow: var(--cp-shadow-lg, 0 20px 60px rgba(20, 57, 31, 0.16)); max-width: 720px; width: 100%; max-height: 90vh; display: flex; flex-direction: column; overflow: hidden;">

                    <!-- Header: Preview -->
                    <div class="creator-preview-canvas" style="flex-shrink: 0; padding: 20px; border-bottom: 1px solid rgba(255,255,255,0.1); background: linear-gradient(180deg, var(--cp-ink-deep, #14391f) 0%, var(--cp-ink-deep-2, #0e2415) 100%);">
                        <div style="font-family: var(--cp-font-serif, 'Fraunces', Georgia, serif); font-size: 18px; font-weight: 600; color: white; margin-bottom: 12px;">Vorschau</div>
                        <div class="js-overlay-preview-content">${previewHtml}</div>
                    </div>

                    <!-- Scrollable Middle: Editor -->
                    <div id="${DOM_ID.UNIVERSAL_EDITOR_CONTENT}" style="flex: 1; overflow-y: auto; overflow-x: hidden; padding: 24px 20px; background: white;">
                        ${editorContentHtml}
                    </div>

                    <!-- Footer: Buttons -->
                    <div style="flex-shrink: 0; padding: 16px 20px; border-top: 1px solid var(--cp-ink-line, rgba(20, 57, 31, 0.12)); background: var(--cp-cream, #faf6ec);">
                        ${buttonsHtml}
                        ${ownerType === 'step' ? '<button type="button" class="btn btn-sm" id="btnAcceptStepFromOverlay" style="background:var(--cp-avocado,#5fa052);color:white;border:none;border-radius:12px;padding:10px 18px;font-weight:600;margin-top:12px;width:100%;"><i class="bi bi-check-lg me-1"></i>Step akzeptieren</button>' : ''}
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
            // ✅ FIX (2026-04-02): Auto-save current editor value before closing overlay
            const currentEditorEl = overlayEl.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
            if (currentEditorEl && currentEditorEl.dataset.editorFor) {
                const value = applyEditorValue(currentEditorEl);
                const extras = applyEditorExtras(currentEditorEl);
                const varName = currentEditorEl.dataset.editorFor;

                // Only save if there's a value (don't save empty selections)
                if (value !== null && value !== '') {
                    updateContextValue(context, varName, value, extras);
                }
            }
            closeUnifiedOverlay();
            if (typeof onClose === 'function') onClose();
        });

        // Apply button
        $overlay.on('click.universal', `#${DOM_ID.BTN_APPLY_VAR}`, function(e) {
            e.stopPropagation();  // Prevent old document-level handler from firing
            // ✅ Get fresh editor element (important after variable switch!)
            const currentEditorEl = overlayEl.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
            applyUnifiedEditorValue(currentEditorEl, config);
        });

        $overlay.on('click.universal', `#${DOM_ID.BTN_ACCEPT_PROBABILITY_STEP}`, async function(e) {
            e.stopPropagation();
            if (context.type !== 'probability') return;

            const acceptMasterId = (this.dataset.masterId || context.probabilityMasterId || context.masterId || '').toString().trim();
            if (!acceptMasterId) return;

            saveCurrentEditorValueBeforeAccept('probability', acceptMasterId);
            closeUnifiedOverlay();

            const helpers = window.MasterStepCreatorHelpers || {};
            const probabilityVars = window.probabilityStates?.[acceptMasterId]?.values || {};
            if (typeof helpers.acceptProbabilityTemplateStep === 'function') {
                await helpers.acceptProbabilityTemplateStep(acceptMasterId, probabilityVars);
            }

            if (typeof onClose === 'function') onClose();
        });

        $overlay.on('click.universal', `#${DOM_ID.BTN_ACCEPT_STEP_OVERLAY}`, function(e) {
            e.stopPropagation();
            if (context.type !== 'step') return;

            acceptActiveStep();

            if (typeof onClose === 'function') onClose();
        });

        // ✅ NEU (2026-05-04): Step akzeptieren Button im Footer
        $overlay.on('click.universal', '#btnAcceptStepFromOverlay', function(e) {
            e.stopPropagation();
            if (context.type !== 'step') return;

            // Aktuellen Editor-Wert speichern bevor Accept
            saveCurrentEditorValueBeforeAccept('step', null);
            closeUnifiedOverlay();

            // Step akzeptieren
            acceptActiveStep();

            if (typeof onClose === 'function') onClose();
        });

        // Pick-mode buttons (article, pronoun, value, ingredient, fraction)
        $overlay.on('click.universal', 'button[data-pick-mode]', function(e) {
            e.stopImmediatePropagation();
            // ✅ Get fresh editor element
            const currentEditorEl = overlayEl.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
            if (!currentEditorEl || !currentEditorEl.isConnected) return;
            _handlePickModeClick(this, currentEditorEl, false);
        });

        // Duration unit buttons
        $overlay.on('click.universal', 'button[data-duration-unit]', function(e) {
            e.stopImmediatePropagation();
            // ✅ Get fresh editor element
            const currentEditorEl = overlayEl.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
            _handleDurationUnitClick(this, currentEditorEl);
        });

        // Quick apply buttons for special editors
        $overlay.on('click.universal', '#BtnPickDurationQuick, #BtnPickTempQuick, #BtnPickCountQuick', function(e) {
            e.stopPropagation();  // Prevent old document-level handler from firing
            // ✅ Get fresh editor element
            const currentEditorEl = overlayEl.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
            applyUnifiedEditorValue(currentEditorEl, config);
        });

        // Plus button for multi-ingredients
        $overlay.on('click.universal', `.${CSS_CLASS.JS_ADD_INGREDIENT_TO_LIST}`, function(e) {
            e.stopPropagation();
            // ✅ Get fresh editor element
            const currentEditorEl = overlayEl.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
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
        $overlay.on('click.universal', `.creator-preview-canvas .${CSS_CLASS.TEMPLATE_VAR}[data-var], .creator-preview-canvas .js-probability-var[data-var], .creator-preview-canvas .${CSS_CLASS.JS_OPTIONAL_VAR_ADD}[data-optional-var]`, function(e) {
            e.stopPropagation();
            e.preventDefault();

            const clickedToken = this;
            const newVarName = clickedToken.dataset.optionalVar || clickedToken.dataset.var || clickedToken.dataset.varKey;

            if (!newVarName) {
                console.warn('[Preview Token Click] No var name found!');
                return;
            }

            // Don't switch if it's the same variable
            if (newVarName === config.varName) {
                DEBUG("EVENT", "Preview Token Click: Already editing this variable", { varName: newVarName });
                return;
            }

            DEBUG("EVENT", "Preview Token Click: Switching to variable", { newVarName });
            switchEditorVariable(newVarName, config);
        });

        // Remove optional variable (clear value)
        $overlay.on('click.universal', `.${CSS_CLASS.JS_EDITOR_REMOVE_OPTIONAL}`, function(e) {
            e.stopPropagation();
            const removeVarName = config.varName;
            // Clear the value in context
            updateContextValue(context, removeVarName, '', {});
            // Clear multi-ingredients if any
            saveMultiIngredientsForContext(context, removeVarName, []);
            // Close overlay
            closeUnifiedOverlay();
            if (typeof onClose === 'function') onClose();
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
        DEBUG("DATA", "updateContextValue called", { context: context.type, varName, value, extras });

        // ✅ FIX (2026-05-04): Sicherstellen dass value immer ein String ist, nie ein Objekt
        let normalizedValue = value;
        if (typeof normalizedValue === 'object' && normalizedValue !== null) {
            console.warn('updateContextValue: value is an object, converting to string', { varName, value: normalizedValue });
            // Wenn es ein Objekt mit 'name' property ist, nimm den Namen
            normalizedValue = normalizedValue.name || normalizedValue.value || String(normalizedValue);
        }
        normalizedValue = String(normalizedValue || '');

        if (context.type === 'step') {
            // Smart Step Creator: Update activeStep and re-render
            const stepDraft = getCurrentStepDraft();
            if (!stepDraft) {
                console.error('[updateContextValue] No activeStep available!');
                return;
            }

            // Save value to activeStep (ensure string)
            stepDraft.values[varName] = normalizeValueToString(normalizedValue);

            // Handle extras (e.g., pronoun for state variables)
            if (extras && extras.pronoun) {
                stepDraft.values['pronoun'] = normalizeValueToString(extras.pronoun);
            }

            // Re-render step text
            preserveWindowScroll(() => {
                renderMasterText();
            });

        } else if (context.type === 'probability') {
            // Probability Area: Update probability state object and re-render (UNIFIED!)
            const masterId = context.probabilityMasterId || context.masterId;
        

            ensureProbabilityDraftForEditing(masterId);

            // Get probability state from global dictionary (in CreatePostingPage.js)
            if (!window.probabilityStates || !window.probabilityStates[masterId]) {
                
                return;
            }

            const prob = ensureProbabilityDraftForEditing(masterId);
            

            // Save value to probability state (wie activeStep!)
            if (draftEngine && typeof draftEngine.setProbabilityValue === 'function') {
                draftEngine.setProbabilityValue(masterId, varName, value, extras);
            } else {
                prob.values[varName] = value;
                if (extras && extras.pronoun) {
                    prob.values['pronoun'] = extras.pronoun;
                }
            }
         
        
        }
    }

    /**
     * Updates the preview in the overlay header with fresh content from DOM
     * NEU (2026-03-28): Preview soll nach Wert-Auswahl aktualisiert werden!
     */
    function updateOverlayPreview(context) {
        const overlay = document.getElementById(DOM_ID.UNIVERSAL_EDITOR_OVERLAY);
        if (!overlay) {
            return; // Overlay nicht offen
        }

        // ✅ FIX (2026-05-04): Ziel-Container korrekt finden
        const previewSection = overlay.querySelector('.js-overlay-preview-content');
        if (!previewSection) {
            console.warn('[updateOverlayPreview] Preview section not found in overlay');
            return;
        }

        const sourcePreviewHtml = renderOverlayPreviewForContext(context);

        if (sourcePreviewHtml) {
            previewSection.innerHTML = sourcePreviewHtml;
            DEBUG("OVERLAY", "updateOverlayPreview: Preview updated", { context: context.type });
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

            DEBUG("INGREDIENT", "Unified Apply: Multi-ingredient workflow", {
                existingMulti: existingMulti.length,
                selectedValues: selectedValues.length
            });

            // If we already have ingredients in the list
            if (existingMulti && existingMulti.length > 0) {
                // User selected NEW ingredients to add
                if (selectedValues && selectedValues.length > 0) {
                    DEBUG("INGREDIENT", "Unified Apply: Adding new ingredients to existing list");

                    const article = editorEl.dataset.selectedArticle || '';
                    const fraction = editorEl.dataset.selectedFraction || '';

                    const newNormalized = normalizeIngredientValues(selectedValues);
                    newNormalized.forEach(item => {
                        if (article) item.article = article;
                        else if (!item.article) item.article = '';
                        if (!item.fraction) item.fraction = fraction;
                    });

                    // ✅ FIX (2026-04-02): Entferne Duplikate (gleicher Name, andere Fraktion)
                    // Wenn neue Zutat mit Fraktion hinzugefügt wird, entferne alte Version ohne Fraktion
                    DEBUG("INGREDIENT", "Unified Apply: Filtering duplicates", {
                        existingMulti: JSON.parse(JSON.stringify(existingMulti)),
                        newNormalized: JSON.parse(JSON.stringify(newNormalized)),
                        fraction
                    });

                    let filteredExisting = [...existingMulti];
                    newNormalized.forEach(newItem => {
                        // Entferne existierende Zutat mit gleichem Namen (unabhängig von Fraktion)
                        filteredExisting = filteredExisting.filter(existing =>
                            getIngredientName(existing) !== getIngredientName(newItem)
                        );
                    });

                    // Combine filtered existing + new
                    const combinedList = [...filteredExisting, ...newNormalized];
                    DEBUG("INGREDIENT", "Unified Apply: Combined list created", {
                        filteredExisting: JSON.parse(JSON.stringify(filteredExisting)),
                        combinedList: JSON.parse(JSON.stringify(combinedList))
                    });

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

                    // ✅ FIX (2026-04-02): Chips deselektieren nach Hinzufügen
                    editorEl.dataset.selectedIngredientValues = "[]";
                    editorEl.querySelectorAll('`.${CSS_CLASS.INGREDIENT_CHIP_ACTIVE}`').forEach(btn => btn.classList.remove('active'));

                    // ✅ NEU (2026-03-28): Switch editor statt Close+Reopen (Overlay bleibt offen!)
                    config.currentVal = ingredientComposed;
                    switchEditorVariable(varName, config);
                    return;  // ← Important: Overlay bleibt offen!
                } else {
                    // No new selection — check if user picked a Hybrid option (e.g. "Salzwasser")
                    const hybridValue = editorEl.dataset.selectedValue || '';
                    if (hybridValue && editorEl.dataset.hybrid === "1") {
                        // User hat Hybrid-Option gewählt → Multi-Ingredients löschen
                        DEBUG("INGREDIENT", "Unified Apply: Hybrid override selected", { hybridValue });
                        saveMultiIngredientsForContext(context, varName, []);
                        const article = editorEl.dataset.selectedArticle || '';
                        value = composeArticleAndNoun(article, hybridValue);
                        // Continue to normal context update + close below
                    } else {
                        // Keine Auswahl — existierende Liste beibehalten und CLOSE
                        DEBUG("INGREDIENT", "Unified Apply: No new selection, closing with existing list");
                        const normalized = normalizeIngredientValues(existingMulti);
                        value = formatSelectedIngredientList(normalized, currentLang || 'de');
                        // Continue to normal context update + close below
                    }
                }
            } else {
                // First time - store initial selection
                if (selectedValues && selectedValues.length > 0) {
                    const article = editorEl.dataset.selectedArticle || '';
                    const fraction = editorEl.dataset.selectedFraction || '';

                    const normalized = normalizeIngredientValues(selectedValues);
                    normalized.forEach(item => {
                        if (article) item.article = article;
                        else if (!item.article || item.article === '') item.article = '';
                        if (!item.fraction || item.fraction === '') item.fraction = fraction;
                    });

                    saveMultiIngredientsForContext(context, varName, normalized);
                    value = formatSelectedIngredientList(normalized, currentLang || 'de');
                    DEBUG("INGREDIENT", "Unified Apply: Stored initial multi-ingredients", { normalized });

                    // ✅ FIX (2026-04-02): UPDATE CONTEXT und neu laden (wie bei existingMulti)
                    updateContextValue(context, varName, value, extras);
                    updateOverlayPreview(context);

                    if (typeof onApply === 'function') {
                        onApply(value, extras);
                    }

                    // ✅ FIX (2026-04-02): Editor neu laden damit Badges erscheinen und Chips deselektiert werden
                    config.currentVal = value;
                    switchEditorVariable(varName, config);
                    return;  // ← Important: Overlay bleibt offen aber wird neu geladen!
                }
            }
        }

        // ✅ WICHTIG: Wenn wir hier ankommen (kein Ingredient-Variable oder keine Selection),
        // dann normale Logik verwenden

        DEBUG("OVERLAY", "applyUnifiedEditorValue: Normal path - calling updateContextValue", { varName, value, extras });

        // UPDATE CONTEXT (Step or Probability Token) - UNIFIED for all cases
        updateContextValue(context, varName, value, extras);

        // ✅ NEU (2026-03-28): Preview im Overlay-Header aktualisieren!
        // Nach updateContextValue() wurde renderMasterText/renderProbabilityTemplate aufgerufen
        // → DOM wurde aktualisiert → Preview aus DOM in Overlay kopieren
        updateOverlayPreview(context);

        // Call onApply callback for custom extra logic (optional)
        if (typeof onApply === 'function') {
            onApply(value, extras);
            DEBUG("OVERLAY", "applyUnifiedEditorValue: onApply callback executed");
        } else {
            DEBUG("OVERLAY", "applyUnifiedEditorValue: No onApply callback provided");
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
            if (article) item.article = article;
            else if (!item.article || item.article === '') item.article = '';
            if (!item.fraction || item.fraction === '') item.fraction = fraction;
        });

        // Get existing list and add new items
        const existingList = getMultiIngredientsForContext(context, varName);
        const combinedList = [...existingList, ...normalized];
        saveMultiIngredientsForContext(context, varName, combinedList);

        const composed = formatSelectedIngredientList(combinedList, currentLang || 'de');

        DEBUG("INGREDIENT", "Unified Plus: Added ingredients", { normalized, combinedList, composed });

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
        const overlayEl = document.getElementById(DOM_ID.UNIVERSAL_EDITOR_OVERLAY);
        if (!overlayEl) {
            console.error('[Switch Variable] Overlay not found!');
            return;
        }

        const { context, masterId } = config;

        // Get current value for new variable
        let newVal = '';
        if (context.type === 'step' && getCurrentStepDraft()) {
            newVal = getCurrentStepDraft().values[newVarName] || '';
        } else if (context.type === 'probability') {
            const prob = draftEngine && typeof draftEngine.getProbabilityDraft === 'function'
                ? draftEngine.getProbabilityDraft(masterId)
                : window.probabilityStates[masterId];
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
        const buttonRow = tempDiv.querySelector(`.${CSS_CLASS.JS_EDITOR_ACTION_ROW}`);
        if (buttonRow) buttonRow.remove();
        const editorContentHtml = tempDiv.innerHTML;

        // ✅ FIX (2026-05-04): Ersetze kompletten Container-Inhalt statt nur innerDiv (verhindert Verschachtelung)
        editorContainer.innerHTML = editorContentHtml;

        // Get new editor element and set data attributes
        const newEditorEl = editorContainer.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
        if (newEditorEl) {
            Object.keys(dataAttributes).forEach(key => {
                newEditorEl.dataset[key] = dataAttributes[key];
            });
        }

        // Update "Entfernen" button visibility based on whether new variable is optional
        const removeBtn = overlayEl.querySelector(`.${CSS_CLASS.JS_EDITOR_REMOVE_OPTIONAL}`);
        if (removeBtn) {
            const templateForCheck = context.templateRaw || activeStep?.templateRaw || "";
            const isInBrackets = new RegExp(`\\[[^\\]]*\\{\\{\\s*${newVarName}\\s*\\}\\}[^\\]]*\\]`).test(templateForCheck);
            removeBtn.style.display = isInBrackets ? '' : 'none';
            removeBtn.dataset.var = newVarName;
        }

        // Update config
        config.varName = newVarName;
        config.currentVal = newVal;

        DEBUG("OVERLAY", "Switch Variable: Switched to", { newVarName });
    }

    /**
     * Closes the unified overlay
     */
    function closeUnifiedOverlay() {
        const overlay = document.getElementById(DOM_ID.UNIVERSAL_EDITOR_OVERLAY);
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

        const stepDraft = getCurrentStepDraft();
        if (!stepDraft) {
            console.warn("[openInlineEditor] No active draft available");
            return;
        }
        const currentVal = stepDraft.values[varName] ?? "";
        const masterId = stepDraft.master_id || "";

        // Shared editor entry for manual step selection
        openStepDraftEditor({
            source: 'manual',
            varName: varName,
            currentVal: currentVal,
            masterId: masterId,
            context: {
                type: 'step',
                source: 'manual',
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
        if ((isIngredientVariable(varName) && !isHybridIngredientVariable(varName)) || isCompactSpecialVariable(varName)) return "";
        const optionsMasterId = (contextMasterId || activeStep?.master_id || "").toString();
        const allOptions = getRawVarOptions(varName, optionsMasterId, context);
        const filteredSet = new Set((filteredOptions || []).map(normalizeOptionValue));
        const extras = allOptions.filter(o => !filteredSet.has(normalizeOptionValue(o)));
        if (!extras.length) return "";
        const extraButtons = renderPillButtons(extras, "value", null);
        return `
        <div class="mt-2">
          <button type="button" class="cp-pill ${CSS_CLASS.JS_TOGGLE_FALLBACK_OPTIONS}"
                  style="background:white;color:var(--cp-ink-deep);border:1px solid var(--cp-ink-line);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;"
                  data-expanded="0">
            Weitere anzeigen ▼
          </button>
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
            // ✅ Avocado Pill Design
            const pillStyle = isActive
                ? `background:var(--cp-avocado,#5fa052);color:white;border:1px solid var(--cp-avocado,#5fa052);`
                : `background:white;color:var(--cp-ink-deep,#14391f);border:1px solid var(--cp-ink-line,rgba(20,57,31,0.12));`;
            return `
        <button type="button"
                class="cp-pill"
                style="${pillStyle}border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;transition:all 0.15s;"
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
                class="cp-pill"
                style="background:white;color:var(--cp-ink-deep);border:1px solid var(--cp-ink-line);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;"
                data-duration-unit="${escapeHtml(u.key)}">
          ${escapeHtml(u.label)}
        </button>
      `).join("");

            const perPackageBtn = perPackage ? `
        <div class="mt-2">
          <button type="button"
                  class="cp-pill js-duration-per-package"
                  style="background:white;color:var(--cp-orange,#ff9a3c);border:1px solid var(--cp-orange,#ff9a3c);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;"
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
                  class="cp-pill js-duration-per-package"
                  style="background:white;color:var(--cp-orange);border:1px solid var(--cp-orange);border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;"
                  data-duration-unit="per_package">
            ${escapeHtml(perPackage.label)}
          </button>` : ''}
        </div>

        <div class="d-flex gap-2 align-items-center mt-2 ${CSS_CLASS.JS_EDITOR_ACTION_ROW}">
          <button type="button" class="btn btn-sm" id="BtnPickDurationQuick"
                  style="background:var(--cp-gradient-coral);color:white;border:none;border-radius:12px;padding:10px 18px;font-weight:700;flex:1;">
            <i class="bi bi-check-lg me-1"></i>Einsetzen
          </button>
          <button type="button" class="btn btn-sm" id="BtnCloseVarTop"
                  style="background:white;color:var(--cp-ink-deep);border:1px solid var(--cp-ink-line);border-radius:12px;padding:10px 18px;font-weight:600;">
            <i class="bi bi-x-lg me-1"></i>Schließen
          </button>
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

        <div class="d-flex gap-2 align-items-center mt-2 ${CSS_CLASS.JS_EDITOR_ACTION_ROW}">
          <button type="button" class="btn btn-sm" id="BtnPickTempQuick"
                  style="background:var(--cp-gradient-coral);color:white;border:none;border-radius:12px;padding:10px 18px;font-weight:700;flex:1;">
            <i class="bi bi-check-lg me-1"></i>Einsetzen
          </button>
          <button type="button" class="btn btn-sm" id="BtnCloseVarTop"
                  style="background:white;color:var(--cp-ink-deep);border:1px solid var(--cp-ink-line);border-radius:12px;padding:10px 18px;font-weight:600;">
            <i class="bi bi-x-lg me-1"></i>Schließen
          </button>
        </div>
      `;
        }


        if (varName === "count") {
            return `
        <div class="d-flex gap-2 align-items-center ${CSS_CLASS.JS_EDITOR_ACTION_ROW}">
          <input type="number" min="1" step="1" id="${DOM_ID.COUNT_VALUE_INPUT}"
                 class="cp-input"
                 style="max-width:110px;border:1px solid var(--cp-ink-line);border-radius:12px;padding:10px 14px;"
                 value="${escapeHtml(extractLeadingNumber(currentVal) || "1")}" />
          <button type="button" class="btn btn-sm" id="BtnPickCountQuick"
                  style="background:var(--cp-gradient-coral);color:white;border:none;border-radius:12px;padding:10px 18px;font-weight:700;flex:1;">
            <i class="bi bi-check-lg me-1"></i>Einsetzen
          </button>
          <button type="button" class="btn btn-sm" id="BtnCloseVarTop"
                  style="background:white;color:var(--cp-ink-deep);border:1px solid var(--cp-ink-line);border-radius:12px;padding:10px 18px;font-weight:600;">
            <i class="bi bi-x-lg me-1"></i>Schließen
          </button>
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
        if (!host) host = $("#DOM_ID.INLINE_VAR_EDITOR_HOST");
        if (!host) return;

        const varName = activeToken.varName;

        // Spezialfälle zuerst
        const stepDraft = getCurrentStepDraft();
        if (!stepDraft) return;

        if (varName === "duration") {
            const unit = host.dataset.durationUnit || "minute";
            const labels = getDurationUnits();
            if (unit === "per_package") {
                const perPackageLabel = labels.find(x => x.key === "per_package")?.label ?? "laut Packungsanweisung";
                stepDraft.values[varName] = perPackageLabel;
            } else {
                const n = $(`#${DOM_ID.DURATION_VALUE_INPUT}`)?.value?.trim() || "";
                const nTo = $(`#${DOM_ID.DURATION_VALUE_TO_INPUT}`)?.value?.trim() || "";
                const unitLabel = labels.find(x => x.key === unit)?.label ?? unit;
                const numPart = (n && nTo && nTo !== n) ? `${n}-${nTo}` : n;
                const composed = numPart ? `${numPart} ${unitLabel}` : "";
                if (composed) stepDraft.values[varName] = composed;
            }
            rerenderAfterValueSet();
            return;
        }

        if (varName === "temp") {
            const n = $(`#${DOM_ID.TEMP_VALUE_INPUT}`)?.value?.trim() || "";
            const u = $(`#${DOM_ID.TEMP_UNIT_SELECT}`)?.value || "C";
            const composed = n ? `${n} ${u}` : "";
            if (composed) stepDraft.values[varName] = composed;
            rerenderAfterValueSet();
            return;
        }


        if (varName === "count") {
            const n = $(`#${DOM_ID.COUNT_VALUE_INPUT}`)?.value?.trim() || "1";
            const normalized = /^\d+$/.test(n) ? n : "1";
            stepDraft.values[varName] = normalized;
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
            const multiIngredientsObj = stepDraft._multiIngredients || {};
            const multiIngredients = multiIngredientsObj[varName] || [];
            const article = host.dataset.selectedArticle ?? "";
            const fraction = host.dataset.selectedFraction ?? "";
            const selectedValues = JSON.parse(host.dataset.selectedIngredientValues || "[]");

            DEBUG("INGREDIENT", "applyCurrentEditorSelection: Processing multi-ingredients", { multiIngredients, selectedValues });

            if (multiIngredients && multiIngredients.length > 0) {
                // Check if user selected NEW ingredients to add
                if (selectedValues && selectedValues.length > 0) {
                    // User selected new ingredients - ADD to existing list
                    DEBUG("INGREDIENT", "applyCurrentEditorSelection: Adding new ingredients to existing list");

                    const newNormalized = normalizeIngredientValues(selectedValues);
                    newNormalized.forEach(item => {
                        if (article) item.article = article;
                        else if (!item.article) item.article = '';
                        if (!item.fraction) item.fraction = fraction;
                    });

                    // Combine existing + new
                    const combinedList = [...multiIngredients, ...newNormalized];

                    // Format the combined list
                    let ingredientComposed = formatSelectedIngredientList(combinedList, currentLang);
                    DEBUG("INGREDIENT", "applyCurrentEditorSelection: Combined list created", { combinedList, ingredientComposed });

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
                        stepDraft._fractionData = null;
                    }

                    stepDraft.values[varName] = ingredientComposed;
                    // Store per variable
                    if (!stepDraft._multiIngredients) stepDraft._multiIngredients = {};
                    stepDraft._multiIngredients[varName] = combinedList;
                    DEBUG("DRAFT", "applyCurrentEditorSelection: Updated _multiIngredients", { varName, multiIngredients: stepDraft._multiIngredients[varName] });

                    // Re-open editor to show updated chips (don't close!)
                    renderMasterText();
                    openInlineEditor(varName, activeToken.tokenId);
                    return;
                } else {
                    // No new selection - just use existing list and CLOSE editor
                    DEBUG("INGREDIENT", "applyCurrentEditorSelection: No new selection, closing editor with existing list");
                    const normalized = normalizeIngredientValues(multiIngredients);
                    let ingredientComposed = formatSelectedIngredientList(normalized, currentLang);

                    const hasFractions = normalized.some(item => item.fraction && item.fraction !== "");
                    if (hasFractions) {
                        const frOpts = getFractionOptions();
                        stepDraft._fractionData = normalized
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
                        stepDraft._fractionData = null;
                    }

                    stepDraft.values[varName] = ingredientComposed;
                    // Store per variable
                    if (!stepDraft._multiIngredients) stepDraft._multiIngredients = {};
                    stepDraft._multiIngredients[varName] = normalized;

                    rerenderAfterValueSet();
                    return;
                }
            }

            // Fallback: Single-ingredient selection (old workflow)
            // article, fraction, selectedValues already declared above
            DEBUG("INGREDIENT", "applyCurrentEditorSelection - Fallback: Processing selectedValues", { selectedValues });

            // Hybrid: if no ingredient chips selected, fall through to regular value logic
            if (!Array.isArray(selectedValues) || !selectedValues.length) {
                if (!isHybridIngredientVariable(varName)) return;
                // fall through to normal value handling below
            } else {
                // Single ingredient with article/fraction
                // Manual article selection (from article buttons) overrides auto-detected article
                const normalized = normalizeIngredientValues(selectedValues);
                normalized.forEach(item => {
                    if (article) item.article = article;
                    else if (!item.article) item.article = '';
                    if (!item.fraction) item.fraction = fraction;
                });

                // Check if we should ADD to existing multi-ingredients or create new (per variable)
                const multiIngredientsObj = stepDraft._multiIngredients || {};
                const existingMulti = multiIngredientsObj[varName] || [];
                let combinedList = existingMulti.length > 0 ? [...existingMulti, ...normalized] : normalized;

                // Format the ingredient list
                let ingredientComposed = formatSelectedIngredientList(combinedList, currentLang);
                DEBUG("INGREDIENT", "applyCurrentEditorSelection - Fallback: Lists created", {
                    normalized,
                    existingMulti,
                    varName,
                    combinedList,
                    ingredientComposed
                });

                // Store fraction data
                const hasFractions = combinedList.some(item => item.fraction && item.fraction !== "");
                if (hasFractions) {
                    const frOpts = getFractionOptions();
                    stepDraft._fractionData = combinedList
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
                    stepDraft._fractionData = null;
                }

                stepDraft.values[varName] = ingredientComposed;

                // Store combined list in _multiIngredients for further additions via Plus button (per variable)
                if (!stepDraft._multiIngredients) stepDraft._multiIngredients = {};
                stepDraft._multiIngredients[varName] = combinedList;
                DEBUG("DRAFT", "applyCurrentEditorSelection - Fallback: Stored in _multiIngredients", { varName, multiIngredients: stepDraft._multiIngredients[varName] });

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
            const hasSepState = /\{\{\s*state\s*\}\}/i.test(stepDraft?.templateRaw || "");

            // Save pronoun
            if (pronoun) {
                stepDraft.values["pronoun"] = pronoun;
            }

            // If state selected AND there's a separate {{state}} token, save it there too
            if (value && hasSepState) {
                stepDraft.values["state"] = value;
            }
            // If state selected but NO separate {{state}} token, combine them
            else if (value && !hasSepState) {
                const combined = `${pronoun} ${value}`.trim();
                stepDraft.values["pronoun"] = combined;
            }

            rerenderAfterValueSet();
            return;
        }

        // Combined pronoun+state editor: handle both selections
        if (stateVar) {
            // Always save pronoun if selected (even without state)
            if (pronoun) {
                stepDraft.values["pronoun"] = pronoun;
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

        stepDraft.values[varName] = composed;

        // Auch {pronoun}-Token setzen falls im Template vorhanden
        if (stateVar && pronoun) {
            stepDraft.values["pronoun"] = pronoun;
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
        temp.querySelectorAll(`.${CSS_CLASS.PLACEHOLDER_RESET}, .${CSS_CLASS.OPTIONAL_INLINE_PILL}`).forEach(el => el.remove());
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
        const overlay = document.getElementById(DOM_ID.UNIVERSAL_EDITOR_OVERLAY);
        if (!overlay || overlay.dataset.overlayOwner !== contextType) return false;

        const currentEditorEl = overlay.querySelector(`.${CSS_CLASS.DURATION_EDITOR}`);
        if (!currentEditorEl) return false;

        const varName = currentEditorEl.dataset.editorFor;
        if (!varName) return false;

        // Extract current editor value
        const value = applyEditorValue(currentEditorEl);
        const extras = applyEditorExtras(currentEditorEl);

        // ✅ FIX (2026-04-02): Wenn value null ist, NICHT überschreiben (behält bestehenden Wert)
        // Das passiert z.B. wenn "Step akzeptieren" geklickt wird während Overlay offen ist,
        // aber selectedIngredientValues leer ist (weil bereits via "Einsetzen" gespeichert wurde)
        if (value === null || value === '') {
            DEBUG("DRAFT", "saveCurrentEditorValueBeforeAccept: Value is null/empty, skipping save to preserve existing value");
            return false;
        }

        // Save based on context type (ensure strings)
        if (contextType === 'step') {
            const stepDraft = getCurrentStepDraft();
            if (!stepDraft) return false;
            stepDraft.values[varName] = normalizeValueToString(value);
            if (extras && extras.pronoun) {
                stepDraft.values['pronoun'] = normalizeValueToString(extras.pronoun);
            }
            preserveWindowScroll(() => {
                renderMasterText();
            });
        } else if (contextType === 'probability') {
            const masterId = contextId;
            ensureProbabilityDraftForEditing(masterId);
            if (!window.probabilityStates || !window.probabilityStates[masterId]) return false;
            if (draftEngine && typeof draftEngine.setProbabilityValue === 'function') {
                draftEngine.setProbabilityValue(masterId, varName, normalizeValueToString(value), extras);
            } else {
                window.probabilityStates[masterId].values[varName] = normalizeValueToString(value);
                if (extras && extras.pronoun) {
                    window.probabilityStates[masterId].values['pronoun'] = normalizeValueToString(extras.pronoun);
                }
            }
            if (typeof window.renderProbabilityTemplate === 'function') {
                window.renderProbabilityTemplate(masterId);
            }
        }

        return true; // Value was saved
    }

    function acceptActiveStep() {
        const stepDraft = getCurrentStepDraft();
        if (!stepDraft) return;

        // ✅ NEU (2026-03-28): Gemeinsame Funktion für Editor-Wert speichern
        const overlay = document.getElementById(DOM_ID.UNIVERSAL_EDITOR_OVERLAY);
        const shouldCloseOverlay = !!overlay && overlay.dataset.overlayOwner === 'step';
        saveCurrentEditorValueBeforeAccept('step', null);
        if (shouldCloseOverlay) {
            closeUnifiedOverlay();
        }

        const step = steps.find(s => (s?.master_id || "") === (stepDraft.master_id || ""));
        const payload = {
            de: getRenderedTextForLang(step, "de"),
            en: getRenderedTextForLang(step, "en"),
            esp: getRenderedTextForLang(step, "esp"),
            prt: getRenderedTextForLang(step, "prt"),
            phase: parseInt(step?.phase ?? 0, 10) || 0,
            equipment: parseInt(step?.equipment ?? 0, 10) || 0,
            masterTemplateId: (step?.master_id || stepDraft.master_id || "").toString(),
            stableReference: buildStableStepReference(step)
        };
        const textCurrent = payload[currentLang] || payload.de || payload.en || "";
        const ingredientName = resolveAcceptedIngredientName();

        if (typeof window.addStep === "function") {
            // ✅ NEU (2026-05-04): Wenn wir einen existierenden Step bearbeiten, entferne den alten zuerst
            if (stepDraft._editingExistingStepId) {
                const $existingRow = window.$(`#selectedSteps .step-row[data-step-id="${window.CSS.escape(stepDraft._editingExistingStepId)}"]`);
                if ($existingRow.length) {
                    $existingRow.remove();
                    DEBUG("ACCEPT", "Removed existing step before update", { oldId: stepDraft._editingExistingStepId });
                }
            }

            const generatedId =
                typeof window.createFallbackStepId === "function"
                    ? window.createFallbackStepId()
                    : uid("smart_step");
            window.addStep(String(generatedId), null, textCurrent || `Schritt ${generatedId}`, {
                skipRender: true,
                ingredientName,
                masterTemplateId: payload.masterTemplateId,
                stepData: payload,
                fractionData: stepDraft._fractionData || null
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

            // Reset current step preview after accept
            activeStep = null;
            if (draftEngine && typeof draftEngine.setActiveStepDraft === 'function') {
                draftEngine.setActiveStepDraft(null);
            }
            renderMasterText();

            return;
        }

        const selected = document.querySelector("#selectedSteps");
        if (!selected) return;
        selected.insertAdjacentHTML(
            "beforeend",
            `<div class="dynamic-item d-flex align-items-center step-row"><div class="small flex-grow-1"><span class="step-text-content">${escapeHtml(textCurrent || "Schritt")}</span></div></div>`
        );

        // Reset current step preview after accept
        activeStep = null;
        if (draftEngine && typeof draftEngine.setActiveStepDraft === 'function') {
            draftEngine.setActiveStepDraft(null);
        }
        renderMasterText();
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

        // Equipment Auto-Prefill aus letzter Auswahl
        if (window._lastEquipmentValue && /\{\{\s*equipment\s*\}\}/.test(templateRaw)) {
            activeStep.values.equipment = window._lastEquipmentValue;
        }

        // Ingredient-Group-basierte Defaults anwenden
        var ingredients = getSelectedIngredientsFromPage();
        var primaryIngredient = ingredients.length ? ingredients[0] : null;
        if (primaryIngredient && window.MasterStepRenderer) {
            var groupDefaults = MasterStepRenderer.getSmartDefaults(stepId, {
                ingredientGroupId: primaryIngredient.groupId || ''
            });
            if (groupDefaults) {
                Object.keys(groupDefaults).forEach(function (key) {
                    if (key === 'ingredient' || key === 'ingredients') return;
                    if (groupDefaults[key] && !activeStep.values[key]) {
                        activeStep.values[key] = groupDefaults[key];
                    }
                });
            }
        }

        if (draftEngine && typeof draftEngine.setActiveStepDraft === 'function') {
            draftEngine.setActiveStepDraft(activeStep);
        }

        closeInlineEditor();
        renderMasterText();
        setActiveButton(stepId);
    }

    function setActiveButton(stepId) {
        document.querySelectorAll(`.${CSS_CLASS.TEMPLATE_CARD}.active`).forEach(x => x.classList.remove("active"));
        const btn = document.querySelector(`.${CSS_CLASS.TEMPLATE_CARD}[data-step-id="${CSS.escape(stepId)}"]`);
        if (btn) btn.classList.add("active");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 9: EVENT HANDLERS
    // ════════════════════════════════════════════════════════════════════════════
    // Globale Event-Delegation, Click-Handler, Token-Handler

    /**
     * Binds all global event handlers via delegation.
     * Handles: Step-Cards, Tokens, Buttons, Options, Multi-Ingredient Chips
     */
    function wireEvents() {
        document.addEventListener("click", (e) => {
            // Step Card
            const card = e.target.closest(`.${CSS_CLASS.TEMPLATE_CARD}`);
            if (card) {
                onStepCardClick(card);
                return;
            }

                        // Token in MasterText
            const token = e.target.closest(`.${CSS_CLASS.TEMPLATE_VAR}`);
            if (token) {
                const stepDraft = getCurrentStepDraft();
                if (!stepDraft) return;
                const varName = token.dataset.var || token.dataset.placeholderKey || token.dataset.var;
                const tokenId = token.dataset.tokenId || token.dataset.placeholderTokenId || token.dataset.tokenId;
                // Sequential editing: {state} clicked with separate unfilled {pronoun} → open pronoun first
                if (isStateVariable(varName) && /\{\{\s*pronoun\s*\}\}/i.test(stepDraft.templateRaw || "")) {
                    const pronounToken = document.querySelector("#CurrentStepText .placeholder-token[data-var='pronoun']");
                    if (pronounToken && !((stepDraft.values["pronoun"] || "").toString().trim())) {
                        openInlineEditor("pronoun", pronounToken.dataset.tokenId);
                        return;
                    }
                }
                openInlineEditor(varName, tokenId);
                return;
            }

            const optionalAdd = e.target.closest(`.${CSS_CLASS.JS_OPTIONAL_VAR_ADD}`);
            if (optionalAdd) {
                if (!getCurrentStepDraft()) return;
                const varName = (optionalAdd.dataset.optionalVar || "").toString();
                const tokenId = (optionalAdd.dataset.tokenId || uid("optional")).toString();
                if (!varName) return;
                openInlineEditor(varName, tokenId);
                return;
            }

            // Reset button near token
            const reset = e.target.closest(`.${CSS_CLASS.PLACEHOLDER_RESET}`);
            const stepDraft = getCurrentStepDraft();
            if (reset && stepDraft) {
                const varName = (reset.dataset.var || "").toString().trim();
                if (varName) {
                    delete stepDraft.values[varName];
                    renderMasterText();
                    closeInlineEditor();
                }
                return;
            }

            // "Weitere anzeigen" toggle
            const fallbackToggle = e.target.closest(`.${CSS_CLASS.JS_TOGGLE_FALLBACK_OPTIONS}`);
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

            // ── Remove Multi-Ingredient Chip ──
            const removeChip = e.target.closest("button[data-remove-multi-ingredient]");
            if (removeChip) {
                e.stopPropagation();
                e.preventDefault();

                const idx = parseInt(removeChip.dataset.removeMultiIngredient, 10);
                DEBUG("EVENT", "Remove Chip clicked", { removeChip, idx, activeStep, activeToken });
                if (!activeStep || !activeToken || isNaN(idx)) {
                    DEBUG("EVENT", "Remove Chip: Validation failed");
                    return;
                }

                // Get list for current variable
                const stepDraft = getCurrentStepDraft();
                if (!stepDraft) {
                    return;
                }
                const multiIngredientsObj = stepDraft._multiIngredients || {};
                const existingList = multiIngredientsObj[activeToken.varName] || [];
                if (idx < 0 || idx >= existingList.length) return;

                // Remove item at index
                existingList.splice(idx, 1);
                if (!stepDraft._multiIngredients) stepDraft._multiIngredients = {};
                stepDraft._multiIngredients[activeToken.varName] = existingList;

                // Update preview text
                const composed = formatSelectedIngredientList(existingList, currentLang);
                stepDraft.values[activeToken.varName] = composed;

                DEBUG("INGREDIENT", "Remove Chip: Updated value", { composed });

                renderMasterText();

                // Update overlay preview if it exists
                const overlay = document.getElementById(DOM_ID.UNIVERSAL_EDITOR_OVERLAY);
                if (overlay) {
                    const currentStepWrap = document.querySelector(".current-step-wrap");
                    const previewSection = overlay.querySelector('.creator-preview-canvas > div');
                    if (currentStepWrap && previewSection) {
                        previewSection.innerHTML = currentStepWrap.innerHTML;
                        DEBUG("OVERLAY", "Remove Chip: Updated overlay preview");
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
                if (!host) host = $("#DOM_ID.INLINE_VAR_EDITOR_HOST");
                if (!host) return;
                _handlePickModeClick(pickBtn, host, false); // false = use IDs (Step Creator)
                return;
            }

            // duration unit pick
            const du = e.target.closest("button[data-duration-unit]");
            if (du) {
                let host = document.querySelector('#universalEditorOverlay .duration-editor');
                if (!host) host = $("#DOM_ID.INLINE_VAR_EDITOR_HOST");
                if (!host) return;
                _handleDurationUnitClick(du, host);
                return;
            }

            // quick duration apply
            if (e.target.id === "BtnPickDurationQuick") {
                let host = document.querySelector('#universalEditorOverlay .duration-editor');
                if (!host) host = $("#DOM_ID.INLINE_VAR_EDITOR_HOST");
                if (!host) return;
                if (!host.dataset.durationUnit) host.dataset.durationUnit = "minute";
                const labels = getDurationUnits();
                let composed;
                if (host.dataset.durationUnit === "per_package") {
                    composed = labels.find(x => x.key === "per_package")?.label ?? "laut Packungsanweisung";
                } else if (host.dataset.durationUnit === "short") {
                    composed = labels.find(x => x.key === "short")?.label ?? "kurz";
                } else {
                    const n = $(`#${DOM_ID.DURATION_VALUE_INPUT}`)?.value?.trim() || "";
                    const nTo = $(`#${DOM_ID.DURATION_VALUE_TO_INPUT}`)?.value?.trim() || "";
                    const unitLabel = labels.find(x => x.key === host.dataset.durationUnit)?.label ?? host.dataset.durationUnit;
                    const numPart = (n && nTo && nTo !== n) ? `${n}-${nTo}` : n;
                    composed = numPart ? `${numPart} ${unitLabel}` : "";
                }

                // direkt übernehmen:
                const stepDraft = getCurrentStepDraft();
                if (!stepDraft) return;
                if (composed) stepDraft.values["duration"] = composed;
                rerenderAfterValueSet();
                return;
            }

            if (e.target.id === "BtnPickCountQuick") {
                const n = $(`#${DOM_ID.COUNT_VALUE_INPUT}`)?.value?.trim() || "1";
                const normalized = /^\d+$/.test(n) ? n : "1";
                const stepDraft = getCurrentStepDraft();
                if (!stepDraft) return;
                stepDraft.values["count"] = normalized;
                rerenderAfterValueSet();
                return;
            }
            // quick temp apply
            if (e.target.id === "BtnPickTempQuick") {
                const n = $(`#${DOM_ID.TEMP_VALUE_INPUT}`)?.value?.trim() || "";
                const u = $(`#${DOM_ID.TEMP_UNIT_SELECT}`)?.value || "C";
                const composed = n ? `${n} ${u}` : "";

                const stepDraft = getCurrentStepDraft();
                if (!stepDraft) return;
                stepDraft.values["temp"] = composed;
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
                const stepDraft = getCurrentStepDraft();
                if (stepDraft) {
                    const step = steps.find(s => s.master_id === stepDraft.master_id);
                    stepDraft.templateRaw = step?.templates?.[currentLang] ?? "";
                    closeInlineEditor();
                    renderMasterText();
                    setActiveButton(stepDraft.master_id);
                }
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // SECTION 10: INITIALIZATION & PUBLIC API
    // ════════════════════════════════════════════════════════════════════════════
    // Modul-Initialisierung, Public-API-Export

    /**
     * Initializes the Smart Step Creator module.
     * Loads all required data, sets up event handlers, renders UI.
     * @returns {Promise<void>}
     */
    async function init() {
        try {
            DEBUG("DATA", "Initializing Smart Step Creator...");

            // Load all required data in parallel
            const [masterStepsData, catalogData, matchRulesData] = await Promise.all([
                loadJson(),
                loadVariableCatalog(),
                loadIngredientMatchRules()
            ]);

            // Validate critical data
            if (!masterStepsData) {
                throw new Error("Master steps data is null or undefined");
            }

            doc = masterStepsData;
            variableCatalog = catalogData || { variables: {} };
            ingredientMatchRules = matchRulesData || { variable_rules: {}, step_variable_rules: {} };
            steps = doc.master_steps || [];

            if (steps.length === 0) {
                DEBUG("DATA", "Warning: No master steps found in loaded data");
            }

            DEBUG("DATA", "Data loaded successfully", {
                stepCount: steps.length,
                variableCount: Object.keys(variableCatalog.variables || {}).length
            });

            // Initialize UI
            renderStepButtons();
            renderMasterText();
            wireEvents();

            DEBUG("DATA", "Smart Step Creator initialized successfully");

        } catch (error) {
            DEBUG("DATA", "CRITICAL: Failed to initialize Smart Step Creator", {
                error: error.message,
                stack: error.stack
            });

            // Show user-friendly error message
            const target = masterText();
            if (target) {
                target.innerHTML = `
                    <div class="alert alert-danger" role="alert">
                        <strong>Fehler beim Laden der Master Templates</strong><br>
                        ${escapeHtml(error.message)}<br>
                        <small>Bitte Seite neu laden oder Administrator kontaktieren.</small>
                    </div>
                `;
            }

            // Re-throw to signal initialization failure to caller
            throw error;
        }
    }

    // ── Inline-editor helpers exposed for the probability area ──────────────────────────────────────
    /**
     * Builds inline editor HTML for variable editing (used by Probability Area).
     * Returns HTML string instead of opening a modal - allows embedding in custom containers.
     *
     * @param {string} varName - Variable name to edit (e.g., "ingredient", "state")
     * @param {string} currentVal - Current variable value
     * @param {object} opts - Options
     * @param {boolean} opts.suppressPronounButtons - Hide pronoun buttons
     * @param {string} opts.masterId - Step master ID for context
     * @param {Array} opts.multiIngredients - Multi-ingredient data
     * @returns {string} HTML string with editor UI
     *
     * @example
     * const html = buildInlineEditorHtml("ingredient", "Kartoffel", {
     *   masterId: "PREP_CUT_01",
     *   multiIngredients: []
     * });
     * document.getElementById('editorContainer').innerHTML = html;
     */
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
            const n = (editorEl.querySelector(`#${DOM_ID.DURATION_VALUE_INPUT}`) || {}).value?.trim() || '';
            const nTo = (editorEl.querySelector('#DurationValueToInput') || {}).value?.trim() || '';
            const unitLabel = (units.find(x => x.key === unit) || {}).label || unit;
            const numPart = (n && nTo && nTo !== n) ? `${n}-${nTo}` : n;
            return numPart ? `${numPart} ${unitLabel}` : null;
        }
        if (varName === 'temp') {
            const n = (editorEl.querySelector(`#${DOM_ID.TEMP_VALUE_INPUT}`) || {}).value?.trim() || '';
            const u = (editorEl.querySelector(`#${DOM_ID.TEMP_UNIT_SELECT}`) || {}).value || 'C';
            return n ? `${n} ${u}` : null;
        }
        if (varName === 'count') {
            const n = ((editorEl.querySelector(`#${DOM_ID.COUNT_VALUE_INPUT}`) || {}).value || '1').trim();
            return /^\d+$/.test(n) ? n : '1';
        }
        if (isIngredientVariable(varName)) {
            const selectedValues = JSON.parse(editorEl.dataset.selectedIngredientValues || '[]');
            DEBUG("INGREDIENT", "applyEditorValue: Ingredient variable", { varName, selectedValues });

            // ✅ FIX (2026-04-02): Wenn selectedValues leer, prüfe ob Multi-Ingredients existieren
            if (!Array.isArray(selectedValues) || !selectedValues.length) {
                // Check if we have existing multi-ingredients (already saved via "Einsetzen")
                const existingMulti = activeStep?._multiIngredients?.[varName];
                if (existingMulti && existingMulti.length > 0) {
                    DEBUG("INGREDIENT", "applyEditorValue: Using existing multi-ingredients", { existingMulti });
                    const normalized = normalizeIngredientValues(existingMulti);
                    return formatSelectedIngredientList(normalized, currentLang);
                }
                // Hybrid: if no ingredient chips selected AND no multi-ingredients, fall through
                if (!isHybridIngredientVariable(varName)) return null;
                // fall through
            } else {
                // Normalize to new format (includes per-ingredient articles)
                const normalized = normalizeIngredientValues(selectedValues);

                // Apply global fraction and article to each ingredient (if not already set)
                const globalArticle = editorEl.dataset.selectedArticle || '';
                const globalFraction = editorEl.dataset.selectedFraction || '';

                DEBUG("INGREDIENT", "applyEditorValue: Applying global article/fraction", {
                    globalArticle,
                    globalFraction,
                    before: JSON.parse(JSON.stringify(normalized))
                });

                // ✅ FIX (2026-04-04): Manueller Artikel-Button überschreibt Auto-Artikel
                normalized.forEach(item => {
                    if (globalArticle) item.article = globalArticle;
                    else if (!item.article || item.article === '') item.article = '';
                    if (!item.fraction || item.fraction === '') item.fraction = globalFraction;
                });

                // Format the ingredient list (handles per-ingredient fractions and articles)
                const ingredientResult = formatSelectedIngredientList(normalized, currentLang);

                DEBUG("INGREDIENT", "applyEditorValue: Final result", {
                    after: JSON.parse(JSON.stringify(normalized)),
                    ingredientResult
                });

                return ingredientResult;
            }
        }
        const article = editorEl.dataset.selectedArticle || '';
        let value = editorEl.dataset.selectedValue || '';

        // ✅ FIX (2026-05-04): Wenn value ein Objekt ist, konvertiere zu String
        if (typeof value === 'object' && value !== null) {
            console.warn('applyEditorValue: selectedValue is an object, converting to string', value);
            // Wenn es ein Objekt mit 'name' property ist (z.B. Ingredient), nimm den Namen
            value = value.name || value.value || JSON.stringify(value);
        }
        value = String(value || '');

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
        getIngredientMatchRule, // ← Export für prefer_option_default Prüfung in buildVariablesForTemplate
        renderTemplate,  // ← Export für renderProbabilityTemplate
        renderTemplateDraftHtml,
        renderEditableStepPreview,
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
        openStepDraftEditor,
        openUniversalVariableEditor,
        closeUnifiedOverlay,
        getMultiIngredientsForContext,
        saveMultiIngredientsForContext,
        // Filter functions (Phase 1)
        setSearchQuery,
        setPhaseFilter,
        renderStepButtons,
        // Ingredient matching (Phase 5)
        filterIngredientsByVarType,
        selectIngredientsForDisplay
    });


    document.addEventListener("DOMContentLoaded", init);
})();
(() => {
    var resolveLangKey = window.CreatePostingUtils.resolveLangKey;
    var escapeHtml = window.CreatePostingUtils.escapeHtml;

    /**
     * Formats ingredient names as grammatically correct list with language-specific conjunctions.
     *
     * Examples:
     * - DE: "Kartoffel, Zwiebel und Knoblauch"
     * - EN: "potato, onion and garlic"
     * - ES: "patata, cebolla y ajo"
     *
     * @param {Array<string>} names - Array of ingredient names
     * @param {string} langKey - Language code (de, en, esp, prt, nl, etc.)
     * @returns {string} Formatted ingredient list
     *
     * @example
     * formatIngredientList(["Kartoffel", "Zwiebel", "Knoblauch"], "de")
     * // Returns: "Kartoffel, Zwiebel und Knoblauch"
     */
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
            const pillStyle = isActive
                ? `background:var(--cp-ink-deep);color:white;border:1px solid var(--cp-ink-deep);`
                : `background:white;color:var(--cp-ink-deep);border:1px solid var(--cp-ink-line);`;
            const safe = escapeHtml(name);
            const safeId = escapeHtml(ingId);
            const iconHtml = (ing?.iconHtml || "").toString();
            const label = `${iconHtml ? `${iconHtml} ` : ''}${safe}`;
            return `<button type="button" class="cp-pill inline-equipment-opt js-prob-ingredient-chip"
                    style="${pillStyle}border-radius:999px;padding:7px 14px;font-size:12px;font-weight:600;"
                    data-value="${safe}" data-id="${safeId}">${label}</button>`;
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










