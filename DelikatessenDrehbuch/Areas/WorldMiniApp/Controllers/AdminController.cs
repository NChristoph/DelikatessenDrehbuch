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
            // Already authenticated → go to the target (or the manager).
            if (IsAdminAuthenticated())
            {
                if (!string.IsNullOrWhiteSpace(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(CreatorManager));
            }

            // Not authenticated (or the 2h admin session expired) → SHOW the login form.
            // (Previously this redirected to Login itself, which caused an infinite redirect loop.)
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

        // POST: Admin/CreateClaimLink
        // Generates a claim link so a content creator can take ownership of a pre-built channel.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClaimLink(string sourceHash)
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            var channel = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == sourceHash);
            if (channel == null)
            {
                TempData["Error"] = "Kanal nicht gefunden.";
                return RedirectToAction(nameof(CreatorManager));
            }

            var token = Guid.NewGuid().ToString("N");
            _context.ChannelClaims.Add(new ChannelClaim
            {
                Token = token,
                SourceHash = sourceHash,
                CreatorName = channel.UserName ?? string.Empty,
                Status = "pending",
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
            });
            await _context.SaveChangesAsync();

            var link = Url.Action("ClaimChannel", "Shared", new { area = "WorldMiniApp", token }, Request.Scheme);
            TempData["ClaimLink"] = link;
            TempData["Success"] = $"Claim-Link für '{channel.UserName}' erstellt (14 Tage gültig). Link an den Creator senden.";
            _logger.LogInformation("Admin created claim link for channel {Hash} ({Name})", sourceHash, channel.UserName);

            return RedirectToAction(nameof(CreatorManager));
        }

        // GET: Admin/RegenerateCaptions?postingId=123
        // Repairs a video whose caption translations failed (e.g. bad API key): reads the existing
        // de.vtt from Bunny and regenerates the translated tracks. de.vtt itself is left as-is.
        [HttpGet]
        public async Task<IActionResult> RegenerateCaptions(int postingId)
        {
            if (!IsAdminAuthenticated())
            {
                return RedirectToAction(nameof(Login), new { returnUrl = Request.Path });
            }

            var posting = await _context.WorldUserPosting.FirstOrDefaultAsync(p => p.Id == postingId);
            if (posting == null)
            {
                return CaptionResultPage(false, $"Posting {postingId} nicht gefunden.");
            }

            string? videoGuid = null;
            if (!string.IsNullOrWhiteSpace(posting.Source)
                && Uri.TryCreate(posting.Source, UriKind.Absolute, out var uri))
            {
                var seg = uri.AbsolutePath.Trim('/').Split('/');
                if (seg.Length > 0 && Guid.TryParse(seg[0], out _))
                {
                    videoGuid = seg[0];
                }
            }

            if (string.IsNullOrWhiteSpace(videoGuid))
            {
                return CaptionResultPage(false, $"Keine Video-GUID ermittelbar (evtl. Bild-Post?) für Posting {postingId}.");
            }

            var captionService = HttpContext.RequestServices
                .GetRequiredService<DelikatessenDrehbuch.Areas.WorldMiniApp.Services.CaptionGenerationService>();
            var result = await captionService.RegenerateTranslationsAsync(videoGuid);

            if (!result.Found)
            {
                return CaptionResultPage(false, $"Keine deutsche Untertitelspur (de.vtt) für Posting {postingId} in Bunny gefunden. Zuerst hochladen/transkribieren.");
            }

            var ok = result.Succeeded == result.Total && result.Total > 0;
            return CaptionResultPage(ok,
                $"Untertitel für „{posting.Title}“ (Posting {postingId}): {result.Succeeded}/{result.Total} Sprachen neu erzeugt.");
        }

        private IActionResult CaptionResultPage(bool success, string message)
        {
            var color = success ? "#2f855a" : "#c53030";
            var bg = success ? "#f0fff4" : "#fff5f5";
            var icon = success ? "✓" : "⚠";
            var profileUrl = Url.Action("MyProfile", "Feed", new { area = "WorldMiniApp", userHash = GetCurrentUserHash() }) ?? "/";
            var safeMessage = System.Net.WebUtility.HtmlEncode(message);

            var html = $@"<!doctype html><html lang=""de""><head><meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Untertitel</title></head>
<body style=""margin:0;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#f5efe1;display:flex;min-height:100vh;align-items:center;justify-content:center;"">
  <div style=""max-width:420px;width:90%;background:{bg};border:1px solid {color}33;border-radius:16px;padding:24px;text-align:center;box-shadow:0 8px 32px rgba(0,0,0,.08);"">
    <div style=""font-size:40px;color:{color};line-height:1;"">{icon}</div>
    <p style=""margin:14px 0 20px;color:#2d3748;font-size:15px;line-height:1.5;"">{safeMessage}</p>
    <a href=""{profileUrl}"" style=""display:inline-block;padding:11px 22px;background:#2d4f1e;color:#fff;text-decoration:none;border-radius:10px;font-weight:600;font-size:14px;"">Zurück zum Profil</a>
  </div>
</body></html>";

            return Content(html, "text/html");
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
