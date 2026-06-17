# Handoff: Creator Upload, 10-Sprachen-Rezepte, Zutaten-KI und Creator-Verguetung

Stand: 2026-06-17
Ziel: Vorlage fuer eine Gemini-Praesentation zur aktuellen Creator-Journey in der WorldMiniApp.

---

## 1. Kurzfassung fuer Gemini

Erstelle eine praesentationstaugliche Story ueber die WorldMiniApp als mobile-first Social-Recipe-Plattform. Der Fokus liegt auf dem Creator-Upload: Creator laden ein Bild oder Video hoch, geben Rezeptdaten, Zutaten, Zubereitungsschritte und Keywords ein. Die App verarbeitet Medien, uebersetzt Rezeptschritte automatisch in 10 Sprachen, hilft beim Erstellen fehlender Zutaten per KI und macht den Content im Feed sowie im Marktplatz monetarisierbar.

Wichtig: Die Praesentation soll nicht wie technische API-Dokumentation wirken, sondern als Produkt- und Investoren-/Partner-Handoff. Sie soll die Nutzerreise, die Automatisierung, die globale Mehrsprachigkeit und die Creator-Verguetung klar erklaeren.

Kernaussage:
WorldMiniApp verwandelt Creator-Rezepte in internationale, kaufbare Food-Assets: ein Upload wird zu Feed-Content, mehrsprachigem Rezept, Zutaten-Datensatz, Meal-Plan-Baustein und potenzieller Creator-Einnahme.

---

## 2. Produkt-Narrativ

Die Creator-Funktion ist der Einstiegspunkt fuer neue Rezeptinhalte. Ein Creator erstellt ein Posting in fuenf Bereichen:

1. Basics: Titel, Kategorie, Personenanzahl, Zubereitungszeit und Praeferenz.
2. Media: Bild oder Video fuer den Feed.
3. Zutaten: Auswahl aus dem Katalog oder KI-gestuetztes Anlegen fehlender Zutaten.
4. Schritte: einfache Texteingabe statt altem komplexem Master-Step-System.
5. Keywords: Tags fuer Suche, Feed-Kontext und spaetere Empfehlungssysteme.

Nach dem Absenden wird aus dem Creator-Input ein strukturierter Rezept-Datensatz. Dieser Datensatz kann im Feed konsumiert, in Essensplaenen genutzt und ueber den Marketplace verkauft werden.

---

## 3. Creator Upload: aktueller Ablauf

### Einstieg

- Route: `GET /WorldMiniApp/Home/Upload`
- View: `Areas/WorldMiniApp/Views/Home/CreatePosting.cshtml`
- Controller: `Areas/WorldMiniApp/Controllers/RecipeController.cs`
- Upload-Action: `UploadNewVideo`

Die UI ist mobile-first und als Creator-Studio aufgebaut. Sie zeigt eine Hero-Leiste mit Live-Status, Tabs fuer die fuenf Bereiche und eine Publish-Bar. Die Upload-Berechtigung ist an Worldcoin/World ID gekoppelt: Uploads sind fuer orb-verifizierte Creator bzw. fuer den Superuser erlaubt.

### Validierung und Schutz

- Nur berechtigte Creator duerfen posten.
- Datei ist Pflicht.
- Upload-Rate-Limit: 5 Uploads pro 10 Minuten pro User.
- Idempotenz ueber `X-Idempotency-Key`, damit ein Netzwerk-Retry kein Doppelposting erzeugt.
- Bilder und Videos werden anhand des Content-Types getrennt verarbeitet.

### Bild-Pfad

Bei Bildern wird synchron verarbeitet:

- Upload/Optimierung ueber den registrierten `IBlobUploadService`.
- Bild wird zu WEBP mit Source und Thumbnail.
- Rezeptschritte werden, falls vorhanden, uebersetzt.
- Rezept wird ueber `SaveNewRecipeService.SaveNewAsync` gespeichert.
- `WorldUserPosting` wird als Feed-Posting angelegt.
- Keywords werden mit dem Rezept verknuepft.

