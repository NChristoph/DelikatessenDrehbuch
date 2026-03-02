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
            // 1. MOCK CHECK für Localhost
            if (data.Payload.Proof.StartsWith("mock-"))
            {
                _logger.LogInformation("🎭 Mock Proof akzeptiert");
                return new WorldcoinVerifyResponse
                {
                    Success = true,
                    Detail = "Mock Proof accepted (Development Mode)"
                };
            }



            try
            {
                _logger.LogInformation("VerifyUrl={Url}", VERIFY_URL);
                using var client = new HttpClient();

                // Headers setzen
                client.DefaultRequestHeaders.Add("User-Agent", "DelikatessenDrehbuch/1.0");
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));


                // Request Body als Dictionary: signal_hash nur senden wenn custom Signal vorhanden
                // (verifyCloudProof sendet signal_hash auch nicht wenn kein Signal gesetzt)
                var bodyDict = new Dictionary<string, string>
                {
                    ["merkle_root"] = data.Payload.MerkleRoot,
                    ["nullifier_hash"] = data.Payload.NullifierHash,
                    ["proof"] = data.Payload.Proof,
                    ["verification_level"] = data.Payload.VerificationLevel,
                    ["action"] = data.Action
                };

                if (!string.IsNullOrEmpty(data.Signal))
                {
                    bodyDict["signal_hash"] = data.Signal;
                }

                var requestJson = JsonSerializer.Serialize(bodyDict);

                _logger.LogInformation("📤 Sende Verify Request an: {Url}", VERIFY_URL);
                _logger.LogInformation("📋 Action: {Action}, Level: {Level}",
                    data.Action,
                    data.Payload.VerificationLevel);
                _logger.LogInformation("📋 Request Body: {Body}", requestJson);

                // API Call
                var jsonContent = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(VERIFY_URL, jsonContent);
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