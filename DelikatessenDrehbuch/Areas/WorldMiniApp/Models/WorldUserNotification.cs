using System.ComponentModel.DataAnnotations;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    // Lightweight, app-scoped notification entity (not a full notification system).
    // Stored in SQL so it survives app restarts and works in Azure without Redis.
    public sealed class WorldUserNotification
    {
        public int Id { get; set; }

        // Recipient in WorldMiniApp (cookie: WorldMiniAppUserHash).
        [MaxLength(256)]
        public string UserHash { get; set; } = string.Empty;

        // UI icon key (e.g. Bootstrap icon class "bi-bell", "bi-stars").
        [MaxLength(64)]
        public string Icon { get; set; } = "bi-bell";

        // Who/what sent it (system, ai, marketplace, user:xyz, etc.).
        [MaxLength(128)]
        public string Sender { get; set; } = "system";

        // Human readable content.
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        // Where to go when the user clicks (relative URL preferred).
        [MaxLength(600)]
        public string? Href { get; set; }

        // Stable key used to aggregate repeated events on the same target.
        [MaxLength(300)]
        public string? NotificationKey { get; set; }

        // Logical notification type (like-video, comment-reply, etc.).
        [MaxLength(64)]
        public string? EventType { get; set; }

        // Latest actor that triggered the aggregated notification.
        [MaxLength(128)]
        public string? LatestActorName { get; set; }

        // Optional short context, for example a comment excerpt or recipe title.
        [MaxLength(400)]
        public string? ContextText { get; set; }

        // Total number of aggregated events represented by this notification row.
        public int AggregateCount { get; set; } = 1;

        // Number of unseen events inside this aggregated row.
        public int UnreadEventCount { get; set; } = 1;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsSeen { get; set; }

        public DateTime? SeenAtUtc { get; set; }
    }
}
