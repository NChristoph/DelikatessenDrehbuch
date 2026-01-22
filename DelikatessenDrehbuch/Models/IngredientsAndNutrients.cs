using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class IngredientsAndNutrients
    {
        public int Id { get; set; }
        public string Name_DE { get; set; }
        public string Name_EN { get; set; }
        public string Name_PRT { get; set; }
        public string Name_ESP { get; set; }
        public int Calories_a_100g { get; set; }
        public int Weight_per_piece { get; set; }
        [NotMapped]
        public string Groupe { get; set; }
        public Group? Group { get; set; }
        public decimal Fat_a_100g { get; set; }
        public decimal Saturated_fat_a_100g { get; set; }
        public decimal Carbohydrates_a_100g { get; set; }
        public decimal Sugar_a_100g { get; set; }
        public decimal Salt_a_100g { get; set; }
        public decimal Protein_a_100g { get; set; }
        public decimal Fiber_a_100g { get; set; }


    }
}
