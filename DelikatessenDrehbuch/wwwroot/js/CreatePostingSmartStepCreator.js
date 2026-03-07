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

        // 1) variable_options[varName][lang]
        const vo = doc.variable_options?.[varName];
        if (vo && typeof vo === "object") {
            const list = vo[currentLang] ?? vo[currentLang.toLowerCase()] ?? vo[DEFAULT_LANG] ?? vo.de;
            if (Array.isArray(list)) return list.filter(x => x !== null && x !== undefined);
        }

        // 2) Spezialfall: equipment oben in doc.equipment (id->name)
        //    (falls du das als IDs verwendest)
        if (varName === "equipment" && doc.equipment) {
            // wenn doc.variable_options.equipment existiert, nimmt er die eh schon (oben)
            // ansonsten fallback aus doc.equipment map:
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
    function renderTemplate(templateRaw, stepId, values) {
        if (!templateRaw) return "";
        values = values || {};

        let index = 0;

        return templateRaw.replace(/{{\s*([^}]+?)\s*}}/g, (_m, varRaw) => {
            const varName = (varRaw ?? "").trim();
            const tokenId = `${stepId}_${varName}_${index++}`;
            const val = (values[varName] ?? "").toString();

            // Anzeige: wenn gesetzt -> Wert anzeigen, sonst Variablenname
            const display = val.trim().length > 0 ? val : varName;

            // Token bleibt IMMER klickbar + gelb highlight
            // Reset bleibt IMMER daneben (damit man schnell auf Default zurück kann)
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
              title="Zurücksetzen">↺</button>
    `;
        });
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

        const icon = meta?.icon ?? "✨";
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
                                                     data-step-id="${escapeHtml(step.master_id ?? "")}"
                                                     data-title="${escapeHtml(title)}"
                                                     data-template-raw="${encodeAttr(templateRaw)}">
                                                 <div class="template-title">
                                                         ${escapeHtml(title)}
                                                 </div>
                                                 <div
                                                     class="template-snippet">
                                                         ${escapeHtml(templateRaw)}
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
            target.innerHTML = "Wähle eine Zutat und ein Template.";
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

    function getSelectedIngredientNamesFromPage() {
        const rows = Array.from(document.querySelectorAll("#selectedIngredients .ingredient-row .display-name-selected"));
        return rows
            .map(x => (x?.textContent || "").toString().trim())
            .filter(Boolean);
    }

    
    function getIngredientEmoji(name) {
        const text = (name || "").toString().toLowerCase();
        if (text.includes("basil")) return "🌿";
        if (text.includes("tomat")) return "🍅";
        if (text.includes("zwiebel")) return "🧅";
        if (text.includes("knoblauch")) return "🧄";
        if (text.includes("reis")) return "🍚";
        if (text.includes("salat")) return "🥗";
        return "🥣";
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
        const ingredientVar = isIngredientVariable(varName);
        const noArticleVar = isGrindSizeVariable(varName);

        const articleButtons = (ingredientVar || noArticleVar) ? "" : renderPillButtons(getArticleOptions(), "article", null);
        const options = ingredientVar ? getSelectedIngredientNamesFromPage() : getVarOptions(varName);
        const selectedIngredientValues = ingredientVar ? parseSelectedIngredientValues(currentVal, options) : [];
        const valueButtons = ingredientVar
            ? options.map(name => {
                const value = (name || "").toString().trim();
                const active = selectedIngredientValues.includes(value) ? " active" : "";
                const safe = escapeHtml(value);
                return `<button type="button" class="ingredient-chip${active}" data-pick-mode="ingredient-value" data-pick-value="${safe}">${getIngredientEmoji(value)} ${safe}</button>`;
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
          ${escapeHtml(u.key)}
        </button>
      `).join("");

            return `
        <div class="d-flex gap-2 align-items-center">
          <input type="number" min="1" step="1" id="DurationValueInput"
                 class="form-control form-control-sm"
                 style="max-width:110px"
                 value="${escapeHtml(extractLeadingNumber(currentVal) || "10")}" />
          <button type="button" class="btn btn-sm btn-outline-light" id="BtnPickDurationQuick">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVarTop">Schließen</button>
        </div>

        <div class="d-flex flex-wrap gap-2 mt-2">
          ${unitBtns}
        </div>

        <div class="d-flex gap-2 align-items-center mt-3">
          <input type="text" class="form-control form-control-sm" id="DurationPreview"
                 placeholder="z.B. 10 Minuten" value="${escapeHtml(currentVal)}" />
        </div>
      `;
        }

        // temp -> Input + Unit (°C/°F) (du wolltest dropdown)
        if (varName === "temp") {
            return `
        <div class="d-flex gap-2 align-items-center">
          <input type="number" min="0" step="1" id="TempValueInput"
                 class="form-control form-control-sm"
                 style="max-width:110px"
                 value="${escapeHtml(extractLeadingNumber(currentVal) || "180")}" />

          <select id="TempUnitSelect" class="form-select form-select-sm" style="max-width:140px">
            <option value="°C">°C</option>
            <option value="°F">°F</option>
          </select>

          <button type="button" class="btn btn-sm btn-outline-light" id="BtnPickTempQuick">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" id="BtnCloseVarTop">Schließen</button>
        </div>

        <div class="d-flex gap-2 align-items-center mt-3">
          <input type="text" class="form-control form-control-sm" id="TempPreview"
                 placeholder="z.B. 180 °C" value="${escapeHtml(currentVal)}" />
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

        // Normalfall: Artikel + Wert
        const article = host.dataset.selectedArticle ?? "";
        let value = host.dataset.selectedValue ?? "";
        const noArticleVar = isGrindSizeVariable(varName);

        if (isIngredientVariable(varName)) {
            const selectedValues = JSON.parse(host.dataset.selectedIngredientValues || "[]");
            if (!Array.isArray(selectedValues) || !selectedValues.length) return;
            activeStep.values[varName] = formatSelectedIngredientList(selectedValues, currentLang);
            rerenderAfterValueSet();
            return;
        }

        // fallback: free text
        if (!value) {
            value = $("#FreeTextValueInput")?.value?.trim() || "";
        }

        let composed = value;

        // Artikel nur wenn nicht "ohne"
        if (!noArticleVar && article && article !== "ohne") {
            composed = `${article} ${value}`.trim();
        }

        // wenn nur artikel geklickt aber kein value -> nix setzen
        if (!value) return;

        activeStep.values[varName] = composed;
        rerenderAfterValueSet();
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

    function acceptActiveStep() {
        if (!activeStep) return;

        const step = steps.find(s => (s?.master_id || "") === (activeStep.master_id || ""));
        const payload = {
            de: getRenderedTextForLang(step, "de"),
            en: getRenderedTextForLang(step, "en"),
            esp: getRenderedTextForLang(step, "esp"),
            prt: getRenderedTextForLang(step, "prt"),
            phase: parseInt(step?.phase ?? 0, 10) || 0,
            equipment: parseInt(step?.equipment ?? 0, 10) || 0
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
                const row = mode === "article" ? $("#ArticleBtnRow") : $("#ValueBtnRow");
                if (row) row.querySelectorAll("button[data-pick-mode]").forEach(b => {
                    if (b.dataset.pickMode === mode) b.classList.remove("active");
                });
                pickBtn.classList.add("active");

                if (mode === "article") host.dataset.selectedArticle = val;
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

                // preview
                const n = $("#DurationValueInput")?.value?.trim() || "";
                const labels = getDurationUnits();
                const unitLabel = labels.find(x => x.key === host.dataset.durationUnit)?.label ?? host.dataset.durationUnit;
                const composed = n ? `${n} ${unitLabel}` : "";
                const prev = $("#DurationPreview");
                if (prev) prev.value = composed;
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
                const prev = $("#DurationPreview");
                if (prev) prev.value = composed;

                // direkt übernehmen:
                activeStep.values["duration"] = composed;
                rerenderAfterValueSet();
                return;
            }

            // quick temp apply
            if (e.target.id === "BtnPickTempQuick") {
                const n = $("#TempValueInput")?.value?.trim() || "";
                const u = $("#TempUnitSelect")?.value || "°C";
                const composed = n ? `${n} ${u}` : "";
                const prev = $("#TempPreview");
                if (prev) prev.value = composed;

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

    function getIngredientEmojiForHelper(name) {
        const text = (name || "").toString().toLowerCase();
        if (text.includes("basil")) return "🌿";
        if (text.includes("tomat")) return "🍅";
        if (text.includes("zwiebel")) return "🧅";
        if (text.includes("knoblauch")) return "🧄";
        if (text.includes("reis")) return "🍚";
        if (text.includes("salat")) return "🥗";
        return "🥣";
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
            const emoji = getIngredientEmojiForHelper(name); return `<button type="button" class="btn btn-sm ${btnClass} inline-equipment-opt js-prob-ingredient-chip" data-value="${safe}" data-id="${safeId}">${emoji} ${safe}</button>`;
        }).join("");
    }

    function resolveIngredientInsertValue(selectedNames, langKey, fallbackValue) {
        const fromSelection = formatIngredientList(selectedNames || [], langKey);
        return fromSelection || (fallbackValue || "").toString();
    }

    window.MasterStepCreatorHelpers = {
        formatIngredientList,
        buildIngredientChipsHtml,
        resolveIngredientInsertValue
    };
})();




