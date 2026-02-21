import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js/+esm";

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const REMEMBER_LOGIN_KEY = "remember_login";

const ALLOW_MOCK_EVERYWHERE = true;

let currentConfig = {
    level: 'wallet',
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


function formatWalletAuthError(payload) {
    if (!payload) return 'WalletAuth ohne Antwort.';

    const candidates = [
        payload?.error_code,
        payload?.errorCode,
        payload?.message,
        payload?.detail,
        payload?.description,
        payload?.status
    ].filter(Boolean);

    return candidates.length ? candidates.join(' | ') : 'WalletAuth abgebrochen oder nicht unterstützt.';
}

async function startLoginProcess() {
    try {
        const env = await diagnoseEnvironment();

        log("🔐 Wallet-Authentifizierung wird gestartet...");
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
            log("❌ World App Kontext nicht erkannt. Bitte Mini App direkt in World App öffnen.", true);
            const consentButton = document.getElementById('consentLoginButton');
            if (consentButton) consentButton.disabled = false;
            return;
        }

        const nonce = normalizeNonce(await fetchNonce());

        log(`Bitte Wallet-Signatur bestätigen... (nonce:${nonce.length})`);

        const { commandPayload, finalPayload } = await MiniKit.commandsAsync.walletAuth({
            nonce
        });

        if (finalPayload?.status === 'success') {
            await completeSiwe(finalPayload, nonce);
        } else {
            const details = formatWalletAuthError(finalPayload || commandPayload);
            const raw = JSON.stringify(finalPayload || commandPayload || {}).substring(0, 220);
            log(`❌ WalletAuth fehlgeschlagen: ${details.substring(0, 120)} | ${raw}`, true);
            console.error('WalletAuth error payload', { commandPayload, finalPayload });
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
    log(`🎭 Mock Wallet Login - TEST MODUS`);
    await new Promise(r => setTimeout(r, 600));

    const fakeWallet = `0x${Date.now().toString(16).padEnd(40, '0').slice(0, 40)}`;
    const mockPayload = {
        status: 'success',
        message: `mock-siwe-message-${Date.now()}`,
        signature: `mock-signature-${Date.now()}`,
        address: fakeWallet,
        version: 1
    };

    const nonce = await fetchNonce();
    await completeSiwe(mockPayload, nonce, true);
}

async function completeSiwe(payload, nonce, isMock = false) {
    try {
        log("📤 Prüfe Server...");
        const rememberLogin = getRememberLoginValue();

        const response = await fetch('/WorldMiniApp/Auth/CompleteSiwe', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                payload,
                nonce,
                rememberLogin
            })
        });

        if (!response.ok && !isMock) {
            let backendMessage = '';
            try {
                const errJson = await response.json();
                backendMessage = errJson?.message || '';
            } catch (_) {
                const errorText = await response.text();
                backendMessage = errorText || '';
            }

            log(`❌ Anmeldung fehlgeschlagen: ${(backendMessage || 'Unbekannter Fehler').substring(0, 120)}`, true);
            return;
        }

        const data = response.ok ? await response.json() : null;
        const walletAddress = data?.walletAddress || payload.address;

        log("🎉 Erfolgreich!");
        sessionStorage.setItem("user_verified", "true");
        await new Promise(r => setTimeout(r, 600));

        const storage = rememberLogin ? localStorage : sessionStorage;
        storage.setItem("UserToken", walletAddress.toLowerCase());

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
    updateStoredLoginInfo(storedHash, "wallet");

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
