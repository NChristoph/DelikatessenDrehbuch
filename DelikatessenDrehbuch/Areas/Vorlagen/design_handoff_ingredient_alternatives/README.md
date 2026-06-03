# Handoff: Zutaten-Alternativen Pop-up (Avocado-Stil)

## Overview
This is the **"Alternativen für …"** modal that opens when a user taps the **swap** button on an ingredient in the Recipe Detail screen. It shows the original ingredient (with macros) and an AI-generated list of substitute ingredients. Each substitute shows a match score, macro deltas vs. the original, a short description, a preparation hint, pros/cons, a taste note, and two actions (use this alternative / add to batch).

The design is a **restyle** of an older version of this pop-up (purple gradient header, loud solid macro pills, blue CTAs) — re-skinned to match the app's **Avocado** design system (cream background, forest-green ink, Fraunces serif numerals, soft white cards).

## About the Design Files
The files in this bundle are **design references created in HTML/React (Babel-in-browser JSX)** — prototypes that show the intended look and behavior. They are **not** production code to copy directly. The task is to **recreate these designs in the target codebase's existing environment** (the app appears to use server-rendered `.cshtml` + vanilla JS in places, and React-style components elsewhere) using its established patterns, component library, and design tokens. If no shared component environment exists yet, pick the most appropriate one for the project.

The JSX uses inline `style` objects only so the visual values are explicit and easy to read off — translate them into the codebase's styling approach (CSS modules, utility classes, styled-components, etc.).

## Fidelity
**High-fidelity (hifi).** Final colors, typography, spacing, radii, and shadows are all specified below and in the source files. Recreate pixel-perfectly, then swap inline styles for the codebase's tokens/components.

## Screen / View

### Modal: "Alternativen für {ingredient}"
- **Purpose**: Let the user review AI-suggested ingredient substitutes and either replace the original or queue the substitute for a batch.
- **Presentation**: Bottom-sheet that slides up over the Recipe Detail screen. Sheet height **92%** of the viewport, top corners rounded **28px**, background cream `#f5efe1`, drop shadow `0 -20px 60px rgba(10,28,17,0.30)`. Behind it a backdrop `rgba(10,28,17,0.42)` with `backdrop-filter: blur(3px)`. Tapping the backdrop, the close (×) button, or "Abbrechen" dismisses it.

The sheet has three regions: **Header** (fixed) · **Scroll body** · **Footer** (fixed).

---

#### 1. Header (fixed)
- Background: `linear-gradient(135deg, #1f4329, #0e2415)` (forest green — replaces the old purple).
- Padding `16px 18px 18px`, white text.
- **Drag handle**: 38×4px, radius 2, `rgba(255,255,255,0.30)`, centered, 14px below top.
- **Eyebrow**: "Alternativen für" — Inter 10.5px / 700, uppercase, letter-spacing `0.14em`, `rgba(255,255,255,0.62)`.
- **Title**: ingredient name (e.g. "Carrot") — Fraunces 26px / 600, letter-spacing `-0.02em`, line-height 1.05, white.
- **AI meta row** (10px below title, gap 7px):
  - Engine/model chip: pill, bg `rgba(255,255,255,0.12)`, border `1px solid rgba(255,255,255,0.16)`, padding `4px 10px`, Inter 11px / 600, `rgba(255,255,255,0.92)`, `white-space: nowrap`. Leading sparkle icon in gold `#f5b942`. Text: `{engine} · {model}` (e.g. "ChatGPT · GPT-4o-mini").
  - Latency chip: clock icon + `{ms/1000}s` (e.g. "9.8s"), Inter 11px / 600, `rgba(255,255,255,0.55)`.
- **Close button** (top-right): 34×34 circle, bg `rgba(255,255,255,0.14)`, border `1px solid rgba(255,255,255,0.18)`, white × icon.

#### 2. Scroll body
Vertical stack, gap 12px, padding `14px 16px 100px`, scrollable (hidden scrollbar). Contains the Original card followed by one card per alternative.

**Original card**
- bg `#faf6ec`, border `1px solid rgba(20,57,31,0.07)`, radius 18, padding `14px 16px`.
- Eyebrow "ORIGINAL": Inter 10.5px / 700, uppercase, tracking `0.16em`, `#9aa295`.
- Name (Fraunces 21px / 600, `#14391f`, tracking `-0.02em`) + amount (Inter 13px / 500, `#6b7868`), baseline-aligned, gap 7px.
- Macro pills row (see **Macro pills** below): kcal · P · C · F.

