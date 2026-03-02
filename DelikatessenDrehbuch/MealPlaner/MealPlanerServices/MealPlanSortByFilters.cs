using DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces;
using DelikatessenDrehbuch.StaticScripts;

public class MealPlanSortByFilters : IMealPlanSortByFilters
{
    private readonly Random _random = new();

    // balanceddiet: 1x Fisch, 3x vegetarisch, 3x Fleisch (bei 7 Tagen)
    // preferablymeat: 5x Fleisch, 2x vegetarisch
    // preferablyvegetarian: 5x vegetarisch, 2x Fleisch vorspeise
    public List<int> SortRecipeIdsByFilters(string category, List<string> filterBools, int dayCount)
    {
        var filters = new HashSet<string>(filterBools.Select(f => f.ToLower()));

        bool prefersBalanced = filters.Contains("balanceddiet");
        bool prefersMeat = filters.Contains("preferablymeat");
        bool prefersVegetarian = filters.Contains("preferablyvegetarian");
        bool cookingTimeOne = filters.Contains("cookingtimeone");
        bool gentlyIngredients = filters.Contains("gentlyIngredients");

        bool hasPreference = prefersBalanced || prefersMeat || prefersVegetarian;

        // „Only“-Filter: wenn vegan/vegetarisch direkt gewählt werden,
        // wird die Präferenzlogik ignoriert
        bool wantsOnlyVeganOrVegetarian = filters.Contains("vegan") || filters.Contains("vegetarisch");
        if (wantsOnlyVeganOrVegetarian)
        {
            hasPreference = false;
        }

        // 1) Basis: alle Rezepte oder nur <=45 Minuten
        IEnumerable<int> baseIds = cookingTimeOne
            ? StaticData.CookingTimeOne
            : StaticData.RecipeAndIngredients.Keys;

        // 2) Kategorie einschränken (Hauptspeise, Vorspeise, Dessert)
        switch (category.ToLower())
        {
            case "hauptspeise":
                baseIds = baseIds.Intersect(StaticData.MainMeals);
                break;
            case "vorspeise":
                baseIds = baseIds.Intersect(StaticData.Appetizers);
                break;
            case "dessert":
                baseIds = baseIds.Intersect(StaticData.Desserts);
                break;
            default:
                // falls Kategorie leer oder unbekannt → nichts weiter tun
                break;
        }

        IEnumerable<int> recipeIds;

        // 3) Präferenz-Verteilung ODER normale Filter
        if (hasPreference && dayCount > 0)
        {
            recipeIds = BuildIdsByPreference(
                baseIds.ToList(),
                filters,
                prefersBalanced,
                prefersMeat,
                prefersVegetarian,
                dayCount,
                gentlyIngredients);
        }
        else
        {
            recipeIds = ApplyNormalIncludeExcludeFilters(baseIds, filters);
        }

        // 4) Eindeutig
        var finalIds = recipeIds
            .Distinct()
            .ToList();

        // 5) Auf dayCount begrenzen – bei gentlyingredients „smart“,
        // sonst wie bisher zufällig

        if (dayCount > 0 && finalIds.Count > dayCount)
        {
            if (gentlyIngredients)
            {
                finalIds = PickSmartByIngredients(finalIds, dayCount);
            }
            else
            {
                finalIds = finalIds
                    .OrderBy(_ => _random.Next())
                    .Take(dayCount)
                    .ToList();
            }
        }


        return finalIds;
    }

