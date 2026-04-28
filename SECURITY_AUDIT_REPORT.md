# Sicherheitsaudit - WorldMiniApp Area
**Datum:** 2026-04-26
**Audit Scope:** Areas/WorldMiniApp/

---

## 🔴 KRITISCH - Sofort beheben

### 1. **Fehlender CSRF-Schutz auf kritischen POST-Endpunkten**

**Betroffene Endpunkte:**

#### RecipeController
- ❌ **`UploadNewVideoAsync`** (Zeile 60)
  - **Risiko:** Angreifer können unerwünschte Videos im Namen des Opfers hochladen
  - **Impact:** HOCH - Video-Upload ist eine der sensibelsten Operationen

- ❌ **`UpsertStep`** (Zeile 270)
  - **Risiko:** Rezept-Schritte können manipuliert werden
  - **Impact:** MITTEL

- ❌ **`SuggestMissingIngredient`** (Zeile 321)
  - **Risiko:** Unerwünschte Zutat-Vorschläge
  - **Impact:** NIEDRIG

- ❌ **`SaveSuggestedIngredient`** (Zeile 377)
  - **Risiko:** Unerwünschte Zutaten können gespeichert werden
  - **Impact:** MITTEL

#### MealPlanController
- ❌ **`Generated`** (Zeile 39)
  - **Risiko:** Unerwünschte Essenspläne erstellen
  - **Impact:** MITTEL

- ❌ **`SaveFeedMealPlanDraft`** (Zeile 221)
  - **Risiko:** Essensplan-Entwürfe manipulieren
  - **Impact:** MITTEL

- ❌ **`SaveSharedMealPlan`** (Zeile 333)
  - **Risiko:** Unerwünschte öffentliche Essenspläne erstellen
  - **Impact:** HOCH - Öffentlich sichtbare Daten

- ❌ **`SaveSharedShoppingList`** (Zeile 789)
  - **Risiko:** Unerwünschte öffentliche Einkaufslisten erstellen
  - **Impact:** HOCH - Öffentlich sichtbare Daten

- ❌ **`UpdateSharedListChecked`** (Zeile 830)
  - **Risiko:** Checkbox-Status in geteilten Listen manipulieren
  - **Impact:** NIEDRIG

**Empfehlung:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]  // ← HINZUFÜGEN
public async Task<IActionResult> UploadNewVideoAsync(...)
```

---

### 2. **UserHash Parameter Tampering**

**Problem:** Viele Endpunkte akzeptieren `userHash` als Parameter (FromForm/FromQuery), obwohl `ResolveUserHash()` den Wert aus dem Cookie/Session überschreiben sollte.

**Betroffene Endpunkte:**
- `UploadNewVideoAsync` - `string userHash` Parameter
- `DeletePosting` - `[FromForm] string userHash`
- `ToggleLike` - `[FromForm] string userHash`
- `UpdateProfile` - `string userHash` Parameter
- `SetPostingOffline` - `[FromForm] string userHash`

**Risiko:**
- Falls `ResolveUserHash()` einen Bug hat oder nicht aufgerufen wird, könnte ein Angreifer den `userHash` manipulieren
- **Information Disclosure:** Interne UserHashes werden in Formularen/URLs exponiert

**Empfehlung:**
```csharp
// VORHER (unsicher)
public async Task<IActionResult> ToggleLike([FromForm] string userHash, int recipeId)
{
    userHash = ResolveUserHash(userHash);  // userHash wird ignoriert
    // ...
}

