# Handoff: Marketplace Card Redesign (Variante A · Magazine Hero)

## Overview
Redesign der **Marketplace-Seite** in der Avocado-App. Auf dem Marktplatz werden **Ernährungspläne**, **Diätpläne** und **Sport-/Workout-Pläne** verkauft. Die alten Karten zeigten nur ein Hero-Bild + Preis. Die neuen Karten zeigen den **gesamten Plan-Inhalt**: alle enthaltenen Gerichte/Workouts in einem Karussell, ausklappbare Makro-Nährwerte, klare Stats (Rating, Verkäufe, kcal/Tag), WLD/USDC-Toggle und einen prominenten Kauf-Button.

## About the Design Files
Die Dateien in diesem Bundle sind **Design-Referenzen, erstellt in HTML/React** — Prototypen, die das beabsichtigte Aussehen und Verhalten zeigen. **Sie sind nicht für 1:1-Übernahme gedacht.** Aufgabe: Diese Designs im **bestehenden Codebase** der Avocado-App (vermutlich React/Next.js + Tailwind oder ähnlich) mit den dort etablierten Patterns, Komponenten und Libraries nachbauen. Die Inline-Styles im Prototyp sind als visuelle Spec gedacht, nicht als finale Implementierung — bitte in das Theme-System der App übersetzen.

## Fidelity
**High-fidelity.** Alle Farben, Typografie, Abstände, Border-Radii, Shadows und Interaktionen sind final spezifiziert. Pixelgenaue Umsetzung wird erwartet. Die exakten Hex-Werte und Maße findest du im Abschnitt "Design Tokens".

## Scope
Nur die **Marketplace-Karte** (Variante A · "Magazine Hero"). Die Karte funktioniert für alle drei Plan-Typen:
1. **Ernährungs-/Diätplan** (z.B. "High-Protein Cut") — zeigt Gerichte-Karussell + Makros-Panel
2. **Workout-/Sportplan** (z.B. "Push Pull Legs · 6 Wochen") — zeigt Workout-Karussell, **kein** Makros-Panel
3. **Kombi-Plan** (Ernährung + Workout, z.B. "Keto Kombi · Train & Eat") — zeigt Gerichte-Karussell + Makros + Workout-Anzahl in Stats

## Layout (von oben nach unten)

```
┌──────────────────────────────────────────────┐
│  HERO (4:3 Ratio)                            │  ← aktuelles Karussell-Bild als Cover
│  [Premium Badge]              [♡ Like]       │
│                                              │
│  [Tag1] [Tag2] [Tag3]                        │  ← Glass-Tags am unteren Rand
│  Plan-Titel (Fraunces 22px)                  │
│  🥑 Creator · Dauer                          │
├──────────────────────────────────────────────┤
│  STATS-STRIP (4 Spalten, beige bg #faf6ec)   │
│  Rezepte | Workouts* | kcal/Tag | ★ Rating   │  ← *nur wenn vorhanden
├──────────────────────────────────────────────┤
│  GERICHTE/TRAININGS IM PLAN          1/6     │
│  [Img][Img][Img][Img][Img][Img] →            │  ← horizontales Karussell, 84×84
│        ●  ─                                   │  ← Dot-Indikator
├──────────────────────────────────────────────┤
│  [▼ Makros & Nährwerte]                      │  ← collapsible, nur bei Mahlzeiten-Plan
│  └─ (expanded: dunkelgrünes Panel mit        │
│        kcal-Großzahl + 3 Makro-Bars)         │
├──────────────────────────────────────────────┤
│  [WLD ⇄ USDC]    [Vorschau]  [2.4 WLD →]    │  ← Currency-Toggle + Buy-Button
├──────────────────────────────────────────────┤
│  ●●● 87 planen jetzt              📅 21.04   │  ← Social Proof Footer (cream bg)
└──────────────────────────────────────────────┘
```

## Komponenten-Spezifikation

### 1. Card-Container
- Background: `#ffffff`
- Border-radius: `22px`
- Box-shadow: `0 8px 24px rgba(20, 57, 31, 0.08)`
- Margin-bottom: `20px`
- Overflow: hidden

### 2. Hero-Sektion
- Aspect-ratio: `4/3`
- Background: aktuelles Bild aus `dishes[carouselIdx]` als `cover`
- Overlay: `linear-gradient(180deg, rgba(0,0,0,0.45) 0%, transparent 35%, transparent 50%, rgba(0,0,0,0.7) 100%)`

