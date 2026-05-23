# World Chain Trading Agent Service

Trading agent service using Coinbase AgentKit for World Chain (Optimism L2).

## Features

- ✅ **WLD Native Gas** - Pays gas fees in WLD automatically
- ✅ **Uniswap V3 Integration** - Direct trading on World Chain
- ✅ **Automated Trading** - Background loop every 5 minutes
- ✅ **Risk Management** - Position sizing based on risk level
- ✅ **RESTful API** - Integration with C# ASP.NET app
- ✅ **Price Trend Analysis** - Momentum-based signals

## Prerequisites

1. **Node.js** v20+ ([Download](https://nodejs.org/))
2. **npm** or **yarn**
3. SQL Server database (already running from C# app)

## Setup

### 1. Install Node.js

Download and install from https://nodejs.org/ (LTS version recommended)

### 2. Install Dependencies

```bash
cd WorldChainAgentService
npm install
```

### 3. Configure Environment

Copy `.env.example` to `.env`:

```bash
cp .env.example .env
```

Edit `.env` and fill in:

```env
# Your agent's private key (from WorldTradingAgents table)
AGENT_PRIVATE_KEY=your_decrypted_private_key_here

# Database connection (already configured)
DATABASE_URL=Server=tcp:delikatessendrehbuch...

# Optional: Coinbase CDP API keys (for advanced features)
CDP_API_KEY_NAME=...
CDP_API_KEY_PRIVATE_KEY=...
```

### 4. Build and Run

```bash
# Development mode (auto-reload)
npm run dev

# Production build
npm run build
npm start
```

## API Endpoints

The service runs on `http://localhost:3000` by default.

### Health Check
```
GET /health
```

### Get Agent Status
```
GET /api/agent/:agentId/status
```

### Analyze Market (without executing)
```
POST /api/agent/:agentId/analyze
```

### Execute Single Trade
```
POST /api/agent/:agentId/trade
```

### Start Automated Trading
```
POST /api/agent/:agentId/start
```

### Stop Automated Trading
```
POST /api/agent/:agentId/stop
```

### 🆕 AgentKit Endpoints (WLD Gas Payment)

**Initialize AgentKit:**
```bash
POST /api/agentkit/initialize
Content-Type: application/json

{
  "privateKey": "0x...",
  "worldIdHash": "optional-world-id-hash"
}
```

**Execute Swap (Gas in WLD!):**
```bash
POST /api/agentkit/swap
Content-Type: application/json

{
  "tokenIn": "0x4200000000000000000000000000000000000006",
  "tokenOut": "0x2cFc85d8E48F8EAB294be644d9E25C3030863003",
  "amountIn": "0.001",
  "slippagePercent": 3
}
```

**Get Wallet Info:**
```bash
GET /api/agentkit/wallet
```

## Integration with C# App

The C# app can call this service via HTTP:

```csharp
// Start automated trading for an agent
var response = await httpClient.PostAsync(
    "http://localhost:3000/api/agent/2/start",
    null
);
```

## How Gas Works on World Chain

World Chain uses **WLD as the native gas token**!

- ✅ No need for ETH or WETH for gas
- ✅ Gas fees automatically deducted from WLD balance
- ✅ Very cheap (~$0.03 per transaction)
- ✅ Verified World ID users may get free gas

The agent automatically handles WLD gas payments through viem and the World Chain RPC.

## Architecture

```
┌─────────────────────────────┐
│   C# ASP.NET Core Web App   │
│   (UI, Database, Auth)      │
│          ↓ HTTP API         │
├─────────────────────────────┤
│   Node.js Agent Service     │
│   (Trading Logic)           │
│          ↓ viem             │
├─────────────────────────────┤
│   World Chain (Chain ID 480)│
│   - Uniswap V3              │
│   - WLD/WETH Pool           │
└─────────────────────────────┘
```

## Trading Strategy

1. **Price Collection** - Fetches WLD/ETH price every 5 minutes
2. **Trend Analysis** - Calculates price change over time
3. **Signal Generation**:
   - Uptrend > 1% → BUY (60-90% confidence)
   - Downtrend > 1% → SELL (60-90% confidence)
   - Sideways → HOLD
4. **Execution** - Only executes if confidence ≥ 60%
5. **Risk Management** - Position size = 2% of balance × risk multiplier

## Troubleshooting

### "npm: command not found"
Install Node.js from https://nodejs.org/

### "Module not found" errors
Run `npm install` to install dependencies

### "Connection refused" to C# API
Make sure the C# app is running on port 5000 (or update `CSHARP_API_URL` in `.env`)

### Gas errors / Transaction failed
- Check agent has sufficient WLD balance (needs both for trading AND gas)
- Verify Chain ID is 480
- Check RPC endpoint is working

## Development

```bash
# Watch mode (auto-reload on file changes)
npm run dev

# Type checking
npm run build

# Format code (if prettier is installed)
npx prettier --write src/
```

## 🌐 Azure Deployment

**Siehe:** [AZURE_DEPLOYMENT.md](./AZURE_DEPLOYMENT.md)

Deploy diesen Service als **zweiten App Service** im gleichen App Service Plan wie DelikatessenDrehbuch (keine Extra-Kosten!).

Schnellstart:
1. App Service in Azure erstellen (gleicher Plan)
2. Environment Variables konfigurieren
3. Git Push → automatischer Build
4. C# Service URL auf Production updaten

## License

MIT

## Support

For issues or questions, contact the development team.
