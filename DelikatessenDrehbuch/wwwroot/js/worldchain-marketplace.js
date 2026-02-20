const DEFAULT_WORLD_CHAIN_ID = 480;

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
        testMode: !!cfg.testMode
    };
}

function requireEthers() {
    if (!window.ethers) throw new Error("Ethers ist nicht geladen.");
}

function getEthereumProvider() {
    return window.ethereum
        || window.world?.ethereum
        || window.worldEthereum
        || window.minikit?.ethereum
        || window.MiniKit?.ethereum
        || null;
}

function requireWallet() {
    const provider = getEthereumProvider();
    if (!provider) {
        throw new Error("Kein Wallet gefunden. Öffne den Marktplatz direkt in der World App und aktualisiere die Seite.");
    }
    return provider;
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
    requireWallet();

    const { chainId, marketplaceAddress } = cfg;

    // Token-Adresse je nach Zahlungsmittel wählen
    const tokenAddress = tokenKey === "USDT" ? cfg.usdtTokenAddress : cfg.wldTokenAddress;

    if (!tokenAddress || !marketplaceAddress) {
        throw new Error("Smart-Contract Konfiguration fehlt (Token/Marketplace Adresse).");
    }

    const ethereumProvider = requireWallet();
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

async function getConnectedWalletAddress() {
    requireWallet();
    const ethereumProvider = requireWallet();
    const provider = new window.ethers.BrowserProvider(ethereumProvider);
    const accounts = await provider.send("eth_accounts", []);
    return accounts?.[0] || "";
}

async function connectWallet() {
    requireEthers();
    requireWallet();

    const ethereumProvider = requireWallet();
    const provider = new window.ethers.BrowserProvider(ethereumProvider);
    await provider.send("eth_requestAccounts", []);
    await ensureWorldChain(provider, getCfg().chainId);

    const signer = await provider.getSigner();
    return signer.getAddress();
}

window.worldChainMarketplace = {
    buyListingWithWorldChain,
    getAvailableTokens,
    isTestMode,
    getConnectedWalletAddress,
    connectWallet,
    getEthereumProvider
};
