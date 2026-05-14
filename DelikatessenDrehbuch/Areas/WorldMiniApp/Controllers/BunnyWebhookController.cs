using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    [Route("WorldMiniApp/Bunny/[action]")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class BunnyWebhookController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BunnyWebhookController> _logger;
        private readonly CaptionGenerationService _captionService;
        private readonly IConfiguration _config;

        public BunnyWebhookController(
            ApplicationDbContext context,
            ILogger<BunnyWebhookController> logger,
            CaptionGenerationService captionService,
            IConfiguration config)
        {
            _context = context;
            _logger = logger;
            _captionService = captionService;
            _config = config;
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> VideoWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                _logger.LogInformation(
                    "Bunny webhook received. RemoteIp={RemoteIp} ContentType={ContentType}",
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.ContentType);

                var signatureVersion = Request.Headers["X-BunnyStream-Signature-Version"].FirstOrDefault();
                var signatureAlgorithm = Request.Headers["X-BunnyStream-Signature-Algorithm"].FirstOrDefault();
                var signature = Request.Headers["X-BunnyStream-Signature"].FirstOrDefault();

                var validateSignature = string.Equals(
                    _config["Bunny_Net_Validate_Webhook_Signature"],
                    "true",
                    StringComparison.OrdinalIgnoreCase);

                if (validateSignature && !string.IsNullOrWhiteSpace(signature))
                {
                    var readOnlyApiKey = _config["Bunny_Net_Passwort_Lager_ReadOnly"];
                    if (!string.IsNullOrWhiteSpace(readOnlyApiKey))
                    {
                        if (!ValidateSignature(body, signature, signatureVersion, signatureAlgorithm, readOnlyApiKey))
                        {
                            _logger.LogWarning(
                                "Invalid Bunny webhook signature. Version={Version} Algorithm={Algorithm}",
                                signatureVersion,
                                signatureAlgorithm);
                            return Unauthorized("Invalid signature");
                        }

                        _logger.LogWarning("Bunny webhook signature validated successfully.");
                    }
                    else
                    {
                        _logger.LogWarning("Bunny webhook signature was sent, but Bunny_Net_Passwort_Lager_ReadOnly is not configured.");
                    }
                }
                else if (!validateSignature && !string.IsNullOrWhiteSpace(signature))
                {
                    _logger.LogWarning("Bunny webhook signature received but validation is disabled.");
                }

                JsonElement payload;
                try
                {
                    payload = JsonSerializer.Deserialize<JsonElement>(body);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Bunny webhook rejected: invalid JSON body.");
                    return BadRequest("Invalid JSON");
                }

                if (!payload.TryGetProperty("VideoGuid", out var videoGuidElement))
                {
                    _logger.LogWarning("Bunny webhook rejected: missing VideoGuid.");
                    return BadRequest("Missing VideoGuid");
                }

                var videoGuid = videoGuidElement.GetString();
                if (string.IsNullOrWhiteSpace(videoGuid))
                {
                    _logger.LogWarning("Bunny webhook rejected: invalid VideoGuid.");
                    return BadRequest("Invalid VideoGuid");
                }

                if (!payload.TryGetProperty("Status", out var statusElement))
                {
                    _logger.LogWarning("Bunny webhook rejected: missing Status. VideoGuid={VideoGuid}", videoGuid);
                    return BadRequest("Missing Status");
                }

                int status;
                try
                {
                    status = statusElement.GetInt32();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Bunny webhook rejected: Status is not an integer. VideoGuid={VideoGuid}", videoGuid);
                    return BadRequest("Invalid Status");
                }

                _logger.LogWarning("Bunny webhook parsed. VideoGuid={VideoGuid} Status={Status}", videoGuid, status);

                if (status != 3)
                {
                    _logger.LogWarning("Ignoring Bunny webhook status {Status} for video {VideoGuid}", status, videoGuid);
                    return Ok();
                }

                var pendingVideo = await _context.WorldUserPendingVideos
                    .Include(pv => pv.Posting)
                    .FirstOrDefaultAsync(pv => pv.VideoGuid == videoGuid);

                if (pendingVideo == null)
                {
                    _logger.LogWarning("No pending video found for Bunny webhook VideoGuid={VideoGuid}", videoGuid);
                    return Ok();
                }

                var notification = new WorldUserNotification
                {
                    UserHash = pendingVideo.UserHash,
                    Description = $"Dein Video \"{pendingVideo.Posting?.Title ?? "Rezept"}\" ist fertig!",
                    CreatedAtUtc = DateTime.UtcNow,
                    IsSeen = false,
                    EventType = "video-ready",
                    Icon = "bi-camera-video-fill",
                    Sender = "system",
                    Href = $"/WorldMiniApp/Home/Index?scrollToId={pendingVideo.Posting?.Recipe?.Id ?? 0}",
                    NotificationKey = $"video-ready:{pendingVideo.PostingId}",
                    ContextText = pendingVideo.Posting?.Title
                };

                await _context.WorldUserNotifications.AddAsync(notification);
                _context.WorldUserPendingVideos.Remove(pendingVideo);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Bunny webhook processed successfully. PostingId={PostingId} VideoGuid={VideoGuid}",
                    pendingVideo.PostingId,
                    videoGuid);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var videoUrl = await GetBunnyVideoUrlAsync(videoGuid);
                        if (!string.IsNullOrWhiteSpace(videoUrl))
                        {
                            _logger.LogWarning("Starting caption generation for VideoGuid={VideoGuid}", videoGuid);
                            await _captionService.GenerateCaptionsForVideoAsync(videoUrl, videoGuid);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Caption generation failed in background task. VideoGuid={VideoGuid}", videoGuid);
                    }
                });

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Bunny webhook");
                return StatusCode(500);
            }
        }

        private Task<string?> GetBunnyVideoUrlAsync(string videoGuid)
        {
            try
            {
                var streamHostname = _config["Bunny_Net_Stream_Host_Name"];
                if (string.IsNullOrWhiteSpace(streamHostname))
                {
                    _logger.LogWarning("Bunny_Net_Stream_Host_Name is missing for webhook caption generation.");
                    return Task.FromResult<string?>(null);
                }

                var hostname = streamHostname.Replace("https://", "").Replace("http://", "").TrimEnd('/');
                var playlistUrl = $"https://{hostname}/{videoGuid}/playlist.m3u8";
                _logger.LogWarning("Bunny playlist URL prepared for caption generation. VideoGuid={VideoGuid} Url={Url}", videoGuid, playlistUrl);
                return Task.FromResult<string?>(playlistUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Bunny video URL for VideoGuid={VideoGuid}", videoGuid);
                return Task.FromResult<string?>(null);
            }
        }

        private bool ValidateSignature(
            string rawBody,
            string signatureHeader,
            string? signatureVersion,
            string? signatureAlgorithm,
            string signatureSecret)
        {
            try
            {
                if (signatureVersion != "v1")
                {
                    _logger.LogWarning("Unsupported Bunny signature version: {Version}", signatureVersion);
                    return false;
                }

                if (signatureAlgorithm != "hmac-sha256")
                {
                    _logger.LogWarning("Unsupported Bunny signature algorithm: {Algorithm}", signatureAlgorithm);
                    return false;
                }

                var keyBytes = Encoding.UTF8.GetBytes(signatureSecret);
                var bodyBytes = Encoding.UTF8.GetBytes(rawBody);

                using var hmac = new HMACSHA256(keyBytes);
                var hash = hmac.ComputeHash(bodyBytes);
                var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();

                if (signatureHeader.Length != expectedSignature.Length)
                {
                    return false;
                }

                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedSignature),
                    Encoding.UTF8.GetBytes(signatureHeader));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Bunny webhook signature");
                return false;
            }
        }
    }
}
