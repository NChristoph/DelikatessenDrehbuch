using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public abstract class WorldMiniAppBaseController : Controller
    {
        protected IConfiguration Configuration =>
            HttpContext.RequestServices.GetRequiredService<IConfiguration>();

        protected string SuperUserHash => Configuration["WorldMiniApp:SuperUserHash"] ?? string.Empty;

        protected string ResolveUserHash(string userHash)
        {
            return WorldMiniAppUserHashHelper.Resolve(HttpContext, userHash);
        }

        // Parameterlose Variante für Controller, die keinen expliziten Hash übergeben.
        // Identität kommt ohnehin nur aus dem Auth-Cookie (Claim).
        protected string ResolveUserHash()
        {
            return WorldMiniAppUserHashHelper.Resolve(HttpContext);
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
