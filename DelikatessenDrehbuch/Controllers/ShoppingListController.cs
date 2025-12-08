using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.ShoppingList.Model;
using DelikatessenDrehbuch.ShoppingList.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.ConstrainedExecution;

namespace DelikatessenDrehbuch.Controllers
{
    public class ShoppingListController : Controller
    {
        private readonly IShoppingListService _shoppingListService;
        private readonly ApplicationDbContext _context;
        public ShoppingListController(IShoppingListService shoppingListService, ApplicationDbContext context)
        {
            _shoppingListService = shoppingListService;
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShoppingList([FromBody] List<ShoppingListModel> data)
        {
            // 1. Sicherheitscheck
            if (data == null || !data.Any())
            {
                return BadRequest(new { success = false, message = "Die Liste ist leer." });
            }

            try
            {
                var userEmail = User.Identity.Name;

                var token = _shoppingListService.CreateShoppingList(data, userEmail);


                var link = Url.Action("LoadShoppingList", "ShoppingList", new { token }, Request.Scheme);

                // 5. Antwort an JavaScript senden
                return Ok(new { success = true, shareUrl = link });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [AllowAnonymous]
        [HttpGet("/ShoppingList/{token?}", Name = "ShoppingList")]
        [HttpGet("/ShoppingList/LoadShoppingList")]
        public IActionResult LoadShoppingList(string? token)
        {
         
            var model=_shoppingListService.GetShoppingList(token);

            return View("~/Views/ShoppingList/ShoppingList.cshtml", model);
        }



    }
}



