import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js@2.0.3/+esm";
// World ID 4.0: verify wurde aus MiniKit entfernt und läuft jetzt über IDKit.
// IDKit-Core ist die Vanilla-Variante (kein React nötig).
// WICHTIG: über esm.sh laden, NICHT über jsDelivr "+esm" – idkit-core lädt zur
// Laufzeit ein 827-KB-WASM-Modul, das jsDelivr "+esm" nicht korrekt ausliefert
// ("failed to compile WebAssembly: HTTP status code is not ok"). esm.sh liefert
// die .wasm samt Abhängigkeiten korrekt aus. Version exakt gepinnt (= Backend 4.1.8).
import { IDKit, orbLegacy, deviceLegacy, proofOfHuman, isInWorldApp } from "https://esm.sh/@worldcoin/idkit-core@4.1.8";

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const REMEMBER_LOGIN_KEY = "remember_login";
const VERIFY_ACTION = "login-delikatessendrehbuch";
const PAY_ACTION = "pay-delikatessendrehbuch";

const ALLOW_MOCK_EVERYWHERE = false;

let currentConfig = {
    level: 'device',
    redirectUrl: '/WorldMiniApp/Home/Setup'
};

let loginModalRetryTimer = null;
let loginModalOpening = false;

function log(msg, error = false) {
    console.log(msg);
    dbg(msg, error ? 'error' : 'info');
    const el = document.getElementById('login-status');
    if (el) {
        el.innerHTML = `<div style="color:${error ? 'red' : '#555'}">${msg}</div>`;
    }
}