**Premium-Badge** (top-left, nur wenn `plan.badge`):
- Background: `rgba(255,255,255,0.95)`
- Color: `#8a6418`
- Padding: `4px 10px`, border-radius: `999px`
- Font: 10px, weight 700, letter-spacing 0.04em, uppercase
- Star-Icon: SVG, fill `#c8a45c`, 10×10

**Like-Button** (top-right):
- 32×32, border-radius 50%, white bg (95%)
- Heart-Icon: stroke `#14391f`, strokeWidth 2

**Tags** (bottom-left, über Titel):
- 3 max anzeigen
- Background: `rgba(255,255,255,0.16)` mit `backdrop-filter: blur(8px)`
- Color: white, border `1px solid rgba(255,255,255,0.22)`
- Padding: `3px 8px`, border-radius 999px, font 10px/500

**Titel**:
- Font: `'Fraunces', serif`, 22px, weight 600
- Letter-spacing: -0.02em, line-height 1.1
- Color: white, text-shadow: `0 2px 12px rgba(0,0,0,0.3)`

**Creator-Row** (unter Titel):
- 12px, white at 95% opacity
- Format: `🥑 Creator · 14 Tage` (mit gutem Gap, separator dot opacity 0.5)

### 3. Stats-Strip
- Grid: 4 Spalten gleich breit
- Background: `#faf6ec`
- Border-bottom: `1px solid rgba(20,57,31,0.08)`
- Spalten getrennt durch `1px solid rgba(20,57,31,0.08)`
- Pro Zelle: 12px Padding vertikal, center-aligned
  - Großzahl: Fraunces 18px/600, color `#14391f`, line-height 1
  - Label: 10px/500, color `#6b7868`, uppercase, letter-spacing 0.04em, marginTop 3px

Spalten je nach Plan-Typ:
- **Mahlzeit**: Rezepte | kcal/Tag | ★ Rating | Verkauft
- **Workout**: Workouts | Dauer | ★ Rating | Verkauft
- **Kombi**: Rezepte | Workouts | kcal/Tag | ★ Rating

### 4. Karussell
- Header-Row: padding 0 16px 10px, justify-between
  - Label: 11px/600, color `#14391f`, uppercase, letter-spacing 0.06em
    - Mahlzeit: "Gerichte im Plan"
    - Workout: "Trainings im Plan"
  - Counter: 10px, color `#6b7868`, format "1 / 6"
- Track: flex, gap 8px, overflow-x auto, scroll-snap-type x mandatory, padding 0 16px
- Item:
  - Width 84px, flex-shrink 0
  - 84×84 Bild, border-radius 12px
  - Active: `border: 2px solid #14391f` + scale 1; inactive: opacity 0.7, scale 0.95
  - Transition: opacity .25s, transform .25s
  - Unter Bild:
    - Name: 10px/500, color `#1a2e20`, marginTop 5px, line-height 1.2, max 2 Zeilen (height 24px overflow hidden)
    - Sub: 9px, color `#6b7868`, marginTop 1px, format "520 kcal · 25 Min" (Workout: "4×8 · Push")
- Dots-Indikator (zentriert, paddingTop 8px):
  - Aktiv: 16×5px, color `#14391f`
  - Inaktiv: 5×5px, color `rgba(20,57,31,0.2)`
  - Border-radius 999px, transition all .25s

### 5. Makro-Panel (nur bei Plänen mit `macros`)
**Toggle-Button**:
- Width 100%, padding 10px 14px, border-radius 12px
- Inaktiv: bg `#faf6ec`, color `#14391f`, border `1px solid rgba(20,57,31,0.1)`
- Aktiv: bg `#14391f`, color white
- Font 12px/600
- Linke Seite: Donut-SVG-Icon + Text "Makros & Nährwerte"
- Chevron rechts: rotiert 180° wenn open

**Expanded Panel**:
- Background: `#14391f`, border-radius 14px, padding 14px 16px, marginTop 8px
- Animation: `mp-fadeIn .25s ease`
- Top-Row:
  - Links: kleines uppercase Label "Pro Tag" (10px/rgba 0.6) + große Zahl Fraunces 28px/600 weiß + " kcal" (13px/400, opacity 0.6)
  - Rechts: Dauer + "{n} Rezepte" (white, 11px/600)
