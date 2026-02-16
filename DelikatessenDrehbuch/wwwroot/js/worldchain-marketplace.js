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
        marketplaceAddress: cfg.marketplaceAddress || ""
    };
}

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

async function buyListingWithWorldChain({ listingId, price, buyerHash }) {
    requireEthers();
    requireWallet();

    const { chainId, wldTokenAddress, marketplaceAddress } = getCfg();
    if (!wldTokenAddress || !marketplaceAddress) {
        throw new Error("Smart-Contract Konfiguration fehlt (WLD Token/Marketplace Adresse).");
    }

    const provider = new window.ethers.BrowserProvider(window.ethereum);
    await provider.send("eth_requestAccounts", []);
    await ensureWorldChain(provider, chainId);

    const signer = await provider.getSigner();
    const owner = await signer.getAddress();

    const token = new window.ethers.Contract(wldTokenAddress, ERC20_ABI, signer);
    const decimals = await token.decimals();
    const amountWei = window.ethers.parseUnits(String(price), decimals);

    await ensureAllowance({ signer, owner, wldTokenAddress, marketplaceAddress, amountWei });

    const marketplace = new window.ethers.Contract(marketplaceAddress, MARKETPLACE_ABI, signer);
    const buyTx = await marketplace.buyListing(listingId, amountWei, buyerHash);
    const receipt = await buyTx.wait();

    return {
        txHash: receipt?.hash || buyTx.hash,
        walletAddress: owner
    };
}

window.worldChainMarketplace = {
    buyListingWithWorldChain
};
