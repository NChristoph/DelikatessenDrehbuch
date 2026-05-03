# Handoff: Create Posting Flow — Redesign

## Overview

Redesign of the "Create Posting" flow used by creators in the recipe marketplace. The old version used a dark blue/purple palette with gradient buttons that did not match the rest of the marketplace. This redesign brings the flow in line with the existing **marketplace + meal planner** visual language (cream backgrounds, forest-green ink, coral accents, Fraunces serif headlines) and adds **TikTok-style DNA**: media-first thinking, large typography, sticky bottom CTA, live preview of how the posting will look in the feed.

The flow has 5 sections — **Basis · Media · Zutaten · Steps · Keywords** — and ends with a **Veröffentlichen** action.

## About the Design Files

The files in this bundle are **design references created in HTML** — prototypes showing intended look and behavior, **not production code to copy directly**.

The implementation task is to **recreate these designs in the target codebase's existing environment** (likely React + the existing marketplace component library, given the rest of the project), reusing already-defined design tokens, typography, primitives, and i18n setup. If parts of the styling system don't yet exist in the codebase, follow the **Design Tokens** section below to add them.

The HTML uses inline styles for speed of iteration — production code should use the project's idiomatic styling approach (CSS modules, Tailwind, styled-components, etc.).

## Fidelity

**High-fidelity (hifi).** Pixel-perfect mockups with final colors, typography, spacing, radii, and interactions. Recreate the UI pixel-perfectly using the codebase's existing libraries and patterns.

## Variants

Three layout directions are presented side-by-side in `Create Posting.html`. Pick one (or merge):

| ID | Name | Idea | When to use |
|---|---|---|---|
| **A** | **Scroll Stack** | Sticky tab-bar at top acts as anchor links into a single long scroll. Tabs highlight as user scrolls. | Default recommendation — closest to the existing screens, lowest friction, best discoverability. |
| **B** | **Hybrid Hero** | Dark forest-green hero header that shows a live preview of the posting (title, category, persons, duration, progress). Sticky tabs sit half-overlapping the hero. Body identical to A. | When you want a strong "you are creating something" feel and a permanent header preview. |
| **C** | **Wizard** | One step per fullscreen, segmented progress bar at top, prev / next at bottom. | When the team prefers a guided flow over a long scroll, or for first-time creators. |

Recommend **Variant A** for primary implementation. C can ship as an opt-in "guided mode" later.

## Screens / Sections

All variants share the same five section bodies. Each section has the same chrome: white card, `border-radius: 22px`, soft shadow, header row with **icon tile + Fraunces title + subtitle + step counter pill (e.g. "1/5")**. When a section is "complete" the icon tile becomes a green checkmark and the counter pill turns green.

### 1. Basis

**Purpose:** Capture metadata about the recipe.

**Fields:**
- `Titel` — text input, icon `✏️`, placeholder `z.B. Cremige Tomatensuppe`
- `Kategorie` — select, full-width on its row half (grid 1fr 1fr with `Präferenz`). Options: Hauptgericht 🍽️, Suppe 🥣, Salat 🥗, Snack 🍿, Süß 🍰, Drink 🥤
- `Präferenz` — select. Options: Vegetarisch 🥦, Vegan 🌱, Fleisch 🥩, Fisch 🐟, Glutenfrei 🌾, Laktosefrei 🥛
- `Personen` — number input, icon `👥`, suffix `Pers.`
- `Dauer` — number input, icon `⏱`, suffix `Min`

**Inputs** are white cards, `1px solid rgba(20,57,31,0.1)`, `border-radius: 14px`, padding `12px 14px`, `font-size: 15px`, `font-weight: 500`. Labels above are 11px uppercase tracked `0.06em`, color `#6b7868`.

Section is "complete" when title + category + persons + duration are all set.

### 2. Media

**Purpose:** Upload the feed video / image (this is the TikTok-style hero of the posting).

