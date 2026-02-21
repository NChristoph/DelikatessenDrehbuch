import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js/+esm";

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const REMEMBER_LOGIN_KEY = "remember_login";
const VERIFY_ACTION = "login";

const ALLOW_MOCK_EVERYWHERE = true;

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
    const isLocalhost = window.location.hostname === 'localhost' ||
        window.location.hostname === '127.0.0.1';
    return { miniKitExists, isLocalhost };
}

function formatVerifyError(payload) {
    if (!payload) return 'Verify ohne Antwort.';

    const candidates = [
        payload?.error_code,
        payload?.errorCode,
        payload?.message,
        payload?.detail,
        payload?.description,
        payload?.status
    ].filter(Boolean);

    return candidates.length ? candidates.join(' | ') : 'Verifizierung abgebrochen oder nicht unterstützt.';
}

async function startLoginProcess() {
    try {
        const env = await diagnoseEnvironment();

        log("🔐 World ID Verifizierung wird gestartet...");
        await new Promise(r => setTimeout(r, 600));

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

        const verificationLevel = currentConfig.level || 'device';
        log(`Bitte World ID bestätigen (${verificationLevel})...`);

        // verify() gibt NullifierHash zurück — stabil und eindeutig pro User
        const { commandPayload, finalPayload } = await MiniKit.commandsAsync.verify({
            action: VERIFY_ACTION,
            verification_level: verificationLevel
        });

        if (finalPayload?.status === 'success') {
            await completeVerify(finalPayload);
        } else {
            const details = formatVerifyError(finalPayload || commandPayload);
            const raw = JSON.stringify(finalPayload || commandPayload || {}).substring(0, 220);
            log(`❌ Verifizierung fehlgeschlagen: ${details.substring(0, 120)} | ${raw}`, true);
            console.error('Verify error payload', { commandPayload, finalPayload });
            const consentButton = document.getElementById('consentLoginButton');
            if (consentButton) consentButton.disabled = false;
            return;
        }
    } catch (error) {
        console.error("Login Error:", error);
        log(`Fehler: ${error.message || 'Unbekannt'}`, true);
    }
}

async function useMockLogin() {
    log(`🎭 Mock Login - TEST MODUS`);
    await new Promise(r => setTimeout(r, 600));

    // Stabiler Mock-NullifierHash: einmal generieren, dann wiederverwenden
    let fakeHash = localStorage.getItem("mock_nullifier_hash");
    if (!fakeHash) {
        fakeHash = `0x${Date.now().toString(16).padEnd(64, 'a').slice(0, 64)}`;
        localStorage.setItem("mock_nullifier_hash", fakeHash);
    }

    const mockPayload = {
        status: 'success',
        proof: `mock-proof-${Date.now()}`,
        merkle_root: `mock-root-${Date.now()}`,
        nullifier_hash: fakeHash,
        verification_level: currentConfig.level || 'device'
    };

    await completeVerify(mockPayload, true);
}

async function completeVerify(payload, isMock = false) {
    try {
        log("📤 Prüfe Server...");
        const rememberLogin = getRememberLoginValue();

        const response = await fetch('/WorldMiniApp/Auth/VerifyAction', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify({
                payload: payload,
                action: VERIFY_ACTION,
                signal: '',
                rememberLogin
            })
        });

        if (!response.ok && !isMock) {
            let backendMessage = '';
            try {
                const errText = await response.text();
                backendMessage = errText || '';
            } catch (_) { }

            log(`❌ Anmeldung fehlgeschlagen: ${(backendMessage || 'Unbekannter Fehler').substring(0, 120)}`, true);
            const consentButton = document.getElementById('consentLoginButton');
            if (consentButton) consentButton.disabled = false;
            return;
        }

        // NullifierHash als UserToken speichern — das ist der stabile Identifier
        const userHash = payload.nullifier_hash;

        log("🎉 Erfolgreich!");
        sessionStorage.setItem("user_verified", "true");
        await new Promise(r => setTimeout(r, 600));

        const storage = rememberLogin ? localStorage : sessionStorage;
        storage.setItem("UserToken", userHash);

        if (!rememberLogin) {
            localStorage.removeItem("UserToken");
        }

        localStorage.setItem(REMEMBER_LOGIN_KEY, rememberLogin ? "true" : "false");

        if (currentConfig.redirectUrl) {
            window.location.href = currentConfig.redirectUrl;
        } else {
            // Seite neu laden damit Links mit userHash aktualisiert werden
            window.location.reload();
        }
    } catch (error) {
        log("❌ Netzwerkfehler", true);
    }
}

function getAntiForgeryToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
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

    const storedHash = getStoredUserHash();
    const rememberLogin = localStorage.getItem(REMEMBER_LOGIN_KEY) === "true";

    if (storedHash && rememberLogin) {
        const separator = redirectUrl.includes('?') ? '&' : '?';
        redirectUrl += `${separator}userHash=${encodeURIComponent(storedHash)}`;
        window.location.href = redirectUrl;
        return;
    }

    if (storedHash) {
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
