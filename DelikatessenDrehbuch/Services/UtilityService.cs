using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class UtilityService : IUtilityService
    {
        public List<string> GetListFromQueryString(string query)
        {
            return query.ToLower().Split(",").ToList();
        }
        public float? GetCaloriesByIngredientHandlers(List<IngredientHandlerModel> ingredientHandlerModels)
        {
            float? result = 0f;
            foreach (var ingredient in ingredientHandlerModels)
            {
                if (ingredient.Measure.UnitOfMeasurement != "g." && ingredient.Measure.UnitOfMeasurement != "ml")
                {
                    if (ingredient.Ingredient.AverageWeight != null && ingredient.Ingredient.Calories != null)
                        result += (ingredient.Ingredient.Calories / 100) * ingredient.Ingredient.AverageWeight;
                }
                else
                {
                    if (ingredient.Ingredient.Calories != null && ingredient.Measure.UnitOfMeasurement != "Bund")
                        result += (ingredient.Ingredient.Calories / 100) * (float)ingredient.Quantity.Quantitys;
                }


            }
            var fixedResult = (float?)System.Math.Round((decimal)result, 0);
            return fixedResult;
        }

        public string[] SplitLinesToArray(string convertToArray)
        {
            var lines = convertToArray.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                                 .Select(line => line.Trim())
                                 .ToArray();

            return lines;
        }

        public string[] SplitToArrayBySeperator(string convertToArray, char separator)
        {
            var array = convertToArray.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);


            return array;
        }
    }
}
