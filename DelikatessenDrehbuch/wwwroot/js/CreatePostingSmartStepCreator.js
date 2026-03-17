// master-steps-ui.js
// Erwartet:
// - /data/master_steps.json (wwwroot/data/master_steps.json)
// - <div id="insertContainer"></div>  (Liste der Step-Buttons)
// - <div id="MasterText"></div>       (Aktueller Step + Inline-Editor darunter)
// Optional:
// - <select id="LangSelect"></select>

(() => {
    const JSON_URL = "/data/master_steps.json";
    const DEFAULT_LANG = "de";

    // -----------------------------
    // STATE
    // -----------------------------
    let doc = null;
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

    // -----------------------------
    // LOAD
    // -----------------------------
    async function loadJson() {
        const res = await fetch(`${JSON_URL}?v=${Date.now()}`, { cache: "no-store" });
        if (!res.ok) throw new Error("Konnte master_steps.json nicht laden: " + res.status);
        return await res.json();
    }

    // -----------------------------
    // OPTION LOOKUP (NUR JSON!)
    // -----------------------------
    // liefert Liste von Optionen für eine Variable (Buttons)
    function getVarOptions(varName) {
        if (!doc) return [];

        const key = (varName || "").toString().trim();
        const normalizedKey = key.toLowerCase().replace(/[_\\-\\s]+/g, "");
        const aliasMap = { pronomen: "pronoun" };
        const alias = aliasMap[normalizedKey] || null;
        const effectiveKey = alias || key;
        const effectiveNormalizedKey = (alias || normalizedKey).toLowerCase().replace(/[_\\-\\s]+/g, "");

        // variable_options[varName][lang] (robust gegen _/-/Case)
        let vo = doc.variable_options?.[effectiveKey];
        if (!vo && doc.variable_options && typeof doc.variable_options === "object") {
            const match = Object.entries(doc.variable_options)
                .find(([k]) => (k || "").toString().toLowerCase().replace(/[_\-\s]+/g, "") === effectiveNormalizedKey);
            vo = match ? match[1] : null;
        }
        if (vo && typeof vo === "object") {
            const langKey = (currentLang || DEFAULT_LANG || "de").toLowerCase();
            const list = vo[currentLang] ?? vo[langKey] ?? vo[DEFAULT_LANG] ?? vo.de;
            if (Array.isArray(list)) return list.filter(x => x !== null && x !== undefined);
        }

        // Spezialfall: equipment Map
        if (normalizedKey === "equipment" && doc.equipment) {
            return Object.values(doc.equipment);
        }

        return [];
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

            const label = varsInGroup.join(" / ");
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
            const label      = inner.replace(/{{\s*([^}]+?)\s*}}/g, "$1");
            // Encode as sentinel-delimited marker (safe from {{}} regex)
            return `${PILL_START}${firstVar}${PILL_SEP}${tokenId}${PILL_SEP}${label}${PILL_END}`;
        });

        // Pass 2: render regular {{var}} tokens
        let tokenIndex = 0;
        processed = processed.replace(/{{\s*([^}]+?)\s*}}/g, (_m, varRaw) => {
            const varName = (varRaw ?? "").trim();
            const tid     = `${stepId}_${varName}_${tokenIndex++}`;
            const val     = (values[varName] ?? "").toString();
            const display = val.trim().length > 0 ? val : varName;
            return `<span class="token-highlight placeholder-token template-var" draggable="false" data-var="${escapeHtml(varName)}" data-token-id="${escapeHtml(tid)}" data-has-value="${val.trim().length > 0 ? "1" : "0"}">${escapeHtml(display)}</span><button type="button" class="placeholder-reset" data-token-id="${escapeHtml(tid)}" data-var="${escapeHtml(varName)}" title="Zurücksetzen" aria-label="Zurücksetzen"><i class="bi bi-arrow-counterclockwise" aria-hidden="true"></i></button>`;
        });

        // Pass 3: replace pill markers with inline pill HTML
        processed = processed.replace(
            new RegExp(`\\x01([^\\x02]*)\\x02([^\\x02]*)\\x02([^\\x03]*)\\x03`, "g"),
            (_m, firstVar, tokenId, label) =>
                `<span class="optional-inline-pill js-optional-var-add" role="button" tabindex="0" data-optional-var="${escapeHtml(firstVar)}" data-token-id="${escapeHtml(tokenId)}" title="Optional hinzufügen: ${escapeHtml(label)}"><i class="bi bi-plus-circle-dotted" aria-hidden="true"></i><span class="optional-pill-label">${escapeHtml(label)}</span></span>`
        );

        return processed;
    }

    // Renders a read-only template preview for the step-card list.
    // Optional [{{var}}] segments appear as small faded badges instead of raw [brackets].
    function snippetPreview(templateRaw) {
        if (!templateRaw) return "";
        const optionalPattern = /\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]/g;
        const parts = [];
        let lastIndex = 0;
        let match;
        while ((match = optionalPattern.exec(templateRaw)) !== null) {
            const textBefore = templateRaw.slice(lastIndex, match.index)
                .replace(/{{\s*([^}]+?)\s*}}/g, "$1");
            parts.push(escapeHtml(textBefore));
            const inner  = (match[1] ?? match[2] ?? "").toString();
            const vars   = getTemplateVariables(inner);
            const label  = inner.replace(/{{\s*([^}]+?)\s*}}/g, "$1");
            parts.push(`<span class="opt-snippet-badge"><i class="bi bi-plus-circle-dotted" aria-hidden="true"></i> ${escapeHtml(label)}</span>`);
            lastIndex = match.index + match[0].length;
        }
        const remaining = templateRaw.slice(lastIndex).replace(/{{\s*([^}]+?)\s*}}/g, "$1");
        parts.push(escapeHtml(remaining));
        return parts.join("");
    }

    // -----------------------------
    // RENDER: Step Button Liste
    // -----------------------------
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
    function closeInlineEditor() {
        activeToken = null;
        const host = $("#InlineVarEditorHost");
        if (host) host.innerHTML = "";
    }

    function isIngredientVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase();
        return key === "ingredient" || key === "ingredients" || key === "liquid" || key === "fat";
    }

    function isGrindSizeVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "grindsize" || key.includes("grindsize");
    }

    function isNoArticleVariable(varName) {
        const key = (varName || "").toString().trim().toLowerCase().replace(/_/g, "");
        return key === "state" || key === "duration" || key === "count" || key === "item" || key === "mode" || key === "component" || key === "components" || key === "pronoun" || key === "pronomen" || isGrindSizeVariable(varName);
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
    function getSelectedIngredientsFromPage() {
        const rows = Array.from(document.querySelectorAll("#selectedIngredients .ingredient-row"));
        return rows.map(row => {
            const name = (row.querySelector(".ingredient-name-text")?.textContent || "").trim();
            const icon = (row.dataset.groupIcon || row.querySelector(".ingredient-group-icon")?.innerHTML || "").trim();
            return { name, icon };
        }).filter(x => x.name);
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
        <div class="small text-muted mb-1">Wert für <strong>${escapeHtml(varName)}</strong></div>
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
        const options = ingredientVar ? getSelectedIngredientNamesFromPage() : getVarOptions(varName);
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
        <div class="small text-muted mb-1">Wert für <strong>${escapeHtml(varName)}</strong></div>

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

        <div class="small text-muted mb-1">${escapeHtml(varName)} einsetzen</div>
        <div class="d-flex flex-wrap gap-2" id="ValueBtnRow">
          ${valueButtons || (ingredientVar
            ? `<div class="text-muted small">Keine Zutaten ausgewählt.</div>`
            : `<div class="text-muted small">Keine Optionen im JSON gefunden: variable_options.${escapeHtml(varName)}.${escapeHtml(currentLang)}</div>`)}
        </div>

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
        <div class="d-flex gap-2 align-items-center">
          <input type="number" min="0" step="1" id="TempValueInput"
                 class="form-control form-control-sm"
                 style="max-width:110px; background:#ffffff !important; color:#111827 !important; -webkit-text-fill-color:#111827 !important; caret-color:#111827 !important; text-shadow:none !important;"
                 value="${escapeHtml(extractLeadingNumber(currentVal) || "180")}" />

          <select id="TempUnitSelect" class="form-select form-select-sm" style="max-width:140px; background:#ffffff !important; color:#111827 !important; -webkit-text-fill-color:#111827 !important; text-shadow:none !important;">
            <option value="°C">°C</option>
            <option value="°F">°F</option>
          </select>

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
            const u = $("#TempUnitSelect")?.value || "°C";
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
        // Step-Text neu rendern (Tokens die gesetzt sind werden zu Text)
        renderMasterText();

        // Editor schließen
        closeInlineEditor();
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
                const u = $("#TempUnitSelect")?.value || "°C";
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
            doc = await loadJson();
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

    // ── Inline-editor helpers exposed for the probability area ──────────────────
    // buildInlineEditorHtml: same logic as openInlineEditor but returns HTML.
    // Uses class-based rows so multiple host containers don't conflict.
    function buildInlineEditorHtml(varName, currentVal) {
        const compactSpecialVar = isCompactSpecialVariable(varName);
        if (compactSpecialVar) {
            const specialBlock = renderSpecialEditor(varName, currentVal);
            // renderSpecialEditor already includes BtnPickDurationQuick/BtnCloseVarTop
            return `<div class="duration-editor prob-inline-editor" data-editor-for="${escapeHtml(varName)}" data-selected-article="" data-selected-pronoun="" data-selected-value="" data-duration-unit="minute" data-selected-ingredient-values="[]">
  <div class="small text-muted mb-1">Wert für <strong>${escapeHtml(varName)}</strong></div>
  ${specialBlock}
</div>`;
        }

        const ingredientVar = isIngredientVariable(varName);
        const noArticleVar = isNoArticleVariable(varName);
        const stateVar = isStateVariable(varName);
        const articleButtons = (ingredientVar || noArticleVar) ? '' : renderPillButtons(getArticleOptions(), 'article', null);
        const pronounButtons = stateVar ? renderPillButtons(getVarOptions('pronoun'), 'pronoun', null) : '';
        const ingredientItems = ingredientVar ? getSelectedIngredientsFromPage() : [];
        const options = ingredientVar ? ingredientItems.map(x => x.name) : getVarOptions(varName);
        const selectedIngredientValues = ingredientVar ? parseSelectedIngredientValues(currentVal, options) : [];
        const valueButtons = ingredientVar
            ? ingredientItems.map(({ name, icon }) => {
                const value = name.trim();
                const active = selectedIngredientValues.includes(value) ? ' active' : '';
                const safe = escapeHtml(value);
                const iconPart = icon ? `<span class="chip-icon" aria-hidden="true">${icon}</span> ` : '';
                return `<button type="button" class="ingredient-chip${active}" data-pick-mode="ingredient-value" data-pick-value="${safe}">${iconPart}${safe}</button>`;
            }).join('')
            : renderPillButtons(options, 'value', currentVal);
        const specialBlock = renderSpecialEditor(varName, currentVal);

        return `<div class="duration-editor prob-inline-editor" data-editor-for="${escapeHtml(varName)}" data-selected-article="" data-selected-pronoun="" data-selected-value="" data-duration-unit="minute" data-selected-ingredient-values="${escapeHtml(JSON.stringify(selectedIngredientValues))}">
  <div class="small text-muted mb-1">Wert für <strong>${escapeHtml(varName)}</strong></div>
  ${specialBlock}
  ${(ingredientVar || noArticleVar) ? '' : `<div class="small text-muted mt-2 mb-1">Artikel</div><div class="d-flex flex-wrap gap-2 mb-2 js-article-btn-row">${articleButtons}</div>`}
  ${stateVar ? `<div class="small text-muted mt-2 mb-1">Pronomen</div><div class="d-flex flex-wrap gap-2 mb-2 js-pronoun-btn-row">${pronounButtons}</div>` : ''}
  <div class="small text-muted mb-1">${escapeHtml(varName)} einsetzen</div>
  <div class="d-flex flex-wrap gap-2 js-value-btn-row">
    ${valueButtons || (ingredientVar
        ? '<div class="text-muted small">Keine Zutaten ausgewählt.</div>'
        : `<div class="text-muted small">Keine Optionen: ${escapeHtml(varName)}</div>`)}
  </div>
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
            const u = (editorEl.querySelector('#TempUnitSelect') || {}).value || '°C';
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

    // Merge into MasterStepCreatorHelpers (second IIFE adds formatIngredientList etc.)
    window.MasterStepCreatorHelpers = Object.assign(window.MasterStepCreatorHelpers || {}, {
        buildInlineEditorHtml,
        applyEditorValue,
        applyEditorExtras
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










