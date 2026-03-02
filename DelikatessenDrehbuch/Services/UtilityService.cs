using DelikatessenDrehbuch.Models;
using System.Security.Cryptography;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class UtilityService : IUtilityService
    {
        public int GetRandomIntFromList(List<int> list)
        {
            Random random = new Random();
            int index = random.Next(list.Count);
            return list[index];
        }

        public List<int> GetRandomIntsFromList(List<int> list, int count)
        {
            Random random = new Random();
            return list.OrderBy(x => random.Next()).Take(count).ToList();
        }
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

        public List<int> ConvertStringListToIntList(List<string> idsString)
        {
            var ids = idsString.Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                               .Where(n => n.HasValue)
                               .Select(n => n.Value)
                               .ToList();

            return ids;
        }

      

        public string GenerateRandomToken(int length)
        {
            var buf = new byte[length];
            RandomNumberGenerator.Fill(buf);
            return Convert.ToBase64String(buf).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public List<string> GetTrueBoolNamesFromModel(PersonalMealPlanSettings settings)
        {

            var bools = typeof(PersonalMealPlanSettings)
                       .GetProperties()
                       .Where(p => p.PropertyType == typeof(bool));

            return bools.Where(b => (bool)b.GetValue(settings) == true).Select(x => x.Name.ToLower()).ToList();
        }


    }
}
