# Handoff: Home Screen · Avocado App

Redesign der **Home-Seite** der Avocado-Mini-App. Neuer Aufbau: ein dominanter Hero-Button (Feed) oben, zwei quadratische Tiles darunter (Upload + Mein Bereich), und der Marktplatz als breiter Footer-Button.

---

## 1. Overview

Die Home-Seite ist der Einstiegspunkt der World-Mini-App **„Avocado"**. Nach Logo/Branding sehen Nutzer:innen drei klar hierarchisierte Aktionen:

1. **Feed** (Hero, volle Breite) — Community, Trends, neue Rezepte. Visuell der dominierende Touchpoint.
2. **Hochladen** + **Mein Bereich** (zwei gleich große Squares nebeneinander) — Sekundäre Aktionen.
3. **Marktplatz** (volle Breite) — Pläne kaufen & verkaufen.

Darunter Login-CTA und optionaler Debug-Block (nur in Dev-Builds).

## 2. About the Design Files

Die Dateien in diesem Bundle sind **Design-Referenzen in HTML + React (UMD + Babel-standalone)** — Prototypen, die das beabsichtigte Aussehen und Verhalten dokumentieren. **Sie sind nicht für 1:1-Übernahme gedacht.**

Aufgabe: Diese Designs in der **bestehenden Avocado-Codebase** mit den dort etablierten Patterns, Komponenten, Theme- und Icon-Systemen nachbauen. Inline-Styles sind als visuelle Spec zu lesen, nicht als finale Implementation — bitte ins Theme-System (Tailwind / CSS-Variablen / styled-components — je nach Stack) übersetzen.

## 3. Fidelity

**High-fidelity.** Alle Farben, Verläufe, Typografie, Abstände, Radii und Shadows sind final. Pixelgenaue Umsetzung wird erwartet.

---

## 4. Layout

```
┌────────────────────────────────────┐
│                              [DE ▾]│  TopBar (Lang-Switcher)
│                                    │
│             ╭─────╮                │
│             │ 🥑  │                │  Logo (88×88 white circle)
│             ╰─────╯                │
│            Avocado                 │  Fraunces 30/700
│       ─── MINI APP ───             │  Caps eyebrow 11/700
│                                    │
│  ╔══════════════════════════════╗  │
│  ║ [● LIVE]              ✨ ✨   ║  │  ◆ HERO: Feed
│  ║                              ║  │    Avocado→Forest-Verlauf
│  ║  ╭───╮  Feed              › ║  │    Goldglow + Sparkles
│  ║  │✨ │  Community, Trends   ║  │    62×62 Glass-Icon, Fraunces 30
│  ║  ╰───╯  & neue Rezepte      ║  │
│  ╚══════════════════════════════╝  │
│                                    │
│  ╭──────────────╮ ╭──────────────╮ │  2× Square-Tiles (1:1)
│  │ 👆       [Orb]│ │ 👤           │ │  Hochladen │ Mein Bereich
│  │              │ │              │ │  Avocado    Dark-Forest
│  │ Hochladen    │ │ Mein Bereich │ │
│  │ Werde Creator│ │ Pläne & List.│ │
│  ╰──────────────╯ ╰──────────────╯ │
│                                    │
│  ╭──────────────────────────────╮  │
│  │ 🏪  Marktplatz               │  │  Marktplatz (volle Breite)
│  │     Diätpläne kaufen & verk. │  │  Gold-Verlauf
│  ╰──────────────────────────────╯  │
│                                    │
│      ╭──────────────────╮          │
│      │ ⇥  Jetzt einloggen│         │  Login-Pill (dark)
│      ╰──────────────────╯          │
│                                    │
│  ┌─ Debug-Modus ──────────────┐   │  Dev-only, hinter Flag
│  │ 0x2da3…ff48                │   │
│  │ [ Test-Hash A ]            │   │
│  │ [ Test-Hash B ]            │   │
│  │ [ Debug abmelden ]         │   │
│  └────────────────────────────┘   │
│                                    │
│         v2.4.1 · build 2026.05.14  │  Footer-Hash
└────────────────────────────────────┘
```