// =====================================================================
// DEBUG-OVERLAY für die World App (Webview hat keine DevTools).
// Zeigt jeden Schritt + Fehler on-screen UND speichert sie in localStorage,
// damit sie auch nach einem Schwarzbild/Reload beim Neuöffnen sichtbar sind.
// Aufrufen über dbg(text, 'info'|'warn'|'error'). Sichtbar über den 🐞-Button
// unten rechts. Komplett deaktivierbar: localStorage 'worldid_debug_off' = '1'.
// =====================================================================
const DBG_KEY = 'worldid_debug_log';
function dbgPush(line) {
    try {
        const arr = JSON.parse(localStorage.getItem(DBG_KEY) || '[]');
        arr.push(line);
        while (arr.length > 400) arr.shift();
        localStorage.setItem(DBG_KEY, JSON.stringify(arr));
    } catch (_) { }
}
function dbgRender(line) {
    const box = document.getElementById('worldid-debug-body');
    if (!box) return;
    const div = document.createElement('div');
    div.textContent = line;
    if (line.includes('ERROR')) div.style.color = '#ff6b6b';
    else if (line.includes('WARN')) div.style.color = '#ffd166';
    else div.style.color = '#9be39b';
    box.appendChild(div);
    box.scrollTop = box.scrollHeight;
}
function dbg(msg, level = 'info') {
    let text;
    if (typeof msg === 'string') text = msg;
    else { try { text = JSON.stringify(msg); } catch (_) { text = String(msg); } }
    const t = new Date().toISOString().substr(11, 12);
    const line = `[${t}] ${String(level).toUpperCase()}: ${text}`;
    dbgPush(line);
    dbgRender(line);
}
function dbgInitOverlay() {
    if (localStorage.getItem('worldid_debug_off') === '1') return;
    if (document.getElementById('worldid-debug-panel')) return;
    const btnCss = 'background:#333;color:#fff;border:none;border-radius:4px;padding:2px 8px;font:11px sans-serif';
    const toggle = document.createElement('button');
    toggle.textContent = '🐞';
    toggle.style.cssText = 'position:fixed;right:8px;bottom:8px;z-index:2147483647;background:#222;color:#fff;border:none;border-radius:50%;width:42px;height:42px;font-size:18px;opacity:0.75';
    const panel = document.createElement('div');
    panel.id = 'worldid-debug-panel';
    panel.style.cssText = 'position:fixed;left:0;right:0;bottom:0;height:48vh;background:rgba(0,0,0,0.93);z-index:2147483646;display:none;flex-direction:column';
    panel.innerHTML =
        '<div style="display:flex;gap:6px;align-items:center;padding:6px;background:#111">' +
        '<strong style="color:#fff;flex:1;font:12px sans-serif">World ID Debug</strong>' +
        '<button id="worldid-debug-copy" style="' + btnCss + '">Copy</button>' +
        '<button id="worldid-debug-clear" style="' + btnCss + '">Clear</button>' +
        '<button id="worldid-debug-close" style="' + btnCss + '">Schließen</button>' +
        '</div>' +
        '<div id="worldid-debug-body" style="flex:1;overflow:auto;padding:6px;font:11px monospace;color:#9be39b;white-space:pre-wrap;word-break:break-all"></div>';
    document.body.appendChild(panel);
    document.body.appendChild(toggle);
    toggle.onclick = () => { panel.style.display = (panel.style.display === 'none' ? 'flex' : 'none'); };
    document.getElementById('worldid-debug-close').onclick = () => { panel.style.display = 'none'; };
    document.getElementById('worldid-debug-clear').onclick = () => {
        localStorage.removeItem(DBG_KEY);
        document.getElementById('worldid-debug-body').innerHTML = '';
    };
    document.getElementById('worldid-debug-copy').onclick = () => {
        const txt = (JSON.parse(localStorage.getItem(DBG_KEY) || '[]')).join('\n');
        if (navigator.clipboard) { navigator.clipboard.writeText(txt); }
        const c = document.getElementById('worldid-debug-copy');
        const old = c.textContent; c.textContent = 'kopiert!'; setTimeout(() => c.textContent = old, 1200);
    };
    // Bereits gespeicherte Logs (auch von vor einem Schwarzbild) anzeigen.
    try { (JSON.parse(localStorage.getItem(DBG_KEY) || '[]')).forEach(dbgRender); } catch (_) { }
    // Umgebungs-Diagnose beim Start.
    dbg('=== Overlay bereit ===');
    dbg('URL: ' + location.href);
    dbg('UA: ' + navigator.userAgent);
    dbg('MiniKit geladen: ' + (typeof MiniKit !== 'undefined'));
    try { dbg('MiniKit.isInstalled: ' + (typeof MiniKit !== 'undefined' && typeof MiniKit.isInstalled === 'function' ? MiniKit.isInstalled() : 'n/a')); } catch (e) { dbg('isInstalled Fehler: ' + e.message, 'error'); }
    try { dbg('IDKit geladen: ' + (typeof IDKit !== 'undefined')); } catch (e) { dbg('IDKit Fehler: ' + e.message, 'error'); }
    try { dbg('isInWorldApp(): ' + (typeof isInWorldApp === 'function' ? isInWorldApp() : 'n/a')); } catch (e) { dbg('isInWorldApp Fehler: ' + e.message, 'error'); }
}
// Globale, sonst unsichtbare Fehler einfangen.
window.addEventListener('error', (e) => {
    dbg('window.error: ' + (e.message || (e.error && e.error.message) || 'unbekannt') + ' @ ' + (e.filename || '') + ':' + (e.lineno || ''), 'error');
});
window.addEventListener('unhandledrejection', (e) => {
    const r = e.reason;
    dbg('unhandledRejection: ' + (r && r.message ? r.message : String(r)) + (r && r.stack ? ' | ' + r.stack : ''), 'error');
});
document.addEventListener('DOMContentLoaded', dbgInitOverlay);

function getStoredUserHash() {
    return sessionStorage.getItem("UserToken") || localStorage.getItem("UserToken");
}

function handleLoginAbort() {
    console.warn('Login modal closed before verification. User stays in app and can retry.');
    const el = document.getElementById('login-status');
    if (el) {
        el.innerHTML = '<div style="color:#6c757d">Login abgebrochen. Bitte erneut auf einen Bereich tippen.</div>';
    }
}

