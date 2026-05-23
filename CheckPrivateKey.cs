using System;
using System.Security.Cryptography;
using System.Text;
using Nethereum.Signer;

namespace DelikatessenDrehbuch.Diagnostics
{
    /// <summary>
    /// Tool zum Prüfen, ob der verschlüsselte Private Key zur Wallet-Adresse passt
    /// </summary>
    public class PrivateKeyChecker
    {
        public static void CheckPrivateKey()
        {
            Console.WriteLine("=== PRIVATE KEY CHECKER ===\n");

            // 1. Deine Daten
            var encryptedKeyBase64 = "qaV11o8vCYr9yME1PaHNrCorAKo/Zv0/yHKK9JLvQYdtnhH1pTyvotYTw6bOYXfEsQ5Y4e7lnLjGvPd0lzyUs1gz0xvmv7eDzdxmZ8Nl0005Wkzzq4NBozEAwkAsPYWT6C2KQWgpB6JJ7QBqqfvxhPTlgng2Xy+5tfB5thxj95vxDFzdNOL2bSZI6LiZVmBpPlrcfV1FTm0VJmxBVF6/GqBr27HGm/LVh7kk9KJ0X4UBKTf1qNvfNQEkw7Tr3qiCn40nBLkCndJYeI9Ll8SrYtlWSgNXSNFMfJxI0UQrm4aax4o5drxJxQYkr32Jj39gb8OuEUNToahP3L5GIm15XPu/UzT+xZt0TJs8CX7n46Y=";
            var expectedAddress = "0x67c15f0f39b5f80ea65a373670f72736657663a5";

            // 2. Encryption Key (aus appsettings.json)
            var encryptionKeyString = "CHANGE_THIS_TO_SECURE_KEY_32BYTES!"; // ⚠️ ANPASSEN falls geändert!
            var encryptionKey = Encoding.UTF8.GetBytes(encryptionKeyString.PadRight(32).Substring(0, 32));

            Console.WriteLine($"Encrypted Key (Base64): {encryptedKeyBase64.Substring(0, 50)}...");
            Console.WriteLine($"Expected Address: {expectedAddress}");
            Console.WriteLine();

            try
            {
                // 3. Entschlüssele den Private Key
                Console.WriteLine("🔓 Decrypting private key...");
                var privateKeyHex = DecryptPrivateKey(encryptedKeyBase64, encryptionKey);
                Console.WriteLine($"Decrypted Private Key: {privateKeyHex.Substring(0, 20)}... (length: {privateKeyHex.Length})");
                Console.WriteLine();

                // 4. Versuche Ethereum-Adresse aus Private Key abzuleiten
                Console.WriteLine("🔑 Attempting to derive Ethereum address from private key...");

                try
                {
                    // Versuche mit Nethereum (secp256k1)
                    var ecKey = new EthECKey(privateKeyHex);
                    var derivedAddress = ecKey.GetPublicAddress();

                    Console.WriteLine($"✅ Derived Address (secp256k1): {derivedAddress}");
                    Console.WriteLine($"📋 Expected Address:            {expectedAddress}");
                    Console.WriteLine();

                    if (derivedAddress.Equals(expectedAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("🎉 SUCCESS! Private key matches the address!");
                        Console.WriteLine("✅ Die 33 WLD können abgehoben werden!");
                    }
                    else
                    {
                        Console.WriteLine("❌ MISMATCH! Private key does NOT match the address!");
                        Console.WriteLine("💔 Die 33 WLD sind VERLOREN!");
                        Console.WriteLine();
                        Console.WriteLine("Grund:");
                        Console.WriteLine("- Die Wallet-Adresse wurde ZUFÄLLIG generiert (nicht aus Private Key abgeleitet)");
                        Console.WriteLine("- Der Private Key passt NICHT zur Adresse");
                        Console.WriteLine("- Niemand hat den richtigen Private Key für diese Adresse");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ ERROR deriving address with secp256k1: {ex.Message}");
                    Console.WriteLine();
                    Console.WriteLine("Das bestätigt, dass der Private Key NICHT im korrekten Ethereum-Format ist!");
                    Console.WriteLine("💔 Die 33 WLD sind VERLOREN!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\n=== ENDE ===");
        }

        private static string DecryptPrivateKey(string encryptedKeyBase64, byte[] encryptionKey)
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
