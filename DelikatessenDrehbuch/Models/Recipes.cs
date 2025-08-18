using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class Recipes
    {
        public int Id { get; set; }
        public string? Category { get; set; }
        public string? OwnerEmail { get; set; }
        public string Name { get; set; }
        public string Preparation { get; set; }
        public string? ImagePath { get; set; }
        public int? PreparationTime { get; set; }
        public string? Description { get; set; }

        public string? Calories { get; set; }
        public int? LikeCount { get; set; }
        public int? RecipePersonCount { get; set; }
        [NotMapped]
        public IFormFile? FormFile { get; set; }


    }
}
