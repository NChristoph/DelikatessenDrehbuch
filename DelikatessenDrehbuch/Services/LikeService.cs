using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class LikeService:ILikeService
    {
        private readonly ApplicationDbContext _context;
      

        public LikeService(ApplicationDbContext context)
        {
            _context = context;
          
        }

        public async Task<List<Like>> GetLikesByRecipeIdAsync(int recipeId)
        {
            return await _context.Likes.Where(x=>x.Recipe.Id == recipeId).ToListAsync();
        }

      

        public async Task RemoveLikeAsync(Like like)
        {
            like.Recipe.LikeCount--;
            _context.Likes.Remove(like);
           
           await _context.SaveChangesAsync();
        }

        public async Task CreateLikeAsync(int recipesId, string userMail)
        {
            var like = new Like
            {
                UserMail = userMail,
                Recipe = await _context.Recipes.SingleAsync(x=>x.Id==recipesId)
            };

            _context.Likes.Add(like);

            like.Recipe.LikeCount++;

            await _context.SaveChangesAsync();
        }
    }
}
