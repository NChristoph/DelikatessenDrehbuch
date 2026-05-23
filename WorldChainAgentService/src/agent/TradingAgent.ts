import { createWalletClient, http, parseUnits, formatUnits, type Address } from 'viem';
import { privateKeyToAccount } from 'viem/accounts';
import { worldchain } from 'viem/chains';
import { DatabaseService } from '../services/DatabaseService';
import { UniswapService } from '../services/UniswapService';

interface TradingSignal {
  action: 'BUY' | 'SELL' | 'HOLD';
  confidence: number;
  suggestedAmount: number;
  reason: string;
}

interface AgentConfig {
  id: number;
  walletAddress: string;
  encryptedPrivateKey: string;
  currentBalanceWLD: number;
  currentBalanceETH: number;
  riskLevel: string;
  strategy: string;
}

export class TradingAgent {
  private database: DatabaseService;
  private uniswap: UniswapService;
  private activeAgents: Map<number, NodeJS.Timeout> = new Map();

  constructor(database: DatabaseService) {
    this.database = database;
    this.uniswap = new UniswapService();
  }

  /**
   * Get agent configuration and status
   */
  async getAgentStatus(agentId: number): Promise<any> {
    const agent = await this.database.getAgent(agentId);

    if (!agent) {
      throw new Error(`Agent ${agentId} not found`);
    }

    // Get current prices
    const wldPrice = await this.uniswap.getWLDPrice();

    return {
      id: agent.id,
      walletAddress: agent.walletAddress,
      balanceWLD: agent.currentBalanceWLD,
      balanceETH: agent.currentBalanceETH,
      totalValueUSD: agent.currentBalanceWLD * wldPrice,
      profitLoss: agent.currentBalanceWLD - agent.initialFundingWLD,
      status: agent.status,
      strategy: agent.strategy,
      riskLevel: agent.riskLevel,
      isAutomated: this.activeAgents.has(agentId)
    };
  }

  /**
   * Analyze market and generate trading signal
   */
  async analyzeMarket(agentId: number): Promise<TradingSignal> {
    const agent = await this.database.getAgent(agentId);

    if (!agent) {
      throw new Error(`Agent ${agentId} not found`);
    }

    console.log(`📊 Analyzing market for agent ${agentId}...`);

    // Get current WLD/ETH price
    const currentPrice = await this.uniswap.getWLDPrice();

    // Get recent price history
    const recentPrices = await this.database.getRecentPrices(24); // last 24 entries

    if (recentPrices.length < 2) {
      return {
        action: 'HOLD',
        confidence: 55,
        suggestedAmount: 0,
        reason: `Collecting price data... (${recentPrices.length}/2 samples needed)`
      };
    }

    // Calculate price change over time
    const oldestPrice = recentPrices[0].price;
    const priceChange = ((currentPrice - oldestPrice) / oldestPrice) * 100;

    console.log(`💰 Current price: ${currentPrice.toFixed(6)} ETH`);
    console.log(`📈 Price change: ${priceChange.toFixed(2)}%`);

    // Risk multiplier based on agent settings
    const riskMultiplier = agent.riskLevel === 'Low' ? 0.5 :
                          agent.riskLevel === 'High' ? 1.5 : 1.0;

    // Generate signal based on price momentum
    if (priceChange > 1.0) {
      // Strong uptrend → BUY
      const confidence = Math.min(60 + Math.floor(priceChange * 10), 90);
      const amount = this.calculatePositionSize(agent, riskMultiplier);

      return {
        action: 'BUY',
        confidence,
        suggestedAmount: amount,
        reason: `🚀 Strong uptrend: ${priceChange.toFixed(2)}% - Good entry point`
      };
    } else if (priceChange < -1.0 && agent.currentBalanceWLD > 1) {
      // Strong downtrend → SELL
      const confidence = Math.min(60 + Math.floor(Math.abs(priceChange) * 10), 90);
      const amount = Math.min(agent.currentBalanceWLD * 0.3, this.calculatePositionSize(agent, riskMultiplier));

      return {
        action: 'SELL',
        confidence,
        suggestedAmount: amount,
        reason: `📉 Strong downtrend: ${priceChange.toFixed(2)}% - Take profits/cut losses`
      };
    } else if (priceChange > 0.3) {
      // Mild uptrend
      const confidence = 50 + Math.floor(priceChange * 20);
      const amount = this.calculatePositionSize(agent, riskMultiplier);

      return {
        action: 'BUY',
        confidence,
        suggestedAmount: amount,
        reason: `📊 Mild uptrend: ${priceChange.toFixed(2)}% - Conservative buy`
      };
    } else if (priceChange < -0.3) {
      return {
        action: 'HOLD',
        confidence: 65,
        suggestedAmount: 0,
        reason: `⚠️ Mild downtrend: ${priceChange.toFixed(2)}% - Waiting for reversal`
      };
    } else {
      return {
        action: 'HOLD',
        confidence: 60,
        suggestedAmount: 0,
        reason: `➡️ Sideways: ${priceChange.toFixed(2)}% - No clear trend`
      };
    }
  }

