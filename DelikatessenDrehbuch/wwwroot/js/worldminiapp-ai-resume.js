// WorldMiniApp AI resume manager (multi-job).
// Stores multiple concurrent jobs in sessionStorage and offers a simple UI integration.
// ASCII-only on purpose (avoid encoding issues).
(function () {
  if (window.WorldMiniAppAiResume && window.WorldMiniAppAiResume.__v === 2) return;

  const STORAGE_KEY = "worldminiapp.ai.resume.v3";

  function now() { return Date.now(); }

  function safeJsonParse(text) {
    try { return text ? JSON.parse(text) : null; } catch { return null; }
  }

  function readStore() {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      const obj = safeJsonParse(raw) || {};
      const items = Array.isArray(obj.items) ? obj.items : [];
      const activeKey = (obj.activeKey || "").toString();
      return { items, activeKey };
    } catch {
      return { items: [], activeKey: "" };
    }
  }

  function writeStore(store) {
    try {
      sessionStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ items: store.items || [], activeKey: store.activeKey || "", updatedAt: now() })
      );
    } catch {
      // ignore
    }
  }

  function makeKey(entry) {
    const recipeId = Number(entry.recipeId || 0);
    const variantType = (entry.variantType || "").toString().trim().toLowerCase();
    const aiProvider = (entry.aiProvider || "openai").toString().trim().toLowerCase();
    return `${recipeId}:${variantType}:${aiProvider}`;
  }

  function normalizeEntry(entry) {
    const e = { ...(entry || {}) };
    e.recipeId = Number(e.recipeId || 0);
    e.variantType = (e.variantType || "").toString().trim().toLowerCase();
    e.aiProvider = (e.aiProvider || "openai").toString().trim().toLowerCase();
    e.stage = (e.stage || "").toString();
    e.jobId = e.jobId ? e.jobId.toString() : null;
    e.title = (e.title || "").toString();
    e.message = (e.message || "").toString();
    e.updatedAt = now();
    e.key = makeKey(e);
    return e;
  }

  function upsert(entry, setActive = true) {
    const store = readStore();
    const e = normalizeEntry(entry);
    const idx = store.items.findIndex(x => (x && x.key) === e.key);
    if (idx >= 0) store.items[idx] = { ...store.items[idx], ...e };
    else store.items.unshift(e);
    if (setActive) store.activeKey = e.key;
    writeStore(store);
    return e;
  }

  function removeByKey(key) {
    const store = readStore();
    const k = (key || "").toString();
    store.items = store.items.filter(x => x && x.key !== k);
    if (store.activeKey === k) store.activeKey = (store.items[0]?.key || "");
    writeStore(store);
  }

  function clearAll() {
    writeStore({ items: [], activeKey: "" });
  }

  function list() {
    return readStore().items.slice();
  }

  function getActive() {
    const store = readStore();
    const active = store.items.find(x => x && x.key === store.activeKey);
    return active || store.items[0] || null;
  }

  function setActive(key) {
    const store = readStore();
    const k = (key || "").toString();
    if (store.items.some(x => x && x.key === k)) {
      store.activeKey = k;
      writeStore(store);
    }
  }

  function anyAiModalOpen() {
    if (document.body.classList.contains("modal-open")) return true;
    if (document.querySelector(".modal-backdrop")) return true;
    const ids = ["wmAiResultModal", "aiTransformModal"];
    for (const id of ids) {
      const el = document.getElementById(id);
      if (el && el.classList.contains("show")) return true;
    }
    return false;
  }

  async function fetchJson(url) {
    const res = await fetch(url, { method: "GET", headers: { "Accept": "application/json" } });
    const text = await res.text().catch(() => "");
    const payload = safeJsonParse(text);
    return { ok: res.ok, status: res.status, payload, raw: text };
  }

  // UI integration (optional elements)
  function initUi(opts) {
    const mode = (opts && opts.mode) ? opts.mode : "bar";
    const bar = document.getElementById("wmAiResumeBar");
    const statusText = document.getElementById("wmAiResumeText");
    const spinner = document.getElementById("wmAiResumeSpinner");
    const openBtn = document.getElementById("wmAiResumeOpenBtn");
    const dismissBtn = document.getElementById("wmAiResumeDismissBtn");
    const listBtn = document.getElementById("wmAiResumeListBtn");

    const toastEl = document.getElementById("wmAiToast");
    const toastText = document.getElementById("wmAiToastText");
    const toastOpenBtn = document.getElementById("wmAiToastOpenBtn");
    let toastInstance = null;

    const modalEl = document.getElementById("wmAiResultModal");
    const modalTitle = document.getElementById("wmAiResultTitle");
    const modalLoading = document.getElementById("wmAiResultLoading");
    const modalError = document.getElementById("wmAiResultError");
    const modalContent = document.getElementById("wmAiResultContent");
    const modalSummary = document.getElementById("wmAiResultSummary");
    const modalIngredientsText = document.getElementById("wmAiResultIngredientsText");
    const modalPreparationText = document.getElementById("wmAiResultPreparationText");
    let modalInstance = null;

    const listModalEl = document.getElementById("wmAiJobsModal");
    const listBody = document.getElementById("wmAiJobsList");
    let listModalInstance = null;

    function showToast(msg, showOpen) {
      if (!toastEl || !window.bootstrap) return;
      if (anyAiModalOpen()) return;
      if (toastText) toastText.textContent = msg || "AI ist fertig.";
      if (toastOpenBtn) toastOpenBtn.style.display = showOpen ? "" : "none";
      toastInstance = toastInstance || new bootstrap.Toast(toastEl, { delay: 4500 });
      toastInstance.show();
    }

    function renderBar() {
      if (!bar || !statusText || !spinner) return;
      if (mode !== "bar") { bar.style.display = "none"; return; }
      const active = getActive();
      if (!active || !active.recipeId || !active.variantType) { bar.style.display = "none"; return; }
      if (anyAiModalOpen()) { bar.style.display = "none"; return; }

      const stage = (active.stage || "").toString();
      if (stage !== "queued" && stage !== "running" && stage !== "ready" && stage !== "error") { bar.style.display = "none"; return; }

      const label = active.title ? `AI: ${active.title}` : "AI Vorschau";
      if (stage === "queued" || stage === "running") {
        spinner.style.display = "";
        statusText.textContent = `${label} wird erstellt...`;
      } else if (stage === "ready") {
        spinner.style.display = "none";
        statusText.textContent = `${label} ist bereit.`;
      } else if (stage === "error") {
        spinner.style.display = "none";
        statusText.textContent = `${label}: Fehler (erneut oeffnen).`;
      }

      const all = list();
      if (listBtn) listBtn.style.display = all.length > 1 ? "" : "none";
      bar.style.display = "";
    }

    function renderJobsList() {
      if (!listBody) return;
      const items = list();
      if (items.length === 0) {
        listBody.innerHTML = "<div class=\"text-muted\">Keine AI Jobs.</div>";
        return;
      }
      const rows = items.map(it => {
        const stage = (it.stage || "").toString();
        const title = it.title ? it.title : it.variantType;
        const badge = stage === "ready" ? "bg-success" : stage === "error" ? "bg-danger" : "bg-warning text-dark";
        const stageText = stage || "pending";
        return `
          <div class="d-flex align-items-center justify-content-between border rounded-3 p-2 mb-2">
            <div class="me-2" style="min-width:0;">
              <div class="fw-semibold text-truncate">${escapeHtml(title)}</div>
              <div class="small text-muted">${escapeHtml(it.variantType || "")}</div>
            </div>
            <div class="d-flex align-items-center gap-2">
              <span class="badge ${badge}">${escapeHtml(stageText)}</span>
              <button type="button" class="btn btn-sm btn-outline-primary" data-wm-open="${escapeHtml(it.key)}">Oeffnen</button>
            </div>
          </div>`;
      }).join("");
      listBody.innerHTML = rows;

      listBody.querySelectorAll("[data-wm-open]").forEach(btn => {
        btn.addEventListener("click", async () => {
          const key = btn.getAttribute("data-wm-open");
          const entry = list().find(x => x && x.key === key);
          if (!entry) return;
          setActive(entry.key);
          renderBar();
          if (listModalInstance) listModalInstance.hide();
          await openOverlay(entry);
        });
      });
    }

    function escapeHtml(str) {
      return (str || "").toString()
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;")
        .replaceAll("'", "&#039;");
    }

    async function openOverlay(entry) {
      const e = normalizeEntry(entry);
      setActive(e.key);
      renderBar();

      // Prefer recipe page overlay.
      if (typeof window.openAiJobResult === "function" && e.jobId) {
        window.openAiJobResult(e.jobId, e.variantType, e.aiProvider || "openai");
        return;
      }

      if (!e.jobId && typeof window.openAiTransformModal === "function" && e.variantType) {
        window.openAiTransformModal(e.variantType, e.aiProvider || "openai");
        return;
      }

      if (!e.jobId) {
        showToast("AI laeuft noch. Bitte kurz warten.", false);
        return;
      }

      if (!modalEl || !window.bootstrap) return;
      modalInstance = modalInstance || new bootstrap.Modal(modalEl);
      modalInstance.show();
      if (modalTitle) modalTitle.textContent = e.title ? `AI: ${e.title}` : "AI Vorschau";
      if (modalError) { modalError.style.display = "none"; modalError.textContent = ""; }
      if (modalContent) modalContent.style.display = "none";
      if (modalLoading) modalLoading.style.display = "";

      const jobId = e.jobId;
      try {
        // If still running, wait a bit.
        const st0 = await fetchJson(`/WorldMiniApp/Home/GetAiRecipeVariantJobStatus?jobId=${encodeURIComponent(jobId)}`);
        const s = (st0.payload && st0.payload.state) ? st0.payload.state.toString() : "";
        if (!st0.ok && st0.status === 404 && typeof window.openAiTransformModal === "function") {
          modalInstance.hide();
          window.openAiTransformModal(e.variantType, e.aiProvider || "openai");
          return;
        }
        if (s && s !== "ready" && s !== "error") {
          const start = now();
          while ((now() - start) < 20000) {
            if (!modalEl.classList.contains("show")) break;
            await new Promise(r => setTimeout(r, 1200));
            const st1 = await fetchJson(`/WorldMiniApp/Home/GetAiRecipeVariantJobStatus?jobId=${encodeURIComponent(jobId)}`);
            const s1 = (st1.payload && st1.payload.state) ? st1.payload.state.toString() : "";
            if (s1 === "ready" || s1 === "error") break;
          }
        }
        const result = await fetchJson(`/WorldMiniApp/Home/GetAiRecipeVariantJobResult?jobId=${encodeURIComponent(jobId)}`);
        if (!result.ok || !result.payload) {
          throw new Error((result.payload && result.payload.message) ? result.payload.message : (result.raw || "AI-Ergebnis ist noch nicht bereit."));
        }
        const payload = result.payload;
        if (modalTitle) modalTitle.textContent = payload.title || (e.title ? `AI: ${e.title}` : "AI Vorschau");
        if (modalSummary) modalSummary.textContent = payload.summary || "";
        if (modalIngredientsText) modalIngredientsText.textContent = payload.ingredientsText || "";
        if (modalPreparationText) modalPreparationText.textContent = payload.preparationText || "";
        if (modalLoading) modalLoading.style.display = "none";
        if (modalContent) modalContent.style.display = "";
      } catch (err) {
        if (modalLoading) modalLoading.style.display = "none";
        if (modalContent) modalContent.style.display = "none";
        if (modalError) {
          modalError.textContent = (err && err.message) ? err.message : "AI-Ergebnis konnte nicht geladen werden.";
          modalError.style.display = "";
        }
      }
    }

    function openActive() {
      const active = getActive();
      if (!active) return;
      openOverlay(active);
    }

    function openList() {
      if (!listModalEl || !window.bootstrap) return;
      listModalInstance = listModalInstance || new bootstrap.Modal(listModalEl);
      renderJobsList();
      listModalInstance.show();
    }

    openBtn && openBtn.addEventListener("click", openActive);
    toastOpenBtn && toastOpenBtn.addEventListener("click", openActive);
    dismissBtn && dismissBtn.addEventListener("click", () => {
      const active = getActive();
      if (active) removeByKey(active.key);
      renderBar();
    });
    listBtn && listBtn.addEventListener("click", openList);

    async function pollOnce() {
      const items = list();
      const inflight = items.filter(x => x && x.jobId && (x.stage === "queued" || x.stage === "running"));
      if (inflight.length === 0) return;

      // poll a few entries per tick to avoid storms
      const batch = inflight.slice(0, 3);
      for (const it of batch) {
        try {
          const st = await fetchJson(`/WorldMiniApp/Home/GetAiRecipeVariantJobStatus?jobId=${encodeURIComponent(it.jobId)}`);
          if (!st.ok) {
            if (st.status === 404 && it.updatedAt && (now() - Number(it.updatedAt)) < 15000) continue;
            upsert({ ...it, stage: "error", message: (st.payload && st.payload.message) ? st.payload.message : (st.raw || `HTTP ${st.status}`) }, false);
            showToast("AI Vorschau ist fehlgeschlagen.", true);
            continue;
          }
          const nextStage = (st.payload && st.payload.state) ? st.payload.state.toString() : "";
          const nextTitle = (st.payload && st.payload.title) ? st.payload.title.toString() : (it.title || "");
          const msg = (st.payload && st.payload.message) ? st.payload.message.toString() : "";
          const updated = upsert({ ...it, stage: nextStage, title: nextTitle, message: msg }, false);
          if (nextStage === "ready") showToast("AI Vorschau ist fertig.", true);
          if (nextStage === "error") showToast("AI Vorschau ist fehlgeschlagen.", true);
          // keep stage running/queued silent
          if (getActive() && getActive().key === updated.key) renderBar();
        } catch {
          // ignore
        }
      }
    }

    setInterval(pollOnce, 1500);
    renderBar();
  }

  // Back-compat: setState(clearState/readState) for existing calls.
  const api = {
    __v: 2,
    init: initUi,
    upsert,
    removeByKey,
    clearAll,
    list,
    getActive,
    setActive,
    anyAiModalOpen,
    // legacy
    setState: function (state) { return upsert(state, true); },
    clearState: function () {
      const active = getActive();
      if (active) removeByKey(active.key);
    },
    readState: function () { return getActive(); },
    toast: function () { /* no-op, old api */ }
  };

  window.WorldMiniAppAiResume = api;
})();

