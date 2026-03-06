// selected-ingredients-min.js (angepasst auf .ingredient-db-row aus deinem Razor)
(() => {
    const DEFAULT_LANG = "de";
    // nimm ggf. dein global currentLang, sonst fallback
    const getLang = () => (window.currentLang || DEFAULT_LANG).toString().toLowerCase();

    const SELECTED_LIST_ID = "SelectedIngredientsList";
    const SMART_CHIPS_ID = "SmartStepIngredientChips";

    const state = {
        selected: [] // [{ id, name, qty, unit }]
    };

    function resolveNameFromRow(row) {
        const lang = getLang();

        // generisch: data-name-xx lesen (funktioniert sobald du mehr Sprachen ergänzt)
        const attr = `name${lang.toUpperCase()}`; // dataset key z.B. nameDe, nameEn
        const byLang = (row.dataset?.[attr] || "").toString().trim();
        if (byLang) return byLang;

        // fallback de/en
        const de = (row.dataset?.nameDe || "").toString().trim();
        if (de) return de;

        const en = (row.dataset?.nameEn || "").toString().trim();
        if (en) return en;

        // letzter fallback: sichtbarer Text
        const text = (row.querySelector(".display-name")?.textContent || "").toString().trim();
        return text || "Zutat";
    }

    function resolveQtyUnitFromRow(row) {
        const qty = (row.getAttribute("data-selected-qty") || "").toString().trim();
        const unit = (row.getAttribute("data-selected-unit") || "").toString().trim();
        return { qty, unit };
    }

    function resolveUnitLabelFromRow(row, unitValue) {
        const normalized = (unitValue || "").toString().trim().toLowerCase();
        if (!normalized) return "";

        const select = row ? row.querySelector('.js-db-unit') : null;
        if (!select) return unitValue || "";

        const option = Array.from(select.options || []).find(o => (o.value || "").toString().trim().toLowerCase() === normalized);
        return (option?.textContent || unitValue || "").toString().trim();
    }

    function upsertSelectedFromRow(row) {
        const id = (row.getAttribute("data-ingredient-id") || "").toString().trim();
        if (!id) return;

        const name = resolveNameFromRow(row);
        const { qty, unit } = resolveQtyUnitFromRow(row);

        const existing = state.selected.find(x => x.id === id);
        if (existing) {
            // Update (falls User nachträglich Menge/Einheit ändert und nochmal + klickt)
            existing.name = name;
            existing.qty = qty;
            existing.unit = unit;
        } else {
            state.selected.push({ id, name, qty, unit });
        }

        renderAll();
    }

    function removeSelected(id) {
        id = (id ?? "").toString().trim();
        if (!id) return;
        state.selected = state.selected.filter(x => x.id !== id);
        renderAll();
    }

    // -----------------------------
    // RENDER
    // -----------------------------
    function renderSelectedList() {
        const host = document.getElementById(SELECTED_LIST_ID);
        if (!host) return;

        if (!state.selected.length) {
            host.innerHTML = `<span class="text-muted small">Noch keine Zutaten ausgewählt.</span>`;
            return;
        }

        host.innerHTML = state.selected.map(x => {
            const qtyUnit = (x.qty || x.unit) ? ` <span class="opacity-75">(${escapeHtml(x.qty || "0")} ${escapeHtml(x.unit || "")})</span>` : "";
            return `
        <span class="badge rounded-pill text-bg-dark d-inline-flex align-items-center gap-2"
              data-selected-id="${escapeHtml(x.id)}">
          <span>${escapeHtml(x.name)}${qtyUnit}</span>
          <button type="button"
                  class="btn btn-sm btn-link p-0 text-light js-remove-selected"
                  data-ingredient-id="${escapeHtml(x.id)}"
                  title="Entfernen"
                  style="text-decoration:none; line-height:1;">
            ✕
          </button>
        </span>
      `;
        }).join("");
    }

    function renderSmartStepChips() {
        const host = document.getElementById(SMART_CHIPS_ID);
        if (!host) return;

        if (!state.selected.length) {
            host.innerHTML = `<span class="text-muted small">Wähle Zutaten im Katalog – hier erscheinen dann Chips.</span>`;
            return;
        }

        host.innerHTML = state.selected.map(x => `
      <button type="button"
              class="btn btn-sm btn-outline-light js-smart-chip"
              data-ingredient-id="${escapeHtml(x.id)}"
              data-ingredient-name="${escapeHtml(x.name)}"
              data-qty="${escapeHtml(x.qty || "")}"
              data-unit="${escapeHtml(x.unit || "")}">
        ${escapeHtml(x.name)}
      </button>
    `).join("");
    }

    function renderAll() {
        renderSelectedList();
        renderSmartStepChips();
    }

    // -----------------------------
    // EVENTS
    // -----------------------------
    function wireEvents() {
        // + klicken
        document.addEventListener("click", (e) => {
            const addBtn = e.target.closest(".js-add-ingredient");
            if (!addBtn) return;

            e.preventDefault();
            const row = addBtn.closest(".ingredient-db-row");
            if (!row) return;

            upsertSelectedFromRow(row);
        });

        // Entfernen
        document.addEventListener("click", (e) => {
            const rm = e.target.closest(".js-remove-selected");
            if (!rm) return;

            e.preventDefault();
            removeSelected(rm.dataset.ingredientId);
        });

        // (Optional) Wenn qty/unit geändert wird und Zutat ist schon selected → live updaten:
        document.addEventListener("input", (e) => {
            const inp = e.target.closest(".js-db-qty");
            if (!inp) return;
            const row = inp.closest(".ingredient-db-row");
            if (!row) return;

            const id = (row.getAttribute("data-ingredient-id") || "").toString().trim();
            const existing = state.selected.find(x => x.id === id);
            if (!existing) return;

            const { qty, unit } = resolveQtyUnitFromRow(row);
            existing.qty = qty;
            existing.unit = unit;
            renderAll();
        });

        document.addEventListener("change", (e) => {
            const sel = e.target.closest(".js-db-unit");
            if (!sel) return;
            const row = sel.closest(".ingredient-db-row");
            if (!row) return;

            const id = (row.getAttribute("data-ingredient-id") || "").toString().trim();
            const existing = state.selected.find(x => x.id === id);
            if (!existing) return;

            const { qty, unit } = resolveQtyUnitFromRow(row);
            existing.qty = qty;
            existing.unit = unit;
            renderAll();
        });
    }

    function escapeHtml(str) {
        return (str ?? "")
            .toString()
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    document.addEventListener("DOMContentLoaded", () => {
        wireEvents();
        renderAll();
    });
})();


//__-------___//
// ingredient-move-dock.js
(() => {
    const DEFAULT_LANG = "de";
    const getLang = () => (window.currentLang || DEFAULT_LANG).toString().toLowerCase();

    const SELECTED_HOST_ID = "SelectedIngredientsList";
    const REMAINING_HOST_ID = "RemainingIngredientsList";

    // Referenz auf Original-Katalog-Container (da wo deine ingredient-db-row initial drin sind)
    // Passe den Selector an deinen echten Wrapper an!
    const CATALOG_WRAPPER_SELECTOR = "#ingredientCatalogHost";
    // Wenn du keinen Wrapper hast: setz im Razor um deine Katalogliste <div id="ingredientCatalogHost">...</div>

    const state = {
        selectedIds: new Set()
    };

    const $ = (s, root = document) => root.querySelector(s);
    const $$ = (s, root = document) => Array.from(root.querySelectorAll(s));

    function resolveNameFromRow(row) {
        const lang = getLang();

        const attr = `name${lang.toUpperCase()}`; // nameDe, nameEn, ...
        const byLang = (row.dataset?.[attr] || "").toString().trim();
        if (byLang) return byLang;

        const de = (row.dataset?.nameDe || "").toString().trim();
        if (de) return de;

        const en = (row.dataset?.nameEn || "").toString().trim();
        if (en) return en;

        const text = (row.querySelector(".display-name")?.textContent || "").toString().trim();
        return text || "Zutat";
    }

    function resolveQtyUnitFromRow(row) {
        const qty = (row.getAttribute("data-selected-qty") || "").toString().trim();
        const unit = (row.getAttribute("data-selected-unit") || "").toString().trim();
        return { qty, unit };
    }

    // Build a compact dock item (separate from the heavy catalog row)
    function buildDockItem(row, mode) {
        // mode: "selected" | "remaining"
        const id = (row.getAttribute("data-ingredient-id") || "").toString().trim();
        const name = resolveNameFromRow(row);
        const { qty, unit } = resolveQtyUnitFromRow(row);
        const unitLabel = resolveUnitLabelFromRow(row, unit);
        const meta = (qty || unit) ? `${qty || "0"} ${unitLabel || unit || ""}`.trim() : "";

        const el = document.createElement("div");
        el.className = "dock-item";
        el.setAttribute("data-ingredient-id", id);
        el.setAttribute("data-mode", mode);

        if (mode === "selected") {
            el.innerHTML = `
          <button type="button" class="btn btn-sm btn-glass dock-main-action w-100 js-edit-selected" data-ingredient-id="${escapeHtml(id)}" title="Bearbeiten">
            <div class="dock-name">${escapeHtml(name)}</div>
            ${meta ? `<div class="dock-meta">${escapeHtml(meta)}</div>` : ``}
          </button>
          <div class="dock-actions">
            <button type="button" class="btn btn-sm btn-glass btn-glass-danger js-remove-selected" data-ingredient-id="${escapeHtml(id)}" title="Entfernen">✕</button>
          </div>
        `;
        } else {
            el.innerHTML = `
          <div class="flex-grow-1">
            <div class="dock-name">${escapeHtml(name)}</div>
            ${meta ? `<div class="dock-meta">${escapeHtml(meta)}</div>` : ``}
          </div>
          <div class="dock-actions">
            <button type="button" class="btn btn-sm btn-glass js-add-from-remaining" data-ingredient-id="${escapeHtml(id)}" title="Hinzufügen">＋</button>
          </div>
        `;
        }

        return el;
    }

    // We keep original catalog row in memory by id:
    function getCatalogRowById(id) {
        return $(`.ingredient-db-row[data-ingredient-id="${CSS.escape(id)}"]`);
    }

    function renderDocks() {
        const selectedHost = document.getElementById(SELECTED_HOST_ID);
        const remainingHost = document.getElementById(REMAINING_HOST_ID);
        if (!selectedHost || !remainingHost) return;

        selectedHost.innerHTML = "";
        remainingHost.innerHTML = "";

        // Selected from state
        Array.from(state.selectedIds).forEach(id => {
            const row = getCatalogRowById(id);
            if (!row) return;
            selectedHost.appendChild(buildDockItem(row, "selected"));
        });

        // Remaining = everything not selected
        $$(".ingredient-db-row").forEach(row => {
            const id = (row.getAttribute("data-ingredient-id") || "").toString().trim();
            if (!id) return;
            if (state.selectedIds.has(id)) return;
            remainingHost.appendChild(buildDockItem(row, "remaining"));
        });
    }

    // Move the ORIGINAL ROW out of catalog UI (so it's removed there)
    function hideRowFromCatalog(row) {
        // echte Entfernung aus dem Katalog: detach and store on a hidden stash container
        // damit du es später wieder zurück bringen kannst, ohne es zu verlieren.
        let stash = document.getElementById("IngredientRowStash");
        if (!stash) {
            stash = document.createElement("div");
            stash.id = "IngredientRowStash";
            stash.style.display = "none";
            document.body.appendChild(stash);
        }
        stash.appendChild(row); // detach
    }

    function showRowBackToCatalog(row) {
        const catalog = $(CATALOG_WRAPPER_SELECTOR);
        if (!catalog) return;
        catalog.appendChild(row);
    }

    function addIngredient(id) {
        id = (id || "").toString().trim();
        if (!id) return;
        const row = getCatalogRowById(id);
        if (!row) return;

        state.selectedIds.add(id);

        // aus Katalog entfernen
        hideRowFromCatalog(row);

        renderDocks();
    }

    function removeIngredient(id) {
        id = (id || "").toString().trim();
        if (!id) return;

        state.selectedIds.delete(id);

        // wieder in den Katalog zurück
        const row = getCatalogRowById(id);
        if (row) showRowBackToCatalog(row);

        renderDocks();
    }

    function escapeHtml(str) {
        return (str ?? "")
            .toString()
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function wireEvents() {
        // PLUS im Katalog
        document.addEventListener("click", (e) => {
            const btn = e.target.closest(".js-add-ingredient");
            if (!btn) return;
            e.preventDefault();

            const row = btn.closest(".ingredient-db-row");
            const id = (btn.dataset.ingredientId || row?.getAttribute("data-ingredient-id") || "").toString();
            addIngredient(id);
        });

        // PLUS in Remaining
        document.addEventListener("click", (e) => {
            const btn = e.target.closest(".js-add-from-remaining");
            if (!btn) return;
            e.preventDefault();
            addIngredient(btn.dataset.ingredientId);
        });

        // Entfernen in Selected
        document.addEventListener("click", (e) => {
            const btn = e.target.closest(".js-remove-selected");
            if (!btn) return;
            e.preventDefault();
            removeIngredient(btn.dataset.ingredientId);
        });

        // Whole selected list row is clickable (except remove button)
        document.addEventListener("click", (e) => {
            if (e.target.closest(".js-edit-selected")) return;
            if (e.target.closest(".js-remove-selected")) return;

            const item = e.target.closest('.dock-item[data-mode="selected"]');
            if (!item) return;

            const editBtn = item.querySelector('.js-edit-selected');
            if (!editBtn) return;
            e.preventDefault();
            editBtn.click();
        });

        // Edit selected item directly via same popup logic as ingredient selection
        document.addEventListener("click", (e) => {
            const btn = e.target.closest(".js-edit-selected");
            if (!btn) return;
            e.preventDefault();

            const id = (btn.dataset.ingredientId || "").toString().trim();
            if (!id) return;

            if (typeof window.openIngredientConfigPopupByIngredientId === "function") {
                const opened = window.openIngredientConfigPopupByIngredientId(id, btn.closest('.dock-item'));
                if (opened) return;
            }

            // fallback
            const row = getCatalogRowById(id);
            if (!row) return;
            showRowBackToCatalog(row);
            row.scrollIntoView({ behavior: "smooth", block: "center" });
        });

        document.addEventListener('creator:ingredient-config-applied', () => {
            renderDocks();
        });
    }

    document.addEventListener("DOMContentLoaded", () => {
        // Wenn du keinen wrapper hast, musst du einen setzen:
        // <div id="ingredientCatalogHost"> ... deine ingredient-db-row ... </div>
        wireEvents();
        renderDocks();
    });
})();
