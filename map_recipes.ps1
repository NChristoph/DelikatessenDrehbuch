# Load export with ingredients_and_nutrients
$json = Get-Content 'C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Data\exports\recipe_schema_export_20260217_194918.json' -Raw | ConvertFrom-Json

# Build ingredients_and_nutrients name->id map
$ian = $json.ingredients_and_nutrients
$nameToIanId = @{}
foreach ($item in $ian) {
    $nameToIanId[$item.Name.ToLower().Trim()] = $item.Id
}

# IMPROVED matching with two strategies:
# Strategy A: Recipe name CONTAINS IAN name → prefer LONGEST IAN name (most specific)
# Strategy B: IAN name CONTAINS recipe name → prefer SHORTEST IAN name (most specific)
# Strategy A wins over B (more reliable). Min 3 chars for substring matches.
function Find-IanId {
    param([string]$recipeName)
    $lower = $recipeName.ToLower().Trim()

    # Direct match (highest priority)
    if ($nameToIanId.ContainsKey($lower)) { return $nameToIanId[$lower] }

    # Strategy A: recipe name contains IAN name (e.g., "Knoblauchzehen" contains "knoblauchzehe")
    # Prefer the LONGEST matching IAN name
    $bestA_Key = $null
    $bestA_Len = 0
    foreach ($key in $nameToIanId.Keys) {
        if ($key.Length -ge 3 -and $lower.Contains($key)) {
            if ($key.Length -gt $bestA_Len) {
                $bestA_Len = $key.Length
                $bestA_Key = $key
            }
        }
    }
    if ($bestA_Key) { return $nameToIanId[$bestA_Key] }

    # Strategy B: IAN name contains recipe name (e.g., "olivenöl" contains "öl")
    # Prefer the SHORTEST matching IAN name (most specific)
    # Require recipe name to be at least 4 chars to avoid false positives
    if ($lower.Length -ge 4) {
        $bestB_Key = $null
        $bestB_Len = [int]::MaxValue
        foreach ($key in $nameToIanId.Keys) {
            if ($key.Contains($lower)) {
                if ($key.Length -lt $bestB_Len) {
                    $bestB_Len = $key.Length
                    $bestB_Key = $key
                }
            }
        }
        if ($bestB_Key) { return $nameToIanId[$bestB_Key] }
    }

    return $null
}

$recipes = $json.recipes

# Dish type clusters
$clusters = @{
    'lasagne' = 'lasagne|lasagna'
    'spaghetti' = 'spaghetti'
    'pasta_gericht' = 'pasta|nudel|penne|rigatoni|tagliatelle|fettuccine|linguine|farfalle|tortellini|gnocchi|mac.*cheese'
    'bolognese' = 'bolognese|rag[uu]'
    'braten' = 'braten|brathuhn|schweinebraten|rinderbraten'
    'pfannengericht' = 'pfanne|pan|gebraten'
    'salat' = 'salat|salad'
    'suppe' = 'suppe|soup'
    'eintopf' = 'eintopf|stew'
    'curry' = 'curry'
    'pizza' = 'pizza'
    'risotto' = 'risotto'
    'burger' = 'burger|hamburger'
    'chili' = 'chili|chilli'
    'bowl' = 'bowl|buddha|poke'
    'wrap_burrito' = 'wrap|burrito|tortilla|quesadilla'
    'wok_gericht' = 'wok|stir.fry|thai|pad|asia'
    'quiche' = 'quiche|tarte'
    'muffin' = 'muffin|cupcake'
    'cheesecake' = 'cheesecake|k.sekuchen'
    'eis_parfait' = '\beis\b|ice.cream|parfait|sorbet|gelato'
    'steak' = 'steak|filet|entrecote|ribeye'
    'fisch' = 'fisch|fish|lachs|salmon|thunfisch|tuna|garnele|shrimp|kabeljau|forelle|dorade'
    'taco' = 'taco'
    'auflauf' = 'auflauf|gratin|casserole'
    'schnitzel' = 'schnitzel|cordon.bleu'
    'vegetarischer_eintopf' = 'vegetarisch.*eintopf|gem.se.*eintopf|linsen.*eintopf'
    'hefeteig' = 'hefeteig|hefezopf|brioche|brot.*hefe|hefe.*brot'
}

foreach ($type in ($clusters.Keys | Sort-Object)) {
    $pattern = $clusters[$type]
    $matched = @($recipes | Where-Object { $_.Name -match $pattern })
    if ($matched.Count -gt 0) {
        Write-Host ""
        Write-Host "=== $type === ($($matched.Count) recipes)"

        # Map recipe ingredient names to ian IDs
        $ianCounts = @{}
        $ianNames = @{}
        foreach ($r in $matched) {
            foreach ($ing in $r.Ingredients) {
                $ingName = $ing.IngredientName
                $ianId = Find-IanId $ingName
                if ($ianId) {
                    $key = [string]$ianId
                    if ($ianCounts.ContainsKey($key)) { $ianCounts[$key]++ } else { $ianCounts[$key] = 1 }
                    if (-not $ianNames.ContainsKey($key)) { $ianNames[$key] = ($ian | Where-Object { $_.Id -eq $ianId }).Name }
                }
            }
        }

        $sorted = $ianCounts.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 20
        foreach ($t in $sorted) {
            $name = $ianNames[$t.Key]
            $pct = [math]::Round(($t.Value / $matched.Count) * 100)
            Write-Host "  IAN_ID:$($t.Key) Count:$($t.Value) ($pct%) $name"
        }
    }
}
