# HomeController Review Empfehlungen

Stand: 2026-06-13

## Ausgangslage

`Areas/WorldMiniApp/Controllers/HomeController.cs` ist aktuell zu breit geschnitten. Der Controller umfasst mehrere fachliche Bereiche gleichzeitig:

- Startseite und World-MiniApp-Deep-Links
- Creator-Dashboard
- Rezeptvorschau und Rezeptdetailseite
- AI-Rezeptvarianten-Endpunkte
- World-User-Notifications
- Rezept-zu-Feed-Publishing
- Client-Lokalisierungsstrings
- Rezeptspeicherung mit AI-Übersetzung
- Marketplace-Preview
- Hilfslogik für Nutrition, Medienpfade, Einheiten und Share-Links

Dadurch wird der Controller schwer zu testen, schwer zu ändern und riskant bei zukünftigen Refactorings. Ein Controller sollte HTTP-Anfragen entgegennehmen, validieren, Services aufrufen und passende Responses zurückgeben. Fachlogik, Query-Komposition, DTO-Aufbau und Berechnungen sollten in Services oder klar benannten ViewModels liegen.

## Zielbild

Der `HomeController` sollte nur noch die Home-/Entry-Point-Verantwortung tragen:

- `Index`
- MiniApp-Deep-Link-Normalisierung
- Post-Login-Redirect vorbereiten
- einfache View-Rückgabe

Alle anderen Verantwortlichkeiten sollten in eigene Controller und Services wandern.

## Empfohlener Controllerschnitt

| Bereich | Neuer Zielort | Warum |
| --- | --- | --- |
| Startseite / Deep-Link | `HomeController` | Das ist die eigentliche Home-Verantwortung. |
| Rezeptdetail / Share-Link | `RecipeViewController` oder vorhandener `RecipeController` | Rezeptanzeige ist ein eigener Feature-Slice. |
| Rezeptvorschau JSON | `RecipeViewController` | Gehört fachlich zur Rezeptanzeige, nicht zur Home-Seite. |
| Notifications | `WorldNotificationsController` | Eigenständige kleine API mit klarer CRUD-artiger Verantwortung. |
| Client-Lokalisierung | `WorldLocalizationController` | Ressourcen-Ausgabe ist Querschnitt, kein Home-Thema. |
| Creator-Dashboard | `CreatorDashboardController` | Dashboard hat eigene Queries, Analytics und ViewModel. |
| Publish Recipe to Feed | `RecipePublishController` oder `RecipeController` | Publishing ist Rezept-/Feed-Workflow, nicht Home. |
| AI-Rezept-Endpunkte | `RecipeAiController` | Die Endpunkte sind aktuell deaktiviert; isolieren erleichtert späteres Löschen oder Reaktivieren. |
| SaveRecipeWithTranslation | `RecipeController` oder `RecipeTranslationController` | Rezept-Erstellung mit Übersetzung ist Schreiblogik und sollte einen Service nutzen. |
| MarketplacePreview | `MarketplaceController` | Marketplace-Daten gehören in den Marketplace-Bereich. |

## Empfohlene Services

### `WorldMiniAppDeepLinkService`

Aufgabe:

- `returnTo`, `path` und Wildcard-`extraPath` normalisieren
- nur interne `/WorldMiniApp...`-Ziele erlauben
- QueryString sauber übernehmen
- offene Redirects verhindern

Warum:

- Security-relevante Redirect-Logik ist aktuell direkt im Controller.
- Eine eigene Klasse macht Tests für gültige/ungültige Deep-Links einfach.

### `CreatorDashboardService`

Aufgabe:

- Sales laden
- WLD-/USDC-/USDT-Summen berechnen
- Watch-Analytics und Ad-Preferences zusammensetzen
- `UserDashboardViewModel` erstellen

Warum:

- `UserDashboard` enthält Query- und Aggregationslogik.
- Das Dashboard kann später erweitert werden, ohne den Controller aufzublähen.

