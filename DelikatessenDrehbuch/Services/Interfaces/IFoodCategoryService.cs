using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IFoodCategoryService
    {
        Task<FoodCategory?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<FoodCategory?> GetByKeyAsync(string categoryKey, CancellationToken cancellationToken = default);
        Task<List<FoodCategory>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<IngredientsAndNutrients>> GetIngredientsByCategoryKeyAsync(string categoryKey, CancellationToken cancellationToken = default);
        string GetLocalizedName(FoodCategory? category, string? language);
    }
}
