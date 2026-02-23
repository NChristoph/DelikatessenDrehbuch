using System.Collections.Generic;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class PlanCardViewModel
    {
        public int ListingId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleJsSafe { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public string CreatorHash { get; set; } = string.Empty;
        public string? SellerWalletAddress { get; set; }

        public int DayCount { get; set; }
        public int RecipeCount { get; set; }
        public string CreatedDateLabel { get; set; } = string.Empty;

        public decimal PriceWld { get; set; }
        public string PriceLabel => $"Freischalten - {PriceWld:0.##} WLD";

        public decimal? CaloriesKcal { get; set; }
        public decimal? ProteinGrams { get; set; }
        public bool IsLowCarb { get; set; }
        public bool IsDietFriendly { get; set; }

        public decimal Rating { get; set; }
        public int SoldCount { get; set; }
        public int ActivePlannerCount { get; set; }

        public string? HeroImageUrl { get; set; }
        public List<string> HeroImageUrls { get; set; } = new();
        public string HeroImageAlt { get; set; } = "Essensplan Vorschau";

        public decimal? FatGrams { get; set; }
        public decimal? CarbsGrams { get; set; }

        public string JourneyLabel => $"Teil der {CreatorName} Journey";
    }
}
