# CreatePosting KI Handover 2026-04-13

## Zweck

Diese Datei ist eine kompakte Session-Uebergabe fuer jede nachfolgende KI.
Sie beschreibt, was in dieser Session an der CreatePosting-Logik geaendert wurde,
warum diese Aenderungen gemacht wurden und welche Stellen im Code dabei zusammenhaengen.

Der Fokus dieser Session lag auf:

- ingredient matching fuer vorausgefuellte Step-Variablen
- hybride Variablen wie `base`
- dem oberen Probability-/Vorschlagsbereich "Was koennte es sein?"
- der Trennung zwischen "was darf matchen?" und "was soll im Satz sichtbar sein?"
- einem neuen Prep-Step `PREP_SMASH_01` fuer "flachdruecken" / smashed potatoes

---

## Kernproblem vor der Session

Die Datenbank `IngredientsAndNutrients` enthaelt bereits viele nuetzliche `is_*`-Bools
wie `is_liquid`, `is_cuttable`, `is_fryable`, `is_powder` usw.

Diese Informationen wurden aber im CreatePosting-Flow noch nicht konsequent genutzt.
Dadurch entstanden mehrere Probleme:

- `thickener` war zu grob und zog Grundnahrungsmittel wie Pasta/Reis mit hinein
- `base` wurde in Hybrid-Faellen oft unnatuerlich aus vielen Zutaten zusammengesetzt
- `COOK_SAUTE_01` zeigte fast alle ausgewaehlten Zutaten an, auch unpassende
- der obere Probability-Bereich behandelte Defaults teilweise wie echte User-Eingaben
- optionale Teile verschwanden oben, obwohl der User sie noch gar nicht gesetzt hatte

---

## Wichtige Architektur-Idee

Es gibt jetzt bewusst zwei Ebenen:

1. Match-Ebene
   Diese bestimmt, welche Zutaten fachlich zu einer Variable/einem Step passen.

2. Display-Ebene
   Diese bestimmt, welche der gematchten Zutaten wirklich im sichtbaren Satz angezeigt werden.

Das ist wichtig:

- der Editor darf viele passende Zutaten kennen
- der sichtbare Satz soll aber nur 1-3 sinnvolle Zutaten zeigen

Ohne diese Trennung wird der Vorschlagstext schnell unlesbar und unnatuerlich.

---

## Neue/geaenderte Dateien

### 1. `wwwroot/data/ingredient_match_rules.json`

Neu in dieser Session.

Zweck:

- deklaratives Ingredient-Matching per JSON
- Nutzung vorhandener `is_*`-Flags + `GroupId`
- Step-spezifische Regeln statt immer mehr Hardcoding in JS

Wichtige Inhalte:

- `variable_rules`
- `step_variable_rules`
- spaeter ergaenzt um `display_limit`, `display_prefer_groups`, `display_prefer_flags`

Wichtige Session-Regeln:

- `thickener` nutzt jetzt `isPowder`
- `base` schliesst `liquid`, `fat`, `powder`, Gewuerze und mehrere Gruppen aus
- `COOK_SEAR_01.base` bekommt eigene Match-Regel
- `COOK_SAUTE_01.ingredient` wurde spaeter stark eingegrenzt:
  - `isCuttable`
  - nicht `isLiquid`
  - nicht `isFat`
  - nicht `isPowder`
  - keine Gruppe `5` (Gewuerze)

Display-Regeln:

- `ingredient` zeigt standardmaessig maximal `2`
- `ingredients` zeigt standardmaessig maximal `3`
- `COOK_SAUTE_01` bevorzugt fuer die Anzeige vor allem Gruppen `2`, `1`, `6`, `3`
- `PREP_SMASH_01` ist absichtlich eng gemappt:
  - bevorzugt kartoffelartige Zutaten ueber `allow_name_contains`
  - erlaubt alternativ harte, boilable Gemuese aus Gruppe `2`
  - schliesst `liquid`, `fat`, `powder`, Gewuerze und Sonstiges aus

### 1b. Neuer Step `PREP_SMASH_01`

Neu spaeter in der Session ergaenzt.

Zweck:

- Schritt fuer "flachdruecken" / "leicht zerdruecken"
- gedacht fuer Faelle wie smashed potatoes
- Template auf Deutsch:
  - `Druecke {{ingredient}}[ mit einem {{tool}}] flach, bis {{pronoun}} {{state}} {{copula}}.`

Wichtige Begleitregeln:

- `wwwroot/data/master_step_option_rules.json`
  - neue Action `smash`
  - bevorzugte Tools: `Kartoffelstampfer`, `Glas`
  - bevorzugte States: `flachgedrueckt`, `leicht aufgebrochen`
