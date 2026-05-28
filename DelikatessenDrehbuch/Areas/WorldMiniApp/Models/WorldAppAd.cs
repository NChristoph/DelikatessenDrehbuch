using System.ComponentModel.DataAnnotations;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldAppAd
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Image or Video URL for the ad content
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string MediaUrl { get; set; } = string.Empty;

        /// <summary>
        /// Thumbnail for video ads
        /// </summary>
        [MaxLength(1000)]
        public string? ThumbnailUrl { get; set; }

        /// <summary>
        /// Is this a video ad (true) or image ad (false)?
        /// </summary>
        public bool IsVideo { get; set; }

        /// <summary>
        /// Target URL when user clicks the ad (can be external or internal)
        /// </summary>
        [MaxLength(1000)]
        public string? TargetUrl { get; set; }

        /// <summary>
        /// Call-to-action button text (e.g., "Learn More", "Shop Now")
        /// </summary>
        [MaxLength(50)]
        public string? CtaText { get; set; }

        /// <summary>
        /// Is this ad currently active?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Ad starts showing from this date
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Ad stops showing after this date
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Show ad every X videos (e.g., 5 = show ad after every 5 videos)
        /// </summary>
        public int ShowEveryXVideos { get; set; } = 5;

        /// <summary>
        /// Priority (higher = shown more often if multiple active ads)
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Total number of times this ad was viewed (impressions)
        /// </summary>
        public int ViewCount { get; set; } = 0;

        /// <summary>
        /// Total number of times this ad was clicked
        /// </summary>
        public int ClickCount { get; set; } = 0;

        /// <summary>
        /// Target audience: "all", "orb", "device" (empty = all)
        /// </summary>
        [MaxLength(20)]
        public string? TargetAudience { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// User hash of the creator (for tracking who created the ad)
        /// </summary>
        [MaxLength(200)]
        public string? CreatedByUserHash { get; set; }
    }

    /// <summary>
    /// Detailed tracking for each ad impression
    /// </summary>
    public class WorldAppAdImpression
    {
        public int Id { get; set; }

        public int AdId { get; set; }
        public WorldAppAd? Ad { get; set; }

        /// <summary>
        /// User who saw the ad (can be null for anonymous)
        /// </summary>
        [MaxLength(200)]
        public string? UserHash { get; set; }

        /// <summary>
        /// Was the ad clicked?
        /// </summary>
        public bool WasClicked { get; set; }

        /// <summary>
        /// Timestamp when ad was viewed
        /// </summary>
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when ad was clicked (null if not clicked)
        /// </summary>
        public DateTime? ClickedAt { get; set; }

        /// <summary>
        /// User's device/platform info
        /// </summary>
        [MaxLength(200)]
        public string? UserAgent { get; set; }
    }
}
