using System.ComponentModel.DataAnnotations;
using WorldMiniApp.Services;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Speichert User-Interaktionen für Feed-Algorithmus und ML-Training
    /// </summary>
    public class WorldUserInteraction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(128)]
        public string UserHash { get; set; } = string.Empty;

        [Required]
        public int PostingId { get; set; }

        [Required]
        public InteractionType InteractionType { get; set; }

        /// <summary>
        /// View-Duration in Sekunden (nur bei View/WatchComplete)
        /// </summary>
        public double? Duration { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual WorldUserPosting? Posting { get; set; }
        public virtual WorldAppUser? User { get; set; }
    }
}
