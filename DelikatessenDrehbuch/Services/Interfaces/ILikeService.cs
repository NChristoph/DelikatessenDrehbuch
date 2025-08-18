using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface ILikeService
    {
        Task<List<Like>> GetLikesByRecipeIdAsync(int recipeId);
        Task RemoveLikeAsync(Like like);
        Task CreateLikeAsync(int recipesId,string userMail);


    }
}
