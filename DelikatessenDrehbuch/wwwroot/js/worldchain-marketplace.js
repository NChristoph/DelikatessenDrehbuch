const DEFAULT_WORLD_CHAIN_ID = 480;
const APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";

// Token-Dezimalstellen
const TOKEN_DECIMALS = { WLD: 18, USDCE: 6 };

// Token-Mapping: Marketplace-UI-Token → MiniKit-Symbol
const TOKEN_TO_MINIKIT = { WLD: "WLD", USDT: "USDCE" };

// Ethers-ABI (Fallback für Nicht-World-App Umgebungen)
const ERC20_ABI = [
    "function approve(address spender, uint256 amount) external returns (bool)",
    "function allowance(address owner, address spender) external view returns (uint256)",
    "function decimals() external view returns (uint8)",
    "function symbol() external view returns (string)"
];

const MARKETPLACE_ABI = [
    "function buyListing(uint256 listingId, uint256 amount, string calldata buyerHash) external",
    "event ListingPurchased(uint256 indexed listingId, address indexed buyer, uint256 amount, string buyerHash)"
];

function getCfg() {
    const cfg = window.worldChainMarketplaceConfig || {};
    return {
        chainId: Number(cfg.chainId || DEFAULT_WORLD_CHAIN_ID),
        wldTokenAddress: cfg.wldTokenAddress || "",
        usdtTokenAddress: cfg.usdtTokenAddress || "",
        marketplaceAddress: cfg.marketplaceAddress || "",
        paymentWalletAddress: cfg.paymentWalletAddress || "",
        testMode: !!cfg.testMode
    };
}

// ===================== MiniKit Integration =====================

// MiniKit dynamisch laden (World App Webview)
let _miniKitCache = null;
let _miniKitLoading = null;

async function getMiniKit() {
    if (_miniKitCache) return _miniKitCache;
    if (_miniKitLoading) return _miniKitLoading;

    _miniKitLoading = (async () => {
        try {
            const mod = await import("https://cdn.jsdelivr.net/npm/@worldcoin/minikit-js@1.1.0/+esm");
            _miniKitCache = mod.MiniKit;
            try { _miniKitCache.install({ appId: APP_ID }); } catch {}
            console.log("[Marketplace] MiniKit geladen");
            return _miniKitCache;
        } catch (e) {
            console.log("[Marketplace] MiniKit nicht verfügbar, Fallback auf Ethers:", e?.message);
            return null;
        }
    })();

    return _miniKitLoading;
}

// Preis in kleinste Token-Einheit umrechnen
function tokenToSmallestUnit(amount, tokenSymbol) {
    const decimals = TOKEN_DECIMALS[tokenSymbol] || 18;
    // Genau auf Dezimalstellen runden um Floating-Point-Fehler zu vermeiden
    const factor = BigInt(10) ** BigInt(decimals);
    const wholePart = BigInt(Math.floor(amount));
    const fracStr = amount.toFixed(decimals).split('.')[1] || '0';
    const fracPart = BigInt(fracStr.padEnd(decimals, '0').slice(0, decimals));
    return (wholePart * factor + fracPart).toString();
}

// ---- MiniKit: Wallet Auth ----
async function connectWalletMiniKit() {
    const mk = await getMiniKit();
    if (!mk) throw new Error("MiniKit nicht verfügbar.");

    const nonce = crypto.randomUUID().replace(/-/g, '');

    const res = await mk.commandsAsync.walletAuth({
        nonce,
        statement: 'Wallet mit Marketplace verbinden'
    });

    const payload = res?.finalPayload || res;

    if (payload?.status !== 'success') {
        throw new Error("Wallet-Verbindung abgebrochen.");
    }

    return payload.address;
}

// ---- MiniKit: Pay (Token-Zahlung über World App UI) ----
async function buyWithMiniKitPay({ listingId, price, paymentToken }) {
    const mk = await getMiniKit();
    if (!mk) throw new Error("MiniKit nicht verfügbar.");

    const cfg = getCfg();
    const payTo = cfg.paymentWalletAddress || cfg.marketplaceAddress;
    if (!payTo) {
        throw new Error("Zahlungsadresse nicht konfiguriert.");
    }

    const reference = `mp-${listingId}-${Date.now()}`;
    const minikitSymbol = TOKEN_TO_MINIKIT[paymentToken] || "WLD";
    const tokenAmount = tokenToSmallestUnit(price, minikitSymbol);

    console.log(`[Marketplace] MiniKit Pay: ${price} ${minikitSymbol} → ${payTo} (ref: ${reference})`);

    const res = await mk.commandsAsync.pay({
        reference,
        to: payTo,
        tokens: [
            {
                symbol: minikitSymbol,
                token_amount: tokenAmount
            }
        ],
        description: `Marketplace Kauf #${listingId}`
    });

    const payload = res?.finalPayload || res;

    if (payload?.status !== 'success') {
        throw new Error("Zahlung abgebrochen oder fehlgeschlagen.");
    }

    return {
        txHash: payload.transaction_id || reference,
        walletAddress: '',
        paymentToken: paymentToken,
        reference: reference,
        transactionId: payload.transaction_id || ''
    };
}

