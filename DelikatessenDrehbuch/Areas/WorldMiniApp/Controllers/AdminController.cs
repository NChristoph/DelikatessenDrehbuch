using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class AdminController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private string? GetCurrentUserHash()
        {
            return Request.Cookies["WorldMiniAppUserHash"];
        }

        private bool IsAdmin()
        {
            var userHash = GetCurrentUserHash();
            return !string.IsNullOrWhiteSpace(userHash) &&
                   !string.IsNullOrWhiteSpace(SuperUserHash) &&
                   userHash.Equals(SuperUserHash, StringComparison.OrdinalIgnoreCase);
        }

        // GET: Admin/CreatorManager
        public async Task<IActionResult> CreatorManager()
        {
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
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
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
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
                TempData["Error"] = $"Fehler beim Erstellen: {ex.Message}";
            }

            return RedirectToAction(nameof(CreatorManager));
        }

        // POST: Admin/LoginAsCreator
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LoginAsCreator(string targetHash)
        {
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
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

            // Setze den Creator-Hash als aktuellen User (Session + Cookie)
            WorldMiniApp.Services.WorldMiniAppUserHashHelper.Persist(HttpContext, targetHash, false);

            TempData["Success"] = $"Du bist jetzt als Creator {targetHash.Substring(0, 10)}... angemeldet!";
            _logger.LogInformation("Admin logged in as creator: {Hash}", targetHash);

            return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
        }

        // POST: Admin/ReturnToAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReturnToAdmin()
        {
            var originalHash = Request.Cookies["AdminOriginalHash"];

            if (!string.IsNullOrWhiteSpace(originalHash))
            {
                // Setze Admin-Hash zurück (Session + Cookie)
                WorldMiniApp.Services.WorldMiniAppUserHashHelper.Persist(HttpContext, originalHash, false);

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
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
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
                TempData["Error"] = $"Fehler beim Löschen: {ex.Message}";
            }

            return RedirectToAction(nameof(CreatorManager));
        }

        // GET: Admin/ReportedContent
        public async Task<IActionResult> ReportedContent(string status = "all")
        {
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
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
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
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
                TempData["Error"] = $"Fehler: {ex.Message}";
            }

            return RedirectToAction(nameof(ReportedContent));
        }

        // POST: Admin/DeleteReportedPosting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReportedPosting(int postingId)
        {
            if (!IsAdmin())
            {
                return Unauthorized("Nur für Admins zugänglich.");
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
                TempData["Error"] = $"Fehler beim Löschen: {ex.Message}";
            }

            return RedirectToAction(nameof(ReportedContent));
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
                return Json(new { success = false, error = ex.Message });
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
