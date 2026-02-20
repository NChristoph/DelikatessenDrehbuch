using System.Text.Json.Serialization; // Standard .NET
using Newtonsoft.Json;                // Newtonsoft (oft in älteren/komplexen Projekten)

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    // === 1. EMPFANG: Daten vom Frontend (Legacy MiniKit verify) ===
    public class VerifyRequestDto
    {
        [JsonPropertyName("payload")]
        [JsonProperty("payload")]
        public VerifyPayloadDto Payload { get; set; }

        [JsonPropertyName("action")]
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonPropertyName("signal")]
        [JsonProperty("signal")]
        public string Signal { get; set; }

        [JsonPropertyName("rememberLogin")]
        [JsonProperty("rememberLogin")]
        public bool RememberLogin { get; set; }
    }

    // === 1b. EMPFANG: Daten vom Frontend (WalletAuth / SIWE) ===
    public class WalletAuthRequestDto
    {
        [JsonPropertyName("payload")]
        [JsonProperty("payload")]
        public WalletAuthPayloadDto Payload { get; set; }

        [JsonPropertyName("nonce")]
        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonPropertyName("rememberLogin")]
        [JsonProperty("rememberLogin")]
        public bool RememberLogin { get; set; }
    }

    public class WalletAuthPayloadDto
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonPropertyName("message")]
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonPropertyName("signature")]
        [JsonProperty("signature")]
        public string Signature { get; set; }

        [JsonPropertyName("address")]
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonPropertyName("version")]
        [JsonProperty("version")]
        public int Version { get; set; }
    }

    public class WalletNonceResponseDto
    {
        [JsonPropertyName("nonce")]
        [JsonProperty("nonce")]
        public string Nonce { get; set; }
    }

    // === 1c. Interne SIWE Verification Response ===
    public class WalletSiweVerifyResponseDto
    {
        public bool IsValid { get; set; }
        public string Address { get; set; }
    }

    public class VerifyPayloadDto
    {
        [JsonPropertyName("status")]
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonPropertyName("proof")]
        [JsonProperty("proof")]
        public string Proof { get; set; }

        // HIER WAR DAS PROBLEM (Unterstrich): Jetzt doppelt abgesichert!
        [JsonPropertyName("merkle_root")]
        [JsonProperty("merkle_root")]
        public string MerkleRoot { get; set; }

        [JsonPropertyName("nullifier_hash")]
        [JsonProperty("nullifier_hash")]
        public string NullifierHash { get; set; }

        [JsonPropertyName("verification_level")]
        [JsonProperty("verification_level")]
        public string VerificationLevel { get; set; }
    }

    // === 2. VERSAND: Daten an die Worldcoin Developer API ===
    public class WorldcoinVerifyRequest
    {
        [JsonPropertyName("merkle_root")]
        [JsonProperty("merkle_root")]
        public string MerkleRoot { get; set; }

        [JsonPropertyName("nullifier_hash")]
        [JsonProperty("nullifier_hash")]
        public string NullifierHash { get; set; }

        [JsonPropertyName("proof")]
        [JsonProperty("proof")]
        public string Proof { get; set; }

        [JsonPropertyName("verification_level")]
        [JsonProperty("verification_level")]
        public string VerificationLevel { get; set; }

        [JsonPropertyName("action")]
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonPropertyName("signal_hash")]
        [JsonProperty("signal_hash")]
        public string Signal { get; set; }
    }

    // === 3. ANTWORT: Das Ergebnis von der Worldcoin API ===
    public class WorldcoinVerifyResponse
    {
        [JsonPropertyName("success")]
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonPropertyName("code")]
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonPropertyName("detail")]
        [JsonProperty("detail")]
        public string Detail { get; set; }
    }

    public class RefreshLoginRequest
    {
        [JsonPropertyName("userHash")]
        [JsonProperty("userHash")]
        public string UserHash { get; set; }

        [JsonPropertyName("rememberLogin")]
        [JsonProperty("rememberLogin")]
        public bool? RememberLogin { get; set; }
    }
}
