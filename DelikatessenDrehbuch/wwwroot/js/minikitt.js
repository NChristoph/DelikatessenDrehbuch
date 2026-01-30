import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js@1.1.0/+esm";

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const ACTION = "login-delikatessendrehbuch";

// === DEIN TEST-SCHALTER ===
// Setze das auf TRUE, um überall (auch am PC) den Mock-Login zu erzwingen.
// Setze das auf FALSE, wenn du live gehst (damit nur World App User reinkommen).
const ALLOW_MOCK_EVERYWHERE = true;

let currentConfig = {
    level: 'orb',
    redirectUrl: '/WorldMiniApp/Home/Setup'
};
const REMEMBER_LOGIN_KEY = "remember_login";

function log(msg, error = false) {
    console.log(msg);
    const el = document.getElementById('login-status');
    if (el) {
        el.innerHTML = `<div style="color:${error ? 'red' : '#555'}">${msg}</div>`;
    }
}

async function diagnoseEnvironment() {
    const miniKitExists = typeof MiniKit !== 'undefined';
    // Wir prüfen auch auf 127.0.0.1 und localhost
    const isLocalhost = window.location.hostname === 'localhost' ||
        window.location.hostname === '127.0.0.1';
    return { miniKitExists, isLocalhost };
}

async function startLoginProcess() {
    try {
        const env = await diagnoseEnvironment();

        log(currentConfig.level === 'orb'
            ? "🔐 Orb-Verifizierung wird gestartet..."
            : "📱 Device-Verifizierung wird gestartet...");

        await new Promise(r => setTimeout(r, 1000));

        // CHECK: MiniKit vorhanden?
        if (!env.miniKitExists) {

            // === HIER IST DIE ÄNDERUNG ===
            // Wenn wir auf Localhost sind ODER der Test-Schalter an ist:
            if (env.isLocalhost || ALLOW_MOCK_EVERYWHERE) {
                console.warn("⚠️ Nutze Mock-Login (Test Modus aktiv)");
                await useMockLogin(); // Fake Login starten
                return;
            }

            log("❌ MiniKit nicht verfügbar. Bitte in World App öffnen!", true);
            return;
        }

        // ... Ab hier läuft der echte World App Login weiter ...
        try {
            MiniKit.install({ appId: APP_ID });
        } catch (e) { console.warn("Install Note:", e); }

        await new Promise(r => setTimeout(r, 800));
        log("Bitte bestätigen...");

        const res = await MiniKit.commandsAsync.verify({
            action: ACTION,
            signal: "", // WICHTIG: Dein Backend ersetzt das leere Signal automatisch durch den Hash
            verification_level: currentConfig.level
        });

        if (res.finalPayload && res.finalPayload.status === 'success') {
            log("✅ Proof erhalten!");
            await verifyBackend(res.finalPayload);
        } else {
            log("❌ Abgebrochen", true);
            setTimeout(closeModal, 1500);
        }

    } catch (error) {
        console.error("Login Error:", error);
        log(`Fehler: ${error.message || 'Unbekannt'}`, true);
    }
}

// === MOCK (SIMULATION) ===
async function useMockLogin() {
    log(`🎭 Mock Login (${currentConfig.level}) - TEST MODUS`);
    await new Promise(r => setTimeout(r, 1000));

    // Wir generieren Fake-Daten, damit das Backend zufrieden ist
    const mockPayload = {
        status: 'success',
        verification_level: currentConfig.level,
        proof: "mock-proof-" + Date.now(),
        merkle_root: "mock-root",
        nullifier_hash: "mock-user-" + Date.now() // Jedes Mal ein neuer Fake-User
    };

    await verifyBackend(mockPayload);
}

// ... (Der Rest bleibt gleich: verifyBackend, openModal, closeModal, triggerLogin) ...

async function verifyBackend(payload) {
    try {
        log("📤 Prüfe Server...");
        const rememberLogin = getRememberLoginValue();

        const response = await fetch('/WorldMiniApp/Auth/VerifyAction', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                payload,
                action: ACTION,
                signal: "",
                rememberLogin: rememberLogin
            })
        });

        if (response.ok) {
            log("🎉 Erfolgreich!");
            sessionStorage.setItem("user_verified", "true");
            await new Promise(r => setTimeout(r, 800));
            localStorage.setItem("UserToken", payload.nullifier_hash);
            localStorage.setItem(REMEMBER_LOGIN_KEY, rememberLogin ? "true" : "false");

            if (currentConfig.redirectUrl) {
                window.location.href = currentConfig.redirectUrl;
            } else {
                closeModal();
            }
        } else {
            const errorText = await response.text();
            log(`❌ Server Fehler: ${errorText.substring(0, 50)}`, true);
        }
    } catch (error) {
        log("❌ Netzwerkfehler", true);
    }
}

function openModal(modalId = 'loginModal') {
    const el = document.getElementById(modalId);
    if (el) {
        const modal = new bootstrap.Offcanvas(el, { backdrop: true });
        modal.show();
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

async function refreshRememberedLogin(userHash) {
    try {
        const response = await fetch('/WorldMiniApp/Auth/RefreshStatus', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                userHash: userHash,
                rememberLogin: true
            })
        });
        if (response.ok) {
            const data = await response.json();
            updateStoredLoginInfo(userHash, data.status);
        }
        sessionStorage.setItem("user_verified", "true");
    } catch (error) {
        console.warn("RefreshStatus failed", error);
    }
}


window.triggerLogin = (level, redirectUrl) => {
    console.log(`Trigger Login: Level=${level}, Ziel=${redirectUrl}`);

    // AUTOMATISCH HOLEN: Wir schauen hier im JS nach dem Token
    const storedHash = localStorage.getItem("UserToken");
    const rememberLogin = localStorage.getItem(REMEMBER_LOGIN_KEY) === "true";

    if (storedHash && rememberLogin) {
        console.log("Hash automatisch gefunden:", storedHash);
        // URL erweitern
        const separator = redirectUrl.includes('?') ? '&' : '?';
        redirectUrl += `${separator}userHash=${encodeURIComponent(storedHash)}`;
        window.location.href = redirectUrl;
        return;
    }

    if (storedHash) {
        console.log("Hash automatisch gefunden:", storedHash);
        // URL erweitern
        const separator = redirectUrl.includes('?') ? '&' : '?';
        redirectUrl += `${separator}userHash=${encodeURIComponent(storedHash)}`;
    }

    // Config setzen
    currentConfig.level = level;
    currentConfig.redirectUrl = redirectUrl;

    openModal('loginModal');
    bindConsentButton();
};

window.retryVerification = () => {
    openModal('loginModal');
    bindConsentButton();
};

window.initAutoLogin = (level) => {
    const storedHash = localStorage.getItem("UserToken");
    const rememberLogin = localStorage.getItem(REMEMBER_LOGIN_KEY) === "true";

    currentConfig.level = level;
    currentConfig.redirectUrl = "";
    openModal('loginModal');
    updateStoredLoginInfo(storedHash, "-");

    if (storedHash) {
        refreshRememberedLogin(storedHash);
    }

    bindConsentButton();
};

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
