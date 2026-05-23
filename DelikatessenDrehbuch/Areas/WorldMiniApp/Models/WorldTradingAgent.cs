using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Autonomer Trading Agent für WLD Tokens
    /// </summary>
    public class WorldTradingAgent
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// World App User Hash (Owner)
        /// </summary>
        [Required]
        [MaxLength(256)]
        public string UserHash { get; set; } = string.Empty;

        /// <summary>
        /// Agent Wallet Address (generiert beim Erstellen)
        /// </summary>
        [Required]
        [MaxLength(42)]
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>
        /// Encrypted Private Key (AES verschlüsselt)
        /// </summary>
        [Required]
        public string EncryptedPrivateKey { get; set; } = string.Empty;

        /// <summary>
        /// Agent Status
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Created"; // Created, Active, Paused, Stopped

        /// <summary>
        /// Initiales Funding (WLD)
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal InitialFundingWLD { get; set; }

        /// <summary>
        /// Aktueller Balance (WLD) - wird periodisch aktualisiert
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal CurrentBalanceWLD { get; set; }

        /// <summary>
        /// Aktueller ETH Balance für Gas Fees
        /// </summary>
        [Column(TypeName = "decimal(18,9)")]
        public decimal CurrentBalanceETH { get; set; }

        /// <summary>
        /// Auto-Swap Threshold: Wenn ETH < dieser Wert, wird automatisch WLD → ETH geswapped
        /// </summary>
        [Column(TypeName = "decimal(18,9)")]
        public decimal AutoSwapThresholdETH { get; set; } = 0.0005m; // Default: 0.0005 ETH

        /// <summary>
        /// Wie viel WLD wird bei Auto-Swap zu ETH konvertiert
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal AutoSwapAmountWLD { get; set; } = 0.5m; // Default: 0.5 WLD

        /// <summary>
        /// Profit/Loss in WLD
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal TotalProfitLossWLD { get; set; }

        /// <summary>
        /// Profit/Loss in Prozent
        /// </summary>
        [Column(TypeName = "decimal(8,4)")]
        public decimal ProfitLossPercentage { get; set; }

        /// <summary>
        /// Anzahl Trades ausgeführt
        /// </summary>
        public int TotalTrades { get; set; }

        /// <summary>
        /// Erfolgreiche Trades
        /// </summary>
        public int SuccessfulTrades { get; set; }

        /// <summary>
        /// Trading-Strategie
        /// </summary>
        [MaxLength(50)]
        public string Strategy { get; set; } = "GridTrading"; // GridTrading, Arbitrage, SimpleSwap

        /// <summary>
        /// Risk Level (Low, Medium, High)
        /// </summary>
        [MaxLength(20)]
        public string RiskLevel { get; set; } = "Medium";

        /// <summary>
        /// Stop-Loss Prozent (Agent stoppt bei diesem Verlust)
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal StopLossPercentage { get; set; } = 20m; // Default 20%

        /// <summary>
        /// Erstellt am
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gestartet am
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gestoppt am
        /// </summary>
        public DateTime? StoppedAt { get; set; }

        /// <summary>
        /// Letzte Balance-Update
        /// </summary>
        public DateTime? LastBalanceUpdate { get; set; }

        /// <summary>
        /// Letzte Trading-Aktivität
        /// </summary>
        public DateTime? LastTradeAt { get; set; }

        /// <summary>
        /// Agent Name (optional, vom User)
        /// </summary>
        [MaxLength(100)]
        public string? AgentName { get; set; }

        /// <summary>
        /// Letztes Trading-Signal (BUY, SELL, HOLD)
        /// </summary>
        [MaxLength(10)]
        public string? LastSignalAction { get; set; }

        /// <summary>
        /// Konfidenz des letzten Signals (0-100%)
        /// </summary>
        public int? LastSignalConfidence { get; set; }

        /// <summary>
        /// Zeitpunkt des letzten Signals
        /// </summary>
        public DateTime? LastSignalAt { get; set; }

        /// <summary>
        /// Grund/Erklärung für das letzte Signal
        /// </summary>
        [MaxLength(500)]
        public string? LastSignalReason { get; set; }
    }
}
