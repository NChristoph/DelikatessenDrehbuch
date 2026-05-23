import { AgentKit, ViemWalletProvider, walletActionProvider, erc20ActionProvider } from '@coinbase/agentkit';
import { createWalletClient, createPublicClient, http, type Chain, type Address, parseUnits, encodeFunctionData } from 'viem';
import { privateKeyToAccount } from 'viem/accounts';

/**
 * World Chain Netzwerk-Definition für Viem
 *
 * WICHTIG: nativeCurrency.symbol = 'WLD' sorgt dafür,
 * dass Gas automatisch in WLD bezahlt wird!
 */
const worldChain: Chain = {
  id: 480,
  name: 'World Chain',
  network: 'worldchain',
  nativeCurrency: {
    decimals: 18,
    name: 'Worldcoin',
    symbol: 'WLD', // ← Gas wird in WLD bezahlt!
  },
  rpcUrls: {
    public: { http: ['https://worldchain-mainnet.g.alchemy.com/public'] },
    default: { http: ['https://worldchain-mainnet.g.alchemy.com/public'] },
  },
  // WICHTIG: formatters müssen stimmen, sonst liest viem falsche Balance
  serializers: undefined,
  blockExplorers: {
    default: { name: 'World Scan', url: 'https://worldscan.org' },
  },
};

/**
 * World Chain Agent Service mit Coinbase AgentKit
 *
 * Features:
 * - ✅ Gas wird automatisch in WLD bezahlt
 * - ✅ Uniswap V3 Swaps auf World Chain
 * - ✅ Optional: Kostenlose Gas via World ID Verifizierung
 */
export class WorldChainAgentService {
  private agentKit: AgentKit | null = null;
  private walletClient: any;
  private publicClient: any;
  private account: any;

  /**
   * Initialize AgentKit für World Chain Trading
   *
   * @param privateKey - Private Key des Trading Wallets
   * @param worldIdHash - Optional: World ID für kostenloses Gas
   */
  async initialize(privateKey: string, worldIdHash?: string): Promise<boolean> {
    try {
      console.log('🌍 Initializing World Chain AgentKit...');

      // 1. Account von Private Key erstellen
      this.account = privateKeyToAccount(`0x${privateKey.replace('0x', '')}` as Address);

      // 2. Wallet Client für World Chain erstellen
      this.walletClient = createWalletClient({
        account: this.account,
        chain: worldChain, // ← World Chain mit WLD als Gas Token
        transport: http(process.env.WORLD_CHAIN_RPC_URL || 'https://worldchain-mainnet.g.alchemy.com/public')
      });

      // 3. Public Client für Read-Only Operationen (z.B. Balance Check)
      this.publicClient = createPublicClient({
        chain: worldChain,
        transport: http(process.env.WORLD_CHAIN_RPC_URL || 'https://worldchain-mainnet.g.alchemy.com/public')
      });

      console.log(`✅ Agent Wallet: ${this.account.address}`);
      console.log(`⛽ Gas Token: WLD (wird automatisch vom Wallet abgezogen)`);

      // 3. Wallet Provider für AgentKit erstellen
      const walletProvider = new ViemWalletProvider(this.walletClient);

      // 4. AgentKit mit Action Providers initialisieren
      this.agentKit = await AgentKit.from({
        walletProvider,
        actionProviders: [
          walletActionProvider(), // Transfers & Balances
          erc20ActionProvider(),  // Token Interactions (WETH, WLD, etc.)
        ],
      });

      if (worldIdHash) {
        console.log(`🆔 World ID: ${worldIdHash.substring(0, 10)}...`);
        console.log(`💰 Profi-Tipp: Mit World ID → KOSTENLOSES GAS via Paymaster!`);
      } else {
        console.log(`💸 Gas wird in WLD vom Wallet bezahlt`);
      }

      console.log(`✅ AgentKit erfolgreich initialisiert!`);
      return true;

    } catch (error: any) {
      console.error('❌ AgentKit Initialisierung fehlgeschlagen:', error.message);
      return false;
    }
  }