Viewport: **390 px** (iPhone-Mini-App-Frame in World App). Außen-Padding 14 px, Card-Gap 12 px.

---

## 5. Komponenten-Spec

### 5.1 TopBar (Lang-Switcher)

- Rechts ausgerichtet, Padding `12px 18px 0`
- Pill: white bg, border `1px solid rgba(20,57,31,0.08)`, border-radius 999px, padding `7px 12px`
- Font: Inter 11px/700, color `#14391f`, letter-spacing 0.08em
- Inhalt: „DE ▾" (chevron-down 12px, color `#14391f`)
- Shadow: `0 2px 8px rgba(20,57,31,0.05)`
- Verhalten: Klick öffnet Locale-Picker-Sheet (DE/EN min.)

### 5.2 LogoBlock

- Padding-top 16px, text-align center
- **Circle**: 88×88, border-radius 50%, white bg, inline-flex center
  - Shadow: `0 8px 24px rgba(20,57,31,0.10), inset 0 0 0 1px rgba(20,57,31,0.04)`
  - Inhalt: Avocado-SVG-Mark (48×56)
    - Outer skin: `linear-gradient #5fa052 → #3f7536`
    - Flesh: `linear-gradient #f5e8b8 → #d9c98a`
    - Pit: circle r=7 fill `#7a4a26`, highlight r=2 fill `#9a6234` opacity 0.6
- **Title** „Avocado": Fraunces 30/700, letter-spacing −0.025em, color `#14391f`, line-height 1.05, margin-top 14px
- **Eyebrow** „MINI APP": Inter 11/700, letter-spacing 0.24em, uppercase, color `#9aa295`, margin-top 8px, mit zwei 16×1px-Strichen rechts und links

### 5.3 Hero Card (Feed) — **NEU, Hauptkomponente**

Der visuell dominante Button der Seite. Sticht klar aus den anderen Cards heraus.

| Eigenschaft | Wert |
|---|---|
| Width | 100% |
| Padding | `24px 22px` |
| Border-radius | `26px` |
| Background | `linear-gradient(135deg, #5fa052 0%, #2f6a3a 55%, #14391f 100%)` |
| Box-shadow | `0 16px 36px rgba(20,57,31,0.30)` |
| Color | white |
| Position | relative, overflow hidden |

**Layer 1 · Goldglow (decorativ, top-right)**
- Position: absolute, top −60, right −40
- 220×220 circle
- Background: `radial-gradient(circle, rgba(245,233,160,0.32) 0%, rgba(245,233,160,0) 70%)`
- pointer-events none

**Layer 2 · Sparkle-Scatter (decorativ, bottom-right)**
- SVG 120×120, position absolute right 10, bottom −10, opacity 0.55
- 3× 4-Punkt-Sterne in verschiedenen Größen + 3 kleine Kreise
- Fill `rgba(255,255,255,0.85)`, gestaffelte Opacities (1.0 / 0.7 / 0.5)
- Genaue Pfade siehe `home.jsx` → `HHeroCard`

**Layer 3 · LIVE-Pill (top-left, z-index 1)**
- Inline-flex, gap 6, padding `4px 10px`
- Background: `rgba(255,255,255,0.16)`, backdrop-filter `blur(6px)`
- Color white, font 10px/700, letter-spacing 0.12em, uppercase
- Inhalt: 6×6 Dot in `#f5e9a0` mit Glow `box-shadow: 0 0 8px #f5e9a0` + Text „Live"

**Layer 4 · Content-Row (z-index 1)**
- Flex align-end, gap 16, margin-top 14
- Icon-Tile: 62×62, border-radius 18, bg `rgba(255,255,255,0.18)`, border `1px solid rgba(255,255,255,0.22)`, backdrop-filter `blur(8px)`. Inhalt: Sparkles-Icon 30px, color `#f5e9a0` (warmes Gold)
- Text-Block:
  - Titel „Feed": Fraunces 30/700, letter-spacing −0.025em, line-height 1
  - Subtitle „Community, Trends & neue Rezepte": Inter 13/500, opacity 0.88, line-height 1.3, margin-top 6
