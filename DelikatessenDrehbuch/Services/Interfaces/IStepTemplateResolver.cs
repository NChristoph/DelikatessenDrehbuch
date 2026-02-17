namespace DelikatessenDrehbuch.Services.Interfaces
{
    /// <summary>
    /// Service to resolve multilingual preparation step templates.
    /// Loads step_templates.json, ingredient_grammar.json and group_cooking_defaults.json
    /// and resolves step text for a given language and ingredient set.
    /// </summary>
    public interface IStepTemplateResolver
    {
        /// <summary>
        /// Get a preparation step in the specified language.
        /// </summary>
        /// <param name="stepId">ID of the step template (1-335)</param>
        /// <param name="lang">Language code: "de", "en", "pt", "es"</param>
        /// <returns>The localized step text, or German fallback if not found</returns>
        string GetStep(int stepId, string lang);

        /// <summary>
        /// Get all steps for a list of step IDs in the specified language.
        /// </summary>
        List<ResolvedStep> GetSteps(List<int> stepIds, string lang);

        /// <summary>
        /// Get the grammatical article form for an ingredient.
        /// </summary>
        /// <param name="ingredientId">Ingredient ID</param>
        /// <param name="lang">Language code</param>
        /// <param name="grammaticalCase">For DE: "nom", "akk", "dat". For PT/ES: "art"</param>
        /// <returns>Article + ingredient name (e.g., "den Zucker", "o Açúcar")</returns>
        string GetIngredientWithArticle(int ingredientId, string lang, string grammaticalCase = "nom");

        /// <summary>
        /// Get the default cut style for an ingredient based on its group.
        /// </summary>
        /// <param name="ingredientId">Ingredient ID</param>
        /// <param name="lang">Language code</param>
        /// <returns>Cut style text (e.g., "kleine Würfel", "small cubes")</returns>
        string GetDefaultCutStyle(int ingredientId, string lang);

        /// <summary>
        /// Get the ingredient name in the specified language.
        /// </summary>
        string GetIngredientName(int ingredientId, string lang);

        /// <summary>
        /// Get all available step IDs for a given action.
        /// </summary>
        List<int> GetStepIdsByAction(string action);

        /// <summary>
        /// Get the group cooking defaults for a group.
        /// </summary>
        GroupCookingDefaults GetGroupDefaults(int groupId, string lang);
    }

    public class ResolvedStep
    {
        public int Id { get; set; }
        public string Action { get; set; }
        public int Phase { get; set; }
        public string Text { get; set; }
        public List<int> LinkedIngredients { get; set; } = new();
        public List<string> Equipment { get; set; } = new();
    }

    public class GroupCookingDefaults
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public List<string> TypicalActionChain { get; set; } = new();
        public List<CutStyleOption> CutStyles { get; set; } = new();
        public string DefaultCut { get; set; }
        public List<PrepMethodOption> PrepMethods { get; set; } = new();
    }

    public class CutStyleOption
    {
        public string Id { get; set; }
        public string Text { get; set; }
    }

    public class PrepMethodOption
    {
        public string Id { get; set; }
        public string Text { get; set; }
    }
}
