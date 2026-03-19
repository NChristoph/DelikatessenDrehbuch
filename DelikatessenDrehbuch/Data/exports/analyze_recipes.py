"""
Recipe Analysis Script
Run: python analyze_recipes.py
Outputs: recipe_analysis_results.json
"""
import json
import re
from collections import Counter, defaultdict

FILE = "recipe_schema_export_20260216_205825.json"
OUT  = "recipe_analysis_results.json"

with open(FILE, "r", encoding="utf-8") as f:
    data = json.load(f)

recipes = data["recipes"]
print(f"Total recipes: {len(recipes)}")

# Global ingredient id -> name map
ing_names = {}
for r in recipes:
    for ing in r.get("Ingredients", []):
        ing_names[ing["IngredientId"]] = ing["IngredientName"]

# ===== 1. CATEGORIES =====
cat_counter = Counter()
for r in recipes:
    cat_counter[r.get("Category", "UNKNOWN")] += 1

# ===== 2. TOP INGREDIENTS PER CATEGORY =====
cat_ingredients = defaultdict(Counter)
for r in recipes:
    cat = r.get("Category", "UNKNOWN")
    seen = set()
    for ing in r.get("Ingredients", []):
        iid = ing["IngredientId"]
        if iid not in seen:
            cat_ingredients[cat][iid] += 1
            seen.add(iid)

# ===== 3 & 4. DISH-TYPE CLUSTERS =====
dish_patterns = {
    "lasagne":      r"lasagne",
    "spaghetti":    r"spaghetti",
    "pasta":        r"pasta|penne|tagliatelle|farfalle|fusilli|linguine|rigatoni|fettuccine|nudel|cannelloni|makkaroni|mac.and.cheese|carbonara",
    "gnocchi":      r"gnocchi",
    "braten":       r"braten|schmorbraten",
    "pfanne":       r"pfanne|pfannengericht",
    "risotto":      r"risotto",
    "curry":        r"curry",
    "burger":       r"burger",
    "pizza":        r"pizza|flammkuchen",
    "salat":        r"salat",
    "suppe":        r"suppe|eintopf",
    "auflauf":      r"auflauf|gratin",
    "schnitzel":    r"schnitzel",
    "wrap":         r"wrap|burrito|taco(?!s)|tacos|quesadilla|fajita|enchilada|tortilla",
    "bowl":         r"bowl|poke|bibimbap",
    "kuchen":       r"kuchen|torte|muffin|brownie|cookie|gugelhupf|cheesecake|strudel",
    "one_pot":      r"one.pot|one.pan",
    "fisch":        r"lachs|forelle|thunfisch|garnele|shrimp|fisch|zander|kabeljau|muschel|vongole|jakobsmuschel|sardine|smelt|oktopus",
    "huhn":         r"h[uü]hn|chicken|hendl|h[aä]hnchen|poulet|gefl[uü]gel",
    "rind":         r"rind|beef|gulasch|boeuf|sauerbraten|ochsen",
    "schwein":      r"schwein|pork|schweins",
    "wok":          r"wok|stir.?fry",
    "reis":         r"\breis\b",
    "kartoffel":    r"kartoffel|erd[aä]pfel|pommes|wedges|bratkartoffel|kroketten|r[oö]sti",
    "quiche":       r"quiche|tarte(?!tte)",
    "brot":         r"\bbrot\b|focaccia|ciabatta|br[oö]tchen|naan",
    "ofen":         r"ofen|backofen|gebacken|\u00fcberback",
}

dish_clusters = defaultdict(list)
for r in recipes:
    name_lower = r["Name"].lower()
    for dish, pattern in dish_patterns.items():
        if re.search(pattern, name_lower):
            dish_clusters[dish].append(r)

# Compute top 20 ingredients per cluster
cluster_top_ingredients = {}
for dish, recs in dish_clusters.items():
    ing_counter = Counter()
    seen_per_recipe = defaultdict(set)
    for r in recs:
        seen = set()
        for ing in r.get("Ingredients", []):
            iid = ing["IngredientId"]
            if iid not in seen:
                ing_counter[iid] += 1
                seen.add(iid)
    top20 = []
    for iid, cnt in ing_counter.most_common(20):
        pct = round(100 * cnt / len(recs))
        top20.append({
            "ingredientId": iid,
            "ingredientName": ing_names.get(iid, "UNKNOWN"),
            "count": cnt,
            "pctOfCluster": pct
        })
    cluster_top_ingredients[dish] = {
        "recipeCount": len(recs),
        "topIngredients": top20
    }

# Compute top 20 ingredients per category
cat_top_ingredients = {}
for cat in cat_ingredients:
    top20 = []
    total = cat_counter[cat]
    for iid, cnt in cat_ingredients[cat].most_common(20):
        top20.append({
            "ingredientId": iid,
            "ingredientName": ing_names.get(iid, "UNKNOWN"),
            "count": cnt,
            "pctOfCategory": round(100 * cnt / total)
        })
    cat_top_ingredients[cat] = {
        "recipeCount": total,
        "topIngredients": top20
    }

# Recipe names per dish cluster
cluster_recipe_names = {}
for dish, recs in sorted(dish_clusters.items(), key=lambda x: -len(x[1])):
    cluster_recipe_names[dish] = [{"id": r["Id"], "name": r["Name"]} for r in recs]

# Unmatched recipes
matched_ids = set()
for recs in dish_clusters.values():
    for r in recs:
        matched_ids.add(r["Id"])
unmatched = [{"id": r["Id"], "name": r["Name"], "category": r["Category"]}
             for r in recipes if r["Id"] not in matched_ids]

# ===== OUTPUT =====
result = {
    "summary": {
        "totalRecipes": len(recipes),
        "totalUniqueIngredients": len(ing_names),
        "totalIngredientUsages": sum(len(r.get("Ingredients", [])) for r in recipes)
    },
    "categories": {cat: cnt for cat, cnt in cat_counter.most_common()},
    "topIngredientsPerCategory": cat_top_ingredients,
    "dishClusters": {dish: cluster_top_ingredients[dish]["recipeCount"]
                     for dish, _ in sorted(dish_clusters.items(), key=lambda x: -len(x[1]))},
    "topIngredientsPerDishCluster": cluster_top_ingredients,
    "clusterRecipeNames": cluster_recipe_names,
    "unmatchedRecipes": unmatched,
    "unmatchedCount": len(unmatched)
}

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)

print(f"\nResults written to {OUT}")
print(f"\n=== CATEGORIES ===")
for cat, cnt in cat_counter.most_common():
    print(f"  {cat}: {cnt}")

print(f"\n=== DISH CLUSTERS ===")
for dish, recs in sorted(dish_clusters.items(), key=lambda x: -len(x[1])):
    print(f"  {dish}: {len(recs)}")

print(f"\n=== TOP 20 INGREDIENTS PER CLUSTER ===")
for dish in sorted(cluster_top_ingredients.keys(), key=lambda d: -cluster_top_ingredients[d]["recipeCount"]):
    info = cluster_top_ingredients[dish]
    if info["recipeCount"] < 3:
        continue
    print(f"\n--- {dish} ({info['recipeCount']} recipes) ---")
    for item in info["topIngredients"]:
        print(f"  {item['ingredientId']:>4} | {item['ingredientName']:<35} | {item['count']:>3} ({item['pctOfCluster']}%)")

print(f"\nUnmatched recipes: {len(unmatched)}")