**Alternative card** (one per substitute)
- bg white, radius 22, padding `16px 16px 16px 18px`, shadow `0 2px 10px rgba(20,57,31,0.06)`, `overflow: hidden`.
- **Avocado accent strip**: absolutely positioned left edge, 3px wide, inset 14px top/bottom, radius 3, `#5fa052`.
- **Top row** (space-between, align flex-start, gap 12):
  - Left: name (Fraunces 20px / 600, `#14391f`, tracking `-0.02em`, line-height 1.12) over amount (Fraunces 14px / 500, `#6b7868`, 3px below).
  - Right (column, align flex-end, gap 7):
    - **Match badge**: pill, bg `#5fa052`, white text, padding `5px 11px 5px 9px`, Inter 11.5px / 700, shadow `0 2px 8px rgba(95,160,82,0.30)`, leading check icon. Text "`{match}% Match`".
    - **Delta pills row** (wrap, gap 5, justify flex-end, max-width 150): kcal · P · C · F deltas (signed, e.g. "+4", "-1.8").
- **Description**: Inter 14px / line-height 1.5, `#2a4a32`, 12px below top row, `text-wrap: pretty`.
- **Zubereitung box**: bg `rgba(95,160,82,0.10)`, border `1px solid rgba(95,160,82,0.18)`, radius 14, padding `11px 13px`, Inter 13.5px / line-height 1.45, `#2a4a32`. Label "Zubereitung:" bold `#14391f`, then prep text.
- **Vorteile / Nachteile** (two equal columns, gap 16, 16px below):
  - Column heading: Inter 11px / 700, uppercase, tracking `0.10em`. Vorteile `#3f7a36`, Nachteile `#c2502f`.
  - List items: gap 7, each a bullet dot (5×5 circle) + text. Vorteile dot `#5fa052`, Nachteile dot `#ff7849`. Item text Inter 13px / line-height 1.4, `#2a4a32`, dot offset `margin-top: 6px`.
- **Geschmack box**: bg `#faf6ec`, border `1px solid rgba(20,57,31,0.06)`, radius 14, padding `11px 13px`, Inter 13px / 1.45, `#6b7868`. Label "Geschmack:" bold `#14391f`.
- **Actions** (column, gap 8, 16px below):
  - Primary "Diese Alternative verwenden": full-width, bg `linear-gradient(135deg, #1a3a23, #0e2415)`, white, radius 14, padding 13px, Inter 14px / 700, leading check icon, shadow `0 6px 18px rgba(20,57,31,0.24)`.
  - Secondary "Zum Batch hinzufügen": full-width, bg white, border `1.5px solid rgba(20,57,31,0.18)`, radius 14, padding 12px, Inter 13.5px / 600, `#14391f`, leading plus icon in `#5fa052`.

#### 3. Footer (fixed)
- Padding `12px 16px 22px`, background `linear-gradient(180deg, rgba(245,239,225,0) 0%, #f5efe1 36%)` so the scroll content fades under it (`margin-top: -28px`, container `pointer-events: none`, button `pointer-events: auto`).
- **Abbrechen** button: full-width, bg white, border `1px solid rgba(20,57,31,0.12)`, radius 16, padding 13px, Inter 14px / 600, `#6b7868`, shadow `0 2px 8px rgba(20,57,31,0.05)`.

---

### Macro pills (shared)
Soft tinted pills (tinted bg + 1px inset ring + colored Fraunces numeral) — replacing the old loud solid pills. Each tone:

| Macro | bg | text/numeral | inset ring |
|---|---|---|---|
| kcal | `rgba(20,57,31,0.07)` | `#2a4a32` | `rgba(20,57,31,0.10)` |
| protein (P) | `rgba(95,160,82,0.16)` | `#3f7a36` | `rgba(95,160,82,0.28)` |
| carbs (C) | `rgba(60,108,158,0.13)` | `#3a6390` | `rgba(60,108,158,0.24)` |
| fat (F) | `rgba(245,185,66,0.20)` | `#9a7415` | `rgba(245,185,66,0.40)` |

- **Original pill**: radius 999, padding `5px 11px`, value Fraunces 13px / 600 + suffix Inter 10px / 700 (suffix at 0.78 opacity). Suffixes: "kcal", "P", "C", "F".
- **Delta pill**: radius 999, padding `4px 9px`, value Fraunces 12px / 600 + suffix Inter 9.5px / 700. Value carries the sign ("+4" / "-1.8").

