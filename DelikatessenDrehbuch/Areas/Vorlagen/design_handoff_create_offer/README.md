# Handoff: „Angebot erstellen" (Create Offer) — Avocado Marketplace

## Überblick
Der **„Angebot erstellen"**-Screen, mit dem ein Verkäufer ein neues Listing im Marketplace anlegt. Der Verkäufer wählt einen **Angebotstyp**, beschreibt das Angebot, legt einen **Preis in WLD** fest und verbindet eine **Auszahlungs-Wallet**. Jeder Verkauf wird **80/20** zwischen Verkäufer und Plattform aufgeteilt.

Dieses Bundle enthält **zwei Design-Richtungen** derselben Funktion:

1. **Formular-Variante** (`Create Offer.html` + `create-offer.jsx`) — **das ist die in diesem Handoff primär beschriebene Version.** Klassischer, durchnummerierter Formular-Flow (1 → Typ, 2 → Beschreibung, 3 → Preis). Vollständige typ-spezifische Feldsätze, zwei Wallets (WLD + USDT/USDC), Sprach-Toggle.
2. **Social-Composer-Variante / v2** (`Create Offer v2.html` + `create-offer-v2.jsx`) — Alternative im Stil eines Social-Media-Post-Composers (Foto-Upload zuerst, Typ als Pills, Live-Feed-Vorschau). Detailbeschreibung siehe Abschnitt „Variante B" unten.

> Wenn nur **eine** Version umgesetzt werden soll: nimm die **Formular-Variante** (Abschnitt „Variante A"). Sie ist der vollständigere, validierte Flow.

---

## Über die Design-Dateien (bitte zuerst lesen)
Die Dateien in diesem Bundle sind **Design-Referenzen, erstellt als HTML/React-Prototypen** (Babel-im-Browser, JSX). Sie zeigen das **gewünschte Aussehen und Verhalten** — sie sind **kein** produktiver Code zum direkten Übernehmen.

**Aufgabe:** Diese HTML-Designs in der **bestehenden Umgebung des Ziel-Codebase** nachbauen (React, Vue, SwiftUI, native, …) — mit dessen etablierten Patterns, Komponenten und Design-Tokens. Falls noch keine Umgebung existiert: das für das Projekt am besten passende Framework wählen und die Designs dort umsetzen.

Inline-`style`-Objekte, der `<style>`-Block und `styles.css` dienen nur dazu, alle Werte **explizit** zu machen. Übersetze sie in den Ansatz des Codebase (Tokens, CSS-Variablen, Utility-Klassen, Styled-Components, …). Alle visuellen Werte sind unten zusätzlich aufgelistet.

## Fidelity
**High-Fidelity (hifi).** Finale Farben, Typografie, Abstände, Radien und Schatten sind unten und im Quellcode spezifiziert. Zuerst pixelgenau nachbauen, dann auf die Tokens/Komponenten des Codebase mappen.

---

# Variante A — Formular-Variante (primär)
Dateien: `Create Offer.html`, `create-offer.jsx`, `styles.css`.

## Screen / View

### Screen: „Angebot erstellen"
Vertikal scrollender Screen in einem 390px-Phone-Frame. Hintergrund **Cream `#f5efe1`** (`var(--bg-cream)`, via `.mp-screen`). Der Inhalt liegt in einem scrollenden Container `.co-scroll` (padding `16px 16px 40px`, vertikaler Stack, gap `14px`).

Aufbau von oben nach unten:

#### 0. Sprach-Toggle (schwebend)
- Position: `absolute`, `top 14px / right 16px`, `z-index 30`.
- Pill: bg weiß, border `1px var(--ink-line)`, radius `999px`, padding `6px 11px`, Inter `12px / 600`, Farbe `var(--ink-deep)`, Schatten `var(--shadow-sm)`.
- Inhalt: kleines Flag-Badge „GB" (`9px / 700`, bg `var(--bg-cream-2)`, Farbe `var(--ink-mid)`, padding `2px 4px`, radius `4px`) · aktueller Code („EN"/„DE") · Chevron (opacity 0.6).
- Klick togglet `EN ⇄ DE`. *(Im Prototyp wird nur das Label umgeschaltet; in Produktion die i18n-Sprache umstellen.)*

#### 1. Zurück-Link
- `.co-back`: Textbutton, kein Hintergrund, Inter `14px / 600`, `var(--ink-deep)`, Back-Icon + „Zurück zum Marktplatz". Navigiert zurück zum Marketplace-Feed.

#### 2. Intro-Karte (`.co-intro`)
- bg weiß (`var(--card)`), radius `var(--radius-lg)` (22px), padding `20px 20px 22px`, Schatten `var(--shadow-sm)`.
- **H1** „Angebot erstellen": **Fraunces** `30px / 600`, tracking `-0.025em`, line-height `1.04`, Farbe `var(--ink-deep)`.
- **Absatz**: Inter `14px`, line-height `1.55`, `var(--ink-mute)`, `text-wrap: pretty`. Text: „Wähle einen Angebotstyp, beschreibe ihn und leg einen Preis in WLD fest. Optional kannst du zusätzlich eine USDT/USDC-Auszahlungsadresse speichern."

#### 3. Wallet-Karten (2×) (`.co-wallet`)
Dunkle Karte, bg `linear-gradient(135deg,#1f4329,#0e2415)`, radius `var(--radius-lg)`, padding `16px 18px`, Schatten `var(--shadow-md)`, weiße Schrift. Row mit space-between.
- **Text links**: Titel `13.5px / 600`. Darunter, je nach Status:
  - *Nicht verbunden*: `.co-wallet-warn` — `12.5px`, Farbe `var(--gold)`, fett-Teil weiß-ish; Text z. B. „**Nicht verbunden** — Wallet wird für Auszahlung benötigt".
  - *Verbunden*: `.co-wallet-ok` — `12.5px`, `rgba(255,255,255,0.85)`, grüner Punkt `.co-dot` (7×7, `var(--avocado)`, Ring `0 0 0 3px rgba(95,160,82,0.25)`) + „Verbunden · {addr}".
- **Button rechts** `.co-wallet-btn`: 78×56, radius `16px`, bg `rgba(255,255,255,0.06)`, border `1px rgba(255,255,255,0.30)`, weiße Schrift `11.5px / 600`, Wallet-Icon über Label. Hover bg `rgba(255,255,255,0.13)`. Verbunden (`.is-on`): bg `rgba(95,160,82,0.22)`, border `rgba(95,160,82,0.55)`, Label „Trennen" (sonst „Verbinden").
- **Karte 1**: „Deine Auszahlungs-Wallet (WLD)", addr „2.84 WLD". **Pflicht** zum Verkaufen.
- **Karte 2**: „Deine Auszahlungs-Wallet (USDT/USDC)", addr „0x8f…a3d". **Optional**.

