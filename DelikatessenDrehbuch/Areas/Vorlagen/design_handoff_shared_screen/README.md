# Handoff: Geteilte Inhalte (Gruppen-Feed) · Avocado App

Redesign des **„Geteilte Inhalte"**-Screens als social-media-feel Community-Hub. Ersetzt die bisherige Liste-+-Karten-Struktur durch Stories-Row, Hero-Banner und segmentierte Tab-Navigation (Feed · Chat · Aufgaben · Mitglieder).

---

## 1. Overview

Auf diesem Screen verwaltet ein Nutzer seine **geteilten Gruppen-Feeds** — Familienpläne, WG-Küchen, Sport-Crews etc. Pro Gruppe kann er:

- **Inhalte teilen** (Meal-Pläne, Texte, Aufgaben) — Feed
- **Chatten** mit den Mitgliedern — Chat
- **Aufgaben** verwalten (geteilte To-do-Liste) — Aufgaben
- **Mitglieder** sehen / einladen — Mitglieder

Der Screen ist erreichbar über die Home-Page (Square „Mein Bereich" → Tab „Geteilt") oder direkt über Bottom-Nav. Mobile-only, 390 px Viewport.

## 2. About the Design Files

Die Dateien in diesem Bundle sind **Design-Referenzen in HTML + React (UMD + Babel-standalone)** — Prototypen, die das beabsichtigte Aussehen und Verhalten dokumentieren. **Sie sind nicht für 1:1-Übernahme gedacht.**

Aufgabe: Diese Designs in der **bestehenden Avocado-Codebase** mit den dort etablierten Patterns, Komponenten und Theme nachbauen. Inline-Styles im Prototyp sind als visuelle Spec zu lesen — bitte ins Theme-System (Tailwind / CSS-Variablen / styled-components) übersetzen.

## 3. Fidelity

**High-fidelity.** Alle Farben, Verläufe, Typografie, Abstände, Radii, Shadows und Mikro-Interaktionen sind final spezifiziert.

---

## 4. Layout

```
┌────────────────────────────────────────────┐
│ [‹ Home]      GRUPPEN          [DE ▾]      │  TopBar
│              Geteilte Inhalte               │
├────────────────────────────────────────────┤
│  ╭───╮  ╭───╮  ╭───╮  ╭───╮  ╭───╮  ╭───╮ │
│  │ + │  │AV │  │WK │  │FP │  │T  │  │SC │ │  Stories-Row (h-scroll)
│  ╰───╯  ╰───╯  ╰───╯  ╰───╯  ╰───╯  ╰───╯ │  (aktive Gruppe = Ring)
│   Neu   Geteilte WG    Fam.   Test  Sport  │
├────────────────────────────────────────────┤
│ ╔════════════════════════════════════════╗ │
│ ║ [🛡 AKTIVE GRUPPE]                      ║ │  Hero-Banner
│ ║ Geteilte Inhalte                       ║ │  Forest-Gradient + Goldglow
│ ║ ●●●●  4 Mitglieder · 8 Beiträge        ║ │
│ ║ ┌──┐ ┌──┐ ┌──┐    [🔗 Einladen]        ║ │
│ ║ │ 4│ │ 8│ │ 3│                          ║ │  Quick-Stats
│ ║ │ME│ │BE│ │AU│                          ║ │
│ ║ └──┘ └──┘ └──┘                          ║ │
│ ╚════════════════════════════════════════╝ │
├────────────────────────────────────────────┤
│  ┌─Feed─┬─Chat•4─┬─Aufgaben─┬─Mitglieder─┐ │  Segmentierte Tab-Bar
├────────────────────────────────────────────┤
│         …Tab-Body (siehe §6)…              │
└────────────────────────────────────────────┘
```

Viewport: 390 px. Außen-Padding 14 px.

---

## 5. Header-Komponenten

### 5.1 TopBar

- Padding `14px 16px 8px`, flex space-between
- **Back-Pill** (links): white bg, border `1px solid rgba(20,57,31,0.08)`, border-radius 999, padding `7px 14px 7px 10px`, Inter 13/600, color `#14391f`. Inhalt: `back`-Icon 15px + Text „Home". Shadow `0 2px 8px rgba(20,57,31,0.05)`.
- **Title-Block** (zentriert): Eyebrow „GRUPPEN" (Inter 10/700, color `#9aa295`, letter-spacing 0.18em, uppercase). Title „Geteilte Inhalte" (Fraunces 18/700, color `#14391f`, letter-spacing −0.02em).
- **Lang-Switcher** (rechts): identisch zum Back-Pill, Inhalt „DE ▾" (Inter 11/700, letter-spacing 0.06em + chev 11px).