async function diagnoseEnvironment() {
    const miniKitExists = typeof MiniKit !== 'undefined';
    const isLocalhost = window.location.hostname === 'localhost' ||
        window.location.hostname === '127.0.0.1';
    return { miniKitExists, isLocalhost };
}

// MiniKit v2 (@worldcoin/minikit-js@2.x): Befehle werden DIREKT top-level aufgerufen
// (await MiniKit.<command>(...)). Die frühere Namespace-API MiniKit.commandsAsync.*
// wurde in v2 ENTFERNT. ACHTUNG: Der World-ID-`verify`-Befehl wurde in v2 ebenfalls
// entfernt und ist nach IDKit (@worldcoin/idkit) umgezogen – ein funktionierendes
// MiniKit.verify gibt es hier also NICHT mehr. walletAuth/pay existieren weiterhin
// top-level (Response-Form: { executedWith, data }).
// Dieser Helper bevorzugt daher die Top-Level-API und fällt nur für alte SDKs auf
// commandsAsync zurück.
async function runMiniKitCommand(name, payload) {
    if (MiniKit && typeof MiniKit[name] === 'function') {
        return await MiniKit[name](payload);
    }
    if (MiniKit && MiniKit.commandsAsync && typeof MiniKit.commandsAsync[name] === 'function') {
        return await MiniKit.commandsAsync[name](payload);
    }
    throw new Error(`MiniKit.${name} ist in dieser SDK-Version nicht verfügbar (verify wurde in v2 nach IDKit verschoben).`);
}

