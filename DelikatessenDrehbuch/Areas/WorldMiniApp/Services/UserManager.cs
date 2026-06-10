using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public class UserManager : IUserManager
    {
        private readonly ApplicationDbContext _context;

        public UserManager(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateNewUserAsync(VerifyRequestDto request)
        {
            var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == request.Payload.NullifierHash);

            if (user != null)
            {
                user.Lastlogin = DateTime.Now;
                user.IsVerified = request.Payload.VerificationLevel;
                user.RememberLogin = request.RememberLogin;
            }
            else
            {
                user = new WorldAppUser
                {
                    UserHash = request.Payload.NullifierHash,
                    IsVerified = request.Payload.VerificationLevel,
                    Lastlogin = DateTime.Now,
                    RememberLogin = request.RememberLogin
                };
                _context.WorldAppUser.Add(user);
            }

            await _context.SaveChangesAsync();
        }


        public async Task CreateOrUpdateTestUserAsync(string userHash, bool rememberLogin)
        {
            var normalizedHash = userHash?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedHash))
            {
                return;
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == normalizedHash);

            if (user != null)
            {
                user.Lastlogin = DateTime.Now;
                user.IsVerified = "test";
                user.RememberLogin = rememberLogin;
                user.UserName ??= normalizedHash;
            }
            else
            {
                user = new WorldAppUser
                {
                    UserHash = normalizedHash,
                    IsVerified = "test",
                    Lastlogin = DateTime.Now,
                    RememberLogin = rememberLogin,
                    UserName = normalizedHash
                };
                _context.WorldAppUser.Add(user);
            }

            await _context.SaveChangesAsync();
        }
        public async Task CreateOrUpdateWalletUserAsync(string walletAddress, bool rememberLogin)
        {
            var normalizedWallet = walletAddress?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedWallet))
            {
                return;
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == normalizedWallet);

            if (user != null)
            {
                user.Lastlogin = DateTime.Now;
                user.IsVerified = "wallet";
                user.RememberLogin = rememberLogin;
            }
            else
            {
                user = new WorldAppUser
                {
                    UserHash = normalizedWallet,
                    IsVerified = "wallet",
                    Lastlogin = DateTime.Now,
                    RememberLogin = rememberLogin,
                    UserName = normalizedWallet
                };
                _context.WorldAppUser.Add(user);
            }

            await _context.SaveChangesAsync();
        }

        // World ID 4.0 / IDKit: Identität ist der RP-scoped `nullifier` aus dem Proof.
        // verificationLevel z.B. "orb" (aus dem gewählten Preset abgeleitet).
        public async Task CreateOrUpdateWorldIdUserAsync(string nullifier, string verificationLevel, bool rememberLogin)
        {
            var normalizedHash = nullifier?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedHash))
            {
                return;
            }

            // Login läuft device-level -> Default "device" (nicht "orb"!), falls das
            // Result keinen identifier liefert. Orb wird nur für Upload/Creator gebraucht.
            var level = string.IsNullOrWhiteSpace(verificationLevel) ? "device" : verificationLevel.Trim();

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == normalizedHash);

            if (user != null)
            {
                user.Lastlogin = DateTime.Now;
                // Eine bereits vorhandene Orb-Verifizierung NICHT durch ein Device-Login
                // herabstufen (Orb-Status wird für Upload/Creator benötigt).
                if (!string.Equals(user.IsVerified, "orb", StringComparison.OrdinalIgnoreCase))
                {
                    user.IsVerified = level;
                }
                user.RememberLogin = rememberLogin;
            }
            else
            {
                user = new WorldAppUser
                {
                    UserHash = normalizedHash,
                    IsVerified = level,
                    Lastlogin = DateTime.Now,
                    RememberLogin = rememberLogin
                };
                _context.WorldAppUser.Add(user);
            }

            await _context.SaveChangesAsync();
        }
    }
}