## Interactions & Behavior
- **Open**: tap the swap (↔) button on any ingredient row in Recipe Detail → modal opens for that ingredient. (Wired in `recipe-detail.jsx`: `RDIngredientCard` `onSwap` → `RDScreen` `swapOpen` state → renders `AltModal`.)
- **Dismiss**: backdrop click, close (×) button, or "Abbrechen" → `onClose`.
- **Slide-up**: bottom-sheet enters from the bottom (add a transform/opacity transition, ~250ms ease-out, when you wire real animation — the prototype renders it open).
- **"Diese Alternative verwenden"**: should replace the original ingredient in the recipe with the chosen substitute (scaled amount), then close the modal. *(Not yet wired in the prototype — buttons are visual.)*
- **"Zum Batch hinzufügen"**: queues the substitute into a batch list without closing. *(Not yet wired.)*
- Respect reduced-motion: skip the slide animation.

## State Management
- `swapOpen: boolean` + `swapTarget: ingredient` — which ingredient's alternatives are shown.
- Alternatives data per ingredient: fetched from the AI service. While loading, show a skeleton/spinner in the scroll body; on error show a retry state. The meta row (engine, model, latency) comes from the AI response.
- Selecting an alternative mutates the recipe's ingredient list (and recomputes nutrition totals on the Nutrition tab).

## Data Shape
```ts
interface AltMeta { engine: string; model: string; ms: number; }
interface Macros { kcal: number; protein: number; carbs: number; fat: number; }
interface AltOption {
  name: string;
  amount: string;              // e.g. "180g"
  match: number;               // 0–100
  delta: { kcal: string; protein: string; carbs: string; fat: string }; // signed strings
  desc: string;
  prep: string;                // Zubereitung
  pros: string[];              // Vorteile
  cons: string[];              // Nachteile
  taste: string;               // Geschmack
}
interface AltModalData {
  original: { name: string; amount: string; macros: Macros };
  meta: AltMeta;
  options: AltOption[];
}
```

## Design Tokens
From `styles.css` (`:root`) plus modal-specific values:

- **Colors**: cream bg `#f5efe1`, card-soft `#faf6ec`, white `#ffffff`, ink-deep `#14391f`, ink-deep-2 `#0e2415`, ink-mid `#2a4a32`, ink-mute `#6b7868`, ink-mute-2 `#9aa295`, avocado `#5fa052`, pro-green-text `#3f7a36`, coral `#ff7849`, con-coral-text `#c2502f`, gold `#f5b942`.
- **Header gradient**: `linear-gradient(135deg, #1f4329, #0e2415)`. **CTA gradient**: `linear-gradient(135deg, #1a3a23, #0e2415)`.
- **Radii**: pill 999; boxes 14; cards 18 (original) / 22 (alternative) / 16 (footer button); sheet top 28.
- **Shadows**: card `0 2px 10px rgba(20,57,31,0.06)`; CTA `0 6px 18px rgba(20,57,31,0.24)`; match badge `0 2px 8px rgba(95,160,82,0.30)`; sheet `0 -20px 60px rgba(10,28,17,0.30)`.
- **Type**: Fraunces (opsz 9–144, weights 500/600/700) for titles, ingredient names, numerals; Inter (400/500/600/700) for everything else.

## Assets
- **Icons** (inline stroke SVGs, 24×24 viewBox, stroke-width 2): close, sparkle (filled), check, plus, clock. Replace with the codebase's icon set (Lucide / Heroicons / internal).
- No images in the modal. Fonts loaded from Google Fonts (Fraunces, Inter) — use the codebase's font pipeline.

## Files in this bundle
- `Ingredient Alternatives.html` — runnable prototype. Two artboards on a design canvas: (1) the modal open over Recipe Detail, (2) the standalone scroll content (alternative cards).
- `ingredient-alternatives.jsx` — the modal: `AltModal`, `AltOptionCard`, `AltOriginalCard`, macro/delta pills, pro/con columns, icons, and sample data.
- `recipe-detail.jsx` — the host Recipe Detail screen; shows how the swap button opens `AltModal` (`onSwap` → `swapOpen`).
- `styles.css` — shared Avocado design tokens (`:root` variables).
- `design-canvas.jsx` — pan/zoom canvas used only to present the artboards (not part of the feature).

## To run
Open `Ingredient Alternatives.html` in a browser. React + Babel are loaded from CDN; JSX is transpiled in-browser (dev only — precompile for production).
