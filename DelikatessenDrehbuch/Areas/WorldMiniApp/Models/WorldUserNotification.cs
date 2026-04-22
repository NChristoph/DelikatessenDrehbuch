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

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsSeen { get; set; }

        public DateTime? SeenAtUtc { get; set; }
    }
}

