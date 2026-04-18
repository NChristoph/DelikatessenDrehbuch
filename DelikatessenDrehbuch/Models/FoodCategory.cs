namespace DelikatessenDrehbuch.Models
{
    public sealed class FoodCategory
    {
        public int Id { get; set; }
        public string CategoryKey { get; set; } = string.Empty;

        public string? Name_DE { get; set; }
        public string? Name_EN { get; set; }
        public string? Name_PRT { get; set; }
        public string? Name_ESP { get; set; }
        public string? Name_ID { get; set; }
        public string? Name_NL { get; set; }
        public string? Name_SE { get; set; }
        public string? Name_DK { get; set; }
        public string? Name_NO { get; set; }
        public string? Name_MS { get; set; }

        public ICollection<IngredientsAndNutrients> Ingredients { get; set; } = new List<IngredientsAndNutrients>();
    }
}
