import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js/+esm";

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const REMEMBER_LOGIN_KEY = "remember_login";
const VERIFY_ACTION = "login-delikatessendrehbuch";
const PAY_ACTION = "pay-delikatessendrehbuch";

const ALLOW_MOCK_EVERYWHERE = false;

let currentConfig = {
    level: 'device',
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

async function startLoginProcess() {
    try {
        const env = await diagnoseEnvironment();

        log("🌍 World ID Verifizierung wird gestartet...");
        await new Promise(r => setTimeout(r, 600));

        if (!env.miniKitExists) {
            log("MiniKit nicht verfuegbar. Bitte in World App oeffnen!", true);
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

        log("Bitte World ID in der World App bestätigen...");

        const { commandPayload, finalPayload } = await MiniKit.commandsAsync.verify({
            action: VERIFY_ACTION,
            verification_level: 'device'
        });

        if (finalPayload?.status === 'success') {
            await completeVerify(finalPayload);
        } else {
            const errorCode = finalPayload?.error_code || commandPayload?.error_code || '';
            const errorMsg = finalPayload?.message || commandPayload?.message || '';
            const details = errorCode || errorMsg || 'Verifizierung abgebrochen.';
            log(`❌ World ID fehlgeschlagen: ${String(details).substring(0, 160)}`, true);
            console.error('Verify error payload', { commandPayload, finalPayload });
            const consentButton = document.getElementById('consentLoginButton');
            if (consentButton) consentButton.disabled = false;
        }
    } catch (error) {
        console.error("Login Error:", error);
        log(`Fehler: ${error.message || 'Unbekannt'}`, true);
    }
}

async function completeVerify(payload) {
    try {
        log("📤 Prüfe Server...");
        const rememberLogin = getRememberLoginValue();

        const response = await fetch('/WorldMiniApp/Auth/VerifyAction', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                payload,
                action: VERIFY_ACTION,
                signal: '',
                rememberLogin
            })
        });

        if (!response.ok) {
            let backendMessage = '';
            try {
                const errJson = await response.json();
                backendMessage = errJson?.message || '';
            } catch (_) {
                backendMessage = await response.text();
            }
            log(`Anmeldung fehlgeschlagen: ${(backendMessage || 'Unbekannter Fehler').substring(0, 120)}`, true);
            return;
        }

        log("🎉 Erfolgreich!");
        sessionStorage.setItem("user_verified", "true");
        await new Promise(r => setTimeout(r, 600));

        const nullifierHash = payload.nullifier_hash;
        const storage = rememberLogin ? localStorage : sessionStorage;
        storage.setItem("UserToken", nullifierHash);

        if (!rememberLogin) {
            localStorage.removeItem("UserToken");
        }

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
    console.log(`Trigger Login: Level=${level}, Ziel=${redirectUrl}`);

    const activeHash = await resolveActiveUserHash();

    // Security: userHash wird nicht mehr als URL-Parameter gesendet.
    // Die Identitaet kommt ausschliesslich aus der serverseitigen Session.
    if (activeHash) {
        window.location.href = redirectUrl;
        return;
    }

    currentConfig.level = level;
    currentConfig.redirectUrl = redirectUrl;

    showLoginModalWithFallback();
};

function showLoginModalWithFallback() {
    openModal('loginModal');
    bindConsentButton();

    const loginModal = document.getElementById('loginModal');
    if (!loginModal) {
        return;
    }

    window.setTimeout(() => {
        if (!loginModal.classList.contains('show')) {
            openModal('loginModal');
            bindConsentButton();
        }
    }, 180);
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

    const nonce = normalizeNonce(await fetchNonce());

    const { commandPayload, finalPayload } = await MiniKit.commandsAsync.walletAuth({
        nonce
    });

    if (finalPayload?.status === 'success') {
        return await completeSiwe(finalPayload, nonce);
    } else {
        const details = [
            finalPayload?.error_code,
            finalPayload?.errorCode,
            finalPayload?.message,
            finalPayload?.detail
        ].filter(Boolean).join(' | ') || 'WalletAuth abgebrochen oder nicht unterstützt.';
        throw new Error(`WalletAuth fehlgeschlagen: ${details}`);
    }
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

    const { commandPayload, finalPayload } = await MiniKit.commandsAsync.walletAuth({
        nonce
    });

    if (finalPayload?.status === 'success') {
        const address = finalPayload.address;
        if (!address) throw new Error('Keine Wallet-Adresse in der Antwort.');
        return address;
    } else {
        const details = [
            finalPayload?.error_code,
            finalPayload?.message
        ].filter(Boolean).join(' | ') || 'WalletAuth abgebrochen.';
        throw new Error(`Wallet-Verbindung fehlgeschlagen: ${details}`);
    }
}

window.connectWalletOnly = connectWalletOnly;

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

    const { commandPayload, finalPayload } = await MiniKit.commandsAsync.pay({
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
