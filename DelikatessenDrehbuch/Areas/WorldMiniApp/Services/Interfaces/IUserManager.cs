using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IUserManager
    {
        public Task CreateNewUser(VerifyRequestDto request);
    }
}
