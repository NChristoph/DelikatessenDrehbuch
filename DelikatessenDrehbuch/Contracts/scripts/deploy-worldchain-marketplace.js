const hre = require("hardhat");

function required(name) {
  const value = process.env[name];
  if (!value || !value.trim()) {
    throw new Error(`Missing environment variable: ${name}`);
  }
  return value.trim();
}

async function main() {
  const [deployer] = await hre.ethers.getSigners();
  console.log("Deployer:", deployer.address);

  const treasury = required("TREASURY_ADDRESS");
  const deployMockToken = (process.env.DEPLOY_MOCK_WLD || "false").toLowerCase() === "true";

  let wldTokenAddress = process.env.WLD_TOKEN_ADDRESS?.trim();

  if (deployMockToken) {
    const MockWLD = await hre.ethers.getContractFactory("MockWLD");
    const mockWld = await MockWLD.deploy();
    await mockWld.waitForDeployment();
    wldTokenAddress = await mockWld.getAddress();
    console.log("MockWLD deployed:", wldTokenAddress);

    const faucetAmount = process.env.MOCK_FAUCET_MINT || "1000000";
    const mintTx = await mockWld.faucetMint(hre.ethers.parseUnits(faucetAmount, 18));
    await mintTx.wait();
    console.log(`Minted ${faucetAmount} mWLD to deployer via faucetMint.`);
  }

  if (!wldTokenAddress) {
    throw new Error("Set WLD_TOKEN_ADDRESS or DEPLOY_MOCK_WLD=true");
  }

  const Marketplace = await hre.ethers.getContractFactory("MealPlanMarketplaceWorldChain");
  const marketplace = await Marketplace.deploy(wldTokenAddress, treasury);
  await marketplace.waitForDeployment();

  const marketplaceAddress = await marketplace.getAddress();
  console.log("Marketplace deployed:", marketplaceAddress);

  console.log("--- Copy to appsettings ---");
  console.log(`WorldChain:ChainId=${hre.network.config.chainId}`);
  console.log(`WorldChain:WldTokenAddress=${wldTokenAddress}`);
  console.log(`WorldChain:MarketplaceContractAddress=${marketplaceAddress}`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
