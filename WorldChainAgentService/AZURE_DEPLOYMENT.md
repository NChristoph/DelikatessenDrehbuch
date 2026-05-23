# Azure App Service Deployment Guide

## 🎯 Ziel
Node.js Service im **gleichen App Service Plan** wie DelikatessenDrehbuch deployen (keine Extra-Kosten).

---

## 📋 Voraussetzungen

- Azure Subscription mit aktuellem App Service Plan
- Git installiert
- Node.js 18+ installiert (für lokalen Build-Test)

---

## 🚀 Schritt 1: App Service in Azure erstellen

### Option A: Azure Portal (empfohlen für Anfänger)

1. **Azure Portal öffnen:** https://portal.azure.com

2. **"App Services" suchen** → "Erstellen"

3. **Grundlagen konfigurieren:**
   - **Ressourcengruppe:** Gleiche wie DelikatessenDrehbuch
   - **Name:** `worldchain-agent-service` (wird zu: worldchain-agent-service.azurewebsites.net)
   - **Veröffentlichen:** Code
   - **Runtimestapel:** Node 18 LTS
   - **Region:** Gleiche wie DelikatessenDrehbuch

4. **App Service-Plan:**
   - ✅ **"Vorhandenen Plan verwenden"** auswählen
   - Deinen bestehenden Plan auswählen (z.B. `DelikatessenDrehbuchPlan`)

5. **Überwachen:**
   - Application Insights: Ja (empfohlen)

6. **"Überprüfen und erstellen"** → **"Erstellen"**

### Option B: Azure CLI (schneller für Profis)

```bash
# Login
az login

# App Service erstellen (im gleichen Plan wie DelikatessenDrehbuch)
az webapp create \
  --resource-group DEINE_RESOURCE_GROUP \
  --plan DEIN_APP_SERVICE_PLAN \
  --name worldchain-agent-service \
  --runtime "NODE:18-lts"
```

---

## ⚙️ Schritt 2: Umgebungsvariablen konfigurieren

Im Azure Portal:

1. **App Service öffnen** → **Konfiguration** → **Anwendungseinstellungen**

2. **Diese Variablen hinzufügen:**

| Name | Wert | Beschreibung |
|------|------|--------------|
| `WORLD_CHAIN_RPC_URL` | `https://worldchain-mainnet.g.alchemy.com/public` | World Chain RPC |
| `CHAIN_ID` | `480` | World Chain ID |
| `WLD_TOKEN_ADDRESS` | `0x2cFc85d8E48F8EAB294be644d9E25C3030863003` | WLD Token |
| `WETH_ADDRESS` | `0x4200000000000000000000000000000000000006` | WETH Token |
| `UNISWAP_V3_ROUTER` | `0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD` | Uniswap Router |
| `CSHARP_API_URL` | `https://DEINE-APP.azurewebsites.net` | URL deines C# Service |
| `ENCRYPTION_KEY` | `(Siehe appsettings.json)` | Gleicher Key wie in C# |
| `TRADE_INTERVAL_MINUTES` | `5` | Trading Intervall |
| `MIN_CONFIDENCE_PERCENT` | `60` | Min. Confidence für Trades |
| `PORT` | `8080` | ⚠️ Wichtig: Azure verwendet 8080 |
| `WEBSITE_NODE_DEFAULT_VERSION` | `18-lts` | Node.js Version |

3. **"Speichern"** klicken

---

## 📦 Schritt 3: Code deployen

### Option A: Git Deployment (empfohlen)

1. **Deployment Center öffnen:**
   - Azure Portal → App Service → **Deployment Center**

2. **Git als Quelle wählen:**
   - **Quelle:** Local Git
   - **"Speichern"** → Git-URL wird angezeigt

3. **Deployment Credentials erstellen:**
   - Deployment Center → **Anmeldeinformationen**
   - Benutzername & Passwort notieren

4. **Git Remote hinzufügen:**

```bash
cd C:\Users\Free8\source\repos\DelikatessenDrehbuch\WorldChainAgentService

# Azure Git Remote hinzufügen
git remote add azure https://DEIN-BENUTZERNAME@worldchain-agent-service.scm.azurewebsites.net/worldchain-agent-service.git

# Code pushen (Build läuft automatisch!)
git add .
git commit -m "Initial Azure deployment"
git push azure main
```

5. **Build-Log verfolgen:**
   - Azure Portal → App Service → **Log Stream**
   - Oder: https://worldchain-agent-service.scm.azurewebsites.net/api/deployments

### Option B: ZIP Deployment (schneller, aber manuell)

```bash
# Build erstellen
npm install
npm run build

# ZIP erstellen (nur dist, node_modules, package.json)
# Dann im Azure Portal → Deployment Center → ZIP Deploy
```

### Option C: GitHub Actions (automatisch bei jedem Push)

