# PowerShell script to replace T() calls with SharedLocalizer in Feed/Index.cshtml

$filePath = "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Areas\WorldMiniApp\Views\Feed\Index.cshtml"

# Read file content
$content = Get-Content $filePath -Raw -Encoding UTF8

# Define replacements (German text -> Resource key)
$replacements = @{
    'T\("Zum Aktualisieren ziehen", "Pull to refresh", "Desliza para actualizar", "Puxe para atualizar"\)' = '@SharedLocalizer["Feed.PullToRefresh"]'
    'T\("Loslassen zum Aktualisieren", "Release to refresh", "Suelta para actualizar", "Solte para atualizar"\)' = '@SharedLocalizer["Feed.ReleaseToRefresh"]'
    'T\("Aktualisiere Feed\.\.\.", "Refreshing feed\.\.\.", "Actualizando feed\.\.\.", "Atualizando feed\.\.\."\)' = '@SharedLocalizer["Feed.RefreshingFeed"]'
    'T\("Rezepte von", "Recipes by", "Recetas de", "Receitas de"\)' = '@SharedLocalizer["Feed.RecipesBy"]'
    'T\("Keine Rezepte gefunden", "No recipes found", "No se encontraron recetas", "Nenhuma receita encontrada"\)' = '@SharedLocalizer["Feed.NoRecipes"]'
    'T\("Mehr", "More", "Más", "Mais"\)' = '@SharedLocalizer["Feed.More"]'
    'T\("Weniger", "Less", "Menos", "Menos"\)' = '@SharedLocalizer["Feed.Less"]'
    'T\("Teilen", "Share", "Compartir", "Partilhar"\)' = '@SharedLocalizer["Feed.Share"]'
    'T\("Marktplatz", "Marketplace", "Mercado", "Mercado"\)' = '@SharedLocalizer["Feed.Marketplace"]'
    'T\("Swipe nach links: Angebote vom aktuellen Creator", "Swipe left: listings from current creator", "Desliza a la izquierda: ofertas del creador actual", "Deslize para a esquerda: ofertas do criador atual"\)' = '@SharedLocalizer["Feed.MarketplaceSwipeLeft"]'
    'T\("Noch keine Angebote\.", "No listings yet\.", "Aún no hay ofertas\.", "Ainda sem ofertas\."\)' = '@SharedLocalizer["Feed.NoListingsYet"]'
    'T\("Dieser Creator hat aktuell keine Angebote\.", "This creator currently has no listings\.", "Este creador no tiene ofertas actualmente\.", "Este criador não tem ofertas no momento\."\)' = '@SharedLocalizer["Feed.CreatorNoListings"]'
    'T\("Kommentare", "Comments", "Comentarios", "Comentários"\)' = '@SharedLocalizer["Feed.Comments"]'
    'T\("Neueste", "Newest", "Recientes", "Recentes"\)' = '@SharedLocalizer["Feed.CommentsNewest"]'
    'T\("Noch keine Kommentare\. Sei der Erste!", "No comments yet\. Be the first!", "Todavia no hay comentarios\. Se el primero!", "Ainda nao ha comentarios\. Seja o primeiro!"\)' = '@SharedLocalizer["Feed.CommentsEmpty"]'
    'T\("Kommentar hinzufuegen\.\.\.", "Add a comment\.\.\.", "Agregar un comentario\.\.\.", "Adicionar um comentario\.\.\."\)' = '@SharedLocalizer["Feed.AddComment"]'
    'T\("Suche im Feed", "Search in feed", "Buscar en el feed", "Buscar no feed"\)' = '@SharedLocalizer["Feed.SearchTitle"]'
    'T\("Suchbegriff", "Search term", "Término de búsqueda", "Termo de pesquisa"\)' = '@SharedLocalizer["Feed.SearchTerm"]'
    'T\("Name oder Keyword", "Name or keyword", "Nombre o palabra clave", "Nome ou palavra-chave"\)' = '@SharedLocalizer["Feed.SearchPlaceholder"]'
    'T\("Kategorie", "Category", "Categoría", "Categoria"\)' = '@SharedLocalizer["Feed.Category"]'
    'T\("Alle Kategorien", "All categories", "Todas las categorías", "Todas as categorias"\)' = '@SharedLocalizer["Feed.AllCategories"]'
    'T\("Vorspeise", "Appetizer", "Entrada", "Entrada"\)' = '@SharedLocalizer["Meal.Starter"]'
    'T\("Hauptspeise", "Main course", "Plato principal", "Prato principal"\)' = '@SharedLocalizer["Meal.Main"]'
    'T\("Dessert", "Dessert", "Postre", "Sobremesa"\)' = '@SharedLocalizer["Meal.Dessert"]'
    'T\("Suchen", "Search", "Buscar", "Buscar"\)' = '@SharedLocalizer["Feed.Search"]'
    'T\("jetzt", "now", "ahora", "agora"\)' = '@SharedLocalizer["Feed.TimeNow"]'
    'T\("Std\.", "h", "h", "h"\)' = '@SharedLocalizer["Feed.TimeHours"]'
    'T\("T\.", "d", "d", "d"\)' = '@SharedLocalizer["Feed.TimeDays"]'
}

# Apply replacements
foreach ($pattern in $replacements.Keys) {
    $replacement = $replacements[$pattern]
    $content = $content -replace $pattern, $replacement
}

# Write back to file
$content | Set-Content $filePath -Encoding UTF8 -NoNewline

Write-Host "Replacements completed successfully!" -ForegroundColor Green
Write-Host "Modified file: $filePath" -ForegroundColor Cyan
