using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class AdminController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly WorldMiniApp.Services.IWildCoinService _coinService;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger, WorldMiniApp.Services.IWildCoinService coinService)
        {
            _context = context;
            _logger = logger;
            _coinService = coinService;
        }

        private string? GetCurrentUserHash()
        {
            // Identität aus dem signierten Auth-Cookie (Claim), NICHT aus dem
            // client-kontrollierten Anzeige-Cookie.
            var resolved = WorldMiniApp.Services.WorldMiniAppUserHashHelper.Resolve(HttpContext);
            return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
        }

        private bool IsAdmin()
        {
            var userHash = GetCurrentUserHash();
            return !string.IsNullOrWhiteSpace(userHash) &&
                   !string.IsNullOrWhiteSpace(SuperUserHash) &&
                   userHash.Equals(SuperUserHash, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAdminAuthenticated()
        {
            // Prüfe erst ob User überhaupt Admin ist
            if (!IsAdmin())
            {
                return false;
            }

            // Prüfe ob Admin bereits eingeloggt ist (Session)
            var isAuthenticated = HttpContext.Session.GetString("WorldMiniAppAdminAuthenticated");
            if (isAuthenticated != "true")
            {
                return false;
            }

            // Prüfe ob Admin-Session abgelaufen ist
            var expiryString = HttpContext.Session.GetString("WorldMiniAppAdminExpiry");
            if (!string.IsNullOrEmpty(expiryString))
            {
                if (DateTime.TryParse(expiryString, out var expiryTime))
                {
                    if (DateTime.UtcNow > expiryTime)
                    {
                        // Session abgelaufen
                        HttpContext.Session.Remove("WorldMiniAppAdminAuthenticated");
                        HttpContext.Session.Remove("WorldMiniAppAdminExpiry");
                        return false;
                    }
                }
            }

            return true;
        }

        private string GetAdminPassword()
        {
            return Configuration["WorldMiniApp:AdminPassword"] ?? string.Empty;
        }

        // GET: Admin/Login
        public IActionResult Login(string returnUrl = "")
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            // Wenn bereits eingeloggt, weiterleiten
            if (IsAdminAuthenticated())
            {
                if (!string.IsNullOrWhiteSpace(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(CreatorManager));
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Admin/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string password, string returnUrl = "")
        {
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
            }

            var configuredPasswordHash = GetAdminPassword();

            if (string.IsNullOrWhiteSpace(configuredPasswordHash))
            {
                TempData["Error"] = "Admin-Passwort ist nicht konfiguriert.";
                return View();
            }

            // Prüfe ob Passwort korrekt ist (BCrypt-Vergleich)
            bool isPasswordValid = false;
            try
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(password, configuredPasswordHash);
            }
            catch (Exception)
            {
                // Falls Hash ungültig ist, versuche Klartext-Vergleich (für Migration)
                isPasswordValid = password == configuredPasswordHash;
            }

            if (isPasswordValid)
            {
                // Admin-Session setzen (gültig für 2 Stunden)
                HttpContext.Session.SetString("WorldMiniAppAdminAuthenticated", "true");

                // Setze Ablaufzeit für Admin-Session (2 Stunden)
                var expiryTime = DateTime.UtcNow.AddHours(2);
                HttpContext.Session.SetString("WorldMiniAppAdminExpiry", expiryTime.ToString("o"));

                TempData["Success"] = "Erfolgreich als Admin eingeloggt!";
                _logger.LogInformation("Admin logged in: {UserHash}", GetCurrentUserHash());

                if (!string.IsNullOrWhiteSpace(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(CreatorManager));
            }
            else
            {
                TempData["Error"] = "Falsches Passwort.";
                _logger.LogWarning("Failed admin login attempt: {UserHash}", GetCurrentUserHash());
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }
        }

        // POST: Admin/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("WorldMiniAppAdminAuthenticated");
            TempData["Success"] = "Erfolgreich ausgeloggt.";
            _logger.LogInformation("Admin logged out: {UserHash}", GetCurrentUserHash());
            return RedirectToAction("MyProfile", "Feed", new { area = "WorldMiniApp", userHash = GetCurrentUserHash() });
        }

        // GET: Admin/CreatorManager
        public async Task<IActionResult> CreatorManager()
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Url.Action(nameof(CreatorManager)) });
            }

            var creators = await _context.WorldAppUser
                .OrderByDescending(u => u.CreatedAt)
                .Take(200)
                .ToListAsync();

            var model = new CreatorManagerViewModel
            {
                Creators = creators,
                IsAdmin = true,
                CurrentUserHash = GetCurrentUserHash()
            };

            return View(model);
        }

        // POST: Admin/CreateCreator
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCreator(string userName, string? bio, bool isOrbVerified)
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                TempData["Error"] = "Benutzername ist erforderlich.";
                return RedirectToAction(nameof(CreatorManager));
            }

            // Generiere einen eindeutigen Hash für den Creator
            var randomHash = GenerateRandomHash();

            var creator = new WorldAppUser
            {
                UserHash = randomHash,
                UserName = userName.Trim(),
                Bio = bio?.Trim(),
                IsVerified = isOrbVerified ? "orb" : "device",
                WildCoinBalance = 0
                // CreatedAt wird vom DB-Default gesetzt
                // Lastlogin bleibt NULL
            };

            try
            {
                await _context.WorldAppUser.AddAsync(creator);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Creator '{userName}' erfolgreich erstellt! Hash: {randomHash}";
                _logger.LogInformation("Admin created creator: {UserName} with hash {Hash}", userName, randomHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Erstellen des Creators {UserName}", userName);
                // Keine rohe ex.Message in der UI – Details nur im Log.
                TempData["Error"] = "Fehler beim Erstellen des Creators.";
            }

            return RedirectToAction(nameof(CreatorManager));
        }

        // POST: Admin/LoginAsCreator
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginAsCreator(string targetHash)
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            if (string.IsNullOrWhiteSpace(targetHash))
            {
                TempData["Error"] = "Ungültiger Creator Hash.";
                return RedirectToAction(nameof(CreatorManager));
            }

            // Speichere den ursprünglichen Admin-Hash für später
            Response.Cookies.Append("AdminOriginalHash", GetCurrentUserHash() ?? "", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromHours(24)
            });

            // Setze den Creator-Hash als aktuellen User (signiertes Auth-Cookie)
            await WorldMiniApp.Services.WorldMiniAppUserHashHelper.SignInAsync(HttpContext, targetHash, isPersistent: false);

            TempData["Success"] = $"Du bist jetzt als Creator {targetHash.Substring(0, 10)}... angemeldet!";
            _logger.LogInformation("Admin logged in as creator: {Hash}", targetHash);

            return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
        }

        // POST: Admin/ReturnToAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnToAdmin()
        {
            var originalHash = Request.Cookies["AdminOriginalHash"];

            if (!string.IsNullOrWhiteSpace(originalHash))
            {
                // Setze Admin-Hash zurück (signiertes Auth-Cookie)
                await WorldMiniApp.Services.WorldMiniAppUserHashHelper.SignInAsync(HttpContext, originalHash, isPersistent: false);

                Response.Cookies.Delete("AdminOriginalHash");

                TempData["Success"] = "Zurück zum Admin-Account!";
            }

            return RedirectToAction(nameof(CreatorManager));
        }

        // POST: Admin/DeleteCreator
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCreator(string userHash)
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            var creator = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            if (creator == null)
            {
                TempData["Error"] = "Creator nicht gefunden.";
                return RedirectToAction(nameof(CreatorManager));
            }

            try
            {
                _context.WorldAppUser.Remove(creator);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Creator '{creator.UserName}' wurde gelöscht.";
                _logger.LogInformation("Admin deleted creator: {UserName} with hash {Hash}", creator.UserName, userHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Löschen des Creators {UserHash}", userHash);
                // Keine rohe ex.Message in der UI – Details nur im Log.
                TempData["Error"] = "Fehler beim Löschen des Creators.";
            }

            return RedirectToAction(nameof(CreatorManager));
        }

        // GET: Admin/ReportedContent
        public async Task<IActionResult> ReportedContent(string status = "all")
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            var query = _context.WorldUserReports
                .Include(r => r.Posting)
                    .ThenInclude(p => p.Recipe)
                .AsQueryable();

            if (status != "all")
            {
                query = query.Where(r => r.Status == status);
            }

            var reports = await query
                .OrderByDescending(r => r.CreatedAt)
                .Take(100)
                .Select(r => new ReportedContentViewModel
                {
                    ReportId = r.Id,
                    PostingId = r.PostingId,
                    PostTitle = r.Posting != null && r.Posting.Recipe != null ? r.Posting.Recipe.Title : "Unbekannt",
                    PostThumbnail = r.Posting != null ? r.Posting.ThumbnailUrl : "",
                    ReporterHash = r.ReporterHash,
                    Reason = r.Reason,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt,
                    Status = r.Status,
                    ReviewedAt = r.ReviewedAt,
                    ReviewedBy = r.ReviewedBy,
                    ReviewNotes = r.ReviewNotes,
                    ReportCount = r.Posting != null ? r.Posting.ReportCount : 0,
                    IsHidden = r.Posting != null && r.Posting.IsHiddenPendingReview
                })
                .ToListAsync();

            var model = new ReportedContentListViewModel
            {
                Reports = reports,
                CurrentFilter = status,
                IsAdmin = true,
                CurrentUserHash = GetCurrentUserHash()
            };

            return View(model);
        }

        // POST: Admin/ReviewReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewReport(int reportId, string action, string? notes)
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            var report = await _context.WorldUserReports
                .Include(r => r.Posting)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                TempData["Error"] = "Report nicht gefunden.";
                return RedirectToAction(nameof(ReportedContent));
            }

            try
            {
                report.ReviewedAt = DateTime.UtcNow;
                report.ReviewedBy = GetCurrentUserHash();
                report.ReviewNotes = notes;

                if (action == "approve")
                {
                    report.Status = "action_taken";
                    if (report.Posting != null)
                    {
                        report.Posting.IsHiddenPendingReview = true;
                    }
                    TempData["Success"] = "Report genehmigt und Inhalt versteckt.";
                }
                else if (action == "dismiss")
                {
                    report.Status = "dismissed";
                    TempData["Success"] = "Report abgelehnt.";
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Admin reviewed report {ReportId}: {Action}", reportId, action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Review des Reports {ReportId}", reportId);
                // Keine rohe ex.Message in der UI – Details nur im Log.
                TempData["Error"] = "Fehler beim Bearbeiten des Reports.";
            }

            return RedirectToAction(nameof(ReportedContent));
        }

        // POST: Admin/DeleteReportedPosting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReportedPosting(int postingId)
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            var posting = await _context.WorldUserPosting.FirstOrDefaultAsync(p => p.Id == postingId);
            if (posting == null)
            {
                TempData["Error"] = "Posting nicht gefunden.";
                return RedirectToAction(nameof(ReportedContent));
            }

            try
            {
                _context.WorldUserPosting.Remove(posting);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Posting gelöscht.";
                _logger.LogInformation("Admin deleted reported posting: {PostingId}", postingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Löschen des Postings {PostingId}", postingId);
                // Keine rohe ex.Message in der UI – Details nur im Log.
                TempData["Error"] = "Fehler beim Löschen des Postings.";
            }

            return RedirectToAction(nameof(ReportedContent));
        }

        // GET: Admin/Payouts — offene Verkäufer-Auszahlungsanträge (Modell B Cash-out)
        public async Task<IActionResult> Payouts()
        {
            if (!IsAdminAuthenticated())
                return RedirectToAction(nameof(Login), new { returnUrl = Url.Action(nameof(Payouts)) });

            ViewData["IsAdmin"] = true;
            ViewData["CurrentUserHash"] = GetCurrentUserHash();
            var pending = await _coinService.GetPendingPayoutsAsync();
            return View(pending);
        }

        // POST: Admin/MarkPayoutPaid — nach manueller On-Chain-Auszahlung den TxHash eintragen
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPayoutPaid(int payoutId, string txHash)
        {
            if (!IsAdminAuthenticated())
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });

            if (string.IsNullOrWhiteSpace(txHash))
            {
                TempData["Error"] = "TxHash erforderlich.";
                return RedirectToAction(nameof(Payouts));
            }

            var ok = await _coinService.MarkPayoutPaidAsync(payoutId, txHash, GetCurrentUserHash() ?? "admin");
            TempData[ok ? "Success" : "Error"] = ok ? "Auszahlung als bezahlt markiert." : "Antrag nicht gefunden.";
            return RedirectToAction(nameof(Payouts));
        }

        // POST: Admin/RejectPayout — ablehnen, reserviertes Guthaben wird zurückgebucht
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayout(int payoutId, string? note)
        {
            if (!IsAdminAuthenticated())
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });

            var ok = await _coinService.RejectPayoutAsync(payoutId, note ?? "abgelehnt", GetCurrentUserHash() ?? "admin");
            TempData[ok ? "Success" : "Error"] = ok ? "Auszahlung abgelehnt, Guthaben zurückgebucht." : "Antrag nicht gefunden.";
            return RedirectToAction(nameof(Payouts));
        }

        private static string GenerateRandomHash()
        {
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            var hash = BitConverter.ToString(randomBytes).Replace("-", "").ToLowerInvariant();
            return $"0x{hash}";
        }

        // GET: Admin/GeneratePasswordHash (Hilfstool zum Hashen von Passwörtern)
        [HttpGet]
        public IActionResult GeneratePasswordHash()
        {
            // Nur für SuperUser zugänglich (keine Session-Prüfung nötig)
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
            }

            return View();
        }

        // POST: Admin/GeneratePasswordHash
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GeneratePasswordHash(string plainPassword)
        {
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
            }

            if (string.IsNullOrWhiteSpace(plainPassword))
            {
                TempData["Error"] = "Bitte geben Sie ein Passwort ein.";
                return View();
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(plainPassword);
            ViewData["HashedPassword"] = hashedPassword;
            ViewData["PlainPassword"] = plainPassword;

            return View();
        }

        // GET: Admin/GetCreatorRecipes?creatorHash=...
        [HttpGet]
        public async Task<IActionResult> GetCreatorRecipes(string creatorHash)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, error = "Unauthorized" });
            }

            var recipes = await _context.WorldUserPosting
                .Include(p => p.Recipe)
                .Where(p => p.CreatorId == creatorHash && p.Recipe != null)
                .OrderByDescending(p => p.CreationTime)
                .Select(p => new
                {
                    recipeId = p.Recipe!.Id,
                    title = p.Recipe.Title,
                    thumbnail = p.ThumbnailUrl ?? p.Source,
                    createdAt = p.CreationTime,
                    postingId = p.Id
                })
                .ToListAsync();

            return Json(new { success = true, recipes });
        }

        // POST: Admin/DeleteRecipe
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRecipe(int recipeId)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, error = "Unauthorized" });
            }

            try
            {
                // 1. WorldUserPosting löschen (sofern vorhanden)
                var postings = await _context.WorldUserPosting
                    .Include(p => p.Recipe)
                    .Where(p => p.Recipe != null && p.Recipe.Id == recipeId)
                    .ToListAsync();

                if (postings.Any())
                {
                    _context.WorldUserPosting.RemoveRange(postings);
                }

                // 2. RecipeBaseDataImage löschen
                var images = await _context.RecipeBaseDataImage
                    .Include(i => i.Recipe)
                    .Where(i => i.Recipe != null && i.Recipe.Id == recipeId)
                    .ToListAsync();

                if (images.Any())
                {
                    _context.RecipeBaseDataImage.RemoveRange(images);
                }

                // 3. RecipeBaseData löschen
                var recipe = await _context.RecipeBaseData.FindAsync(recipeId);
                if (recipe != null)
                {
                    _context.RecipeBaseData.Remove(recipe);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin deleted recipe {RecipeId}", recipeId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recipe {RecipeId}", recipeId);
                // Kein ex.Message an den Client – Details nur im Log.
                return Json(new { success = false, error = "Fehler beim Löschen des Rezepts." });
            }
        }
    }

    public class CreatorManagerViewModel
    {
        public List<WorldAppUser> Creators { get; set; } = new();
        public bool IsAdmin { get; set; }
        public string? CurrentUserHash { get; set; }
    }

    public class ReportedContentListViewModel
    {
        public List<ReportedContentViewModel> Reports { get; set; } = new();
        public string CurrentFilter { get; set; } = "all";
        public bool IsAdmin { get; set; }
        public string? CurrentUserHash { get; set; }
    }

    public class ReportedContentViewModel
    {
        public int ReportId { get; set; }
        public int PostingId { get; set; }
        public string PostTitle { get; set; } = string.Empty;
        public string PostThumbnail { get; set; } = string.Empty;
        public string ReporterHash { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }
        public string? ReviewNotes { get; set; }
        public int ReportCount { get; set; }
        public bool IsHidden { get; set; }
    }
}
