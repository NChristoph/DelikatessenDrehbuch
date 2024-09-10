using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Policy;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceProvider _serviceProvider;


        public PaymentController(ApplicationDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;

        }
        public IActionResult Index()
        {
            return View();
        }

        public async Task< IActionResult> Success()
        {

            var user = User.Identity;
            if(user!=null&&user.IsAuthenticated)
            {
                var currentUser = _context.Users.FirstOrDefault(x => x.UserName == user.Name);
                var rolmenager = _serviceProvider.GetService<RoleManager<IdentityRole>>();


              
                if (currentUser != null)
                {
                    var userManager = _serviceProvider.GetService<UserManager<IdentityUser>>();
                   
                  var result= await  userManager.AddToRoleAsync(currentUser, "PremiumUser");
                    
                }
            }
            return View();
        }

       
    }
}
