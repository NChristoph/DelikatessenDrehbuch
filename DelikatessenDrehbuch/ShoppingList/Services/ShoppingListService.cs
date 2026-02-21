using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.ShoppingList.Model;
using System.Text.Json;

namespace DelikatessenDrehbuch.ShoppingList.Services.Interfaces
{
    public class ShoppingListService : IShoppingListService
    {
        
        private readonly ApplicationDbContext _context;
        private IUtilityService _utilityService;
        public ShoppingListService(ApplicationDbContext context,IUtilityService utilityService)
        {
            _context = context;
            _utilityService = utilityService;
        }
        public string CreateShoppingList(List<ShoppingListModel> shoppingListModels,string userMail)
        {
            var mealPlan=_context.SavedMealPlan.FirstOrDefault(x => x.UserMail == userMail);

            string jsonString = JsonSerializer.Serialize(shoppingListModels);

            mealPlan.ShoppingList = jsonString;

            var token = _utilityService.GenerateRandomToken(30);
            mealPlan.Token= token;

            _context.SaveChanges();
            return token;
        }

        public List<ShoppingListModel> GetShoppingList(string token) 
        {
            var mealPlan = _context.SavedMealPlan.FirstOrDefault(x => x.Token == token);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<ShoppingListModel>>(mealPlan.ShoppingList, options);
        }

    }
}
