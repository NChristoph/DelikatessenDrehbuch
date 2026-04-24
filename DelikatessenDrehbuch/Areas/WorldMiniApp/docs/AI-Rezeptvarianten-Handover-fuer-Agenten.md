# AI-Handover: Rezeptvarianten & Zutatenvorschau

## Zweck dieser Datei

Diese Datei ist eine operative Kurzanleitung fuer eine AI/einen Agenten, der an der Rezeptvarianten-Pipeline der `WorldMiniApp` arbeitet.

Sie beantwortet vor allem diese Fragen:

- Wo startet der Request?
- Welche Services formen die AI-Antwort?
- Wo wird normalisiert, validiert und gespeichert?
- Warum kann die UI schlechter aussehen als der rohe AI-Output?
- Welche Stellen sind bei Bugs fast immer relevant?

Die allgemeine Funktionsdokumentation liegt in:

- [AI-Rezept-Transformation-Dokumentation.md](C:/Users/Free8/source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Areas/WorldMiniApp/docs/AI-Rezept-Transformation-Dokumentation.md)

Diese Datei ist die praktische Ergaenzung dazu.

---

## Schnellueberblick

Die Rezeptvarianten-Funktion besteht aus 4 Schichten:

1. `ShowRecipe.cshtml`
   Nimmt User-Aktionen entgegen, laedt Vorschlaege/Vorschau und rendert die Antwort.

2. `HomeController.cs`
   Bietet die HTTP-Endpunkte fuer Suggest, Preview/Job und Save.

3. `RecipeAiTransformService.cs`
   Kernlogik fuer Prompting, Tool-Calling, Normalisierung, Korrektur, Schutzlogik und Fallbacks.

4. Datenbank/Persistenz
   Gespeicherte Varianten landen in `RecipeAiBaseRecipe` plus Zutaten-/Schritt-Tabellen.

---

## Wichtigste Dateien

### UI

- [ShowRecipe.cshtml](C:/Users/Free8/source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Areas/WorldMiniApp/Views/Home/ShowRecipe.cshtml)
  Zentrale UI fuer:
  - Auswahl der AI-Variante
  - Zutaten-/Konzept-Vorschlaege
  - Vorschau-Overlay
  - Uebernahme der Vorschau in die Seite

Wichtige Funktionen in der View:

- `renderAiPreview(...)`
- `renderPreviewToPage(...)`
- `buildAiIngredientMarkup(...)`
- `formatIngredientQuantity(...)`
- `saveAiPreview(...)`

### Controller

- [HomeController.cs](C:/Users/Free8/source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Areas/WorldMiniApp/Controllers/HomeController.cs)

Wichtige Endpunkte:

- `SuggestAiVariantIngredients(...)`
- `StartAiRecipeVariantJob(...)`
- `GetAiRecipeVariantJobStatus(...)`
- `GetAiRecipeVariantJobResult(...)`
- `SaveAiRecipeVariant(...)`

### Service

- [RecipeAiTransformService.cs](C:/Users/Free8/source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Areas/WorldMiniApp/Services/RecipeAiTransformService.cs)

Das ist die wichtigste Datei im gesamten System.

Wichtige Methoden:

- `BuildIngredientSuggestionsAsync(...)`
- `SelectRecipeConceptsAsync(...)`
- `BuildPreviewPromptRequest(...)`
- `NormalizePreview(...)`
- `NormalizePreviewIngredients(...)`
- `PatchMissingIngredientsFromIngredientsText(...)`
- `PruneUnusedPreviewIngredients(...)`
- `ExecuteStructuredOpenAiPromptCallWithToolsAsync(...)`
- `ExecuteToolCallAsync(...)`
- `BuildSafeConceptPlanReason(...)`
- `BuildIngredientsText(...)`

---

## Request-Flows

## 1. Zutaten-/Konzeptvorschlaege

Flow:

`ShowRecipe.cshtml`
-> `POST /SuggestAiVariantIngredients`
-> `HomeController.SuggestAiVariantIngredients(...)`
-> `RecipeAiTransformService.BuildIngredientSuggestionsAsync(...)`

Dieser Flow erzeugt:

- 4 Konzeptideen
- `conceptIngredientPlan`
- Highlights / Summary / Approach

Wichtig:

- Hier wird noch kein finales Rezept gebaut.
- Hier entscheidet sich, welche Zutaten spaeter in die Vorschau muessen.
- Wenn hier falsche `ingredientId` oder schwammige Begruendungen entstehen, wird spaeter fast alles instabil.