### Video-Pfad

Videos laufen asynchron:

- Video-Bytes werden aus dem Request gesichert.
- Rezeptdaten werden in einfache Daten kopiert, nicht als EF-Entities.
- Hintergrundverarbeitung startet per Task mit eigenem DI-Scope.
- Video wird hochgeladen und verarbeitet.
- Rezeptschritte werden uebersetzt.
- Zutaten werden aus IDs/Mengen/Masseinheiten rekonstruiert.
- Rezept, Posting und Keyword-Links werden gespeichert.
- Bei Video-GUID wird ein `WorldUserPendingVideo` angelegt.
- Creator bekommt eine Notification: Upload abgeschlossen bzw. bei Fehler fehlgeschlagen.

Praesentationston:
Der Upload fuehlt sich fuer Creator schnell an: Videos muessen nicht im Request fertig transkodiert werden, sondern werden im Hintergrund vorbereitet und spaeter im Feed sichtbar.

---

## 4. Automatische Uebersetzung in 10 Sprachen

Service: `Areas/WorldMiniApp/Services/RecipeTranslationService.cs`

Die Rezeptschritte werden automatisch in 10 Sprachen erzeugt:

- Deutsch: `de`
- Englisch: `en`
- Spanisch: `esp`
- Portugiesisch: `prt`
- Indonesisch: `id`
- Niederlaendisch: `nl`
- Schwedisch: `sv`
- Daenisch: `da`
- Norwegisch: `no`
- Malaiisch: `ms`

Technischer Kern:

- Modell: aktuell `gpt-4o-mini`
- Ausgabeformat: JSON
- Inhalt: Titel und Zubereitungsschritte
- Stilregel: imperative, natuerliche Kochsprache
- Zutaten werden nicht neu uebersetzt, weil Zutaten bereits mehrsprachig im Katalog liegen.

Produktnutzen:

- Ein Creator muss nur einmal posten.
- Das Rezept wird sofort international nutzbar.
- Feed, Rezeptansicht, Essensplanung und Marktplatz koennen denselben strukturierten Inhalt verwenden.
- Die globale Skalierung entsteht nicht erst durch manuelle Redaktion, sondern direkt im Upload-Prozess.

---

## 5. Zutaten-Hinzufuegung mit KI

Endpunkte in `RecipeController`:

- `SuggestMissingIngredient`
- `SaveSuggestedIngredient`

Problem:
Creator verwenden Zutaten, die noch nicht im Katalog existieren. Rohe Freitext-Zutaten waeren schlecht fuer Suche, Naehrwerte, Skalierung und Einkaufslisten.

Loesung:
Die App prueft zuerst, ob es passende Zutaten im bestehenden Katalog gibt. Wenn ja, werden Treffer vorgeschlagen. Wenn nein, erstellt die KI einen strukturierten Vorschlag fuer eine neue Katalog-Zutat.

Gespeichert wird nur, wenn Schutzregeln erfuellt sind:

- Creator muss berechtigt sein.
- Debug-Fallback wird nicht gespeichert.
- Sprachfelder duerfen nicht offensichtlich unuebersetzt sein.
- Genus-/Artikel-Felder muessen plausibel sein.
- Duplikate werden erkannt und wiederverwendet.
- Rate-Limit: maximal 10 neue Zutaten pro 30 Minuten pro User.

Produktnutzen:

- Der Zutatenkatalog waechst kontrolliert.
- Neue Zutaten bekommen mehrsprachige Namen und Naehrwertbasis.
- Essensplanung, Einkaufslisten und Naehrwertberechnung bleiben strukturiert.
- Creator koennen schnell arbeiten, ohne Datenqualitaet zu zerstoeren.

---

## 6. Feed: Creator Content und Marketplace-Verbindung

Feed-Datei: `Areas/WorldMiniApp/Views/Feed/Index.cshtml`
Creator-Endpunkt: `FeedController.CreatorPostings`

