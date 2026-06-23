# Handoff: Pitch Deck (Creator & Investoren)

## Overview
Dies ist ein **Inhalts- und Design-Handoff für eine Präsentation** (Pitch Deck) der App
**„Delikatessen Drehbuch" – die Social-Cooking World Mini App**. Ziel: Ein Designer (oder
Claude im Design-Modus) baut daraus ein fertiges, schön gestaltetes Slide-Deck.

Es gibt **zwei Decks** mit gemeinsamer Substanz, aber unterschiedlichem Fokus:
- `slides-investors.md` — 12 Slides, Fokus „Warum wird das groß / Investment".
- `slides-creators.md` — 5–6 Slides, Fokus „Was springt für mich als Creator raus".

> ⚠️ **Wichtig — die App ist pre-launch.** Es wurden **keine** Traktions-/Nutzerzahlen erfunden.
> Alle Stellen, die echte Zahlen brauchen, sind mit `[… einsetzen]` markiert. Bitte als Platzhalter
> im Design belassen (z. B. als auffällige Markierung), damit der Gründer sie ersetzen kann.

## About this Handoff
Die `.md`-Dateien enthalten den **fertigen Folientext + Speaker-Notes + Struktur**. Die Aufgabe
des Designers ist **Gestaltung, Layout, Visuals, Iconografie und Hierarchie** — nicht das Erfinden
neuer Inhalte. Inhalt darf gekürzt/umformuliert werden, aber **keine Features oder Claims hinzufügen**,
die nicht in diesem Handoff stehen (Faktentreue zur echten App).

## Deliverable (gewünschtes Ergebnis)
- Ein **Präsentations-Deck** im Hochkant- **oder** Querformat — Empfehlung: **16:9 quer** für
  Investoren-Meetings, optional **9:16 hochkant** für eine mobile/in-App-Variante.
- Format: bevorzugt **HTML/CSS-Slidedeck** (z. B. reveal.js oder eigenständige `index.html` mit
  Slides), alternativ als saubere statische Slides (eine Section pro Slide). So kann der Gründer
  es im Browser zeigen und als PDF exportieren.
- Jede Slide: **eine Kernaussage**, große Headline, wenig Text, ein starkes Visual.

## Zielgruppen & Ton
- **Investoren:** selbstbewusst, datengetrieben, „category-defining". Nüchtern bei Zahlen
  (pre-launch ehrlich framen — „Produkt fertig, jetzt skalieren wir").
- **Creator:** warm, einladend, „du verdienst Geld mit deiner Leidenschaft". Emotional, bildstark.

## Fidelity
**Medium-high.** Marke, Farben, Typo und Stimmung sind unten verbindlich spezifiziert (das Deck
soll *wie die App* aussehen). Genaues Slide-Layout ist gestalterischer Freiraum.

---

## Brand & Design Tokens („Avocado"-System der App)

Das Deck soll die visuelle Identität der App tragen.

### Farben
| Token | Hex | Verwendung |
|---|---|---|
| Ink (Dunkelgrün) | `#14391f` | Titel, Text auf Hell, dunkle Panels |
| Avocado-Grün | `#5fa052` → `#3f7536` (Gradient) | Primär-Buttons, Akzente, „Geld/Erfolg" |
| Creme / Beige | `#faf6ec` | Slide-Hintergrund (warm, nicht reinweiß) |
| Weiß | `#ffffff` | Karten/Cards |
| Coral (Akzent) | `#e8765a` *(ca. – Coral-Akzent der App)* | Highlights, „Like/Herz", Call-outs |
| Muted Text | `#7c8a7f` | Sekundärtext, Captions |
| Soft Line | `rgba(20,57,31,0.10)` | Trennlinien, Card-Rahmen |

### Typografie
- **Headlines / Zahlen:** `Fraunces` (Serif, 600–700) — die „Magazine"-Anmutung der App.
- **Body / UI:** `Inter` (Sans, 400–700).
- Große Kennzahlen (z. B. „80 %") als **Fraunces Display** prominent setzen.

### Form & Stil
- Border-Radius großzügig: **18–22px** für Cards.
- Soft Shadows: `0 8px 24px rgba(20,57,31,0.08)`.
- Viel Weißraum, ruhige Layouts, ein Hero-Visual pro Slide.
- Icon-Stil: **outline / stroke 2px** (wie in der App, lucide/feather-artig).
- Emoji sparsam als Feature-Marker erlaubt (🥑 ist das Maskottchen/Akzent der App).

### Krypto-Notation
Beträge in **WLD** und **USDC** (World Chain). Wo Geld gezeigt wird, ein dezenter
`WLD ⇄ USDC`-Toggle-Look passt zur App.

---

## Asset-Hinweise (für den Designer)
- **Screenshots** der App stark einsetzen (Feed, Wochenplaner, Marktplatz, Gruppen-Feed).
  Diese liefert der Gründer separat — im Layout **Platzhalter-Frames** (Phone-Mockups, 9:16)
  vorsehen.
- Mockup-Empfehlung: Smartphone-Frames, da es eine Mobile-/Mini-App ist.
- Logo/Maskottchen: 🥑-Avocado-Welt; falls echtes Logo vorhanden, ersetzt es die Emoji.

## So nutzt du diesen Handoff
1. Lies `slides-investors.md` bzw. `slides-creators.md` — jede `##`-Überschrift = **eine Slide**.
2. „**Headline**" = große Aussage auf der Slide. „**On-Slide**" = sichtbarer Text (kurz halten!).
   „**Speaker Notes**" = NICHT auf die Slide, nur Sprechtext für den Gründer.
3. Baue das Deck im Avocado-Look (Tokens oben).
4. Platzhalter `[…]` sichtbar/markiert lassen.
