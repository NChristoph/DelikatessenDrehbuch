using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Speichert historische Token-Preise für Trend-Analyse
    /// </summary>
    public class WorldTokenPrice
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Token In Address (z.B. WLD)
        /// </summary>
        [Required]
        [MaxLength(42)]
        public string TokenIn { get; set; } = string.Empty;

        /// <summary>
        /// Token Out Address (z.B. WETH)
        /// </summary>
        [Required]
        [MaxLength(42)]
        public string TokenOut { get; set; } = string.Empty;

        /// <summary>
        /// Preis (1 TokenIn = X TokenOut)
        /// </summary>
        [Column(TypeName = "decimal(18,9)")]
        public decimal Price { get; set; }

        /// <summary>
        /// Zeitpunkt der Preis-Abfrage
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
