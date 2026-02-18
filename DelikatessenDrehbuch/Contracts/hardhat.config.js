require("@nomicfoundation/hardhat-toolbox");
require("dotenv").config();

const PRIVATE_KEY = process.env.DEPLOYER_PRIVATE_KEY || "";
const WORLDCHAIN_RPC_URL = process.env.WORLDCHAIN_RPC_URL || "";
const WORLDCHAIN_CHAIN_ID = Number(process.env.WORLDCHAIN_CHAIN_ID || 480);

module.exports = {
  solidity: {
    version: "0.8.24",
    settings: {
      optimizer: {
        enabled: true,
        runs: 200,
      },
    },
  },
  defaultNetwork: "hardhat",
  paths: {
    sources: "./",
    tests: "./test",
    cache: "./cache",
    artifacts: "./artifacts"
  },
  networks: {
    hardhat: {},
    worldchainTestnet: {
      url: WORLDCHAIN_RPC_URL,
      chainId: WORLDCHAIN_CHAIN_ID,
      accounts: PRIVATE_KEY ? [PRIVATE_KEY] : [],
    },
  },
};
