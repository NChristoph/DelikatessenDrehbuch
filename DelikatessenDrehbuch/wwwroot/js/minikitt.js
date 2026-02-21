import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js@1.1.0/+esm";

if (typeof window !== "undefined") {
    window.MiniKit = MiniKit;
}

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const ACTION = "login-delikatessendrehbuch";

// === TEST-SCHALTER ===
// TRUE = Mock-Login ueberall (auch am PC), FALSE = nur World App
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

// === LOGIN: Immer verify() mit NullifierHash ===
async function startLoginProcess() {
    try {
        const env = await diagnoseEnvironment();

        log(currentConfig.level === 'orb'
            ? "Orb-Verifizierung wird gestartet..."
            : "Device-Verifizierung wird gestartet...");

        await new Promise(r => setTimeout(r, 1000));

        if (!env.miniKitExists) {
            if (env.isLocalhost || ALLOW_MOCK_EVERYWHERE) {
                console.warn("Nutze Mock-Login (Test Modus aktiv)");
                await useMockLogin();
                return;
            }

            log("MiniKit nicht verfuegbar. Bitte in World App oeffnen!", true);
            return;
        }

        try {
            MiniKit.install({ appId: APP_ID });
        } catch (e) { console.warn("Install Note:", e); }

        await new Promise(r => setTimeout(r, 800));
        log("Bitte bestaetigen...");

        const res = await MiniKit.commandsAsync.verify({
            action: ACTION,
            signal: "",
            verification_level: currentConfig.level
        });

        if (res.finalPayload && res.finalPayload.status === 'success') {
            log("Proof erhalten!");
            await verifyBackend(res.finalPayload);
        } else {
            log("Abgebrochen", true);
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

// === MOCK (SIMULATION) ===
async function useMockLogin() {
    log(`Mock Login (${currentConfig.level}) - TEST MODUS`);
    await new Promise(r => setTimeout(r, 1000));

    let fakeHash = localStorage.getItem("mock_nullifier_hash");
    if (!fakeHash) {
        fakeHash = "mock-user-" + Date.now();
        localStorage.setItem("mock_nullifier_hash", fakeHash);
    }

    const mockPayload = {
        status: 'success',
        verification_level: currentConfig.level,
        proof: "mock-proof-" + Date.now(),
        merkle_root: "mock-root",
        nullifier_hash: fakeHash
    };

    await verifyBackend(mockPayload);
}

// === BACKEND VERIFY (NullifierHash) ===
async function verifyBackend(payload) {
    try {
        log("Pruefe Server...");
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
            log("Erfolgreich!");
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
            log(`Server Fehler: ${errorText.substring(0, 50)}`, true);
        }
    } catch (error) {
        log("Netzwerkfehler", true);
    }
}

// === WALLET AUTH: Nur fuer Marketplace (kaufen) ===
async function startWalletAuthProcess(redirectUrl) {
    try {
        log("Wallet-Verbindung wird hergestellt...");

        const nonceResp = await fetch('/WorldMiniApp/Auth/Nonce', { method: 'GET' });
        if (!nonceResp.ok) {
            log("Nonce konnte nicht geladen werden", true);
            return;
        }
        const nonceData = await nonceResp.json();
        const nonce = nonceData.nonce;

        const { commandPayload, finalPayload } = await MiniKit.commandsAsync.walletAuth({
            nonce: nonce,
            statement: 'Sign in to Delikatessen Drehbuch',
            expirationTime: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)
        });

        if (finalPayload?.status === 'success') {
            await completeWalletAuth(finalPayload, nonce, redirectUrl);
        } else {
            log("Wallet-Auth fehlgeschlagen", true);
            console.error('WalletAuth error:', { commandPayload, finalPayload });
        }
    } catch (error) {
        console.error("WalletAuth Error:", error);
        log(`Wallet-Auth Fehler: ${error.message || 'Unbekannt'}`, true);
    }
}

async function completeWalletAuth(payload, nonce, redirectUrl) {
    try {
        log("Wallet wird geprueft...");
        const rememberLogin = getRememberLoginValue();

        const response = await fetch('/WorldMiniApp/Auth/CompleteSiwe', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                payload: {
                    status: payload.status,
                    message: payload.message,
                    signature: payload.signature,
                    address: payload.address,
                    version: payload.version || 1
                },
                nonce: nonce,
                rememberLogin: rememberLogin
            })
        });

        if (!response.ok) {
            let errMsg = '';
            try { errMsg = await response.text(); } catch (_) { }
            log(`Wallet-Auth Server-Fehler: ${(errMsg || 'Unbekannt').substring(0, 120)}`, true);
            return;
        }

        const result = await response.json();
        log("Wallet verbunden!");
        sessionStorage.setItem("user_verified", "true");
        await new Promise(r => setTimeout(r, 600));

        const storage = rememberLogin ? localStorage : sessionStorage;
        storage.setItem("WalletAddress", result.walletAddress);
        if (!rememberLogin) {
            localStorage.removeItem("WalletAddress");
        }

        if (redirectUrl) {
            window.location.href = redirectUrl;
        } else {
            window.location.reload();
        }
    } catch (error) {
        log("Netzwerkfehler bei Wallet-Auth", true);
        console.error("WalletAuth complete error:", error);
    }
}

// === Marketplace: Wallet verbinden nur beim Kaufen ===
window.connectWalletForMarketplace = async (redirectUrl) => {
    await startWalletAuthProcess(redirectUrl || '');
};

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