    /// <summary>
    /// Erzeugt eine Liste von Rezept-IDs anhand der Präferenzverteilung.
    /// Arbeitet NUR innerhalb der bereits vorgefilterten baseIds.
    /// Bei gentlyIngredients werden Rezepte mit ähnlichen Zutaten bevorzugt.
    /// </summary>
    private IEnumerable<int> BuildIdsByPreference(
        List<int> baseIds,
        HashSet<string> filters,
        bool prefersBalanced,
        bool prefersMeat,
        bool prefersVegetarian,
        int dayCount,
        bool gentlyIngredients)
    {
        // Basislisten innerhalb der bereits gefilterten IDs
        var fishIds = StaticData.FishRecipeIds
            .Intersect(baseIds)
            .ToList();

        var vegIds = StaticData.VegetarianRecipeIds
            .Concat(StaticData.VeganRecipeIds)
            .Distinct()
            .Intersect(baseIds)
            .ToList();

        var meatIds = StaticData.MeatRecipeIds
            .Intersect(baseIds)
            .ToList();

        // --- "no"-Filter berücksichtigen ---

        // kein Fisch
        if (filters.Contains("nofisch"))
        {
            fishIds = new List<int>();
        }

        // keine veganen Rezepte → nur vegetarische zulassen
        if (filters.Contains("novegan"))
        {
            vegIds = StaticData.VegetarianRecipeIds
                .Intersect(baseIds)
                .ToList();
        }

        // keine vegetarischen Rezepte:
        // sinnvoll hauptsächlich bei „preferablymeat“ → nur Fleisch
        if (filters.Contains("novegetarisch"))
        {
            if (prefersMeat)
            {
                vegIds = new List<int>(); // nur Fleisch
            }
        }

        // kein Schweinefleisch
        if (filters.Contains("noschweinefleisch") && StaticData.PorkRecipeIds != null)
        {
            meatIds = meatIds
                .Except(StaticData.PorkRecipeIds)
                .ToList();
        }

        // --- Verteilung bestimmen (Fish/Veg/Meat Counts) ---
        var (fishCount, vegCount, meatCount) = GetPreferenceDistribution(
            prefersBalanced, prefersMeat, prefersVegetarian, dayCount);

        var result = new List<int>();
        var chosenIngredients = new HashSet<int>();

        // Hilfsfunktion für einen "Bucket" (Fisch/Veg/Meat)
        List<int> PickFromBucket(List<int> source, int count)
        {
            if (count <= 0 || source.Count == 0)
                return new List<int>();

            if (!gentlyIngredients)
            {
                return PickRandom(source, count);
            }

            // „intelligent“: Rezepte mit möglichst vielen gemeinsamen Zutaten bevorzugen
            return PickSmartFromSource(source, count, chosenIngredients);
        }

        var pickedFish = PickFromBucket(fishIds, fishCount);
        result.AddRange(pickedFish);
        AddIngredientsToSet(pickedFish, chosenIngredients);

        var pickedVeg = PickFromBucket(vegIds, vegCount);
        result.AddRange(pickedVeg);
        AddIngredientsToSet(pickedVeg, chosenIngredients);

        var pickedMeat = PickFromBucket(meatIds, meatCount);
        result.AddRange(pickedMeat);
        AddIngredientsToSet(pickedMeat, chosenIngredients);

        return result;
    }

    /// <summary>
    /// Berechnet, wie viele Fisch / Veggie / Fleisch-Gerichte
    /// für die gegebene Tageszahl gewählt werden sollen.
    /// </summary>
    private (int fish, int veg, int meat) GetPreferenceDistribution(
        bool prefersBalanced,
        bool prefersMeat,
        bool prefersVegetarian,
        int dayCount)
    {
        // Basis für 7 Tage
        int baseFish = 0, baseVeg = 0, baseMeat = 0;

        if (prefersBalanced)
        {
            baseFish = 1;
            baseVeg = 3;
            baseMeat = 3;
        }
        else if (prefersMeat)
        {
            baseFish = 0;
            baseVeg = 2;
            baseMeat = 5;
        }
        else if (prefersVegetarian)
        {
            baseFish = 0;
            baseVeg = 5;
            baseMeat = 2;
        }

        var factor = dayCount / 7.0;

        int fish = (int)Math.Round(baseFish * factor);
        int veg = (int)Math.Round(baseVeg * factor);
        int meat = (int)Math.Round(baseMeat * factor);

        int sum = fish + veg + meat;

        if (sum < dayCount)
        {
            // fehlende Tage geben wir bevorzugt Fleisch
            meat += dayCount - sum;
        }
        else if (sum > dayCount)
        {
            int diff = sum - dayCount;
            meat = Math.Max(0, meat - diff);
        }

        return (fish, veg, meat);
    }

    /// <summary>
    /// Wählt zufällig count Elemente aus der Liste aus (ohne Duplikate).
    /// </summary>
    private List<int> PickRandom(List<int> source, int count)
    {
        if (count <= 0 || source.Count == 0)
            return new List<int>();

        return source
            .OrderBy(_ => _random.Next())
            .Take(Math.Min(count, source.Count))
            .ToList();
    }

