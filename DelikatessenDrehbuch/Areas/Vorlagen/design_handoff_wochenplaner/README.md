# Handoff: Avocado Wochenplaner — TikTok-Feed-Redesign

## Overview
Drei Designvarianten für einen Wochenplaner einer Food/Rezept-App ("Avocado"). Der Wochenplaner zeigt 7 Tage mit je 3 Mahlzeiten (Vorspeise / Hauptspeise / Dessert), Nährwerte gesamt + pro Tag, Aktionen (Speichern, Shuffle, Teilen, Kalender-Export, Einkaufsliste). Der Stil ist an den TikTok-Feed der Marke angelehnt: dunkles Waldgrün + Koralle-Akzent, große Foodbilder, Pill-Buttons, runde Icons.

## About the Design Files
Die Dateien in diesem Paket sind **Design-Referenzen, gebaut in HTML/React (via Babel-Standalone)** — Prototypen, die den gewünschten Look und das Verhalten zeigen. Sie sind **nicht zum direkten Ausliefern gedacht**.

Aufgabe: Diese HTML-Designs **in der bestehenden Codebasis nachbauen** (React Native, SwiftUI, Flutter, Web-React etc.) mit den dort üblichen Komponenten- und State-Mustern. Falls noch keine Codebasis existiert, das passende Framework wählen.

## Fidelity
**Hi-fidelity.** Farben, Typografie, Spacing, Radien und Schatten sind final. Pixelgenaue Übernahme erwünscht. Bilder sind Unsplash-Platzhalter und müssen durch echte Rezeptbilder ersetzt werden.

## Variants
Drei eigenständige Varianten — der User entscheidet sich am Ende für **eine** (oder du baust alle drei und schaltest per Theme um):

### A · Editorial Stack (`A-Editorial-Stack.html` / `v1-editorial.jsx` + `v1.css`)
Cream Background, vertikales Stack-Layout. Jede Mahlzeit = große Foodkarte mit Caption-over-Image. Sticky Header mit Brand-Pill + Tag-Navigation. Macro-Block in dunkelgrün. Coral-FAB unten rechts.

### B · Story Rail (`B-Story-Rail.html` / `v2-story-rail.jsx` + `v2.css`)
Cream Background, große Serif-Headline. Pro Tag eine **horizontale Swipe-Rail** mit kompakten Mahlzeit-Cards. Quick-Action-Bar oben (Speichern/Shuffle/Teilen/Kalender/Einkauf). Donut-Macro-Card. Floating Bottom-Bar mit primärer Aktion.

### C · Dark Cinematic Green (`C-Dark-Green.html` / `v3-cinematic.jsx` + `v3.css`) — **User-Favorit**
Voller Bild-Hero (Food-Foto) mit grünem Gradient-Overlay. Frosted-Glass Sheet darüber im dunkelgrünen Avocado-Look (`#0d1f15` Basis, `rgba(18,42,28,0.78)` Sheet). Tag-Pills horizontal scrollend, kompakte Mahlzeit-Reihen, Coral-FAB mit Glow. **Funktional ist diese Variante das Ziel.**

## Screens / Views

### Hauptscreen: Wochenplaner-Modal (390×844, iPhone)
Ein vertikal scrollbares Modal/Sheet. Komponenten von oben nach unten:

#### 1. Top-Bar (sticky)
- 38×38px Round-Icon links (Zurück) — Variante A/C; Variante B hat Pull-Handle (40×4 abgerundeter Strich)
- Brand-Pill mittig: dunkelgrünes Pill `#1f4530`, weiße Typo, 🥑 Emoji + "Wochenplaner"
- 38×38px Round-Icon rechts (Mehr / 3-Dots oder Close-X)

#### 2. Hero-Bereich
- **Eyebrow**: 11px, 700, Letter-Spacing 0.14em, UPPERCASE, Coral `#ff7a59`. Inhalt: "Dein Plan · KW 17" oder "KW 17 · 22.–28. April"
- **Title**: Fraunces, 34–52px (je Variante), Weight 500–600, Letter-Spacing -0.025 bis -0.035em, Color `#1f4530` (light) oder `#ffffff` (dark)
- **Meta-Chips**: Personen, Total-kcal, Progress (gefüllte Slots) — als Pill-Badges, hellgrün-transparenter BG (`rgba(45,90,61,0.08)`), 12.5px 600 Inter