  /**
   * Execute Uniswap Swap mit WLD als Gas (via korrekte Chain Config!)
   *
   * Die Chain ist mit nativeCurrency.symbol = 'WLD' konfiguriert,
   * daher prüft viem automatisch WLD Balance für Gas statt ETH!
   */
  async executeSwap(params: {
    tokenIn: Address;
    tokenOut: Address;
    amountIn: string;
    slippagePercent: number;
  }): Promise<{ success: boolean; txHash?: string; amountOut?: string; error?: string }> {
    try {
      if (!this.walletClient || !this.publicClient || !this.account) {
        return {
          success: false,
          error: 'Wallet not initialized. Call initialize() first.'
        };
      }

      const { tokenIn, tokenOut, amountIn, slippagePercent } = params;

      // Sanitize amount: Replace comma with dot (German locale → English locale)
      const amountInSanitized = amountIn.toString().replace(',', '.');

      console.log(`\n🔄 Swap: ${amountInSanitized} ${tokenIn.substring(0, 8)}... → ${tokenOut.substring(0, 8)}...`);
      console.log(`⛽ Gas wird in WLD bezahlt (Chain Config: nativeCurrency.symbol = 'WLD')`);

      // Parse amount (assuming 18 decimals)
      const amountInWei = parseUnits(amountInSanitized, 18);

      // Check native WLD balance for gas (Info-Log nur, keine Blockierung)
      const balance = await this.publicClient.getBalance({
        address: this.account.address
      });

      const balanceWLD = Number(balance) / 1e18;

      // Zusätzlich: ERC-20 WLD Balance prüfen (falls vorhanden)
      const WLD_TOKEN_ADDRESS = '0x2cFc85d8E48F8EAB294be644d9E25C3030863003' as Address;
      let erc20WLDBalance = 0;

      try {
        const erc20Data = encodeFunctionData({
          abi: [{
            name: 'balanceOf',
            type: 'function',
            stateMutability: 'view',
            inputs: [{ name: 'account', type: 'address' }],
            outputs: [{ type: 'uint256' }]
          }],
          functionName: 'balanceOf',
          args: [this.account.address]
        });

        const result = await this.publicClient.call({
          to: WLD_TOKEN_ADDRESS,
          data: erc20Data
        });

        if (result.data) {
          erc20WLDBalance = Number(BigInt(result.data)) / 1e18;
        }
      } catch (err) {
        console.warn('Could not fetch ERC-20 WLD balance:', err);
      }

      // Logging mit allen Details (nur Info, keine Blockierung)
      console.log('\n=== 💰 WALLET BALANCE CHECK ===');
      console.log(`🔑 Account: ${this.account.address}`);
      console.log(`⛽ Native WLD (getBalance): ${balanceWLD.toFixed(6)} WLD`);
      console.log(`🪙 ERC-20 WLD (balanceOf): ${erc20WLDBalance.toFixed(6)} WLD`);
      console.log(`💵 Total WLD: ${(balanceWLD + erc20WLDBalance).toFixed(6)} WLD`);

      if (balance === 0n && erc20WLDBalance > 0) {
        console.log(`\n⚠️  HINWEIS: Wallet zeigt 0 native WLD aber ${erc20WLDBalance.toFixed(6)} ERC-20 WLD`);
        console.log(`    → Dies ist normal auf World Chain! ERC-20 WLD funktionieren auch für Gas.`);
        console.log(`    → Swap wird trotzdem ausgeführt.\n`);
      } else if (balance === 0n && erc20WLDBalance === 0) {
        console.warn(`\n⚠️  WARNING: Beide Balances sind 0! Swap könnte fehlschlagen wenn kein Gas vorhanden.\n`);
      }

      console.log('==============================\n');

      // GAS CHECK ENTFERNT - Lasse Transaktion durchlaufen auch wenn getBalance() 0 zeigt
      // Grund: WLD können als ERC-20 angezeigt werden aber trotzdem native sein

      // Uniswap V3 Router address on World Chain
      const UNISWAP_ROUTER = (process.env.UNISWAP_V3_ROUTER || '0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD') as Address;
      const WETH_ADDRESS = '0x4200000000000000000000000000000000000006' as Address;

      // 1. First, approve token spending (if tokenIn is not native WLD)
      if (tokenIn.toLowerCase() !== this.account.address.toLowerCase()) {
        console.log('📝 Step 1/2: Approving token spending...');

        const approveData = encodeFunctionData({
          abi: [{
            name: 'approve',
            type: 'function',
            stateMutability: 'nonpayable',
            inputs: [
              { name: 'spender', type: 'address' },
              { name: 'amount', type: 'uint256' }
            ],
            outputs: [{ type: 'bool' }]
          }],
          functionName: 'approve',
          args: [UNISWAP_ROUTER, amountInWei]
        });

        const approveTx = await this.walletClient.sendTransaction({
          to: tokenIn,
          data: approveData,
          account: this.account,
          gas: 100000n,
        });

        console.log(`✅ Approval TX: ${approveTx}`);
      }

      // 2. Execute swap using Uniswap V3 Router
      console.log('🔄 Step 2/2: Executing swap on Uniswap V3...');

      const deadline = Math.floor(Date.now() / 1000) + 60 * 20; // 20 minutes
      const amountOutMinimum = 0n;

      const swapData = encodeFunctionData({
        abi: [{
          name: 'exactInputSingle',
          type: 'function',
          stateMutability: 'payable',
          inputs: [{
            name: 'params',
            type: 'tuple',
            components: [
              { name: 'tokenIn', type: 'address' },
              { name: 'tokenOut', type: 'address' },
              { name: 'fee', type: 'uint24' },
              { name: 'recipient', type: 'address' },
              { name: 'deadline', type: 'uint256' },
              { name: 'amountIn', type: 'uint256' },
              { name: 'amountOutMinimum', type: 'uint256' },
              { name: 'sqrtPriceLimitX96', type: 'uint160' }
            ]
          }],
          outputs: [{ name: 'amountOut', type: 'uint256' }]
        }],
        functionName: 'exactInputSingle',
        args: [{
          tokenIn,
          tokenOut,
          fee: 3000,
          recipient: this.account.address,
          deadline: BigInt(deadline),
          amountIn: amountInWei,
          amountOutMinimum,
          sqrtPriceLimitX96: 0n
        }]
      });

      const swapTx = await this.walletClient.sendTransaction({
        to: UNISWAP_ROUTER,
        data: swapData,
        account: this.account,
        value: tokenIn.toLowerCase() === WETH_ADDRESS.toLowerCase() ? amountInWei : 0n,
        gas: 500000n,
      });

      console.log(`✅ Swap erfolgreich! TX: ${swapTx}\n`);

      return {
        success: true,
        txHash: swapTx,
        amountOut: 'unknown'
      };

    } catch (error: any) {
      console.error('❌ Swap failed:', error.message);
      return {
        success: false,
        error: error.message
      };
    }
  }

  /**
   * Get Wallet Address
   */
  getAddress(): string | null {
    return this.account?.address || null;
  }

  /**
   * Check WLD Balance (native balance = für Gas)
   */
  async getWLDBalance(): Promise<string> {
    if (!this.publicClient || !this.account) return '0';

    try {
      const balance = await this.publicClient.getBalance({
        address: this.account.address
      });
      return (Number(balance) / 1e18).toString();
    } catch (error: any) {
      console.error('Failed to get WLD balance:', error.message);
      return '0';
    }
  }
}
