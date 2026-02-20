using AspNetCoreGeneratedDocument;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public class UserManager : IUserManager
    {

        private readonly ApplicationDbContext _context;
        public UserManager(ApplicationDbContext context)
        {
         _context = context;
        }
        public Task CreateNewUser(VerifyRequestDto request)
        {
            var user = _context.WorldAppUser.FirstOrDefault(x => x.UserHash == request.Payload.NullifierHash);

            if(user != null)
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


            _context.SaveChangesAsync();

            return Task.CompletedTask;
        }
    }
}
