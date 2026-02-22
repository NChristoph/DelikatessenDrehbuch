const WORLD_TESTNET_CHAIN_ID = 480;
const DEFAULT_WORLD_CHAIN_ID = WORLD_TESTNET_CHAIN_ID;

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

const announcedProviders = [];

if (typeof window !== "undefined" && window.addEventListener) {
    window.addEventListener("eip6963:announceProvider", (event) => {
        const detail = event?.detail;
        if (detail?.provider) {
            announcedProviders.push(detail);
        }
    });

    window.dispatchEvent(new Event("eip6963:requestProvider"));
}

function getCfg() {
    const cfg = window.worldChainMarketplaceConfig || {};
    return {
        chainId: Number(cfg.chainId || DEFAULT_WORLD_CHAIN_ID),
        wldTokenAddress: cfg.wldTokenAddress || "",
        usdtTokenAddress: cfg.usdtTokenAddress || "",
        marketplaceAddress: cfg.marketplaceAddress || "",
        testMode: !!cfg.testMode
    };
}

function requireEthers() {
    if (!window.ethers) throw new Error("Ethers ist nicht geladen.");
}

function assertWorldTestnet(chainId) {
    if (Number(chainId) !== WORLD_TESTNET_CHAIN_ID) {
        throw new Error(`Dieses Feature ist aktuell nur auf World Testnet (Chain ${WORLD_TESTNET_CHAIN_ID}) aktiv.`);
    }
}

function isLikelyWorldProvider(provider, info = null) {
    if (!provider) return false;
    const hints = [
        provider?.isWorldApp,
        provider?.isMiniKit,
        info?.name,
        info?.rdns,
        info?.uuid
    ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();

    return hints.includes("world") || hints.includes("mini");
}

function getEthereumProviderCandidates() {
    const candidates = [];

    if (window.world?.ethereum) candidates.push({ provider: window.world.ethereum, source: "window.world.ethereum" });
    if (window.worldEthereum) candidates.push({ provider: window.worldEthereum, source: "window.worldEthereum" });
    if (window.minikit?.ethereum) candidates.push({ provider: window.minikit.ethereum, source: "window.minikit.ethereum" });
    if (window.MiniKit?.ethereum) candidates.push({ provider: window.MiniKit.ethereum, source: "window.MiniKit.ethereum" });

    if (window.ethereum?.providers?.length) {
        for (const provider of window.ethereum.providers) {
            candidates.push({ provider, source: "window.ethereum.providers" });
        }
    }

    if (window.ethereum) candidates.push({ provider: window.ethereum, source: "window.ethereum" });

    for (const announced of announcedProviders) {
        candidates.push({ provider: announced.provider, source: "eip6963", info: announced.info || null });
    }

    const seen = new Set();
    return candidates.filter((item) => {
        if (!item.provider || seen.has(item.provider)) return false;
        seen.add(item.provider);
        return true;
    });
}

function getEthereumProvider() {
    const candidates = getEthereumProviderCandidates();
    if (!candidates.length) return null;

    const preferred = candidates.find((x) => isLikelyWorldProvider(x.provider, x.info));
    return (preferred || candidates[0]).provider;
}

async function waitForEthereumProvider(timeoutMs = 3000) {
    const started = Date.now();
    let provider = getEthereumProvider();

    while (!provider && Date.now() - started < timeoutMs) {
        await new Promise((resolve) => setTimeout(resolve, 200));
        window.dispatchEvent(new Event("eip6963:requestProvider"));
        provider = getEthereumProvider();
    }

    return provider;
}

async function requireWallet() {
    const provider = await waitForEthereumProvider();
    if (!provider) {
        throw new Error("Kein Wallet gefunden. In Debug/Tunnel: World App öffnen, HTTPS-Tunnel-URL verwenden und Seite neu laden.");
    }
    return provider;
}

function getMiniKitWalletAddress() {
    return window.MiniKit?.walletAddress || window.minikit?.walletAddress || "";
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

async function buyListingWithWorldChain({ listingId, price, buyerHash, paymentToken }) {
    const cfg = getCfg();
    const tokenKey = (paymentToken || "WLD").toUpperCase();

    requireEthers();
    await requireWallet();

    const { chainId, marketplaceAddress } = cfg;
    assertWorldTestnet(chainId);

    const tokenAddress = tokenKey === "USDT" ? cfg.usdtTokenAddress : cfg.wldTokenAddress;

    if (!tokenAddress || !marketplaceAddress) {
        throw new Error("Smart-Contract Konfiguration fehlt (Token/Marketplace Adresse).");
    }

    const ethereumProvider = await requireWallet();
    const provider = new window.ethers.BrowserProvider(ethereumProvider);
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

function getAvailableTokens() {
    const cfg = getCfg();
    const tokens = [];
    if (cfg.wldTokenAddress) tokens.push({ key: "WLD", label: "WLD", address: cfg.wldTokenAddress });
    if (cfg.usdtTokenAddress) tokens.push({ key: "USDT", label: "USDT", address: cfg.usdtTokenAddress });
    return tokens;
}

function isTestMode() {
    return getCfg().testMode;
}
//

async function getConnectedWalletAddress() {
    const miniKitWallet = getMiniKitWalletAddress();
    if (miniKitWallet) {
        return miniKitWallet;
    }

    const ethereumProvider = await waitForEthereumProvider();
    if (!ethereumProvider) return "";

    const provider = new window.ethers.BrowserProvider(ethereumProvider);
    const accounts = await provider.send("eth_accounts", []);
    return accounts?.[0] || "";
}

async function connectWallet() {
    const miniKitWallet = getMiniKitWalletAddress();
    if (miniKitWallet) {
        return miniKitWallet;
    }

    requireEthers();
    const ethereumProvider = await requireWallet();
    const provider = new window.ethers.BrowserProvider(ethereumProvider);

    const existingAccounts = await provider.send("eth_accounts", []);
    if (!existingAccounts?.length) {
        await provider.send("eth_requestAccounts", []);
    }

    const cfg = getCfg();
    assertWorldTestnet(cfg.chainId);
    await ensureWorldChain(provider, cfg.chainId);

    const signer = await provider.getSigner();
    return signer.getAddress();
}

window.worldChainMarketplace = {
    buyListingWithWorldChain,
    getAvailableTokens,
    isTestMode,
    getConnectedWalletAddress,
    connectWallet,
    getEthereumProvider,
    getEthereumProviderCandidates,
    waitForEthereumProvider,
    worldTestnetChainId: WORLD_TESTNET_CHAIN_ID
};
