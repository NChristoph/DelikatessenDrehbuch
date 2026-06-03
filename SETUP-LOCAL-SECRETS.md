# 🔐 Lokale Entwicklungs-Secrets einrichten

## ⚠️ WICHTIG: Diese Dateien sind in .gitignore!

Die folgenden Dateien werden **NICHT** ins Git-Repository gepusht:
- `appsettings.Development.json`
- `Properties/launchSettings.json`

Sie müssen diese Dateien lokal erstellen!

---

## 🚀 Setup für neue Entwickler

### Schritt 1: Template-Dateien kopieren

```bash
# Im Projekt-Root
cd DelikatessenDrehbuch

# appsettings.Development.json erstellen
cp appsettings.Development.json.template appsettings.Development.json

# launchSettings.json erstellen
cd Properties
cp launchSettings.json.template launchSettings.json
cd ..
```

### Schritt 2: Admin-Passwort-Hash generieren

1. Starten Sie die App:
   ```bash
   dotnet run
   ```

2. Öffnen Sie: `https://localhost:5001/WorldMiniApp/Admin/GeneratePasswordHash`

3. Geben Sie Ihr gewünschtes Admin-Passwort ein

4. **Kopieren Sie den generierten BCrypt-Hash**

### Schritt 3: Secrets einfügen

Öffnen Sie die beiden Dateien und ersetzen Sie die Platzhalter:

**In `appsettings.Development.json`:**
```json
"WorldMiniApp": {
  "SuperUserHash": "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839",
  "AdminPassword": "HIER_DEN_KOPIERTEN_HASH_EINFÜGEN",
  ...
}
```

**In `Properties/launchSettings.json`:**
```json
"environmentVariables": {
  ...
  "WorldMiniApp__SuperUserHash": "0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839",
  "WorldMiniApp__AdminPassword": "HIER_DEN_KOPIERTEN_HASH_EINFÜGEN"
}
```

### Schritt 4: Weitere Secrets

Füllen Sie auch diese Werte aus (bei Bedarf):
- `Bunny_Net_Passwort_Lager`
- `Bunny_Net_Api_Stream`
- `Blob_Conection_String`
- `EmailPasswort`
- `OPENAI_API_KEY`
- `gemini_api_key`

---

## 🔒 Sicherheit

✅ **Diese Dateien werden NICHT ins Git gepusht**
✅ **Jeder Entwickler hat seine eigenen lokalen Secrets**
✅ **Production-Secrets sind in Azure Application Settings**

❌ **NIEMALS** Secrets direkt ins Git committen!
❌ **NIEMALS** die Template-Dateien mit echten Secrets überschreiben!

---

## 📚 Weitere Informationen

- **Admin Security Guide:** `ADMIN-SECURITY-GUIDE.md`
- **Azure Setup:** `azure-config-secrets.ps1`
- **Key Vault:** `AZURE-KEY-VAULT-SETUP.md`