  /**
   * Execute trade based on signal
   */
  async executeTrade(agentId: number): Promise<any> {
    const agent = await this.database.getAgent(agentId);

    if (!agent) {
      throw new Error(`Agent ${agentId} not found`);
    }

    // Get trading signal
    const signal = await this.analyzeMarket(agentId);

    console.log(`🎯 Signal: ${signal.action} (${signal.confidence}% confidence)`);
    console.log(`💡 Reason: ${signal.reason}`);

    // Save signal to database
    await this.database.updateAgentSignal(agentId, signal);

    // Check if we should execute
    const minConfidence = parseInt(process.env.MIN_CONFIDENCE_PERCENT || '60');

    if (signal.confidence < minConfidence || signal.action === 'HOLD') {
      console.log(`⏭️ Skipping trade (confidence ${signal.confidence}% < ${minConfidence}% or HOLD signal)`);
      return {
        executed: false,
        signal,
        reason: 'Confidence too low or HOLD signal'
      };
    }

    // Decrypt private key
    const privateKey = await this.database.decryptPrivateKey(agent.encryptedPrivateKey);

    try {
      console.log(`🔄 Executing ${signal.action} trade: ${signal.suggestedAmount.toFixed(4)} tokens`);

      // Execute swap via Uniswap
      const result = await this.uniswap.executeSwap({
        privateKey,
        action: signal.action,
        amount: signal.suggestedAmount,
        slippage: 3 // 3% slippage tolerance
      });

      if (!result.success) {
        // Log failed trade
        await this.database.logTrade({
          agentId,
          tradeType: signal.action,
          fromAmount: signal.suggestedAmount,
          toAmount: 0,
          status: 'Failed',
          errorMessage: result.error
        });

        return {
          executed: false,
          signal,
          error: result.error
        };
      }

      // Update agent balances
      if (signal.action === 'BUY') {
        agent.currentBalanceETH -= signal.suggestedAmount;
        agent.currentBalanceWLD += result.amountOut;
      } else {
        agent.currentBalanceWLD -= signal.suggestedAmount;
        agent.currentBalanceETH += result.amountOut;
      }

      await this.database.updateAgentBalances(agentId, agent.currentBalanceWLD, agent.currentBalanceETH);

      // Log successful trade
      await this.database.logTrade({
        agentId,
        tradeType: signal.action,
        fromAmount: signal.suggestedAmount,
        toAmount: result.amountOut,
        txHash: result.txHash,
        status: 'Success'
      });

      console.log(`✅ Trade successful! TX: ${result.txHash}`);

      return {
        executed: true,
        signal,
        result
      };

    } catch (error: any) {
      console.error(`❌ Trade failed: ${error.message}`);

      await this.database.logTrade({
        agentId,
        tradeType: signal.action,
        fromAmount: signal.suggestedAmount,
        toAmount: 0,
        status: 'Failed',
        errorMessage: error.message
      });

      return {
        executed: false,
        signal,
        error: error.message
      };
    }
  }

  /**
   * Start automated trading for an agent
   */
  startAutomatedTrading(agentId: number): void {
    if (this.activeAgents.has(agentId)) {
      console.log(`⚠️ Agent ${agentId} is already running`);
      return;
    }

    console.log(`🤖 Starting automated trading for agent ${agentId}`);

    const intervalMinutes = parseInt(process.env.TRADE_INTERVAL_MINUTES || '5');

    // Execute trade immediately
    this.executeTrade(agentId).catch(console.error);

    // Then every X minutes
    const interval = setInterval(() => {
      this.executeTrade(agentId).catch(console.error);
    }, intervalMinutes * 60 * 1000);

    this.activeAgents.set(agentId, interval);
  }

  /**
   * Stop automated trading for an agent
   */
  stopAutomatedTrading(agentId: number): void {
    const interval = this.activeAgents.get(agentId);

    if (interval) {
      clearInterval(interval);
      this.activeAgents.delete(agentId);
      console.log(`🛑 Stopped automated trading for agent ${agentId}`);
    }
  }

  /**
   * Stop all automated trading
   */
  stopAll(): void {
    this.activeAgents.forEach((interval, agentId) => {
      clearInterval(interval);
      console.log(`🛑 Stopped agent ${agentId}`);
    });
    this.activeAgents.clear();
  }

  /**
   * Calculate position size based on risk management
   */
  private calculatePositionSize(agent: AgentConfig, riskMultiplier: number): number {
    // Risk 2% of current balance per trade
    const riskPerTrade = agent.currentBalanceWLD * 0.02 * riskMultiplier;

    // Min 1 WLD, Max 20% of balance
    const minSize = 1;
    const maxSize = agent.currentBalanceWLD * 0.2;

    return Math.max(minSize, Math.min(riskPerTrade, maxSize));
  }
}