    /// <summary>
    /// „Intelligente“ Auswahl: bevorzugt Rezepte mit ähnlichen Zutaten.
    /// Wird für Präferenz-Buckets genutzt.
    /// </summary>
    private List<int> PickSmartFromSource(
        List<int> source,
        int count,
        HashSet<int> chosenIngredients)
    {
        if (count <= 0 || source.Count == 0)
            return new List<int>();

        // Wenn noch keine Zutaten gewählt sind → einfach zufällig starten
        if (chosenIngredients.Count == 0)
        {
            var first = PickRandom(source, count: 1);
            var resultInit = new List<int>(first);
            AddIngredientsToSet(first, chosenIngredients);

            if (count == 1)
                return resultInit;

            // Rest rekursiv bzw. iterativ holen
            var remaining = source.Except(first).ToList();
            var rest = PickSmartFromSource(remaining, count - 1, chosenIngredients);
            resultInit.AddRange(rest);
            return resultInit;
        }

        // Sonst: alle Kandidaten nach Ähnlichkeit sortieren
        var weighted = source
            .Select(id => new
            {
                Id = id,
                Score = IngredientSimilarityScore(id, chosenIngredients)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(_ => _random.Next()) // Zufall als Tiebreaker
            .Take(count)
            .Select(x => x.Id)
            .ToList();

        return weighted;
    }

    /// <summary>
    /// Wählt eine „zutaten­schonende“ Menge aus einer fertigen ID-Liste
    /// (für den Fall ohne Präferenz, aber mit gentlyingredients).
    /// </summary>
    private List<int> PickSmartByIngredients(List<int> source, int count)
    {
        if (count <= 0 || source.Count == 0)
            return new List<int>();

        var result = new List<int>();
        var chosenIngredients = new HashSet<int>();

        // Start: zufällig ein erstes Rezept nehmen
        var first = PickRandom(source, 1);
        result.AddRange(first);
        AddIngredientsToSet(first, chosenIngredients);

        var remaining = source.Except(first).ToList();

        while (result.Count < count && remaining.Count > 0)
        {
            var next = remaining
                .Select(id => new
                {
                    Id = id,
                    Score = IngredientSimilarityScore(id, chosenIngredients)
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(_ => _random.Next())
                .First().Id;

            result.Add(next);
            AddIngredientsToSet(new[] { next }, chosenIngredients);
            remaining = remaining.Where(r => r != next).ToList();
        }

        return result;
    }

    /// <summary>
    /// Normale Filterlogik ohne Präferenzen.
    /// Arbeitet innerhalb der bereits gefilterten baseIds.
    /// </summary>
    private IEnumerable<int> ApplyNormalIncludeExcludeFilters(
        IEnumerable<int> baseIds,
        HashSet<string> filters)
    {
        IEnumerable<int> recipeIds = baseIds;

        bool wantsVegan = filters.Contains("vegan");
        bool wantsVegetarian = filters.Contains("vegetarisch");

        // INKLUSIV: nur vegan/vegetarisch
        if (wantsVegan || wantsVegetarian)
        {
            var includeIds = new List<int>();

            if (wantsVegan)
            {
                includeIds.AddRange(
                    StaticData.VeganRecipeIds.Intersect(baseIds));
            }

            if (wantsVegetarian)
            {
                includeIds.AddRange(
                    StaticData.VegetarianRecipeIds.Intersect(baseIds));
            }

            recipeIds = includeIds;
        }

        // EXKLUSIV-FILTER
        if (filters.Contains("novegan"))
        {
            recipeIds = recipeIds.Except(StaticData.VeganRecipeIds).Intersect(baseIds);
        }

        if (filters.Contains("novegetarisch"))
        {
            recipeIds = recipeIds.Except(StaticData.VegetarianRecipeIds).Intersect(baseIds);
        }

        if (filters.Contains("nofisch"))
        {
            recipeIds = recipeIds.Except(StaticData.FishRecipeIds).Intersect(baseIds);
        }

        if (filters.Contains("noschweinefleisch") && StaticData.PorkRecipeIds != null)
        {
            recipeIds = recipeIds.Except(StaticData.PorkRecipeIds).Intersect(baseIds);
        }

        return recipeIds.Distinct().ToList();
    }

    // ------- Hilfsfunktionen für Zutaten-Ähnlichkeit -------

    private int IngredientSimilarityScore(int recipeId, HashSet<int> chosenIngredients)
    {
        if (!StaticData.RecipeAndIngredients.TryGetValue(recipeId, out var recipeIngs))
            return 0;

        return recipeIngs.Count(i => chosenIngredients.Contains(i));
    }

    private void AddIngredientsToSet(IEnumerable<int> recipeIds, HashSet<int> targetSet)
    {
        foreach (var id in recipeIds)
        {
            if (!StaticData.RecipeAndIngredients.TryGetValue(id, out var ings))
                continue;

            foreach (var ing in ings)
                targetSet.Add(ing);
        }
    }
}
