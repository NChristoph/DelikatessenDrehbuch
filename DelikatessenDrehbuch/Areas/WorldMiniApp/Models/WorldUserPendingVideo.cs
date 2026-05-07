using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Tracks videos that are pending Bunny Stream transcoding.
    /// Used to send notifications when videos are ready.
    /// </summary>
    [Table("WorldUserPendingVideos")]
    public class WorldUserPendingVideo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PostingId { get; set; }

        [Required]
        [MaxLength(100)]
        public string VideoGuid { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string UserHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual WorldUserPosting? Posting { get; set; }
    }
}
