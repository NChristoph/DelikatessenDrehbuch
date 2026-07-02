# Handoff: User Profile — Avocado Marketplace

## Überblick
Der **Profil-Screen** des eingeloggten Users: Header mit Identität/Stats, Quick-Action-Buttons, Admin-Zugang + Ad Manager, ausklappbares Creator Dashboard, und eine Tab-Navigation (Likes · AI · Creators · Created · Bought) mit den jeweiligen Inhaltslisten.

## Über die Design-Dateien (bitte zuerst lesen)
Die Dateien in diesem Bundle sind **Design-Referenzen, erstellt als HTML/React-Prototypen** (Babel-im-Browser, JSX). Sie zeigen das **gewünschte Aussehen und Verhalten** — sie sind **kein** produktiver Code zum direkten Übernehmen.

**Aufgabe:** Diesen Screen in der **bestehenden Umgebung des Ziel-Codebase** nachbauen (React, Vue, SwiftUI, native, …) — mit dessen etablierten Patterns, Komponenten und Design-Tokens. Inline-`style`-Objekte und `styles.css` machen alle Werte nur **explizit**; übersetze sie in den Ansatz des Codebase (Tokens, CSS-Variablen, Utility-Klassen, …). Alle visuellen Werte sind unten zusätzlich aufgelistet.

## Fidelity
**High-Fidelity (hifi).** Finale Farben, Typografie, Abstände, Radien und Schatten sind unten und im Quellcode spezifiziert.

## Dateien
- `User Profile.html` — Entry, lädt React + Babel und rendert alle Artboards in einem Design Canvas.
- `profile.jsx` — alle Komponenten (Top-Bar, Profile-Card, Admin-Banner, Tab-Bar, Tab-Bodies, Creator-Dashboard).
- `styles.css` — geteiltes Marketplace-Stylesheet (`.mp-screen`, `.mp-feed`).
- `design-canvas.jsx` — Pan-/Zoom-Canvas, Sections + Artboards (nur fürs Vorschau-Bundle, keine Produktionsabhängigkeit).

## Design-Tokens
| Rolle | Wert |
|---|---|
| Background (Page) | `#f5efe1` |
| Card surface | `#ffffff` |
| Cream (chips, sub-cards) | `#faf6ec` |
| Profile-Card-Verlauf | `linear-gradient(160deg,#eaf6ec 0%,#e2f2e5 100%)` (helles Mint) |
| Neutrale Segment-Hülle (Tab-Bar, Admin-Banner) | `rgba(20,20,20,0.045)` |
| Forest Green (primary) | `#14391f` |
| Forest Green Dark | `#0e2415` |
| Avocado (success / WLD) | `#5fa052` |
| Avocado Dark | `#3f7536` |
| Gold/Premium-Akzent | `#f5b942` |
| Coral (delete-Icons) | `#ff7849` |
| Amber (eye-off, pending) | `#d99a3c` |
| Crimson (delete fill) | `#c44e4e` |
| Ink Mute | `#6b7868` |
| Ink Soft | `#9aa295` |

## Typografie
- Display / Werte: **Fraunces** 600/700, letterspacing −0.02em.
- UI Text: **Inter** 500/600/700.
- Caps-Labels (Stats / Tab-Labels): Inter 700, 9.5–11px, letterspacing 0.08–0.18em.

## Anatomie (oben nach unten)

### 1. Top-Bar
Pillförmiger `← Zurück zum Feed`-Button links (weiß, Schatten). Rechts ein Sprach-Umschalter-Pill: kleines „GB"-Flag-Badge (cream bg) + aktueller Code (`EN`/`DE`) + Chevron. Klick togglet die Sprache. Beide Buttons weiß mit `box-shadow: 0 2px 8px rgba(20,57,31,0.06)`.

### 2. Profile-Card
Gerundete Card (Radius 24), Hintergrund **helles Mint-Grün** (`linear-gradient(160deg,#eaf6ec,#e2f2e5)`), Border `1px rgba(20,57,31,0.06)`.
- **Identität**: Avatar 76×76, Radius 22, dunkelgrüner Verlauf (`#1a3a23→#0e2415`), Initialen in Fraunces 30/700 weiß. Daneben Name (Fraunces 26/700) + `@handle` (Inter 13, `--ink-mute`). Kein „Verified"-Badge in dieser Version.
- **Stats**: genau **2 Werte** nebeneinander — `Following` und `Likes` (Fraunces 22/700 groß + darunter Caps-Label 10px/700, `--ink-soft`).
- **Primary-Action**: `Einstellungen`-Button, volle Breite, dunkelgrüner Verlauf, Zahnrad-Icon.
- **Quick-Actions**, 2×2-Grid, alle gleich gestylt (cream `#faf6ec` bg, `1px rgba(20,57,31,0.08)` border, Radius 16, Icon + Label):
  1. **Geteilte Inhalte** (Send/Share-Icon)
  2. **Marketplace** (Shop-Icon)
  3. **My listings** (Bag-Icon)
  4. **Verkaufsplan erstellen** (Doc-Plus-Icon, zweizeiliges Label)