// World-ID-Login über IDKit 4.0 (ersetzt den entfernten MiniKit.verify-Befehl).
// Ablauf: Request-Kontext + signiertes rp_context vom Server holen → IDKit-Request
// bauen → in der World App verifizieren → Result serverseitig prüfen lassen.
async function startLoginProcess() {
    dbg('### LOGIN START ###');
    try {
        log("🌍 World ID Verifizierung wird gestartet...");

        // 0) World-App-Bridge initialisieren. idkit-core nutzt INNERHALB der World App
        //    die MiniKit-Bridge für den Verifizierungs-Flow. Ohne MiniKit.install meldet
        //    die World App "MiniKit-Version wird nicht unterstützt" (Schwarzbild).
        try {
            dbg('Schritt 0: MiniKit.install({appId}) ...');
            MiniKit.install({ appId: APP_ID });
            dbg('Schritt 0: MiniKit.install OK. isInstalled=' + (typeof MiniKit.isInstalled === 'function' ? MiniKit.isInstalled() : 'n/a'));
        } catch (e) {
            dbg('Schritt 0: MiniKit.install Fehler: ' + (e && e.message ? e.message : e), 'warn');
            console.warn("MiniKit.install:", e);
        }

        // 1) Request-Kontext inkl. backend-signiertem rp_context vom Server holen.
        //    (Der RP-Signing-Key liegt nur serverseitig/im Node-Signer.)
        dbg('Schritt 1: hole WorldIdRequestContext ...');
        const ctxResponse = await fetch('/WorldMiniApp/Auth/WorldIdRequestContext', {
            method: 'GET',
            headers: { 'Accept': 'application/json' }
        });
        dbg('Schritt 1: HTTP ' + ctxResponse.status);
        if (!ctxResponse.ok) {
            log("❌ World ID ist nicht konfiguriert oder die Signierung schlug fehl.", true);
            const cb = document.getElementById('consentLoginButton');
            if (cb) cb.disabled = false;
            return;
        }
        const ctx = await ctxResponse.json();
        dbg('Schritt 1: ctx app_id=' + ctx.app_id + ' env=' + ctx.environment
            + ' rp_id=' + (ctx.rp_context && ctx.rp_context.rp_id)
            + ' created_at=' + (ctx.rp_context && ctx.rp_context.created_at)
            + ' expires_at=' + (ctx.rp_context && ctx.rp_context.expires_at)
            + ' hatSignatur=' + !!(ctx.rp_context && ctx.rp_context.signature));

        // 2) IDKit-Request bauen. orbLegacy = Orb-Verifizierung (proof of personhood).
        //    signal kann leer bleiben (am Login ist noch kein Nutzer gebunden).
        log("Bitte World ID bestätigen...");
        // Preset = deviceLegacy: Device-Level-Verifizierung (entspricht dem alten
        // verification_level:'device'). Das hat praktisch jeder World-App-Nutzer.
        // orbLegacy/proofOfHuman würden Orb verlangen und device-only-Nutzer mit
        // "credential_unavailable" aussperren.
        dbg('Schritt 2: IDKit.request(...).preset(deviceLegacy) bauen ...');
        const request = await IDKit.request({
            app_id: ctx.app_id,
            action: ctx.action,
            rp_context: ctx.rp_context,
            allow_legacy_proofs: ctx.allow_legacy_proofs ?? true,
            environment: ctx.environment || 'production'
        }).preset(deviceLegacy({ signal: '' }));
        dbg('Schritt 2: request gebaut. connectorURI=' + (request && request.connectorURI ? String(request.connectorURI).substring(0, 80) : 'KEINE'));

        // 3) Verifizierungs-Flow auslösen.
        //    - INNERHALB der World App: NICHT zur connectorURI navigieren! idkit-core
        //      löst den nativen World-ID-Flow über die World-App-Bridge selbst aus.
        //    - AUSSERHALB der World App (Browser/Desktop): connectorURI als Deep-Link.
        let inWorldApp = false;
        try { inWorldApp = (typeof isInWorldApp === 'function') ? isInWorldApp() : false; } catch (e) { dbg('isInWorldApp Fehler: ' + e.message, 'warn'); }
        dbg('Schritt 3: isInWorldApp=' + inWorldApp);
        if (!inWorldApp && request.connectorURI) {
            dbg('Schritt 3: außerhalb World App -> navigiere zu connectorURI');
            window.location.href = request.connectorURI;
        }

        // 4) Auf Abschluss warten; Result unverändert an den Server zur Verifizierung.
        dbg('Schritt 4: pollUntilCompletion() ... (wartet auf Bestätigung)');
        const result = await request.pollUntilCompletion();
        dbg('Schritt 4: Result erhalten: ' + JSON.stringify(result).substring(0, 200));

        // Bei explizitem Misserfolg NICHT an den Server schicken (sonst lehnt der
        // v4-Verify den "success:false"-Body mit 400 ab und die Meldung ist nichtssagend).
        if (result && result.success === false) {
            const code = result.error || 'unknown';
            dbg('Schritt 4: Verifizierung nicht erfolgreich: ' + code, 'error');
            if (code === 'credential_unavailable') {
                log('Für diese Anmeldung fehlt die passende World-ID-Verifizierung.', true);
            } else if (code === 'user_rejected' || code === 'cancelled' || code === 'canceled') {
                log('Anmeldung abgebrochen.', true);
            } else {
                log('World ID: ' + code, true);
            }
            const cb = document.getElementById('consentLoginButton');
            if (cb) cb.disabled = false;
            return;
        }

        await completeVerify(result);
    } catch (error) {
        dbg('LOGIN FEHLER: ' + (error && error.message ? error.message : error) + (error && error.stack ? ' | STACK: ' + error.stack : ''), 'error');
        console.error("Login Error:", error);
        log(`Fehler: ${error.message || 'Unbekannt'}`, true);
        const cb = document.getElementById('consentLoginButton');
        if (cb) cb.disabled = false;
    }
}

