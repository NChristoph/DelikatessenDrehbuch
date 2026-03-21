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
    }
}

