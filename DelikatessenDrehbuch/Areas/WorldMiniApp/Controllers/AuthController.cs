using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class AuthController : Controller
    {
        private const string SessionUserHashKey = "WorldMiniAppUserHash";
        private readonly IAuthService _authService;
        private readonly IUserManager _userManager;
        private readonly IWorldAppMealPlanService _worldAppMealPlanService;
        private readonly ApplicationDbContext _context;
        public AuthController(IAuthService authService, IUserManager userManager, IWorldAppMealPlanService worldAppMealPlanService, ApplicationDbContext context)
        {
            _authService = authService;
            _userManager = userManager;
            _worldAppMealPlanService = worldAppMealPlanService;
            _context = context;
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> VerifyAction([FromBody] VerifyRequestDto request)
        {
            if (request?.Payload == null || request.Payload.Status != "success")
            {
                return BadRequest("Payload invalid.");
            }

            try
            {
                
                var isValid = await _authService.VerifyProofWithWorldcoin(request);

                if (isValid.Success)
                {
                    await _userManager.CreateNewUser(request);
                    HttpContext.Session.SetString(SessionUserHashKey, request.Payload.NullifierHash);
                   

                    return Ok(new { status = 200, message = "Erfolg!" });
                }
                else
                {
                    return BadRequest("Verifizierung fehlgeschlagen (False zurückgegeben).");
                }
            }
            catch (Exception ex)
            {
                
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RefreshStatus([FromBody] RefreshLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.UserHash))
            {
                return BadRequest("UserHash missing.");
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == request.UserHash);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            user.Lastlogin = DateTime.Now;
            if (request.RememberLogin.HasValue)
            {
                user.RememberLogin = request.RememberLogin.Value;
            }

            await _context.SaveChangesAsync();
            HttpContext.Session.SetString(SessionUserHashKey, request.UserHash);

            return Ok(new { status = user.IsVerified, rememberLogin = user.RememberLogin });
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}
