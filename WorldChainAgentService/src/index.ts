import express from 'express';
import dotenv from 'dotenv';
import { TradingAgent } from './agent/TradingAgent';
import { DatabaseService } from './services/DatabaseService';
import { WorldChainAgentService } from './services/WorldChainAgentService';

// Load environment variables
dotenv.config();

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 3000;

// Initialize services
const database = new DatabaseService();
const tradingAgent = new TradingAgent(database);
const worldChainAgent = new WorldChainAgentService();

// Health check endpoint
app.get('/health', (req, res) => {
  res.json({
    status: 'ok',
    service: 'World Chain Agent Service',
    chainId: process.env.CHAIN_ID
  });
});

// Get agent status
app.get('/api/agent/:agentId/status', async (req, res) => {
  try {
    const { agentId } = req.params;
    const status = await tradingAgent.getAgentStatus(parseInt(agentId));
    res.json({ success: true, data: status });
  } catch (error: any) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// Analyze market (get trading signal without executing)
app.post('/api/agent/:agentId/analyze', async (req, res) => {
  try {
    const { agentId } = req.params;
    const signal = await tradingAgent.analyzeMarket(parseInt(agentId));
    res.json({ success: true, signal });
  } catch (error: any) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// Execute trade
app.post('/api/agent/:agentId/trade', async (req, res) => {
  try {
    const { agentId } = req.params;
    const result = await tradingAgent.executeTrade(parseInt(agentId));
    res.json({ success: true, trade: result });
  } catch (error: any) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// Start background trading loop
app.post('/api/agent/:agentId/start', async (req, res) => {
  try {
    const { agentId } = req.params;
    tradingAgent.startAutomatedTrading(parseInt(agentId));
    res.json({ success: true, message: 'Automated trading started' });
  } catch (error: any) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// Stop background trading loop
app.post('/api/agent/:agentId/stop', async (req, res) => {
  try {
    const { agentId } = req.params;
    tradingAgent.stopAutomatedTrading(parseInt(agentId));
    res.json({ success: true, message: 'Automated trading stopped' });
  } catch (error: any) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// ==================== AGENTKIT ENDPOINTS ====================

// Initialize AgentKit for an agent
app.post('/api/agentkit/initialize', async (req, res) => {
  try {
    const { privateKey, worldIdHash } = req.body;

    if (!privateKey) {
      return res.status(400).json({ success: false, error: 'privateKey required' });
    }

    const success = await worldChainAgent.initialize(privateKey, worldIdHash);

    res.json({
      success,
      address: worldChainAgent.getAddress(),
      message: success ? 'AgentKit initialized with WLD gas support' : 'Initialization failed'
    });
  } catch (error: any) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// Execute swap via AgentKit (pays gas in WLD!)
app.post('/api/agentkit/swap', async (req, res) => {
  try {
    const { tokenIn, tokenOut, amountIn, slippagePercent } = req.body;

    if (!tokenIn || !tokenOut || !amountIn) {
      return res.status(400).json({
        success: false,
        error: 'Missing required fields: tokenIn, tokenOut, amountIn'
      });
    }

    console.log(`\n🔄 AgentKit Swap Request:`);
    console.log(`   From: ${tokenIn}`);
    console.log(`   To: ${tokenOut}`);
    console.log(`   Amount: ${amountIn}`);
    console.log(`   ⛽ Gas: WLD (automatically deducted)\n`);

    const result = await worldChainAgent.executeSwap({
      tokenIn,
      tokenOut,
      amountIn,
      slippagePercent: slippagePercent || 3
    });

    res.json(result);
  } catch (error: any) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// Get AgentKit wallet info
app.get('/api/agentkit/wallet', async (req, res) => {
  try {
    const address = worldChainAgent.getAddress();
    const wldBalance = await worldChainAgent.getWLDBalance();

    res.json({
      success: true,
      address,
      wldBalance,
      gasToken: 'WLD'
    });
  } catch (error: any) {
    res.status(500).json({ success: false, error: error.message });
  }
});

// Start server
app.listen(PORT, () => {
  console.log(`🚀 World Chain Agent Service running on port ${PORT}`);
  console.log(`🌍 Chain ID: ${process.env.CHAIN_ID}`);
  console.log(`📊 RPC: ${process.env.WORLD_CHAIN_RPC_URL}`);
});

// Graceful shutdown
process.on('SIGINT', () => {
  console.log('Shutting down gracefully...');
  tradingAgent.stopAll();
  process.exit(0);
});
