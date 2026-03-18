// master-steps-ui.js
// Erwartet:
// - /data/master_steps.json (wwwroot/data/master_steps.json)
// - <div id="insertContainer"></div>  (Liste der Step-Buttons)
// - <div id="MasterText"></div>       (Aktueller Step + Inline-Editor darunter)
// Optional:
// - <select id="LangSelect"></select>

(() => {
    const JSON_URL = "/data/master_steps.json";
    const OPTION_RULES_URL = "/data/master_step_option_rules.json";
    const DEFAULT_LANG = "de";

    // -----------------------------
    // STATE
    // -----------------------------
    let doc = null;
    let optionRules = { defaults: {}, steps: {} };
    let steps = [];
    let currentLang = DEFAULT_LANG;

    // active step
    let activeStep = null; // { master_id, title, templateRaw, values:{} }
    let activeToken = null; // { varName, tokenId }

    // -----------------------------
    // DOM
    // -----------------------------
    const $ = (s) => document.querySelector(s);
    const insertContainer = () => $("#insertContainer");
    const masterText = () => $("#MasterText");
    const langSelect = () => $("#LangSelect");

    // -----------------------------
    // UTIL
    // -----------------------------
    function escapeHtml(str) {
        return (str ?? "")
            .toString()
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

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
    // LOAD
    // -----------------------------
    async function loadJson() {
        const res = await fetch(`${JSON_URL}?v=${Date.now()}`, { cache: "no-store" });
        if (!res.ok) throw new Error("Konnte master_steps.json nicht laden: " + res.status);
        return await res.json();
    }

    async function loadOptionRules() {
        try {
            const res = await fetch(`${OPTION_RULES_URL}?v=${Date.now()}`, { cache: "no-store" });
            if (!res.ok) return { defaults: {}, steps: {} };
            const data = await res.json();
            return data && typeof data === "object" ? data : { defaults: {}, steps: {} };
        } catch {
            return { defaults: {}, steps: {} };
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
        return (value || "").toString().trim().toLowerCase();
    }

    function getIngredientFamilyForStep(masterId) {
        if (!masterId) return null;
        const step = steps.find(s => (s?.master_id || "") === masterId);
        const fk = step?.stable_taxonomy_keys?.ingredient_family_keys;
        if (!fk) return null;
        return (Array.isArray(fk) ? fk[0] : fk.toString().trim()) || null;
    }

    function getActionForStep(masterId) {
        if (!masterId) return null;
        const step = steps.find(s => (s?.master_id || "") === masterId);
        return (step?.action || "").toString().trim() || null;
    }

    function resolveRuleList(ruleValue) {
        if (!ruleValue) return [];
        if (Array.isArray(ruleValue)) {
            return ruleValue.filter(x => x !== null && x !== undefined)
                .map(x => (x || "").toString().trim())
                .filter(Boolean);
        }
        if (typeof ruleValue === "object") {
            const langKey = (currentLang || DEFAULT_LANG || "de").toLowerCase();
            const list = ruleValue[currentLang] ?? ruleValue[langKey] ?? ruleValue[DEFAULT_LANG] ?? ruleValue.de ?? ruleValue.default;
            return Array.isArray(list)
                ? list.filter(x => x !== null && x !== undefined).map(x => (x || "").toString().trim()).filter(Boolean)
                : [];
        }
        return [];
    }

    function getMergedOptionRules(masterId, varName) {
        const { normalizedKey } = normalizeVarKey(varName);
        const defaults = optionRules?.defaults || {};
        const stepsMap = optionRules?.steps || {};
        const defaultRule = Object.entries(defaults)
            .find(([key]) => normalizeVarKey(key).normalizedKey === normalizedKey)?.[1] || null;
        const stepRule = masterId && stepsMap[masterId] && typeof stepsMap[masterId] === "object"
            ? (Object.entries(stepsMap[masterId]).find(([key]) => normalizeVarKey(key).normalizedKey === normalizedKey)?.[1] || null)
            : null;

        // Action layer
        const actionKey = getActionForStep(masterId);
        const actionRulesMap = optionRules?.action;
        const actionRule = actionKey && actionRulesMap && typeof actionRulesMap[actionKey] === "object"
            ? (Object.entries(actionRulesMap[actionKey]).find(([key]) => normalizeVarKey(key).normalizedKey === normalizedKey)?.[1] || null)
            : null;

        // Ingredient-family layer
        const familyKey = getIngredientFamilyForStep(masterId);
        const familyRulesMap = optionRules?.ingredient_family;
        const familyRule = familyKey && familyRulesMap && typeof familyRulesMap[familyKey] === "object"
            ? (Object.entries(familyRulesMap[familyKey]).find(([key]) => normalizeVarKey(key).normalizedKey === normalizedKey)?.[1] || null)
            : null;

        // Merge priority: step > action > ingredient_family > defaults
        const stepAllowed = resolveRuleList(stepRule?.allowed);
        const actionAllowed = resolveRuleList(actionRule?.allowed);
        const familyAllowed = resolveRuleList(familyRule?.allowed);
        const defaultAllowed = resolveRuleList(defaultRule?.allowed);
        const allowed = stepAllowed.length ? stepAllowed : (actionAllowed.length ? actionAllowed : (familyAllowed.length ? familyAllowed : defaultAllowed));

        const preferred = [...resolveRuleList(stepRule?.preferred), ...resolveRuleList(actionRule?.preferred), ...resolveRuleList(familyRule?.preferred), ...resolveRuleList(defaultRule?.preferred)]
            .filter((value, index, arr) => arr.findIndex(x => normalizeOptionValue(x) === normalizeOptionValue(value)) === index);

        const blocked = [...resolveRuleList(defaultRule?.blocked), ...resolveRuleList(familyRule?.blocked), ...resolveRuleList(actionRule?.blocked), ...resolveRuleList(stepRule?.blocked)]
            .filter((value, index, arr) => arr.findIndex(x => normalizeOptionValue(x) === normalizeOptionValue(value)) === index);

        return { allowed, preferred, blocked };
    }

    function applyOptionRules(baseOptions, varName, masterId) {
        const original = Array.isArray(baseOptions)
            ? baseOptions.filter(x => x !== null && x !== undefined).map(x => (x || "").toString().trim()).filter(Boolean)
            : [];
        if (!original.length) return [];

        const unique = original.filter((value, index, arr) => arr.findIndex(x => normalizeOptionValue(x) === normalizeOptionValue(value)) === index);
        const rules = getMergedOptionRules(masterId, varName);
        const blockedSet = new Set((rules.blocked || []).map(normalizeOptionValue));
        const allowedSet = new Set((rules.allowed || []).map(normalizeOptionValue));
        const preferredOrder = new Map((rules.preferred || []).map((value, index) => [normalizeOptionValue(value), index]));

        let filtered = unique.filter(option => !blockedSet.has(normalizeOptionValue(option)));
        if (allowedSet.size) {
            filtered = filtered.filter(option => allowedSet.has(normalizeOptionValue(option)));
        }
        if (!filtered.length) {
            filtered = unique.filter(option => !blockedSet.has(normalizeOptionValue(option)));
        }

        return filtered.sort((a, b) => {
            const aKey = normalizeOptionValue(a);
            const bKey = normalizeOptionValue(b);
            const aPreferred = preferredOrder.has(aKey);
            const bPreferred = preferredOrder.has(bKey);
            if (aPreferred && bPreferred) return preferredOrder.get(aKey) - preferredOrder.get(bKey);
            if (aPreferred) return -1;
            if (bPreferred) return 1;
            return unique.findIndex(x => normalizeOptionValue(x) === aKey) - unique.findIndex(x => normalizeOptionValue(x) === bKey);
        });
    }

    // liefert ALLE Rohoptionen ohne Filterung (für "Weitere anzeigen" Fallback)
    function getRawVarOptions(varName) {
        if (!doc) return [];
        const { key, normalizedKey } = normalizeVarKey(varName);
        let options = [];
        let vo = doc.variable_options?.[key];
        if (!vo && doc.variable_options && typeof doc.variable_options === "object") {
            const match = Object.entries(doc.variable_options)
                .find(([candidateKey]) => normalizeVarKey(candidateKey).normalizedKey === normalizedKey);
            vo = match ? match[1] : null;
        }
        if (vo && typeof vo === "object") {
            const langKey = (currentLang || DEFAULT_LANG || "de").toLowerCase();
            const list = vo[currentLang] ?? vo[langKey] ?? vo[DEFAULT_LANG] ?? vo.de;
            if (Array.isArray(list)) {
                options = list.filter(x => x !== null && x !== undefined).map(x => (x || "").toString().trim()).filter(Boolean);
            }
        }
        if (!options.length && normalizedKey === "equipment" && doc.equipment) {
            options = Object.values(doc.equipment).map(x => (x || "").toString().trim()).filter(Boolean);
        }
        return options.filter((v, i, a) => a.findIndex(x => normalizeOptionValue(x) === normalizeOptionValue(v)) === i);
    }

    // liefert Liste von Optionen für eine Variable (Buttons)
    function getVarOptions(varName, contextMasterId) {
        if (!doc) return [];

        const { key, normalizedKey } = normalizeVarKey(varName);
        let options = [];

        let vo = doc.variable_options?.[key];
        if (!vo && doc.variable_options && typeof doc.variable_options === "object") {
            const match = Object.entries(doc.variable_options)
                .find(([candidateKey]) => normalizeVarKey(candidateKey).normalizedKey === normalizedKey);
            vo = match ? match[1] : null;
        }
        if (vo && typeof vo === "object") {
            const langKey = (currentLang || DEFAULT_LANG || "de").toLowerCase();
            const list = vo[currentLang] ?? vo[langKey] ?? vo[DEFAULT_LANG] ?? vo.de;
            if (Array.isArray(list)) {
                options = list.filter(x => x !== null && x !== undefined);
            }
        }

        if (!options.length && normalizedKey === "equipment" && doc.equipment) {
            options = Object.values(doc.equipment);
        }

        const masterId = (contextMasterId || activeStep?.master_id || "").toString().trim();
        const ruledOptions = applyOptionRules(options, key, masterId);
        if (normalizedKey === "shape") {
            const blocked = new Set(["fein", "feine", "grob", "grobe"]);
            const filtered = ruledOptions.filter(option => {
                const normalized = normalizeOptionValue(option);
                return normalized && !blocked.has(normalized);
            });
            const hasGehackt = filtered.some(option => normalizeOptionValue(option) === "gehackt");
            return hasGehackt ? filtered : ["gehackt", ...filtered];
        }
        return ruledOptions;
    }
    function getArticleOptions() {
        // variable_options.articles[lang] + "ohne"
        const list = doc?.variable_options?.articles?.[currentLang] ?? doc?.variable_options?.articles?.de ?? [];
        const cleaned = Array.isArray(list) ? list : [];
        return ["ohne", ...cleaned.filter(x => (x ?? "").toString().trim().length > 0)];
    }

    function getDurationUnits() {
        // duration_units: { minute:{de:"Minuten"...}, hour:{de:"Stunden"...}}
        const du = doc?.duration_units;
        if (!du) return [
            { key: "minute", label: "Minute(n)" },
            { key: "hour", label: "Stunde(n)" }
        ];

        const minuteLabel = du.minute?.[currentLang] ?? du.minute?.de ?? "Minuten";
        const hourLabel = du.hour?.[currentLang] ?? du.hour?.de ?? "Stunden";
        return [
            { key: "minute", label: minuteLabel },
            { key: "hour", label: hourLabel }
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

    function renderStepButtons() {
        const container = insertContainer();
        if (!container) return;

        container.innerHTML = "";

        // sortieren: phase -> master_id
        const sorted = [...steps].sort((a, b) => {
            const pa = a.phase ?? 0;
            const pb = b.phase ?? 0;
            if (pa !== pb) return pa - pb;
            return (a.master_id ?? "").localeCompare(b.master_id ?? "");
        });

        // gruppieren
        const byPhase = new Map();
        for (const step of sorted) {
            const p = (step.phase ?? 0).toString();
            if (!byPhase.has(p)) byPhase.set(p, []);
            byPhase.get(p).push(step);
        }

        // render pro Phase
        for (const [phaseKey, phaseSteps] of byPhase.entries()) {
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

            // Steps in Phase
            phaseSteps.forEach(step => {
                const title = step.description ?? "";
                const templateRaw = step.templates?.[currentLang] ?? "";

                    container.insertAdjacentHTML("beforeend", `
                                             <button type="button"
                                                     class="template-card w-100 mb-2"
                                                     data-theme="${window.CreatePostingCurrentTheme || document.querySelector('.smart-step-creator')?.getAttribute('data-theme') || 'gold'}"
                                                     data-step-id="${escapeHtml(step.master_id ?? "")}"
                                                     data-title="${escapeHtml(title)}"
                                                     data-template-raw="${encodeAttr(templateRaw)}">
                                                 <div class="template-title">
                                                         ${escapeHtml(title)}
                                                 </div>
                                                 <div
                                                     class="template-snippet">
                                                         ${snippetPreview(templateRaw)}
                                                 </div>
                                             </button>       
                    `);
            });
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
    function closeInlineEditor() {
        activeToken = null;
        const host = $("#InlineVarEditorHost");
        if (host) host.innerHTML = "";
    }

    function isIngredientVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase();
        return key === "ingredient" || key === "ingredient2" || key === "ingredients" || key === "liquid" || key === "fat";
    }

    function isGrindSizeVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "grindsize" || key.includes("grindsize");
    }

    function isNoArticleVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "state" || key === "duration" || key === "count" || key === "mode" || key === "component" || key === "components" || key === "pronoun" || key === "pronomen" || key === "shape" || key === "finish" || key === "marinade" || key === "method" || key === "thickener" || key === "action" || isGrindSizeVariable(varName);
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
    const EGG_PARTS_BY_LANG = {
        de:  [{ name: "Eiklar",         icon: "🥚" }, { name: "Eigelb",          icon: "🥚" }, { name: "Eischnee",        icon: "🥚" }],
        en:  [{ name: "egg white",      icon: "🥚" }, { name: "egg yolk",        icon: "🥚" }, { name: "beaten egg white", icon: "🥚" }],
        esp: [{ name: "clara de huevo", icon: "🥚" }, { name: "yema de huevo",   icon: "🥚" }, { name: "claras montadas",  icon: "🥚" }],
        prt: [{ name: "clara de ovo",   icon: "🥚" }, { name: "gema de ovo",     icon: "🥚" }, { name: "claras em neve",   icon: "🥚" }],
        id:  [{ name: "putih telur",    icon: "🥚" }, { name: "kuning telur",    icon: "🥚" }, { name: "putih telur kocok",icon: "🥚" }],
        nl:  [{ name: "eiwit",          icon: "🥚" }, { name: "eigeel",          icon: "🥚" }, { name: "opgeklopt eiwit",  icon: "🥚" }],
        sv:  [{ name: "äggvita",        icon: "🥚" }, { name: "äggula",          icon: "🥚" }, { name: "vispad äggvita",   icon: "🥚" }],
        da:  [{ name: "æggehvide",      icon: "🥚" }, { name: "æggeblomme",     icon: "🥚" }, { name: "pisket æggehvide", icon: "🥚" }],
        no:  [{ name: "eggehvite",      icon: "🥚" }, { name: "eggeplomme",     icon: "🥚" }, { name: "pisket eggehvite", icon: "🥚" }],
        ms:  [{ name: "putih telur",    icon: "🥚" }, { name: "kuning telur",    icon: "🥚" }, { name: "putih telur pukul",icon: "🥚" }]
    };

    const EGG_TERMS = ["ei", "eier", "egg", "eggs", "huevo", "huevos", "ovo", "ovos", "telur"];

    function getEggPartsForLang(lang) {
        return EGG_PARTS_BY_LANG[(lang || "de").toLowerCase()] || EGG_PARTS_BY_LANG.de;
    }

    function hasEggIngredient(items) {
        return items.some(x => EGG_TERMS.some(t => x.name.toLowerCase().includes(t)));
    }

    function getEggPartsFromAcceptedSteps() {
        const stepRows = document.querySelectorAll('#selectedSteps .step-row[data-master-template-id="PREP_SEPARATE_01"]');
        if (!stepRows.length) return [];
        const eggParts = getEggPartsForLang(currentLang);
        const result = [];
        stepRows.forEach(row => {
            const refJson = (row.dataset.stepReferenceJson || "").toString();
            const ingName = (row.dataset.ingredientName || "").toString().toLowerCase();
            const hasEgg = EGG_TERMS.some(t => ingName.includes(t) || refJson.toLowerCase().includes(t));
            if (hasEgg) {
                eggParts.forEach(ep => {
                    if (!result.some(r => r.name.toLowerCase() === ep.name.toLowerCase())) {
                        result.push(ep);
                    }
                });
            }
        });
        return result;
    }

    function getSelectedIngredientsFromPage() {
        const rows = Array.from(document.querySelectorAll("#selectedIngredients .ingredient-row"));
        let items = rows.map(row => {
            const name = (row.querySelector(".ingredient-name-text")?.textContent || "").trim();
            const icon = (row.dataset.groupIcon || row.querySelector(".ingredient-group-icon")?.innerHTML || "").trim();
            return { name, icon };
        }).filter(x => x.name);

        const addEggParts = (activeStep && activeStep.master_id === "PREP_SEPARATE_01" && hasEggIngredient(items));
        const eggExtras = addEggParts ? getEggPartsForLang(currentLang) : getEggPartsFromAcceptedSteps();

        if (eggExtras.length) {
            const existing = new Set(items.map(x => x.name.toLowerCase()));
            eggExtras.forEach(ep => {
                if (!existing.has(ep.name.toLowerCase())) {
                    items.push(ep);
                    existing.add(ep.name.toLowerCase());
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
            if (byLower.has(key) && !selected.includes(byLower.get(key))) {
                selected.push(byLower.get(key));
            }
        });
        return selected;
    }

    function formatSelectedIngredientList(names, langKey) {
        if (window.MasterStepCreatorHelpers && typeof window.MasterStepCreatorHelpers.formatIngredientList === "function") {
            return window.MasterStepCreatorHelpers.formatIngredientList(names || [], langKey || currentLang);
        }
        const list = (names || []).map(x => (x || "").toString().trim()).filter(Boolean);
        if (!list.length) return "";
        if (list.length === 1) return list[0];
        if (list.length === 2) return `${list[0]} und ${list[1]}`;
        const head = list.slice(0, -1).join(", ");
        const tail = list[list.length - 1];
        return `${head}, und ${tail}`;
    }

    function openInlineEditor(varName, tokenId) {
        if (!activeStep) return;

        activeToken = { varName, tokenId };

        const host = $("#InlineVarEditorHost");
        if (!host) return;

        const currentVal = activeStep.values[varName] ?? "";
        const compactSpecialVar = isCompactSpecialVariable(varName);
        if (compactSpecialVar) {
            const specialBlockCompact = renderSpecialEditor(varName, currentVal);
            host.innerHTML = `
      <div class="duration-editor mt-2" data-editor-for="${escapeHtml(varName)}">
        <div class="small text-muted mb-1"><strong>${escapeHtml(getVarDisplayName(varName))}</strong> auswählen</div>
        ${specialBlockCompact}
      </div>
    `;
            host.dataset.selectedArticle = "";
            host.dataset.selectedPronoun = "";
            host.dataset.selectedValue = "";
            host.dataset.selectedIngredientValues = "[]";
            return;
        }
        const ingredientVar = isIngredientVariable(varName);
        const noArticleVar = isNoArticleVariable(varName);
        const stateVar = isStateVariable(varName);
        // If template has a separate {{pronoun}} token, don't embed pronoun buttons in state editor
        const hasSeparatePronounToken = /\{\{\s*pronoun\s*\}\}/i.test(activeStep?.templateRaw || "");

                const articleButtons = (ingredientVar || noArticleVar) ? "" : renderPillButtons(getArticleOptions(), "article", null);
        const pronounButtons = (stateVar && !hasSeparatePronounToken) ? renderPillButtons(getVarOptions("pronoun"), "pronoun", null) : "";
        const ingredientItems = ingredientVar ? getSelectedIngredientsFromPage() : [];
        const options = ingredientVar ? ingredientItems.map(x => x.name) : getVarOptions(varName);
        const selectedIngredientValues = ingredientVar ? parseSelectedIngredientValues(currentVal, options) : [];
        const valueButtons = ingredientVar
            ? ingredientItems.map(({ name, icon }) => {
                const value = name.trim();
                const active = selectedIngredientValues.includes(value) ? " active" : "";
                const safe = escapeHtml(value);
                const iconPart = icon ? `<span class="chip-icon" aria-hidden="true">${icon}</span> ` : "";
                return `<button type="button" class="ingredient-chip${active}" data-pick-mode="ingredient-value" data-pick-value="${safe}">${iconPart}${safe}</button>`;
            }).join("")
            : renderPillButtons(options, "value", currentVal);

        // Spezial UI für duration/temp
        const specialBlock = renderSpecialEditor(varName, currentVal);

        host.innerHTML = `
      <div class="duration-editor mt-2" data-editor-for="${escapeHtml(varName)}">
        <div class="small text-muted mb-1"><strong>${escapeHtml(getVarDisplayName(varName))}</strong> auswählen</div>

        ${specialBlock}

        ${(ingredientVar || noArticleVar) ? "" : `
        <div class="small text-muted mt-2 mb-1">Artikel</div>
        <div class="d-flex flex-wrap gap-2 mb-2" id="ArticleBtnRow">
          ${articleButtons}
        </div>
        `}

        ${stateVar ? `
        <div class="small text-muted mt-2 mb-1">Pronomen</div>
        <div class="d-flex flex-wrap gap-2 mb-2" id="PronounBtnRow">
          ${pronounButtons}
        </div>
        ` : ""}

        <div class="small text-muted mb-1">${escapeHtml(getVarDisplayName(varName))} einsetzen</div>
        <div class="d-flex flex-wrap gap-2" id="ValueBtnRow">
          ${valueButtons || (ingredientVar
            ? `<div class="text-muted small">Keine Zutaten ausgewählt.</div>`
            : `<div class="text-muted small">Keine Optionen im JSON gefunden: variable_options.${escapeHtml(varName)}.${escapeHtml(currentLang)}</div>`)}
        </div>

        ${ingredientVar ? "" : renderFallbackSection(varName, options)}

        <div class="d-flex gap-2 align-items-center mt-3">
          <button type="button" class="btn btn-sm creator-cta-primary" id="BtnApplyVar">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVar">Schließen</button>
        </div>
      </div>
    `;

        // preset selected article/value state
        host.dataset.selectedArticle = ""; // "der/die/..." oder "" (=ohne)
        host.dataset.selectedPronoun = "";
        host.dataset.selectedValue = "";   // gewählter Wert
        host.dataset.selectedIngredientValues = JSON.stringify(selectedIngredientValues);
    }

    function renderFallbackSection(varName, filteredOptions) {
        if (isIngredientVariable(varName) || isCompactSpecialVariable(varName)) return "";
        const allOptions = getRawVarOptions(varName);
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
            const unitBtns = units.map(u => `
        <button type="button"
                class="btn btn-sm btn-outline-light pill-like"
                data-duration-unit="${escapeHtml(u.key)}">
          ${escapeHtml(u.label)}
        </button>
      `).join("");

            return `
        <div class="d-flex gap-2 align-items-center">
          <input type="number" min="1" step="1" id="DurationValueInput"
                 class="form-control form-control-sm"
                 style="max-width:110px; background:#ffffff !important; color:#111827 !important; -webkit-text-fill-color:#111827 !important; caret-color:#111827 !important; text-shadow:none !important;"
                 value="${escapeHtml(extractLeadingNumber(currentVal) || "10")}" />
          <button type="button" class="btn btn-sm btn-outline-light" id="BtnPickDurationQuick">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVarTop">Schließen</button>
        </div>

        <div class="d-flex flex-wrap gap-2 mt-2">
          ${unitBtns}
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

        <div class="d-flex gap-2 align-items-center mt-2">
          <button type="button" class="btn btn-sm btn-outline-light" id="BtnPickTempQuick">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVarTop">Schließen</button>
        </div>
      `;
        }


        if (varName === "count") {
            return `
        <div class="d-flex gap-2 align-items-center">
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

        const host = $("#InlineVarEditorHost");
        if (!host) return;

        const varName = activeToken.varName;

        // Spezialfälle zuerst
        if (varName === "duration") {
            const n = $("#DurationValueInput")?.value?.trim() || "";
            const unit = host.dataset.durationUnit || "minute";
            const labels = getDurationUnits();
            const unitLabel = labels.find(x => x.key === unit)?.label ?? unit;
            const composed = n ? `${n} ${unitLabel}` : "";
            if (composed) activeStep.values[varName] = composed;
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
            const selectedValues = JSON.parse(host.dataset.selectedIngredientValues || "[]");
            if (!Array.isArray(selectedValues) || !selectedValues.length) return;
            activeStep.values[varName] = formatSelectedIngredientList(selectedValues, currentLang);
            rerenderAfterValueSet();
            return;
        }

        let composed = value;

        // Artikel nur wenn nicht "ohne"
        if (!noArticleVar && article && article !== "ohne") {
            composed = `${article} ${value}`.trim();
        }

        const hasSepPronoun = /\{\{\s*pronoun\s*\}\}/i.test(activeStep?.templateRaw || "");
        if (stateVar && !hasSepPronoun && pronoun && value) {
            composed = `${pronoun} ${value}`.trim();
        }

        // wenn nur artikel geklickt aber kein value -> nix setzen
        if (!value) return;

        // Detect if we should auto-open state editor after pronoun
        const isPronounToken = varName === "pronoun";
        const templateRaw = activeStep?.templateRaw || "";
        const stateUnfilled = !((activeStep.values["state"] || "").toString().trim());
        const shouldAutoState = isPronounToken &&
            /\{\{\s*state\s*\}\}/i.test(templateRaw) &&
            stateUnfilled;

        activeStep.values[varName] = composed;
        // Auch {pronoun}-Token setzen falls im Template vorhanden
        if (stateVar && pronoun) {
            activeStep.values["pronoun"] = pronoun;
        }
        rerenderAfterValueSet();

        if (shouldAutoState) {
            const stateToken = document.querySelector("#CurrentStepText .placeholder-token[data-var='state']");
            if (stateToken) {
                openInlineEditor("state", stateToken.dataset.tokenId);
            }
        }
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
        const rendered = renderTemplate(templateRaw, activeStep?.master_id || "step", activeStep?.values || {});
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

    function acceptActiveStep() {
        if (!activeStep) return;

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
                stepData: payload
            });
            if (typeof window.updateStepIndices === "function") {
                window.updateStepIndices();
            } else if (typeof window.updateStoryProgress === "function") {
                window.updateStoryProgress();
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

            // Artikel/Wert Button Picks
            const pickBtn = e.target.closest("button[data-pick-mode]");
            if (pickBtn) {
                const host = $("#InlineVarEditorHost");
                if (!host) return;

                const mode = pickBtn.dataset.pickMode;
                const val = pickBtn.dataset.pickValue ?? "";

                if (mode === "ingredient-value") {
                    const selected = JSON.parse(host.dataset.selectedIngredientValues || "[]");
                    const list = Array.isArray(selected) ? selected : [];
                    const idx = list.indexOf(val);
                    if (idx >= 0) {
                        list.splice(idx, 1);
                        pickBtn.classList.remove("active");
                    } else {
                        list.push(val);
                        pickBtn.classList.add("active");
                    }
                    host.dataset.selectedIngredientValues = JSON.stringify(list);
                    host.dataset.selectedValue = list[0] || "";
                    return;
                }

                // toggle active style
                const row = mode === "article" ? $("#ArticleBtnRow") : (mode === "pronoun" ? $("#PronounBtnRow") : $("#ValueBtnRow"));
                if (row) row.querySelectorAll("button[data-pick-mode]").forEach(b => {
                    if (b.dataset.pickMode === mode) b.classList.remove("active");
                });
                pickBtn.classList.add("active");

                if (mode === "article") host.dataset.selectedArticle = val;
                if (mode === "pronoun") host.dataset.selectedPronoun = val;
                if (mode === "value") host.dataset.selectedValue = val;
                return;
            }

            // duration unit pick
            const du = e.target.closest("button[data-duration-unit]");
            if (du) {
                const host = $("#InlineVarEditorHost");
                if (!host) return;
                host.dataset.durationUnit = du.dataset.durationUnit;

                // aktiv markieren
                du.parentElement?.querySelectorAll("button[data-duration-unit]")?.forEach(b => b.classList.remove("active"));
                du.classList.add("active");
                return;
            }

            // quick duration apply
            if (e.target.id === "BtnPickDurationQuick") {
                const host = $("#InlineVarEditorHost");
                if (!host) return;
                if (!host.dataset.durationUnit) host.dataset.durationUnit = "minute";
                const n = $("#DurationValueInput")?.value?.trim() || "";
                const labels = getDurationUnits();
                const unitLabel = labels.find(x => x.key === host.dataset.durationUnit)?.label ?? host.dataset.durationUnit;
                const composed = n ? `${n} ${unitLabel}` : "";

                // direkt übernehmen:
                activeStep.values["duration"] = composed;
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
            const loaded = await Promise.all([loadJson(), loadOptionRules()]);
            doc = loaded[0];
            optionRules = loaded[1] || { defaults: {}, steps: {} };
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
    function buildInlineEditorHtml(varName, currentVal, opts) {
        const suppressPronounButtons = !!(opts && opts.suppressPronounButtons);
        const optionsMasterId = (opts && opts.masterId) || activeStep?.master_id || "";
        const compactSpecialVar = isCompactSpecialVariable(varName);
        if (compactSpecialVar) {
            const specialBlock = renderSpecialEditor(varName, currentVal);
            // renderSpecialEditor already includes BtnPickDurationQuick/BtnCloseVarTop
            return `<div class="duration-editor prob-inline-editor" data-editor-for="${escapeHtml(varName)}" data-selected-article="" data-selected-pronoun="" data-selected-value="" data-duration-unit="minute" data-selected-ingredient-values="[]">
  <div class="small text-muted mb-1"><strong>${escapeHtml(getVarDisplayName(varName))}</strong> auswählen</div>
  ${specialBlock}
</div>`;
        }

        const ingredientVar = isIngredientVariable(varName);
        const noArticleVar = isNoArticleVariable(varName);
        const stateVar = isStateVariable(varName);
        const rawCurrentVal = (currentVal || '').toString().trim();
        const articleOptions = getArticleOptions();
        const pronounOptions = getVarOptions('pronoun', optionsMasterId);
        let prefilledArticle = '';
        let prefilledPronoun = '';
        let prefilledValue = rawCurrentVal;

        if (stateVar && !suppressPronounButtons && rawCurrentVal) {
            const pronounMatch = pronounOptions.find(option => {
                const value = (option || '').toString().trim();
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
            const articleMatch = articleOptions.find(option => {
                const value = (option || '').toString().trim();
                if (!value || value === 'ohne') return false;
                const lowerCurrent = rawCurrentVal.toLowerCase();
                const lowerOption = value.toLowerCase();
                return lowerCurrent === lowerOption || lowerCurrent.startsWith(`${lowerOption} `);
            });

            if (articleMatch) {
                prefilledArticle = articleMatch;
                prefilledValue = rawCurrentVal.slice(articleMatch.length).trim();
            }
        }

        const articleButtons = (ingredientVar || noArticleVar) ? '' : renderPillButtons(articleOptions, 'article', prefilledArticle);
        const pronounButtons = (stateVar && !suppressPronounButtons) ? renderPillButtons(['', ...pronounOptions], 'pronoun', prefilledPronoun) : '';
        const ingredientItems = ingredientVar ? getSelectedIngredientsFromPage() : [];
        const options = ingredientVar ? ingredientItems.map(x => x.name) : getVarOptions(varName, optionsMasterId);
        const selectedIngredientValues = ingredientVar ? parseSelectedIngredientValues(currentVal, options) : [];
        const valueButtons = ingredientVar
            ? ingredientItems.map(({ name, icon }) => {
                const value = name.trim();
                const active = selectedIngredientValues.includes(value) ? ' active' : '';
                const safe = escapeHtml(value);
                const iconPart = icon ? `<span class="chip-icon" aria-hidden="true">${icon}</span> ` : '';
                return `<button type="button" class="ingredient-chip${active}" data-pick-mode="ingredient-value" data-pick-value="${safe}">${iconPart}${safe}</button>`;
            }).join('')
            : renderPillButtons(options, 'value', prefilledValue);
        const specialBlock = renderSpecialEditor(varName, currentVal);

        return `<div class="duration-editor prob-inline-editor" data-editor-for="${escapeHtml(varName)}" data-selected-article="${escapeHtml(prefilledArticle)}" data-selected-pronoun="${escapeHtml(prefilledPronoun)}" data-selected-value="${escapeHtml(prefilledValue)}" data-duration-unit="minute" data-selected-ingredient-values="${escapeHtml(JSON.stringify(selectedIngredientValues))}">
  <div class="small text-muted mb-1"><strong>${escapeHtml(getVarDisplayName(varName))}</strong> auswählen</div>
  ${specialBlock}
  ${(ingredientVar || noArticleVar) ? '' : `<div class="small text-muted mt-2 mb-1">Artikel</div><div class="d-flex flex-wrap gap-2 mb-2 js-article-btn-row">${articleButtons}</div>`}
  ${stateVar ? `<div class="small text-muted mt-2 mb-1">Pronomen</div><div class="d-flex flex-wrap gap-2 mb-2 js-pronoun-btn-row">${pronounButtons}</div>` : ''}
  <div class="d-flex flex-wrap gap-2 js-value-btn-row">
    ${valueButtons || (ingredientVar
        ? '<div class="text-muted small">Keine Zutaten ausgewählt.</div>'
        : `<div class="text-muted small">Keine Optionen: ${escapeHtml(getVarDisplayName(varName))}</div>`)}
  </div>
  ${ingredientVar ? '' : renderFallbackSection(varName, options)}
  <div class="d-flex gap-2 align-items-center mt-3">
    <button type="button" class="btn btn-sm creator-cta-primary js-prob-inline-apply">Einsetzen</button>
    <button type="button" class="btn btn-sm btn-outline-secondary js-prob-inline-close">Schließen</button>
  </div>
</div>`;
    }

    // applyEditorValue: reads the prob-inline-editor element and returns the composed value.
    function applyEditorValue(editorEl) {
        if (!editorEl) return null;
        const varName = (editorEl.dataset.editorFor || '').trim();

        if (varName === 'duration') {
            const n = (editorEl.querySelector('#DurationValueInput') || {}).value?.trim() || '';
            const unit = editorEl.dataset.durationUnit || 'minute';
            const units = getDurationUnits();
            const unitLabel = (units.find(x => x.key === unit) || {}).label || unit;
            return n ? `${n} ${unitLabel}` : null;
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
            if (!Array.isArray(selectedValues) || !selectedValues.length) return null;
            return formatSelectedIngredientList(selectedValues, currentLang);
        }
        const article = editorEl.dataset.selectedArticle || '';
        const value = editorEl.dataset.selectedValue || '';
        if (!value) return null;
        return (article && article !== 'ohne') ? `${article} ${value}` : value;
    }

    // applyEditorExtras: returns companion variable values (e.g. pronoun for state vars).
    // Use alongside applyEditorValue when the template may have both {state} and {pronoun} tokens.
    function applyEditorExtras(editorEl) {
        if (!editorEl) return null;
        const varName = (editorEl.dataset.editorFor || '').trim();
        if (!isStateVariable(varName)) return null;
        const pronoun = (editorEl.dataset.selectedPronoun || '').trim();
        return pronoun ? { pronoun } : null;
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
        renderTemplateWithConfig,
        buildInlineEditorHtml,
        applyEditorValue,
        applyEditorExtras,
        triggerPronounBeforeState,
        getVarDisplayName
    });

    document.addEventListener("DOMContentLoaded", init);
})();
(() => {
    function resolveLangKey(value) {
        const normalized = (value || "de").toString().toLowerCase();
        if (normalized === "es") return "esp";
        if (normalized === "pt") return "prt";
        if (normalized === "se") return "sv";
        if (normalized === "dk") return "da";
        return normalized;
    }

    function escapeHtml(value) {
        return (value ?? "")
            .toString()
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

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










