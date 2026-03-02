using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.ShoppingList.Model;

namespace DelikatessenDrehbuch.ShoppingList.Services.Interfaces
{
    public interface IShoppingListService
    {
        public string CreateShoppingList(List<ShoppingListModel> shoppingListModels,string userMail);

        public List<ShoppingListModel> GetShoppingList(string token);
    }
}
