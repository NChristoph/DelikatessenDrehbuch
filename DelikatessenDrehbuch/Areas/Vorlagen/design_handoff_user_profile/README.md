# User Profile · Handoff (Avocado-Stil)

Profil-Screen mit Header, Quick-Actions, Tab-Navigation (Likes · AI · Abos · Erstellt · Gekauft · Videos) und ausklappbarem Creator Dashboard.

## Dateien
- `User Profile.html` — Entry, lädt React + Babel und rendert alle 7 Artboards in einem Design Canvas.
- `profile.jsx` — alle Komponenten (Top-Bar, Profile-Card, Tab-Bar, Tab-Bodies, Creator-Dashboard).
- `styles.css` — geteiltes Marketplace-Stylesheet (`.mp-screen`, `.mp-feed`).
- `design-canvas.jsx` — Pan-/Zoom-Canvas, Sections + Artboards.

## Design-Tokens (alle Cream-/Avocado-Werte)
| Rolle | Hex |
|---|---|
| Background (Page) | `#f5efe1` |
| Card surface | `#ffffff` |
| Cream (chips, sub-cards) | `#faf6ec` |
| Hero-Verlauf (Profile-Card) | `linear-gradient(160deg,#f7f1e0 0%,#f1e6d4 60%,#f6d8c4 100%)` |
| Forest Green (primary) | `#14391f` |
| Forest Green Dark | `#0e2415` |
| Avocado (success / WLD) | `#5fa052` |
| Avocado Dark | `#3f7536` |
| Coral (only delete-icons) | `#ff7849` |
| Amber (eye-off, pending) | `#d99a3c` |
| Crimson (delete fill) | `#c44e4e` |
| Ink Mute | `#6b7868` |
| Ink Soft | `#9aa295` |

## Typografie
- Display / Werte: **Fraunces** 600/700, letterspacing −0.02em.
- UI Text: **Inter** 500/600/700.
- Caps-Labels (Stats / Tab-Labels): Inter 700, 9.5–11px, letterspacing 0.08–0.18em.

## Anatomie
1. **Top-Bar** — pillförmiger Back-Button + DE-Selector. Beide weiß, soft Shadow.
2. **Profile-Card** — gerundete Card (24r) mit Pfirsich-Cream-Verlauf, AV-Avatar (76×76, dunkelgrün, Fraunces). ORB-VERIFIED-Pill mit grünem Check. Stats-Reihe (Fraunces big number + Caps-Label). Primary-Action `Einstellungen` (gradient forest), darunter 2×Mini-Buttons + 1× full-width Button-Reihe.
3. **Creator-Dashboard-Toggle** — full-width pill, schaltet zwischen weiß (closed) und dunkel-grün (open) um. Caret zeigt Status.
4. **Tab-Bar** — segmentiertes Steuerelement in `rgba(20,57,31,0.06)`-Hülle. Aktiver Tab = weiße Kapsel mit Schatten und dunkelgrünem Icon/Label.
5. **Tab-Inhalte**
   - *Likes / Videos*: 3-Spalten-Grid, 9:14, weiche Schatten.
   - *AI*: gleiche Grid + "Angepasst"-Pill oben + dunkler Gradient-Footer mit Titel & "Variante"-Tag.
   - *Abos*: Listen-Card mit Avatar.
   - *Erstellt*: Card mit Datum, schwarz-grünem `OPEN`-Pill und coral-getöntem Trash-Button.
   - *Gekauft*: Card mit Preis (Fraunces) + tx-Hash + dunkelgrünem `Plan öffnen`.
   - *Videos*: Tile mit Action-Stack (Edit grün / Hide amber / Delete crimson) + Camera-Badge.
6. **Creator Dashboard** (offen)
   - Dark Hero-Card (forest gradient) mit "PREMIUM CREATOR AREA"-Eyebrow, Fraunces-Heading, Hash-Code + Kopieren.
   - "Verfügbar"-Card mit XL-Fraunces-Wert + Avocado-Gradient-CTA.
   - 2×2 Stat-Tile-Grid (Verkäufe / Verfügbar / Ausstehend / Bereits erhalten).
   - Watch-Analytics: Heading-Card mit 4 Stat-Tiles + 2 Cream-Highlight-Boxen (Von dir angesehen, Watch-Time).
   - Top-Clips: rangierte Liste, Avocado-Akzent.
   - Sales-Activity: Cream-Rows mit grünem Check, Datum, +0,8 WLD und `Details ▾`. Footer-Buttons: `Geld anfordern` (Avocado) + `Listings` (weiß).

## Komponenten-API (profile.jsx)
- `<PScreen initialTab="likes" dashboardOpen={false} />` — Root. Tab- und Dashboard-State live.
- Sub-Komponenten exportiert nicht — alle in der Datei vereint.
- `<PStatTile icon label value unit accent />` — wiederverwendbares Stat-Pattern.
- `<PMiniBtn icon label active multiline />` — quick-action Button.
- `<PIcon name size color />` — interner Icon-Set (Stroke-SVG).

## State / Daten-Schema
```ts
type Profile = {
  name: string;
  handle: string;        // ohne '@'
  hashShort: string;     // 0x...XXXX
  initials: string;      // 2 Buchstaben für Avatar
  verified: boolean;
  stats: { abos: number; likes: number; follower: number };
};

type SaleEntry = {
  name: string; date: string; amount: string; // "+0,8 WLD"
};

type DashboardData = {
  available: { value: number; unit: 'WLD' };
  metrics: { sales: number; pending: number; received: number };
  watch: { qualified: number; viewers: number; minutes: number; avgSec: number };
  selfWatch: { clips: number; minutes: number };
  topClips: { title: string; views: number; mins: string; avg: string }[];
  sales: SaleEntry[];
};
```

## Verhalten
- **Tab-Wechsel** → klar abgrenzbarer State. Tab-Bar bleibt sichtbar; Inhalte tauschen.
- **Dashboard-Toggle** klappt das Dashboard zwischen Header und Tab-Bar ein. Toggle-Button kehrt im offenen Zustand seine Farbe um.
- **Geld anfordern** sollte Wallet-Flow auslösen (placeholder).
- **OPEN / Plan öffnen** navigiert zum Plan-Detail.
- **Trash-Buttons** brauchen Confirm-Dialog (nicht im Mock).

## Responsives Verhalten
- Innenbreite 390px (iPhone). Auf größeren Screens: Card centered, max-width ~480px.
- Tab-Bar bei <360px: Icons bleiben, Caps-Label kann auf 9px schrumpfen — bereits eingestellt.

## Checklist Hand-off
- [x] Avocado/Forest Green statt Coral als Primary
- [x] Cream Background statt grau
- [x] Fraunces für Display + Zahlen
- [x] Segmentierte Tab-Bar (statt 6 lose Pills)
- [x] Verifiziert: Profile-Card, Dashboard-Header, Stat-Tiles
- [ ] Real-Daten Wiring (Wallet, API)
- [ ] Empty-States für leere Tabs (kein Like, keine Videos…)
- [ ] Dark Mode (out of scope)

## Bekannte Inkonsistenzen ggü. Original-Screens
- Tipo-Fehler **„Deschbort"** im Original wurde zu **„Dashboard"** korrigiert.
- **„Plan Ã¶ffnen"** Encoding-Bug → **„Plan öffnen"**.
- AV-Avatar war zuvor blaues Square — jetzt forest-green Rounded-Square (passt zur Brand).