## 2. Vorschau der konkreten Rezeptvariante

Flow:

`ShowRecipe.cshtml`
-> Job-Start / Polling / Result
-> `RecipeAiTransformService`
-> finale `RecipeAiTransformPreview`

Diese Preview enthaelt:

- `title`
- `summary`
- `ingredients`
- `ingredientsText`
- `preparationText`
- `steps`
- `highlights`

Wichtig:

- Der rohe AI-Output ist nicht die Endwahrheit.
- Danach laufen noch Normalisierung, Reparatur und Schutzlogik.

## 3. Speichern

Flow:

`ShowRecipe.cshtml`
-> `POST /SaveAiRecipeVariant`
-> `HomeController.SaveAiRecipeVariant(...)`

Gespeichert werden:

- Kopfdatensatz in `RecipeAiBaseRecipe`
- Zutaten in `RecipeAiBaseRecipeIngredients`
- Schritte in `RecipeAiBaseRecipeSteps`
- ausgewaehlte Zutaten in `RecipeAiBaseRecipeSelectedIngredients`

---

## Die eigentliche Wahrheit im System

Wenn etwas nicht zusammenpasst, gilt diese Reihenfolge:

1. `preview.ingredients`
2. `preview.preparationText`
3. `preview.steps`
4. `preview.ingredientsText`
5. UI-Rendering

Warum das wichtig ist:

- Die AI kann plausiblen Freitext schicken, aber falsche IDs.
- Die UI kann die Daten schlechter anzeigen als sie wirklich im JSON vorliegen.
- `ingredientsText` ist nur ein abgeleiteter Text und sollte nie als alleinige Quelle behandelt werden.

---

## Wo typische Fehler entstehen

## A. Tool-Calling-Schleifen

Symptom:

- viele `get_ingredients(...)`-Calls
- danach `OpenAI tool calling exceeded max rounds`

Relevante Stellen:

- `ExecuteStructuredOpenAiPromptCallWithToolsAsync(...)`
- `BuildConceptPickPromptRequest(...)`
- `AllowedToolCategoryKeys`
- `MaxToolRounds`

Merke:

- Wenn das Modell kreuz und quer neue Kategorien nachlaedt, ist das fast nie ein DB-Problem.
- Meist sind Tool-Freiraeume zu weit oder der Prompt ist nicht hart genug.

## B. AI nennt Zutat A, liefert aber ID von Zutat B

Symptom:

- `ingredientId=7`, Text sagt aber Olivenoel
- Summary/Reason passen nicht zur ID

Relevante Stellen:

- `BuildSafeConceptPlanReason(...)`
- Konzept-Prompt
- Nachbearbeitung der `ConceptIngredientPlan`

Merke:

- IDs und Namen nie blind aus dem Modell vertrauen.
- Immer serverseitig gegen die echte aufgeloeste Zutat absichern.

## C. Neue Zutaten verschwinden wieder

Symptom:

- AI-Output enthaelt z. B. `Rote Linsen` und `Aquafaba`
- UI oder spaetere Version zeigt sie nicht mehr

Relevante Stellen:

- `NormalizePreviewIngredients(...)`
- `PatchMissingIngredientsFromIngredientsText(...)`
- `PruneUnusedPreviewIngredients(...)`
- `BuildIngredientsText(...)`

Merke:

- `PruneUnusedPreviewIngredients(...)` entfernt Zutaten, die im finalen Text nicht als verwendet erkannt werden.
- Wenn die Zutat sinnvoll geplant ist, aber im Text nicht deutlich genug vorkommt, kann sie dort verloren gehen.

## D. UI zeigt schlechteren Inhalt als der AI-Response

Symptom:

- API/Response sieht gut aus
- Seite zeigt alte oder unvollstaendige Zutatenliste

Relevante Stellen:

- `renderAiPreview(...)`
- `renderPreviewToPage(...)`
- `buildAiIngredientMarkup(...)`
- `formatIngredientQuantity(...)`

Merke:

- Overlay-Vorschau und Seitenansicht muessen dieselbe Formatlogik nutzen.
- Unterschiedliche Renderer fuehren schnell zu "AI war gut, UI zeigt Mist".

---

## Wichtige Schutzmechanismen, die bereits eingebaut wurden

Folgende Guards existieren bereits:

- Tool-Call-Logging fuer `get_ingredients`
- Begrenzte Tool-Runden
- Eingeschraenkte erlaubte Tool-Kategorien pro Prompt
- serverseitig sichere `reason`-Texte fuer Konzeptzutaten
- `ingredientPlan` wird als Vertrag behandelt
- fehlende Plan-Zutaten werden nachgezogen
- undefinierte generische Pantry-Phrasen werden bereinigt
- `ingredientsText` wird aus den kanonischen Zutatenzeilen neu aufgebaut

Wenn du aenderst, pruefe immer zuerst, ob du nicht einen bestehenden Guard aus Versehen aushebelst.

---

## Debug-Strategie fuer neue Bugs

Wenn eine Rezeptvariante kaputt aussieht, arbeite in dieser Reihenfolge:

1. OpenAI-Response ansehen
   Ist die Rohantwort schon falsch?

2. Logs von `get_ingredients` ansehen
   Kommen die richtigen Kategorien und Kandidaten?

3. `NormalizePreview(...)` pruefen
   Wurde die Preview nachtraeglich veraendert?

4. `PruneUnusedPreviewIngredients(...)` pruefen
   Wurde eine neue Zutat wieder entfernt?

5. `ShowRecipe.cshtml` pruefen
   Wird das korrekte `preview.ingredients` wirklich angezeigt?

Wenn das rohe JSON gut ist, aber die Seite schlecht aussieht, ist es fast immer:

- ein UI-Renderproblem
- oder eine spaete Normalisierung, nicht das Modell selbst

---

## Konkrete Stellen fuer haeufige Eingriffe

### Prompting verbessern

Datei:

- [RecipeAiTransformService.cs](C:/Users/Free8/source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Areas/WorldMiniApp/Services/RecipeAiTransformService.cs)

Suche nach:

- `BuildConceptPickPromptRequest`
- `BuildPreviewPromptRequest`
- QA-/Repair-Prompts

### Zutat wird nicht angezeigt

Pruefe:

- `NormalizePreviewIngredients(...)`
- `PatchMissingIngredientsFromIngredientsText(...)`
- `PruneUnusedPreviewIngredients(...)`
- `BuildIngredientsText(...)`
- `buildAiIngredientMarkup(...)`

### Mengen oder Einheiten sehen falsch aus

Pruefe:

- `NormalizeQuantityAndMeasure(...)`
- `NormalizeMeasureToken(...)`
- `formatIngredientQuantity(...)`
- `buildAiIngredientMarkup(...)`

### Gespeicherte Variante unterscheidet sich von Vorschau

Pruefe:

- `SaveAiRecipeVariant(...)`
- `ResolveOrCreateIngredientMeasureQuantityId(...)`
- Erstellung der `RecipeAiBaseRecipeIngredients`

---

## Praktische Regeln fuer spaetere AI-Agenten

1. Nicht sofort den Prompt beschuldigen.
   Erst pruefen, ob der Fehler erst in Normalisierung oder UI entsteht.

2. `preview.ingredients` ist entscheidend.
   Wenn dort die Zutat fehlt, ist es ein Backend-/Normalisierungsproblem.
   Wenn sie dort vorhanden ist, aber nicht sichtbar, ist es ein UI-Problem.

3. Freitext nie ohne ID-Validierung vertrauen.
   Besonders bei `reason`, `summary`, `approach` und Pantry-Zutaten.

4. Bei High-Protein-/Low-Carb-Varianten auf echte Kochbarkeit achten.
   Nicht nur Proteinsprache, sondern echte umsetzbare Schritte.

5. Nach jeder relevanten Aenderung mindestens diese zwei Dinge pruefen:
   - wird die Zutat im finalen `preparationText` wirklich benutzt?
   - wird sie in `preview.ingredients` und in der UI sichtbar?

---

## Empfehlung fuer die naechste AI

Wenn du neu in das Thema einsteigst, beginne mit:

1. [HomeController.cs](C:/Users/Free8/source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Areas/WorldMiniApp/Controllers/HomeController.cs)
2. [RecipeAiTransformService.cs](C:/Users/Free8/source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Areas/WorldMiniApp/Services/RecipeAiTransformService.cs)
3. [ShowRecipe.cshtml](C:/Users/Free8/source/repos/DelikatessenDrehbuch/DelikatessenDrehbuch/Areas/WorldMiniApp/Views/Home/ShowRecipe.cshtml)

Und zwar in genau dieser Reihenfolge:

- erst API-Grenzen verstehen
- dann Normalisierung/Prompting verstehen
- dann erst UI reparieren

So spart man sich sehr viel Fehlersuche.
