using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;

namespace DelikatessenDrehbuch.Services
{
    public class RecipesService : IRecipesService
    {
        private readonly ApplicationDbContext _context;

        public RecipesService(ApplicationDbContext context)
        {
            _context = context;
        }
        public Recipes GetOneRendomRecipeFromIdList(List<int> ids)
        {
            Random rand = new Random();
            var randomId = ids.OrderBy(x => rand.Next()).Take(1).FirstOrDefault();
            return _context.Recipes.Single(x => x.Id == randomId);
        }

        public List<int> GetRecipesIdsByCategory(string category)
        {
            return _context.Recipes.Where(x => x.Category.ToLower().Trim() == category.ToLower().Trim())
                                                  .Select(x => x.Id).ToList();
        }
    }
}