#### 4. Split-Banner (`.co-banner`)
- bg `#f6ead0`, border `1px #e9d3a3`, radius `var(--radius-md)`, padding `13px 15px`, Inter `13px`, line-height `1.5`, Farbe `#715a24`. Info-Icon links (`var(--ink-deep)`).
- Text: „**80% für dich** — Bei jedem Verkauf bekommst du automatisch 80% des Preises direkt auf deine Wallet. 20% gehen als Plattform-Gebühr an uns." (fett-Teile in `var(--ink-deep)`).

#### 5. Sektion 1 — „Was möchtest du anbieten?" (`.co-block`)
Karte (bg weiß, radius `var(--radius-lg)`, padding `18px 18px 20px`, Schatten `var(--shadow-sm)`). Kopf = nummeriertes Badge `.co-num` (24×24, radius `8px`, bg `var(--ink-deep)`, weiße Fraunces `13px / 600`, Inhalt „1") + Titel `.co-block-title` (`15.5px / 700`, `var(--ink-deep)`).

**Typ-Grid** `.co-types`: `grid-template-columns: repeat(3, 1fr)`, gap `9px`. Fünf Kacheln `.co-type`:
- Default: column-Stack zentriert, padding `16px 6px 13px`, bg `var(--card-soft)`, border `1.5px var(--ink-line)`, radius `16px`. Icon (26×26 stroke, `var(--ink-deep)`) + Label `12.5px / 600` (`var(--ink-deep)`). Hover: border `rgba(20,57,31,0.28)`.
- **Ausgewählt** (`.sel`): border `2px var(--ink-deep)`, bg weiß, Schatten `var(--shadow-sm)`, Icon-Farbe → `var(--avocado)`.
- Typen (id · Label · Icon): `plan` · **Essensplan** · calendar · `recipe` · **Einzelrezept** · doc · `object` · **Objekt** · cube · `digital` · **Digital** · download · `service` · **Dienstleistung** · service.

**Konditionaler Sub-Block** `.co-sub` (unter dem Grid; `margin-top 16px`, `padding-top 16px`, oben `1px var(--ink-line)`), wechselt mit dem gewählten Typ:
- **Essensplan** → Sub-Label „Plan auswählen" + Liste `.co-pick`-Karten (bg weiß, border `1.5px`, radius `14px`, padding `14px 16px`; Name **Fraunces 17px / 600**, Meta „Erstellt: {Datum}" `12.5px var(--ink-mute)`). Ausgewählt (`.sel`): border `2px var(--ink-deep)`, bg `var(--card-soft)`, grüner Check oben rechts (`.co-pick-check`, 22×22 Kreis, bg `var(--avocado)`). Beispiel-Pläne: „Feed-Plan" (17.05.2026), „Teste das" (22.05.2026). **Pflicht.**
- **Einzelrezept** → „Rezept auswählen" + gestyltes `<select>` `.co-select` (bg weiß, border `1.5px`, radius `12px`, padding `13px 38px 13px 14px`, eigener Chevron rechts). Optionen z. B. „Avocado Toast Deluxe", „Protein Power Bowl", „Green Detox Smoothie", „Overnight Oats". **Pflicht.**
- **Objekt** → „Produktdetails": Bild-URL-Input, Bestand-Input („leer = unbegrenzt"), Checkbox-Row `.co-checkrow` „Versand nötig (Lieferadresse vom Käufer abfragen)" (`.co-box` 22×22, radius `7px`; aktiv `.on` → bg `var(--ink-deep)` + Check).
- **Digital** → „Digitales Produkt": Download-/Datei-URL-Input + optionaler Titelbild-URL-Input. Hinweis `.co-hint`: „Der Link wird erst nach dem Kauf freigeschaltet."
- **Dienstleistung** → „Dienstleistung": optionaler Titelbild-URL-Input. Hinweis: „Beschreibe deine Dienstleistung unten. Nach dem Kauf wirst du benachrichtigt, um Kontakt aufzunehmen."