// Schickt das IDKit-Result an den Server (v4-Verify) und schließt den Login ab.
async function completeVerify(result) {
    try {
        log("📤 Prüfe Server...");
        const rememberLogin = getRememberLoginValue();

        // Der eigentliche Proof steckt verschachtelt: { success:true, result:{...} }.
        // An den v4-Verify gehört NUR der innere Proof (protocol_version, nonce,
        // action, responses), NICHT der success/result-Wrapper.
        const proof = (result && result.result) ? result.result : result;
        dbg('completeVerify: Proof pv=' + (proof && proof.protocol_version)
            + ' resp0=' + (proof && proof.responses && proof.responses[0] && proof.responses[0].identifier));

        dbg('completeVerify: POST /VerifyWorldId ...');
        const response = await fetch(`/WorldMiniApp/Auth/VerifyWorldId?rememberLogin=${rememberLogin ? 'true' : 'false'}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            // Nur den inneren Proof weiterleiten ("forward as-is").
            body: JSON.stringify(proof)
        });
        dbg('completeVerify: HTTP ' + response.status);

        if (!response.ok) {
            let backendMessage = '';
            try {
                const errJson = await response.json();
                backendMessage = errJson?.message || '';
                // v4-Detail (Debug) sichtbar machen.
                if (errJson && (errJson.v4status || errJson.v4body)) {
                    dbg('v4 verify -> status=' + errJson.v4status + ' body=' + errJson.v4body, 'error');
                }
            } catch (_) {
                backendMessage = await response.text();
            }
            log(`Anmeldung fehlgeschlagen: ${(backendMessage || 'Unbekannter Fehler').substring(0, 120)}`, true);
            return;
        }

        const data = await response.json();
        log("🎉 Erfolgreich!");
        sessionStorage.setItem("user_verified", "true");

        // Identität liegt jetzt serverseitig im Auth-Cookie. UserToken wird nur noch
        // kosmetisch für bestehenden Frontend-Code gehalten.
        const userHash = data?.userHash || '';
        const storage = rememberLogin ? localStorage : sessionStorage;
        if (userHash) storage.setItem("UserToken", userHash);
        if (!rememberLogin) localStorage.removeItem("UserToken");
        localStorage.setItem(REMEMBER_LOGIN_KEY, rememberLogin ? "true" : "false");

        if (currentConfig.redirectUrl) {
            window.location.href = currentConfig.redirectUrl;
        } else {
            window.location.reload();
        }
    } catch (error) {
        log("❌ Netzwerkfehler", true);
    }
}

function openModal(modalId = 'loginModal') {
    const el = document.getElementById(modalId);
    if (!el) {
        console.error(`[MiniKit] Modal element #${modalId} not found`);
        return;
    }

    if (typeof bootstrap === 'undefined') {
        console.error('[MiniKit] Bootstrap not loaded');
        return;
    }

    try {
        const modal = bootstrap.Offcanvas.getOrCreateInstance(el, { backdrop: true });
        console.log('[MiniKit] Opening login modal');
        modal.show();
    } catch (error) {
        console.error('[MiniKit] Error opening modal:', error);
    }
}

function closeModal(modalId = 'loginModal') {
    const el = document.getElementById(modalId);
    const modal = bootstrap.Offcanvas.getInstance(el);
    if (modal) modal.hide();
}

function getRememberLoginValue() {
    const toggle = document.getElementById('rememberLoginToggle');
    if (toggle) {
        return toggle.checked;
    }
    return localStorage.getItem(REMEMBER_LOGIN_KEY) === "true";
}


async function getServerSessionUserHash() {
    try {
        const response = await fetch('/WorldMiniApp/Auth/SessionStatus', { method: 'GET' });
        if (!response.ok) {
            return null;
        }

        const data = await response.json();
        return data?.isLoggedIn ? (data?.userHash || null) : null;
    } catch (_) {
        return null;
    }
}

async function resolveActiveUserHash() {
    const rememberLogin = getRememberLoginValue();
    const storedHash = getStoredUserHash();
    const serverUserHash = await getServerSessionUserHash();

    if (serverUserHash) {
        if (rememberLogin) {
            localStorage.setItem("UserToken", serverUserHash);
            localStorage.setItem(REMEMBER_LOGIN_KEY, "true");
            sessionStorage.removeItem("UserToken");
        } else {
            sessionStorage.setItem("UserToken", serverUserHash);
            localStorage.removeItem("UserToken");
        }
        sessionStorage.setItem("user_verified", "true");
        return serverUserHash;
    }

    if (storedHash) {
        sessionStorage.removeItem("UserToken");
        localStorage.removeItem("UserToken");
    }

    sessionStorage.removeItem("user_verified");
    return null;
}

function updateStoredLoginInfo(userHash, verifyLevel) {
    const hashEl = document.getElementById('storedUserHash');
    const levelEl = document.getElementById('storedVerifyLevel');
    if (hashEl) {
        hashEl.textContent = userHash ? "gesehen" : "-";
    }
    if (levelEl) {
        levelEl.textContent = verifyLevel ? "gesehen" : "-";
    }
}

window.triggerLogin = async (level, redirectUrl) => {
    console.log(`[MiniKit] Trigger Login: Level=${level}, Ziel=${redirectUrl}`);

    const activeHash = await resolveActiveUserHash();

    // Security: userHash wird nicht mehr als URL-Parameter gesendet.
    // Die Identitaet kommt ausschliesslich aus der serverseitigen Session.
    if (activeHash) {
        console.log('[MiniKit] User already logged in, redirecting');
        window.location.href = redirectUrl;
        return;
    }

    console.log('[MiniKit] No active session, showing login modal');
    currentConfig.level = level;
    currentConfig.redirectUrl = redirectUrl;

    showLoginModalWithFallback();
};

function showLoginModalWithFallback() {
    console.log('[MiniKit] showLoginModalWithFallback called');
    const loginModal = document.getElementById('loginModal');
    if (!loginModal) {
        console.error('[MiniKit] loginModal element not found in DOM');
        return;
    }

    if (loginModal.classList.contains('show')) {
        console.log('[MiniKit] Modal already showing');
        bindConsentButton();
        return;
    }

    if (loginModalOpening) {
        console.log('[MiniKit] Modal already opening');
        bindConsentButton();
        return;
    }

    console.log('[MiniKit] Opening login modal');
    loginModalOpening = true;
    openModal('loginModal');
    bindConsentButton();

    if (loginModalRetryTimer) {
        window.clearTimeout(loginModalRetryTimer);
    }

    loginModalRetryTimer = window.setTimeout(() => {
        loginModalRetryTimer = null;
        loginModalOpening = false;
        if (!loginModal.classList.contains('show')) {
            console.log('[MiniKit] Modal did not open, retrying');
            openModal('loginModal');
            bindConsentButton();
        }
    }, 220);
}

window.retryVerification = async () => {
    const activeHash = await resolveActiveUserHash();
    updateStoredLoginInfo(activeHash, currentConfig.level || 'device');
    if (activeHash) {
        return;
    }
    showLoginModalWithFallback();
};

window.initAutoLogin = async (level) => {
    currentConfig.level = level;
    currentConfig.redirectUrl = "";

    const activeHash = await resolveActiveUserHash();
    updateStoredLoginInfo(activeHash, level);

    if (activeHash) {
        return;
    }

    // Nur Status initialisieren. Login-Modal wird als Fallback erst bei Nutzeraktion geoeffnet.
};

window.getStoredUserHash = getStoredUserHash;

document.addEventListener('DOMContentLoaded', () => {
    const loginModal = document.getElementById('loginModal');
    if (loginModal) {
        loginModal.addEventListener('shown.bs.offcanvas', () => {
            loginModalOpening = false;
            if (loginModalRetryTimer) {
                window.clearTimeout(loginModalRetryTimer);
                loginModalRetryTimer = null;
            }
        });

        loginModal.addEventListener('hidden.bs.offcanvas', () => {
            loginModalOpening = false;
            if (loginModalRetryTimer) {
                window.clearTimeout(loginModalRetryTimer);
                loginModalRetryTimer = null;
            }
            if (sessionStorage.getItem("user_verified") !== "true") {
                handleLoginAbort();
            }
        });
    }
});

function bindConsentButton() {
    const consentButton = document.getElementById('consentLoginButton');
    if (consentButton) {
        consentButton.disabled = false;
        consentButton.onclick = () => {
            consentButton.disabled = true;
            startLoginProcess();
        };
    }
}

// ── Wallet Auth (SIWE) – nur für Marketplace-Käufe ──

async function fetchNonce() {
    const response = await fetch('/WorldMiniApp/Auth/Nonce');
    if (!response.ok) {
        throw new Error('Nonce konnte nicht geladen werden');
    }
    const data = await response.json();
    if (!data?.nonce) {
        throw new Error('Nonce fehlt');
    }

    return data.nonce;
}

function normalizeNonce(rawNonce) {
    const normalized = String(rawNonce || '').replace(/[^a-zA-Z0-9]/g, '');
    if (normalized.length < 8) {
        throw new Error('Nonce ungültig (mind. 8 alphanumerische Zeichen erforderlich).');
    }
    return normalized;
}

// Check MiniKit capabilities
const hasWalletAuth = typeof MiniKit !== 'undefined' && typeof MiniKit.walletAuth === 'function';
const hasSignMessage = typeof MiniKit !== 'undefined' && typeof MiniKit.signMessage === 'function';

async function startWalletAuth() {
    const env = await diagnoseEnvironment();

    if (!env.miniKitExists) {
        throw new Error('MiniKit nicht verfuegbar. Bitte in World App oeffnen.');
    }

    try {
        MiniKit.install({ appId: APP_ID });
    } catch (e) {
        console.warn("Install Note:", e);
    }

    console.log('🟢 [startWalletAuth] Fetching nonce...');
    const nonce = normalizeNonce(await fetchNonce());
    console.log('🟢 [startWalletAuth] Nonce:', nonce);

    console.log('🟢 [startWalletAuth] Calling MiniKit.walletAuth with action: wallet-auth-marketplace');

    let result;
    try {
        // Versuche walletAuth ODER signMessage als Fallback
        if (hasWalletAuth) {
            console.log('🔵 Using MiniKit.walletAuth');
            result = await MiniKit.walletAuth({ nonce });
        } else if (hasSignMessage) {
            console.log('🔵 Using MiniKit.signMessage (Fallback)');
            console.warn('⚠️ walletAuth nicht verfügbar, versuche signMessage...');
            result = await MiniKit.signMessage({
                message: `Wallet verbinden\nNonce: ${nonce}`
            });
        } else {
            throw new Error('Weder walletAuth noch signMessage verfügbar!');
        }
    } catch (error) {
        console.error(`❌ MiniKit Call hat einen Fehler geworfen:`, error);
        throw error;
    }

    console.log('🟢 [startWalletAuth] Raw result:', JSON.stringify(result, null, 2));

    if (!result) {
        alert(`❌ MiniKit.walletAuth() gab kein Ergebnis zurück!\n\nresult ist: ${result}\nTyp: ${typeof result}`);
        throw new Error('MiniKit.walletAuth returned undefined');
    }

    // Neue MiniKit API: result.data enthält die Daten
    console.log('🔍 Result structure (startWalletAuth):', {
        'result.data': result.data,
        'result.executedWith': result.executedWith,
        'full': result
    });
    const payload = result.data || result;

    console.log('🟢 [startWalletAuth] payload:', JSON.stringify(payload, null, 2));

    // Check für Success und completeSiwe aufrufen
    if (payload?.address || result?.address) {
        console.log('✅ [startWalletAuth] Success! Completing SIWE...');
        return await completeSiwe(payload, nonce);
    }

    // Fehler
    const errorDetails = {
        status: payload?.status || result?.status,
        error_code: payload?.error_code || result?.error_code,
        message: payload?.message || result?.message
    };
    console.error('❌ [startWalletAuth] Error:', errorDetails);

    const debugText = `❌ WALLET AUTH DEBUG (startWalletAuth)

Status: ${errorDetails.status || 'undefined'}
Error Code: ${errorDetails.error_code || 'none'}
Message: ${errorDetails.message || 'none'}

--- Full Result ---
${JSON.stringify(result, null, 2)}`;

    alert(debugText);

    throw new Error('WalletAuth fehlgeschlagen: Keine Address im Result');
}

async function completeSiwe(payload, nonce) {
    const response = await fetch('/WorldMiniApp/Auth/CompleteSiwe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ payload, nonce, rememberLogin: false })
    });

    if (!response.ok) {
        let msg = '';
        try {
            const errJson = await response.json();
            msg = errJson?.message || '';
        } catch (_) {
            msg = await response.text();
        }
        throw new Error(msg || 'Wallet-Anmeldung fehlgeschlagen');
    }

    const data = response.ok ? await response.json() : null;
    return data?.walletAddress || payload.address;
}

