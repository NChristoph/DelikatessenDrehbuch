using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    /// <summary>
    /// Kommentar-bezogene Endpunkte des Feeds (Lesen, Schreiben, Reaktionen,
    /// Melden, Löschen, Anheften, Moderation). Aus <see cref="FeedController"/>
    /// herausgelöst, behält aber via Attribut-Route die ursprünglichen
    /// "/WorldMiniApp/Feed/&lt;Action&gt;"-Pfade, damit das Frontend (hartcodierte
    /// fetch-URLs in Feed/Index.cshtml) unverändert weiterläuft.
    /// </summary>
    [Area("WorldMiniApp")]
    [Route("WorldMiniApp/Feed/[action]")]
    public class FeedCommentsController : WorldMiniAppBaseController
    {
        private const int CommentAutoHideReportThreshold = 3;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<FeedCommentsController> _logger;

        public FeedCommentsController(ApplicationDbContext context, ILogger<FeedCommentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetComments(int postingId, string sort = "top", CancellationToken cancellationToken = default)
        {
            if (postingId <= 0)
                return BadRequest(new { message = "postingId fehlt." });

            var currentUserHash = ResolveUserHash(string.Empty);
            var canWrite = await CanWriteCommentsAsync(_context, currentUserHash, cancellationToken);
            var canReactOrReport = await CanReactOrReportCommentsAsync(currentUserHash, cancellationToken);
            var canModerate = await CanModerateCommentsAsync(currentUserHash, cancellationToken);

            var creatorUserHash = await _context.WorldUserPosting
                .AsNoTracking()
                .Where(p => p.Id == postingId)
                .Select(p => p.CreatorId)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var comments = await _context.WorldUserComments
                .AsNoTracking()
                .Where(x => x.WorldUserPostingId == postingId && !x.IsDeleted)
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => new CommentListItem
                {
                    Id = x.Id,
                    ParentCommentId = x.ParentCommentId,
                    UserHash = x.UserHash,
                    UserName = x.UserName,
                    VerificationLevel = x.VerificationLevel,
                    Text = x.CommentText,
                    CreatedAtUtc = x.CreatedAtUtc,
                    IsOwn = !string.IsNullOrWhiteSpace(currentUserHash) && x.UserHash == currentUserHash,
                    IsPinned = x.IsPinned
                })
                .ToListAsync(cancellationToken);

            var commentIds = comments.Select(x => x.Id).ToList();
            var reactionStats = await GetCommentReactionStatsAsync(commentIds, currentUserHash, cancellationToken);
            var reportedIds = await GetReportedCommentIdsAsync(commentIds, currentUserHash, cancellationToken);
            var reportCounts = await GetCommentReportCountsAsync(commentIds, cancellationToken);

            var visibleComments = comments
                .Where(item =>
                {
                    var reportCount = reportCounts.TryGetValue(item.Id, out var rc) ? rc : 0;
                    var autoHidden = reportCount >= CommentAutoHideReportThreshold;
                    return !autoHidden || item.IsOwn || canModerate;
                })
                .ToList();

            var replyCounts = visibleComments
                .Where(x => x.ParentCommentId.HasValue)
                .GroupBy(x => x.ParentCommentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var normalizedSort = NormalizeCommentSort(sort);

            object MapComment(CommentListItem item)
            {
                var reactionSummary = reactionStats.TryGetValue(item.Id, out var stats)
                    ? stats
                    : new CommentReactionSummary();
                var reportCount = reportCounts.TryGetValue(item.Id, out var resolvedReportCount)
                    ? resolvedReportCount
                    : 0;

                return new
                {
                    id = item.Id,
                    parentCommentId = item.ParentCommentId,
                    userHash = item.UserHash,
                    userName = item.UserName,
                    verificationLevel = item.VerificationLevel,
                    text = item.Text,
                    createdAtUtc = item.CreatedAtUtc,
                    isOwn = item.IsOwn,
                    isCreator = !string.IsNullOrWhiteSpace(creatorUserHash) && item.UserHash == creatorUserHash,
                    isPinned = item.IsPinned,
                    canDelete = item.IsOwn || IsCommentModerator(currentUserHash),
                    likeCount = reactionSummary.LikeCount,
                    dislikeCount = reactionSummary.DislikeCount,
                    myReaction = reactionSummary.MyReaction,
                    hasReported = reportedIds.Contains(item.Id),
                    reportCount,
                    isAutoHidden = reportCount >= CommentAutoHideReportThreshold
                };
            };

            var topLevelComments = visibleComments
                .Where(x => !x.ParentCommentId.HasValue)
                .Select(x => new
                {
                    Raw = x,
                    Reaction = reactionStats.TryGetValue(x.Id, out var rs) ? rs : new CommentReactionSummary(),
                    ReplyCount = replyCounts.TryGetValue(x.Id, out var rc) ? rc : 0
                });

            var orderedTopLevel = normalizedSort == "new"
                ? topLevelComments
                    .OrderByDescending(x => x.Raw.IsPinned)
                    .ThenByDescending(x => x.Raw.CreatedAtUtc).ToList()
                : topLevelComments
                    .OrderByDescending(x => x.Raw.IsPinned)
                    .ThenByDescending(x => x.Reaction.LikeCount - x.Reaction.DislikeCount)
                    .ThenByDescending(x => x.Reaction.LikeCount)
                    .ThenByDescending(x => x.ReplyCount)
                    .ThenByDescending(x => x.Raw.CreatedAtUtc)
                    .ToList();

            var isCurrentUserCreator = !string.IsNullOrWhiteSpace(currentUserHash) && currentUserHash == creatorUserHash;

            return Json(new
            {
                success = true,
                postingId,
                canWrite,
                canReactOrReport,
                canModerate,
                canPin = isCurrentUserCreator,
                isLoggedIn = !string.IsNullOrWhiteSpace(currentUserHash),
                sort = normalizedSort,
                commentCount = visibleComments.Count,
                items = orderedTopLevel.Select(parent => new
                {
                    id = parent.Raw.Id,
                    parentCommentId = parent.Raw.ParentCommentId,
                    userHash = parent.Raw.UserHash,
                    userName = parent.Raw.UserName,
                    verificationLevel = parent.Raw.VerificationLevel,
                    text = parent.Raw.Text,
                    createdAtUtc = parent.Raw.CreatedAtUtc,
                    isOwn = parent.Raw.IsOwn,
                    isCreator = !string.IsNullOrWhiteSpace(creatorUserHash) && parent.Raw.UserHash == creatorUserHash,
                    isPinned = parent.Raw.IsPinned,
                    canDelete = parent.Raw.IsOwn || IsCommentModerator(currentUserHash),
                    likeCount = parent.Reaction.LikeCount,
                    dislikeCount = parent.Reaction.DislikeCount,
                    myReaction = parent.Reaction.MyReaction,
                    hasReported = reportedIds.Contains(parent.Raw.Id),
                    reportCount = reportCounts.TryGetValue(parent.Raw.Id, out var parentReportCount) ? parentReportCount : 0,
                    isAutoHidden = reportCounts.TryGetValue(parent.Raw.Id, out var parentAutoHideCount) && parentAutoHideCount >= CommentAutoHideReportThreshold,
                    replyCount = parent.ReplyCount,
                    replies = visibleComments
                        .Where(x => x.ParentCommentId == parent.Raw.Id)
                        .OrderBy(x => x.CreatedAtUtc)
                        .Select(x => MapComment(x))
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([FromForm] int postingId, [FromForm] string text, [FromForm] int? parentCommentId, CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash();
            if (postingId <= 0)
                return BadRequest(new { message = "Posting fehlt." });

            if (!await CanWriteCommentsAsync(_context, userHash, cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nur orb-verifizierte Nutzer koennen Kommentare schreiben." });

            var normalizedText = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedText))
                return BadRequest(new { message = "Kommentar ist leer." });

            if (normalizedText.Length > 500)
                return BadRequest(new { message = "Kommentar ist zu lang." });

            var posting = await _context.WorldUserPosting
                .AsNoTracking()
                .Include(x => x.Recipe)
                .FirstOrDefaultAsync(x => x.Id == postingId && !x.IsOffline, cancellationToken);
            if (posting == null)
                return NotFound(new { message = "Posting wurde nicht gefunden." });

            WorldUserComment? parentComment = null;
            int? normalizedParentCommentId = null;
            if (parentCommentId.HasValue && parentCommentId.Value > 0)
            {
                parentComment = await _context.WorldUserComments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == parentCommentId.Value && x.WorldUserPostingId == postingId && !x.IsDeleted, cancellationToken);

                if (parentComment == null)
                    return BadRequest(new { message = "Antwortziel wurde nicht gefunden." });

                normalizedParentCommentId = parentComment.ParentCommentId ?? parentComment.Id;
            }

            var user = await _context.WorldAppUser
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserHash == userHash, cancellationToken);
            if (user == null)
                return BadRequest(new { message = "User wurde nicht gefunden." });

            var entity = new WorldUserComment
            {
                WorldUserPostingId = postingId,
                ParentCommentId = normalizedParentCommentId,
                UserHash = userHash,
                UserName = string.IsNullOrWhiteSpace(user.UserName) ? "User" : user.UserName.Trim(),
                VerificationLevel = user.IsVerified ?? string.Empty,
                CommentText = normalizedText,
                CreatedAtUtc = DateTime.UtcNow,
                IsDeleted = false
            };

            await _context.WorldUserComments.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await TryAddCommentNotificationsAsync(posting, entity, parentComment, cancellationToken);

            var commentCount = await _context.WorldUserComments
                .AsNoTracking()
                .CountAsync(x => x.WorldUserPostingId == postingId && !x.IsDeleted, cancellationToken);

            return Json(new
            {
                success = true,
                postingId,
                commentCount,
                item = new
                {
                    id = entity.Id,
                    userHash = entity.UserHash,
                    userName = entity.UserName,
                    verificationLevel = entity.VerificationLevel,
                    text = entity.CommentText,
                    createdAtUtc = entity.CreatedAtUtc,
                    isOwn = true,
                    parentCommentId = entity.ParentCommentId
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCommentReaction([FromForm] int commentId, [FromForm] string reaction, CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash();
            if (commentId <= 0)
                return BadRequest(new { message = "Kommentar fehlt." });

            if (!await CanReactOrReportCommentsAsync(userHash, cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nur angemeldete Nutzer koennen auf Kommentare reagieren." });

            var normalizedReaction = (reaction ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedReaction != "like" && normalizedReaction != "dislike")
                return BadRequest(new { message = "Reaktion ist ungueltig." });

            var comment = await _context.WorldUserComments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);
            if (comment == null)
                return NotFound(new { message = "Kommentar wurde nicht gefunden." });

            var entity = await _context.WorldUserCommentReactions
                .FirstOrDefaultAsync(x => x.WorldUserCommentId == commentId && x.UserHash == userHash, cancellationToken);

            var wantsLike = normalizedReaction == "like";
            var shouldNotifyReaction = false;
            if (entity == null)
            {
                entity = new WorldUserCommentReaction
                {
                    WorldUserCommentId = commentId,
                    UserHash = userHash,
                    IsLike = wantsLike,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _context.WorldUserCommentReactions.AddAsync(entity, cancellationToken);
                shouldNotifyReaction = true;
            }
            else if (entity.IsLike == wantsLike)
            {
                _context.WorldUserCommentReactions.Remove(entity);
                normalizedReaction = string.Empty;
            }
            else
            {
                entity.IsLike = wantsLike;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                shouldNotifyReaction = true;
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (shouldNotifyReaction)
            {
                var user = await _context.WorldAppUser
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserHash == userHash, cancellationToken);
                var actorName = user?.UserName ?? "Jemand";
                await TryAddCommentReactionNotificationAsync(userHash, actorName, commentId, wantsLike, cancellationToken);
            }

            var summary = await GetCommentReactionSummaryAsync(commentId, userHash, cancellationToken);
            return Json(new
            {
                success = true,
                commentId,
                likeCount = summary.LikeCount,
                dislikeCount = summary.DislikeCount,
                myReaction = summary.MyReaction
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportComment([FromForm] int commentId, [FromForm] string reason, CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash();
            if (commentId <= 0)
                return BadRequest(new { message = "Kommentar fehlt." });

            if (!await CanReactOrReportCommentsAsync(userHash, cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nur angemeldete Nutzer koennen Kommentare melden." });

            var comment = await _context.WorldUserComments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);
            if (comment == null)
                return NotFound(new { message = "Kommentar wurde nicht gefunden." });

            if (string.Equals(comment.UserHash, userHash, StringComparison.Ordinal))
                return BadRequest(new { message = "Eigene Kommentare musst du nicht melden." });

            var existingReport = await _context.WorldUserCommentReports
                .AsNoTracking()
                .AnyAsync(x => x.WorldUserCommentId == commentId && x.UserHash == userHash, cancellationToken);
            if (existingReport)
                return Json(new { success = true, commentId, hasReported = true });

            var normalizedReason = (reason ?? string.Empty).Trim();
            if (normalizedReason.Length > 500)
                normalizedReason = normalizedReason[..500];

            var report = new WorldUserCommentReport
            {
                WorldUserCommentId = commentId,
                UserHash = userHash,
                Reason = normalizedReason,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.WorldUserCommentReports.AddAsync(report, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true, commentId, hasReported = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment([FromForm] int commentId, CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash();
            if (commentId <= 0)
                return BadRequest(new { message = "Kommentar fehlt." });

            var comment = await _context.WorldUserComments
                .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);
            if (comment == null)
                return NotFound(new { message = "Kommentar wurde nicht gefunden." });

            var canModerate = await CanModerateCommentsAsync(userHash, cancellationToken);
            if (!canModerate && !string.Equals(comment.UserHash, userHash, StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Du darfst diesen Kommentar nicht loeschen." });

            comment.IsDeleted = true;
            comment.CommentText = string.Empty;

            var childComments = await _context.WorldUserComments
                .Where(x => x.ParentCommentId == commentId && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var child in childComments)
            {
                child.IsDeleted = true;
                child.CommentText = string.Empty;
            }

            var deletedCommentIds = childComments.Select(x => x.Id).Append(commentId).ToList();
            var reports = await _context.WorldUserCommentReports
                .Where(x => deletedCommentIds.Contains(x.WorldUserCommentId))
                .ToListAsync(cancellationToken);
            if (reports.Count > 0)
                _context.WorldUserCommentReports.RemoveRange(reports);

            await _context.SaveChangesAsync(cancellationToken);

            var commentCount = await _context.WorldUserComments
                .AsNoTracking()
                .CountAsync(x => x.WorldUserPostingId == comment.WorldUserPostingId && !x.IsDeleted, cancellationToken);

            return Json(new
            {
                success = true,
                commentId,
                postingId = comment.WorldUserPostingId,
                commentCount
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PinComment([FromForm] int commentId, CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash();
            if (commentId <= 0)
                return BadRequest(new { message = "Kommentar fehlt." });

            var comment = await _context.WorldUserComments
                .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);
            if (comment == null)
                return NotFound(new { message = "Kommentar wurde nicht gefunden." });

            var posting = await _context.WorldUserPosting
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == comment.WorldUserPostingId, cancellationToken);
            if (posting == null || !string.Equals(posting.CreatorId, userHash, StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nur der Creator kann Kommentare anheften." });

            var currentlyPinned = await _context.WorldUserComments
                .Where(x => x.WorldUserPostingId == comment.WorldUserPostingId && x.IsPinned && x.Id != commentId)
                .ToListAsync(cancellationToken);
            foreach (var pinned in currentlyPinned)
                pinned.IsPinned = false;

            comment.IsPinned = !comment.IsPinned;
            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true, commentId, isPinned = comment.IsPinned });
        }

        [HttpGet]
        public async Task<IActionResult> CommentModeration(CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash();

            // Prüfe ob User Moderator ist
            if (!IsCommentModerator(userHash))
                return Forbid();

            // Prüfe ob Admin-Passwort eingegeben wurde
            if (!IsCommentModeratorAuthenticated(userHash))
                return RedirectToAction("Login", "Admin", new { area = "WorldMiniApp", returnUrl = Url.Action(nameof(CommentModeration), "FeedComments", new { area = "WorldMiniApp", userHash }) });

            var reportRows = await _context.WorldUserCommentReports
                .AsNoTracking()
                .Join(_context.WorldUserComments.AsNoTracking(),
                    report => report.WorldUserCommentId,
                    comment => comment.Id,
                    (report, comment) => new { report, comment })
                .Where(x => !x.comment.IsDeleted)
                .OrderByDescending(x => x.report.CreatedAtUtc)
                .Select(x => new
                {
                    x.comment.Id,
                    x.comment.WorldUserPostingId,
                    x.comment.CommentText,
                    x.comment.UserName,
                    x.comment.UserHash,
                    x.comment.VerificationLevel,
                    x.comment.CreatedAtUtc,
                    ReportReason = x.report.Reason,
                    ReportCreatedAtUtc = x.report.CreatedAtUtc
                })
                .ToListAsync(cancellationToken);

            var items = reportRows
                .GroupBy(x => new
                {
                    x.Id,
                    x.WorldUserPostingId,
                    x.CommentText,
                    x.UserName,
                    x.UserHash,
                    x.VerificationLevel,
                    x.CreatedAtUtc
                })
                .Select(g => new CommentModerationItemViewModel
                {
                    CommentId = g.Key.Id,
                    PostingId = g.Key.WorldUserPostingId,
                    CommentText = g.Key.CommentText,
                    CommentUserName = g.Key.UserName,
                    CommentUserHash = g.Key.UserHash,
                    VerificationLevel = g.Key.VerificationLevel,
                    CommentCreatedAtUtc = g.Key.CreatedAtUtc,
                    ReportCount = g.Count(),
                    LatestReportAtUtc = g.Max(x => x.ReportCreatedAtUtc),
                    Reasons = g.GroupBy(x => string.IsNullOrWhiteSpace(x.ReportReason) ? "Ohne Grund" : x.ReportReason.Trim())
                        .Select(reasonGroup => new CommentReportReasonViewModel
                        {
                            Reason = reasonGroup.Key,
                            Count = reasonGroup.Count()
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList()
                })
                .OrderByDescending(x => x.LatestReportAtUtc)
                .ToList();

            var model = new CommentModerationViewModel
            {
                UserHash = userHash,
                OpenReportCount = reportRows.Count,
                Items = items
            };

            return View("~/Areas/WorldMiniApp/Views/Feed/CommentModeration.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveCommentReport([FromForm] int commentId, [FromForm] string actionType, CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash();
            if (!IsCommentModeratorAuthenticated(userHash))
                return Forbid();

            if (commentId <= 0)
                return BadRequest();

            var normalizedAction = (actionType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedAction != "dismiss" && normalizedAction != "delete")
                return BadRequest();

            var reports = await _context.WorldUserCommentReports
                .Where(x => x.WorldUserCommentId == commentId)
                .ToListAsync(cancellationToken);

            if (reports.Count > 0)
                _context.WorldUserCommentReports.RemoveRange(reports);

            if (normalizedAction == "delete")
            {
                var comment = await _context.WorldUserComments
                    .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);

                if (comment != null)
                {
                    comment.IsDeleted = true;
                    comment.CommentText = string.Empty;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(CommentModeration), new { userHash });
        }

        // ---------------------------------------------------------------------
        // Kommentar-spezifische Helfer (geteilte Moderations-/Notification-Engine
        // liegt in WorldMiniAppBaseController).
        // ---------------------------------------------------------------------

        private async Task<bool> CanReactOrReportCommentsAsync(string userHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userHash))
                return false;

            return await _context.WorldAppUser
                .AsNoTracking()
                .AnyAsync(x => x.UserHash == userHash, cancellationToken);
        }

        private Task<bool> CanModerateCommentsAsync(string userHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsCommentModerator(userHash));
        }

        private static string NormalizeCommentSort(string? sort)
        {
            return string.Equals(sort, "new", StringComparison.OrdinalIgnoreCase) ? "new" : "top";
        }

        private sealed class CommentReactionSummary
        {
            public int LikeCount { get; init; }
            public int DislikeCount { get; init; }
            public string MyReaction { get; init; } = string.Empty;
        }

        private sealed class CommentListItem
        {
            public int Id { get; init; }
            public int? ParentCommentId { get; init; }
            public string UserHash { get; init; } = string.Empty;
            public string UserName { get; init; } = string.Empty;
            public string VerificationLevel { get; init; } = string.Empty;
            public string Text { get; init; } = string.Empty;
            public DateTime CreatedAtUtc { get; init; }
            public bool IsOwn { get; init; }
            public bool IsPinned { get; init; }
        }

        private async Task<Dictionary<int, CommentReactionSummary>> GetCommentReactionStatsAsync(List<int> commentIds, string currentUserHash, CancellationToken cancellationToken = default)
        {
            if (commentIds == null || commentIds.Count == 0)
                return new Dictionary<int, CommentReactionSummary>();

            var grouped = await _context.WorldUserCommentReactions
                .AsNoTracking()
                .Where(x => commentIds.Contains(x.WorldUserCommentId))
                .GroupBy(x => x.WorldUserCommentId)
                .Select(g => new
                {
                    CommentId = g.Key,
                    LikeCount = g.Count(x => x.IsLike),
                    DislikeCount = g.Count(x => !x.IsLike)
                })
                .ToListAsync(cancellationToken);

            var summaries = grouped.ToDictionary(
                x => x.CommentId,
                x => new CommentReactionSummary
                {
                    LikeCount = x.LikeCount,
                    DislikeCount = x.DislikeCount
                });

            if (!string.IsNullOrWhiteSpace(currentUserHash))
            {
                var ownReactions = await _context.WorldUserCommentReactions
                    .AsNoTracking()
                    .Where(x => commentIds.Contains(x.WorldUserCommentId) && x.UserHash == currentUserHash)
                    .Select(x => new
                    {
                        x.WorldUserCommentId,
                        MyReaction = x.IsLike ? "like" : "dislike"
                    })
                    .ToListAsync(cancellationToken);

                foreach (var own in ownReactions)
                {
                    if (summaries.TryGetValue(own.WorldUserCommentId, out var existing))
                    {
                        summaries[own.WorldUserCommentId] = new CommentReactionSummary
                        {
                            LikeCount = existing.LikeCount,
                            DislikeCount = existing.DislikeCount,
                            MyReaction = own.MyReaction
                        };
                    }
                    else
                    {
                        summaries[own.WorldUserCommentId] = new CommentReactionSummary
                        {
                            MyReaction = own.MyReaction
                        };
                    }
                }
            }

            return summaries;
        }

        private async Task<CommentReactionSummary> GetCommentReactionSummaryAsync(int commentId, string currentUserHash, CancellationToken cancellationToken = default)
        {
            var stats = await GetCommentReactionStatsAsync(new List<int> { commentId }, currentUserHash, cancellationToken);
            return stats.TryGetValue(commentId, out var summary)
                ? summary
                : new CommentReactionSummary();
        }

        private async Task<HashSet<int>> GetReportedCommentIdsAsync(List<int> commentIds, string currentUserHash, CancellationToken cancellationToken = default)
        {
            if (commentIds == null || commentIds.Count == 0 || string.IsNullOrWhiteSpace(currentUserHash))
                return new HashSet<int>();

            var ids = await _context.WorldUserCommentReports
                .AsNoTracking()
                .Where(x => commentIds.Contains(x.WorldUserCommentId) && x.UserHash == currentUserHash)
                .Select(x => x.WorldUserCommentId)
                .ToListAsync(cancellationToken);

            return ids.ToHashSet();
        }

        private async Task<Dictionary<int, int>> GetCommentReportCountsAsync(List<int> commentIds, CancellationToken cancellationToken = default)
        {
            if (commentIds == null || commentIds.Count == 0)
                return new Dictionary<int, int>();

            return await _context.WorldUserCommentReports
                .AsNoTracking()
                .Where(x => commentIds.Contains(x.WorldUserCommentId))
                .GroupBy(x => x.WorldUserCommentId)
                .Select(g => new { CommentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CommentId, x => x.Count, cancellationToken);
        }

        private async Task TryAddCommentNotificationsAsync(WorldUserPosting posting, WorldUserComment newComment, WorldUserComment? parentComment, CancellationToken cancellationToken)
        {
            try
            {
                if (posting == null || newComment == null)
                    return;

                var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var excerpt = BuildCommentNotificationExcerpt(newComment.CommentText);
                var href = BuildCommentNotificationHref(posting, newComment);

                if (!string.IsNullOrWhiteSpace(parentComment?.UserHash)
                    && !string.Equals(parentComment.UserHash, newComment.UserHash, StringComparison.OrdinalIgnoreCase))
                {
                    recipients.Add(parentComment.UserHash);
                }

                if (!string.IsNullOrWhiteSpace(posting.CreatorId)
                    && !string.Equals(posting.CreatorId, newComment.UserHash, StringComparison.OrdinalIgnoreCase))
                {
                    recipients.Add(posting.CreatorId);
                }

                foreach (var recipientUserHash in recipients)
                {
                    var isReplyTarget = !string.IsNullOrWhiteSpace(parentComment?.UserHash)
                        && string.Equals(parentComment.UserHash, recipientUserHash, StringComparison.OrdinalIgnoreCase);
                    var isCreatorTarget = string.Equals(posting.CreatorId, recipientUserHash, StringComparison.OrdinalIgnoreCase);

                    if (isReplyTarget)
                    {
                        var aggregateCommentId = parentComment?.ParentCommentId ?? parentComment?.Id ?? newComment.Id;
                        await UpsertInteractionNotificationAsync(
                            _context,
                            recipientUserHash,
                            "comment-reply",
                            "bi-reply-fill",
                            href,
                            $"comment-reply:{aggregateCommentId}",
                            newComment.UserName,
                            excerpt,
                            cancellationToken);
                    }
                    else if (isCreatorTarget)
                    {
                        await UpsertInteractionNotificationAsync(
                            _context,
                            recipientUserHash,
                            "comment-video",
                            "bi-chat-dots-fill",
                            href,
                            $"comment-video:{posting.Id}",
                            newComment.UserName,
                            excerpt,
                            cancellationToken);
                    }
                }
            }
            catch
            {
                // best-effort
            }
        }

        private static string BuildCommentNotificationHref(WorldUserPosting posting, WorldUserComment comment)
        {
            var recipeId = posting?.Recipe?.Id ?? 0;
            var scrollToId = recipeId > 0 ? recipeId : posting?.Id ?? 0;
            var postingId = posting?.Id ?? 0;
            var commentId = comment?.Id ?? 0;
            return $"/WorldMiniApp/Feed?scrollToId={scrollToId}&openComments=1&postingId={postingId}&commentId={commentId}";
        }

        private static string BuildCommentNotificationExcerpt(string? text)
        {
            var normalized = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            normalized = normalized.Replace("\r", " ").Replace("\n", " ");
            return normalized.Length <= 120
                ? normalized
                : $"{normalized[..117].TrimEnd()}...";
        }

        private async Task TryAddCommentReactionNotificationAsync(string actorUserHash, string actorName, int commentId, bool isLike, CancellationToken cancellationToken)
        {
            try
            {
                var comment = await _context.WorldUserComments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken);

                if (comment == null || string.IsNullOrWhiteSpace(comment.UserHash))
                    return;

                // Don't notify yourself
                if (string.Equals(comment.UserHash, actorUserHash, StringComparison.OrdinalIgnoreCase))
                    return;

                var excerpt = BuildCommentNotificationExcerpt(comment.CommentText);
                var posting = await _context.WorldUserPosting
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == comment.WorldUserPostingId, cancellationToken);

                var recipeId = posting?.Recipe?.Id ?? 0;
                var scrollToId = recipeId > 0 ? recipeId : posting?.Id ?? 0;
                var href = $"/WorldMiniApp/Feed?scrollToId={scrollToId}&openComments=1&postingId={comment.WorldUserPostingId}&commentId={commentId}";
                var sender = isLike ? "like-comment" : "dislike-comment";
                var icon = isLike ? "bi-heart-fill" : "bi-hand-thumbs-down-fill";
                await UpsertInteractionNotificationAsync(
                    _context,
                    comment.UserHash,
                    sender,
                    icon,
                    href,
                    $"{sender}:{commentId}",
                    actorName,
                    excerpt,
                    cancellationToken);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
