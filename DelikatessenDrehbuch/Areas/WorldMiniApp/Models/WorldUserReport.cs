using System.ComponentModel.DataAnnotations;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Tracks user reports on content (videos/recipes).
    /// Used for content moderation and spam protection.
    /// </summary>
    public class WorldUserReport
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(128)]
        public string ReporterHash { get; set; } = string.Empty;

        [Required]
        public int PostingId { get; set; }

        public virtual WorldUserPosting? Posting { get; set; }

        [Required]
        [MaxLength(50)]
        public string Reason { get; set; } = string.Empty; // "spam", "inappropriate", "copyright", "other"

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Moderation status
        [MaxLength(20)]
        public string Status { get; set; } = "pending"; // "pending", "reviewed", "dismissed", "action_taken"

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(128)]
        public string? ReviewedBy { get; set; }

        [MaxLength(500)]
        public string? ReviewNotes { get; set; }
    }
}