### 5.2 Stories-Row (Gruppen-Carousel)

- Horizontaler Scroll, padding `0 16px`, gap 14 zwischen Tiles
- Pro Tile: 64 px breit, flex-col-center, gap 6
- **Create-Tile** (immer erste): Circle 56×56, bg `#faf6ec`, border `2px dashed rgba(20,57,31,0.25)`. Inhalt: `plus` 22px. Label „Neu" (Inter 10/600 color `#6b7868`).
- **Group-Tile**:
  - **Inaktiv**: Avatar 56×56 (initials gradient — siehe §11 Avatar-System). Label `#6b7868`/500.
  - **Aktiv**: Outer ring `linear-gradient(135deg, #5fa052, #14391f)` 2.5px + white inner ring 2px. Avatar shrinkt auf 50 px. Label `#14391f`/700.
  - **Unread-Badge** (top-right): min-width 18, height 18, padding `0 5px`, border-radius 999, bg `#ff7849`, white border 2px, Inter 10/700 white.

### 5.3 Hero-Banner

Margin `0 14px`, border-radius 24, padding `20px 18px`. Identisches Visual-Pattern wie der Home-Hero, aber kompakter.

| Eigenschaft | Wert |
|---|---|
| Background | `linear-gradient(135deg, #5fa052 0%, #2f6a3a 55%, #14391f 100%)` |
| Box-shadow | `0 14px 32px rgba(20,57,31,0.28)` |
| Goldglow (top-right) | radial 180×180, `rgba(245,233,160,0.28)→0`, top −50, right −30 |

**Inhalt** (z-index 1):
- **Eyebrow-Pill** „[shield] AKTIVE GRUPPE": bg `rgba(255,255,255,0.16)`, backdrop-blur 6, padding `3px 9px 3px 7px`, radius 999, Inter 9.5/700 white, letter-spacing 0.12em uppercase. Shield-Icon 11px in `#f5e9a0`.
- **Title**: Fraunces 26/700, letter-spacing −0.025em, line-height 1.05.
- **Member-Row** (margin-top 8): Stacked Avatars (4 max, je −8 margin-left, ring `rgba(20,57,31,0.4)`, 22 px), gefolgt von Meta-Text Inter 12/500 opacity 0.9: „{n} Mitglieder · {m} Beiträge".
- **Einladen-Button** (top-right): white bg (`rgba(255,255,255,0.95)`), border-radius 14, padding `10px 12px`, Inter 11.5/700 `#14391f`. Icon `link` 13px + Text. Shadow `0 4px 12px rgba(0,0,0,0.18)`.
- **Stats-Grid**: 3 Spalten (1fr), gap 8, margin-top 14. Pro Stat: bg `rgba(255,255,255,0.10)`, border `1px solid rgba(255,255,255,0.14)`, radius 12, padding `8px 10px`, backdrop-blur 6. Wert: Fraunces 18/700 white. Label: Inter 9/600 opacity 0.75 letter-spacing 0.08em uppercase.

### 5.4 Tab-Bar (segmentiert)

Margin `20px 14px 16px`, bg `rgba(20,57,31,0.06)`, border-radius 14, padding 4. Grid 4-Spalten, gap 2.

Pro Tab:
- flex-col-center, gap 3, padding `8px 4px`, border-radius 11
- **Aktiv**: bg white, shadow `0 2px 8px rgba(20,57,31,0.10)`, Icon + Label `#14391f`/700, strokeWidth 2.4
- **Inaktiv**: bg transparent, Icon + Label `#6b7868`/600, strokeWidth 2
- Icon 17px, Label Inter 10, letter-spacing 0.04em
- **Badge** (optional, top-right des Tabs): min-width 14, height 14, padding `0 4px`, radius 999, bg `#ff7849`, white text Inter 8.5/700

Tabs: `Feed (feed)`, `Chat (msg)`, `Aufgaben (list)`, `Mitglieder (users)`.

---

## 6. Tab-Bodies

### 6.1 Feed

**Composer-Trigger** (oben, klickbar — öffnet Vollbild-Composer):
- Margin `0 14px 16px`, width `calc(100% - 28px)`
- bg white, border `1px solid rgba(20,57,31,0.08)`, radius 16, padding `12px 14px`
- Layout: Avatar 34 + Placeholder-Text (Inter 13 color `#9aa295`) + „Plan"-Quick-Pill rechts
- Plan-Pill: bg `rgba(95,160,82,0.10)`, color `#5fa052`, padding `5px 10px`, radius 999, Inter 11/700, mit `download`-Icon 12px

**Post-Card** — universelle Komponente für 3 Post-Typen:

