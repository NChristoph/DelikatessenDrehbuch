using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IUserManager
    {
        public Task CreateNewUserAsync(VerifyRequestDto request);
        public Task CreateOrUpdateWalletUserAsync(string walletAddress, bool rememberLogin);
        public Task CreateOrUpdateTestUserAsync(string userHash, bool rememberLogin);
    }
}

