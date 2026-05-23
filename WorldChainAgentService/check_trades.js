const sql = require('mssql');

const config = {
  server: 'delikatessendrehbuch.database.windows.net',
  database: 'DelikatessenDrehbuch_Datenbank-2024-8-8-7-54_2025-06-17T08-19Z',
  user: 'ChristophNitsch',
  password: 'Ni049753!',
  options: {
    encrypt: true,
    trustServerCertificate: false
  }
};

async function checkTrades() {
  try {
    await sql.connect(config);
    
    // Get Agent 2 info
    const agentResult = await sql.query`
      SELECT * FROM WorldTradingAgents WHERE Id = 2
    `;
    
    console.log('\n=== AGENT 2 STATUS ===');
    const agent = agentResult.recordset[0];
    console.log('Status:', agent.Status);
    console.log('Balance WLD:', agent.CurrentBalanceWLD);
    console.log('Balance ETH:', agent.CurrentBalanceETH);
    console.log('Total Trades:', agent.TotalTrades);
    console.log('Successful Trades:', agent.SuccessfulTrades);
    console.log('Last Signal:', agent.LastSignalAction, '(' + agent.LastSignalConfidence + '%)');
    console.log('Last Signal Reason:', agent.LastSignalReason);
    console.log('Last Trade At:', agent.LastTradeAt);
    
    // Get recent trades
    const tradesResult = await sql.query`
      SELECT TOP 10 * FROM WorldAgentTrades 
      WHERE AgentId = 2 
      ORDER BY ExecutedAt DESC
    `;
    
    console.log('\n=== RECENT TRADES ===');
    tradesResult.recordset.forEach(trade => {
      console.log('\nTrade ID:', trade.Id);
      console.log('  Type:', trade.TradeType);
      console.log('  Status:', trade.Status);
      console.log('  From Amount:', trade.FromAmount);
      console.log('  To Amount:', trade.ToAmount);
      console.log('  TX Hash:', trade.TxHash || 'N/A');
      console.log('  Error:', trade.ErrorMessage || 'N/A');
      console.log('  Executed At:', trade.ExecutedAt);
    });
    
  } catch (err) {
    console.error('Error:', err.message);
  } finally {
    await sql.close();
  }
}

checkTrades();