- Chevron-right: 20px, color `rgba(255,255,255,0.85)`

**Verhalten**
- Klick → Route `/feed`
- Hover (Desktop): box-shadow vertieft sich auf `0 20px 44px rgba(20,57,31,0.36)` (sanfte 200ms ease)
- Active/pressed: scale(0.985)

### 5.4 Square Cards (Hochladen + Mein Bereich)

Zwei gleich große Tiles in einem 2-Spalten-Grid, `gap: 12px`.

| Eigenschaft | Wert |
|---|---|
| Layout | `display: grid; grid-template-columns: 1fr 1fr; gap: 12px` |
| Tile-Größe | `aspect-ratio: 1 / 1`, width 100% |
| Padding | 18px |
| Border-radius | 24px |
| Struktur | flex column, justify-between (Icon oben, Text unten) |

**Tile-Anatomie**
- **Icon-Square** (oben): 48×48, border-radius 14, bg `palette.iconBg`, color `palette.iconFg`. Icon 22px.
- **Text-Block** (unten):
  - Titel: Fraunces 20/700, letter-spacing −0.02em, line-height 1.05
  - Subtitle: Inter 12/500, opacity 0.85, line-height 1.25, margin-top 4
- **Decor-Icon** (optional, decorativ): position absolute right −22, bottom −22, opacity 0.16, size 140px
- **Badge** (optional, top-right): pill `rgba(255,255,255,0.92)` bg, color `#14391f`, padding `3px 8px 3px 7px`, border-radius 999, font 9px/700, letter-spacing 0.04em. Inhalt: `shieldCheck` 10px in `#5fa052` + Text

**Palette · Hochladen** (Avocado-Variante)
```
bg:      linear-gradient(135deg, #5fa052 0%, #3f7536 100%)
fg:      white
iconBg:  rgba(255,255,255,0.16)
iconFg:  white
shadow:  0 12px 28px rgba(63,117,54,0.32)
badge:   "Orb"     (mit shield-check)
decor:   shieldCheck
icon:    finger
title:   "Hochladen"
sub:     "Werde Creator"
```

**Palette · Mein Bereich** (Dark-Forest-Variante)
```
bg:      linear-gradient(135deg, #2f6a3a 0%, #1a3a23 100%)
fg:      white
iconBg:  rgba(255,255,255,0.16)
iconFg:  white
shadow:  0 12px 28px rgba(20,57,31,0.22)
decor:   idCard
icon:    user
title:   "Mein Bereich"
sub:     "Pläne & Listen"
```

Beide Squares haben **gleiche Größe, Radius, Padding und Typo** — nur Farbe + Inhalt unterscheiden sich. Visuell als Paar lesbar.

### 5.5 Marketplace Card (volle Breite, unten)

Standard `HActionCard` mit Gold-Palette.

| Eigenschaft | Wert |
|---|---|
| Background | `linear-gradient(135deg, #f5b942 0%, #d99a3c 100%)` |
| Foreground | `#3a2510` (warmes Dunkelbraun) |
| Icon-Bg | `rgba(255,255,255,0.7)` |
| Icon-Fg | `#7a4a18` |
| Shadow | `0 10px 24px rgba(217,154,60,0.32)` |
| Decor-Icon | `shop` (150px, opacity 0.18, rechts) |
| Icon | `shop` |
| Title | „Marktplatz" — Fraunces 22/700 |
| Subtitle | „Diätpläne kaufen & verkaufen" — Inter 13/500 |
| Padding | `18px` |
| Border-radius | `24px` |

### 5.6 Login CTA

- Position: zentriert, margin-top 24
- Pill: bg `linear-gradient(135deg, #1a3a23, #0e2415)`, border-radius 999
- Padding `12px 20px`, font Inter 14/700, color white
- Shadow `0 8px 22px rgba(20,57,31,0.28)`
- Inhalt: `login`-Icon 15px + Text „Jetzt einloggen"
- Klick: World MiniKit `walletAuth()`