**Empty state:**
- Full-width 9:14 aspect-ratio drop zone with `2px dashed rgba(20,57,31,0.2)` border on a `linear-gradient(180deg, #faf6ec, #f5efe1)` background
- Center: 64×64 coral gradient circle with upload icon (`linear-gradient(135deg, #ff9a3c, #ff7849)`, glow shadow `0 10px 24px rgba(255,120,73,0.4)`)
- Below circle: Fraunces 22px "Upload starten" + 13px muted "Wähle ein Video oder Bild für den Feed"
- Spec pills below: `9:16` · `MP4 · MOV` · `≤ 60s`
- Trio of secondary buttons under the dropzone: 📷 Kamera · 🖼 Galerie · ✨ AI-Gen

**Filled state:**
- Same 9:14 box, now showing the uploaded media as `background-image`
- TikTok-style overlay: vertical column on the right (heart, comment, bookmark) with frosted-glass circular buttons `36×36, rgba(0,0,0,0.35), backdrop-filter: blur(8px)`
- Bottom-left text overlay: small caps "VORSCHAU · FEED" + Fraunces 18px headline
- Top-right `Ersetzen` chip
- Trio of edit buttons under the preview: ✂️ Trimmen · 🎵 Sound · 📐 Format

Section is "complete" when media is set.

### 3. Zutaten (Ingredients)

**Purpose:** Build the ingredient list with quantities.

**Layout (top → bottom):**
1. Search input with 🔍 icon
2. **Selected list** (only shown when ingredients exist): each item is a cream card `#faf6ec`, padding `10px 12px`, radius 14, containing `[emoji 22px] [name 14/600] [stepper -/+]`. The stepper is a pill: `−` text button, count text "N Stk.", `+` button is a dark-green 22px circle.
3. **Suggestions** strip — heading "Vorschläge zum Antippen" with badge "✨ AI vorgeschlagen". Below, wrap of pills `[emoji name +]`. Pills already added show a green check instead of `+` and use `rgba(95,160,82,0.14)` background.
4. **Zutat fehlt? AI bitten** — full-width dashed-border button at the bottom

Section is "complete" when ≥ 2 ingredients are added.

### 4. Steps

**Purpose:** Build the step-by-step instructions, with AI assistance. **All step-related tooling lives in this single section** — no separate "Smart Step" tab.

**Layout (top → bottom, all inside the same section card):**
1. **"Was könnte es sein?"** card — cream tile that shows AI-detected recipe types as percentage chips: `[★ 8% Suppe] [7% Eintopf] [5% Curry] [4% Pasta-Gericht]`. The top match is dark-ink filled with a gold star, the rest are white. Selecting a type seeds the step templates below.
2. **Erkannte Steps** — list of detected step cards. Each has:
   - small caps label "ERKANNTER STEP #N" + two buttons (`✕` ghost / `✓ Akzeptieren` solid dark)
   - body text formatted like a script: action verb in **coral**, ingredient/object **bold with dotted coral underline**, hint in muted gray
3. **Smart Step Creator** (inline, not a separate tab/section) — dark forest-green panel `#14391f` with category filter chips (Alle, ⭐ Beste, 🥕 Vorbereitung, 🔥 Kochen, ✨ Finishing, 🍽️ Servieren). Active chip is white-on-dark.
4. **Templates** — list of step templates, each showing emoji tile + title + body text + star rating. Choosing one creates a new step from that template.

### 5. Keywords

**Purpose:** Tag the post.

**Layout:**
- Header: "N ausgewählt · max 8 sichtbar im Feed"
- Wrap of pills (white on cream when inactive, dark forest-green filled when selected)
- Once 1+ selected: cream preview card showing `#hashtag` style with coral-colored hashes

Section is "complete" when ≥ 3 keywords selected.

## Top Bar (variants A & C)

