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
        public string? Icon { get; set; }

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
        public int? FoodCategoryId { get; set; }

        [NotMapped]
        public string Groupe { get; set; }

        public Group? Group { get; set; }
        public FoodCategory? FoodCategory { get; set; }

        public decimal Fat_a_100g { get; set; }
        public decimal Saturated_fat_a_100g { get; set; }
        public decimal Carbohydrates_a_100g { get; set; }
        public decimal Sugar_a_100g { get; set; }
        public decimal Salt_a_100g { get; set; }
        public decimal Protein_a_100g { get; set; }
        public decimal Fiber_a_100g { get; set; }

        public bool is_liquid { get; set; }
        public bool is_hard { get; set; }
        public bool is_soft { get; set; }
        public bool is_fat { get; set; }

        // Zubereitungs-Tags für Step-Filterung
        public bool is_peelable { get; set; }
        public bool is_cuttable { get; set; }
        public bool is_grateable { get; set; }
        public bool is_fryable { get; set; }
        public bool is_roastable { get; set; }
        public bool is_grillable { get; set; }
        public bool is_steamable { get; set; }
        public bool is_boilable { get; set; }
        public bool is_searable { get; set; }
        public bool is_poachable { get; set; }
        public bool is_smokable { get; set; }
        public bool is_flambeable { get; set; }
        public bool is_blendable { get; set; }
        public bool is_powder { get; set; }
    }
}
