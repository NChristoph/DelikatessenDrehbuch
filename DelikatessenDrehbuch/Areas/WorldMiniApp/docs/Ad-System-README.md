# 📊 World Mini App - Ad System Documentation

## Überblick

Das Ad-System ermöglicht es, Werbeanzeigen im Video-Feed zu schalten. Ads erscheinen automatisch zwischen Videos und werden mit detaillierten Analytics getrackt.

## Features

✅ **Video & Bild Ads** - Beide Formate werden unterstützt
✅ **Flexible Platzierung** - Alle X Videos konfigurierbar
✅ **Analytics** - Views, Clicks, CTR Tracking
✅ **Targeting** - Nach Verification-Level (Orb/Device/All)
✅ **Scheduling** - Start/End-Datum pro Ad
✅ **Priority System** - Ads mit höherer Priority werden häufiger gezeigt
✅ **Admin Interface** - Einfache Verwaltung über Web-UI

---

## Installation

### 1. SQL Tabellen erstellen

Führe das SQL-Script aus:

```bash
C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch\Areas\WorldMiniApp\SQL\CreateAdTables.sql
```

In SQL Server Management Studio:
1. Öffne das Script
2. Wähle deine Database
3. Execute (F5)

Das erstellt 2 Tabellen:
- `WorldAppAds` - Ad Stammdaten
- `WorldAppAdImpressions` - Detailliertes Tracking

### 2. Service registrieren (Program.cs)

Füge in `Program.cs` hinzu:

```csharp
// In der Services-Sektion (nach anderen WorldMiniApp Services)
builder.Services.AddScoped<IAdInjectionService, AdInjectionService>();
```

### 3. Feed Integration (optional)

Falls du Ads automatisch im Feed injizieren willst, kannst du den `AdInjectionService` in deinem FeedController verwenden:

```csharp
// Im FeedController Constructor
private readonly IAdInjectionService _adService;

public FeedController(ApplicationDbContext context, IAdInjectionService adService, ...)
{
    _adService = adService;
    // ...
}

// Im Index/Feed Action
var postings = await query.Take(20).ToListAsync();
var feedWithAds = await _adService.InjectAdsIntoFeed(postings, userVerificationLevel);
```

---

## Verwendung

### Admin Interface

Öffne: **`https://localhost:7179/WorldMiniApp/Admin/Ads`**

Nur für Master-User (dein Hash) zugänglich!

#### Ad erstellen:

1. Klick auf "Create New Ad"
2. Fülle die Felder aus:
   - **Title**: Kurzer Titel
   - **Media URL**: Link zu Bild oder Video
   - **Is Video**: Checkbox für Video-Ads
   - **Target URL**: Wohin führt der Click?
   - **CTA Text**: Button-Text (z.B. "Jetzt kaufen")
   - **Show Every X Videos**: Alle wieviele Videos? (z.B. 5)
   - **Priority**: 1-100 (höher = häufiger gezeigt)
   - **Target Audience**: All / Orb / Device
   - **Start/End Date**: Optional zeitlich begrenzen

3. Klick "Create Ad"

#### Ad bearbeiten:

1. Klick auf "Edit" bei einem Ad
2. Ändere Felder
3. Speichern

#### Ad aktivieren/deaktivieren:

- Klick "Deactivate" / "Activate"
- Inaktive Ads werden nicht im Feed gezeigt

#### Analytics anschauen:

- Klick "Analytics" bei einem Ad
- Siehst:
  - Total Views (Impressions)
  - Total Clicks
  - CTR (Click-Through-Rate)
  - Daily Stats Chart (letzte 30 Tage)
  - Liste aller Impressions

---

## API Endpoints

### Track Ad View (Impression)

```http
POST /api/worldminiapp/ads/impression/{adId}
```

Wird automatisch aufgerufen wenn Ad im Feed erscheint.

**Response:**
```json
{
  "success": true,
  "impressionId": 123
}
```

### Track Ad Click

```http
POST /api/worldminiapp/ads/click/{adId}
Content-Type: application/json

{
  "impressionId": 123
}
```

Wird aufgerufen wenn User auf Ad klickt.

**Response:**
```json
{
  "success": true,
  "targetUrl": "https://example.com"
}
```

### Get Analytics (Admin Only)

```http
GET /api/worldminiapp/ads/analytics
```

Returns all ads with stats.

---

## Frontend Integration

### Ad im Feed anzeigen

Beispiel HTML/JS:

```html
<div class="ad-container" data-ad-id="@ad.Id">
    @if (ad.IsVideo)
    {
        <video src="@ad.MediaUrl" poster="@ad.ThumbnailUrl"
               autoplay muted loop playsinline></video>
    }
    else
    {
        <img src="@ad.MediaUrl" alt="@ad.Title" />
    }

    <div class="ad-overlay">
        <div class="ad-badge">Sponsored</div>
        <h3>@ad.Title</h3>
        @if (!string.IsNullOrEmpty(ad.TargetUrl))
        {
            <a href="@ad.TargetUrl" class="ad-cta" onclick="trackAdClick(@ad.Id)">
                @(ad.CtaText ?? "Learn More")
            </a>
        }
    </div>
</div>

<script>
// Track impression when ad enters viewport
const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            const adId = entry.target.dataset.adId;
            fetch(`/api/worldminiapp/ads/impression/${adId}`, { method: 'POST' })
                .then(r => r.json())
                .then(data => {
                    // Store impressionId for click tracking
                    entry.target.dataset.impressionId = data.impressionId;
                });
            observer.unobserve(entry.target);
        }
    });
}, { threshold: 0.5 });

document.querySelectorAll('.ad-container').forEach(ad => observer.observe(ad));

// Track clicks
function trackAdClick(adId) {
    const container = document.querySelector(`[data-ad-id="${adId}"]`);
    const impressionId = container.dataset.impressionId;

    fetch(`/api/worldminiapp/ads/click/${adId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ impressionId: parseInt(impressionId) })
    });
}
</script>
```

---

## Database Schema

### WorldAppAds

| Column | Type | Description |
|--------|------|-------------|
| Id | INT | Primary Key |
| Title | NVARCHAR(200) | Ad title |
| Description | NVARCHAR(500) | Optional description |
| MediaUrl | NVARCHAR(1000) | Image/Video URL |
| ThumbnailUrl | NVARCHAR(1000) | Video thumbnail |
| IsVideo | BIT | Is this a video ad? |
| TargetUrl | NVARCHAR(1000) | Click destination |
| CtaText | NVARCHAR(50) | Call-to-action text |
| IsActive | BIT | Currently active? |
| StartDate | DATETIME2 | Show from this date |
| EndDate | DATETIME2 | Show until this date |
| ShowEveryXVideos | INT | Frequency (every X videos) |
| Priority | INT | 1-100 (higher = more often) |
| ViewCount | INT | Total impressions |
| ClickCount | INT | Total clicks |
| TargetAudience | NVARCHAR(20) | all/orb/device |
| CreatedAt | DATETIME2 | Creation timestamp |
| UpdatedAt | DATETIME2 | Last update |
| CreatedByUserHash | NVARCHAR(200) | Creator hash |

### WorldAppAdImpressions

| Column | Type | Description |
|--------|------|-------------|
| Id | INT | Primary Key |
| AdId | INT | FK to WorldAppAds |
| UserHash | NVARCHAR(200) | Viewer hash (nullable) |
| WasClicked | BIT | Was this impression clicked? |
| ViewedAt | DATETIME2 | Impression timestamp |
| ClickedAt | DATETIME2 | Click timestamp (nullable) |
| UserAgent | NVARCHAR(200) | User's device info |

---

## Best Practices

### Ad Qualität

✅ **Bilder**: Mindestens 1080x1920 (9:16 Portrait)
✅ **Videos**: MP4, max 30 Sekunden, mit Audio
✅ **Dateigröße**: Bilder <2MB, Videos <10MB
✅ **CTA**: Klar und actionable ("Jetzt kaufen", nicht "Mehr Info")

### Platzierung

- **5-7 Videos** = Gute Balance zwischen UX und Monetization
- **Zu oft** (jedes 2. Video) = Nervt User
- **Zu selten** (alle 20 Videos) = Wenig Impressions

### Targeting

- **Orb Users** = Premium Audience, higher engagement
- **Device Users** = Größere Reichweite
- **All** = Maximum Reichweite

### A/B Testing

1. Erstelle 2 Ads mit gleicher Priority
2. Lass beide 1 Woche laufen
3. Vergleiche CTR in Analytics
4. Deaktiviere schlechter performenden Ad

---

## Troubleshooting

### Ad wird nicht angezeigt

Checke:
- ✅ IsActive = true?
- ✅ StartDate in der Vergangenheit?
- ✅ EndDate in der Zukunft?
- ✅ MediaUrl erreichbar? (Test im Browser)
- ✅ ShowEveryXVideos richtig gesetzt?

### Tracking funktioniert nicht

Checke:
- ✅ JavaScript im Frontend korrekt?
- ✅ API Endpoints erreichbar?
- ✅ Browser Console für Errors

### Analytics zeigen keine Daten

- ✅ Ads müssen min. 1x gezeigt worden sein
- ✅ Analytics-Fenster = letzte 30 Tage
- ✅ Checke Impressions-Tabelle direkt in SQL

---

## Performance

### Database Indexes

Die SQL-Scripts erstellen automatisch:
- Index auf `(IsActive, Priority, ViewCount)` für schnelle Ad-Selection
- Index auf `(AdId, ViewedAt)` für Analytics-Queries
- Index auf `UserHash` für User-Tracking

### Caching

AdInjectionService cached keine Daten - Ads werden live aus DB geladen.
Falls Performance-Probleme:

```csharp
// In AdInjectionService, füge IMemoryCache hinzu
_cache.Set("active_ads", activeAds, TimeSpan.FromMinutes(5));
```

---

## Support

Bei Fragen:
1. Checke dieses README
2. Schaue in den Code-Comments
3. Teste im Dev-Environment erst

Happy Advertising! 🎯