### 5.7 Debug-Block (nur in Dev-Build)

- Sichtbar nur wenn `showDebug === true` (in Production: `process.env.NODE_ENV !== 'production'`)
- Container: margin `24px 14px 0`, padding `18px 14px`, bg `#faf6ec`, border `1px dashed rgba(20,57,31,0.18)`, border-radius 18
- Header: caps „DEBUG-MODUS" (Inter 10/700, color `#9aa295`, letter-spacing 0.18em)
- Hash-Display: monospace 10px, color `#c44e4e`, weiße Inner-Box, kurz mit `…`-Truncation
- 2× Login-Buttons (Test-Hash A/B): white bg, forest-green text, border `1px solid rgba(20,57,31,0.12)`, pill 999, mit Icon-Vorspann
- 1× Logout-Button: `rgba(196,78,78,0.06)` bg, `#c44e4e` text, `rgba(196,78,78,0.3)` border

### 5.8 Footer-Hash

- text-align center, font monospace 10px, color `#c8cec5`, margin-top 18
- Format: `v{version} · build {build}` — z. B. `v2.4.1 · build 2026.05.14`

---

## 6. Design Tokens

### Colors

```css
:root {
  /* Backgrounds */
  --bg-screen:     #ffffff;   /* Home-Page bg (anders als Marketplace cream!) */
  --card-soft:     #faf6ec;   /* Debug-Block, sub-cards */

  /* Brand */
  --ink-deep:      #14391f;
  --ink-deep-2:    #0f2c18;
  --ink-mid:       #2a4a32;
  --ink-text:      #1a2e20;
  --ink-mute:      #6b7868;
  --ink-soft:      #9aa295;
  --ink-line:      rgba(20,57,31,0.12);

  /* Accents */
  --avocado:       #5fa052;
  --avocado-dark:  #3f7536;
  --gold-warm:     #f5e9a0;   /* Hero-Glow + Hero-Icon */
  --gold:          #f5b942;
  --gold-dark:     #d99a3c;
  --crimson:       #c44e4e;   /* Debug logout */
}
```

### Typografie

- **Sans**: `'Inter', system-ui, -apple-system, sans-serif` (UI, Body)
- **Serif**: `'Fraunces', Georgia, serif` (Display, Titel)

| Use | Family | Size | Weight | LS |
|---|---|---|---|---|
| Logo-Title „Avocado" | Fraunces | 30 | 700 | −0.025em |
| Hero-Title „Feed" | Fraunces | 30 | 700 | −0.025em |
| Square-Title | Fraunces | 20 | 700 | −0.02em |
| Action-Card-Title | Fraunces | 22 | 700 | −0.02em |
| Subtitle | Inter | 12–13 | 500 | — |
| Mini-App eyebrow | Inter | 11 | 700 | 0.24em uppercase |
| LIVE-Pill | Inter | 10 | 700 | 0.12em uppercase |
| Lang-Switcher | Inter | 11 | 700 | 0.08em |
| Debug-Header | Inter | 10 | 700 | 0.18em uppercase |
| Footer-Hash | Mono | 10 | 400 | — |

### Spacing

- Page horizontal padding: 14px
- Card-Gap: 12px
- Section-Gap (Logo→Cards): 24px
- Login-CTA-Gap: 24px

### Border-Radii

| Element | Wert |
|---|---|
| Hero Card | 26px |
| Square Card | 24px |
| Action Card (Markt) | 24px |
| Icon-Tile (Hero) | 18px |
| Icon-Tile (Squares) | 14px |
| Pills (LIVE, Badge, Login, Lang) | 999px |
| Debug-Container | 18px |

### Shadows