1. **GitHub Repo verbinden:**
   - Azure Portal → Deployment Center
   - **Quelle:** GitHub
   - Repo & Branch auswählen
   - Workflow-Datei wird automatisch erstellt

---

## 🔍 Schritt 4: Deployment überprüfen

### Health Check testen:

```bash
curl https://worldchain-agent-service.azurewebsites.net/health
```

**Erwartete Antwort:**
```json
{
  "status": "ok",
  "service": "World Chain Agent Service",
  "chainId": "480"
}
```

### Logs ansehen:

**Azure Portal:**
- App Service → **Log Stream**

**Via Browser:**
- https://worldchain-agent-service.scm.azurewebsites.net → **Debug Console**

---

## 🔗 Schritt 5: C# Service aktualisieren

### In Azure Portal (App Service: DelikatessenDrehbuch):

1. **Konfiguration** → **Anwendungseinstellungen**

2. **Neue Variable hinzufügen:**
   ```
   Name:  AgentKit__ServiceUrl
   Wert:  https://worldchain-agent-service.azurewebsites.net
   ```

3. **"Speichern"** → App wird automatisch neu gestartet

### Oder in appsettings.Production.json:

```json
{
  "AgentKit": {
    "ServiceUrl": "https://worldchain-agent-service.azurewebsites.net"
  }
}
```

---

## ✅ Schritt 6: End-to-End Test

1. **C# Service triggert Trade-Signal** (alle 5 Minuten)

2. **C# ruft Node.js AgentKit auf:**
   ```
   POST https://worldchain-agent-service.azurewebsites.net/api/agentkit/swap
   ```

3. **AgentKit führt Trade aus mit WLD Gas ⛽**

4. **Result wird in C# Datenbank geloggt**

**Logs checken:**
- Node.js: Azure Portal → worldchain-agent-service → Log Stream
- C#: Azure Portal → DelikatessenDrehbuch → Log Stream

---

## 🛠️ Troubleshooting

### Problem: "Application Error"

**Lösung:**
1. Log Stream öffnen
2. Fehler identifizieren (meist: fehlende Environment Variables)
3. Konfiguration → Anwendungseinstellungen überprüfen

### Problem: "Cannot find module '@coinbase/agentkit'"

**Lösung:**
```bash
# Sicherstellen, dass package.json korrekt ist
# Dann: Re-deploy
git push azure main --force
```

### Problem: C# kann Node.js nicht erreichen

**Lösung:**
1. Health Check testen (siehe oben)
2. Firewall-Regeln prüfen (sollte standardmäßig offen sein)
3. URL in C# Config überprüfen (`https://` nicht vergessen!)

### Problem: "Port 3000 already in use"

**Lösung:**
- Azure verwendet Port 8080 (nicht 3000)
- Environment Variable `PORT=8080` in Azure setzen
- Code nutzt automatisch `process.env.PORT || 3000`

---

## 📊 Kosten

- ✅ **Keine Extra-Kosten** für den App Service Plan (du zahlst nur für den Plan, nicht pro App)
- ⚠️ **Datenübertragung:** Minimale Kosten für API-Calls zwischen Services (meist < 1€/Monat)
- ⚠️ **Logs:** Application Insights kostenlos bis 5GB/Monat

---

## 🔄 Updates deployen

```bash
# Code ändern
git add .
git commit -m "Update trading logic"

# Nach Azure pushen (Build + Deploy automatisch)
git push azure main
```

**Zero-Downtime Deployment:**
- Azure führt automatisch Rolling Deployment durch
- Service bleibt während Update erreichbar

---

## 🔐 Sicherheit

### Wichtig:

1. **Private Keys NIEMALS in Code oder Environment Variables!**
   - Bleiben verschlüsselt in SQL Datenbank
   - C# entschlüsselt → sendet an Node.js zur Laufzeit

2. **HTTPS erzwingen:**
   - Azure Portal → App Service → TLS/SSL → **HTTPS Only** aktivieren

3. **CORS konfigurieren (falls Browser-Zugriff nötig):**
   ```typescript
   app.use(cors({
     origin: 'https://delikatessendrehbuch.azurewebsites.net',
     credentials: true
   }));
   ```

---

## 📚 Weitere Infos

- [Azure App Service Docs](https://docs.microsoft.com/azure/app-service/)
- [Node.js auf Azure](https://docs.microsoft.com/azure/app-service/quickstart-nodejs)
- [Coinbase AgentKit Docs](https://docs.cdp.coinbase.com/agentkit/docs/welcome)

---

## 🎉 Fertig!

Dein Trading Agent läuft jetzt auf Azure mit:
- ✅ Automatischen Trades alle 5 Minuten
- ✅ Gas-Zahlung in WLD (via AgentKit)
- ✅ Zero-Downtime Deployments
- ✅ Keine Extra-Kosten (gleicher App Service Plan)

**Service URL:** https://worldchain-agent-service.azurewebsites.net