| Eigenschaft | Wert |
|---|---|
| Margin | `0 14px 12px` |
| Background | white |
| Border-radius | 18 |
| Padding | 14 |
| Border | `1px solid rgba(20,57,31,0.04)` |
| Shadow | `0 4px 14px rgba(20,57,31,0.06)` |

**Header**: Avatar 36 + flex-col (Name Fraunces 14/700 `#14391f` + Role-Tag + Meta Inter 11/`#9aa295` „{time} · {action}") + `more`-Icon 18 `#9aa295` rechts.

**Role-Tag** (z.B. OWNER): Inter 8/700, color `#5fa052`, bg `rgba(95,160,82,0.12)`, padding `2px 6px`, radius 4, letter-spacing 0.12em.

**Payload (3 Varianten)**:

- **mealplan**: Inner-Card bg `#faf6ec`, radius 14, padding 12, flex gap 12. Icon-Tile 48×48 radius 12, gradient `#5fa052 → #3f7536`, white `download`-Icon 22px. Text-Block: Title Fraunces 15/600, Subtitle Inter 11/500 `#6b7868`. Action-Button rechts: bg `#14391f`, white, radius 999, padding `7px 14px`, Inter 11/700 — Text „Öffnen".
- **text**: Plain Text Inter 14/500 `#1a2e20`, line-height 1.45, `text-wrap: pretty`.
- **task**: Inner-Card identisch zu mealplan-Layout, aber: Checkbox 22×22 radius 6 — wenn `done`: bg `#5fa052`, white `check`-Icon. Wenn offen: white bg + border `2px solid rgba(20,57,31,0.2)`. Task-Text Inter 13/600. Wenn done: color `#9aa295`, `text-decoration: line-through`.

**Footer** (Reactions): margin-top 12, padding-top 10, border-top `1px solid rgba(20,57,31,0.06)`, flex gap 14.
- Heart-Button: `heart` 16px + count Inter 12/600. Wenn `liked`: color `#ff7849`, fill `#ff7849`.
- Comment-Button: `comment` 16px + count Inter 12/600, color `#6b7868`.
- **Reaction-Pill** (rechts, margin-left auto): bg `#faf6ec`, radius 999, padding `3px 8px`, Inter 12, max 3 Emojis + count Inter 10/600 `#6b7868`.

### 6.2 Chat

WhatsApp-Style. Padding `0 14px 8px`.

**Day-Divider** (oben): margin `8px 0 14px`, flex gap 10. Linker und rechter Strich: 1 px tall, bg `rgba(20,57,31,0.08)`, flex 1. Label „Heute"/„Gestern"/Datum: Inter 9.5/700, color `#9aa295`, letter-spacing 0.16em uppercase.

**Message-Bubble**:
- Layout: flex (row-reverse wenn `self`), gap 8, align-items flex-end, margin-bottom 12. Padding-left 40 wenn self, sonst padding-right 40 (für asymmetrische Konversation).
- **Fremder Author**: Avatar 30 + Wrap-Block mit Name-Header (margin-bottom 3, margin-left 10): Inter 11/700 `#14391f` mit Time-Anhang Inter 11/500 `#9aa295`. Bubble: bg white, padding `10px 14px`, radius `18px 18px 18px 4px`, Inter 13.5/500 `#1a2e20`, shadow `0 2px 8px rgba(20,57,31,0.06)`.
- **Self**: Bubble: bg `linear-gradient(135deg, #2f6a3a, #14391f)`, color white, radius `18px 18px 4px 18px`, shadow `0 4px 12px rgba(20,57,31,0.22)`. Footer (Time + Check, float right): Inter 10/500 `#9aa295`, `check`-Icon 11px `#5fa052`.
- **Sticker-Variante** (`kind === 'sticker'`): Bubble padding `8px 12px`, Inhalt font-size 22 (Emoji/Sticker). Sonst gleich.
- **Reaction-Float** (optional, hängt halb unter Bubble): margin-top −8, inline-block, bg white, border `1px solid rgba(20,57,31,0.06)`, radius 999, padding `2px 8px`, Inter 12 + count Inter 10/600 `#6b7868`. Shadow `0 2px 6px rgba(20,57,31,0.08)`.

**Composer** (sticky bottom):
- Position sticky bottom 0, bg `rgba(245,239,225,0.92)`, backdrop-blur 12, padding `10px 14px 14px`, border-top `1px solid rgba(20,57,31,0.06)`, z-index 5
- Flex align-center gap 8
- **Attach-Button** (links): 38×38 circle, bg white, border `1px solid rgba(20,57,31,0.08)`, `attach`-Icon 17px `#14391f`. Shadow `0 2px 8px rgba(20,57,31,0.04)`.
- **Input-Pill** (flex 1): bg white, border `1px solid rgba(20,57,31,0.08)`, radius 999, padding `9px 14px 9px 16px`. Input transparent Inter 13.5 `#1a2e20`. Emoji-Icon 20px `#9aa295` rechts.
- **Send-Button** (rechts): 42×42 circle, bg `linear-gradient(135deg, #5fa052, #14391f)`, `send-fill`-Icon 18 white. Shadow `0 6px 16px rgba(20,57,31,0.28)`.

### 6.3 Aufgaben

Padding `0 14px`.

**Add-Task-Row** (oben, margin-bottom 14): Flex gap 8.
- Input (flex 1): bg white, border `1px solid rgba(20,57,31,0.08)`, radius 14, padding `11px 14px`, Inter 13 `#14391f`. Shadow `0 2px 8px rgba(20,57,31,0.04)`.
- Plus-Button: 44×44 radius 14, bg `linear-gradient(135deg, #5fa052, #14391f)`, white `plus`-Icon 18px strokeWidth 2.6. Shadow `0 6px 14px rgba(20,57,31,0.22)`.

**Progress-Card** (margin-bottom 12):
- bg white, border `1px solid rgba(20,57,31,0.04)`, radius 18, padding 14, shadow `0 4px 14px rgba(20,57,31,0.05)`
- Top-Row flex baseline space-between:
  - Links: Eyebrow „DIESE WOCHE" Inter 10/700 `#9aa295` letter-spacing 0.16em uppercase. Wert: Fraunces 24/700 `#14391f` letter-spacing −0.02em, mit Sub-Span „/total erledigt" Fraunces 16/500 `#9aa295`.
  - Rechts: Percentage Fraunces 22/700 color `#5fa052`.
- Bar (margin-top 10): height 8, radius 999, bg `rgba(20,57,31,0.08)`. Fill `linear-gradient(90deg, #5fa052, #14391f)`, breite `done/total * 100%`.

**Task-Row**:
- bg white, border `1px solid rgba(20,57,31,0.04)`, radius 14, padding `12px 14px`, margin-bottom 8, shadow `0 2px 8px rgba(20,57,31,0.04)`. Flex gap 12 align-center.
- Checkbox: 22×22 radius 7. Wenn `done`: `linear-gradient(135deg, #5fa052, #3f7536)` + white `check`-Icon. Sonst: white + `2px solid rgba(20,57,31,0.2)`.
- Text-Block: Task-Text Inter 13.5/600 — done = `#9aa295` line-through, offen = `#14391f`. Optional Sub-Row mit Assignee-Avatar 16 + Meta Inter 10.5/500 `#6b7868` „{name} · {due}".

### 6.4 Mitglieder

Padding `0 14px`.

**Einladungslink-Card** (margin-bottom 14):
- bg white, border `1px dashed rgba(20,57,31,0.18)`, radius 18, padding 14, flex gap 12 align-center
- Icon-Tile 44×44 radius 12, bg `rgba(95,160,82,0.12)`, `link`-Icon 22px `#5fa052`
- Text-Block: Title Fraunces 14/700 `#14391f` „Einladungslink". Sub Inter 11/`#6b7868` (gekürzte URL z.B. „avocado.app/g/x7k2…m9p")
- Copy-Button rechts: bg `#14391f`, white, radius 12, padding `8px 12px`, Inter 11/700, `copy`-Icon 12px + Text „Kopieren"

