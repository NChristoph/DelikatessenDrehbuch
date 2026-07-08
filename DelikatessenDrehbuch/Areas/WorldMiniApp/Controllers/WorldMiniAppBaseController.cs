using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public abstract class WorldMiniAppBaseController : Controller
    {
        protected IConfiguration Configuration =>
            HttpContext.RequestServices.GetRequiredService<IConfiguration>();

        protected string SuperUserHash => Configuration["WorldMiniApp:SuperUserHash"] ?? string.Empty;

        protected string ResolveUserHash(string userHash)
        {
            return WorldMiniAppUserHashHelper.Resolve(HttpContext, userHash);
        }

        // Parameterlose Variante für Controller, die keinen expliziten Hash übergeben.
        // Identität kommt ohnehin nur aus dem Auth-Cookie (Claim).
        protected string ResolveUserHash()
        {
            return WorldMiniAppUserHashHelper.Resolve(HttpContext);
        }

        protected async Task<bool> IsCreatorAllowedAsync(ApplicationDbContext context, string userHash)
        {
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            if (userHash == SuperUserHash)
            {
                return true;
            }

            // While the operator is impersonating a creator ("Anmelden als" in the admin panel), the
            // session hash is the channel's hash — which may be "device". Only the SuperUser can start
            // impersonation, and doing so sets AdminOriginalHash = SuperUserHash (HttpOnly, server-side).
            // Treat that as allowed so building a channel (upload + adding ingredients) works regardless
            // of the placeholder creator's orb status.
            var adminOriginalHash = HttpContext?.Request?.Cookies["AdminOriginalHash"];
            if (!string.IsNullOrWhiteSpace(adminOriginalHash)
                && !string.IsNullOrWhiteSpace(SuperUserHash)
                && adminOriginalHash.Equals(SuperUserHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var user = await context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            return user?.IsVerified == "orb";
        }

        // ---------------------------------------------------------------------
        // Geteilte Kommentar-/Moderations-Helfer (genutzt von FeedController +
        // FeedCommentsController). Die DbContext-Instanz wird – wie bei
        // IsCreatorAllowedAsync – als Parameter übergeben, weil die Basisklasse
        // selbst keinen eigenen injizierten Context hält.
        // ---------------------------------------------------------------------

        protected async Task<bool> CanWriteCommentsAsync(ApplicationDbContext context, string userHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userHash))
                return false;

            if (IsCommentModerator(userHash))
                return true;

            var user = await context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserHash == userHash, cancellationToken);

            return string.Equals(user?.IsVerified, "orb", StringComparison.OrdinalIgnoreCase);
        }

        protected bool IsCommentModerator(string userHash)
        {
            if (string.IsNullOrWhiteSpace(userHash))
                return User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

            return string.Equals(userHash, SuperUserHash, StringComparison.OrdinalIgnoreCase)
                || GetConfiguredCommentModeratorHashes().Contains(userHash, StringComparer.OrdinalIgnoreCase)
                || (User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin"));
        }

        protected bool IsCommentModeratorAuthenticated(string userHash)
        {
            // Prüfe erst ob User überhaupt Moderator ist
            if (!IsCommentModerator(userHash))
            {
                return false;
            }

            // Wenn User ASP.NET Identity Admin ist, keine weitere Prüfung nötig
            if (User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
            {
                return true;
            }

            // Prüfe Admin-Session (für WorldMiniApp-SuperUser)
            var isAuthenticated = HttpContext.Session.GetString("WorldMiniAppAdminAuthenticated");
            if (isAuthenticated != "true")
            {
                return false;
            }

            // Prüfe ob Session abgelaufen ist
            var expiryString = HttpContext.Session.GetString("WorldMiniAppAdminExpiry");
            if (!string.IsNullOrEmpty(expiryString))
            {
                if (DateTime.TryParse(expiryString, out var expiryTime))
                {
                    if (DateTime.UtcNow > expiryTime)
                    {
                        HttpContext.Session.Remove("WorldMiniAppAdminAuthenticated");
                        HttpContext.Session.Remove("WorldMiniAppAdminExpiry");
                        return false;
                    }
                }
            }

            return true;
        }

        private IReadOnlyList<string> GetConfiguredCommentModeratorHashes()
        {
            var values = Configuration
                .GetSection("WorldMiniApp:AdminUserHashes")
                .Get<string[]>();

            if (values != null && values.Length > 0)
            {
                return values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            var raw = Configuration["WorldMiniApp:AdminUserHashes"];
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            return raw
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // ---------------------------------------------------------------------
        // Geteilte Notification-Engine (genutzt von Like- + Kommentar-Benachrichtigungen).
        // ---------------------------------------------------------------------

        protected async Task UpsertInteractionNotificationAsync(
            ApplicationDbContext context,
            string recipientUserHash,
            string sender,
            string icon,
            string href,
            string notificationKey,
            string actorName,
            string? contextText,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(recipientUserHash) || string.IsNullOrWhiteSpace(notificationKey))
                return;

            var normalizedActorName = string.IsNullOrWhiteSpace(actorName) ? "Jemand" : actorName.Trim();
            var normalizedContextText = NormalizeNotificationContext(contextText);

            var notification = await context.WorldUserNotifications
                .FirstOrDefaultAsync(x => x.UserHash == recipientUserHash && x.NotificationKey == notificationKey, cancellationToken);

            if (notification == null)
            {
                notification = new WorldUserNotification
                {
                    UserHash = recipientUserHash,
                    Icon = icon,
                    Sender = sender,
                    EventType = sender,
                    NotificationKey = notificationKey,
                    LatestActorName = normalizedActorName,
                    ContextText = normalizedContextText,
                    AggregateCount = 1,
                    UnreadEventCount = 1,
                    Href = href,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsSeen = false,
                    SeenAtUtc = null
                };

                notification.Description = BuildAggregatedNotificationDescription(sender, notification.LatestActorName, notification.AggregateCount, notification.ContextText);
                await context.WorldUserNotifications.AddAsync(notification, cancellationToken);
            }
            else
            {
                notification.Icon = icon;
                notification.Sender = sender;
                notification.EventType = sender;
                notification.Href = href;
                notification.LatestActorName = normalizedActorName;
                notification.ContextText = normalizedContextText;
                notification.AggregateCount = Math.Max(1, notification.AggregateCount) + 1;
                notification.UnreadEventCount = Math.Max(0, notification.UnreadEventCount) + 1;
                notification.CreatedAtUtc = DateTime.UtcNow;
                notification.IsSeen = false;
                notification.SeenAtUtc = null;
                notification.Description = BuildAggregatedNotificationDescription(sender, notification.LatestActorName, notification.AggregateCount, notification.ContextText);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static string NormalizeNotificationContext(string? contextText)
        {
            var normalized = (contextText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            normalized = normalized.Replace("\r", " ").Replace("\n", " ");
            return normalized.Length <= 400
                ? normalized
                : $"{normalized[..397].TrimEnd()}...";
        }

        private static string BuildAggregatedNotificationDescription(string sender, string? actorName, int aggregateCount, string? contextText)
        {
            var normalizedSender = (sender ?? string.Empty).Trim().ToLowerInvariant();
            var leadActor = string.IsNullOrWhiteSpace(actorName) ? "Jemand" : actorName.Trim();
            var safeCount = Math.Max(1, aggregateCount);
            var moreCount = safeCount - 1;
            var hasMany = moreCount > 0;

            var description = normalizedSender switch
            {
                "like-video" => hasMany
                    ? $"Dein Video gefaellt {leadActor} und {moreCount} weiteren Personen."
                    : $"Dein Video gefaellt {leadActor}.",
                "comment-video" => hasMany
                    ? $"{leadActor} und {moreCount} weitere Personen haben dein Video kommentiert."
                    : $"{leadActor} hat dein Video kommentiert.",
                "comment-reply" => hasMany
                    ? $"{leadActor} und {moreCount} weitere Personen haben auf deinen Kommentar geantwortet."
                    : $"{leadActor} hat auf deinen Kommentar geantwortet.",
                "like-comment" => hasMany
                    ? $"Dein Kommentar gefaellt {leadActor} und {moreCount} weiteren Personen."
                    : $"Dein Kommentar gefaellt {leadActor}.",
                "dislike-comment" => hasMany
                    ? $"{leadActor} und {moreCount} weitere Personen haben deinen Kommentar negativ bewertet."
                    : $"{leadActor} hat deinen Kommentar negativ bewertet.",
                _ => hasMany
                    ? $"{leadActor} und {moreCount} weitere Personen haben reagiert."
                    : $"{leadActor} hat reagiert."
            };

            var normalizedContext = NormalizeNotificationContext(contextText);
            if (string.IsNullOrWhiteSpace(normalizedContext))
                return description;

            return $"{description}\n\"{normalizedContext}\"";
        }
    }
}
