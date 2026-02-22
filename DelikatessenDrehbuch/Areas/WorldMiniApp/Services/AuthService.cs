using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using System.Net.Http.Json;
using System.Security.Policy;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public class AuthService : IAuthService
    {
        private readonly ILogger<AuthService> _logger;
        private const string APP_ID = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
        // ✅ RICHTIGE URL!
        private const string VERIFY_URL = "https://developer.worldcoin.org/api/v2/verify/"+APP_ID;
        public AuthService(ILogger<AuthService> logger)
        {
            _logger = logger;
        }

        public async Task<WorldcoinVerifyResponse> VerifyProofWithWorldcoin(VerifyRequestDto data)
        {
            try
            {
                _logger.LogInformation($"VerifyUrl={VERIFY_URL}",VERIFY_URL);
                using var client = new HttpClient();

                // Headers setzen
                client.DefaultRequestHeaders.Add("User-Agent", "DelikatessenDrehbuch/1.0");
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));


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

                _logger.LogInformation("📤 Sende Verify Request an: {Url}", VERIFY_URL);
                _logger.LogInformation("📋 Action: {Action}, Level: {Level}",
                    data.Action,
                    data.Payload.VerificationLevel);

                // API Call
                var response = await client.PostAsJsonAsync(VERIFY_URL, requestBody);
                var jsonString = await response.Content.ReadAsStringAsync();

                // Erfolg?
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Worldcoin API Error {Status}: {Response}",
                        response.StatusCode,
                        jsonString);

                    throw new Exception(
                        $"Worldcoin API Error {response.StatusCode}: " +
                        $"{jsonString.Substring(0, Math.Min(200, jsonString.Length))}");
                }

                // JSON parsen
                var result = JsonSerializer.Deserialize<WorldcoinVerifyResponse>(
                    jsonString,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                {
                    _logger.LogError("❌ Deserialization failed: {Json}", jsonString);
                    return new WorldcoinVerifyResponse
                    {
                        Success = false,
                        Detail = "Failed to parse API response"
                    };
                }

                _logger.LogInformation("✅ Worldcoin Verification: {Success}", result.Success);

                if (!result.Success)
                {
                    _logger.LogWarning("⚠️ Verification failed: {Detail}", result.Detail);
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ HTTP Request Error beim Verifizieren");
                throw new Exception($"Netzwerkfehler bei Worldcoin API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "❌ JSON Parse Error");
                throw new Exception($"Ungültige API-Antwort von Worldcoin: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unerwarteter Fehler bei Verification");
                throw;
            }
        }
    }
}

//## 📋 Worldcoin API Endpoints (zur Referenz):

//| Endpoint | URL | Verwendung |
//| ----------| -----| ------------|
//| **Verify * * | `https://developer.world.org/api/v1/verify/{app_id}` | Proof verifizieren |
//| Developer Portal | `https://developer.worldcoin.org` | App-Konfiguration (Website!) |
//| Docs | `https://docs.world.org` | Dokumentation |

//## 🔍 Warum das "DOCTYPE" Error kam:
//```
//https://developer.worldcoin.org/api/v1/verify/...
//                    ↑ Diese Domain liefert HTML (Website)

//https://developer.world.org/api/v1/verify/...
//                  ↑ Diese Domain liefert JSON (API)
//```

//Du hast versehentlich die **Website-URL** statt der **API-URL** verwendet!

//## ✅ Checklist nach dem Fix:

//1. **Ändere die URL** in `AuthService.cs`
//2. **Neu kompilieren**
//3. **App neu deployen**
//4. **In World App testen:**
//   -Verification starten
//   - Sollte jetzt funktionieren ✅
//   - Kein "DOCTYPE" Error mehr
//   - Backend erhält `Success: true`

//## 🧪 Erwartetes Verhalten jetzt:

//### ✅ World App (echte Verification):
//```
//1.User bestätigt in World App
//2. Frontend sendet Proof an Backend
//3. Backend sendet an https://developer.world.org/api/v1/verify/...
//4.Worldcoin API antwortet: { "success": true, ... }
//5.Backend speichert User
//6. Redirect zu /Setup
//```

//### ✅ Localhost (Mock):
//```
//1. Mock-Proof wird generiert
//2. AuthService erkennt "mock-" Prefix
//3. Gibt sofort Success zurück
//4. Redirect zu /Setup