- Left: 36×36 white circular back button with shadow
- Center: tiny uppercase "Posting erstellen" + Fraunces 15px "N/5 bereit"
- Right: white pill-shaped language switcher (`DE ▾`)

## Hero Bar (variant B only)

- Forest-green gradient `linear-gradient(180deg, #14391f, #0e2415)`, white text
- Title in Fraunces 32px, the live `data.title`
- Beneath: coral-tinted category pill, persons & duration in muted
- Right: large "N / 5" with the N in coral
- Below: 4px progress bar `rgba(255,255,255,0.1)` track, coral gradient fill

## Sticky Tab Bar (A & B)

- Horizontal scrolling pill tabs: `🧩 Basis · 🎬 Media · 🛒 Zutaten · 📝 Steps · # Keywords`
- Active: forest-green bg, white fg, soft shadow `0 4px 14px rgba(20,57,31,0.18)`
- Inactive: white bg, dark fg, hairline border
- Completed tabs show a tiny green check circle next to the label
- Clicking a tab smooth-scrolls the body to that section anchor; scroll position drives the active tab back via scroll-spy

## Sticky Publish Bar (A & B)

Pinned to bottom on top of a soft cream gradient fade-out:
- White rounded card `border-radius: 18px`, padding `8px 8px 8px 16px`, shadow `0 12px 30px rgba(20,57,31,0.16)`
- Left: Fraunces 16px label — `"N/5 Schritte fertig"`, or `"Bereit zum Posten ✨"` when complete. 4px progress bar beneath, coral gradient (or green when complete)
- Right: **Veröffentlichen** button → coral gradient, paper-plane icon, shadow `0 6px 20px rgba(255,120,73,0.4)`. Disabled (gray) until all 5 sections complete.

## Wizard Bottom Bar (variant C only)

- White "Zurück" outline button (disabled on first step)
- Forest-green gradient "Weiter →" button (flex-fill)
- On final step the right button becomes the coral "Veröffentlichen ✈" button, disabled until all sections complete
- Above the bar: 5-segment progress at the top of the screen — past steps green `#5fa052`, current dark `#14391f`, future muted `rgba(20,57,31,0.1)`

## Interactions & Behavior

- **Tab click** (A, B): smooth-scroll to the section's anchor with `scrollMarginTop: 88px` so the section header lands below the sticky tab bar
- **Scroll spy** (A, B): as the body scrolls, recompute the topmost section whose `offsetTop ≤ scrollTop + 90`, and set that tab active
- **Section "complete" state** changes:
  - icon tile background becomes `#14391f` (forest green) and shows a white checkmark
  - counter pill background becomes `#5fa052` (avocado green), text becomes white
  - tab pill grows a tiny green ✓ circle to the right of the label
- **Stepper buttons (Zutaten):** clamp at 0; reaching 0 removes the row entirely
- **Suggestion pill click:** appends to selected list with default `1 Stk.`; pill state flips to "added" (green check, no longer clickable)
- **Detected step Akzeptieren:** moves the step into a "real steps" list (not yet wired in this prototype — implement with a state slice in real app)
- **Media empty → filled:** clicking the dropzone sets media to a sample URL in the prototype; in real app this opens the file picker / native upload
- **Media Ersetzen:** clears media, returns to empty state
- **Wizard navigation (C):** Zurück / Weiter; clicking a segment in the top progress bar jumps to that step
- **Smooth transitions:** chip activation, button hovers, progress bar width — all `transition: ... .15s–.3s ease`

## State Management

```ts
type CreatePostingState = {
  data: {
    title: string;
    category: { id: string; label: string; emoji: string } | null;
    pref:     { id: string; label: string; emoji: string } | null;
    persons:  string;   // string for input flexibility, parse on submit
    duration: string;
  };
  media: string | null;          // URL after upload
  ingredients: Array<{
    name: string;
    emoji: string;
    amount: number;
    unit: string;                // 'Stk.' | 'EL' | 'TL' | 'g' | 'ml' | ...
  }>;
  detectedSteps: Array<{ action: string; objects: string[]; hint: string; status: 'pending' | 'accepted' | 'rejected' }>;
  steps:    Array<{ /* user-edited step shape */ }>;
  keywords: string[];
  activeTab: 'basis' | 'media' | 'zutaten' | 'steps' | 'keywords';   // for variant A/B scroll spy
  wizardIdx: number;                                                  // for variant C
};
```

