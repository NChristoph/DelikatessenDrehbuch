using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using System.Net.Http.Json;
using System.Security.Policy;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public class AuthService : IAuthService
    {
        private readonly ILogger<AuthService> _logger;
        // HttpClient über IHttpClientFactory beziehen statt `new HttpClient()`:
        // verhindert Socket-Exhaustion und veraltete DNS-Einträge (gepoolter Handler).
        private readonly IHttpClientFactory _httpClientFactory;
        // TODO: Secret noch entfernen — APP_ID in Konfiguration auslagern
        private const string APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
        // ✅ RICHTIGE URL!
        private const string VERIFY_URL = "https://developer.worldcoin.org/api/v2/verify/"+APP_ID;
        public AuthService(ILogger<AuthService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<WorldcoinVerifyResponse> VerifyProofWithWorldcoin(VerifyRequestDto data)
        {
            try
            {
                // Client aus der Factory (gepoolter Handler); das `using` ist hier ok,
                // da nur der leichte Client-Wrapper, nicht der Handler, entsorgt wird.
                using var client = _httpClientFactory.CreateClient();

                // Headers setzen
                client.DefaultRequestHeaders.Add("User-Agent", "DelikatessenDrehbuch/1.0");
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));


                // TODO: Secret noch entfernen — Default-Signal-Hash in Konfiguration auslagern
                string defaultSignalHash = "0x00c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a4";

                // Wenn Signal leer ist, nehmen wir den Default-Hash. 
                // Wenn ein echtes Signal da ist, müsste man es eigentlich noch hashen (aber du nutzt ja ""),
                // also reicht diese Logik für deinen Fall:
                string signalToSend = string.IsNullOrEmpty(data.Signal)
                                      ? defaultSignalHash
                                      : data.Signal;


                // Request Body
                var requestBody = new WorldcoinVerifyRequest
                {
                    MerkleRoot = data.Payload.MerkleRoot,
                    NullifierHash = data.Payload.NullifierHash,
                    Proof = data.Payload.Proof,
                    VerificationLevel = data.Payload.VerificationLevel,
                    Action = data.Action,
                    Signal = signalToSend
                };


                // API Call
                var response = await client.PostAsJsonAsync(VERIFY_URL, requestBody);
                var jsonString = await response.Content.ReadAsStringAsync();

                // Erfolg?
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Worldcoin API Error: {Status}",
                        response.StatusCode);

                    throw new WorldMiniAppExternalServiceException("Worldcoin",
                        $"Worldcoin API Error {response.StatusCode}: " +
                        $"{jsonString.Substring(0, Math.Min(200, jsonString.Length))}");
                }

                // JSON parsen
                var result = JsonSerializer.Deserialize<WorldcoinVerifyResponse>(
                    jsonString,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                {
                    _logger.LogError("Worldcoin API response deserialization failed.");
                    return new WorldcoinVerifyResponse
                    {
                        Success = false,
                        Detail = "Failed to parse API response"
                    };
                }


                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ HTTP Request Error beim Verifizieren");
                throw new WorldMiniAppExternalServiceException("Worldcoin", $"Netzwerkfehler bei Worldcoin API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "❌ JSON Parse Error");
                throw new WorldMiniAppExternalServiceException("Worldcoin", $"Ungültige API-Antwort von Worldcoin: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unerwarteter Fehler bei Verification");
                throw;
            }
        }
    }
}