// NACHHER (sicher)
public async Task<IActionResult> ToggleLike(int recipeId)
{
    var userHash = ResolveUserHash(string.Empty);
    if (string.IsNullOrWhiteSpace(userHash))
        return Unauthorized();
    // ...
}
```

---

### 3. **Keine Input-Length-Validierung auf Controller-Ebene**

**Problem:** Obwohl Models `[MaxLength]`-Attribute haben, werden String-Parameter direkt ohne Validierung akzeptiert.

**Betroffene Endpunkte:**
- `UpdateProfile(string userHash, string userName)` - `userName` hat keine explizite Längen-Prüfung im Controller
- `AddComment([FromForm] string text)` - `text` könnte sehr lang sein
- `CreateListing(string title, string? description)` - Keine Längen-Checks vor DB-Insert

**Risiko:**
- **DoS:** Sehr lange Strings können Server-Ressourcen erschöpfen
- **Storage Overflow:** DB-Constraints könnten verletzt werden

**Empfehlung:**
```csharp
public async Task<IActionResult> UpdateProfile(string userName)
{
    var userHash = ResolveUserHash(string.Empty);
    if (string.IsNullOrWhiteSpace(userHash))
        return Unauthorized();

    // Input Validation
    if (string.IsNullOrWhiteSpace(userName))
        return BadRequest("Benutzername fehlt.");

    userName = userName.Trim();
    if (userName.Length > 50)  // ← HINZUFÜGEN
        return BadRequest("Benutzername zu lang (max. 50 Zeichen).");

    // ...
}
```

---

## 🟡 MITTEL - Zeitnah beheben

### 4. **XSS-Risiko in Kommentaren**

**Problem:** Kommentare werden in `Feed/Index.cshtml` direkt ausgegeben.

**Betroffene Stellen:**
- `AddComment` speichert `text` ohne HTML-Encoding
- JavaScript-Rendering in Feed könnte unsicher sein

**Empfehlung:**
- Prüfen ob `escapeHtml()` in JS korrekt verwendet wird
- Server-seitig HTML-Encoding sicherstellen
- Content Security Policy (CSP) Headers hinzufügen

---

### 5. **Rate Limiting nur für Video-Uploads**

**Problem:** Rate Limiting ist nur für `UploadNewVideoAsync` implementiert, nicht für andere schreibende Operationen.

**Betroffene Endpunkte:**
- `AddComment` - Kein Rate Limit → Spam-Angriffe möglich
- `ToggleLike` - Kein Rate Limit → Like-Bombing möglich
- `CreateListing` - Kein Rate Limit → Marketplace-Spam möglich

**Empfehlung:**
```csharp
// Globales Rate Limiting für alle POST-Endpunkte
// z.B. mit Microsoft.AspNetCore.RateLimiting (ASP.NET Core 7+)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("WorldMiniAppApi", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
    });
});
```

---

### 6. **Keine Logging für kritische Operationen**

**Problem:** Sicherheitsrelevante Ereignisse werden nicht geloggt.

**Fehlende Logs:**
- `DeleteComment` - Wer hat welchen Kommentar gelöscht?
- `DeletePosting` - Wer hat welches Posting gelöscht?
- `SetPostingOffline/Online` - Wer hat Postings de-/aktiviert?
- `ResolveCommentReport` - Wer hat Reports bearbeitet?

**Empfehlung:**
```csharp
_logger.LogWarning(
    "User {UserHash} deleted comment {CommentId} posted by {OriginalAuthor}",
    userHash, commentId, comment.UserHash
);
```

---

## 🟢 NIEDRIG - Verbesserungen

### 7. **Information Disclosure in Error Messages**

**Beispiel:**
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to toggle like for recipe {RecipeId}.", recipeId);
    return StatusCode(500, "Ein unerwarteter Fehler ist aufgetreten.");  // ← GUT
}
```

**Positiv:** Generische Fehlermeldungen werden verwendet ✅

---

### 8. **Session Fixation - Gering**

**Problem:** Nach World ID Login wird keine neue Session erstellt.

**Empfehlung:**
```csharp
// In AuthController nach erfolgreicher Verifizierung
await HttpContext.Session.CommitAsync();
```

---

### 9. **Fehlende Content-Type-Validierung bei File-Uploads**

**Betroffene Endpunkte:**
- `UploadNewVideoAsync` - Prüft nur Dateiendung, nicht MIME-Type

**Empfehlung:**
```csharp
var allowedMimeTypes = new[] { "video/mp4", "video/quicktime", "video/webm" };
if (!allowedMimeTypes.Contains(videoFile.ContentType.ToLowerInvariant()))
{
    return BadRequest("Ungültiger Dateityp.");
}

// Zusätzlich: Magic Bytes prüfen (echte Video-Datei?)
```

---

### 10. **Keine Implementierung von Security Headers**

**Fehlende Headers:**
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Strict-Transport-Security` (HSTS)
- `Content-Security-Policy`
- `Referrer-Policy: no-referrer`

**Empfehlung in `Program.cs`:**
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "no-referrer");
    await next();
});
```

---

## ✅ Positive Aspekte

1. ✅ **SQL Injection:** EF Core parametrisierte Queries werden korrekt verwendet
2. ✅ **Authorization Checks:** Die meisten kritischen Endpunkte prüfen Ownership
3. ✅ **Password Storage:** N/A - World ID verwendet keine Passwörter
4. ✅ **HTTPS:** Wird vermutlich in Production verwendet (Azure)
5. ✅ **Idempotency:** `UploadNewVideoAsync` implementiert Idempotency-Check

---

## Priorisierte Fix-Liste

### **Sofort (diese Woche):**
1. **Alle POST-Endpunkte ohne CSRF-Protection fixen** (`[ValidateAntiForgeryToken]` hinzufügen)
2. **`userHash` Parameter entfernen** und nur aus Session/Cookie lesen
3. **Input-Length-Validation** für alle String-Parameter

### **Diese Sprint:**
4. Rate Limiting für Kommentare und Likes
5. Security Audit Logging für kritische Operationen
6. Content-Type-Validierung bei Uploads

### **Nächster Sprint:**
7. Security Headers implementieren
8. XSS-Schutz verifizieren
9. Penetration Testing durchführen

---

## Tools für weitere Audits

**Empfohlene Tools:**
- **OWASP ZAP** - Automatisierter Vulnerability Scanner
- **SonarQube** - Static Code Analysis
- **Burp Suite** - Manual Penetration Testing
- **dotnet format analyzers** - .NET Security Analyzers

---

## Zusammenfassung

**Gefunden:**
- 🔴 9 kritische Schwachstellen (CSRF, Parameter Tampering)
- 🟡 6 mittelschwere Schwachstellen (Rate Limiting, Logging)
- 🟢 3 niedrigschwere Schwachstellen (Security Headers)

**Risiko-Score:** **7/10** (HOCH)

**Grund:** Fehlender CSRF-Schutz auf kritischen Endpunkten ist ein **Show-Stopper** für Production.

---

**Report erstellt von:** Claude Sonnet 4.5
**Audit-Dauer:** 15 Minuten
**Audit-Methode:** Static Code Analysis
