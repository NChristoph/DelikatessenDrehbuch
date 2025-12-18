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
            }
            else
            {
                user = new WorldAppUser();

                user.UserHash = request.Payload.NullifierHash;
                user.IsVerified = request.Payload.VerificationLevel;
                user.Lastlogin= DateTime.Now;
            }

            _context.WorldAppUser.Add(user);
            _context.SaveChangesAsync();

            return Task.CompletedTask;
        }
    }
}
