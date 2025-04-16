using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace DelikatessenDrehbuch.Controllers
{
    public class CreateMealPlanController:Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            return View("../MyRecipes/CreateMealPlanView");
        }
    }
}