```css
--shadow-hero:    0 16px 36px rgba(20,57,31,0.30);
--shadow-up:      0 12px 28px rgba(63,117,54,0.32);   /* Hochladen-Square */
--shadow-area:    0 12px 28px rgba(20,57,31,0.22);    /* Mein-Bereich-Square */
--shadow-market:  0 10px 24px rgba(217,154,60,0.32);  /* Marktplatz */
--shadow-login:   0 8px 22px rgba(20,57,31,0.28);
--shadow-logo:    0 8px 24px rgba(20,57,31,0.10);
--shadow-pill:    0 2px 8px rgba(20,57,31,0.05);
```

---

## 7. Icons

Im Prototyp Inline-SVG-Set (stroke-based, `strokeWidth: 2`, `strokeLinecap: 'round'`, `strokeLinejoin: 'round'`). Verwendete Icons:

| Name | Verwendung | Lucide-Äquivalent |
|---|---|---|
| `sparkles` | Hero-Icon (Feed) | `Sparkles` |
| `finger` | Hochladen-Square | `Fingerprint` |
| `user` | Mein-Bereich-Square | `User` |
| `idCard` | Decor in Mein Bereich | `IdCard` |
| `shop` | Marktplatz + Decor | `Store` |
| `shieldCheck` | Orb-Badge + Decor | `ShieldCheck` |
| `login` | Login-Pill | `LogIn` |
| `logout` | Debug-Logout | `LogOut` |
| `orb` | Debug Test-Hash A | `Globe` |
| `chev` | Lang-Switcher | `ChevronDown` |
| `chevR` | Hero-Chevron | `ChevronRight` |

In Production durch `lucide-react` o.ä. ersetzen.

---

## 8. Interactions & Behavior

| Element | Aktion |
|---|---|
| Lang-Switcher „DE ▾" | Öffnet Locale-Picker (Bottom-Sheet) |
| Hero Card „Feed" | `/feed` — Community-/Trending-Feed |
| Square „Hochladen" | `/upload` — gated auf Orb-Verified (sonst Verify-Flow zuerst) |
| Square „Mein Bereich" | `/profil` |
| Card „Marktplatz" | `/marktplatz` |
| Login-Pill | World MiniKit `walletAuth()` |
| Test-Hash A/B | Mock-Login (nur in Dev-Build) |
| Debug-Logout | Clear Auth-State |

### Animationen

- Card-Hover (Desktop): shadow-deepen 200ms ease-out
- Card-Press: `transform: scale(0.985)` 100ms
- LIVE-Dot: optional Pulse-Animation `0 0 0 0 → 0 0 0 6px transparent` 1.4s infinite
- Page-Enter: stagger fade-in (Hero 0ms, Squares 80ms, Markt 160ms) — `opacity 0→1, translateY 8→0`, 320ms ease-out

---

## 9. State

```ts
type AuthState = 'guest' | 'authed' | 'verifying';

interface HomeProps {
  showDebug?: boolean;       // default: NODE_ENV !== 'production'
  authState: AuthState;
  locale: 'de' | 'en';
  feedHasNew?: boolean;      // wenn true: LIVE-Pill pulst stärker
}
```

`HScreen` ist sonst stateless — alle dynamischen Daten kommen aus globalem Auth- / Locale-Context.

---

## 10. Accessibility

- **Hero Card**: `aria-label="Feed öffnen — Community, Trends und neue Rezepte"`
- **Squares**: `aria-label="Rezept hochladen"` / `"Mein Bereich öffnen"`
- **Marktplatz**: `aria-label="Marktplatz öffnen — Diätpläne kaufen und verkaufen"`
- **Kontrast**: white auf `#14391f` (Hero Mitte) = 12.8:1 (AAA). Gold-Icon `#f5e9a0` auf `#2f6a3a` = 7.4:1 (AAA). Geprüft.
- **Touch-Targets**: Hero ~110px hoch, Squares ~175×175, Markt ~92px hoch — alle weit über 44px-Minimum
- **Reduced Motion**: respektiere `prefers-reduced-motion` — disable Pulse + Stagger-Fade
- **Lang-Attribut**: `<html lang="de">` per default, Switch via State

---

## 11. Responsive

