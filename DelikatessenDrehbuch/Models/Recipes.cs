using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace DelikatessenDrehbuch.Models
{
    public class Recipes
    {
        public int Id { get; set; }
        public string? Category { get;set; }
        public string? OwnerEmail { get; set; }
        public string Name { get; set; }
        public string Preparation { get; set; }
        public string? ImagePath { get; set; }
        public int? LikeCount { get; set; }
        [NotMapped]
        public IFormFile? FormFile { get; set; }

        public bool Vegan { get; set; }
        public bool Vegetarian { get; set; }
        public bool LowCap { get; set; }
        public bool Bake { get; set; }
        public bool BBQ { get; set; }

    }
}
