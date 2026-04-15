namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class MissingIngredientLookupRequest
    {
        public string IngredientName { get; set; } = string.Empty;
        public string UserHash { get; set; } = string.Empty;
    }

    public sealed class MissingIngredientSaveRequest
    {
        public string UserHash { get; set; } = string.Empty;
        public MissingIngredientAiProposal? Suggestion { get; set; }
    }

    public sealed class MissingIngredientLookupResponse
    {
        public bool Success { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<MissingIngredientExistingMatchDto> Matches { get; set; } = new();
        public MissingIngredientAiProposal? Suggestion { get; set; }
    }

    public sealed class MissingIngredientSaveResponse
    {
        public bool Success { get; set; }
        public bool ReusedExisting { get; set; }
        public string Message { get; set; } = string.Empty;
        public IngredientCatalogItemDto? Ingredient { get; set; }
    }

    public sealed class MissingIngredientExistingMatchDto
    {
        public int Id { get; set; }
        public string NameDe { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int? GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public bool ExactMatch { get; set; }
    }

    public sealed class IngredientCatalogItemDto
    {
        public int Id { get; set; }
        public string Icon { get; set; } = string.Empty;
        public int? GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string GroupIcon { get; set; } = string.Empty;

        public string Name_DE { get; set; } = string.Empty;
        public string Name_EN { get; set; } = string.Empty;
        public string Name_PRT { get; set; } = string.Empty;
        public string Name_ESP { get; set; } = string.Empty;
        public string Name_ID { get; set; } = string.Empty;
        public string Name_NL { get; set; } = string.Empty;
        public string Name_SE { get; set; } = string.Empty;
        public string Name_DK { get; set; } = string.Empty;
        public string Name_NO { get; set; } = string.Empty;
        public string Name_MS { get; set; } = string.Empty;

        public string Genus_DE { get; set; } = string.Empty;
        public string Genus_EN { get; set; } = string.Empty;
        public string Genus_ESP { get; set; } = string.Empty;
        public string Genus_PRT { get; set; } = string.Empty;
        public string Genus_ID { get; set; } = string.Empty;
        public string Genus_MS { get; set; } = string.Empty;
        public string Genus_NL { get; set; } = string.Empty;
        public string Genus_SE { get; set; } = string.Empty;
        public string Genus_DK { get; set; } = string.Empty;
        public string Genus_NO { get; set; } = string.Empty;

        public bool is_liquid { get; set; }
        public bool is_hard { get; set; }
        public bool is_soft { get; set; }
        public bool is_fat { get; set; }
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

    public sealed class MissingIngredientAiSuggestionResult
    {
        public string Model { get; set; } = string.Empty;
        public string RawJson { get; set; } = string.Empty;
        public MissingIngredientAiProposal Proposal { get; set; } = new();
    }

    public sealed class MissingIngredientAiProposal
    {
        public bool IsDebugFallback { get; set; }
        public string OriginalQuery { get; set; } = string.Empty;
        public string CanonicalName { get; set; } = string.Empty;
        public string ConfirmationPrompt { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public decimal Confidence { get; set; }

        public string Icon { get; set; } = string.Empty;
        public int? GroupId { get; set; }

        public string Name_DE { get; set; } = string.Empty;
        public string Name_EN { get; set; } = string.Empty;
        public string Name_PRT { get; set; } = string.Empty;
        public string Name_ESP { get; set; } = string.Empty;
        public string Name_ID { get; set; } = string.Empty;
        public string Name_NL { get; set; } = string.Empty;
        public string Name_SE { get; set; } = string.Empty;
        public string Name_DK { get; set; } = string.Empty;
        public string Name_NO { get; set; } = string.Empty;
        public string Name_MS { get; set; } = string.Empty;

        public string Genus_DE { get; set; } = string.Empty;
        public string Genus_EN { get; set; } = string.Empty;
        public string Genus_ESP { get; set; } = string.Empty;
        public string Genus_PRT { get; set; } = string.Empty;
        public string Genus_ID { get; set; } = string.Empty;
        public string Genus_MS { get; set; } = string.Empty;
        public string Genus_NL { get; set; } = string.Empty;
        public string Genus_SE { get; set; } = string.Empty;
        public string Genus_DK { get; set; } = string.Empty;
        public string Genus_NO { get; set; } = string.Empty;

        public int Calories_a_100g { get; set; }
        public int Weight_per_piece { get; set; }
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