#### 3. Progress-Bar (nur Variante A)
- 6px hoch, Track `rgba(45,90,61,0.12)`, Fill linear-gradient von `#ff7a59` zu `#ff9576`, voll-rund

#### 4. Action-Row (oder Quick-Bar in B / Action-Rail in C)
- 4–5 Aktionen: **Speichern** (primär, dunkelgrün gefüllt) / Shuffle / Teilen / Kalender / Einkauf
- 44px Höhe, Radius 14px (A) oder 999px Pills (B/C)
- Inter 14px 600/700, weiße Iconographie wenn primär, sonst dunkelgrün auf Weiß

#### 5. Pending-Recipe-Card (Variante A)
Animierter Hinweis "wird hinzugefügt":
- Weißes Card mit 4px Coral linker Border-Bar
- 48×48 Thumbnail
- "WIRD HINZUGEFÜGT" Eyebrow + pulsierender 7×7 Coral-Dot (Animation `pulse 1.4s infinite`)
- Rezept-Titel (14px 600)
- Close-X rechts (28×28 grauer Round-Button)

#### 6. Macro-Block (Variante A & B)
Dunkelgrüne Card (`#1f4530`), Radius 24px, Coral-Glow rechts oben.
- **Variante A**: Total-kcal in Fraunces 40px, daneben kleiner Progress-Ring (56×56) für Wochenziel + 3 horizontale Macro-Bars (Protein, Fett, KH)
- **Variante B**: Großer Multi-Color Donut (108×108) mit kcal in der Mitte + Liste aus 3 Macro-Reihen mit Dot, Wert (Fraunces 20px), Prozent

#### 7. Tag-Navigation
- **Variante A**: Sticky-Bar unter Topbar mit 7 gleich breiten Buttons (Mo–So). Jeder Button zeigt 2-Buchstaben-Kürzel + 3 Status-Dots (gefüllt = vorhanden). Aktiv = dunkelgrüner BG, weiße Typo.
- **Variante C**: Horizontal scrollende Pill-Reihe. Aktive Pill = Coral-BG mit Shadow + weißer Border. Zeigt `Mo` + `1/3` Counter.
- **Variante B**: Keine globale Nav — Tage stehen direkt untereinander.

#### 8. Tages-Sektion
- **Variante A**: Großes Fraunces-Datum (44px Coral) + Tagesname (20px 700 dunkelgrün) + Duplizier-Button. Darunter 3 Mahlzeit-Slots vertikal.
- **Variante B**: Tagesname + Datum links, kcal-Pill (Coral-tint) + 3-Dots rechts. Darunter horizontale Rail.
- **Variante C**: Großes Fraunces-Datum (28px) + kleine kcal-Pill mit pulsierendem Dot. Darunter 3 Mahlzeit-Reihen.

#### 9. Mahlzeit-Card (Hauptkomponente)

**Variante A — Full-Width Hero:**
- Aspect-Ratio 16:10 Foodbild, Radius 20px oben
- Type-Pill oben links (`Vorspeise` / `Hauptspeise` / `Dessert`), schwarz transparent + blur, 10.5px 700 UPPERCASE
- Favorite-Heart oben rechts (34×34 schwarz transparent)
- Gradient-Overlay (transparent → schwarz 70% am Bottom)
- Caption-Bottom: Titel (17px 700 weiß, text-shadow), Stats `🔥 612 kcal` `⏱ 25 Min`
- Action-Footer (border-top): Bearbeiten / Ersetzen / Löschen-Button (rot tint)

**Variante B — Compact Card (180px breit):**
- Aspect-Ratio 4:3 Bild oben mit Type-Pill + Edit-Button
- Body: Titel (13px 700, 2-line clamp), Stats-Row (kcal + Min mit Icons), Macro-Bar (3-Segment Stripe) am Ende