**Completion derivation** (memoized selector):
```ts
const completion = {
  basis:    !!data.title && !!data.category && !!data.persons && !!data.duration,
  media:    !!media,
  zutaten:  ingredients.length >= 2,
  steps:    steps.length >= 1,            // tweak: real definition of "done"
  keywords: keywords.length >= 3,
};
const doneCount = Object.values(completion).filter(Boolean).length;
const ready = doneCount === 5;
```

**Persistence:** save draft to local storage on every change (debounced ~500 ms). On submit, POST to the posting API and clear draft.

## Design Tokens

These tokens are taken from the existing marketplace `styles.css` — reuse them, do not redefine.

### Colors
| Token | Value | Use |
|---|---|---|
| `--bg-cream` | `#f5efe1` | Page background |
| `--bg-cream-2` | `#ede5d2` | Alt page bg |
| `--card` | `#ffffff` | Section card surface |
| `--card-soft` | `#faf6ec` | Cream sub-surface, stats strip |
| `--ink-deep` | `#14391f` | Primary ink, section icon when complete, dark fills |
| `--ink-deep-2` | `#0f2c18` | Hero gradient end |
| `--ink-mid` | `#2a4a32` | Secondary ink |
| `--ink-text` | `#1a2e20` | Body text |
| `--ink-mute` | `#6b7868` | Muted text, labels |
| `--ink-line` | `rgba(20,57,31,0.12)` | Hairlines |
| `--avocado` | `#5fa052` | Success / completion |
| `--coral` | `#ff7849` | Primary CTA, accent |
| `--coral-soft` | `#ffb494` | Coral hover/light |
| `--orange` | `#ff9a3c` | Coral gradient start |
| `--gold` | `#f5b942` | Stars, gold accent |
| `--premium` | `#c8a45c` | Premium badge |

### Typography
- **Sans:** `'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif` — UI, body, inputs
- **Serif:** `'Fraunces', 'Playfair Display', Georgia, serif` — section titles, headlines, big numbers

| Use | Family | Size | Weight | Letter-spacing | Line-height |
|---|---|---|---|---|---|
| Page hero (variant B) | Fraunces | 32 | 600 | -0.02em | 1.05 |
| Wizard step heading (C) | Fraunces | 30 | 600 | -0.02em | 1.0 |
| Section title | Fraunces | 20 | 600 | -0.01em | 1.1 |
| Empty-upload heading | Fraunces | 22 | 600 | -0.01em | 1.1 |
| Step counter (1/5) | Fraunces | 11 | 700 | normal | 1 |
| Tab label | Inter | 13 | 600 | normal | 1 |
| Input value | Inter | 15 | 500 | normal | 1.2 |
| Body text | Inter | 13 | 500 | normal | 1.45 |
| Label (uppercase) | Inter | 11 | 600 | 0.06em | 1 |
| Pill / chip | Inter | 12 | 600 | normal | 1 |
| Caption | Inter | 11 | 500 | normal | 1.3 |

### Radii
| Token | Value | Use |
|---|---|---|
| `--radius-sm` | `10px` | Small inner pieces |
| `--radius-md` | `16px` | Sub-cards, smart-creator panel |
| `--radius-lg` | `22px` | Section cards |
| `--radius-xl` | `28px` | Phone outer |
| pill | `999px` | All chips, tabs, buttons |
| input | `14px` | Text inputs, selects, secondary buttons |