**Member-Row**:
- bg white, border `1px solid rgba(20,57,31,0.04)`, radius 14, padding `10px 14px`, margin-bottom 8, shadow `0 2px 8px rgba(20,57,31,0.04)`. Flex gap 12 align-center.
- Avatar 42 (Initials-Gradient)
- Text-Block:
  - Name Fraunces 15/700 `#14391f` letter-spacing −0.01em mit inline Role-Tag (siehe §6.1) + optional Online-Dot 8×8 `#5fa052` mit white 2px ring
  - Sub Inter 11/`#6b7868` (Activity-Text z.B. „Aktiv jetzt · Beigetreten 14.04")
- `more`-Icon 18 `#9aa295` rechts

---

## 7. Design Tokens

### Colors

```css
:root {
  /* Backgrounds */
  --bg-screen:      #f5efe1;   /* Page (cream) */
  --card:           #ffffff;
  --card-soft:      #faf6ec;
  --composer-bg:    rgba(245, 239, 225, 0.92);

  /* Brand */
  --ink-deep:       #14391f;
  --ink-deep-2:     #0e2415;
  --ink-mid:        #2f6a3a;
  --ink-text:       #1a2e20;
  --ink-mute:       #6b7868;
  --ink-soft:       #9aa295;
  --ink-line:       rgba(20,57,31,0.08);
  --ink-line-2:     rgba(20,57,31,0.04);

  /* Accents */
  --avocado:        #5fa052;
  --avocado-dark:   #3f7536;
  --gold-warm:      #f5e9a0;   /* Hero-Glow + Shield-Icon */
  --coral:          #ff7849;   /* Unread-Badge, Heart-liked */

  /* Gradients */
  --grad-hero:      linear-gradient(135deg, #5fa052 0%, #2f6a3a 55%, #14391f 100%);
  --grad-bubble:    linear-gradient(135deg, #2f6a3a, #14391f);   /* Self-Chat-Bubble */
  --grad-send:      linear-gradient(135deg, #5fa052, #14391f);   /* Send-FAB */
  --grad-checkbox:  linear-gradient(135deg, #5fa052, #3f7536);
  --grad-progress:  linear-gradient(90deg, #5fa052, #14391f);
}
```

### Typografie

| Use | Family | Size | Weight | LS |
|---|---|---|---|---|
| TopBar-Title | Fraunces | 18 | 700 | −0.02em |
| Hero-Title | Fraunces | 26 | 700 | −0.025em |
| Post-Author | Fraunces | 14 | 700 | — |
| Post-Card-Title | Fraunces | 15 | 600 | −0.01em |
| Member-Name | Fraunces | 15 | 700 | −0.01em |
| Progress-Number | Fraunces | 24 | 700 | −0.02em |
| Hero-Stat-Number | Fraunces | 18 | 700 | — |
| Chat-Text | Inter | 13.5 | 500 | — |
| Body | Inter | 13 | 500–600 | — |
| Meta / Timestamp | Inter | 11 | 500 | — |
| Eyebrow (Hero, Sections) | Inter | 10 | 700 | 0.12–0.18em uppercase |
| Role-Tag | Inter | 8 | 700 | 0.12em |
| Tab-Label | Inter | 10 | 600–700 | 0.04em |

Families:
- Sans: `'Inter', system-ui, -apple-system, sans-serif`
- Serif: `'Fraunces', Georgia, serif`

### Spacing

| Token | Wert |
|---|---|
| Page horizontal padding | 14px |
| Card-Gap (Feed) | 12px |
| Member/Task-Row-Gap | 8px |
| Stories-Tile-Gap | 14px |
| Inner-Card-Padding | 12–14px |
| Section-Gap (Hero → Tabs) | 20px |
| Tab-Bar-Margin-bottom | 16px |

### Border-Radii

| Element | Wert |
|---|---|
| Hero-Banner | 24px |
| Post-Card | 18px |
| Member/Task-Row | 14px |
| Inner-Payload-Card | 14px |
| Bubble (Chat, self) | `18 18 4 18` |
| Bubble (Chat, fremd) | `18 18 18 4` |
| Tab-Pill | 11px |
| Tab-Container | 14px |
| Input | 14px |
| Icon-Tile (sm) | 12px |
| Icon-Tile (md) | 14–18px |
| Avatar | 50% |
| Pills / Badges | 999px |

### Shadows

```css
--shadow-pill:     0 2px 8px rgba(20,57,31,0.05);
--shadow-card:     0 4px 14px rgba(20,57,31,0.06);
--shadow-card-sm:  0 2px 8px rgba(20,57,31,0.04);
--shadow-hero:     0 14px 32px rgba(20,57,31,0.28);
--shadow-bubble:   0 4px 12px rgba(20,57,31,0.22);
--shadow-bubble-w: 0 2px 8px rgba(20,57,31,0.06);
--shadow-send:     0 6px 16px rgba(20,57,31,0.28);
--shadow-plus:     0 6px 14px rgba(20,57,31,0.22);
--shadow-reaction: 0 2px 6px rgba(20,57,31,0.08);
--shadow-invite:   0 4px 12px rgba(0,0,0,0.18);
```

---

## 8. Icons

Inline-SVG-Set, strokeWidth 2 (außer angegeben), `strokeLinecap: 'round'`, `strokeLinejoin: 'round'`.

| Name | Verwendung | Lucide-Äquivalent |
|---|---|---|
| `back` | TopBar zurück | `ChevronLeft` |
| `chev` | DE-Switcher | `ChevronDown` |
| `chevR` | (optional in Cards) | `ChevronRight` |
| `plus` | Create-Tile, Add-Task, Composer-FAB | `Plus` |
| `shield` | Aktive-Gruppe-Pill | `ShieldCheck` |
| `link` | Einladen, Invite-Card | `Link2` |
| `download` | Mealplan-Post-Icon, Plan-Quick-Pill | `Download` |
| `send-fill` (filled) | Send-FAB | `Send` (filled) |
| `check` | Checkbox done, Chat read-ack | `Check` |
| `circle` | (optional empty checkbox) | `Circle` |
| `list` | Aufgaben-Tab | `ListTodo` |
| `msg` | Chat-Tab | `MessageCircle` |
| `feed` | Feed-Tab | `LayoutList` |
| `users` | Mitglieder-Tab | `Users` |
| `heart` | Post-Like | `Heart` |
| `comment` | Post-Comment | `MessageSquare` |
| `more` | Post / Member-Menü | `MoreHorizontal` |
| `copy` | Einladungslink-Card | `Copy` |
| `emoji` | Composer | `Smile` |
| `attach` | Composer | `Paperclip` |

In Production durch `lucide-react` o.ä. ersetzen.

---

## 9. Avatar-System (`SAvatar`)

Reine CSS-Avatare aus Initialen + Gradient. Reproduzierbar pro Name.

```ts
const AVATAR_GRADIENTS = [
  ['#5fa052', '#3f7536'], // avocado
  ['#f5b942', '#d99a3c'], // gold
  ['#ff7849', '#c44e4e'], // coral
  ['#2f6a3a', '#14391f'], // forest
  ['#7a4a18', '#3a2510'], // bronze
  ['#8a6418', '#5a3d12'], // wheat
];

// Index = (charCode(name[0]) + charCode(name[1] || 0)) % 6
// Initials = first letter of each word, max 2, uppercase
// Font: Fraunces 700, size = avatar-size * 0.4, white text, ls −0.02em
// Shadow inner: inset 0 0 0 1px rgba(255,255,255,0.2)
// Optional ring: box-shadow: 0 0 0 2px white, 0 0 0 4px <ringColor>
```

**Props**: `name: string`, `size: number = 36`, `ring?: string`

---

## 10. Interactions & Behavior

| Element | Aktion |
|---|---|
| Stories-Tile | Wechselt aktive Gruppe (`setActiveGroup(id)`) — Hero + Body laden Gruppen-Daten |
| Stories-„Neu" | Öffnet Bottom-Sheet „Neue Gruppe erstellen" mit Name-Input + Erstellen-CTA |
| Hero „Einladen" | Öffnet Share-Sheet (native) mit Einladungslink |
| Tab-Click | Wechselt Body, behält aktive Gruppe + Scroll-Position pro Tab |
| Feed-Composer-Trigger | Öffnet Vollbild-Composer (Text / Meal-Plan-Token / Foto / Aufgabe) |
| Post-Heart | Toggle Like — optimistic UI, Count animiert (+1 von unten) |
| Post-„Öffnen" | Routet zu Plan/Aufgabe |
| Chat-Bubble lang gedrückt | Reaction-Picker (Pop-over mit 6 Emojis) |
| Chat-Send | Append zu `messages`, Composer leert, Auto-Scroll |
| Tasks-Checkbox | Toggle done, optimistic, Progress-Bar animiert |
| Member-Online-Dot | Pulsiert wenn `online === true` (1.6s ease, opacity 1→0.6→1) |
| Einladungslink-„Kopieren" | `navigator.clipboard.writeText(link)` + Toast „Link kopiert" |

### Animationen

- **Tab-Switch**: opacity 0→1 + translateY 6→0, 200ms ease-out, stagger 0
- **Bubble-Enter** (Chat): scale 0.92→1 + opacity, 220ms ease-out, transform-origin nach self (rechts/links)
- **Like-Toggle**: Heart skaliert 1→1.25→1 in 240ms cubic-bezier(.34,1.56,.64,1) + Color crossfade 160ms
- **Progress-Bar**: width-transition 400ms ease-out
- **Stories-Active-Switch**: ring-padding 0→2.5 in 180ms ease, label-weight 500↔700 instant

Reduced motion (`@media (prefers-reduced-motion: reduce)`): nur Opacity-Fades, keine Scales/Translates.

---

## 11. State

```ts
type GroupId = string;
type Tab = 'feed' | 'chat' | 'tasks' | 'members';
type Role = 'OWNER' | 'ADMIN' | 'MEMBER';

interface Group {
  id: GroupId;
  name: string;
  members: Member[];        // count = members.length
  posts: number;
  tasks: number;
  unread: number;
  inviteLink: string;
}

interface Member {
  id: string;
  name: string;
  role: Role;
  online: boolean;
  activity: string;          // "Aktiv jetzt · Beigetreten 14.04"
  joinedAt: ISO8601;
}

interface Post {
  id: string;
  type: 'mealplan' | 'text' | 'task';
  author: Member;
  time: string;              // "14:22" | "Gestern" | "Mo, 19.05"
  action: string;            // "hat einen Plan geteilt"
  // Payload by type
  title?: string;            // mealplan
  subtitle?: string;         // mealplan
  text?: string;             // text
  task?: string;             // task
  done?: boolean;            // task
  // Engagement
  likes: number;
  liked: boolean;            // current user
  comments: number;
  reactions?: string[];       // ['🥑','🔥']
  reactionCount?: number;
}

interface Message {
  id: string;
  author: string;            // member name (resolve avatar by name)
  text: string;
  time: string;
  self: boolean;             // sent by current user
  kind?: 'text' | 'sticker';
  reaction?: string;         // single emoji
}

interface Task {
  id: string;
  text: string;
  done: boolean;
  assignee?: string;
  due?: string;
}

interface ScreenState {
  activeGroup: GroupId;
  tab: Tab;
  draftMessage: string;
  draftTask: string;
  draftPost: string;
}
```

**Props der Root-Component** `<SharedScreen initialTab?>` — sonst stateless; alle Daten via Gruppen-API-Hooks (`useGroup(id)`, `useGroupPosts(id)`, `useGroupMessages(id)`, `useGroupTasks(id)`, `useGroupMembers(id)`).

---

## 12. Accessibility

- **TopBar-Back**: `aria-label="Zurück zur Startseite"`
- **Stories-Tiles**: `role="tab"`, `aria-selected={active}`, `aria-label="Gruppe {name} öffnen"`. Container `role="tablist"`.
- **Tab-Bar**: ebenfalls `role="tablist"` + `aria-controls` zu Tab-Bodies.
- **Hero „Einladen"**: `aria-label="Einladungslink zu {gruppe} teilen"`.
- **Post-Heart**: `aria-pressed={liked}`, `aria-label="{liked ? 'Like entfernen' : 'Liken'} — {n} Likes"`.
- **Chat-Bubble**: `role="article"`, eingehende Nachrichten als `aria-live="polite"` Region.
- **Composer-Input**: `aria-label="Nachricht eingeben"`, `aria-multiline="false"`.
- **Checkbox**: `role="checkbox"`, `aria-checked`, `tabindex="0"`, Space toggelt.
- **Kontrast**: white auf `#14391f` = 12.8:1 (AAA). `#9aa295` auf white = 3.1:1 (AA large only — nur für Meta-Text ≥ 14 px verwenden, nicht für Body).
- **Touch-Targets**: Stories-Tiles 56–60 px, Tab-Pills 50 px, Avatars 30–42 px (in Listen sind sie nicht klickbar — der gesamte Row ist es).
- **Reduced Motion**: siehe §10.

---

## 13. Responsive

- **Primär 390 px** (iPhone-Mini-App in World App)
- Auf größeren Screens: Container max-width 480 px, zentriert
- Stories-Row scrollt horizontal — auf > 480 px ggf. Wrap-Variante (Grid 4-Spalten) erwägen
- Hero-Stats bleiben 3-Spalten bis ≥ 320 px, darunter könnte 2+1 brechen (optional)

---

## 14. Files in this Bundle

```
design_handoff_shared_screen/
├── README.md              ← diese Datei
├── Shared.html            ← Lauffähiger Prototyp (Design-Canvas, 4 Tabs als Artboards)
├── shared.jsx             ← Komponenten:
│                            • SScreen (Root) — state + routing
│                            • STopBar
│                            • SStoriesRow
│                            • SGroupHero
│                            • STabBar
│                            • SFeedBody, SPostCard
│                            • SChatBody, SChatMessage, SDayDivider, SChatComposer
│                            • STasksBody
│                            • SMembersBody
│                            • SAvatar (Initials-Gradient-System)
│                            • SIcon (alle SVGs)
├── styles.css             ← App-weite Tokens (geteilt mit Home/Marketplace/Profile)
└── design-canvas.jsx      ← Layout-Helper (NICHT für Production — Authoring-only)
```

**Implementieren musst du nur:**
- `<SharedScreen>` (Root)
- Sub-Komponenten: `TopBar`, `StoriesRow`, `GroupHero`, `TabBar`, `FeedBody`/`PostCard`, `ChatBody`/`Bubble`/`Composer`, `TasksBody`, `MembersBody`
- `Avatar`-Komponente (oder Bibliothek)
- Icon-Mapping zu `lucide-react`

Alles unter `design-canvas.jsx` ist Authoring-Hilfe (Pan-/Zoom-Canvas für die Mockups) — nicht für Production.

---

## 15. Implementierungs-Hinweise

1. **Tab-State**: persistiere `(activeGroup, tab)` in der URL (`/shared/{groupId}/{tab}`) — der Nutzer landet beim Refresh in derselben Ansicht.
2. **Stories-Row**: nutze CSS `scroll-snap-type: x mandatory; scroll-snap-align: start` auf den Tiles — fühlt sich besser an als roher overflow-x.
3. **Avatar-System**: bau die `Avatar`-Komponente als reine UI-Komponente (keine API-Calls). Wenn User-Photos später dazukommen: fallback auf Initials wenn `photoUrl` fehlt.
4. **Chat-Auto-Scroll**: nur scrollen wenn der Nutzer NICHT manuell nach oben gescrollt hat (Bottom-Threshold ~80 px) — sonst zeigt ein „Neue Nachrichten ↓"-Toast unten.
5. **Optimistic UI**: Likes, Task-Done, Message-Send sofort lokal aktualisieren, bei Fail rollback + Toast.
6. **Reactions im Chat**: Long-Press öffnet Reaction-Picker als Pop-over (6 Emojis: 🥑 ❤️ 🔥 👍 😋 🙏). Position relativ zur Bubble.
7. **Backend-Schema**: `Group`, `Member`, `Post`, `Message`, `Task` siehe §11 — bitte mit Backend abstimmen.
8. **Realtime**: Chat + Posts via WebSocket / Pusher / Socket.io. Optimistic IDs werden ersetzt sobald Server bestätigt.
9. **Empty-States**: bauen für: keine Posts, keine Messages, keine Tasks, keine Mitglieder außer self — jeweils Cream-Card mit Eyebrow + freundlichem Text + CTA.
10. **Composer**: Vollbild-Modal mit Type-Switcher oben (Text / Plan / Aufgabe / Foto). Send-Button immer ganz unten — bei Tastatur-Open weiterhin sichtbar.

---

## 16. Changelog ggü. ursprünglichem Screen

| Vorher | Nachher |
|---|---|
| Liste „Meine Gruppen" mit Cards | **Stories-Row** mit Avatar-Carousel (visuell, schneller switchen) |
| „Neuen Gruppen-Feed anlegen" als statische Card im Layout | **„Neu"-Tile in Stories-Row** + Bottom-Sheet-Modal |
| Owner-Block als Profil-Karte mit Eyebrow | In **Hero-Banner integriert** (Member-Stack zeigt alle Mitglieder, nicht nur Owner) |
| Admin-Aktionen / Token / Todo / Chat / Feed alle untereinander | **Segmentierte Tab-Bar** mit 4 Bodies — nur das Relevante sichtbar |
| Chat: Bubbles dunkelgrün, alle Avocado-self, ungestaltete fremde Bubbles | **Asymmetrische Bubbles** (self = Gradient rechts mit `4 18 18 18`-Radius, fremd = white links mit umgekehrtem Radius) + Reactions + Day-Divider + Read-Check |
| Todo-Liste: Input + leere Card | **Progress-Card** mit Fraunces-XL-Zahl + Gradient-Bar + Assignee-Avatars pro Task |
| „Aktiver Feed"-Card mit „Einladungslink kopieren"-Block | Hero-Banner mit **inline „Einladen"-Pill** + Quick-Stats |
| Statische „Avocado OWNER"-Pille | Avatar-System + Role-Tag (`OWNER` als kleines Caps-Pill) |
| Kein Activity-Feed | **Feed-Tab** mit Post-Cards für `mealplan` / `text` / `task` + Reactions |

---

## 17. Offene Punkte

- [ ] Backend-Schema-Review mit Engineering
- [ ] Reaction-Picker-Set finalisieren (welche 6 Emojis?)
- [ ] Composer-Modal Detail-Designs (Plan-Picker, Foto-Upload, Mention `@user`)
- [ ] Push-Notification-Strategie (Unread-Badges in Stories-Row + Tab-Bar)
- [ ] Role-Berechtigungen (was darf MEMBER vs ADMIN vs OWNER?)
- [ ] Edit/Delete-Actions für eigene Posts/Messages/Tasks (Long-Press oder `more`-Menü)
- [ ] Mentions im Chat (Autocomplete + Render `@Lena` als Pill)
- [ ] Search innerhalb einer Gruppe (Top-Bar-Search-Toggle?)
- [ ] Dark Mode (Tokens dafür vorbereitet, nicht ausgearbeitet)
- [ ] i18n: alle Strings als Keys (DE/EN min.)
- [ ] Analytics-Events: `shared_tab_switch`, `shared_group_select`, `shared_post_create`, `shared_message_send`, `shared_task_toggle`, `shared_invite_copy`, `shared_like`

---

Bei Fragen oder Inkonsistenzen ggü. dem Mockup: **dieses Handoff** ist die verbindliche Spec.