#### 6. Sektion 2 — „Angebot beschreiben" (`.co-block`, Badge „2")
- Titel-Input `.co-input` (bg weiß, border `1.5px var(--ink-line)`, radius `12px`, padding `13px 14px`, Inter `14px`; placeholder „Titel für dein Angebot", Farbe `#9aa295`; Focus: border `var(--avocado)` + Ring `0 0 0 3px rgba(95,160,82,0.14)`).
- Beschreibungs-Textarea `.co-input.co-area` (3 Zeilen, `resize: none`, line-height `1.5`; placeholder „Kurze Beschreibung (optional)"). **Optional.**

#### 7. Sektion 3 — „Preis festlegen" (`.co-block`, Badge „3")
- Row `.co-price-row` (gap `14px`): Preis-Box `.co-price-box` (bg weiß, border `1.5px`, radius `14px`, padding `10px 18px`, min-width `120px`) mit zentriertem Input `.co-price-input` (**Fraunces 32px / 600**, `var(--ink-deep)`, kein Rahmen). Daneben Einheit `.co-price-cur` „WLD" (`19px / 700`, `var(--ink-mute)`). Eingabe wird auf `[0-9.]` gefiltert.
- Split-Zeile `.co-split` (`13px`, `var(--ink-mute)`): „Du bekommst: **{price×0.8} WLD** (80%) — Plattform: {price×0.2} WLD (20%)" (fett-Teile `var(--ink-deep)`).

#### 8. CTA + Hinweis
- Button `.co-cta`: volle Breite, bg `linear-gradient(135deg,#1a3a23,#0e2415)`, radius `18px`, padding `17px`, Inter `15.5px / 700`, weiß, Schatten `0 8px 22px rgba(20,57,31,0.26)`, Tag-Icon + „Jetzt verkaufen". Hover: `translateY(-1px)` + stärkerer Schatten. Active: zurück auf 0.
- **Disabled** (`.disabled`, `disabled`-Attr): bg `#a7b6a0`, kein Schatten, `cursor: not-allowed`, kein Hover-Lift.
- Unter dem Button, solange WLD nicht verbunden: Hinweis `.co-cta-note` (`12px`, `var(--ink-mute)`, zentriert): „Verbinde zuerst deine WLD-Wallet, um zu verkaufen."

### Validierung
```
typeOk  = type==='plan' ? !!planId : type==='recipe' ? !!recipe : true
valid   = wldOn && title.trim() && priceNum > 0 && typeOk
```
`valid` steuert den enabled/disabled-Zustand des CTA. (Objekt/Digital/Service haben im Prototyp keine harten Pflichtfelder — in Produktion ggf. ergänzen.)

## Interaktionen & Verhalten
- **Typ-Kachel wählen** → setzt `type`, blendet den passenden Sub-Block ein/aus.
- **Plan-Karte / Rezept-Select** → setzt `planId` bzw. `recipe`.
- **Objekt/Digital/Service-Felder** → schreiben in das `fields`-Objekt via `set(key, value)`.
- **Wallet-Button** → togglet `wldOn` / `usdOn`.
- **Titel / Beschreibung / Preis tippen** → aktualisiert State; Preis-Eingabe filtert auf Ziffern + Punkt; Split rechnet live.
- **Sprach-Toggle** → `EN ⇄ DE`.
- **„Jetzt verkaufen"** → nur bei `valid` aktiv; in Produktion: Listing anlegen + Medien hochladen + zur Bestätigung/zum Feed navigieren. *(Im Prototyp nicht verdrahtet.)*
- **Zurück** → zurück zum Marktplatz.
- Übergänge/Animationen respektieren `prefers-reduced-motion`.

## State Management (Variante A)
```ts
type:    'plan' | 'recipe' | 'object' | 'digital' | 'service'   // default 'plan'
planId:  string            // wenn type==='plan' (default 'p1')
recipe:  string            // wenn type==='recipe'
fields:  { image?, stock?, shipping?:boolean, fileUrl?, cover? }  // typ-spezifisch
title:   string
desc:    string
price:   string            // numerischer String, gefiltert [0-9.] (default '10')
wldOn:   boolean           // WLD-Wallet verbunden — Pflicht
usdOn:   boolean           // USDT/USDC-Wallet verbunden — optional
lang:    'EN' | 'DE'
```
Abgeleitet: `priceNum = parseFloat(price)||0`, `youGet = priceNum*0.8`, `platform = priceNum*0.2`, `typeOk`, `valid`. Beim Veröffentlichen werden diese Werte an die Marketplace-API gesendet und Medien hochgeladen.

---

# Variante B — Social-Composer / v2 (Alternative)
Dateien: `Create Offer v2.html`, `create-offer-v2.jsx`, `styles.css`.

Gleiche Funktion, neu gedacht als **Social-Media-Post-Composer**:
- **Bild zuerst**: große Foto/Video-Upload-Zone als Hero; gefüllt wird sie zu einem 4-Spalten-Foto-Grid mit großer „Cover"-Kachel (2×2) und Entfernen-×.
- **Typ als horizontale Pills** (scrollbare Chips: Essensplan · Rezept · Objekt · Digital · Service) statt Icon-Grid; darunter typ-spezifische Felder in einer weichen Karte.
- **Caption-Stil**: Titel (Fraunces 20px) + Beschreibung in einer Karte.
- **Preis-Karte** mit Live-Split „Du bekommst {price×0.8} WLD · 20% Plattform-Gebühr".
- **Wallet-Strip** (eine antippbare Zeile) statt zweier Karten.
- **Live-Feed-Vorschau** unten: Mini-Marketplace-Card, die sich live aus den Eingaben aktualisiert.
- **„Teilen"-Button in der Top-Bar** als Primäraktion (kein Footer-CTA), disabled bis gültig: `wldOn && title.trim() && price>0 && photos.length>0`.

State (v2): `photos:string[]` ([0] = Cover), `type`, `planId`, `recipe`, `fields`, `title`, `caption`, `price`, `wldConnected`. Detail-Spezifikation der v2-Maße/Farben steht in den v2-Quelldateien (alle Werte sind dort inline bzw. in `styles.css`).

> Entscheidung Formular vs. Social liegt beim Produkt-Team. Bei Unklarheit: **Variante A** umsetzen (validierter, vollständigerer Flow), Variante B als spätere visuelle Iteration.

---

## Design-Tokens (aus `styles.css` `:root`)
**Farben**
- `--bg-cream #f5efe1` · `--bg-cream-2 #ede5d2`
- `--ink-deep #14391f` · `--ink-deep-2 #0f2c18` · `--ink-mid #2a4a32` · `--ink-text #1a2e20` · `--ink-mute #6b7868`
- `--ink-line rgba(20,57,31,0.12)`
- `--avocado #5fa052` · `--coral #ff7849` · `--coral-soft #ffb494` · `--orange #ff9a3c` · `--gold #f5b942` · `--premium #c8a45c`
- `--card #ffffff` · `--card-soft #faf6ec`
- Gradients: Wallet `linear-gradient(135deg,#1f4329,#0e2415)` · CTA `linear-gradient(135deg,#1a3a23,#0e2415)` · Banner-bg `#f6ead0` / border `#e9d3a3` / text `#715a24`.

**Schatten**
- `--shadow-sm 0 2px 8px rgba(20,57,31,0.06)` · `--shadow-md 0 8px 24px rgba(20,57,31,0.10)` · `--shadow-lg 0 20px 60px rgba(20,57,31,0.16)`
- CTA-Schatten `0 8px 22px rgba(20,57,31,0.26)`.

**Radien**
- `--radius-sm 10` · `--radius-md 16` · `--radius-lg 22` · `--radius-xl 28` · Pills `999`. Inputs `12`, Pick/Preis-Box `14`, Typ-Kachel `16`, Wallet-Button `16`.

**Typografie**
- `--font-sans` = **Inter** (UI-Text), Gewichte 400/500/600/700.
- `--font-serif` = **Fraunces** (Überschriften, Plan-Namen, Preis), Gewichte 500/600/700, `opsz 9..144`.
- Skala (A): H1 30 · Sektionstitel 15.5 · Body/Inputs 14 · Labels 12.5–13.5 · Hinweise 12–12.5 · Preis 32 · CTA 15.5.

**Fokus-Ring** (Inputs/Selects): border → `var(--avocado)`, `box-shadow 0 0 0 3px rgba(95,160,82,0.14)`.

## Assets
- **Icons**: inline 24×24-Stroke-SVGs (back, chevron, wallet, info, tag, check, calendar, doc, cube, download, service). Durch das Icon-Set des Codebase ersetzen.
- **Bilder**: in Variante A werden Bilder als **URLs** eingegeben (kein Upload-Widget); in Variante B sind die Foto-Kacheln Gradient-Platzhalter für echte Uploads. In Produktion echten Datei-Upload/Picker anbinden.
- **Fonts**: Fraunces + Inter via Google Fonts — Font-Pipeline des Codebase nutzen.

## Dateien in diesem Bundle
- `Create Offer.html` — lauffähiger Prototyp Variante A (Phone-Frame + Screen).
- `create-offer.jsx` — Variante A: `CreateOffer`, Sub-Feld-Gruppen, `WalletCard`, Icons, Beispiel-Daten.
- `Create Offer v2.html` — lauffähiger Prototyp Variante B.
- `create-offer-v2.jsx` — Variante B: `CreateOfferSocial`, `PreviewCard`, Icons, Beispiel-Daten.
- `styles.css` — gemeinsame Avocado-Tokens (`:root`) + `.mp-screen` und Marketplace-Klassen.

## Starten
`Create Offer.html` (oder `Create Offer v2.html`) im Browser öffnen — React + Babel kommen vom CDN, JSX wird im Browser transpiliert. **Für Produktion JSX vorkompilieren** und in die Build-Pipeline des Codebase integrieren.
