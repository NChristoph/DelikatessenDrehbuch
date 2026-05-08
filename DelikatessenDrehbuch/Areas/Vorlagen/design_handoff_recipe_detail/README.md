# Handoff: Recipe Detail Screen — Avocado-Stil

## Overview

Redesign of the existing **Recipe Detail** screen (the four screenshots provided: Schweinebraten / Ingredients / Steps / Nutrition). The old version was visually disconnected from the rest of the app — generic light grey/blue chrome, blue outline buttons, mismatched typography. This redesign brings the screen in line with the **Marketplace + Create-Posting** visual language: cream backgrounds, forest-green ink, Fraunces serif for titles and big numbers, white cards with soft shadows, and the same segmented tab pattern used throughout the app.

The screen has three tab states — **Ingredients · Steps · Nutrition** — sharing one header + title block.

## About the Design Files

The files in this bundle are **design references created in HTML** — prototypes showing intended look and behavior, **not production code to copy directly**.

The implementation task is to **recreate these designs in the target codebase** (likely React + the existing marketplace component library), reusing already-defined design tokens, typography, primitives, and i18n setup.

The HTML uses inline styles for speed of iteration — production code should use the project's idiomatic styling approach (CSS modules, Tailwind, styled-components, etc.).

## Fidelity

**High-fidelity (hifi).** Pixel-perfect mockups with final colors, typography, spacing, radii, and interactions.

## Screen Anatomy

```
┌──────────────────────────────────┐
│   [hero image, 320 px tall]      │
│   ◄ back        ♥   DE ▾         │
│   @author-tag                    │
│ ─ rounded top, cream sheet ──    │
│   ─                              │  ← drag handle
│   ● HAUPTGERICHT · FLEISCH       │  ← eyebrow with avocado dot
│   Schweinebraten mit dunkler     │  ← Fraunces 26 / 600
│   Biersauce und Ofengemüse       │
│   [👥 4]  [⏱ 120 Min.] [🔥 545]  │  ← stat pills
│   ⤓ Feed-Posting bearbeiten       │  ← outline pill
│ ─────────────────────────────    │
│  ┌ Ingredients · Steps · Nutr ┐  │  ← segmented tab bar
│  └────────────────────────────┘  │
│                                  │
│  [tab-specific body]             │
│                                  │
│ ─ floating bottom bar ───        │
│  [Jetzt kochen ────]  [↗]        │
└──────────────────────────────────┘
```

### Header (image + top controls)

- 320 px tall, full-bleed hero image with a vertical fade to cream at the bottom (`linear-gradient(180deg, rgba(0,0,0,0.18) 0%, rgba(0,0,0,0) 35%, rgba(0,0,0,0) 60%, #f5efe1 100%)` over the image)
- Top-left: 38×38 white-glass circular **back** button, drop-shadow `0 2px 10px rgba(0,0,0,0.15)`, backdrop-blur 6 px
- Top-right: 38×38 circular **heart** + pill-shaped **DE ▾** language switcher, same surface style
- Bottom-left of hero: **author tag** — frosted dark capsule (`rgba(20,57,31,0.55)` + blur 8 px) with a 22×22 colored monogram circle and `@handle`. The status bar text is white over the image area.

### Title Block

- Sits with a **negative top margin (−28 px)** so it overlaps the hero, with `border-top-{left,right}-radius: 28px` against the cream sheet
- 38×4 muted drag-handle line, centered
- **Eyebrow:** `[6 px avocado dot] CATEGORY · PREFERENCE` — Inter 11 / 700, uppercase, tracked `0.08em`, color `#6b7868`
- **Title:** Fraunces 26 / 600, `letter-spacing: -0.02em`, color `#14391f`, `text-wrap: pretty`. Multi-line OK
- **Stat pills:** three white pills in a row, 8 px gap. Each: `[icon] [Fraunces 15 / 600 value] [Inter 12 / 500 unit]`. Icons are stroke SVGs (24×24 viewBox) — `people`, `clock` (avocado-tinted), `flame` (coral-tinted).
- **Edit-feed-posting:** outline pill, `1.5px solid rgba(20,57,31,0.18)`, padding `9px 14px`, Inter 13 / 600, with a small edit icon. Replaces the old blue button.

