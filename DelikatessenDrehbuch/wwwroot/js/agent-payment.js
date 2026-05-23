/**
 * Agent Payment System
 * Ermöglicht es Agents, Zahlungen an User zu senden via MiniKit.pay()
 */

import { MiniKit } from "https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js/+esm";

const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";

/**
 * Konvertiert Token-Betrag in kleinste Einheit (wei)
 * @param {number} amount - Betrag in Token-Einheiten (z.B. 5.0 WLD)
 * @param {number} decimals - Anzahl Dezimalstellen (18 für WLD/WETH, 6 für USDC)
 * @returns {string} Betrag in kleinster Einheit
 */
function toSmallestUnit(amount, decimals = 18) {
    const [whole, frac = ''] = String(amount).split('.');
    const paddedFrac = frac.padEnd(decimals, '0').slice(0, decimals);
    return (BigInt(whole || '0') * (BigInt(10) ** BigInt(decimals)) + BigInt(paddedFrac)).toString();
}

/**
 * Prüft ausstehende Agent-Payments für aktuellen User
 */
async function checkPendingAgentPayments() {
    try {
        const userHash = localStorage.getItem('UserToken') || sessionStorage.getItem('UserToken');
        if (!userHash) {
            console.log('Kein User eingeloggt');
            return [];
        }

        const response = await fetch(`/WorldMiniApp/AgentPayment/GetPendingPayments?userHash=${encodeURIComponent(userHash)}`);

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        const data = await response.json();

        if (data.success && data.count > 0) {
            console.log(`📬 ${data.count} ausstehende Agent-Payments gefunden`);
            return data.payments;
        }

        return [];
    } catch (error) {
        console.error('Fehler beim Abrufen von Pending Payments:', error);
        return [];
    }
}

/**
 * Zeigt Agent-Payment-Notification an
 */
function showAgentPaymentNotification(payment) {
    const existingNotif = document.getElementById('agent-payment-notification');
    if (existingNotif) {
        existingNotif.remove();
    }

    const notif = document.createElement('div');
    notif.id = 'agent-payment-notification';
    notif.className = 'agent-payment-notif';
    notif.innerHTML = `
        <div class="agent-payment-card">
            <div class="agent-payment-header">
                <div class="agent-payment-icon">🤖</div>
                <div class="agent-payment-title">
                    <strong>Agent Payment</strong>
                    <div class="agent-payment-subtitle">${payment.initiatedBy || 'Agent System'}</div>
                </div>
                <button class="agent-payment-close" onclick="document.getElementById('agent-payment-notification').remove()">
                    <i class="bi bi-x-lg"></i>
                </button>
            </div>
            <div class="agent-payment-body">
                <div class="agent-payment-amount">
                    ${payment.amount} ${payment.tokenSymbol}
                </div>
                <div class="agent-payment-desc">
                    ${payment.description || 'Agent Reward'}
                </div>
                <div class="agent-payment-expires">
                    Läuft ab: ${new Date(payment.expiresAt).toLocaleTimeString('de-DE')}
                </div>
            </div>
            <div class="agent-payment-actions">
                <button class="btn-agent-accept" onclick="window.acceptAgentPayment('${payment.reference}')">
                    <i class="bi bi-check-circle-fill"></i> Annehmen
                </button>
                <button class="btn-agent-decline" onclick="window.declineAgentPayment('${payment.reference}')">
                    Ablehnen
                </button>
            </div>
        </div>
    `;

    document.body.appendChild(notif);

    // Auto-hide nach 60 Sekunden
    setTimeout(() => {
        if (document.getElementById('agent-payment-notification')) {
            notif.style.opacity = '0';
            setTimeout(() => notif.remove(), 300);
        }
    }, 60000);
}

/**
 * Agent-Payment annehmen und ausführen
 */
