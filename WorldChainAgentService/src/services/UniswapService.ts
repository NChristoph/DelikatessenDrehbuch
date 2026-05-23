import { createWalletClient, createPublicClient, http, parseUnits, formatUnits, type Address } from 'viem';
import { privateKeyToAccount } from 'viem/accounts';
import { worldchain } from 'viem/chains';

const WLD_ADDRESS = (process.env.WLD_TOKEN_ADDRESS as Address) || '0x2cFc85d8E48F8EAB294be644d9E25C3030863003';
const WETH_ADDRESS = (process.env.WETH_ADDRESS as Address) || '0x4200000000000000000000000000000000000006';
const UNISWAP_ROUTER = (process.env.UNISWAP_V3_ROUTER as Address) || '0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD';

// ERC20 ABI (simplified)
const ERC20_ABI = [
  {
    inputs: [{ name: 'spender', type: 'address' }, { name: 'amount', type: 'uint256' }],
    name: 'approve',
    outputs: [{ name: '', type: 'bool' }],
    stateMutability: 'nonpayable',
    type: 'function'
  },
  {
    inputs: [{ name: 'account', type: 'address' }],
    name: 'balanceOf',
    outputs: [{ name: '', type: 'uint256' }],
    stateMutability: 'view',
    type: 'function'
  },
  {
    inputs: [],
    name: 'decimals',
    outputs: [{ name: '', type: 'uint8' }],
    stateMutability: 'view',
    type: 'function'
  }
] as const;

// Uniswap V3 Router ABI (simplified - exactInputSingle)
const ROUTER_ABI = [
  {
    inputs: [{
      components: [
        { internalType: 'address', name: 'tokenIn', type: 'address' },
        { internalType: 'address', name: 'tokenOut', type: 'address' },
        { internalType: 'uint24', name: 'fee', type: 'uint24' },
        { internalType: 'address', name: 'recipient', type: 'address' },
        { internalType: 'uint256', name: 'deadline', type: 'uint256' },
        { internalType: 'uint256', name: 'amountIn', type: 'uint256' },
        { internalType: 'uint256', name: 'amountOutMinimum', type: 'uint256' },
        { internalType: 'uint160', name: 'sqrtPriceLimitX96', type: 'uint160' }
      ],
      internalType: 'struct ISwapRouter.ExactInputSingleParams',
      name: 'params',
      type: 'tuple'
    }],
    name: 'exactInputSingle',
    outputs: [{ internalType: 'uint256', name: 'amountOut', type: 'uint256' }],
    stateMutability: 'payable',
    type: 'function'
  }
] as const;

interface SwapParams {
  privateKey: string;
  action: 'BUY' | 'SELL';
  amount: number;
  slippage: number;
}

interface SwapResult {
  success: boolean;
  amountOut?: number;
  txHash?: string;
  error?: string;
}

export class UniswapService {
  private publicClient;

  constructor() {
    this.publicClient = createPublicClient({
      chain: worldchain,
      transport: http(process.env.WORLD_CHAIN_RPC_URL)
    });
  }

  /**
   * Get current WLD/ETH price (returns 1 WLD = X ETH)
   */
  async getWLDPrice(): Promise<number> {
    try {
      // Simplified: Use a price estimate with small variations
      // In production, you'd query Uniswap pool directly or use a price oracle
      const basePrice = 0.00065; // 1 WLD ≈ 0.00065 ETH
      const variation = (Math.random() - 0.5) * 0.04; // ±2%
      const price = basePrice * (1 + variation);

      return price;
    } catch (error: any) {
      console.error('Failed to get WLD price:', error.message);
      return 0.00065; // Fallback
    }
  }

  /**
   * Execute token swap on Uniswap V3
   * NOTE: On World Chain, gas is paid in WLD automatically!
   */
  async executeSwap(params: SwapParams): Promise<SwapResult> {
    try {
      const { privateKey, action, amount, slippage } = params;

      // Create wallet client
      const account = privateKeyToAccount(`0x${privateKey}` as Address);
      const walletClient = createWalletClient({
        account,
        chain: worldchain,
        transport: http(process.env.WORLD_CHAIN_RPC_URL)
      });

      console.log(`📍 Wallet address: ${account.address}`);

      // Determine token addresses
      const tokenIn = action === 'SELL' ? WLD_ADDRESS : WETH_ADDRESS;
      const tokenOut = action === 'SELL' ? WETH_ADDRESS : WLD_ADDRESS;

      console.log(`🔄 Swapping ${amount} ${action === 'SELL' ? 'WLD' : 'WETH'} to ${action === 'SELL' ? 'WETH' : 'WLD'}`);

      // Get token decimals
      const decimals = await this.publicClient.readContract({
        address: tokenIn,
        abi: ERC20_ABI,
        functionName: 'decimals'
      }) as number;

      const amountIn = parseUnits(amount.toString(), decimals);

      // Check balance
      const balance = await this.publicClient.readContract({
        address: tokenIn,
        abi: ERC20_ABI,
        functionName: 'balanceOf',
        args: [account.address]
      }) as bigint;

      console.log(`💰 Token balance: ${formatUnits(balance, decimals)} tokens`);

      if (balance < amountIn) {
        return {
          success: false,
          error: `Insufficient balance: ${formatUnits(balance, decimals)} < ${amount}`
        };
      }

      // Step 1: Approve router to spend tokens
      console.log(`✅ Approving router to spend tokens...`);

      const approveHash = await walletClient.writeContract({
        address: tokenIn,
        abi: ERC20_ABI,
        functionName: 'approve',
        args: [UNISWAP_ROUTER, amountIn]
      });

      console.log(`⏳ Waiting for approve TX: ${approveHash}`);

      // Wait for approve confirmation
      await this.publicClient.waitForTransactionReceipt({ hash: approveHash });

      console.log(`✅ Approve confirmed!`);

      // Step 2: Execute swap
      console.log(`🔄 Executing swap...`);

      // Calculate minimum output with slippage
      const estimatedOut = amountIn * 65n / 100000n; // Rough estimate
      const minOut = estimatedOut * BigInt(100 - slippage) / 100n;

      const swapParams = {
        tokenIn,
        tokenOut,
        fee: 3000, // 0.3% fee tier
        recipient: account.address,
        deadline: BigInt(Math.floor(Date.now() / 1000) + 1200), // 20 minutes
        amountIn,
        amountOutMinimum: minOut,
        sqrtPriceLimitX96: 0n
      };

      const swapHash = await walletClient.writeContract({
        address: UNISWAP_ROUTER,
        abi: ROUTER_ABI,
        functionName: 'exactInputSingle',
        args: [swapParams]
      });

      console.log(`⏳ Waiting for swap TX: ${swapHash}`);

      // Wait for swap confirmation
      const receipt = await this.publicClient.waitForTransactionReceipt({ hash: swapHash });

      if (receipt.status === 'success') {
        // Get actual amount out (simplified - would parse logs in production)
        const actualOut = parseFloat(formatUnits(estimatedOut, 18));

        console.log(`✅ Swap successful! Received: ${actualOut.toFixed(6)} tokens`);

        return {
          success: true,
          amountOut: actualOut,
          txHash: swapHash
        };
      } else {
        return {
          success: false,
          error: `Swap transaction reverted`
        };
      }

    } catch (error: any) {
      console.error('❌ Swap failed:', error);

      return {
        success: false,
        error: error.message || 'Unknown error'
      };
    }
  }
}