Im Feed ist jedes Posting mit Creator-Informationen verbunden. Die UI kann den aktuell sichtbaren Creator aus dem Feed-Item lesen und daraus zwei Dinge ableiten:

- Creator-Feed: weitere Postings desselben Creators.
- Marketplace-Kontext: Angebote werden auf den aktuellen Creator gefiltert.

Wichtig fuer die Praesentation:
Der Feed ist nicht nur Konsumflaeche. Er ist der Einstieg in eine Creator-Commerce-Journey. Nutzer sehen ein Rezept, koennen den Creator-Kontext oeffnen und passende Meal-Plan-Angebote desselben Creators entdecken.

Storyline:

1. Nutzer entdeckt ein Rezept im Feed.
2. Nutzer schaut weitere Inhalte des Creators.
3. Nutzer oeffnet passende Angebote.
4. Nutzer kauft einen Essensplan.
5. Creator erhaelt eine Gutschrift.

---

## 7. Creator-Verguetung: zwei Modelle

Service: `Areas/WorldMiniApp/Services/MarketplaceService.cs`
Controller: `Areas/WorldMiniApp/Controllers/MarketplaceController.cs`

Die Creator-Verguetung besteht aus zwei getrennten Modellen, die in der Praesentation klar unterschieden werden muessen:

1. **50 Prozent Gewinnbeteiligung fuer Creator**: strategisches Creator-Share-Modell fuer die allgemeine Plattform-Monetarisierung.
2. **80/20 Marketplace-Split**: spezielles Modell fuer den Verkauf von Essensplaenen im Marketplace.

### 7.1 Allgemeine Creator-Gewinnbeteiligung: 50 Prozent nach gesehenen Minuten

Creators sollen an der Plattform-Wertschoepfung beteiligt werden. Dafuer ist eine **50 Prozent Gewinnbeteiligung fuer Creator** vorgesehen.

Die Verteilung dieses Creator-Pools erfolgt anhand der **gesehenen Minuten**. Das bedeutet: Nicht nur Upload-Menge oder Followerzahl zaehlen, sondern echte Aufmerksamkeit. Creator, deren Videos und Rezepte laenger angesehen werden, erhalten einen groesseren Anteil am Gewinnbeteiligungs-Pool.

Praesentationsbotschaft:

- Creator liefern den Content, der Reichweite, Engagement und Umsatz erzeugt.
- Die Plattform beteiligt Creator deshalb nicht nur punktuell, sondern als dauerhaftes Beteiligungsmodell.
- 50 Prozent Gewinnbeteiligung ist die uebergeordnete Creator-Economy-Story.
- Die Ausschüttung wird nach gesehenen Minuten berechnet.
- Watch-Time macht die Verguetung leistungsbezogen und fairer als reine Klicks oder Upload-Zahlen.
- Das Modell soll Creator motivieren, hochwertige Inhalte, Rezepte und Communities aufzubauen.

Wichtig:
Diese 50-Prozent-Gewinnbeteiligung ist getrennt vom aktuellen Marketplace-Checkout-Split zu erklaeren. Sie ist die grosse Creator-Value-Proposition.

Vereinfachte Formel fuer die Praesentation:

```
Creator-Pool = 50 Prozent des Plattform-Gewinns
Creator-Anteil = eigene gesehene Minuten / alle gesehenen Creator-Minuten
Auszahlung = Creator-Pool * Creator-Anteil
```

### 7.2 Marketplace-Split: 80/20 nur fuer Marktplatz-Verkaeufe

Creators koennen eigene Essensplaene im Marktplatz verkaufen. Ein Listing basiert auf einem `WorldUserMealPlan`.

Zentrale Verkaufsregel:
Ein Creator darf nur Essensplaene verkaufen, die ausschliesslich eigene Rezepte enthalten. Dadurch kann niemand fremde Feed-Rezepte buendeln und monetarisieren.

Kaufmodell aktuell: Modell B

