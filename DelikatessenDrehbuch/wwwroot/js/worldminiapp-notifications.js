// Lightweight notifications for WorldMiniApp (client-only, sessionStorage).
// No backend, no Redis. Used mainly for AI "job ready/error" notices.
// ASCII-only (avoid encoding issues).
(function () {
  if (window.WorldMiniAppNotify && window.WorldMiniAppNotify.__v === 1) return;

  const KEY = "worldminiapp.notifications.v1";
  let serverMode = false;
  let serverCfg = null;
  const UIKEY = "worldminiapp.notifications.ui.v1";

  function now() { return Date.now(); }

  function safeParse(text) {
    try { return text ? JSON.parse(text) : null; } catch { return null; }
  }

  function readStore() {
    try {
      const raw = sessionStorage.getItem(KEY);
      const obj = safeParse(raw) || {};
      const items = Array.isArray(obj.items) ? obj.items : [];
      return { items };
    } catch {
      return { items: [] };
    }
  }

  function writeStore(store) {
    try {
      sessionStorage.setItem(KEY, JSON.stringify({ items: store.items || [], updatedAt: now() }));
    } catch {
      // ignore
    }
  }

  function uuid() {
    return `${now()}_${Math.random().toString(16).slice(2)}`;
  }

  function add(item) {
    const store = readStore();
    const entry = {
      id: uuid(),
      ts: now(),
      kind: (item && item.kind) ? String(item.kind) : "info",
      title: (item && item.title) ? String(item.title) : "Info",
      body: (item && item.body) ? String(item.body) : "",
      actionLabel: (item && item.actionLabel) ? String(item.actionLabel) : "",
      action: (item && item.action) ? item.action : null,
      read: false
    };
    store.items.unshift(entry);
    // cap
    if (store.items.length > 50) store.items = store.items.slice(0, 50);
    writeStore(store);
    notifyChanged();
    return entry;
  }

  function list() {
    return readStore().items.slice();
  }

  function unreadCount() {
    return list().filter(x => x && !x.read).length;
  }

  function markRead(id) {
    const store = readStore();
    const idx = store.items.findIndex(x => x && x.id === id);
    if (idx >= 0) {
      store.items[idx] = { ...store.items[idx], read: true };
      writeStore(store);
      notifyChanged();
    }
  }

  function markAllRead() {
    const store = readStore();
    store.items = store.items.map(x => x ? { ...x, read: true } : x);
    writeStore(store);
    notifyChanged();
  }

  function clearAll() {
    writeStore({ items: [] });
    notifyChanged();
  }

  function notifyChanged() {
    try { window.dispatchEvent(new CustomEvent("wm:notify:changed")); } catch { /* ignore */ }
  }

  function parseHrefPostingId(href) {
    try {
      const raw = String(href || "");
      if (!raw) return 0;
      const url = new URL(raw, window.location.origin);
      return Number(url.searchParams.get("postingId") || 0) || 0;
    } catch {
      return 0;
    }
  }

  function buildFeedCommentBadgeMap(items) {
    const map = {};
    (Array.isArray(items) ? items : []).forEach(it => {
      const sender = String(it?.sender || "").toLowerCase();
      if (sender !== "comment-reply" && sender !== "comment-video") return;
      const postingId = parseHrefPostingId(it?.href);
      if (!postingId) return;
      map[postingId] = (map[postingId] || 0) + 1;
    });
    return map;
  }

  function emitFeedBadgeUpdate(items) {
    try {
      const detail = {
        counts: buildFeedCommentBadgeMap(items)
      };
      window.dispatchEvent(new CustomEvent("wm:notify:feedbadges", { detail }));
    } catch {
      // ignore
    }
  }

  function readAntiForgeryToken(formId) {
    try {
      if (!formId) return null;
      const form = document.getElementById(formId);
      const token = form && form.querySelector('input[name="__RequestVerificationToken"]')?.value;
      return token || null;
    } catch {
      return null;
    }
  }

  async function apiFetch(url, opts) {
    const o = opts || {};
    const headers = { ...(o.headers || {}) };
    const token = serverCfg ? readAntiForgeryToken(serverCfg.antiforgeryFormId) : null;
    if (token) headers["RequestVerificationToken"] = token;
    const res = await fetch(url, { ...o, headers });
    return res;
  }

  async function apiList() {
    if (!serverCfg?.listUrl) return [];
    const res = await apiFetch(serverCfg.listUrl, { method: "GET", headers: { "Accept": "application/json" } });
    if (!res.ok) return [];
    const payload = await res.json().catch(() => null);
    return Array.isArray(payload?.items) ? payload.items : (Array.isArray(payload) ? payload : []);
  }

  async function apiUnread() {
    if (!serverCfg?.unreadUrl) return [];
    const res = await apiFetch(serverCfg.unreadUrl, { method: "GET", headers: { "Accept": "application/json" } });
    if (!res.ok) return [];
    const payload = await res.json().catch(() => null);
    return Array.isArray(payload?.items) ? payload.items : (Array.isArray(payload) ? payload : []);
  }

  async function apiMarkSeen(id) {
    if (!serverCfg?.markSeenUrl) return;
    await apiFetch(serverCfg.markSeenUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json", "Accept": "application/json" },
      body: JSON.stringify({ id })
    });
  }

  async function apiMarkAllSeen() {
    if (!serverCfg?.markAllSeenUrl) return;
    await apiFetch(serverCfg.markAllSeenUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json", "Accept": "application/json" },
      body: JSON.stringify({})
    });
  }

  async function apiClearAll() {
    if (!serverCfg?.clearAllUrl) return;
    await apiFetch(serverCfg.clearAllUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json", "Accept": "application/json" },
      body: JSON.stringify({})
    });
  }

  async function refresh() {
    // In server mode we just trigger a UI update; the UI will refetch.
    notifyChanged();
  }

  // AI helper: create a consistent notification + wire action to open overlay.
  function addAiJobNotice(payload) {
    const p = payload || {};
    const stage = String(p.stage || "");
    const title = p.title ? String(p.title) : "AI Vorschau";
    const recipeId = Number(p.recipeId || 0);
    const variantType = p.variantType ? String(p.variantType) : "";
    const aiProvider = p.aiProvider ? String(p.aiProvider) : "openai";
    const jobId = p.jobId ? String(p.jobId) : "";

    const isError = stage === "error";
    const kind = isError ? "error" : "success";
    const body = isError ? "AI ist fehlgeschlagen." : "AI ist fertig.";

    return add({
      kind,
      title,
      body,
      actionLabel: "Oeffnen",
      action: { type: "ai_open", recipeId, variantType, aiProvider, jobId, stage, title }
    });
  }

  // UI binding for Feed bottom nav (offcanvas overlay)
  function initFeedOverlay(opts) {
    // Enable server mode when endpoints are provided.
    if (opts && opts.listUrl && opts.markSeenUrl) {
      serverMode = true;
      serverCfg = {
        listUrl: String(opts.listUrl),
        unreadUrl: opts.unreadUrl ? String(opts.unreadUrl) : null,
        markSeenUrl: String(opts.markSeenUrl),
        markAllSeenUrl: opts.markAllSeenUrl ? String(opts.markAllSeenUrl) : null,
        clearAllUrl: opts.clearAllUrl ? String(opts.clearAllUrl) : null,
        antiforgeryFormId: opts.antiforgeryFormId ? String(opts.antiforgeryFormId) : null
      };
    }

    const btn = document.getElementById(opts.buttonId);
    const badge = document.getElementById(opts.badgeId);
    const canvasEl = document.getElementById(opts.canvasId);
    const listEl = document.getElementById(opts.listId);
    const markAllBtn = document.getElementById(opts.markAllId);
    const clearBtn = document.getElementById(opts.clearId);
    const filterAllBtn = opts.filterAllId ? document.getElementById(opts.filterAllId) : null;
    const filterAiBtn = opts.filterAiId ? document.getElementById(opts.filterAiId) : null;
    const groupToggle = opts.groupToggleId ? document.getElementById(opts.groupToggleId) : null;

    if (!btn || !canvasEl || !listEl || !window.bootstrap) return;
    const canvas = new bootstrap.Offcanvas(canvasEl);
    const pollMs = Math.max(2000, Number(opts.pollMs || 0) || 0);

    function readUi() {
      try {
        const raw = sessionStorage.getItem(UIKEY);
        const obj = safeParse(raw) || {};
        return {
          filter: (obj.filter === "ai") ? "ai" : "all",
          groupBySender: obj.groupBySender !== false
        };
      } catch {
        return { filter: "all", groupBySender: true };
      }
    }

    function writeUi(next) {
      try { sessionStorage.setItem(UIKEY, JSON.stringify(next || {})); } catch { /* ignore */ }
    }

    function isCanvasOpen() {
      try { return canvasEl.classList.contains("show"); } catch { return false; }
    }

    function applyUiButtons(ui) {
      if (filterAllBtn) filterAllBtn.classList.toggle("active", ui.filter === "all");
      if (filterAiBtn) filterAiBtn.classList.toggle("active", ui.filter === "ai");
      if (groupToggle) groupToggle.checked = !!ui.groupBySender;
    }

    function fmtTime(ts) {
      try {
        if (!ts) return "";
        const n = Number(ts);
        const d = Number.isFinite(n) ? new Date(n) : new Date(String(ts));
        return d.toLocaleTimeString("de-AT", { hour: "2-digit", minute: "2-digit" });
      } catch {
        return "";
      }
    }

    function escapeHtml(str) {
      return (str || "").toString()
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;")
        .replaceAll("'", "&#039;");
    }

  async function updateBadge() {
      const items = serverMode ? await apiUnread() : list();
      const u = serverMode ? items.filter(x => x && !x.isSeen).length : unreadCount();
      emitFeedBadgeUpdate(serverMode ? items.filter(x => x && !x.isSeen) : items.filter(x => x && !x.read));
      if (badge) {
        badge.style.display = u > 0 ? "" : "none";
        badge.textContent = u > 9 ? "9+" : String(u);
      }
      return { items, unread: u };
    }

    function groupItems(items, ui) {
      const filtered = (ui.filter === "ai")
        ? items.filter(x => (String(x?.sender || "")).toLowerCase() === "ai")
        : items;

      if (!ui.groupBySender) {
        return [{ key: "", label: "", items: filtered }];
      }

      const groups = new Map();
      filtered.forEach(it => {
        const sender = String(it?.sender || "Info");
        if (!groups.has(sender)) groups.set(sender, []);
        groups.get(sender).push(it);
      });

      return Array.from(groups.entries()).map(([key, arr]) => ({ key, label: key, items: arr }));
    }

    async function render() {
      const ui = readUi();
      applyUiButtons(ui);

      const items = serverMode ? await apiList() : list();
      await updateBadge();

      if (items.length === 0) {
        listEl.innerHTML = "<div class=\"text-muted\">Keine Benachrichtigungen.</div>";
        return;
      }

      const blocks = groupItems(items, ui);
      listEl.innerHTML = blocks.map(block => {
        const header = (block.label && blocks.length > 1)
          ? `<div class="small text-muted fw-semibold mb-2 mt-2">${escapeHtml(block.label)}</div>`
          : "";

        const rows = block.items.map(it => {
          const isSeen = serverMode ? !!it.isSeen : !!it.read;
          const cls = isSeen ? "opacity-75" : "";
          const kind = serverMode ? String(it.kind || "") : String(it.kind || "");
          const kindCls = kind === "error" ? "text-danger" : kind === "success" ? "text-success" : "text-muted";
          const icon = serverMode ? String(it.icon || "bi-bell") : "";
          const title = serverMode ? String(it.title || "Info") : String(it.title || "");
          const body = serverMode ? String(it.description || "") : String(it.body || "");
          const actionBtn = `<button type="button" class="btn btn-sm btn-outline-primary" data-wm-act="${escapeHtml(it.id)}">Oeffnen</button>`;
          const ts = serverMode ? (it.createdAtUtc || it.createdAt || it.ts) : it.ts;
          return `
            <div class="border rounded-3 p-2 mb-2 ${cls}">
              <div class="d-flex justify-content-between align-items-start gap-2">
                <div style="min-width:0;">
                  <div class="fw-semibold text-truncate">
                    ${serverMode ? `<i class="bi ${escapeHtml(icon)} me-1"></i>` : ""}${escapeHtml(title)}
                  </div>
                  <div class="small ${kindCls}" style="white-space:pre-wrap;">${escapeHtml(body)}</div>
                </div>
                <div class="small text-muted">${escapeHtml(fmtTime(ts))}</div>
              </div>
              <div class="mt-2 d-flex justify-content-end">${actionBtn}</div>
            </div>`;
        }).join("");

        return `${header}${rows}`;
      }).join("");

      listEl.querySelectorAll("[data-wm-act]").forEach(el => {
        el.addEventListener("click", async () => {
          const id = el.getAttribute("data-wm-act");
          const all = serverMode ? await apiList() : list();
          const it = all.find(x => x && String(x.id) === String(id));
          if (!it) return;
          if (serverMode) await apiMarkSeen(it.id);
          else markRead(it.id);
          canvas.hide();
          await runAction(serverMode ? it : it.action);
        });
      });
    }

    function parseWmAiHref(href) {
      try {
        const h = String(href || "");
        if (!h.startsWith("wm-ai://open")) return null;
        const q = h.includes("?") ? h.split("?")[1] : "";
        const params = new URLSearchParams(q);
        const jobId = params.get("jobId") || "";
        const recipeId = Number(params.get("recipeId") || 0);
        const variantType = params.get("variantType") || "";
        const aiProvider = params.get("aiProvider") || "openai";
        return { jobId, recipeId, variantType, aiProvider };
      } catch {
        return null;
      }
    }

    async function runAction(actionOrItem) {
      // Server mode: item is a notification row with Href; client mode: action object.
      if (serverMode) {
        const info = parseWmAiHref(actionOrItem?.href);
        if (info?.jobId) {
          const stage = (String(actionOrItem?.kind || "") === "error") ? "error" : "ready";
          window.WorldMiniAppAiResume?.setState?.({
            recipeId: info.recipeId,
            variantType: info.variantType,
            aiProvider: info.aiProvider,
            stage,
            jobId: info.jobId,
            title: actionOrItem?.title || ""
          });
          await window.WorldMiniAppAiResume?.openOverlay?.();
          return;
        }
        if (actionOrItem?.href) {
          window.location.href = String(actionOrItem.href);
        }
        return;
      }

      const action = actionOrItem;
      if (!action || !action.type) return;
      if (action.type === "ai_open") {
        const stage = action.stage || "ready";
        window.WorldMiniAppAiResume?.setState?.({
          recipeId: action.recipeId,
          variantType: action.variantType,
          aiProvider: action.aiProvider,
          stage,
          jobId: action.jobId,
          title: action.title || ""
        });
        await window.WorldMiniAppAiResume?.openOverlay?.();
      }
    }

    btn.addEventListener("click", () => {
      render();
      canvas.show();
    });

    filterAllBtn && filterAllBtn.addEventListener("click", async () => {
      const ui = readUi();
      const next = { ...ui, filter: "all" };
      writeUi(next);
      await render();
    });
    filterAiBtn && filterAiBtn.addEventListener("click", async () => {
      const ui = readUi();
      const next = { ...ui, filter: "ai" };
      writeUi(next);
      await render();
    });
    groupToggle && groupToggle.addEventListener("change", async () => {
      const ui = readUi();
      const next = { ...ui, groupBySender: !!groupToggle.checked };
      writeUi(next);
      await render();
    });

    markAllBtn && markAllBtn.addEventListener("click", async () => {
      if (serverMode) await apiMarkAllSeen();
      else markAllRead();
      await render();
    });
    clearBtn && clearBtn.addEventListener("click", async () => {
      if (serverMode) await apiClearAll();
      else clearAll();
      await render();
    });

    window.addEventListener("wm:notify:changed", async () => {
      // Always update badge; only re-render list when the canvas is open.
      await updateBadge();
      if (isCanvasOpen()) await render();
    });

    async function pollTick() {
      if (!serverMode || !pollMs) return;
      if (document.hidden) return;
      await updateBadge();
      if (isCanvasOpen()) await render();
    }

    if (serverMode && pollMs) {
      setInterval(pollTick, pollMs);
      window.addEventListener("focus", pollTick);
      document.addEventListener("visibilitychange", pollTick);
    }

    // initial badge + initial list render (only list if canvas opened)
    updateBadge();
    applyUiButtons(readUi());
  }

  window.WorldMiniAppNotify = {
    __v: 1,
    add,
    list,
    unreadCount,
    markRead,
    markAllRead,
    clearAll,
    addAiJobNotice,
    initFeedOverlay,
    refresh,
    __isServerMode: () => serverMode
  };
})();