- Macro-Bars (3 stack, gap 9px):
  - Label-Zeile: 50px Label (rgba 0.7) | 1fr Bar | 36px Wert
  - Bar: height 6px, bg `rgba(255,255,255,0.12)`, fill mit Farbe
    - Protein: `#ff7849` (max 250g)
    - Fett: `#f5b942` (max 250g)
    - KH: `#5fa052` (max 250g)
  - Wert: white/600, format "{n}g"

### 6. Footer (Preis + Buy)
- Padding 16px, flex align-center gap 10px

**Currency-Toggle** (links):
- Pill: bg `rgba(20,57,31,0.06)`, border `1px solid rgba(20,57,31,0.1)`, border-radius 999px, padding 3px 4px
- 2 Inner-Pills (WLD / USDC): padding 3px 8px, border-radius 999px, font 10px/600
  - Aktiv: bg `#14391f`, color white
  - Inaktiv: transparent, color `#14391f`
- Click toggelt zwischen WLD/USDC

**Vorschau-Button**:
- White bg, border `1px solid rgba(20,57,31,0.15)`, border-radius 12px
- Padding 11px 16px, font 13px/600, color `#14391f`

**Buy-Button** (Hauptaktion):
- Background: `linear-gradient(135deg, #ff9a3c, #ff7849)`
- Border-radius 12px, padding 11px 18px
- Font 13px/700, white
- Box-shadow: `0 4px 14px rgba(255, 120, 73, 0.35)`
- Inhalt: "{price} {currency} →"
- Pfeil-Icon rechts: SVG, white, strokeWidth 2.5

### 7. Social-Proof Footer
- Border-top `1px solid rgba(20,57,31,0.08)`
- Padding 10px 16px, bg `#faf6ec`
- Flex justify-between, font 11px, color `#6b7868`
- Links: 3 überlappende 18px-Avatare (Coral/Avocado/Gold) + "**87** planen jetzt" (87 in `#14391f`/bold)
- Rechts: Date "📅 21.04.26"

## Interactions & Behavior

| Aktion | Verhalten |
|---|---|
| Klick auf Hero | Öffnet Plan-Detailseite |
| Klick auf Like-Heart | Toggle favorite, Heart füllt sich coral |
| Karussell scrollen | scroll-snap, dotIndikator + counter updaten |
| Klick auf Karussell-Item | scrollt zu Item + setzt active (border `#14391f`) + ändert Hero-Bild |
| Klick "Makros & Nährwerte" | Expand/Collapse mit `.25s` fade-in animation |
| Klick WLD/USDC Toggle | Schaltet Currency um, Preis updated live |
| Klick "Vorschau" | Öffnet Plan-Preview-Modal |
| Klick Buy-Button | Triggert World Wallet Kauf-Flow |

## State Management

```typescript
// Pro Card-Instanz:
const [currency, setCurrency] = useState<'WLD' | 'USDC'>('WLD');
const [carouselIdx, setCarouselIdx] = useState(0);
const [showMacros, setShowMacros] = useState(false);
const [isFavorite, setIsFavorite] = useState(false);

// Karussell-Scroll detection (debounced):
const handleScroll = () => {
  const itemWidth = 92; // 84px width + 8px gap
  setCarouselIdx(Math.round(trackRef.current.scrollLeft / itemWidth));
};
```

## Data Shape

```typescript
type Plan = {
  id: string;
  type: 'meal' | 'workout' | 'combo';
  title: string;
  creator: string;
  creatorAvatar: string; // emoji or img URL
  badge: 'Premium' | null;
  duration: string;        // "14 Tage", "6 Wochen"
  recipes: number | null;  // null bei reinem Workout-Plan
  workouts: number | null; // null bei reinem Mahlzeiten-Plan
  kcalDay: number | null;  // null bei reinem Workout-Plan
  rating: number;          // 1-5
  sold: number;
  planning: number;        // wieviele planen aktuell
  priceWLD: number;
  priceUSDC: number;
  tags: string[];          // max 3 angezeigt
  macros: {
    protein: number;       // g
    carbs: number;         // g
    fat: number;           // g
  } | null;
  dishes: Array<{
    name: string;
    img: string;
    kcal: number | string; // Workout: "4×8" string
    time: string;          // "25 Min" oder "Push"/"Pull"/"Legs"
  }>;
};
```

