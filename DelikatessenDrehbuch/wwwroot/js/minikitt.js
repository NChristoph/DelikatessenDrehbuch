import { MiniKit, VerificationLevel } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js@1.9.6/+esm";

if (typeof window !== "undefined") {
    window.MiniKit = MiniKit;
}

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const REMEMBER_LOGIN_KEY = "remember_login";
const VERIFY_ACTION = "login";

// === DEIN TEST-SCHALTER ===
// Setze das auf TRUE, um überall (auch am PC) den Mock-Login zu erzwingen.
// Setze das auf FALSE, wenn du live gehst (damit nur World App User reinkommen).
const ALLOW_MOCK_EVERYWHERE = true;

// VerificationLevel Mapping: string -> MiniKit enum
const LEVEL_MAP = {
    'device': VerificationLevel?.Device ?? 'device',
    'orb': VerificationLevel?.Orb ?? 'orb'
};

let currentConfig = {
    level: 'device',
    redirectUrl: ''
};

function log(msg, error = false) {
    console.log(msg);
    const el = document.getElementById('login-status');
    if (el) {
        el.innerHTML = `<div style="color:${error ? 'red' : '#555'}">${msg}</div>`;
    }
}

function getStoredUserHash() {
    return sessionStorage.getItem("UserToken") || localStorage.getItem("UserToken");
}

function handleLoginAbort() {
    window.location.href = 'https://worldcoin.org';
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
            setTimeout(() => {
                closeModal('loginModal');
                handleLoginAbort();
            }, 1500);
        }

        if (typeof MiniKit.isInstalled === 'function' && !MiniKit.isInstalled()) {
            if (ALLOW_MOCK_EVERYWHERE) {
                console.warn("⚠️ World App nicht erkannt, nutze Mock-Login");
                await useMockLogin();
                return;
            }
            log("❌ World App Kontext nicht erkannt. Bitte Mini App direkt in World App öffnen.", true);
            const consentButton = document.getElementById('consentLoginButton');
            if (consentButton) consentButton.disabled = false;
            return;
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

async function verifyBackend(payload) {
    try {
        log("📤 Prüfe Server...");
        const rememberLogin = getRememberLoginValue();
        const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        // verify() gibt NullifierHash zurück — stabil und eindeutig pro User
        const { commandPayload, finalPayload } = await MiniKit.commandsAsync.verify({
            action: VERIFY_ACTION,
            verification_level: verificationLevel
        });

        if (response.ok) {
            log("🎉 Erfolgreich!");
            sessionStorage.setItem("user_verified", "true");
            await new Promise(r => setTimeout(r, 800));
            const storage = rememberLogin ? localStorage : sessionStorage;
            storage.setItem("UserToken", payload.nullifier_hash);
            if (!rememberLogin) {
                localStorage.removeItem("UserToken");
            }
            localStorage.setItem(REMEMBER_LOGIN_KEY, rememberLogin ? "true" : "false");

        if (finalPayload?.status === 'success') {
            await completeVerify(finalPayload);
        } else {
            const details = formatVerifyError(finalPayload || commandPayload);
            const raw = JSON.stringify(finalPayload || commandPayload || {}).substring(0, 300);
            console.error('Verify error payload:', { commandPayload, finalPayload });

            // Bei malformed_request: Fallback auf walletAuth (SIWE)
            const errorCode = finalPayload?.error_code || commandPayload?.error_code || '';
            if (errorCode === 'malformed_request' || errorCode === 'generic_error') {
                log(`Verify fehlgeschlagen (${errorCode}), versuche Wallet-Auth...`);
                console.warn('Verify failed, attempting walletAuth fallback');
                await startWalletAuthProcess();
                return;
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

window.triggerLogin = (level, redirectUrl) => {
    console.log(`Trigger Login: Level=${level}, Ziel=${redirectUrl}`);

    // AUTOMATISCH HOLEN: Wir schauen hier im JS nach dem Token
    const storedHash = getStoredUserHash();
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
    const storedHash = getStoredUserHash();

    currentConfig.level = level;
    currentConfig.redirectUrl = "";
    updateStoredLoginInfo(storedHash, level);

    if (storedHash) {
        sessionStorage.setItem("user_verified", "true");
        return;
    }

    openModal('loginModal');
    bindConsentButton();
};

window.getStoredUserHash = getStoredUserHash;

document.addEventListener('DOMContentLoaded', () => {
    const loginModal = document.getElementById('loginModal');
    if (loginModal) {
        loginModal.addEventListener('hidden.bs.offcanvas', () => {
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
