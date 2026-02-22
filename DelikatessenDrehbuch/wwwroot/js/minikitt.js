import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js/+esm";

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const REMEMBER_LOGIN_KEY = "remember_login";
const VERIFY_ACTION = "login-delikatessendrehbuch";
const PAY_ACTION = "pay";

const ALLOW_MOCK_EVERYWHERE = true;

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

        log("🌍 World ID Verifizierung wird gestartet...");
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

async function useMockLogin() {
    log("🎭 Mock World ID Login - TEST MODUS");
    await new Promise(r => setTimeout(r, 600));

    let fakeNullifierHash = localStorage.getItem("mock_nullifier_hash");
    if (!fakeNullifierHash) {
        fakeNullifierHash = `0x${Date.now().toString(16).padStart(64, '0')}`;
        localStorage.setItem("mock_nullifier_hash", fakeNullifierHash);
    }

    const mockPayload = {
        status: 'success',
        proof: `mock-${Date.now()}`,
        merkle_root: `0x${'0'.repeat(64)}`,
        nullifier_hash: fakeNullifierHash,
        verification_level: 'device'
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
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                payload,
                action: VERIFY_ACTION,
                signal: '',
                rememberLogin
            })
        });

        if (!response.ok && !isMock) {
            let backendMessage = '';
            try {
                const errJson = await response.json();
                backendMessage = errJson?.message || '';
            } catch (_) {
                backendMessage = await response.text();
            }
            log(`❌ Anmeldung fehlgeschlagen: ${(backendMessage || 'Unbekannter Fehler').substring(0, 120)}`, true);
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
        if (env.isLocalhost || ALLOW_MOCK_EVERYWHERE) {
            console.warn("⚠️ Mock Wallet Auth (Test Modus)");
            let fakeWallet = localStorage.getItem("mock_wallet_address");
            if (!fakeWallet) {
                fakeWallet = `0x${Date.now().toString(16).padEnd(40, '0').slice(0, 40)}`;
                localStorage.setItem("mock_wallet_address", fakeWallet);
            }
            const nonce = await fetchNonce();
            const mockPayload = {
                status: 'success',
                message: `mock-siwe-message-${Date.now()}`,
                signature: `mock-signature-${Date.now()}`,
                address: fakeWallet,
                version: 1
            };
            return await completeSiwe(mockPayload, nonce, true);
        }
        throw new Error('MiniKit nicht verfügbar. Bitte in World App öffnen.');
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

async function completeSiwe(payload, nonce, isMock = false) {
    const response = await fetch('/WorldMiniApp/Auth/CompleteSiwe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ payload, nonce, rememberLogin: false })
    });

    if (!response.ok && !isMock) {
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
        if (env.isLocalhost || ALLOW_MOCK_EVERYWHERE) {
            let fakeWallet = localStorage.getItem("mock_wallet_address");
            if (!fakeWallet) {
                fakeWallet = `0x${Date.now().toString(16).padEnd(40, '0').slice(0, 40)}`;
                localStorage.setItem("mock_wallet_address", fakeWallet);
            }
            return fakeWallet;
        }
        throw new Error('MiniKit nicht verfügbar. Bitte in World App öffnen.');
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
