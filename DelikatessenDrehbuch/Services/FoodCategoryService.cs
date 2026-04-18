using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Services
{
    public sealed class FoodCategoryService : IFoodCategoryService
    {
        private readonly ApplicationDbContext _context;

        public FoodCategoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<FoodCategory?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return _context.FoodCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public Task<FoodCategory?> GetByKeyAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            var normalizedKey = NormalizeCategoryKey(categoryKey);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return Task.FromResult<FoodCategory?>(null);
            }

            return _context.FoodCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CategoryKey == normalizedKey, cancellationToken);
        }

        public Task<List<FoodCategory>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return _context.FoodCategories
                .AsNoTracking()
                .OrderBy(x => x.Name_DE)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<IngredientsAndNutrients>> GetIngredientsByCategoryKeyAsync(string categoryKey, CancellationToken cancellationToken = default)
        {
            var normalizedKey = NormalizeCategoryKey(categoryKey);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return new List<IngredientsAndNutrients>();
            }

            return await _context.IngredientsAndNutrients
                .AsNoTracking()
                .Include(x => x.Group)
                .Include(x => x.FoodCategory)
                .Where(x => x.FoodCategory != null && x.FoodCategory.CategoryKey == normalizedKey)
                .OrderBy(x => x.Name_DE)
                .ToListAsync(cancellationToken);
        }

        public string GetLocalizedName(FoodCategory? category, string? language)
        {
            if (category == null)
            {
                return string.Empty;
            }

            var normalizedLanguage = (language ?? "de").Trim().ToLowerInvariant();
            return normalizedLanguage switch
            {
                "en" => category.Name_EN ?? category.Name_DE ?? category.CategoryKey,
                "pt" => category.Name_PRT ?? category.Name_DE ?? category.CategoryKey,
                "es" => category.Name_ESP ?? category.Name_DE ?? category.CategoryKey,
                "id" => category.Name_ID ?? category.Name_DE ?? category.CategoryKey,
                "nl" => category.Name_NL ?? category.Name_DE ?? category.CategoryKey,
                "se" => category.Name_SE ?? category.Name_DE ?? category.CategoryKey,
                "dk" => category.Name_DK ?? category.Name_DE ?? category.CategoryKey,
                "no" => category.Name_NO ?? category.Name_DE ?? category.CategoryKey,
                "ms" => category.Name_MS ?? category.Name_DE ?? category.CategoryKey,
                _ => category.Name_DE ?? category.CategoryKey
            };
        }

        private static string NormalizeCategoryKey(string? categoryKey)
        {
            return (categoryKey ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
