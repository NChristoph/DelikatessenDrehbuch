// master-steps-ui.js
// Erwartet:
// - /data/master_steps.json
// - <div id="insertContainer"></div>
// - <div id="MasterText" class="preview-step-text"></div>
// Optional:
// - <select id="LangSelect">...</select>

(() => {
  const DEFAULT_LANG = "de";
  const JSON_URL = "/data/master_steps.json";

  let currentLang = DEFAULT_LANG;
  let doc = null;
  let steps = [];
  let activeStep = null; // { master_id, description, templateRaw, values:{} }
  let activeTokenId = null;

  // -----------------------------
  // DOM
  // -----------------------------
  const $ = (sel) => document.querySelector(sel);
  const insertContainer = () => $("#insertContainer");
  const masterText = () => $("#MasterText");
  const langSelect = () => $("#LangSelect");

  function ensureEditorHost() {
    const mt = masterText();
    if (!mt) return null;

    let host = $("#MasterTokenEditor");
    if (!host) {
      mt.insertAdjacentHTML("afterend", `<div id="MasterTokenEditor" class="d-none mt-2"></div>`);
      host = $("#MasterTokenEditor");
    }
    return host;
  }

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

  function normalizeLang(lang) {
    return (lang || DEFAULT_LANG).toLowerCase();
  }

  function isNonEmpty(val) {
    return val !== null && val !== undefined && val.toString().trim().length > 0;
  }

  // -----------------------------
  // OPTION RESOLVER
  // -----------------------------
  function getVariableOptions(varName) {
    const vo = doc?.variable_options?.[varName];
    if (vo && Array.isArray(vo[currentLang])) return vo[currentLang];

    // fallback: equipment map (0.. -> text)
    if (varName === "equipment") {
      const map = doc?.equipment;
      if (map && typeof map === "object") {
        return Object.keys(map)
          .sort((a, b) => Number(a) - Number(b))
          .map(k => map[k]);
      }
    }

    return null;
  }

  function getArticleOptions() {
    // bevorzugt: variable_options.articles[lang]
    const arr = doc?.variable_options?.articles?.[currentLang];
    if (Array.isArray(arr) && arr.length) {
      // plus "ohne"
      return [...arr, ""];
    }

    // fallback minimal
    if (currentLang === "de") return ["der", "die", "das", "dem", "den", ""];
    if (currentLang === "en") return ["the", ""];
    if (currentLang === "esp") return ["el", "la", "los", "las", ""];
    if (currentLang === "prt") return ["o", "a", "os", "as", ""];
    if (currentLang === "nl") return ["de", "het", ""];
    // id/sv/da/no/ms meist leer
    return [""];
  }

  function getDurationUnits() {
    const du = doc?.duration_units;
    const min = du?.minute?.[currentLang] ?? "Minuten";
    const hr = du?.hour?.[currentLang] ?? "Stunden";
    return [
      { id: "minute", label: min },
      { id: "hour", label: hr }
    ];
  }

  // -----------------------------
  // TEMPLATE RENDERING
  // -----------------------------
  function renderSnippetPlain(templateRaw) {
    if (!templateRaw) return "";
    const safe = escapeHtml(templateRaw);
    return safe.replace(/{{\s*([^}]+?)\s*}}/g, (_m, v) => escapeHtml((v ?? "").trim()));
  }

  function renderTemplateWithValues(templateRaw, stepId, values) {
    if (!templateRaw) return "";
    values = values || {};

    let index = 0;

    return templateRaw.replace(/{{\s*([^}]+?)\s*}}/g, (_match, variableRaw) => {
      const variable = (variableRaw ?? "").trim();
      const tokenId = `${stepId}_${variable}_${index++}`;

      const val = values[variable];
      if (isNonEmpty(val)) {
        return escapeHtml(val);
      }

      return `
        <span
          class="token-highlight placeholder-token template-var"
          draggable="false"
          data-placeholder-key="${escapeHtml(variable)}"
          data-placeholder-token-id="${escapeHtml(tokenId)}"
        >${escapeHtml(variable)}</span>
        <button
          type="button"
          class="placeholder-reset"
          data-placeholder-token-id="${escapeHtml(tokenId)}"
          data-default-value="${escapeHtml(variable)}"
          title="Zurücksetzen"
        >↺</button>
      `;
    });
  }

  // -----------------------------
  // EDITOR UI (Buttons statt Dropdown)
  // -----------------------------
  function hideEditor() {
    const host = ensureEditorHost();
    if (!host) return;
    host.innerHTML = "";
    host.classList.add("d-none");
    activeTokenId = null;
  }

  function showEditor(html) {
    const host = ensureEditorHost();
    if (!host) return;
    host.innerHTML = html;
    host.classList.remove("d-none");
  }

  function chipButton(label, value, selected = false) {
    // Styling wie "Einsetzen" => btn btn-sm btn-outline-light
    const activeCls = selected ? " active" : "";
    const shown = label === "" ? "ohne" : label;
    return `
      <button type="button"
              class="btn btn-sm btn-outline-light token-chip${activeCls}"
              data-chip-value="${escapeHtml(value)}">
        ${escapeHtml(shown)}
      </button>
    `;
  }

  function buildChipsRow(options, currentVal) {
    const opts = (options || []).map(v => {
      const val = (v ?? "").toString();
      const sel = (val === (currentVal ?? "").toString());
      return chipButton(val, val, sel);
    }).join("");
    return `<div class="d-flex flex-wrap gap-2">${opts}</div>`;
  }

  function buildEditorHtml(varName, tokenId) {
    const currentVal = (activeStep?.values?.[varName] ?? "").toString();

    // -----------------------------
    // ingredient => Artikel Buttons + Nomen Input
    // -----------------------------
    if (varName === "ingredient") {
      // Wir speichern hier in activeStep.values.ingredient direkt den finalen String (z.B. "die Zwiebeln")
      // Dazu: Artikel (Chip) + Textfeld.
      // Wenn Lang kein Artikel: chips zeigen nur "ohne".
      const articles = getArticleOptions();

      // Parse current (wenn schon gesetzt): erstes Wort als artikel (sehr simpel)
      let guessedArticle = "";
      let guessedNoun = currentVal;

      const parts = currentVal.trim().split(/\s+/);
      if (parts.length >= 2) {
        const maybeArt = parts[0].toLowerCase();
        if (articles.map(a => a.toLowerCase()).includes(maybeArt)) {
          guessedArticle = parts[0];
          guessedNoun = parts.slice(1).join(" ");
        }
      }

      const chips = buildChipsRow(articles, guessedArticle);

      return `
        <div class="duration-editor mt-2"
             data-popup="token"
             data-var="${escapeHtml(varName)}"
             data-token-id="${escapeHtml(tokenId)}">
          <div class="small text-muted mb-1">Artikel wählen (${escapeHtml(currentLang)})</div>
          ${chips}

          <div class="small text-muted mb-1 mt-2">Nomen / Zutat</div>
          <div class="d-flex gap-2 align-items-center">
            <input type="text"
                   class="form-control form-control-sm"
                   style="max-width:420px"
                   placeholder="z.B. Zwiebeln / Knoblauch / Hähnchenbrust"
                   value="${escapeHtml(guessedNoun)}"
                   data-token-input="ingredient-noun" />
            <input type="hidden" value="${escapeHtml(guessedArticle)}" data-token-input="ingredient-article" />
            <button type="button" class="btn btn-sm btn-outline-light" data-token-apply="ingredient">Einsetzen</button>
            <button type="button" class="btn btn-sm btn-outline-secondary" data-token-cancel>Schließen</button>
          </div>

          <div class="small text-muted mt-2">
            Tipp: Bei sv/da/no oft ohne Extra-Artikel (Suffix im Wort). Dann einfach „ohne“ wählen.
          </div>
        </div>
      `;
    }

    // -----------------------------
    // duration => number + unit buttons
    // -----------------------------
    if (varName === "duration") {
      const units = getDurationUnits();

      let defaultN = "10";
      let defaultUnitId = "minute";
      const numMatch = currentVal.match(/(\d+)/);
      if (numMatch) defaultN = numMatch[1];

      const lowerVal = currentVal.toLowerCase();
      const minuteLabel = (units.find(u => u.id === "minute")?.label || "").toLowerCase();
      const hourLabel = (units.find(u => u.id === "hour")?.label || "").toLowerCase();
      if (hourLabel && lowerVal.includes(hourLabel)) defaultUnitId = "hour";
      if (minuteLabel && lowerVal.includes(minuteLabel)) defaultUnitId = "minute";

      const unitButtons = buildChipsRow(units.map(u => u.id), defaultUnitId)
        // wir wollen labels anzeigen, nicht ids
        .replaceAll(">minute<", `>${escapeHtml(units[0].label)}<`)
        .replaceAll(">hour<", `>${escapeHtml(units[1].label)}<`);

      return `
        <div class="duration-editor mt-2"
             data-popup="token"
             data-var="${escapeHtml(varName)}"
             data-token-id="${escapeHtml(tokenId)}">
          <div class="small text-muted mb-1">Dauer einsetzen</div>

          <div class="d-flex gap-2 align-items-center">
            <input type="number" min="1" step="1"
                   class="form-control form-control-sm"
                   style="max-width:110px"
                   value="${escapeHtml(defaultN)}"
                   data-token-input="duration-number" />
            <input type="hidden" value="${escapeHtml(defaultUnitId)}" data-token-input="duration-unit" />
            <button type="button" class="btn btn-sm btn-outline-light" data-token-apply="duration">Einsetzen</button>
            <button type="button" class="btn btn-sm btn-outline-secondary" data-token-cancel>Schließen</button>
          </div>

          <div class="mt-2">${unitButtons}</div>
        </div>
      `;
    }

    // -----------------------------
    // number-only vars
    // -----------------------------
    if (varName === "count" || varName === "servings" || varName === "temp") {
      const label =
        varName === "count" ? "Anzahl einsetzen" :
        varName === "servings" ? "Portionen einsetzen" :
        "Temperatur einsetzen";

      const min = varName === "temp" ? 30 : 1;

      return `
        <div class="duration-editor mt-2"
             data-popup="token"
             data-var="${escapeHtml(varName)}"
             data-token-id="${escapeHtml(tokenId)}">
          <div class="small text-muted mb-1">${escapeHtml(label)}</div>
          <div class="d-flex gap-2 align-items-center">
            <input type="number" min="${min}" step="1"
                   class="form-control form-control-sm"
                   style="max-width:110px"
                   value="${escapeHtml(currentVal || (varName === "temp" ? "180" : "2"))}"
                   data-token-input="number" />
            <button type="button" class="btn btn-sm btn-outline-light" data-token-apply="number">Einsetzen</button>
            <button type="button" class="btn btn-sm btn-outline-secondary" data-token-cancel>Schließen</button>
          </div>
        </div>
      `;
    }

    // -----------------------------
    // all other vars with JSON options => BUTTON CHIPS
    // -----------------------------
    const options = getVariableOptions(varName);
    if (Array.isArray(options) && options.length > 0) {
      const chips = buildChipsRow(options, currentVal);

      return `
        <div class="duration-editor mt-2"
             data-popup="token"
             data-var="${escapeHtml(varName)}"
             data-token-id="${escapeHtml(tokenId)}">
          <div class="small text-muted mb-1">${escapeHtml(varName)} auswählen</div>

          ${chips}

          <div class="d-flex gap-2 align-items-center mt-2">
            <input type="hidden" value="${escapeHtml(currentVal)}" data-token-input="chip-selected" />
            <button type="button" class="btn btn-sm btn-outline-light" data-token-apply="chip">Einsetzen</button>
            <button type="button" class="btn btn-sm btn-outline-secondary" data-token-cancel>Schließen</button>
          </div>
        </div>
      `;
    }

    // fallback text
    return `
      <div class="duration-editor mt-2"
           data-popup="token"
           data-var="${escapeHtml(varName)}"
           data-token-id="${escapeHtml(tokenId)}">
        <div class="small text-muted mb-1">Wert für <strong>${escapeHtml(varName)}</strong></div>
        <div class="d-flex gap-2 align-items-center">
          <input type="text"
                 class="form-control form-control-sm"
                 style="max-width:420px"
                 value="${escapeHtml(currentVal)}"
                 data-token-input="text" />
          <button type="button" class="btn btn-sm btn-outline-light" data-token-apply="text">Einsetzen</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" data-token-cancel>Schließen</button>
        </div>
      </div>
    `;
  }

  function openEditorForToken(varName, tokenId) {
    if (!activeStep) return;
    activeTokenId = tokenId;
    showEditor(buildEditorHtml(varName, tokenId));
  }

  // -----------------------------
  // LOAD JSON
  // -----------------------------
  async function loadJson() {
    const res = await fetch(`${JSON_URL}?v=${Date.now()}`, { cache: "no-store" });
    if (!res.ok) throw new Error(`Konnte master_steps.json nicht laden: ${res.status}`);
    return await res.json();
  }

  // -----------------------------
  // RENDER CARDS
  // -----------------------------
  function renderStepCards() {
    const container = insertContainer();
    if (!container) {
      console.error("Container #insertContainer nicht gefunden!");
      return;
    }

    container.innerHTML = "";

    const sorted = [...steps].sort((a, b) => {
      const pa = a.phase ?? 0;
      const pb = b.phase ?? 0;
      if (pa !== pb) return pa - pb;
      return (a.master_id ?? "").localeCompare(b.master_id ?? "");
    });

    sorted.forEach((step) => {
      const title = step.description ?? "";
      const templateRaw = step.templates?.[currentLang] ?? "";
      const snippet = renderSnippetPlain(templateRaw);

      const html = `
        <button type="button"
                class="template-card w-100 mb-2"
                data-step-id="${escapeHtml(step.master_id ?? "")}"
                data-title="${escapeHtml(title)}"
                data-template-raw="${encodeAttr(templateRaw)}">
          <div class="template-title">✨ ${escapeHtml(title)}</div>
          <div class="template-snippet">${snippet}</div>
        </button>
      `;

      container.insertAdjacentHTML("beforeend", html);
    });
  }

  // -----------------------------
  // PREVIEW
  // -----------------------------
  function setPreviewHtml(html) {
    const target = masterText();
    if (!target) return;
    target.innerHTML = html;
  }

  function setPreviewDefault() {
    setPreviewHtml("Wähle eine Zutat und ein Template.");
    hideEditor();
  }

  function setActiveCard(cardEl) {
    document.querySelectorAll(".template-card.active").forEach(x => x.classList.remove("active"));
    if (cardEl) cardEl.classList.add("active");
  }

  function selectStepFromCard(cardBtn) {
    const stepId = cardBtn.dataset.stepId || "";
    const title = cardBtn.dataset.title || "";
    const templateRaw = decodeAttr(cardBtn.dataset.templateRaw || "");

    if (!stepId || !templateRaw) return;

    activeStep = {
      master_id: stepId,
      description: title,
      templateRaw,
      values: {}
    };

    setActiveCard(cardBtn);
    hideEditor();

    const preview = renderTemplateWithValues(templateRaw, stepId, activeStep.values);
    setPreviewHtml(preview);
  }

  function applyValue(varName, value) {
    if (!activeStep) return;

    activeStep.values[varName] = value;

    const preview = renderTemplateWithValues(activeStep.templateRaw, activeStep.master_id, activeStep.values);
    setPreviewHtml(preview);

    hideEditor();
  }

  function resetValueByTokenId(tokenId) {
    if (!activeStep) return;

    const tokenEl = document.querySelector(
      `.template-var[data-placeholder-token-id="${CSS.escape(tokenId)}"]`
    );
    const varName = tokenEl?.dataset?.placeholderKey;
    if (!varName) return;

    delete activeStep.values[varName];

    const preview = renderTemplateWithValues(activeStep.templateRaw, activeStep.master_id, activeStep.values);
    setPreviewHtml(preview);

    hideEditor();
  }

  // -----------------------------
  // EVENTS
  // -----------------------------
  function wireEvents() {
    document.addEventListener("click", (e) => {
      // 1) Card click
      const cardBtn = e.target.closest(".template-card");
      if (cardBtn) {
        selectStepFromCard(cardBtn);
        return;
      }

      if (!activeStep) return;

      // 2) Token click
      const token = e.target.closest(".template-var");
      if (token) {
        const varName = token.dataset.placeholderKey || "";
        const tokenId = token.dataset.placeholderTokenId || "";
        if (!varName || !tokenId) return;

        openEditorForToken(varName, tokenId);
        return;
      }

      // 3) Reset ↺
      const resetBtn = e.target.closest(".placeholder-reset");
      if (resetBtn) {
        const tokenId = resetBtn.dataset.placeholderTokenId || "";
        if (!tokenId) return;
        resetValueByTokenId(tokenId);
        return;
      }

      // 4) Cancel
      const cancelBtn = e.target.closest("[data-token-cancel]");
      if (cancelBtn) {
        hideEditor();
        return;
      }

      // 5) Chip clicked (generic)
      const chip = e.target.closest(".token-chip");
      if (chip) {
        const popup = chip.closest('[data-popup="token"]');
        if (!popup) return;

        // mark active visual
        popup.querySelectorAll(".token-chip.active").forEach(x => x.classList.remove("active"));
        chip.classList.add("active");

        const val = chip.getAttribute("data-chip-value") ?? "";
        const varName = popup.getAttribute("data-var") ?? "";

        // store selected into hidden input depending on popup type
        if (varName === "ingredient") {
          const artHidden = popup.querySelector('[data-token-input="ingredient-article"]');
          if (artHidden) artHidden.value = val;
        } else if (varName === "duration") {
          const unitHidden = popup.querySelector('[data-token-input="duration-unit"]');
          if (unitHidden) unitHidden.value = val; // minute/hour
        } else {
          const h = popup.querySelector('[data-token-input="chip-selected"]');
          if (h) h.value = val;
        }
        return;
      }

      // 6) Apply handlers
      const applyBtn = e.target.closest("[data-token-apply]");
      if (applyBtn) {
        const popup = applyBtn.closest('[data-popup="token"]');
        if (!popup) return;

        const varName = popup.getAttribute("data-var") || "";
        const mode = applyBtn.getAttribute("data-token-apply") || "";

        if (!varName) return;

        if (mode === "text") {
          const inp = popup.querySelector('[data-token-input="text"]');
          applyValue(varName, (inp?.value ?? "").toString());
          return;
        }

        if (mode === "number") {
          const inp = popup.querySelector('[data-token-input="number"]');
          applyValue(varName, (inp?.value ?? "").toString());
          return;
        }

        if (mode === "duration") {
          const n = popup.querySelector('[data-token-input="duration-number"]')?.value ?? "10";
          const unitKey = popup.querySelector('[data-token-input="duration-unit"]')?.value ?? "minute";
          const units = getDurationUnits();
          const unitLabel = units.find(u => u.id === unitKey)?.label ?? "Minuten";
          applyValue(varName, `${n} ${unitLabel}`);
          return;
        }

        if (mode === "chip") {
          const h = popup.querySelector('[data-token-input="chip-selected"]');
          applyValue(varName, (h?.value ?? "").toString());
          return;
        }

        if (mode === "ingredient") {
          const noun = popup.querySelector('[data-token-input="ingredient-noun"]')?.value ?? "";
          const article = popup.querySelector('[data-token-input="ingredient-article"]')?.value ?? "";

          const finalVal = (article || "").trim().length > 0
            ? `${article} ${noun}`.trim()
            : `${noun}`.trim();

          applyValue(varName, finalVal);
          return;
        }
      }

      // 7) Outside click closes editor (optional)
      const editorHost = $("#MasterTokenEditor");
      if (editorHost && !editorHost.classList.contains("d-none")) {
        const insideEditor = e.target.closest("#MasterTokenEditor");
        if (!insideEditor) hideEditor();
      }
    });

    // language change
    const ls = langSelect();
    if (ls) {
      ls.addEventListener("change", () => {
        currentLang = normalizeLang(ls.value);

        renderStepCards();

        activeStep = null;
        setPreviewDefault();
      });
    }
  }

  // -----------------------------
  // INIT
  // -----------------------------
  async function init() {
    try {
      currentLang = normalizeLang(currentLang);
      doc = await loadJson();
      steps = doc.master_steps || [];

      renderStepCards();
      setPreviewDefault();
      ensureEditorHost();
      wireEvents();
    } catch (err) {
      console.error(err);
      setPreviewHtml("Fehler beim Laden der Master Templates. Bitte Console prüfen.");
      hideEditor();
    }
  }

  document.addEventListener("DOMContentLoaded", init);
})();