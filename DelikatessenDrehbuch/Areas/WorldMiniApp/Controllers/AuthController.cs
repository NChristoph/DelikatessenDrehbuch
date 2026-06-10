using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nethereum.Signer;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
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
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(IAuthService authService, IUserManager userManager, IWorldAppMealPlanService worldAppMealPlanService, ApplicationDbContext context, ILogger<AuthController> logger, IWebHostEnvironment environment, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _authService = authService;
            _userManager = userManager;
            _worldAppMealPlanService = worldAppMealPlanService;
            _context = context;
            _logger = logger;
            _environment = environment;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
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
            if (request?.Payload == null)
            {
                return BadRequest(new { status = "error", isValid = false, message = "Payload invalid." });
            }

            if (!IsWalletAuthPayloadSuccessful(request.Payload))
            {
                return BadRequest(new { status = "error", isValid = false, message = "Wallet auth payload invalid." });
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

                await _userManager.CreateOrUpdateWalletUserAsync(verifyResult.Address, request.RememberLogin);

                var normalizedWallet = verifyResult.Address.ToLowerInvariant();
                await WorldMiniAppUserHashHelper.SignInAsync(HttpContext, normalizedWallet, request.RememberLogin, isTestHash: false);

                return Ok(new { status = "success", isValid = true, walletAddress = normalizedWallet });
            }
            catch (WorldMiniAppException ex)
            {
                _logger.LogWarning(ex, "CompleteSiwe WorldMiniApp error.");
                return StatusCode(ex.StatusCode, new { status = "error", isValid = false, message = ex.Message });
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
                    await _userManager.CreateNewUserAsync(request);
                    await WorldMiniAppUserHashHelper.SignInAsync(HttpContext, request.Payload.NullifierHash, request.RememberLogin, isTestHash: false);

                    return Ok(new { status = 200, message = "Erfolg!" });
                }
                else
                {
                    return BadRequest("Verifizierung fehlgeschlagen (False zur�ckgegeben).");
                }
            }
            catch (WorldMiniAppException ex)
            {
                _logger.LogWarning(ex, "VerifyAction WorldMiniApp error.");
                return StatusCode(ex.StatusCode, new { status = "error", message = ex.Message });
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
            // Sicherheit: Test-Hashes umgehen die SIWE-/Worldcoin-Verifizierung komplett
            // und dürfen daher NUR in der Entwicklungsumgebung gesetzt werden.
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(request?.UserHash))
            {
                return BadRequest(new { status = "error", message = "UserHash missing." });
            }

            var userHash = request.UserHash.Trim();
            await _userManager.CreateOrUpdateTestUserAsync(userHash, request.RememberLogin ?? true);
            await WorldMiniAppUserHashHelper.SignInAsync(HttpContext, userHash, request.RememberLogin ?? true, isTestHash: true);

            return Ok(new
            {
                status = "success",
                isValid = true,
                userHash,
                isTestHash = true
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ClearTestHash()
        {
            await WorldMiniAppUserHashHelper.SignOutAsync(HttpContext);

            return Ok(new
            {
                status = "success",
                isLoggedIn = false,
                isTestHash = false
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

        // =====================================================================
        // World ID 4.0 / IDKit Login
        // ---------------------------------------------------------------------
        // MiniKit v2 hat den verify-Befehl entfernt; World-ID-Verifizierung läuft
        // jetzt über IDKit (@worldcoin/idkit-core) im Frontend. IDKit verlangt ein
        // backend-signiertes rp_context (RP-Signatur). Die offizielle Signierfunktion
        // ist Node-only, daher wird sie aus unserem Node-Service bezogen
        // (Config WorldId:NodeSignerUrl). Wir reichen das fertige rp_context nur ans
        // Frontend durch – der Signing-Key verlässt nie den Server.
        // =====================================================================

        /// <summary>
        /// Liefert dem Frontend die für IDKit.request(...) nötigen Parameter inkl.
        /// des serverseitig (im Node-Service) signierten rp_context.
        /// </summary>
        [HttpGet]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> WorldIdRequestContext()
        {
            var appId = _configuration["WorldId:AppId"];
            var action = _configuration["WorldId:Action"] ?? "login-delikatessendrehbuch";
            var environment = _configuration["WorldId:Environment"] ?? "production";
            var signerUrl = _configuration["WorldId:NodeSignerUrl"];

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(signerUrl))
            {
                _logger.LogError("WorldId config missing (AppId/NodeSignerUrl).");
                return StatusCode(StatusCodes.Status500InternalServerError, new { status = "error", message = "World ID ist nicht konfiguriert." });
            }

            try
            {
                // rp_context vom Node-Signer holen (der kennt den RP_SIGNING_KEY + rp_id).
                var client = _httpClientFactory.CreateClient();

                // Optionaler Shared-Secret-Schutz zwischen C# und Node (gleiches Muster wie AgentApi).
                var internalKey = _configuration["TradingAgent:InternalApiKey"];
                if (!string.IsNullOrWhiteSpace(internalKey))
                {
                    client.DefaultRequestHeaders.Add("X-Agent-Api-Key", internalKey);
                }

                var signerResponse = await client.PostAsJsonAsync(signerUrl, new { action });
                if (!signerResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("World ID rp_context signing failed: {Status}", (int)signerResponse.StatusCode);
                    return StatusCode(StatusCodes.Status502BadGateway, new { status = "error", message = "Signierung fehlgeschlagen." });
                }

                // Erwartetes Format vom Node-Signer:
                // { rp_id, nonce, created_at, expires_at, signature }
                // WICHTIG: Die MVC-Pipeline serialisiert mit Newtonsoft. Ein
                // System.Text.Json.JsonElement würde von Newtonsoft FALSCH serialisiert
                // (created_at/expires_at kämen im Frontend als undefined an ->
                // "cannot convert undefined to a BigInt"). Daher als Newtonsoft-JObject
                // zurückgeben.
                var signerBody = await signerResponse.Content.ReadAsStringAsync();
                var rpContext = Newtonsoft.Json.Linq.JObject.Parse(signerBody);

                return Ok(new
                {
                    app_id = appId,
                    action,
                    environment,
                    allow_legacy_proofs = true,
                    rp_context = rpContext
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WorldIdRequestContext failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { status = "error", message = "Ein unerwarteter Fehler ist aufgetreten." });
            }
        }

        /// <summary>
        /// Verifiziert das von IDKit gelieferte Proof-Result serverseitig über den
        /// World-ID v4-Verify-Endpoint und meldet den Nutzer (Identität = nullifier) an.
        /// Das IDKit-Result wird unverändert weitergereicht ("forward as-is").
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> VerifyWorldId([FromBody] Newtonsoft.Json.Linq.JObject idkitResult, [FromQuery] bool rememberLogin = true)
        {
            var rpId = _configuration["WorldId:RpId"];
            if (string.IsNullOrWhiteSpace(rpId))
            {
                _logger.LogError("WorldId:RpId not configured.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { status = "error", message = "World ID ist nicht konfiguriert." });
            }

            if (idkitResult == null)
            {
                return BadRequest(new { status = "error", isValid = false, message = "Kein Result übergeben." });
            }

            try
            {
                // 1) Proof beim World-ID v4-Endpoint verifizieren. Body als ROHEN
                //    JSON-String weiterreichen ("forward as-is"); kein STJ-Re-Serialize
                //    eines Newtonsoft-Objekts.
                var verifyUrl = $"https://developer.world.org/api/v4/verify/{rpId}";
                var client = _httpClientFactory.CreateClient();
                // WICHTIG: User-Agent + Accept setzen. Ohne User-Agent blockt die
                // Cloudflare-WAF vor der World-API den Request mit HTML "403 Forbidden"
                // (kein JSON). Das ist KEIN Proof-Fehler, sondern Edge-Filtering.
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DelikatessenDrehbuch/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                var content = new StringContent(idkitResult.ToString(Newtonsoft.Json.Formatting.None), System.Text.Encoding.UTF8, "application/json");
                var verifyResponse = await client.PostAsync(verifyUrl, content);

                if (!verifyResponse.IsSuccessStatusCode)
                {
                    var body = await verifyResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("World ID v4 verify failed: {Status} {Body}", (int)verifyResponse.StatusCode, body);
                    // TEMP/DEBUG: v4-Ablehnungsgrund an den Client durchreichen, damit er
                    // im Debug-Overlay sichtbar wird. Später wieder entfernen.
                    var snippet = string.IsNullOrEmpty(body) ? "" : body.Substring(0, Math.Min(400, body.Length));
                    return BadRequest(new { status = "error", isValid = false, message = "Verifizierung fehlgeschlagen.", v4status = (int)verifyResponse.StatusCode, v4body = snippet });
                }

                // 2) nullifier + Level aus dem IDKit-Result lesen (responses[0]).
                //    Form: { ..., responses: [ { identifier, nullifier|nullifier_hash, ... } ] }
                string? nullifier = null;
                string verificationLevel = "device";
                if (idkitResult["responses"] is Newtonsoft.Json.Linq.JArray responses && responses.Count > 0)
                {
                    var first = responses[0];
                    nullifier = (string?)first["nullifier"] ?? (string?)first["nullifier_hash"];
                    verificationLevel = (string?)first["identifier"] ?? "device";
                }

                if (string.IsNullOrWhiteSpace(nullifier))
                {
                    _logger.LogWarning("World ID verify ok but nullifier missing in result.");
                    return BadRequest(new { status = "error", isValid = false, message = "Kein nullifier im Result." });
                }

                // 3) Replay-Schutz ergibt sich aus dem UserHash-Unique-Constraint (= nullifier);
                //    CreateOrUpdate legt bestehende Nutzer nicht doppelt an.
                await _userManager.CreateOrUpdateWorldIdUserAsync(nullifier, verificationLevel, rememberLogin);

                // 4) Anmelden (signiertes Auth-Cookie, siehe Cookie-Auth-Umstellung).
                await WorldMiniAppUserHashHelper.SignInAsync(HttpContext, nullifier, rememberLogin, isTestHash: false);

                return Ok(new { status = "success", isValid = true, userHash = nullifier });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyWorldId failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { status = "error", isValid = false, message = "Ein unerwarteter Fehler ist aufgetreten." });
            }
        }

        private static string GenerateSiweNonce()
        {
            return Guid.NewGuid().ToString("N").ToLowerInvariant();
        }

        private static bool IsWalletAuthPayloadSuccessful(WalletAuthPayloadDto payload)
        {
            if (payload == null)
            {
                return false;
            }

            if (string.Equals(payload.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(payload.Address)
                && !string.IsNullOrWhiteSpace(payload.Message)
                && !string.IsNullOrWhiteSpace(payload.Signature);
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


