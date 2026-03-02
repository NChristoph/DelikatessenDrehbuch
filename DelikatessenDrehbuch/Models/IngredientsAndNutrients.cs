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
        public string Name_ID { get; set; }
        public string Name_NL { get; set; }
        public string Name_SE { get; set; }
        public string Name_DK { get; set; }
        public string Name_NO { get; set; }
        public string Name_MS { get; set; }

        public string Genus_DE { get; set; }
        public string Genus_EN { get; set; }
        public string Genus_ESP { get; set; }
        public string Genus_PRT { get; set; }
        public string Genus_ID { get; set; }
        public string Genus_MS { get; set; }
        public string Genus_NL { get; set; }
        public string Genus_SE { get; set; }
        public string Genus_DK { get; set; }
        public string Genus_NO { get; set; }

        public int Calories_a_100g { get; set; }
        public int Weight_per_piece { get; set; }
        public int? GroupId { get; set; }

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