// ===================== Ethers.js Fallback =====================

function requireEthers() {
    if (!window.ethers) throw new Error("Ethers ist nicht geladen.");
}

function requireWallet() {
    if (!window.ethereum) throw new Error("Kein Wallet gefunden. Bitte in der World App öffnen.");
}

async function ensureWorldChain(provider, chainId) {
    const hex = `0x${chainId.toString(16)}`;
    try {
        await provider.send("wallet_switchEthereumChain", [{ chainId: hex }]);
    } catch (error) {
        if (error?.code === 4902) {
            throw new Error("World Chain ist im Wallet noch nicht hinterlegt.");
        }
        throw error;
    }
}

async function ensureAllowance({ signer, owner, wldTokenAddress, marketplaceAddress, amountWei }) {
    const token = new window.ethers.Contract(wldTokenAddress, ERC20_ABI, signer);
    const allowance = await token.allowance(owner, marketplaceAddress);
    if (allowance >= amountWei) return null;

    const tx = await token.approve(marketplaceAddress, amountWei);
    return tx.wait();
}

async function buyWithEthers({ listingId, price, buyerHash, paymentToken }) {
    const cfg = getCfg();
    const tokenKey = (paymentToken || "WLD").toUpperCase();

    requireEthers();
    requireWallet();

    const { chainId, marketplaceAddress } = cfg;
    const tokenAddress = tokenKey === "USDT" ? cfg.usdtTokenAddress : cfg.wldTokenAddress;

    if (!tokenAddress || !marketplaceAddress) {
        throw new Error("Smart-Contract Konfiguration fehlt (Token/Marketplace Adresse).");
    }

    const provider = new window.ethers.BrowserProvider(window.ethereum);
    await provider.send("eth_requestAccounts", []);
    await ensureWorldChain(provider, chainId);

    const signer = await provider.getSigner();
    const owner = await signer.getAddress();

    const token = new window.ethers.Contract(tokenAddress, ERC20_ABI, signer);
    const decimals = await token.decimals();
    const amountWei = window.ethers.parseUnits(String(price), decimals);

    await ensureAllowance({ signer, owner, wldTokenAddress: tokenAddress, marketplaceAddress, amountWei });

    const marketplace = new window.ethers.Contract(marketplaceAddress, MARKETPLACE_ABI, signer);
    const buyTx = await marketplace.buyListing(listingId, amountWei, buyerHash);
    const receipt = await buyTx.wait();

    return {
        txHash: receipt?.hash || buyTx.hash,
        walletAddress: owner,
        paymentToken: tokenKey
    };
}

async function connectWalletEthers() {
    requireEthers();
    requireWallet();

    const provider = new window.ethers.BrowserProvider(window.ethereum);
    await provider.send("eth_requestAccounts", []);
    await ensureWorldChain(provider, getCfg().chainId);

    const signer = await provider.getSigner();
    return signer.getAddress();
}

// ===================== Öffentliche API =====================

// Wallet verbinden: MiniKit zuerst, Ethers-Fallback
async function connectWallet() {
    const mk = await getMiniKit();
    if (mk) {
        console.log("[Marketplace] Wallet verbinden via MiniKit walletAuth");
        return connectWalletMiniKit();
    }

    console.log("[Marketplace] Wallet verbinden via Ethers (Fallback)");
    return connectWalletEthers();
}

// Kauf: MiniKit Pay zuerst, Ethers-Fallback
async function buyListingWithWorldChain({ listingId, price, buyerHash, paymentToken }) {
    const mk = await getMiniKit();
    if (mk) {
        console.log("[Marketplace] Kauf via MiniKit Pay");
        return buyWithMiniKitPay({ listingId, price, paymentToken });
    }

    console.log("[Marketplace] Kauf via Ethers Smart Contract (Fallback)");
    return buyWithEthers({ listingId, price, buyerHash, paymentToken });
}

function getAvailableTokens() {
    const cfg = getCfg();
    const tokens = [];
    if (cfg.wldTokenAddress || cfg.paymentWalletAddress) tokens.push({ key: "WLD", label: "WLD" });
    if (cfg.usdtTokenAddress || cfg.paymentWalletAddress) tokens.push({ key: "USDT", label: "USDT (USDCE)" });
    return tokens;
}

function isTestMode() {
    return getCfg().testMode;
}

async function getConnectedWalletAddress() {
    const mk = await getMiniKit();
    if (mk) return '';

    if (!window.ethereum || !window.ethers) return '';
    const provider = new window.ethers.BrowserProvider(window.ethereum);
    const accounts = await provider.send("eth_accounts", []);
    return accounts?.[0] || "";
}

window.worldChainMarketplace = {
    buyListingWithWorldChain,
    getAvailableTokens,
    isTestMode,
    getConnectedWalletAddress,
    connectWallet,
    getMiniKit
};