async function acceptAgentPayment(reference) {
    try {
        console.log(`💰 Akzeptiere Agent-Payment: ${reference}`);

        // MiniKit installieren
        try {
            MiniKit.install({ appId: APP_ID });
        } catch (e) {
            console.warn("MiniKit Install Note:", e);
        }

        if (typeof MiniKit.isInstalled === 'function' && !MiniKit.isInstalled()) {
            alert('❌ Bitte in World App öffnen');
            return;
        }

        // Pending Payment abrufen
        const userHash = localStorage.getItem('UserToken') || sessionStorage.getItem('UserToken');
        const pendingResponse = await fetch(`/WorldMiniApp/AgentPayment/GetPendingPayments?userHash=${encodeURIComponent(userHash)}`);
        const pendingData = await pendingResponse.json();

        const payment = pendingData.payments?.find(p => p.reference === reference);
        if (!payment) {
            alert('❌ Payment nicht gefunden');
            return;
        }

        // Token-Decimals
        const TOKEN_DECIMALS = { WLD: 18, WETH: 18, USDC: 6, USDCE: 6 };
        const decimals = TOKEN_DECIMALS[payment.tokenSymbol] || 18;
        const amountSmallestUnit = toSmallestUnit(payment.amount, decimals);

        // Notification schließen
        const notif = document.getElementById('agent-payment-notification');
        if (notif) notif.remove();

        // MiniKit.pay() ausführen
        console.log(`🚀 Führe MiniKit.pay() aus: ${payment.amount} ${payment.tokenSymbol} → ${payment.recipientWalletAddress}`);

        const { commandPayload, finalPayload } = await MiniKit.pay({
            reference: payment.reference,
            to: payment.recipientWalletAddress,
            tokens: [{
                symbol: payment.tokenSymbol === 'USDT' ? 'USDCE' : payment.tokenSymbol,
                token_amount: amountSmallestUnit
            }],
            description: payment.description || '🤖 Agent Payment'
        });

        if (finalPayload?.status === 'success') {
            const txHash = finalPayload.transaction_id || finalPayload.txHash || '';
            console.log(`✅ Payment erfolgreich! TxHash: ${txHash}`);

            // Backend benachrichtigen
            await fetch('/WorldMiniApp/AgentPayment/ConfirmPayment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    reference: payment.reference,
                    transactionHash: txHash,
                    status: 'confirmed'
                })
            });

            // Success-Toast
            showToast(`✅ Payment erhalten: ${payment.amount} ${payment.tokenSymbol}`, 'success');
        } else {
            const error = finalPayload?.error_code || commandPayload?.error_code || 'Unbekannt';
            console.error('❌ Payment fehlgeschlagen:', error);

            // Backend benachrichtigen
            await fetch('/WorldMiniApp/AgentPayment/FailPayment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    reference: payment.reference,
                    status: 'failed'
                })
            });

            showToast(`❌ Payment fehlgeschlagen: ${error}`, 'error');
        }
    } catch (error) {
        console.error('Fehler bei Agent-Payment:', error);
        showToast(`❌ Fehler: ${error.message}`, 'error');
    }
}

/**
 * Agent-Payment ablehnen
 */
async function declineAgentPayment(reference) {
    try {
        console.log(`🚫 Lehne Agent-Payment ab: ${reference}`);

        await fetch('/WorldMiniApp/AgentPayment/FailPayment', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                reference: reference,
                status: 'cancelled'
            })
        });

        const notif = document.getElementById('agent-payment-notification');
        if (notif) notif.remove();

        showToast('Payment abgelehnt', 'info');
    } catch (error) {
        console.error('Fehler beim Ablehnen:', error);
    }
}

/**
 * Toast-Nachricht anzeigen
 */
function showToast(message, type = 'info') {
    const bgColors = {
        success: 'linear-gradient(135deg, #5fa052 0%, #3f7536 100%)',
        error: 'linear-gradient(135deg, #c44e4e 0%, #a63838 100%)',
        info: 'linear-gradient(135deg, #2f6a3a 0%, #1a3a23 100%)'
    };

    const toast = document.createElement('div');
    toast.textContent = message;
    toast.style.cssText = `
        position: fixed;
        bottom: 24px;
        right: 24px;
        background: ${bgColors[type] || bgColors.info};
        color: white;
        padding: 14px 20px;
        border-radius: 12px;
        font-size: 14px;
        font-weight: 600;
        z-index: 9999;
        opacity: 0;
        transition: opacity 0.3s;
        box-shadow: 0 8px 24px rgba(0,0,0,0.3);
        max-width: 320px;
    `;
    document.body.appendChild(toast);

    requestAnimationFrame(() => { toast.style.opacity = '1'; });

    setTimeout(() => {
        toast.style.opacity = '0';
        setTimeout(() => toast.remove(), 300);
    }, 4000);
}

/**
 * Auto-Check für Pending Payments (alle 30 Sekunden)
 */
