using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IAuthService
    {
        public Task<WorldcoinVerifyResponse> VerifyProofWithWorldcoin(VerifyRequestDto data);
    }
}