**Variante C — Reihe mit Thumbnail:**
- 60×60 Thumbnail links (Radius 14px)
- Coral-Type-Eyebrow (`VORSPEISE` 9.5px 700)
- Titel (14px 600, ellipsis)
- Stats `612 kcal · 25 Min` (11.5px, 55% white)
- 3-Dots rechts

#### 10. Empty-Slot
Gestricheltes Border-Card (1.5px dashed `rgba(45,90,61,0.25)` oder white-tint in C):
- Plus-Icon im Round-Background (40×40)
- Mahlzeit-Typ-Label
- "Rezept wählen" Sublabel

#### 11. Tages-Nährwert-Footer (Variante A)
Cliclkbarer Streifen unten an jedem Tag:
- "1410 kcal" (Fraunces 16px 700) + "Tages-Nährwerte" + Chevron-Down
- BG `rgba(45,90,61,0.05)`, Radius 14px

#### 12. Einkaufslisten-CTA (Variante A)
Großes Banner unten, dunkelgrüner BG:
- 44×44 Icon-Square (Coral icon)
- "Einkaufsliste laden" + "32 Zutaten aus 12 Rezepten"
- 40×40 Coral Round-Button mit Pfeil

#### 13. Floating Action Button
- 56–58px rund, Coral `#ff7a59`, Plus-Icon weiß
- Position: bottom 18–24px / right 22–24px
- Box-Shadow `0 10px 30px -4px rgba(255,122,89,0.6)`
- Variante C: zusätzlich 2px weißer Ring + Glow-Pseudo-Element

#### 14. Bottom-Bar (nur Variante B)
Floating Pill-Bar unten:
- Frosted-Glass `rgba(255,255,255,0.85)` + blur
- Linke Sekundär-Aktion (Einkauf-Counter "32 Zutaten")
- Rechte Primär-Aktion (Coral, "Rezept hinzufügen")

## Interactions & Behavior
- **Tag-Wechsel** (Variante C): `setActive(i)` schaltet die Pill auf aktiv und zeigt darunter die Mahlzeiten dieses Tages. Smooth fade-in 200ms reicht.
- **Empty-Slot Tap**: Öffnet Rezept-Auswahl-Sheet (nicht Teil dieses Designs)
- **Mahlzeit-Card Tap**: Öffnet Rezept-Detail
- **Edit-Button**: Inline-Edit oder Bottom-Sheet zum Ersetzen
- **Pending-Card Pulse**: 7px Dot, opacity 1↔0.5 + scale 1↔0.8, 1.4s ease infinite
- **FAB**: Öffnet Rezept-Hinzufügen-Flow
- **Sticky-Behavior**: Topbar + Tag-Nav (A) sticky beim Scroll
- **Horizontal Rails** (B & C-days): `overflow-x: auto`, `scroll-snap-type: x mandatory`, `scroll-snap-align: start` an jeder Card

## State Management
```ts
type Plan = {
  id: string;
  name: string;       // "Feed-Plan KW 17"
  weekNumber: number; // 17
  startDate: Date;
  personCount: number;
  days: Day[];        // length 7, Mo–So
};

type Day = {
  date: Date;
  meals: { type: 'starter' | 'main' | 'dessert'; recipeId: string | null }[];
};

type Recipe = {
  id: string;
  title: string;
  imageUrl: string;
  kcal: number;
  durationMin: number;
  macros: { protein: number; fat: number; carbs: number }; // grams
};
```

State-Transitions: `addRecipe(dayIdx, mealIdx, recipeId)`, `removeRecipe(...)`, `duplicateDay(srcIdx, dstIdx)`, `shufflePlan()`, `savePlan()`, `sharePlan()` → returns share-URL, `exportIcs()`, `setPersonCount(n)`.

Datenfetch: Rezepte aus eigener API, Plan persistiert lokal + Server.

## Design Tokens