- **Primär 390 px** (iPhone-Mini-App in World App)
- Auf größeren Screens: Container max-width 480px, zentriert
- Squares behalten 1:1 — Grid bleibt `1fr 1fr` bis ≥ 320px

---

## 12. Files in this Bundle

```
design_handoff_home_redesign/
├── README.md              ← diese Datei
├── Home.html              ← Lauffähiger Prototyp (Design-Canvas mit 2 Artboards)
├── home.jsx               ← Komponenten:
│                            • HScreen (Root)
│                            • HHeroCard (Feed-Hero) ← NEU
│                            • HSquareCard (Upload/Profile) ← NEU
│                            • HActionCard (Marktplatz)
│                            • HLogoBlock, HTopBar, HDebugBlock
│                            • HIcon (SVG-Set)
├── styles.css             ← Globale Tokens (Forest/Cream/Avocado)
├── design-canvas.jsx      ← Layout-Helper (NICHT für Production)
└── shared/
    └── ios-frame.jsx      ← Phone-Frame (NICHT für Production)
```

**Implementieren musst du nur:** `HScreen`, `HHeroCard`, `HSquareCard`, `HActionCard`, `HLogoBlock`, `HTopBar`, `HDebugBlock` und das Icon-Mapping. Alles andere ist Authoring-Hilfe für den Prototyp.

---

## 13. Implementierungs-Hinweise

1. **Tokens zuerst** in Tailwind-Config oder CSS-Variablen anlegen — sind App-weit wiederverwendbar (Marktplatz und Profile teilen sich diese).
2. **Hero-Card als eigene Komponente** — sie wird vermutlich auch auf anderen Screens als Top-CTA wiederverwendet. Props: `icon`, `title`, `subtitle`, `eyebrow?`, `onClick`. Glow + Sparkles als interne SVG-Decor.
3. **Square-Grid**: `grid-template-columns: 1fr 1fr; gap: 12px` — nicht flex. Mit flex sind aspect-ratios fragiler.
4. **Icons** auf `lucide-react` mappen (Tabelle §7) und ein generisches `<Icon name size color>` bauen.
5. **Decor-Sparkles im Hero**: als statisches inline-SVG einbetten — nicht parametrisieren, das sind reine Decorations.
6. **`prefers-reduced-motion`** ehren: Stagger-Entry + LIVE-Pulse hinter Media-Query.
7. **Gold `#f5e9a0`** ist ein neues Token (nur für Hero) — bitte als `--gold-warm` ergänzen.
8. **„Avocado"-Title** ersetzt das alte „Delikatessen Drehbuch" — bitte auch in App-Titel / Manifest / PWA-Metadata updaten falls vorhanden.

---

## 14. Changelog

- **Mein Wochenplan-Card entfernt** (zuvor Position 1)
- **Entdecken → Feed** umbenannt + zum Hero hochgestuft (neue `HHeroCard`-Komponente, Avocado→Forest-Verlauf, Goldglow, Sparkle-Decor, LIVE-Pill)
- **Hochladen + Mein Bereich** sind jetzt **gleich gestaltete Squares** in einem 2-Spalten-Grid (vorher: Hochladen war volle Breite, Mein Bereich ein cream List-Item)
- **Marktplatz** unverändert, Position vom 3. zum letzten der drei Hauptkarten
- **Title „Delikatessen Drehbuch" → „Avocado"** (einzeilig)

---

## 15. Offene Punkte

- [ ] LIVE-Pill-Logik: an welcher Bedingung pulst sie stärker / wird sie zu „● 12 neu"-Counter?
- [ ] Empty/Loading-State für Feed (falls Hero-Card dynamische Preview-Inhalte zeigt — z. B. „Heute trending: …")
- [ ] Dark Mode (Tokens vorbereitet, nicht ausgearbeitet)
- [ ] Analytics-Events: welche Klicks tracken wir? (vorgeschlagen: `home_hero_feed_click`, `home_square_upload_click`, `home_square_profile_click`, `home_marketplace_click`)
- [ ] Hero-Card-Wiederverwendung: wo sonst eingesetzt? → `HHeroCard` in Shared-Layer