- Kaeufer zahlt den vollen Preis an die Plattform-Wallet.
- Zahlung wird serverseitig ueber die World-Payment-API verifiziert.
- Kauf ist fail-closed: ohne gueltige Zahlung wird kein Plan freigeschaltet.
- Nach erfolgreichem Kauf bekommt der Kaeufer eine Kopie des Essensplans.
- Verkauf wird als `MealPlanPurchase` gespeichert.

Marketplace-Verguetung:

- Creator-Anteil: 80 Prozent
- Plattform-Fee: 20 Prozent
- `CreatorAmount = Price * 0.80`
- `PlatformFee = Price - CreatorAmount`
- Creator-Anteil wird dem internen `WildCoinBalance` gutgeschrieben.
- Ledger-Eintrag: `WildCoinTransaction` mit Typ `sale_credit`.

Auszahlung:

- Creator kann eine Auszahlung beantragen.
- Auszahlung reserviert den Betrag sofort vom verfuegbaren Guthaben.
- Antrag wird als `MarketplacePayoutRequest` mit Status `pending` gespeichert.
- Admin ueberweist manuell und markiert den Antrag als `paid`.
- Bei Ablehnung wird der reservierte Betrag zurueckgebucht.

Praesentationston:
Die Plattform hat fuer den Marketplace bereits ein nachvollziehbares 80/20-Revenue-Sharing-Modell: Creator verdienen an verkauften Essensplaenen, waehrend die Plattform 20 Prozent als Marketplace-Gebuehr behaelt. Dieses 80/20-Modell gilt nur fuer Marktplatz-Verkaeufe. Die uebergeordnete Creator-Economy-Story ist zusaetzlich die 50-Prozent-Gewinnbeteiligung fuer Creator.

---

## 8. Datenfluss als Praesentationsgrafik

```
Creator
  -> Upload Studio
  -> Medienverarbeitung
  -> Rezept speichern
  -> 10-Sprachen-Uebersetzung
  -> Zutaten-Katalog / KI-Zutat
  -> Feed Posting
  -> Essensplan
  -> Marketplace Listing
  -> Kauf
  -> Marketplace: 80 Prozent Creator-Gutschrift
  -> Payout
```

Alternative Darstellung:

```
Ein Upload
  = Feed-Video/Bild
  + strukturierter Rezeptdatensatz
  + mehrsprachige Schritte
  + Zutaten/Naehrwerte
  + Meal-Plan-Baustein
  + Marktplatz-Asset
```

---

## 9. Empfohlene Slide-Struktur

### Slide 1: Titel

Titel: WorldMiniApp Creator Engine
Untertitel: Vom Rezept-Upload zum internationalen Creator-Commerce

Kernaussage:
Ein Creator-Upload wird automatisch in Content, Daten, Planung und Umsatz verwandelt.

### Slide 2: Problem

Creator-Rezepte sind normalerweise schwer zu skalieren:

- Medien, Rezepttext und Zutaten sind oft unstrukturiert.
- Mehrsprachigkeit ist teuer.
- Zutaten- und Naehrwertdaten fehlen.
- Monetarisierung passiert getrennt vom Content.

### Slide 3: Loesung

WorldMiniApp verbindet:

- Social Feed
- Rezeptdatenbank
- KI-Uebersetzung
- Zutaten-Katalog
- Essensplanung
- Marketplace
- Creator-Verguetung

### Slide 4: Creator Upload Studio

Zeige die fuenf Upload-Bereiche:

- Basics
- Media
- Ingredients
- Steps
- Keywords

Botschaft:
Creator posten wie in einer Social App, aber im Hintergrund entsteht ein strukturierter Rezept-Datensatz.

### Slide 5: Medienverarbeitung

Bild:
Schneller synchroner Pfad mit WEBP und Thumbnail.

Video:
Asynchroner Pfad mit Hintergrundverarbeitung, Video-Tracking und Notification.

Botschaft:
Die App bleibt schnell, auch wenn Videos mehrere Minuten Verarbeitung brauchen.

### Slide 6: 10-Sprachen-Automatisierung