### 3. Admin-Zugang + Ad Manager
Direkt unter der Profile-Card, im **selben neutralen, hellen Look wie die Tab-Bar** (kein knalliger Farbblock):
- **Admin-Zugang-Karte**: Hülle `rgba(20,20,20,0.045)`, Radius 22, Padding 14/16. Kopfzeile: Schild-Icon + „ADMIN-ZUGANG FREISCHALTEN" (Inter 12/700, uppercase, `--ink-deep`). Darunter eine Reihe: PIN-Eingabefeld (weiß, Radius 16, Schatten `0 2px 8px rgba(20,57,31,0.06)`, Placeholder „PIN eingeben...") + runder Submit-Button (40×40, `--ink-deep` bg, weißer Pfeil-Icon).
- **Ad Manager**-Button darunter: eigenständige weiße Pill (nicht volle Breite, linksbündig), Radius 999, Schatten wie andere Quick-Actions, Megafon-Icon + Label, Textfarbe `--ink-deep`.
- **Wichtig**: Keine Fremdfarben (kein Blau/Lila) — dieser Block muss visuell zur Forest-Green/Cream-Palette des restlichen Screens passen, nicht wie ein fremdes Modul wirken.

### 4. Creator-Dashboard-Toggle
Full-width Pill, schaltet zwischen weiß (closed) und dunkelgrün (open) um. Caret (▾/▴) zeigt Status. Öffnet/schließt den Dashboard-Block darunter.

### 5. Tab-Bar
Segmentiertes Steuerelement in einer neutralen Hülle `rgba(20,20,20,0.045)` (bewusst **kein** Grünstich — neutral grau/schwarz-transparent). Aktiver Tab = weiße Kapsel mit Schatten, dunkelgrünem Icon + Label. Fünf Tabs (in dieser Reihenfolge, Labels wie im Original in Großbuchstaben):
1. **Likes** (gefülltes Herz)
2. **AI** (Sparkle)
3. **Creators** (People-Icon)
4. **Created** (Kalender-Icon)
5. **Bought** (Bag-Icon)

> Es gibt in dieser Version **keinen separaten „Videos"-Tab** mehr (zuvor 6 Tabs, jetzt 5).

### 6. Tab-Inhalte
- *Likes*: 3-Spalten-Grid, 9:14 Ratio, weiche Schatten, Play-Badge unten links.
- *AI*: gleiches Grid + „Angepasst"-Pill oben links + dunkler Gradient-Footer mit Titel & „Variante"-Tag.
- *Creators* (ehem. Abos): Listen-Card mit Avatar + Name + Chevron.
- *Created* (ehem. Erstellt): Card mit Name, Datum, schwarz-grünem `OPEN`-Pill und coral-getöntem Trash-Button.
- *Bought* (ehem. Gekauft): Card mit Preis (Fraunces), tx-Hash-Zeile und dunkelgrünem `Plan öffnen`-Button.

### 7. Creator Dashboard (aufgeklappt)
- **Dark Hero-Card** (Forest-Verlauf) mit „PREMIUM CREATOR AREA"-Eyebrow (goldlich getönt), Fraunces-Heading „Creator Dashboard", User-Hash + Kopieren-Button.
- **„Verfügbar"-Card**: XL-Fraunces-Wert (WLD) + Avocado-Verlauf-CTA „Geld anfordern".
- **2×2 Stat-Tile-Grid**: Verkäufe · Verfügbar · Ausstehend · Bereits erhalten.
- **Watch-Analytics**: Heading-Card mit 4 Stat-Tiles + 2 Cream-Highlight-Boxen (Von dir angesehen, Deine Watch-Time).
- **Top Clips**: rangierte Liste mit Avocado-Akzent-Zahl + Ø-Watch-Sekunden.
- **Sales Activity**: Cream-Rows mit grünem Check-Badge, Name, Datum, `+0,8 WLD`, `Details ▾`. Footer-Buttons: `Geld anfordern` (Avocado-Verlauf) + `Listings` (weiß, Outline).

## Komponenten-API (profile.jsx)
- `<PScreen initialTab="likes" dashboardOpen={false} />` — Root, hält Tab- und Dashboard-State.
- `<PTopBar />`, `<PProfileCard />`, `<PAdminBanner />`, `<PDashboardToggle open onClick />`, `<PTabBar active onChange />`.
- Tab-Bodies: `<PLikesTab />`, `<PAITab />`, `<PAbosTab />`, `<PErstelltTab />`, `<PGekauftTab />`.
- `<PCreatorDashboard />` — kompletter Dashboard-Inhalt.
- `<PStatTile icon label value unit accent />` — wiederverwendbares Stat-Pattern.
- `<PMiniBtn icon label active multiline onClick />` — Quick-Action-Button.
- `<PIcon name size color />` — internes Icon-Set (Stroke-SVG); enthält u. a. `send`, `docPlus`, `shield`, `arrowR`, `megaphone` für die neuen Blöcke.

