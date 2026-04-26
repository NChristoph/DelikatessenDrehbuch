# Handoff: Notification Overlay (Bottom Sheet)

## Overview
Redesign des **Benachrichtigungs-Overlays** in der Avocado-App. Das Overlay erscheint als **Bottom-Sheet über dem Feed** (Reel-Style Video-Hintergrund), nimmt **~2/3 (67%)** der Bildschirmhöhe ein, und folgt der gleichen Designsprache wie die neuen Marketplace-Karten (Cream-BG, Fraunces-Titel, Coral-Akzente, dunkelgrünes Ink-Brand).

## About the Design Files
Die Dateien in diesem Bundle sind **Design-Referenzen, erstellt in HTML/React** — Prototypen, die das beabsichtigte Aussehen und Verhalten zeigen. **Sie sind nicht für 1:1-Übernahme gedacht.** Aufgabe: Diese Designs im **bestehenden Codebase** der Avocado-App mit den dort etablierten Patterns nachbauen. Inline-Styles im Prototyp dienen als visuelle Spec, nicht als finale Implementierung.

## Fidelity
**High-fidelity.** Alle Farben, Typografie, Abstände, Border-Radii, Shadows und Interaktionen sind final spezifiziert.

## Scope
Nur das **Notification Bottom-Sheet** und seine Cards. Der Feed-Hintergrund existiert bereits in der App — das Sheet legt sich darüber.

## Layout

```
┌──────────────────────────────────────────────┐
│  [FEED-HINTERGRUND, 33% sichtbar]           │  ← bestehender Feed
│  ─ Top-Tabs (Creator/Feed/Marktplatz)       │
│  ─ Like-Button                              │
│                                              │
│  ════ DIM-SCRIM (rgba green + 2px blur) ═══│
├──────────────────────────────────────────────┤  ← Sheet beginnt bei 33% von oben
│         ▬▬▬  (Drag-Handle 38×4)              │
│  Benachrichtigungen  [3 neu]            [×] │
│  AI Vorschauen & wichtige Hinweise           │
│                                              │
│  [✓ Alles gelesen] [🗑 Leeren]    [📅 Plan]│
│                                              │
│  [Alle · 5 | Nur AI]         [Toggle Group] │
│                                              │
│  HEUTE (3) ─────────────────────────────    │
│  ┌──────────────────────────────────────┐   │
│  │▎[img] [✦AI] AI Vorschau...   09:16   │   │
│  │      Schweinebraten mit Linsen...    │   │
│  │      [🔥 680 kcal]        [Öffnen →] │   │
│  └──────────────────────────────────────┘   │
│  ... weitere Cards                          │
│  FRÜHER (2) ────────────────────────────    │
└──────────────────────────────────────────────┘
```

## Komponenten-Spezifikation

### 1. Sheet Container
- Position: `absolute`, bottom 0, left 0, right 0
- Height: `67%` der Viewport-Höhe
- Background: `#f5efe1` (Cream)
- Border-radius: top-left 28px, top-right 28px (bottom: 0)
- Box-shadow: `0 -12px 40px rgba(20,57,31,0.25)`
- Display: flex column, overflow hidden

### 2. Backdrop / Scrim
- Position: absolute, fills the screen
- Background: `linear-gradient(180deg, rgba(20,57,31,0.18) 0%, rgba(20,57,31,0.35) 60%, rgba(20,57,31,0.55) 100%)`
- `backdrop-filter: blur(2px)`
- Tap auf Scrim → schließt Sheet

### 3. Drag Handle
- 38×4 pill, `rgba(20,57,31,0.18)`
- Center, padding-top 8px

### 4. Header
- Padding: 8px 20px 14px
- Titel: Fraunces 22px / 600, color `#14391f`, letter-spacing -0.02em
- Unread-Badge (neben Titel): `linear-gradient(135deg, #ff9a3c, #ff7849)`, white, 10px/700, padding 2px 7px, radius 999px, format "{n} neu"
- Sub-Label: 11px, color `#6b7868`, marginTop 3px ("AI Vorschauen & wichtige Hinweise")
- Close-Button (rechts): 28×28 circle, bg `rgba(20,57,31,0.06)`, X-Icon strokeWidth 2.4, color `#14391f`

### 5. Action Row
3 Buttons, gap 8px, margin 0 16px 10px:

| Button | Style |
|---|---|
| **Alles gelesen** | white bg, border `1px rgba(20,57,31,0.12)`, radius 10, padding 7×11, 11px/600, color `#14391f`, Check-Icon |
| **Leeren** | white bg, border `1px rgba(255,120,73,0.28)`, radius 10, padding 7×11, 11px/600, color `#ff7849`, Trash-Icon |
| **Plan öffnen** | `linear-gradient(135deg, #ff9a3c, #ff7849)`, white, radius 10, padding 7×12, 11px/700, shadow `0 3px 10px rgba(255,120,73,0.3)`, margin-left auto |

### 6. Filter / Group Row
**Segmented Pill-Filter** (links):
- Container: bg `rgba(20,57,31,0.06)`, border `1px rgba(20,57,31,0.1)`, radius 999, padding 3px
- Inner-Pills: padding 5×11, 11px/600
- Aktiv: bg `#14391f`, color white
- Inaktiv: transparent, color `#14391f`
- Optionen: "Alle · {n}" / "Nur AI"

