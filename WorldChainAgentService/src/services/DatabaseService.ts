import axios from 'axios';
import * as crypto from 'crypto';

interface AgentConfig {
  id: number;
  walletAddress: string;
  encryptedPrivateKey: string;
  currentBalanceWLD: number;
  currentBalanceETH: number;
  initialFundingWLD: number;
  riskLevel: string;
  strategy: string;
  status: string;
}

interface PriceEntry {
  price: number;
  timestamp: Date;
}

interface TradeLog {
  agentId: number;
  tradeType: string;
  fromAmount: number;
  toAmount: number;
  txHash?: string;
  status: string;
  errorMessage?: string;
}

/**
 * Database Service - Communicates with C# API for database operations
 *
 * In production, you could:
 * 1. Use this HTTP approach (calls C# API)
 * 2. Or connect directly to SQL Server using mssql npm package
 *
 * For now, we'll use HTTP to avoid duplicating DB logic
 */
export class DatabaseService {
  private baseUrl: string;

  constructor() {
    // C# API endpoint (adjust port if needed)
    this.baseUrl = process.env.CSHARP_API_URL || 'http://localhost:5000';
  }

  /**
   * Get agent configuration from database
   */
  async getAgent(agentId: number): Promise<AgentConfig | null> {
    try {
      const response = await axios.get(`${this.baseUrl}/api/agent/${agentId}`);
      return response.data;
    } catch (error: any) {
      console.error(`Failed to get agent ${agentId}:`, error.message);
      return null;
    }
  }

  /**
   * Get recent price history
   */
  async getRecentPrices(limit: number = 24): Promise<PriceEntry[]> {
    try {
      const response = await axios.get(`${this.baseUrl}/api/prices/recent?limit=${limit}`);
      return response.data;
    } catch (error: any) {
      console.error('Failed to get recent prices:', error.message);
      return [];
    }
  }

  /**
   * Save current price to database
   */
  async savePrice(price: number): Promise<void> {
    try {
      await axios.post(`${this.baseUrl}/api/prices`, {
        tokenIn: process.env.WLD_TOKEN_ADDRESS,
        tokenOut: process.env.WETH_ADDRESS,
        price,
        timestamp: new Date()
      });
    } catch (error: any) {
      console.error('Failed to save price:', error.message);
    }
  }

  /**
   * Update agent's last trading signal
   */
  async updateAgentSignal(agentId: number, signal: any): Promise<void> {
    try {
      await axios.post(`${this.baseUrl}/api/agent/${agentId}/signal`, {
        action: signal.action,
        confidence: signal.confidence,
        reason: signal.reason
      });
    } catch (error: any) {
      console.error('Failed to update agent signal:', error.message);
    }
  }

  /**
   * Update agent balances after trade
   */
  async updateAgentBalances(agentId: number, balanceWLD: number, balanceETH: number): Promise<void> {
    try {
      await axios.post(`${this.baseUrl}/api/agent/${agentId}/balances`, {
        balanceWLD,
        balanceETH
      });
    } catch (error: any) {
      console.error('Failed to update agent balances:', error.message);
    }
  }

  /**
   * Log trade to database
   */
  async logTrade(trade: TradeLog): Promise<void> {
    try {
      await axios.post(`${this.baseUrl}/api/agent/${trade.agentId}/trades`, trade);
    } catch (error: any) {
      console.error('Failed to log trade:', error.message);
    }
  }

  /**
   * Decrypt private key (same logic as C# code)
   */
  async decryptPrivateKey(encryptedKey: string): Promise<string> {
    try {
      const encryptionKey = process.env.ENCRYPTION_KEY || 'CHANGE_THIS_TO_SECURE_KEY_32BYTES!';
      const key = Buffer.from(encryptionKey.padEnd(32, ' ').substring(0, 32));

      const fullCipher = Buffer.from(encryptedKey, 'base64');

      // Extract IV (first 16 bytes)
      const iv = fullCipher.subarray(0, 16);
      const cipher = fullCipher.subarray(16);

      // Decrypt
      const decipher = crypto.createDecipheriv('aes-256-cbc', key, iv);
      let decrypted = decipher.update(cipher);
      decrypted = Buffer.concat([decrypted, decipher.final()]);

      return decrypted.toString('utf8');
    } catch (error: any) {
      console.error('Failed to decrypt private key:', error.message);
      throw new Error('Decryption failed');
    }
  }
}