### Segmented Tab Bar

This is the same component used in Create Posting — re-export it.

- Outer container: `rgba(20,57,31,0.06)` background, `border-radius: 999px`, padding 4
- Tabs inside have `flex: 1`, padding `10px 6px`, radius 999
- **Active tab:** `background: white`, `color: #14391f`, shadow `0 4px 12px rgba(20,57,31,0.10)`
- **Inactive tab:** `background: transparent`, `color: #6b7868`
- Tab labels: Inter 13 / 600. Smooth `transition: all .2s` between states

### Tab body — Ingredients

1. **Servings stepper** (centered): white pill containing `Servings for [−] [Fraunces 18/700 value] [+]`
   - Decrement is a 26 px white circle with hairline border
   - Increment is a 26 px solid `#14391f` circle, white `+`
2. **Group sections** — for each (Fleisch · Gemüse · Sonstiges):
   - **Group label:** Inter 11 / 700, uppercase, tracked `0.14em`, color `#9aa295`, padding `0 4px 8px`
   - **Ingredient cards:** white cards, `border-radius: 18px`, padding `12px 14px`, shadow `0 2px 8px rgba(20,57,31,0.05)`, gap 6 px between cards
     - Layout: `[38 px round emoji tile, bg #ede7d4] [name 14 / 700 #14391f] [Fraunces 16 / 600 amount + unit] [30 px swap circle, hairline border]`
     - The amount **scales live** with the servings stepper (multiplied by `servings / RECIPE.servings`, rounded sensibly)

### Tab body — Steps

A vertical stack of step cards, gap 10 px:

- White card, `border-radius: 18px`, padding `14px 16px 16px`, soft shadow
- **Step badge:** small pill, `background: rgba(95,160,82,0.14)`, `color: #14391f`, padding `4px 11px`, Fraunces 11 / 700, label "Step N"
- 10 px gap, then body text — Inter 14.5 / 1.5 line-height, color `#14391f`, `text-wrap: pretty`

(German recipe text is the source of truth — keep i18n keys.)

### Tab body — Nutrition

1. **Big total card** — full-width, `linear-gradient(135deg, #1a3a23, #0e2415)`, `border-radius: 22px`, padding `24px 18px 20px`, white text, shadow `0 12px 30px rgba(20,57,31,0.18)`
   - Number: Fraunces 56 / 700, `letter-spacing: -0.03em`, line-height 1
   - Label below (Inter 11 / 700, uppercase, tracked `0.16em`, `rgba(255,255,255,0.75)`): "Gesamt kcal"
2. **2×2 macro grid** — gap 10 px. Each cell: white card, radius 18, padding `20px 14px 16px`, soft shadow
   - Big number Fraunces 38 / 700, `letter-spacing: -0.02em`, color `#14391f`
   - Caps label (Inter 10 / 700, tracked `0.16em`, `#9aa295`): Protein · Carbs · Fat · Sugar
3. Caption underneath, Inter 11 italic, `#9aa295`: "Nährwerte sind Schätzungen und können variieren."

### Bottom Action Bar

Pinned absolute bottom of the screen on a soft cream fade-out:

- `linear-gradient(180deg, rgba(245,239,225,0) 0%, rgba(245,239,225,0.95) 30%, #f5efe1 100%)`, backdrop-blur 10
- Padding `12px 16px 28px` (28 px is iOS safe-area)
- Layout: `[Jetzt kochen — flex 1] [↗ share button — fixed]`
- **Primary:** forest-green gradient (`linear-gradient(135deg, #1a3a23, #0e2415)`), white, radius 16, shadow `0 6px 20px rgba(20,57,31,0.28)`
- **Share:** white pill, `1px solid rgba(20,57,31,0.12)`, radius 16, share icon

## Interactions & Behavior

