using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class MyRecipesController : Controller
    {
        private readonly ApplicationDbContext _dbcontext;

        public MyRecipesController(ApplicationDbContext context)
        {
            _dbcontext = context;
        }

        public IActionResult Index()
        {
            var myrecipes = _dbcontext.Recipes.Where(x => x.OwnerEmail == User.Identity.Name);

            return View();
        }
    }
}
