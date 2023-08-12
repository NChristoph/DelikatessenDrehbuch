using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DelikatessenDrehbuch.Models
{
    public class Recipes
    {
        public int Id { get; set; }
        public string? OwnerEmail { get; set; }
        public string Name { get; set; }
        public string Preparation { get; set; }
        public string? ImagePath { get; set; }


    }
}