- `wwwroot/data/ingredient_match_rules.json`
  - `PREP_SMASH_01` ist bewusst nicht offen fuer Pasta/Reis/Mehl
  - Kartoffel-Matching laeuft ueber `allow_name_contains`
  - Gemuese darf alternativ ueber Gruppe `2` + `isHard` + `isBoilable` matchen

Wichtig fuer spaetere KIs:

- Wenn `PREP_SMASH_01` kuenftig zu oft vorgeschlagen wird, zuerst die Ingredient-Regel schaerfen, nicht das Template.
- Wenn der Step fuer weitere smashed-Rezepte genutzt werden soll, danach eher `probability_template_presets.json` erweitern als die Matching-Regel wieder breit zu machen.

### 2. `wwwroot/js/create-posting-data-urls.js`

Ergaenzt um:

- `ingredientMatchRules: "/data/ingredient_match_rules.json"`

Damit kann das JSON zentral geladen werden.

### 3. `Areas/WorldMiniApp/Views/Home/CreatePosting.cshtml`

Frontend-Durchreichung erweitert um:

- `data-is-powder`

Grund:

- `is_powder` war in der DB schon vorhanden, wurde aber im Frontend-Matching vorher nicht sauber verwendet.

### 4. `wwwroot/js/CreatePostingPage.js`

Sehr wichtige Datei dieser Session.

Wesentliche Aenderungen:

- `isPowder` bis in Sandbox-/Selected-Ingredient-Objekte durchgereicht
- `matchIngredientsToVariable()` nutzt jetzt SC2-Regeln statt rein grober Logik
- fuer sichtbare Texte wird jetzt `selectIngredientsForDisplay()` verwendet
- `base` wird bei required-Hybrid-Faellen nicht mehr aus allen Zutaten zusammengesetzt, sondern startet mit Default
- Probability-Rendering trennt jetzt sichtbare Defaults von wirklich explizit gesetzten User-Werten
- Edit-Mode schreibt bestehende Werte auch in `_explicitValues`

Wichtige Stellen:

- `buildVariablesForTemplate()`
- `matchIngredientsToVariable()`
- `renderProbabilityTemplate()`
- Probability-Draft-Initialisierung

### 5. `wwwroot/js/CreatePostingSmartStepCreator.js`

Hier sitzt der Kern der neuen Matching- und Editor-Logik.

Neue/erweiterte Verantwortlichkeiten:

- Laden des neuen JSON-Regelwerks
- generisches Auswerten von Rules (`require_all`, `exclude_any`, `allow_groups` usw.)
- neue Display-Auswahl fuer Zutaten:
  - `getIngredientDisplayConfig()`
  - `selectIngredientsForDisplay()`
- `base` ist weiterhin Hybrid
- Label fuer Hybrid-`base` im Editor jetzt klarer:
  - "Zutat oder Basis"

Diese Datei exportiert jetzt u.a.:

- `filterIngredientsByVarType`
- `selectIngredientsForDisplay`

### 6. `wwwroot/js/create-posting-template-draft-engine.js`

Erweitert um getrennte Speicherung expliziter User-Werte:

- neues Feld: `_explicitValues`

Wichtig:

- `values` = sichtbare/aktuelle Werte im Probability-Draft
- `_explicitValues` = nur Werte, die wirklich vom User gesetzt wurden

Das ist die Grundlage dafuer, dass optionale Pills oben wieder korrekt erscheinen.

### 7. `wwwroot/js/create-posting-probability.js`

Der obere Bereich rendert Probability-Templates.

Wichtige Aenderung:

- rendert jetzt im `mode: 'probability'`
- uebergibt getrennt:
  - `varsOverride` fuer sichtbare Vorschauwerte
  - `optionalValues` nur aus `_explicitValues`

Ergebnis:

- Defaults duerfen sichtbar sein
- optionale Segmente gelten trotzdem erst dann als "gesetzt", wenn der User sie wirklich gesetzt hat

---

## Warum der obere Bereich vorher falsch war

Das war ein wichtiges Erkenntnisziel der Session.

Der obere Bereich "Was koennte es sein?" hatte zwei gekoppelte Probleme:

### A. Falsche Vorauswahl

`base` wurde in `buildVariablesForTemplate()` fuer required-Variablen bewusst direkt mit dem Default befuellt.
Dadurch stand oben oft `das Gericht`, auch wenn fachlich eigentlich eine konkretere Zutat sinnvoll gewesen waere.

### B. Optionale Teile verschwanden zu frueh

Der obere Renderer behandelte gemergte Default-Werte teilweise wie echte User-Werte.
Dadurch verschwanden optionale Plus-Pills schon dann, wenn z.B. `pronoun` automatisch gesetzt war.