window.startWalletAuth = startWalletAuth;

// ── Wallet Connect (nur Adresse holen, Login-Session bleibt intakt) ──

async function connectWalletOnly() {
    const env = await diagnoseEnvironment();

    if (!env.miniKitExists) {
        throw new Error('MiniKit nicht verfuegbar. Bitte in World App oeffnen.');
    }

    try {
        MiniKit.install({ appId: APP_ID });
    } catch (e) {
        console.warn("Install Note:", e);
    }

    const nonce = normalizeNonce(await fetchNonce());

    let result;
    try {
        result = await MiniKit.walletAuth({ nonce });
    } catch (error) {
        console.error('walletAuth Fehler:', error);
        throw error;
    }

    const address = result?.address || result?.data?.address;

    if (address) {
        return address;
    }

    console.error('Keine Address im walletAuth Result:', result);
    throw new Error('Wallet-Verbindung fehlgeschlagen: Keine Adresse erhalten');
}

window.connectWalletOnly = connectWalletOnly;

// Debug Helper entfernt für Production

// ── MiniKit Pay (für Marketplace-Käufe) ──

async function startMiniKitPayment({ to, tokenSymbol, amount, reference, description }) {
    const env = await diagnoseEnvironment();

    if (!env.miniKitExists) {
        throw new Error('MiniKit nicht verfuegbar. Bitte in World App oeffnen.');
    }

    try {
        MiniKit.install({ appId: APP_ID });
    } catch (e) {
        console.warn("Install Note:", e);
    }

    // World MiniKit Token-Symbole mappen
    let symbol = (tokenSymbol || 'WLD').toUpperCase();
    if (symbol === 'USDT') symbol = 'USDCE';

    // token_amount muss in der kleinsten Einheit sein (wie wei)
    // WLD = 18 Dezimalen, USDCE = 6 Dezimalen
    const TOKEN_DECIMALS_PAY = { WLD: 18, USDCE: 6 };
    const decimals = TOKEN_DECIMALS_PAY[symbol] || 18;
    const [whole, frac = ''] = String(amount).split('.');
    const paddedFrac = frac.padEnd(decimals, '0').slice(0, decimals);
    const amountSmallestUnit = (BigInt(whole || '0') * (BigInt(10) ** BigInt(decimals)) + BigInt(paddedFrac)).toString();

    const { commandPayload, finalPayload } = await MiniKit.pay({
        reference,
        to,
        tokens: [{
            symbol,
            token_amount: amountSmallestUnit
        }],
        description: description || ''
    });

    if (finalPayload?.status === 'success') {
        return {
            status: 'success',
            transaction_id: finalPayload.transaction_id || finalPayload.txHash || ''
        };
    } else {
        const details = [
            finalPayload?.error_code,
            commandPayload?.error_code,
            finalPayload?.message,
            commandPayload?.message
        ].filter(Boolean).join(' | ') || 'Zahlung abgebrochen.';
        console.error('MiniKit Pay error', { commandPayload, finalPayload });
        throw new Error(`Zahlung fehlgeschlagen: ${details}`);
    }
}

window.startMiniKitPayment = startMiniKitPayment;
