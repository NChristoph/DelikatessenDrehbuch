using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public abstract class WorldMiniAppBaseController : Controller
    {
        // TODO: Secret noch entfernen — in Konfiguration (appsettings / Environment Variable) auslagern
        protected const string SuperUserHash = "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839";

        protected string ResolveUserHash(string userHash)
        {
            return WorldMiniAppUserHashHelper.Resolve(HttpContext, userHash);
        }

        protected async Task<bool> IsCreatorAllowedAsync(ApplicationDbContext context, string userHash)
        {
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            if (userHash == SuperUserHash)
            {
                return true;
            }

            var user = await context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            return user?.IsVerified == "orb";
        }
    }
}