Die jetzige Loesung:

- sichtbare Defaults bleiben erlaubt
- Optionalitaet wird nur ueber `_explicitValues` entschieden

---

## Warum `COOK_SAUTE_01` vorher fast alles angezeigt hat

Wichtiger Debug-Befund aus dieser Session:

`COOK_SAUTE_01` hatte vorher keine eigene Step-Rule fuer `ingredient`.

Dann passierte Folgendes:

- keine brauchbaren `required_ingredient_tags`
- kein spezifischer Step-Eintrag in `ingredient_match_rules.json`
- Fallback verhaelt sich dann zu generisch

Ergebnis:

- Zucker
- Pasta
- Reis
- Mehl
- Kraeuter/Gewuerze
- verschiedene Neben-Zutaten

landeten gemeinsam im Satz.

Dieses Problem wurde jetzt durch eine konkrete `COOK_SAUTE_01`-Regel plus Display-Limit entschaerft.

---

## Hybrid-Variablen in dieser Session

### `base`

`base` ist weiterhin bewusst eine Hybrid-Variable.

Das bedeutet:

- Nutzer kann eine konkrete Zutat waehlen
- oder eine abstraktere Basis-Option wie `das Gericht`

Wichtig:

- fuer Auto-Prefill wird `base` nicht mehr aus allen Zutaten zusammengebaut
- im Editor wird `base` klar als "Zutat oder Basis" bezeichnet

### Hybrid-Override

Wenn im Hybrid-Editor bereits Multi-Ingredients vorhanden sind und der User stattdessen eine Hybrid-Option klickt,
wird die Multi-Liste geloescht und der Hybrid-Wert uebernommen.

Das ist wichtig, damit `Salzwasser` oder `das Gericht` nicht mit alten Multi-Zutaten kollidieren.

---

## Designregel fuer kuenftige KI-Weiterarbeit

Wenn ein Step verbessert werden soll, bitte diese Reihenfolge einhalten:

1. Zuerst in `ingredient_match_rules.json` pruefen
   Nicht sofort in JS hardcoden.

2. Match und Display getrennt denken
   Nicht alles, was matcht, direkt im Satz anzeigen.

3. Vorhandene `is_*`-Bools bevorzugen
   Nicht sofort neue DB-Felder anlegen.

4. `GroupId` nur als grobe Grenze nutzen
   Die genauere Logik sollte eher ueber Bools + wenige klare JSON-Regeln laufen.

5. Probability-Bereich beachten
   Oben und unten sehen zwar aehnlich aus, haben aber unterschiedliche Renderpfade.
   Wenn etwas oben "komisch" aussieht, immer `_explicitValues`, `varsOverride` und `mode: 'probability'` pruefen.

---

## Empfohlene naechste Schritte

Wenn eine naechste KI hier weiterarbeitet, sind diese Punkte wahrscheinlich die sinnvollsten:

### 1. Weitere Koch-Steps auf Display-Regeln umstellen

Aktuell ist das System generisch vorhanden, aber noch nicht fuer alle Steps fein gepflegt.
Naheliegende Kandidaten:

- `COOK_FRY_01`
- `COOK_SEAR_01`
- `COOK_ARRANGE_01`
- `FINISH_SPRINKLE_01`

### 2. `base` im oberen Probability-Bereich weiter feinjustieren

Momentan ist `base` fuer required-Faelle bewusst auf Default gestellt.
Wenn gewuenscht, kann man spaeter unterscheiden:

- sichtbarer Default
- vorgeschlagene konkrete Zutaten im Editor

### 3. Browser-Durchklicktest machen

In dieser Session wurde logisch und per Codepfad geprueft, aber kein echter UI-Durchklicktest dokumentiert.
Insbesondere pruefen:

- `COOK_SAUTE_01`
- `COOK_SEAR_01`
- optionale Pills oben im Probability-Bereich
- Hybrid-Verhalten bei `base`

---

## Kurzfassung fuer die naechste KI

Wenn du nur 30 Sekunden hast, merke dir:

- neues JSON-Regelwerk fuer Ingredient-Matching ist jetzt zentrale Quelle
- `is_powder` wurde bis ins Frontend durchgezogen
- `base` ist Hybrid, aber Auto-Prefill startet jetzt sauberer
- Probability oben trennt jetzt sichtbare Defaults und echte User-Werte
- optionale Plus-Pills oben haengen jetzt an `_explicitValues`
- `COOK_SAUTE_01` wurde eingeschraenkt
- Anzeige-Logik ist jetzt getrennt von Match-Logik