- **Tab switch:** instant, no scroll. Each tab body is a separate render — header + title + tab bar stay fixed at the top of the scroll view.
- **Servings stepper:** clamps at min `1` (no max). Updates the displayed amount of every ingredient by `value / RECIPE.servings` and re-rounds (integers shown without decimals; otherwise trim trailing zeros).
- **Swap button (per ingredient):** opens a substitution sheet (out-of-scope for this prototype — show as a no-op visual control).
- **Heart:** toggles favorite state.
- **Back:** pops the navigation stack.
- **Feed-Posting bearbeiten:** routes to the Create-Posting flow, prefilled with this recipe's data.
- **Jetzt kochen:** routes to the cook-mode screen (out-of-scope).
- **Share:** opens native share sheet.

## State

```ts
type RecipeDetailState = {
  recipe: {
    id: string;
    title: string;
    hero: string;        // image URL
    servings: number;    // base servings the recipe was authored at
    duration: number;    // minutes
    kcal: number;
    category: string;
    pref: string;
    author: { name: string; handle: string; avatarUrl: string };
  };
  ingredients: Array<{
    group: 'Fleisch' | 'Gemüse' | 'Sonstiges' | string;
    name: string;
    emoji: string;       // or icon ref / image
    amount: number;
    unit: 'Stk.' | 'g' | 'ml' | 'EL' | 'TL' | string;
  }>;
  steps:    Array<{ n: number; body: string }>;
  nutrition: {
    total: number;       // kcal
    macros: Array<{ key: 'protein' | 'carbs' | 'fat' | 'sugar'; value: number | string }>;
  };
  // UI state
  activeTab: 'ingredients' | 'steps' | 'nutrition';
  servings: number;      // user-adjusted servings count
  isFavorite: boolean;
};
```

**Amount scaling helper:**
```ts
function scaleAmount(base: number, factor: number): string {
  const scaled = +(base * factor).toFixed(2);
  if (Number.isInteger(scaled)) return String(scaled);
  return scaled.toString().replace(/\.?0+$/, '');
}
```

## Design Tokens (reused from marketplace)

### Colors
| Token | Value | Use |
|---|---|---|
| `--bg-cream` | `#f5efe1` | Page background |
| `--card` | `#ffffff` | Cards, stat pills, macro cards |
| `--ink-deep` | `#14391f` | Primary ink, title, totals |
| `--ink-deep-2` | `#0e2415` | Nutrition / CTA gradient end |
| `--ink-mute` | `#6b7868` | Muted text, units, eyebrow |
| `--ink-mute-2` | `#9aa295` | Group labels, macro labels (lighter than `--ink-mute`) |
| `--ink-line` | `rgba(20,57,31,0.12)` | Hairlines, borders |
| `--avocado` | `#5fa052` | Eyebrow dot, step badge tint, success |
| `--avocado-soft` | `rgba(95,160,82,0.14)` | Step badge background |
| `--coral` | `#ff7849` | Flame icon |
| `--orange` | `#ff9a3c` | Coral gradient start (author monogram) |
| `--ing-tile` | `#ede7d4` | Round emoji tile bg in ingredient cards |

### Typography

| Use | Family | Size | Weight | Letter-spacing | Line-height |
|---|---|---|---|---|---|
| Recipe title | Fraunces | 26 | 600 | -0.02em | 1.1 |
| Nutrition total | Fraunces | 56 | 700 | -0.03em | 1.0 |
| Macro number | Fraunces | 38 | 700 | -0.02em | 1.0 |
| Stat pill value | Fraunces | 15 | 600 | -0.01em | 1.0 |
| Servings number | Fraunces | 18 | 700 | normal | 1.0 |
| Step badge | Fraunces | 11 | 700 | -0.01em | 1.0 |
| Ingredient amount | Fraunces | 16 | 600 | normal | 1.0 |
| Tab label | Inter | 13 | 600 | normal | 1.0 |
| Body text (steps) | Inter | 14.5 | 400 | normal | 1.5 |
| Ingredient name | Inter | 14 | 700 | normal | 1.2 |
| Stat pill unit | Inter | 12 | 500 | normal | 1.0 |
| Eyebrow caps | Inter | 11 | 700 | 0.08em | 1.0 |
| Group label caps | Inter | 11 | 700 | 0.14em | 1.0 |
| Macro caps label | Inter | 10 | 700 | 0.16em | 1.0 |
| Disclaimer caption | Inter (italic) | 11 | 400 | normal | 1.4 |

