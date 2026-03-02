namespace DelikatessenDrehbuch.Models
{
    public class PersonalMealPlanSettings
    {
        public int DayCount { get; set; }
        public int PersonCount { get; set; }
        public bool Vegan { get; set; }
        public bool NoVegan { get; set; }
        public bool Vegetarisch { get; set; }
        public bool NoVegetarisch { get; set; }
        public bool NoSchweinefleisch{ get; set; }
        public bool NoFisch{ get; set; }
        public bool CookingTimeOne { get; set; }
        public bool CookingTimeTwo { get; set; }
        public bool BalancedDiet { get; set; }
        public bool PreferablyMeat { get; set; }
        public bool PreferablyVegetarian { get; set; }
        public bool GentlyIngredients { get; set; }

        public List<int> IngredientIds { get; set; } = new();
        public List<int> IngredientsAtHome { get; set; } = new();

      

    }
}