### Shadows
| Token | Value | Use |
|---|---|---|
| `--shadow-sm` | `0 2px 8px rgba(20,57,31,0.06)` | Top-bar buttons |
| `--shadow-md` | `0 8px 24px rgba(20,57,31,0.10)` | Section cards |
| `--shadow-lg` | `0 20px 60px rgba(20,57,31,0.16)` | (unused here, available) |
| Coral CTA glow | `0 6px 20px rgba(255,120,73,0.4)` | Veröffentlichen button |
| Publish bar | `0 12px 30px rgba(20,57,31,0.16)` | Sticky bottom card |

### Spacing
- Page horizontal padding: `14px`
- Section card outer margin-bottom: `14px`
- Section card padding: `18px`
- Section header → body gap: `12px`
- Field group gap: `14px`
- 2-column field grid gap: `10px`
- Pill gap (tabs, suggestions): `6–8px`
- Sticky bar bottom safe-area: `28px`

### Gradients
- Coral CTA: `linear-gradient(135deg, #ff9a3c, #ff7849)`
- Forest-green hero (B): `linear-gradient(180deg, #14391f, #0e2415)`
- Forest-green wizard "Weiter": `linear-gradient(135deg, #14391f, #1a4a2a)`
- Empty dropzone surface: `linear-gradient(180deg, #faf6ec, #f5efe1)`
- Sticky bar fade: `linear-gradient(180deg, rgba(245,239,225,0) 0%, #f5efe1 60%)`

## Assets

- **Fonts:** Google Fonts — Fraunces (500/600/700) + Inter (400/500/600/700). Already loaded in the prototype via the `<link>` to `fonts.googleapis.com`. In production, host locally for performance / privacy.
- **Sample media image** in the prototype: Unsplash photo `1565557623262-b51c2513a641` (curry). In production, replace with the user's actual upload.
- **Icons:** all stroke-based inline SVGs (24×24 viewBox) — back arrow, plus, check, chevron, upload, paper plane. Replace with the project's icon library (Lucide / Heroicons / project-internal set).
- **Emojis:** native Unicode emojis are used as category / preference / ingredient glyphs. They are explicitly part of the brand vibe — do not replace with custom icons unless the team decides to invest in a custom illustration set.

## Files in this bundle

| File | Purpose |
|---|---|
| `Create Posting.html` | Host file — mounts the three variants on a Design Canvas. Open in a browser to interact with all three side-by-side. |
| `create-posting.jsx` | All section components (`CPBasisBody`, `CPMediaBody`, `CPZutatenBody`, `CPStepsBody`, `CPKeywordsBody`), shell chrome (`CPSection`, `CPTabBar`, `CPPublishBar`), and the three top-level variants (`CreatePostingA`, `CreatePostingB`, `CreatePostingC`). |
| `styles.css` | The marketplace design tokens (CSS custom properties) — copy these into the project's global stylesheet if not already present. |
| `design-canvas.jsx` | Pan/zoom canvas component used only to present the three variants — **not part of the deliverable**. |

## Implementation checklist

- [ ] Add the design tokens (Colors / Typography / Radii / Shadows) to the project if they don't already exist
- [ ] Pick a primary variant (recommended: **A · Scroll Stack**)
- [ ] Implement the five section bodies as separate components, each driven by a slice of state
- [ ] Wire scroll-spy + smooth-scroll for the sticky tab bar (A, B)
- [ ] Wire the completion derivation and propagate to: section icon tile, counter pill, tab badge, publish-bar progress + label, publish-button enable
- [ ] Localize all strings (the prototype is German; reuse the existing i18n setup)
- [ ] Replace the sample upload trigger with real file-picker / camera intent
- [ ] Hook the AI suggestion strips (Zutaten suggestions, "Was könnte es sein?", Erkannte Steps) to the existing AI service endpoints
- [ ] Persist draft to local storage (debounced) and clear on successful publish
- [ ] Add real loading / error states for upload, AI calls, and publish