### `RecipePreviewService`

Aufgabe:

- Rezept inklusive Zutaten laden
- Sprache aus Culture/Cookie in DB-Suffix übersetzen
- Zutatenliste und Makros als DTO aufbauen

Warum:

- `GetRecipePreview` enthält viel Mapping- und Nutrition-Code.
- Die Sprachsuffix-Logik kann wiederverwendet werden.

### `RecipeDisplayService`

Aufgabe:

- Rezeptdetailmodell laden
- Posting-Daten laden
- Community-/User-Varianten laden
- Swap-NutritionOverride berechnen
- `ShowRecipeViewModel` erstellen

Warum:

- `ShowRecipe` ist die größte fachliche Action.
- Aktuell wird viel über `ViewData` gesteuert. Ein ViewModel wäre sicherer und lesbarer.

### `WorldNotificationService`

Aufgabe:

- Notifications listen
- einzelne Notification als gesehen markieren
- alle als gesehen markieren
- Notifications löschen
- DTOs für die UI erzeugen

Warum:

- Die Notification-Actions sind klar abgegrenzt.
- Der Service kann Transaktionen und Query-Details kapseln.

### `WorldLocalizationService`

Aufgabe:

- `SharedResources` für eine Culture als Dictionary ausgeben
- Culture validieren
- Fehler sauber loggen

Warum:

- Reflection auf `ResourceManager` gehört nicht in einen Controller.

### `RecipeTranslationSaveService`

Aufgabe:

- Request validieren
- AI-Übersetzung ausführen
- Rezept, Schritte und Zutaten in einer Transaktion speichern
- strukturierte Result-DTOs zurückgeben

Warum:

- `SaveRecipeWithTranslation` ist Schreiblogik mit Transaktion.
- Controller sollten keine komplexe Persistenz-Orchestrierung enthalten.

## ViewModel statt ViewData

Besonders `ShowRecipe` sollte auf ein stark typisiertes ViewModel umgestellt werden.

Vorschlag:

```csharp
public sealed class ShowRecipeViewModel
{
    public RecipeBaseData Recipe { get; set; } = default!;
    public bool CanPublishToFeed { get; set; }
    public int? ExistingPostingId { get; set; }
    public int? PostingIdForShare { get; set; }
    public string? PostingThumbnailUrl { get; set; }
    public string? PostingCreatorName { get; set; }
    public RecipeVariantDisplayModel? Variant { get; set; }
    public RecipeNutritionOverrideViewModel? NutritionOverride { get; set; }
    public int CommunityVariantCount { get; set; }
    public string? TranslatedSteps { get; set; }
}
```

Warum:

- Build-time Sicherheit statt magischer String-Keys.
- Die View wird verständlicher.
- Refactorings werden weniger fehleranfällig.

## Konkrete Umbau-Reihenfolge

### Phase 1: Risikoarme Extraktionen

1. `GetLocalizedStrings` in `WorldLocalizationController` verschieben.
2. `GetWorldUserNotifications`, `MarkWorldUserNotificationSeen`, `MarkAllWorldUserNotificationsSeen`, `ClearWorldUserNotifications` in `WorldNotificationsController` verschieben.
3. DTOs aus `HomeController` nach `Models/ViewModels` oder `Models/Requests` verschieben.

Warum zuerst:

- Diese Bereiche sind klein und fachlich klar.
- Wenig View-Risiko.
- Gute erste Build-/Regression-Kontrolle.

### Phase 2: Dashboard entkoppeln

1. `CreatorDashboardService` erstellen.
2. `UserDashboard` in `CreatorDashboardController` verschieben.
3. Controller-Action auf `return View(await service.BuildAsync(userHash, language))` reduzieren.

Warum:

- Dashboard-Queries und Summenberechnung gehören nicht in den Controller.
- Der Service ist leicht testbar.

### Phase 3: Recipe Preview und Marketplace Preview verschieben

