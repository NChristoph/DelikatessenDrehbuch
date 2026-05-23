using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Nethereum.Signer;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class DiagnosticsController : Controller
    {
        private readonly IConfiguration _configuration;

        public DiagnosticsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Prüft, ob der verschlüsselte Private Key zur Wallet-Adresse passt
        /// URL: /WorldMiniApp/Diagnostics/CheckPrivateKey
        /// </summary>
        public IActionResult CheckPrivateKey()
        {
            var result = new StringBuilder();
            result.AppendLine("=== PRIVATE KEY CHECKER ===\n");

            // 1. Deine Daten (aus DB)
            var encryptedKeyBase64 = "qaV11o8vCYr9yME1PaHNrCorAKo/Zv0/yHKK9JLvQYdtnhH1pTyvotYTw6bOYXfEsQ5Y4e7lnLjGvPd0lzyUs1gz0xvmv7eDzdxmZ8Nl0005Wkzzq4NBozEAwkAsPYWT6C2KQWgpB6JJ7QBqqfvxhPTlgng2Xy+5tfB5thxj95vxDFzdNOL2bSZI6LiZVmBpPlrcfV1FTm0VJmxBVF6/GqBr27HGm/LVh7kk9KJ0X4UBKTf1qNvfNQEkw7Tr3qiCn40nBLkCndJYeI9Ll8SrYtlWSgNXSNFMfJxI0UQrm4aax4o5drxJxQYkr32Jj39gb8OuEUNToahP3L5GIm15XPu/UzT+xZt0TJs8CX7n46Y=";
            var expectedAddress = "0x67c15f0f39b5f80ea65a373670f72736657663a5";

            // 2. Encryption Key (aus appsettings.json)
            var encryptionKeyString = _configuration["TradingAgent:EncryptionKey"] ?? "CHANGE_THIS_TO_SECURE_KEY_32BYTES!";
            var encryptionKey = Encoding.UTF8.GetBytes(encryptionKeyString.PadRight(32).Substring(0, 32));

            result.AppendLine($"Encrypted Key (Base64): {encryptedKeyBase64.Substring(0, 50)}...");
            result.AppendLine($"Expected Address: {expectedAddress}");
            result.AppendLine();

            try
            {
                // 3. Entschlüssele den Private Key
                result.AppendLine("🔓 Decrypting private key...");
                var privateKeyHex = DecryptPrivateKey(encryptedKeyBase64, encryptionKey);
                result.AppendLine($"Decrypted Private Key: {privateKeyHex.Substring(0, 20)}... (length: {privateKeyHex.Length})");
                result.AppendLine();

                // 4. Versuche Ethereum-Adresse aus Private Key abzuleiten
                result.AppendLine("🔑 Attempting to derive Ethereum address from private key...");
                result.AppendLine();

                try
                {
                    // Versuche mit Nethereum (secp256k1)
                    var ecKey = new EthECKey(privateKeyHex);
                    var derivedAddress = ecKey.GetPublicAddress();

                    result.AppendLine($"✅ Derived Address (secp256k1): {derivedAddress}");
                    result.AppendLine($"📋 Expected Address:            {expectedAddress}");
                    result.AppendLine();

                    if (derivedAddress.Equals(expectedAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        result.AppendLine("🎉🎉🎉 SUCCESS! Private key matches the address! 🎉🎉🎉");
                        result.AppendLine("✅ Die 33 WLD KÖNNEN abgehoben werden!");
                        result.AppendLine();
                        result.AppendLine("Du kannst den Private Key verwenden, um die Coins abzuheben!");
                    }
                    else
                    {
                        result.AppendLine("❌❌❌ MISMATCH! Private key does NOT match the address! ❌❌❌");
                        result.AppendLine("💔 Die 33 WLD sind VERLOREN!");
                        result.AppendLine();
                        result.AppendLine("Grund:");
                        result.AppendLine("- Die Wallet-Adresse wurde ZUFÄLLIG generiert (nicht aus Private Key abgeleitet)");
                        result.AppendLine("- Der Private Key passt NICHT zur Adresse");
                        result.AppendLine("- Niemand hat den richtigen Private Key für diese Adresse");
                        result.AppendLine();
                        result.AppendLine("Vergleich:");
                        result.AppendLine($"  Erwartet: {expectedAddress}");
                        result.AppendLine($"  Tatsächlich: {derivedAddress}");
                    }
                }
                catch (Exception ex)
                {
                    result.AppendLine($"❌ ERROR deriving address with secp256k1: {ex.Message}");
                    result.AppendLine();
                    result.AppendLine("Das bestätigt, dass der Private Key NICHT im korrekten Ethereum-Format ist!");
                    result.AppendLine("💔 Die 33 WLD sind VERLOREN!");
                    result.AppendLine();
                    result.AppendLine("Technische Details:");
                    result.AppendLine($"  Exception: {ex.GetType().Name}");
                    result.AppendLine($"  Message: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"❌ FATAL ERROR: {ex.Message}");
                result.AppendLine($"Stack: {ex.StackTrace}");
            }

            result.AppendLine("\n=== ENDE ===");

            return Content(result.ToString(), "text/plain");
        }

        private string DecryptPrivateKey(string encryptedKeyBase64, byte[] encryptionKey)
        {
            var fullCipher = Convert.FromBase64String(encryptedKeyBase64);

            using var aes = Aes.Create();
            aes.Key = encryptionKey;

            var iv = new byte[16];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