## Design Tokens

### Colors
```css
/* Backgrounds */
--bg-cream: #f5efe1;       /* Page background */
--bg-cream-2: #ede5d2;
--card: #ffffff;
--card-soft: #faf6ec;      /* Stats-Strip & Footer */

/* Brand */
--ink-deep: #14391f;       /* Hauptfarbe (dunkles Avocado-Grün) */
--ink-deep-2: #0f2c18;
--ink-text: #1a2e20;
--ink-mute: #6b7868;
--ink-line: rgba(20, 57, 31, 0.12);

/* Accents */
--avocado: #5fa052;        /* KH/Carbs, Verified-Check */
--coral: #ff7849;          /* Protein, Buy-Button-Ende */
--orange: #ff9a3c;         /* Buy-Button-Anfang */
--gold: #f5b942;           /* Fett, Star-Rating */
--premium: #c8a45c;        /* Premium-Badge */
```

### Typography
- Sans: `'Inter', system-ui, sans-serif` (Body, UI)
- Serif: `'Fraunces', 'Playfair Display', Georgia, serif` (Titel, Stats-Großzahlen)

| Use | Family | Size | Weight |
|---|---|---|---|
| Plan-Titel (Hero) | Fraunces | 22px | 600 |
| Stats-Großzahl | Fraunces | 18px | 600 |
| Makro-Großzahl | Fraunces | 28px | 600 |
| Section-Label | Inter | 11px | 600 (uppercase, ls 0.06em) |
| Body | Inter | 12-13px | 500-600 |
| Small/Caption | Inter | 9-10px | 500 |

### Spacing
Cards padding: 16px innen
Gap zwischen Karussell-Items: 8px
Margin zwischen Karten: 20px

### Radii
- Card: 22px
- Inner-Panel (Makros): 14px
- Buttons: 12px
- Image-Tile (Karussell): 12px
- Pills/Tags: 999px (full)

### Shadows
- Card: `0 8px 24px rgba(20, 57, 31, 0.08)`
- Buy-Button: `0 4px 14px rgba(255, 120, 73, 0.35)`

## Assets
- **Bilder**: Im Prototyp Unsplash-Platzhalter — durch echte Plan-Bilder vom Backend ersetzen
- **Icons**: Inline-SVG (siehe Code) — können in der App durch das vorhandene Icon-System ersetzt werden (z.B. `lucide-react`: Heart, Clock, Star, Check, ChevronDown, ArrowRight)
- **Fonts**: Fraunces + Inter via Google Fonts (oder lokale Hosting falls App das vorzieht)

## Files in This Bundle
- `Marketplace.html` — Lauffähiger Prototyp (öffnen im Browser)
- `card-variant-a.jsx` — **Hauptdatei** mit `CardVariantA`-Komponente und Sample-Data (`PLANS`)
- `card-variant-b.jsx`, `card-variant-c.jsx` — alternative Varianten (zur Referenz, nicht zur Implementierung)
- `app.jsx` — Marketplace-Shell (Header, Wallet, Filter-Chips, Bottom-Nav)
- `styles.css` — Globale Tokens und Shell-Styles

## Implementation Notes for Claude Code
1. Bitte die Inline-Styles in das **bestehende Theme-System** übersetzen (Tailwind Classes / styled-components / CSS Modules — je nachdem was im Codebase existiert).
2. Die Card-Komponente soll als **wiederverwendbare** Komponente gebaut werden, die alle drei Plan-Typen handled (`meal` / `workout` / `combo`) — Logic via Conditional Rendering (siehe `card-variant-a.jsx` als Referenz).
3. Daten kommen vermutlich über bestehende API/Hooks — bitte das `Plan`-Interface ans Backend-Schema anpassen.
4. Animationen: `transition: all .25s` für Toggle, `cubic-bezier(.2,.7,.3,1)` für Karussell-Snap fühlt sich gut an.
5. Accessibility: Buy-Button braucht aria-label mit Plan-Titel + Preis. Karussell sollte mit Pfeiltasten navigierbar sein.
6. Mobile-first: alle Maße sind für 390px viewport optimiert. Auf größeren Screens kann die Karte gecappt werden (max-width: 420px).
