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

  function getServerEventCount(item) {
    const unread = Number(item?.unreadEventCount || 0);
    if (Number.isFinite(unread) && unread > 0) return unread;
    return item && item.isSeen ? 0 : 1;
  }

  function getServerAggregateCount(item) {
    const count = Number(item?.aggregateCount || 0);
    return Number.isFinite(count) && count > 0 ? count : 1;
  }

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
      map[postingId] = (map[postingId] || 0) + getServerEventCount(it);
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
      if (filterAllBtn) {
        filterAllBtn.style.background = ui.filter === "all" ? "#14391f" : "transparent";
        filterAllBtn.style.color = ui.filter === "all" ? "#fff" : "#14391f";
      }
      if (filterAiBtn) {
        filterAiBtn.style.background = ui.filter === "ai" ? "#14391f" : "transparent";
        filterAiBtn.style.color = ui.filter === "ai" ? "#fff" : "#14391f";
      }
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
      const u = serverMode
        ? items.reduce((sum, x) => sum + getServerEventCount(x), 0)
        : unreadCount();
      emitFeedBadgeUpdate(serverMode ? items.filter(x => x && !x.isSeen) : items.filter(x => x && !x.read));
      if (badge) {
        badge.style.display = u > 0 ? "" : "none";
        badge.textContent = u > 9 ? "9+" : String(u);
      }
      return { items, unread: u };
    }

    function detectType(it) {
      const sender = String(it?.sender || "").toLowerCase();
      if (sender === "ai") return "ai";
      if (sender === "plan" || sender === "mealplan") return "plan";
      return "system";
    }

    function fmtRelTime(ts) {
      try {
        if (!ts) return "";
        const n = Number(ts);
        const d = Number.isFinite(n) ? new Date(n) : new Date(String(ts));
        const now = new Date();
        const diffMs = now - d;
        const diffDays = Math.floor(diffMs / 86400000);
        if (diffDays <= 0) return d.toLocaleTimeString("de-AT", { hour: "2-digit", minute: "2-digit" });
        if (diffDays === 1) return "Gestern";
        return "Vor " + diffDays + " Tagen";
      } catch { return ""; }
    }

    function isToday(ts) {
      try {
        const n = Number(ts);
        const d = Number.isFinite(n) ? new Date(n) : new Date(String(ts));
        const now = new Date();
        return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth() && d.getDate() === now.getDate();
      } catch { return false; }
    }

    function groupItems(items, ui) {
      const filtered = (ui.filter === "ai")
        ? items.filter(x => detectType(x) === "ai")
        : items;

      if (!ui.groupBySender) {
        return [{ key: "", label: "", items: filtered }];
      }

      const today = [];
      const earlier = [];
      filtered.forEach(it => {
        const ts = serverMode ? (it.createdAtUtc || it.createdAt || it.ts) : it.ts;
        if (isToday(ts)) today.push(it);
        else earlier.push(it);
      });
      const result = [];
      if (today.length) result.push({ key: "today", label: "HEUTE", items: today });
      if (earlier.length) result.push({ key: "earlier", label: "FR\u00dcHER", items: earlier });
      if (!result.length && filtered.length) result.push({ key: "", label: "", items: filtered });
      return result;
    }

    function renderCard(it) {
      const isSeen = serverMode ? !!it.isSeen : !!it.read;
      const type = detectType(it);
      const title = serverMode ? String(it.title || "Info") : String(it.title || "");
      const body = serverMode ? String(it.description || "") : String(it.body || "");
      const ts = serverMode ? (it.createdAtUtc || it.createdAt || it.ts) : it.ts;
      const timeStr = fmtRelTime(ts);
      const href = serverMode ? String(it.href || "") : "";
      const aggregateCount = serverMode ? getServerAggregateCount(it) : 1;
      const kcalMatch = body.match(/(\d+)\s*kcal/i);
      const kcal = kcalMatch ? kcalMatch[1] : "";

      // Unread strip
      const strip = !isSeen
        ? '<div style="position:absolute;top:0;bottom:0;left:0;width:3px;background:linear-gradient(180deg,#ff9a3c,#ff7849);border-radius:3px 0 0 3px;"></div>'
        : "";

      // Thumbnail
      let thumb = "";
      if (type === "ai") {
        thumb = '<div style="width:50px;height:50px;border-radius:11px;background:linear-gradient(135deg,#14391f,#1a4a28);flex-shrink:0;display:flex;align-items:center;justify-content:center;position:relative;">'
          + '<span style="color:#f5b942;font-size:18px;">&#10022;</span>'
          + '<span style="position:absolute;top:-3px;right:-3px;width:18px;height:18px;border-radius:50%;background:linear-gradient(135deg,#14391f,#1a4a28);border:2px solid #fff;display:flex;align-items:center;justify-content:center;font-size:9px;color:#f5b942;">&#10022;</span>'
          + '</div>';
      } else if (type === "plan") {
        thumb = '<div style="width:50px;height:50px;border-radius:11px;background:linear-gradient(135deg,#5fa052,#3a7a30);flex-shrink:0;display:flex;align-items:center;justify-content:center;color:#fff;">'
          + '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/></svg>'
          + '</div>';
      } else {
        thumb = '<div style="width:50px;height:50px;border-radius:11px;background:linear-gradient(135deg,#f5b942,#c8a45c);flex-shrink:0;display:flex;align-items:center;justify-content:center;color:#fff;">'
          + '<span style="font-size:18px;">&#128293;</span>'
          + '</div>';
      }

      // Type badge
      let badge = "";
      if (type === "ai") {
        badge = '<span style="background:linear-gradient(135deg,#14391f,#1a4a28);color:#fff;padding:2px 7px;border-radius:999px;font-size:9px;font-weight:700;letter-spacing:0.04em;text-transform:uppercase;display:inline-flex;align-items:center;gap:3px;"><span style="color:#f5b942;">&#10022;</span> AI</span>';
      } else if (type === "plan") {
        badge = '<span style="background:rgba(95,160,82,0.12);color:#3a7a30;padding:2px 7px;border-radius:999px;font-size:9px;font-weight:700;letter-spacing:0.04em;text-transform:uppercase;">PLAN</span>';
      }

      const aggregatePill = aggregateCount > 1
        ? '<span style="background:rgba(20,57,31,0.08);color:#14391f;padding:2px 7px;border-radius:999px;font-size:9px;font-weight:700;line-height:1;">' + escapeHtml(String(aggregateCount)) + '</span>'
        : "";

      // kcal pill
      const kcalPill = kcal
        ? '<span style="font-size:10px;font-weight:600;color:#6b7868;display:inline-flex;align-items:center;gap:4px;background:#faf6ec;padding:3px 8px;border-radius:999px;"><span style="color:#ff7849;">&#128293;</span>' + escapeHtml(kcal) + ' kcal</span>'
        : "";

      return '<article style="background:#fff;border-radius:16px;padding:12px;margin-bottom:8px;box-shadow:0 3px 10px rgba(20,57,31,0.05);position:relative;overflow:hidden;display:flex;gap:11px;cursor:pointer;" data-wm-act="' + escapeHtml(it.id) + '">'
        + strip + thumb
        + '<div style="flex:1;min-width:0;">'
        + '<div style="display:flex;align-items:center;gap:6px;margin-bottom:3px;">'
        + badge
        + aggregatePill
        + '<span style="font-size:11px;font-weight:600;color:#14391f;flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">' + escapeHtml(title) + '</span>'
        + '<span style="font-size:10px;color:#6b7868;flex-shrink:0;">' + escapeHtml(timeStr) + '</span>'
        + '</div>'
        + '<div style="font-size:12px;color:#1a2e20;font-weight:500;line-height:1.35;margin-bottom:8px;">' + escapeHtml(body) + '</div>'
        + '<div style="display:flex;align-items:center;gap:7px;">'
        + kcalPill
        + '<button type="button" style="margin-left:auto;background:linear-gradient(135deg,#ff9a3c,#ff7849);border:none;border-radius:9px;padding:5px 12px;font-size:10px;font-weight:700;color:#fff;cursor:pointer;display:inline-flex;align-items:center;gap:4px;box-shadow:0 2px 6px rgba(255,120,73,0.3);" data-wm-act="' + escapeHtml(it.id) + '">'
        + '\u00d6ffnen <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5"><path d="M5 12h14M13 5l7 7-7 7"/></svg>'
        + '</button>'
        + '</div>'
        + '</div>'
        + '</article>';
    }

    function renderSectionHeader(label, count) {
      return '<div style="display:flex;align-items:center;gap:8px;padding:4px 4px 8px;">'
        + '<span style="font-size:10px;font-weight:700;color:#14391f;text-transform:uppercase;letter-spacing:0.06em;">' + escapeHtml(label) + '</span>'
        + '<span style="font-size:10px;font-weight:600;color:#6b7868;background:rgba(20,57,31,0.06);padding:1px 7px;border-radius:999px;">' + count + '</span>'
        + '<span style="flex:1;height:1px;background:rgba(20,57,31,0.08);"></span>'
        + '</div>';
    }

    async function render() {
      const ui = readUi();
      applyUiButtons(ui);

      const items = serverMode ? await apiList() : list();
      await updateBadge();

      // Update filter pill count
      if (filterAllBtn) filterAllBtn.textContent = "Alle \u00b7 " + items.length;

      if (items.length === 0) {
        listEl.innerHTML = '<div style="text-align:center;padding:40px 20px;color:#6b7868;font-size:13px;">Keine Benachrichtigungen.</div>';
        return;
      }

      const blocks = groupItems(items, ui);
      listEl.innerHTML = blocks.map(block => {
        const header = block.label
          ? renderSectionHeader(block.label, block.items.length)
          : "";
        const rows = block.items.map(it => renderCard(it)).join("");
        return header + rows;
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