### Radii
| Token | Value | Use |
|---|---|---|
| pill | `999px` | Tab bar, stat pills, servings stepper, badges, edit-feed pill |
| `--radius-md` | `16px` | Bottom CTA buttons |
| `--radius-lg` | `18px` | Ingredient cards, step cards, macro cards |
| `--radius-xl` | `22px` | Nutrition total card |
| sheet top | `28px` | Title-block top corners overlapping the hero |

### Shadows
| Use | Value |
|---|---|
| Stat pill | `0 2px 8px rgba(20,57,31,0.06)` |
| Ingredient / step / macro card | `0 2px 8px rgba(20,57,31,0.05)` |
| Nutrition total card | `0 12px 30px rgba(20,57,31,0.18)` |
| Floating top button (back / heart / lang) | `0 2px 10px rgba(0,0,0,0.15)` |
| Active tab pill | `0 4px 12px rgba(20,57,31,0.10)` |
| CTA glow | `0 6px 20px rgba(20,57,31,0.28)` |

### Spacing

- Hero height: `320px`
- Title-block negative top margin: `-28px`
- Page horizontal padding: `18px`
- Card row gap (ingredients / steps): `6 / 10px`
- Group section gap (ingredients): `14px`
- Macro grid gap: `10px`
- Bottom CTA bar bottom safe-area: `28px`

### Gradients

- Hero fade: `linear-gradient(180deg, rgba(0,0,0,0.18) 0%, rgba(0,0,0,0) 35%, rgba(0,0,0,0) 60%, #f5efe1 100%)`
- Nutrition total: `linear-gradient(135deg, #1a3a23, #0e2415)`
- Primary CTA: `linear-gradient(135deg, #1a3a23, #0e2415)`
- Bottom-bar fade: `linear-gradient(180deg, rgba(245,239,225,0) 0%, rgba(245,239,225,0.95) 30%, #f5efe1 100%)`
- Author monogram: `linear-gradient(135deg, #ff9a3c, #ff7849)`

## Assets

- **Fonts:** Google Fonts — Fraunces (500/600/700) + Inter (400/500/600/700). Host locally in production.
- **Hero image:** placeholder Unsplash photo in the prototype — production should use the recipe's own photo.
- **Icons:** stroke SVGs (24×24 viewBox) — back arrow, heart, share, people, clock, flame, edit, swap, chevron. Replace with the project's icon set (Lucide / Heroicons / internal).
- **Emojis** (ingredient tiles, top match indicator) — keep as Unicode for now, matching the rest of the app's voice.

## Files in this bundle

| File | Purpose |
|---|---|
| `Recipe Detail.html` | Host file — mounts the three tab states on a Design Canvas. |
| `recipe-detail.jsx` | All components for the screen (`RDHeader`, `RDTitleBlock`, `RDTabBar`, `RDServingsStepper`, `RDIngredientCard`, `RDIngredientsBody`, `RDStepsBody`, `RDNutritionBody`, `RDBottomBar`, `RDScreen`). |
| `styles.css` | Marketplace design tokens — copy into the project's global stylesheet if not already present. |

## Implementation checklist

- [ ] Reuse the segmented tab-bar component from Create Posting (extract to a shared primitive)
- [ ] Implement the `scaleAmount` helper and the live-scaling Servings stepper
- [ ] Group ingredients by `group` field; render groups in source order, not alphabetical
- [ ] Wire heart, share, and "Feed-Posting bearbeiten" navigation
- [ ] Localize strings (German is source; reuse the existing i18n setup — note Steps/Ingredients/Nutrition tab labels are currently English in the prototype, decide which language wins)
- [ ] Use the recipe's actual hero photo, not the placeholder
- [ ] Add loading / error states for the recipe fetch and the favorite toggle
- [ ] Match safe-area insets on iOS for the bottom action bar