## State / Daten-Schema
```ts
type Profile = {
  name: string;
  handle: string;        // ohne '@'
  hashShort: string;     // 0x...XXXX
  initials: string;      // 2 Buchstaben für Avatar
  stats: { following: number; likes: number };
};

type SaleEntry = { name: string; date: string; amount: string }; // "+0,8 WLD"

type DashboardData = {
  available: { value: number; unit: 'WLD' };
  metrics: { sales: number; pending: number; received: number };
  watch: { qualified: number; viewers: number; minutes: number; avgSec: number };
  selfWatch: { clips: number; minutes: number };
  topClips: { title: string; views: number; mins: string; avg: string }[];
  sales: SaleEntry[];
};
```
Lokaler UI-State im Prototyp: `tab` (aktiver Tab-Key), `open` (Dashboard auf/zu), `lang` (`EN`/`DE`), `pin` (Eingabewert im Admin-Feld).

## Interaktionen & Verhalten
- **Tab-Wechsel** → Tab-Bar bleibt sichtbar, Inhalt darunter tauscht.
- **Dashboard-Toggle** → klappt Dashboard zwischen Profile-Card/Admin-Block und Tab-Bar ein/aus; Button-Farbe kehrt sich im offenen Zustand um.
- **Admin-PIN-Feld + Pfeil-Button** → in Produktion: PIN validieren, bei Erfolg Admin-Modus/-Ansicht freischalten. Im Prototyp nur UI, kein Backend-Call.
- **Ad Manager**-Button → navigiert zum Ad-Manager-Bereich (placeholder).
- **Geld anfordern** → löst Wallet-Payout-Flow aus (placeholder).
- **OPEN / Plan öffnen** → navigiert zum jeweiligen Plan-/Listing-Detail.
- **Trash-Buttons** → brauchen Confirm-Dialog (nicht im Mock).
- **Sprach-Toggle** → `EN ⇄ DE`, wirkt aktuell nur lokal auf das Label.

## Responsives Verhalten
- Innenbreite 390px (iPhone). Auf größeren Screens: Card zentriert, max-width ~480px.
- Tab-Bar bei <360px: Icons bleiben, Caps-Label kann auf 9px schrumpfen.

## Assets
- **Icons**: inline 24×24-Stroke-SVGs, komplettes Set in `PIcon` (u. a. `back`, `chev`, `cog`, `shop`, `bag`, `send`, `docPlus`, `shield`, `arrowR`, `megaphone`, `heartF`, `sparkle`, `people`, `cal`, `play`, `check`, `trash`, `pen`, `eye-off`, `video`, `swap`, `extLink`, `cash`, `view`, `crowd`, `clock`, `stopwatch`, `trend`). Durch das Icon-Set des Codebase ersetzen.
- **Fonts**: Fraunces + Inter via Google Fonts — Font-Pipeline des Codebase nutzen.
- **Bilder** (Likes/AI/Videos-Grids): aktuell Unsplash-Platzhalter-URLs — durch echte User-/Content-Medien ersetzen.

## Checklist Hand-off
- [x] Mint-Grün Profile-Card statt Cream/Pfirsich-Verlauf
- [x] Nur 2 Stats (Following, Likes), kein Verified-Badge
- [x] Quick-Actions: Geteilte Inhalte / Marketplace / My listings / Verkaufsplan erstellen
- [x] Admin-Zugang- + Ad-Manager-Block, farblich an Forest-Green/Cream/Gold angeglichen (kein Blau/Lila)
- [x] Tab-Bar umbenannt (Creators/Created/Bought), Videos-Tab entfernt
- [ ] Real-Daten-Wiring (Wallet, API, Admin-Auth)
- [ ] Empty-States für leere Tabs (kein Like, keine Videos…)
- [ ] Dark Mode (out of scope)

## Bekannte Historie
- Frühere Version hatte einen blau/lila Admin-Block (Farbfremdkörper) — wurde auf die App-eigene Forest-Green/Gold/Cream-Palette umgestellt.
- Frühere Version hatte 3 Stats (Abos/Likes/Follower) + „Orb verified"-Badge — in dieser Version auf 2 Stats reduziert, Badge entfernt.

## Starten
`User Profile.html` im Browser öffnen — React + Babel kommen vom CDN, JSX wird im Browser transpiliert. **Für Produktion JSX vorkompilieren** und in die Build-Pipeline des Codebase integrieren.
