using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    /// <summary>
    /// Lese-/Status-Endpunkte für die In-App-Benachrichtigungen des Feeds.
    /// Aus <see cref="FeedController"/> herausgelöst; behält via Attribut-Route
    /// die ursprünglichen "/WorldMiniApp/Feed/&lt;Action&gt;"-Pfade.
    /// (Das Schreiben der Notifications passiert über
    /// WorldMiniAppBaseController.UpsertInteractionNotificationAsync.)
    /// </summary>
    [Area("WorldMiniApp")]
    [Route("WorldMiniApp/Feed/[action]")]
    public class FeedNotificationsController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;

        public FeedNotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(string? filter = null, int limit = 50, CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, message = "Nicht angemeldet." });

            var query = _context.WorldUserNotifications
                .AsNoTracking()
                .Where(x => x.UserHash == userHash);

            if (filter == "unread")
                query = query.Where(x => !x.IsSeen);

            var notifications = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(Math.Min(limit, 100))
                .Select(x => new
                {
                    id = x.Id,
                    icon = x.Icon,
                    sender = x.Sender,
                    description = x.Description,
                    href = x.Href,
                    createdAtUtc = x.CreatedAtUtc,
                    isSeen = x.IsSeen,
                    seenAtUtc = x.SeenAtUtc,
                    aggregateCount = x.AggregateCount > 0 ? x.AggregateCount : 1,
                    unreadEventCount = x.UnreadEventCount > 0 ? x.UnreadEventCount : (x.IsSeen ? 0 : 1)
                })
                .ToListAsync(cancellationToken);

            return Json(new { success = true, notifications });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationAsRead([FromForm] int notificationId, CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, message = "Nicht angemeldet." });

            var notification = await _context.WorldUserNotifications
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserHash == userHash, cancellationToken);

            if (notification == null)
                return Json(new { success = false, message = "Benachrichtigung nicht gefunden." });

            if (!notification.IsSeen)
            {
                notification.IsSeen = true;
                notification.SeenAtUtc = DateTime.UtcNow;
                notification.UnreadEventCount = 0;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsAsRead(CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, message = "Nicht angemeldet." });

            var unreadNotifications = await _context.WorldUserNotifications
                .Where(x => x.UserHash == userHash && !x.IsSeen)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var notification in unreadNotifications)
            {
                notification.IsSeen = true;
                notification.SeenAtUtc = now;
                notification.UnreadEventCount = 0;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true, count = unreadNotifications.Count });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadNotificationCount(CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { count = 0 });

            var count = await _context.WorldUserNotifications
                .AsNoTracking()
                .Where(x => x.UserHash == userHash && !x.IsSeen)
                .SumAsync(x => (int?)x.UnreadEventCount, cancellationToken) ?? 0;

            return Json(new { count });
        }
    }
}
