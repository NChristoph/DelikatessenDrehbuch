using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
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
                if (!verifyResult.IsValid || string.IsNullOrWhiteSpace(verifyResult.Address))
                {
                    return BadRequest(new { status = "error", isValid = false, message = verifyResult.Reason ?? "Invalid SIWE message/signature" });
                }

                await _userManager.CreateOrUpdateWalletUser(verifyResult.Address, request.RememberLogin);

                var normalizedWallet = verifyResult.Address.ToLowerInvariant();
                HttpContext.Session.SetString(SessionUserHashKey, normalizedWallet);

                return Ok(new { status = "success", isValid = true, walletAddress = normalizedWallet });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompleteSiwe failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { status = "error", isValid = false, message = "Ein unerwarteter Fehler ist aufgetreten." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                    HttpContext.Session.SetString(SessionUserHashKey, request.Payload.NullifierHash);

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
                return StatusCode(StatusCodes.Status500InternalServerError, "Ein unerwarteter Fehler ist aufgetreten.");
            }
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
            if (payload == null)
            {
                return InvalidSiwe("payload missing");
            }

            if (string.IsNullOrWhiteSpace(payload.Message) || string.IsNullOrWhiteSpace(payload.Signature))
            {
                return InvalidSiwe("SIWE message or signature missing");
            }

            var rawMessage = payload.Message; // EXACT message that was signed
            var messageForParsing = rawMessage.Replace("\r\n", "\n"); // only for regex parsing, no Trim()

            var normalizedSignature = NormalizeSignature(payload.Signature);
            if (string.IsNullOrWhiteSpace(normalizedSignature))
            {
                return InvalidSiwe("signature empty/invalid");
            }

            var claimedAddress = payload.Address?.Trim();
            if (!string.IsNullOrWhiteSpace(claimedAddress) && !EthereumAddressRegex.IsMatch(claimedAddress))
            {
                return InvalidSiwe("payload address invalid");
            }

            // 1) Nonce in message must exist and must match expected nonce
            var nonceMatch = SiweNonceRegex.Match(messageForParsing);
            var signedNonce = nonceMatch.Success ? nonceMatch.Groups["nonce"].Value : string.Empty;

            if (string.IsNullOrWhiteSpace(signedNonce))
            {
                return InvalidSiwe("nonce missing in SIWE message");
            }

            if (!string.Equals(signedNonce, expectedNonce, StringComparison.Ordinal))
            {
                return InvalidSiwe($"nonce mismatch (signed: {signedNonce}, expected: {expectedNonce})");
            }

            // 2) Address in message (if found) should match recovered later
            var messageAddressMatch = SiweAddressLineRegex.Match(messageForParsing);
            var messageAddress = messageAddressMatch.Success ? messageAddressMatch.Groups["address"].Value : string.Empty;

            if (!string.IsNullOrWhiteSpace(messageAddress) && !EthereumAddressRegex.IsMatch(messageAddress))
            {
                return InvalidSiwe("message address invalid");
            }

            try
            {
                // 3) Recover signer address from EXACT raw message
                var recoveredAddress = new EthereumMessageSigner().EncodeUTF8AndEcRecover(rawMessage, normalizedSignature);

                if (string.IsNullOrWhiteSpace(recoveredAddress) || !EthereumAddressRegex.IsMatch(recoveredAddress))
                {
                    return InvalidSiwe("signature recovery produced invalid address");
                }

                // 4) If payload provides claimed address, it MUST match recovered
                if (!string.IsNullOrWhiteSpace(claimedAddress)
                    && !string.Equals(recoveredAddress, claimedAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return InvalidSiwe($"signature does not match payload address (recovered: {recoveredAddress}, payload: {claimedAddress})");
                }

                // 5) If message contains address line, it MUST match recovered
                if (!string.IsNullOrWhiteSpace(messageAddress)
                    && !string.Equals(recoveredAddress, messageAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return InvalidSiwe($"signature does not match SIWE message address (recovered: {recoveredAddress}, message: {messageAddress})");
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
                _logger.LogWarning(ex, "SIWE signature recovery failed.");
                return InvalidSiwe("signature recovery exception");
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