Liste der Sprachen:
DE, EN, ES, PT, ID, NL, SV, DA, NO, MS

Botschaft:
Ein einzelner Creator-Input wird international verfuegbar.

### Slide 7: Zutaten-KI

Flow:

1. Creator sucht Zutat.
2. App sucht im Katalog.
3. Bei Treffer: bestehende Zutat verwenden.
4. Ohne Treffer: KI erstellt strukturierten Vorschlag.
5. Sicherheitschecks verhindern schlechte Daten.
6. Neue Zutat wird in den Katalog uebernommen.

Botschaft:
Die Plattform wird mit jedem Creator besser, ohne Datenqualitaet zu verlieren.

### Slide 8: Feed als Commerce-Einstieg

Erklaere:

- Feed zeigt Creator-Content.
- Creator-Kontext kann weitere Postings laden.
- Marketplace kann Angebote des aktuellen Creators anzeigen.

Botschaft:
Discovery und Kauf liegen nah beieinander.

### Slide 9: Marketplace

Erklaere:

- Creator verkauft eigene Essensplaene.
- Plaene duerfen nur eigene Rezepte enthalten.
- Kaeufer erhaelt eine Kopie des Plans.
- Zahlung wird serverseitig verifiziert.

Botschaft:
Der Marktplatz schuetzt Creator-Rechte und verhindert unberechtigte Monetarisierung fremder Inhalte.

### Slide 10: Creator-Verguetung

Zeige zwei getrennte Verguetungsmodelle:

- 50 Prozent Gewinnbeteiligung fuer Creator als uebergeordnete Creator-Economy-Story.
- Verteilung der 50 Prozent anhand der gesehenen Minuten.
- 80/20-Split nur fuer Marketplace-Verkaeufe von Essensplaenen.
- Marketplace: 80 Prozent Creator, 20 Prozent Plattform-Fee.
- Marketplace: interne Gutschrift im Creator-Guthaben.
- Marketplace: Payout-Antrag und Admin-Auszahlung.

Botschaft:
Die Plattform verbindet watch-time-basierte Creator-Gewinnbeteiligung mit einem konkreten Marketplace-Auszahlungsmodell.

### Slide 11: Warum das skalierbar ist

Punkte:

- Strukturierte Rezeptdaten statt Freitext.
- Mehrsprachigkeit direkt im Upload.
- Zutatenkatalog wird kontrolliert erweitert.
- Feed, Planung und Marketplace nutzen denselben Content.
- Monetarisierung ist an Creator-Eigentum gebunden.

### Slide 12: Naechste Ausbaustufen

Moegliche Roadmap:

- Robuste Background-Queue statt Fire-and-Forget-Task.
- Voll automatisierter Payout-Service.
- Smart-Contract-Split fuer direkten 80/20-On-Chain-Split im Marketplace.
- Ranking/Recommendation fuer Creator-Angebote.
- Bessere Analytics fuer gesehene Minuten, Creator-Umsaetze und Conversion.

---

## 10. Gemini-Prompt

Kopiere diesen Prompt in Gemini:

```text
Erstelle eine professionelle Praesentation auf Deutsch fuer ein Produkt-/Partner-Handoff.

Thema:
WorldMiniApp Creator Engine: Vom Rezept-Upload zum internationalen Creator-Commerce.

Zielgruppe:
Produktpartner, Investoren, technische Entscheider und interne Stakeholder.

Stil:
Modern, klar, mobile-first, social-commerce-orientiert. Keine reine Code-Dokumentation. Bitte als Slides mit kurzen Headlines, starken Kernaussagen und Speaker Notes formulieren.

Inhalt:
1. Die WorldMiniApp ist eine mobile Social-Recipe-Plattform mit Creator-Uploads, Feed, Essensplanung und Marketplace.
2. Creator erstellen Postings in fuenf Bereichen: Basics, Media, Ingredients, Steps, Keywords.
3. Bilder werden synchron optimiert, Videos laufen asynchron im Hintergrund und erzeugen Notifications.
4. Rezeptschritte werden automatisch in 10 Sprachen uebersetzt: de, en, esp, prt, id, nl, sv, da, no, ms.
5. Fehlende Zutaten koennen per KI vorgeschlagen und nach Sicherheitschecks in den Katalog uebernommen werden.
6. Der Feed ist Discovery-Flaeche und Commerce-Einstieg: Nutzer sehen Creator-Content, weitere Creator-Postings und passende Marketplace-Angebote.
7. Im Marketplace verkaufen Creator eigene Essensplaene. Ein Plan darf nur eigene Rezepte enthalten.
8. Zahlungen werden serverseitig verifiziert. Nach erfolgreichem Kauf bekommt der Kaeufer eine Kopie des Essensplans.
9. Creator-Verguetung: Es gibt zwei Modelle. Allgemein ist eine 50 Prozent Gewinnbeteiligung fuer Creator vorgesehen, die anhand der gesehenen Minuten verteilt wird. Fuer Marketplace-Verkaeufe von Essensplaenen gilt separat der 80/20-Split: 80 Prozent Creator-Anteil, 20 Prozent Plattform-Fee. Der Marketplace-Creator-Anteil wird als internes Guthaben gutgeschrieben und kann per Payout-Antrag ausgezahlt werden.
10. Roadmap: robuste Background-Queue, automatisierte Auszahlungen, Smart-Contract-Split, bessere Creator-Analytics und Ranking.

Erzeuge 12 Slides:
1. Titel
2. Problem
3. Loesung
4. Creator Upload Studio
5. Medienverarbeitung
6. 10-Sprachen-Automatisierung
7. Zutaten-KI
8. Feed als Commerce-Einstieg
9. Marketplace
10. Creator-Verguetung
11. Skalierbarkeit
12. Naechste Ausbaustufen

Bitte pro Slide liefern:
- Slide-Titel
- 3 bis 5 Bullet Points
- eine kurze Speaker Note
- Vorschlag fuer Visual/Grafik
```

---

## 11. Wichtige Implementierungsquellen

- `Areas/WorldMiniApp/Views/Home/CreatePosting.cshtml`: Creator Upload UI, 5 Bereiche, Sprachumschalter.
- `Areas/WorldMiniApp/Controllers/RecipeController.cs`: Upload, Video-Hintergrundverarbeitung, KI-Zutaten-Endpunkte.
- `Areas/WorldMiniApp/Services/RecipeTranslationService.cs`: 10-Sprachen-Uebersetzung der Rezeptschritte.
- `Areas/WorldMiniApp/Services/SaveNewRecipeService.cs`: Speichern von Rezept, Schritten, Bildern und Zutaten.
- `Areas/WorldMiniApp/Controllers/FeedController.cs`: Feed, CreatorPostings und Creator-Kontext.
- `Areas/WorldMiniApp/Views/Feed/Index.cshtml`: Feed/Creator/Marketplace-Verknuepfung im Frontend.
- `Areas/WorldMiniApp/Services/MarketplaceService.cs`: Listings, Kaufabschluss, 80/20-Verbuchung fuer Marketplace-Verkaeufe und Payout-Logik.
- `Areas/WorldMiniApp/Controllers/MarketplaceController.cs`: Marketplace-Endpunkte, Listing, Kauf und Auszahlung.

---

## 12. Wichtige Praesentationsbotschaft

WorldMiniApp ist nicht nur ein Rezept-Feed. Die App baut eine Creator-Oekonomie fuer Food-Content:

- Upload erzeugt strukturierten Content.
- KI macht den Content international und datenfaehig.
- Feed erzeugt Reichweite.
- Essensplaene erzeugen kaufbare Produkte.
- Marketplace erzeugt Umsatz.
- 50 Prozent Gewinnbeteiligung nach gesehenen Minuten macht die Creator-Economy attraktiv und leistungsbezogen.
- 80/20-Marketplace-Split macht Essensplan-Verkaeufe konkret abrechenbar.