async function startAgentPaymentWatcher() {
    // Initial Check
    const pending = await checkPendingAgentPayments();
    if (pending.length > 0) {
        showAgentPaymentNotification(pending[0]); // Zeige erstes Pending Payment
    }

    // Wiederkehrender Check alle 30 Sekunden
    setInterval(async () => {
        const pending = await checkPendingAgentPayments();
        if (pending.length > 0 && !document.getElementById('agent-payment-notification')) {
            showAgentPaymentNotification(pending[0]);
        }
    }, 30000);
}

// Global verfügbar machen
window.acceptAgentPayment = acceptAgentPayment;
window.declineAgentPayment = declineAgentPayment;
window.checkPendingAgentPayments = checkPendingAgentPayments;
window.startAgentPaymentWatcher = startAgentPaymentWatcher;

// Auto-Start wenn User eingeloggt
document.addEventListener('DOMContentLoaded', () => {
    const userHash = localStorage.getItem('UserToken') || sessionStorage.getItem('UserToken');
    if (userHash) {
        console.log('🤖 Agent-Payment Watcher gestartet');
        startAgentPaymentWatcher();
    }
});

// CSS für Agent-Payment-Notifications
const style = document.createElement('style');
style.textContent = `
.agent-payment-notif {
    position: fixed;
    bottom: 24px;
    right: 24px;
    z-index: 9998;
    opacity: 1;
    transition: opacity 0.3s;
}

.agent-payment-card {
    background: white;
    border-radius: 18px;
    box-shadow: 0 12px 36px rgba(20,57,31,0.25);
    width: 340px;
    overflow: hidden;
    border: 1px solid rgba(20,57,31,0.08);
}

.agent-payment-header {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 16px;
    background: linear-gradient(135deg, #f5e8b8 0%, #d9c98a 100%);
    border-bottom: 1px solid rgba(20,57,31,0.08);
}

.agent-payment-icon {
    width: 44px;
    height: 44px;
    border-radius: 50%;
    background: white;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 24px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.1);
}

.agent-payment-title {
    flex: 1;
}

.agent-payment-title strong {
    display: block;
    font-family: 'Fraunces', Georgia, serif;
    font-size: 16px;
    font-weight: 700;
    color: #14391f;
}

.agent-payment-subtitle {
    font-size: 11px;
    color: #6b7868;
    margin-top: 2px;
}

.agent-payment-close {
    background: rgba(255,255,255,0.5);
    border: none;
    border-radius: 50%;
    width: 28px;
    height: 28px;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    color: #14391f;
    transition: background 0.2s;
}

.agent-payment-close:hover {
    background: rgba(255,255,255,0.8);
}

.agent-payment-body {
    padding: 18px;
}

.agent-payment-amount {
    font-family: 'Fraunces', Georgia, serif;
    font-size: 28px;
    font-weight: 700;
    color: #14391f;
    margin-bottom: 8px;
}

.agent-payment-desc {
    font-size: 13px;
    color: #6b7868;
    line-height: 1.4;
    margin-bottom: 12px;
}

.agent-payment-expires {
    font-size: 11px;
    color: #9aa295;
    background: #faf6ec;
    padding: 6px 10px;
    border-radius: 8px;
    display: inline-block;
}

.agent-payment-actions {
    display: flex;
    gap: 8px;
    padding: 12px 16px;
    background: #fafafa;
    border-top: 1px solid rgba(20,57,31,0.06);
}

.btn-agent-accept {
    flex: 1;
    background: linear-gradient(135deg, #5fa052 0%, #3f7536 100%);
    color: white;
    border: none;
    border-radius: 12px;
    padding: 12px;
    font-family: 'Inter', sans-serif;
    font-size: 13px;
    font-weight: 700;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    box-shadow: 0 4px 12px rgba(63,117,54,0.25);
    transition: all 0.2s;
}

.btn-agent-accept:hover {
    box-shadow: 0 6px 16px rgba(63,117,54,0.32);
    transform: translateY(-1px);
}

.btn-agent-accept:active {
    transform: scale(0.97);
}

.btn-agent-decline {
    background: white;
    color: #6b7868;
    border: 1px solid rgba(20,57,31,0.12);
    border-radius: 12px;
    padding: 12px 16px;
    font-family: 'Inter', sans-serif;
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
}

.btn-agent-decline:hover {
    background: #fafafa;
    border-color: rgba(20,57,31,0.18);
}

@media (max-width: 480px) {
    .agent-payment-notif {
        left: 12px;
        right: 12px;
        bottom: 12px;
    }

    .agent-payment-card {
        width: 100%;
    }
}
`;
document.head.appendChild(style);
