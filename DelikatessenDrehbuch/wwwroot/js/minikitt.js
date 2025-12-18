import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js@1.1.0/+esm";

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
const ACTION = "login-delikatessendrehbuch";

// Hier speichern wir temporär, wo der User hin will und wie er sich verifizieren muss
let currentConfig = {
    level: 'orb', // Standard
    redirectUrl: '/WorldMiniApp/Home/Setup'
};

function log(msg, error = false) {
    console.log(msg);
    const el = document.getElementById('login-status');
    if (el) {
        el.innerHTML = `<div style="color:${error ? 'red' : '#555'}">${msg}</div>`;
    }
}

// === DIAGNOSE ===
async function diagnoseEnvironment() {
    const miniKitExists = typeof MiniKit !== 'undefined';
    const isLocalhost = window.location.hostname === 'localhost' ||
        window.location.hostname === '127.0.0.1';
    return { miniKitExists, isLocalhost };
}

// === HAUPT-FUNKTION FÜR DEN LOGIN START ===
async function startLoginProcess() {
    try {
        const env = await diagnoseEnvironment();

        // Modal Text anpassen je nach Level
        log(currentConfig.level === 'orb'
            ? "🔐 Orb-Verifizierung wird gestartet..."
            : "📱 Device-Verifizierung wird gestartet...");

        await new Promise(r => setTimeout(r, 1000));

        // CHECK: MiniKit vorhanden?
        if (!env.miniKitExists) {
            if (env.isLocalhost) {
                await useMockLogin(); // Mock nutzen
                return;
            }
            log("❌ MiniKit nicht verfügbar. Bitte in World App öffnen!", true);
            return;
        }

        // MiniKit vorhanden
        try {
            MiniKit.install({ appId: APP_ID });
        } catch (e) { console.warn("Install Note:", e); }

        await new Promise(r => setTimeout(r, 800));

        log("Bitte bestätigen...");

        // === DYNAMISCHER VERIFY AUFRUF ===
        const res = await MiniKit.commandsAsync.verify({
            action: ACTION,
            signal: "",
            verification_level: currentConfig.level // Hier nutzen wir das dynamische Level!
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
    log(`🎭 Mock Login (${currentConfig.level})`);
    await new Promise(r => setTimeout(r, 1000));

    const mockPayload = {
        status: 'success',
        verification_level: currentConfig.level,
        proof: "mock-proof-" + Date.now(),
        merkle_root: "mock-root",
        nullifier_hash: "mock-hash-" + Date.now()
    };

    await verifyBackend(mockPayload);
}

// === BACKEND VERIFICATION ===
async function verifyBackend(payload) {
    try {
        log("📤 Prüfe Server...");

        const response = await fetch('/WorldMiniApp/Auth/VerifyAction', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                payload,
                action: ACTION,
                signal: ""
            })
        });

        if (response.ok) {
            log("🎉 Erfolgreich!");

            // Optional: Merken, dass wir eingeloggt sind (Session Storage ist hier besser als LocalStorage)
            sessionStorage.setItem("user_verified", "true");

            await new Promise(r => setTimeout(r, 800));

            // WEITERLEITUNG ZUR GEWÜNSCHTEN URL
            window.location.href = currentConfig.redirectUrl;
        } else {
            const errorText = await response.text();
            log(`❌ Server Fehler: ${errorText.substring(0, 50)}`, true);
        }

    } catch (error) {
        log("❌ Netzwerkfehler", true);
    }
}

// === HELPER ===
function openModal() {
    const el = document.getElementById('loginModal');
    if (el) {
        const modal = new bootstrap.Offcanvas(el, { backdrop: true });
        modal.show();
    }
}

function closeModal() {
    const el = document.getElementById('loginModal');
    const modal = bootstrap.Offcanvas.getInstance(el);
    if (modal) modal.hide();
}

// === PUBLIC FUNCTIONS (Vom HTML aufrufbar) ===

// Diese Funktion wird jetzt von deinen Buttons aufgerufen!
window.triggerLogin = (level, redirectUrl) => {
    console.log(`Trigger Login: Level=${level}, Ziel=${redirectUrl}`);

    // Config setzen
    currentConfig.level = level;
    currentConfig.redirectUrl = redirectUrl;

    // Modal öffnen & Starten
    openModal();

    // Kleines Delay für die Animation
    setTimeout(startLoginProcess, 500);
};

window.retryVerification = () => {
    // Einfach mit den letzten Werten nochmal starten
    openModal();
    setTimeout(startLoginProcess, 500);
};