using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nethereum.Signer;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class AuthController : Controller
    {
        private const string SessionUserHashKey = "WorldMiniAppUserHash";
        private const string SiweNonceKey = "WorldMiniAppSiweNonce";
        private static readonly Regex EthereumAddressRegex = new("^0x[a-fA-F0-9]{40}$", RegexOptions.Compiled);
        private static readonly Regex SiweNonceRegex = new("^\\s*Nonce:\\s*(?<nonce>[A-Za-z0-9]{8,})\\s*$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex SiweAddressLineRegex = new("(?:^|\\r?\\n)(?<address>0x[a-fA-F0-9]{40})(?:\\r?\\n|$)", RegexOptions.Compiled);

        private readonly IAuthService _authService;
        private readonly IUserManager _userManager;
        private readonly IWorldAppMealPlanService _worldAppMealPlanService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, IUserManager userManager, IWorldAppMealPlanService worldAppMealPlanService, ApplicationDbContext context, ILogger<AuthController> logger)
        {
            _authService = authService;
            _userManager = userManager;
            _worldAppMealPlanService = worldAppMealPlanService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [IgnoreAntiforgeryToken]
        public IActionResult Nonce()
        {
            var nonce = GenerateSiweNonce();
            HttpContext.Session.SetString(SiweNonceKey, nonce);
            return Json(new WalletNonceResponseDto { Nonce = nonce });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CompleteSiwe([FromBody] WalletAuthRequestDto request)
        {
            if (request?.Payload == null || request.Payload.Status != "success")
            {
                return BadRequest(new { status = "error", isValid = false, message = "Payload invalid." });
            }

            if (string.IsNullOrWhiteSpace(request.Nonce))
            {
                return BadRequest(new { status = "error", isValid = false, message = "Nonce missing" });
            }

            var storedNonce = HttpContext.Session.GetString(SiweNonceKey);
            if (!string.IsNullOrWhiteSpace(storedNonce) && !string.Equals(storedNonce, request.Nonce, StringComparison.Ordinal))
            {
                return BadRequest(new { status = "error", isValid = false, message = "Invalid nonce" });
            }

            if (string.IsNullOrWhiteSpace(storedNonce))
            {
                _logger.LogWarning("CompleteSiwe: Session nonce missing, using signed message nonce validation only.");
            }

            try
            {
                var verifyResult = VerifyWalletAuthPayload(request.Payload, request.Nonce);

                // Security: Nonce nach Benutzung invalidieren (Replay-Schutz)
                HttpContext.Session.Remove(SiweNonceKey);

                if (!verifyResult.IsValid || string.IsNullOrWhiteSpace(verifyResult.Address))
                {
                    return BadRequest(new { status = "error", isValid = false, message = verifyResult.Reason ?? "Invalid SIWE message/signature" });
                }

                await _userManager.CreateOrUpdateWalletUser(verifyResult.Address, request.RememberLogin);

                var normalizedWallet = verifyResult.Address.ToLowerInvariant();
                WorldMiniAppUserHashHelper.Persist(HttpContext, normalizedWallet, isTestHash: false);

                return Ok(new { status = "success", isValid = true, walletAddress = normalizedWallet });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompleteSiwe failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { status = "error", isValid = false, message = "Ein unerwarteter Fehler ist aufgetreten." });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> VerifyAction([FromBody] VerifyRequestDto request)
        {
            if (request?.Payload == null || request.Payload.Status != "success")
            {
                return BadRequest("Payload invalid.");
            }

            try
            {
                var isValid = await _authService.VerifyProofWithWorldcoin(request);

                if (isValid.Success)
                {
                    await _userManager.CreateNewUser(request);
                    WorldMiniAppUserHashHelper.Persist(HttpContext, request.Payload.NullifierHash, isTestHash: false);

                    return Ok(new { status = 200, message = "Erfolg!" });
                }
                else
                {
                    return BadRequest("Verifizierung fehlgeschlagen (False zurückgegeben).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyAction failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { status = "error", message = "Ein unerwarteter Fehler ist aufgetreten." });
            }
        }



        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetTestHash([FromBody] RefreshLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.UserHash))
            {
                return BadRequest(new { status = "error", message = "UserHash missing." });
            }

            var userHash = request.UserHash.Trim();
            await _userManager.CreateOrUpdateTestUser(userHash, request.RememberLogin ?? true);
            WorldMiniAppUserHashHelper.Persist(HttpContext, userHash, isTestHash: true);

            return Ok(new
            {
                status = "success",
                isValid = true,
                userHash,
                isTestHash = true
            });
        }

        [HttpGet]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SessionStatus()
        {
            var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Ok(new { isLoggedIn = false });
            }

            var user = await _context.WorldAppUser.AsNoTracking().FirstOrDefaultAsync(x => x.UserHash == userHash);
            if (user == null)
            {
                return Ok(new { isLoggedIn = false });
            }

            return Ok(new
            {
                isLoggedIn = true,
                userHash = user.UserHash,
                rememberLogin = user.RememberLogin,
                isVerified = user.IsVerified
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RefreshStatus([FromBody] RefreshLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.UserHash))
            {
                return BadRequest("UserHash missing.");
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == request.UserHash);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            user.Lastlogin = DateTime.Now;
            if (request.RememberLogin.HasValue)
            {
                user.RememberLogin = request.RememberLogin.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = user.IsVerified, rememberLogin = user.RememberLogin });
        }

        public IActionResult Index()
        {
            return View();
        }

        private static string GenerateSiweNonce()
        {
            return Guid.NewGuid().ToString("N").ToLowerInvariant();
        }

        private WalletSiweVerifyResponseDto VerifyWalletAuthPayload(WalletAuthPayloadDto payload, string expectedNonce)
        {
            if (string.IsNullOrWhiteSpace(payload.Message) || string.IsNullOrWhiteSpace(payload.Signature))
            {
                return InvalidSiwe("SIWE message or signature missing");
            }

            var validationErrors = new List<string>();
            var rawMessage = payload.Message;
            var normalizedMessageForParsing = rawMessage.Replace("\r\n", "\n");
            var normalizedSignature = NormalizeSignature(payload.Signature);
            if (string.IsNullOrWhiteSpace(normalizedSignature))
            {
                validationErrors.Add("signature empty/invalid");
            }

            var claimedAddress = payload.Address?.Trim();
            if (!string.IsNullOrWhiteSpace(claimedAddress) && !EthereumAddressRegex.IsMatch(claimedAddress))
            {
                validationErrors.Add("payload address invalid");
            }

            var nonceMatch = SiweNonceRegex.Match(normalizedMessageForParsing);
            var signedNonce = nonceMatch.Success ? nonceMatch.Groups["nonce"].Value : string.Empty;
            if (string.IsNullOrWhiteSpace(signedNonce))
            {
                validationErrors.Add("nonce missing in SIWE message");
            }
            else if (!string.Equals(signedNonce, expectedNonce, StringComparison.Ordinal))
            {
                validationErrors.Add("nonce mismatch");
            }

            var messageAddressMatch = SiweAddressLineRegex.Match(normalizedMessageForParsing);
            var messageAddress = messageAddressMatch.Success ? messageAddressMatch.Groups["address"].Value : string.Empty;
            if (!string.IsNullOrWhiteSpace(messageAddress) && !EthereumAddressRegex.IsMatch(messageAddress))
            {
                validationErrors.Add("message address invalid");
            }

            if (validationErrors.Count > 0)
            {
                return InvalidSiwe($"Invalid SIWE payload: {string.Join("; ", validationErrors)}");
            }

            try
            {
                var recoveredAddress = new EthereumMessageSigner().EncodeUTF8AndEcRecover(rawMessage, normalizedSignature);
                if (string.IsNullOrWhiteSpace(recoveredAddress) || !EthereumAddressRegex.IsMatch(recoveredAddress))
                {
                    return InvalidSiwe("signature recovery produced invalid address");
                }

                // Security: Recovered address MUSS mit der claimed address uebereinstimmen.
                // Kein Fallback - wenn die Signatur nicht passt, wird abgelehnt.
                if (!string.IsNullOrWhiteSpace(claimedAddress) && !string.Equals(recoveredAddress, claimedAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return InvalidSiwe($"signature address does not match payload address (recovered: {recoveredAddress}, payload: {claimedAddress})");
                }

                if (!string.IsNullOrWhiteSpace(messageAddress) && !string.Equals(recoveredAddress, messageAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return InvalidSiwe($"signature address does not match SIWE message address (recovered: {recoveredAddress}, message: {messageAddress})");
                }

                return new WalletSiweVerifyResponseDto
                {
                    IsValid = true,
                    Address = recoveredAddress,
                    Reason = "OK"
                };
            }
            catch (Exception ex)
            {
                // Security: Wenn Signatur-Recovery fehlschlaegt, ABLEHNEN.
                // Kein Fallback auf claimedAddress/messageAddress.
                _logger.LogWarning(ex, "SIWE signature recovery failed.");
                return InvalidSiwe("signature recovery failed");
            }
        }

        private static WalletSiweVerifyResponseDto InvalidSiwe(string reason)
        {
            return new WalletSiweVerifyResponseDto
            {
                IsValid = false,
                Reason = reason
            };
        }

        private static string NormalizeSignature(string signature)
        {
            var normalized = signature?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (!normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = $"0x{normalized}";
            }

            return normalized;
        }
    }
}


