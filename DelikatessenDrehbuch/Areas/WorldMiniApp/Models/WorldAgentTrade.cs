using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Einzel-Trade vom Trading Agent
    /// </summary>
    public class WorldAgentTrade
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Referenz zum Trading Agent
        /// </summary>
        [Required]
        public int AgentId { get; set; }

        [ForeignKey(nameof(AgentId))]
        public WorldTradingAgent? Agent { get; set; }

        /// <summary>
        /// Transaction Hash auf der Blockchain
        /// </summary>
        [MaxLength(66)]
        public string? TxHash { get; set; }

        /// <summary>
        /// Trade Type
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string TradeType { get; set; } = string.Empty; // Buy, Sell, Swap

        /// <summary>
        /// From Token Address
        /// </summary>
        [Required]
        [MaxLength(42)]
        public string FromToken { get; set; } = string.Empty;

        /// <summary>
        /// To Token Address
        /// </summary>
        [Required]
        [MaxLength(42)]
        public string ToToken { get; set; } = string.Empty;

        /// <summary>
        /// From Amount
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal FromAmount { get; set; }

        /// <summary>
        /// To Amount (erhalten)
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal ToAmount { get; set; }

        /// <summary>
        /// Gas Fees (in ETH/native token)
        /// </summary>
        [Column(TypeName = "decimal(18,9)")]
        public decimal GasFee { get; set; }

        /// <summary>
        /// Profit/Loss für diesen Trade (in WLD equivalent)
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal ProfitLossWLD { get; set; }

        /// <summary>
        /// Trade Status
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed

        /// <summary>
        /// Fehler-Nachricht bei Failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Zeitpunkt des Trades
        /// </summary>
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// DEX verwendet (Uniswap, Sushiswap, etc.)
        /// </summary>
        [MaxLength(50)]
        public string? DexUsed { get; set; }
    }
}