**Gruppieren-Toggle** (rechts):
- Mini-Switch 28×16, knob 12×12, radius 999
- ON: bg `#14391f`, knob right
- OFF: bg `rgba(20,57,31,0.2)`, knob left
- Label "Gruppieren" rechts, 11px/600, color `#14391f`

### 7. List
- Padding: 0 14px 20px, overflow-y auto
- **Section-Header** (wenn `grouping`): "HEUTE" / "FRÜHER" + Counter-Pill (10px/700, uppercase, ls 0.06em, color `#14391f`) + flex-1 hairline `rgba(20,57,31,0.08)`
- Gefiltert nach `time.match(/^\d/)` (Heute = Uhrzeiten, Früher = "Gestern" etc.)

### 8. Notification Card

**Container**:
- bg white, radius 16, padding 12, marginBottom 8
- box-shadow: `0 3px 10px rgba(20,57,31,0.05)`
- display: flex, gap 11px

**Unread-Strip** (left edge, wenn `unread`):
- Position absolute, top/bottom 0, left 0, width 3
- Background: `linear-gradient(180deg, #ff9a3c, #ff7849)`

**Thumb** (50×50, radius 11):
- Mit Bild: `cover`
- Ohne Bild: gradient (Plan: `#5fa052 → #3a7a30`, System: `#f5b942 → #c8a45c`)
- AI-Sparkle Badge (wenn type === 'ai'): 18×18 circle, top -3 right -3, gradient `#14391f → #1a4a28`, white border 2px, gold ✦ symbol

**Content (flex 1)**:
- Type-Badge:
  - AI: gradient `#14391f → #1a4a28`, white, 9px/700 uppercase, "✦ AI" (gold sparkle)
  - Plan: `rgba(95,160,82,0.12)` bg, color `#3a7a30`, 9px/700, "PLAN"
- Title (truncate): 11px/600, color `#14391f`, flex 1, ellipsis
- Time (right): 10px, color `#6b7868`
- Body: 12px/500, color `#1a2e20`, line-height 1.35, marginBottom 8px
- Bottom-Row:
  - kcal-Pill (wenn vorhanden): bg `#faf6ec`, 10px/600, color `#6b7868`, "🔥 {n} kcal" (orange flame)
  - Open-Button (margin-left auto): coral gradient, radius 9, padding 5×12, 10px/700 white, "Öffnen →"

## Data Shape

```typescript
type Notification = {
  id: string | number;
  type: 'ai' | 'plan' | 'system';
  title: string;
  body: string;
  time: string;          // "09:16" für heute, "Gestern" / "Vor 2 Tagen" etc.
  unread: boolean;
  kcal?: number;         // optional bei AI-Mahlzeiten
  img?: string;          // optional Thumbnail
};
```

## Interactions

| Aktion | Verhalten |
|---|---|
| Tap auf Scrim | Sheet schließt (slide down) |
| Drag Handle nach unten | Sheet schließt |
| Tap [×] | Sheet schließt |
| Tap "Alles gelesen" | Setzt alle `unread = false` |
| Tap "Leeren" | Confirm dialog → entfernt alle |
| Tap "Plan öffnen" | Navigiert zu Wochenplan |
| Tap Filter-Pill | Wechselt zwischen `all` und `ai` |
| Tap Gruppieren-Toggle | Schaltet Section-Headers an/aus |
| Tap Notification-Card | Markiert als gelesen, navigiert zu Detail |
| Tap "Öffnen"-Button | Direkter Sprung zum AI-Vorschau-Detail |

## Animation
- Sheet enter: slide-up von `translateY(100%)` zu `0`, 280ms `cubic-bezier(.2,.7,.3,1)`
- Sheet exit: slide-down, 220ms
- Scrim fade in/out: 200ms
- Filter/Toggle changes: 200ms ease

## Design Tokens
```css
--bg-cream: #f5efe1;
--ink-deep: #14391f;
--ink-text: #1a2e20;
--ink-mute: #6b7868;
--coral: #ff7849;
--orange: #ff9a3c;
--avocado: #5fa052;
--gold: #f5b942;
--card: #ffffff;
--card-soft: #faf6ec;
```

Typografie: **Fraunces** (serif, Titel) · **Inter** (sans, Body)

## Files in This Bundle
- `notification-panel.jsx` — `NotificationPanel` Komponente (Sheet + Cards)
- `Marketplace.html` — Lauffähiger Prototyp (Sektion "Benachrichtigungen · Im neuen Stil")
- `styles.css` — Design-Tokens und Shell-Styles

## Implementation Notes
1. Inline-Styles in das **bestehende Theme-System** (Tailwind / styled-components) übersetzen.
2. Sheet-Komponente sollte **wiederverwendbar** sein — nimm einen bestehenden Bottom-Sheet aus der App, falls vorhanden, und style nur den Inhalt.
3. Performance: Liste virtualisieren bei >50 Items (z.B. `react-window`).
4. Accessibility: 
   - Sheet hat `role="dialog"` + `aria-modal="true"`
   - Focus-Trap im Sheet
   - Escape schließt
   - Cards haben `aria-label` mit Titel + Zeit
5. Real-Time: WebSocket / Server-Sent Events für neue Notifications, Badge-Counter live updaten.
