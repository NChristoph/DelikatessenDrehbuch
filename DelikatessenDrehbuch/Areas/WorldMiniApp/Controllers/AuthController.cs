using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IUserManager _userManager;
        public AuthController(IAuthService authService, IUserManager userManager)
        {
            _authService = authService;
            _userManager = userManager;
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
        public IActionResult Index()
        {
            return View();
        }
    }
}
