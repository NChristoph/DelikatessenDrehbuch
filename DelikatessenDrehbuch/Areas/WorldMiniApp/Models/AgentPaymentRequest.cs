using System.ComponentModel.DataAnnotations;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Agent-Payment Request - Agent sendet WLD/WETH an User
    /// </summary>
    public class AgentPaymentRequest
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Empfänger (User) Hash
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string RecipientUserHash { get; set; } = string.Empty;

        /// <summary>
        /// Empfänger Wallet-Adresse
        /// </summary>
        [MaxLength(100)]
        public string? RecipientWalletAddress { get; set; }

        /// <summary>
        /// Token-Symbol (WLD, WETH, USDC)
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string TokenSymbol { get; set; } = "WLD";

        /// <summary>
        /// Betrag (in Token-Einheiten, z.B. 5.0 WLD)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Beschreibung/Grund für Payment
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Eindeutige Referenz für diese Zahlung
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        /// <summary>
        /// Status: pending, confirmed, failed, cancelled
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Transaction Hash (nach Bestätigung)
        /// </summary>
        [MaxLength(100)]
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Agent/System, das die Zahlung initiiert hat
        /// </summary>
        [MaxLength(100)]
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// Erstellt am
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bestätigt am
        /// </summary>
        public DateTime? ConfirmedAt { get; set; }

        /// <summary>
        /// Läuft ab am (optional)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// DTO für Agent Payment Initiation
    /// </summary>
    public class InitiateAgentPaymentRequest
    {
        [Required]
        public string RecipientUserHash { get; set; } = string.Empty;

        [Required]
        public string TokenSymbol { get; set; } = "WLD";

        [Required]
        [Range(0.0001, 10000)]
        public decimal Amount { get; set; }

        [MaxLength(500)]
        public string Description { get; set; } = "Agent Reward";

        public string? InitiatedBy { get; set; }
    }

    /// <summary>
    /// DTO für Payment Confirmation
    /// </summary>
    public class ConfirmAgentPaymentRequest
    {
        [Required]
        public string Reference { get; set; } = string.Empty;

        [Required]
        public string TransactionHash { get; set; } = string.Empty;

        public string Status { get; set; } = "confirmed";
    }
}
