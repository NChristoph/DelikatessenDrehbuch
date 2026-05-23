/**
 * Token Recovery Script
 * Sendet WLD + WETH vom Agent Wallet zurück zur World App
 */

import { createWalletClient, createPublicClient, http, parseUnits, encodeFunctionData, type Address } from 'viem';
import { privateKeyToAccount } from 'viem/accounts';
import * as dotenv from 'dotenv';

dotenv.config();

// World Chain Configuration (mit WLD als native Gas)
const worldChain = {
  id: 480,
  name: 'World Chain',
  network: 'worldchain',
  nativeCurrency: {
    decimals: 18,
    name: 'Worldcoin',
    symbol: 'WLD',
  },
  rpcUrls: {
    public: { http: ['https://worldchain-mainnet.g.alchemy.com/public'] },
    default: { http: ['https://worldchain-mainnet.g.alchemy.com/public'] },
  },
  blockExplorers: {
    default: { name: 'World Scan', url: 'https://worldscan.org' },
  },
};

// Token Adressen
const WLD_TOKEN = '0x2cFc85d8E48F8EAB294be644d9E25C3030863003' as Address;
const WETH_TOKEN = '0x4200000000000000000000000000000000000006' as Address;

// ERC-20 Transfer ABI
const ERC20_ABI = [{
  name: 'transfer',
  type: 'function',
  stateMutability: 'nonpayable',
  inputs: [
    { name: 'to', type: 'address' },
    { name: 'amount', type: 'uint256' }
  ],
  outputs: [{ type: 'bool' }]
}, {
  name: 'balanceOf',
  type: 'function',
  stateMutability: 'view',
  inputs: [{ name: 'account', type: 'address' }],
  outputs: [{ type: 'uint256' }]
}];

async function recoverTokens() {
  try {
    console.log('🔧 Token Recovery Script gestartet...\n');

    // 1. Konfiguration prüfen
    const agentPrivateKey = process.env.AGENT_PRIVATE_KEY;
    const destinationAddress = process.env.DESTINATION_ADDRESS as Address;

    if (!agentPrivateKey) {
      throw new Error('❌ AGENT_PRIVATE_KEY fehlt in .env!');
    }

    if (!destinationAddress) {
      throw new Error('❌ DESTINATION_ADDRESS fehlt in .env! (Deine World App Adresse)');
    }

    console.log(`📍 Ziel-Adresse (World App): ${destinationAddress}\n`);

    // 2. Wallet & Clients erstellen
    const account = privateKeyToAccount(`0x${agentPrivateKey.replace('0x', '')}` as Address);

    const publicClient = createPublicClient({
      chain: worldChain,
      transport: http('https://worldchain-mainnet.g.alchemy.com/public')
    });

    const walletClient = createWalletClient({
      account,
      chain: worldChain,
      transport: http('https://worldchain-mainnet.g.alchemy.com/public')
    });

    console.log(`👛 Agent Wallet: ${account.address}`);

    // 3. Balances checken
    const nativeBalance = await publicClient.getBalance({ address: account.address });
    console.log(`💰 Native WLD Balance: ${Number(nativeBalance) / 1e18} WLD`);

    if (nativeBalance === 0n) {
      console.log('\n⚠️  WARNUNG: 0 native WLD für Gas!');
      console.log('   Optionen:');
      console.log('   1. Sende 0.1 WLD von World App als Gas');
      console.log('   2. Verwende World ID Paymaster (falls verifiziert)');
      console.log('\n❌ Script beendet. Bitte Gas hinzufügen und erneut ausführen.\n');
      return;
    }

    // WLD Token Balance (ERC-20)
    const wldBalanceData = encodeFunctionData({
      abi: ERC20_ABI,
      functionName: 'balanceOf',
      args: [account.address]
    });

    const wldBalanceResult = await publicClient.call({
      to: WLD_TOKEN,
      data: wldBalanceData
    });

    const wldBalance = wldBalanceResult.data ? BigInt(wldBalanceResult.data) : 0n;
    console.log(`🪙 WLD Token Balance: ${Number(wldBalance) / 1e18} WLD`);

    // WETH Token Balance
    const wethBalanceData = encodeFunctionData({
      abi: ERC20_ABI,
      functionName: 'balanceOf',
      args: [account.address]
    });

    const wethBalanceResult = await publicClient.call({
      to: WETH_TOKEN,
      data: wethBalanceData
    });

    const wethBalance = wethBalanceResult.data ? BigInt(wethBalanceResult.data) : 0n;
    console.log(`🪙 WETH Token Balance: ${Number(wethBalance) / 1e18} WETH\n`);

    // 4. WLD Tokens senden
    if (wldBalance > 0n) {
      console.log(`🔄 Sende ${Number(wldBalance) / 1e18} WLD Token...`);

      const wldTransferData = encodeFunctionData({
        abi: ERC20_ABI,
        functionName: 'transfer',
        args: [destinationAddress, wldBalance]
      });

      const wldTxHash = await walletClient.sendTransaction({
        to: WLD_TOKEN,
        data: wldTransferData,
        gas: 100000n
      });

      console.log(`✅ WLD Transfer TX: ${wldTxHash}`);
      console.log(`   https://worldscan.org/tx/${wldTxHash}\n`);
    } else {
      console.log('⏭️  Keine WLD Tokens zum Senden\n');
    }

    // 5. WETH Tokens senden
    if (wethBalance > 0n) {
      console.log(`🔄 Sende ${Number(wethBalance) / 1e18} WETH Token...`);

      const wethTransferData = encodeFunctionData({
        abi: ERC20_ABI,
        functionName: 'transfer',
        args: [destinationAddress, wethBalance]
      });

      const wethTxHash = await walletClient.sendTransaction({
        to: WETH_TOKEN,
        data: wethTransferData,
        gas: 100000n
      });

      console.log(`✅ WETH Transfer TX: ${wethTxHash}`);
      console.log(`   https://worldscan.org/tx/${wethTxHash}\n`);
    } else {
      console.log('⏭️  Keine WETH Tokens zum Senden\n');
    }

    console.log('🎉 Token Recovery erfolgreich abgeschlossen!');
    console.log(`\n🔍 Prüfe deine World App - die Tokens sollten da sein!\n`);

  } catch (error: any) {
    console.error('\n❌ Fehler beim Token Recovery:', error.message);
    console.error('Details:', error);
  }
}

// Script ausführen
recoverTokens();