```css
/* Colors */
--avo-green:      #2d5a3d;  /* primary brand */
--avo-green-deep: #1f4530;  /* deep / surfaces */
--avo-green-soft: #e8efe9;  /* tint backgrounds */
--avo-coral:      #ff7a59;  /* accent / FAB */
--avo-coral-glow: #ffb199;  /* highlight */
--avo-cream:      #faf7f2;  /* light bg */
--avo-paper:      #ffffff;
--avo-ink:        #1a1a1a;
--avo-muted:      #6b6b6b;
--avo-line:       rgba(0,0,0,0.08);

/* Dark variant base */
--v3-bg-base:     #0d1f15;
--v3-sheet:       rgba(18,42,28,0.78); /* over backdrop-blur */

/* Macro semantic */
--macro-protein:  #c55a3d;
--macro-fat:      #e8b34a;
--macro-carbs:    #5b8a52;

/* Radii */
--r-xs: 8px;  --r-sm: 12px;  --r-md: 18px;  --r-lg: 24px;  --r-xl: 32px;

/* Shadows */
--shadow-sm: 0 1px 2px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.06);
--shadow-lg: 0 2px 4px rgba(0,0,0,0.05), 0 16px 40px rgba(0,0,0,0.08);
--shadow-coral: 0 10px 30px -4px rgba(255,122,89,0.6);

/* Type */
--font-sans:    "Inter", system-ui, sans-serif;
--font-display: "Fraunces", "Playfair Display", Georgia, serif;
```

### Type-Scale
| Token | Size | Weight | Letter-Spacing | Usage |
|---|---|---|---|---|
| display-xl | 50–52px | 500 (Fraunces) | -0.035em | Hero-Title |
| display-lg | 34–44px | 500–600 (Fraunces) | -0.025em | Section / Day-Number |
| display-md | 22–28px | 500 (Fraunces) | -0.02em | Stats / Sub-Hero |
| heading | 20px | 700 (Inter) | -0.01em | Tagesname |
| body-lg | 17px | 700 (Inter) | -0.01em | Card-Title (A) |
| body | 14px | 600 (Inter) | normal | Buttons / Item-Title |
| caption | 12.5px | 600 (Inter) | normal | Chips / Stats |
| eyebrow | 10.5–11px | 700 (Inter) | 0.12–0.16em UPPERCASE | Type-Pills, Section-Tags |

## Assets
- **Bilder**: Aktuell Unsplash-Fotos (Schweinebraten, Asia-Pfanne, Pavlova, Salat, Quinoa-Bowl). Müssen durch echte Rezeptbilder ersetzt werden. Mindest-Auflösung 800×600 für Hero-Cards.
- **Icons**: Inline-SVG, Stroke-Style, 14–22px, `stroke-width: 2–2.5`, `linecap: round`. Auch Heroicons- oder Lucide-kompatibel.
- **Emoji**: 🥑 als Brand-Marker (System-Font Emoji ok, oder Custom-SVG).
- **Fonts**: Google Fonts (Inter 400–800, Fraunces 400–600, opsz axis verfügbar).

## Files in this Bundle
- `Wochenplaner.html` — Canvas mit allen 3 Varianten nebeneinander
- `A-Editorial-Stack.html` — Standalone A
- `B-Story-Rail.html` — Standalone B
- `C-Dark-Green.html` — Standalone C (User-Favorit)
- `v1-editorial.jsx` / `v1.css` — A
- `v2-story-rail.jsx` / `v2.css` — B
- `v3-cinematic.jsx` / `v3.css` — C
- `styles.css` — geteilte Tokens + iPhone-Frame
- `design-canvas.jsx` — Canvas-Wrapper (nicht für App-Build, nur fürs Vergleichen)
- `feed-screenshots/` — Original-Brand-Referenzen (TikTok-Feed)

## Empfehlung an den umsetzenden Entwickler
1. Starte mit den Design-Tokens als CSS-Variables / Theme-Object
2. Baue die `MealCard`, `EmptyMealSlot`, `DayHeader`, `MacroBlock`, `ActionPill`, `Fab` als wiederverwendbare Komponenten
3. Variante C ist priorisiert — der dunkelgrüne Look passt zur Brand-DNA. A/B können als Theme-Optionen folgen.
4. Achte auf die Typo-Hierarchie: Fraunces nur für Display, Inter für alles andere
5. Frosted-Glass-Effekte (`backdrop-filter: blur`) testen pro Plattform — auf React Native braucht es `expo-blur` oder ähnliches