1. `GetRecipePreview` nach `RecipeViewController`.
2. `RecipePreviewService` extrahieren.
3. `GetMarketplacePreview` nach `MarketplaceController`.

Warum:

- Beide Endpunkte sind JSON-APIs und nicht Home-spezifisch.
- Die Zuständigkeit wird sofort klarer.

### Phase 4: ShowRecipe umbauen

1. `ShowRecipeViewModel` erstellen.
2. `RecipeDisplayService` erstellen.
3. Swap-/NutritionOverride-Berechnung in eigenen Helper oder Service auslagern.
4. View schrittweise von `ViewData` auf Model-Properties umstellen.
5. Action nach `RecipeViewController` verschieben.

Warum:

- Das ist der größte Block und sollte kontrolliert passieren.
- Erst ViewModel einführen, dann verschieben.

### Phase 5: Schreiblogik auslagern

1. `SaveRecipeWithTranslation` nach `RecipeController` oder `RecipeTranslationController`.
2. `RecipeTranslationSaveService` erstellen.
3. Validation in Request-Validator/Service verschieben.
4. Transaktion im Service kapseln.

Warum:

- Persistenz-Workflows brauchen klare Fehlerbehandlung.
- Spätere Tests sind einfacher.

### Phase 6: Deaktivierte AI-Endpunkte entscheiden

Aktuell geben die AI-Rezeptvarianten-Endpunkte `410 Gone` zurück.

Option A: Endpunkte vollständig entfernen, wenn das Feature dauerhaft aus ist.

Option B: Endpunkte in `RecipeAiController` isolieren, wenn das Feature später wiederkommen soll.

Warum:

- Deaktivierte Features sollten nicht die Home-Dependencies aufblasen.
- `HomeController` braucht dann keine AI-Service-Dependencies mehr.

## Technische Cleanup-Punkte

- Debug-Logging im `Index` entfernen oder hinter `IHostEnvironment.IsDevelopment()` / Config-Flag legen.
- `_recipeAiTransformService`, `_recipeAiVariantJobService`, `_recipeAiNutritionService` aus `HomeController` entfernen, wenn AI-Endpunkte ausgelagert oder gelöscht sind.
- Doppelte Sprachmapping-Logik zentralisieren, z.B. `WorldLanguageService`.
- Nutrition- und Einheitenkonvertierung in einen eigenen Service auslagern.
- `DateTime.Now` durch `DateTime.UtcNow` ersetzen, wo Daten persistiert werden.
- Response-DTOs für JSON-Endpunkte definieren statt anonyme Objekte überall zu verwenden.
- CancellationToken konsequent an EF-Queries weitergeben.

## Tests nach dem Umbau

Mindestens prüfen:

- `/WorldMiniApp`
- Deep-Link mit `returnTo`
- Deep-Link mit Wildcard-`extraPath`
- Rezeptdetail normal
- Rezeptdetail mit `share=1`
- Rezeptdetail mit `direct=true`
- Rezeptdetail mit `swapVariantId`
- Rezeptdetail mit `communityVariantId`
- `GetRecipePreview`
- Notifications lesen/markieren/löschen
- UserDashboard ohne Login
- UserDashboard mit Creator-Login
- Marketplace-Preview
- `SaveRecipeWithTranslation`

Automatisiert sinnvoll:

- Unit-Tests für `NormalizeMiniAppReturnUrl` / Deep-Link-Service.
- Unit-Tests für Sprachsuffix-Mapping.
- Unit-Tests für NutritionOverride bei Swaps.
- Integrationstest für Notifications-Endpoints.

## Priorität

1. Notifications und Localization extrahieren.
2. Dashboard extrahieren.
3. RecipePreview und MarketplacePreview verschieben.
4. `ShowRecipe` mit ViewModel umbauen.
5. Schreiblogik und deaktivierte AI-Endpunkte bereinigen.

Diese Reihenfolge reduziert Risiko, hält die App nach jedem Schritt buildbar und macht den Controller Stück für Stück wieder verständlich.
