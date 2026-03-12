using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IUserManager
    {
        public Task CreateNewUser(VerifyRequestDto request);
        public Task CreateOrUpdateWalletUser(string walletAddress, bool rememberLogin);
        public Task CreateOrUpdateTestUser(string userHash, bool rememberLogin);
    }
}

