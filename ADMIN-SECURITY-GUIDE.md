# 🔐 Admin Security Setup Guide

## Übersicht

Dieses Projekt verwendet **BCrypt-gehashte Passwörter** mit **Session-basierter Authentifizierung** für Admin-Bereiche.

---

## 🏠 Lokale Entwicklung (Development)

### 1. Passwort-Hash generieren

1. Starten Sie die App lokal: `dotnet run`
2. Öffnen Sie: `https://localhost:5001/WorldMiniApp/Admin/GeneratePasswordHash`
3. Geben Sie Ihr Passwort ein (z.B. `DevAdmin123!`)
4. Kopieren Sie den generierten Hash

### 2. Hash in appsettings.Development.json einfügen

```json
"WorldMiniApp": {
  "SuperUserHash": "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839",
  "AdminPassword": "$2a$11$IhrGenerierterHash...",
  ...
}
```

### 3. App neu starten

Beim Login geben Sie Ihr **Klartext-Passwort** ein (`DevAdmin123!`), nicht den Hash!

---

## ☁️ Azure Production (Empfohlen)

### Option A: Application Settings (Einfach) ⭐

**Im Azure Portal:**

1. Öffnen Sie Ihren **App Service**
2. Navigieren Sie zu: **Configuration** → **Application settings**
3. Fügen Sie hinzu:

```
Name:  WorldMiniApp__AdminPassword
Value: $2a$11$IhrGenerierterProductionHash...

Name:  WorldMiniApp__SuperUserHash
Value: 0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839
```

⚠️ **Wichtig:** Doppelte Unterstriche `__` statt `:`!

4. Klicken Sie auf **Save**

**Via Azure CLI:**

```bash
az webapp config appsettings set \
  --resource-group IhrResourceGroup \
  --name IhrAppService \
  --settings WorldMiniApp__AdminPassword='$2a$11$hash...' \
              WorldMiniApp__SuperUserHash='0x2da...'
```

**PowerShell-Skript:** Siehe `azure-config-secrets.ps1`

---

### Option B: Azure Key Vault (Maximale Sicherheit)

Siehe: `AZURE-KEY-VAULT-SETUP.md`

**Vorteile:**
- ✅ Zentrale Secret-Verwaltung
- ✅ Audit-Logging
- ✅ Automatische Rotation
- ✅ RBAC-Zugriffskontrolle

---

## 🔑 Passwort-Anforderungen

**Empfohlene Stärke:**
- Mindestens 12 Zeichen
- Groß- und Kleinbuchstaben
- Zahlen
- Sonderzeichen

**Beispiele:**
- ✅ `Admin!2024$Secure#WorldApp`
- ✅ `Delika#Prod2024!`
- ❌ `admin123` (zu schwach)
- ❌ `password` (zu schwach)

---

## 🛡️ Sicherheits-Features

### 1. BCrypt-Hashing
- Verwendet BCrypt mit Work Factor 11
- Automatisches Salting
- Nicht umkehrbar (one-way hash)

### 2. Session-basierte Authentifizierung
- Admin-Session läuft nach **2 Stunden** ab
- Session wird bei Logout sofort gelöscht
- Automatische Prüfung bei jedem Admin-Zugriff

### 3. Geschützte Bereiche
- ✅ Creator Manager (`/WorldMiniApp/Admin/CreatorManager`)
- ✅ Reported Content (`/WorldMiniApp/Admin/ReportedContent`)
- ✅ Comment Moderation (`/WorldMiniApp/Feed/CommentModeration`)
- ✅ Admin-Funktionen in MyProfile

---

## 📋 Setup-Checkliste

### Development
- [ ] Passwort-Hash mit Generator erstellt
- [ ] Hash in `appsettings.Development.json` eingefügt
- [ ] SuperUserHash konfiguriert
- [ ] Login getestet

### Production (Azure)
- [ ] Passwort-Hash mit Generator erstellt
- [ ] Application Settings in Azure konfiguriert
- [ ] `appsettings.json` bereinigt (keine Secrets im Code)
- [ ] App Service neu gestartet
- [ ] Login getestet

### Optional (Key Vault)
- [ ] Azure Key Vault erstellt
- [ ] Secrets in Key Vault gespeichert
- [ ] Managed Identity aktiviert
- [ ] Access Policy konfiguriert
- [ ] NuGet Packages installiert
- [ ] `Program.cs` erweitert

---

## 🆘 Troubleshooting

### "Admin-Passwort ist nicht konfiguriert"
- Prüfen Sie, ob `WorldMiniApp:AdminPassword` (lokal) oder `WorldMiniApp__AdminPassword` (Azure) gesetzt ist
- In Azure: Configuration → Application settings → Überprüfen

### "Falsches Passwort" (obwohl korrekt)
- Prüfen Sie, ob der Hash korrekt kopiert wurde
- Generieren Sie einen neuen Hash mit dem Generator
- Bei Azure: Prüfen Sie, ob die Setting mit `__` (doppelt) geschrieben ist

### Login funktioniert nicht
1. Überprüfen Sie Sessions: `Program.cs` → `app.UseSession();`
2. Überprüfen Sie UserHash: Cookie `WorldMiniAppUserHash` muss gesetzt sein
3. Überprüfen Sie SuperUserHash: Muss mit aktuellem UserHash übereinstimmen

### Session läuft zu schnell ab
- Ändern Sie in `AdminController.cs` Zeile ~121:
  ```csharp
  var expiryTime = DateTime.UtcNow.AddHours(4); // von 2 auf 4 Stunden
  ```

---

## 🔄 Passwort ändern

### Lokal
1. Neuen Hash generieren: `/WorldMiniApp/Admin/GeneratePasswordHash`
2. In `appsettings.Development.json` aktualisieren
3. App neu starten

### Azure
1. Azure Portal → App Service → Configuration → Application settings
2. Bearbeiten Sie `WorldMiniApp__AdminPassword`
3. Neuen Hash einfügen
4. Save → App startet neu

---

## 📚 Weitere Informationen

- **BCrypt.Net Documentation:** https://github.com/BcryptNet/bcrypt.net
- **Azure App Service Configuration:** https://learn.microsoft.com/azure/app-service/configure-common
- **Azure Key Vault:** https://learn.microsoft.com/azure/key-vault/

---

## 🔒 Best Practices

1. ✅ **Niemals** Passwörter im Code committen
2. ✅ Verwenden Sie `.gitignore` für `appsettings.Development.json`
3. ✅ Verwenden Sie verschiedene Passwörter für Dev und Prod
4. ✅ Rotieren Sie Passwörter regelmäßig (empfohlen: alle 90 Tage)
5. ✅ Verwenden Sie Azure Key Vault für Production
6. ✅ Aktivieren Sie Audit-Logging in Azure
7. ✅ Verwenden Sie Managed Identity statt Service Principals

---

**Erstellt:** 2025-05-29
**Version:** 1.0
**Kontakt:** Bei Fragen siehe Projektdokumentation
