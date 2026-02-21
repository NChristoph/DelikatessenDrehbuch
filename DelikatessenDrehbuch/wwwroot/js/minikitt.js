import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js@1.1.0/+esm";

if (typeof window !== "undefined") {
    window.MiniKit = MiniKit;
}

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const ACTION = "login-delikatessendrehbuch";
const REMEMBER_LOGIN_KEY = "remember_login";

// === DEIN TEST-SCHALTER ===
// Setze das auf TRUE, um überall (auch am PC) den Mock-Login zu erzwingen.
// Setze das auf FALSE, wenn du live gehst (damit nur World App User reinkommen).
const ALLOW_MOCK_EVERYWHERE = true;

let currentConfig = {
    level: 'orb',
    redirectUrl: '/WorldMiniApp/Home/Setup'
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

        if (!env.miniKitExists) {
            if (env.isLocalhost || ALLOW_MOCK_EVERYWHERE) {
                console.warn("⚠️ Nutze Mock-Login (Test Modus aktiv)");
                await useMockLogin();
                return;
            }

            log("❌ MiniKit nicht verfügbar. Bitte in World App öffnen!", true);
            return;
        }

        try {
            MiniKit.install({ appId: APP_ID });
        } catch (e) {
            console.warn("Install Note:", e);
        }

        await new Promise(r => setTimeout(r, 800));
        log("Bitte bestätigen...");

        const res = await MiniKit.commandsAsync.verify({
            action: ACTION,
            signal: "",
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
    } catch (error) {
        console.error("Login Error:", error);
        log(`Fehler: ${error.message || 'Unbekannt'}`, true);
    }
}

async function useMockLogin() {
    log(`🎭 Mock Login (${currentConfig.level}) - TEST MODUS`);
    await new Promise(r => setTimeout(r, 1000));

    const mockPayload = {
        status: 'success',
        verification_level: currentConfig.level,
        proof: "mock-proof-" + Date.now(),
        merkle_root: "mock-root",
        nullifier_hash: "mock-user-" + Date.now()
    };

    await verifyBackend(mockPayload);
}

async function verifyBackend(payload) {
    try {
        log("📤 Prüfe Server...");
        const rememberLogin = getRememberLoginValue();
        const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/WorldMiniApp/Auth/VerifyAction', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(antiForgeryToken ? { 'RequestVerificationToken': antiForgeryToken } : {})
            },
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
            const storage = rememberLogin ? localStorage : sessionStorage;
            storage.setItem("UserToken", payload.nullifier_hash);
            if (!rememberLogin) {
                localStorage.removeItem("UserToken");
            }
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

    const storedHash = getStoredUserHash();
    const rememberLogin = localStorage.getItem(REMEMBER_LOGIN_KEY) === "true";

    if (storedHash && rememberLogin) {
        console.log("Hash automatisch gefunden:", storedHash);
        const separator = redirectUrl.includes('?') ? '&' : '?';
        redirectUrl += `${separator}userHash=${encodeURIComponent(storedHash)}`;
        window.location.href = redirectUrl;
        return;
    }

    if (storedHash) {
        console.log("Hash automatisch gefunden:", storedHash);
        const separator = redirectUrl.includes('?') ? '&' : '?';
        redirectUrl += `${separator}userHash=${encodeURIComponent(storedHash)}`;
    }

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
    updateStoredLoginInfo(storedHash, "-");

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
