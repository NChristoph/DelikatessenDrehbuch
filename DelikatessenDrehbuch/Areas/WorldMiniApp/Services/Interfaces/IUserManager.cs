using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IUserManager
    {
        public Task CreateNewUserAsync(VerifyRequestDto request);
        public Task CreateOrUpdateWalletUserAsync(string walletAddress, bool rememberLogin);
        public Task CreateOrUpdateTestUserAsync(string userHash, bool rememberLogin);
        // World ID 4.0 / IDKit: legt/aktualisiert den Nutzer anhand des nullifier an.
        public Task CreateOrUpdateWorldIdUserAsync(string nullifier, string verificationLevel, bool rememberLogin);
    }
}

