# WorldIdSignerFunction

Eigenständige **Node-Azure-Function**, die das **World ID 4.0 `rp_context`** signiert
(RP-Signatur ist in IDKit 4.0 Pflicht). Hält als einzige Komponente den geheimen
Signing-Key. Die C#-Haupt-App ruft diese Function und kennt **kein** Secret.

```
Frontend (IDKit) ── /WorldMiniApp/Auth/WorldIdRequestContext ──▶ C# AuthController
                                                                      │
                                                 POST {NodeSignerUrl} │  (X-Agent-Api-Key)
                                                                      ▼
                                                       WorldIdSignerFunction (diese Function)
                                                       signRequest(World_4.0_SingKey) ──▶ rp_context
```

## Lokal starten

```bash
cd WorldIdSignerFunction
npm install
func start            # benötigt Azure Functions Core Tools v4
```

Endpoint (lokal): `POST http://localhost:7071/api/worldid/rp-context`
Body: `{ "action": "login-delikatessendrehbuch" }`

## App Settings / Secrets

| Setting             | Bedeutung                                                        |
|---------------------|------------------------------------------------------------------|
| `World_4.0_SingKey` | RP-Signing-Key (geheim) aus dem Developer Portal                 |
| `WORLD_ID_RP_ID`    | `rp_xxx` aus dem Developer Portal                                |
| `INTERNAL_API_KEY`  | optionales Shared-Secret (muss zu C# `TradingAgent:InternalApiKey` passen) |

### Secret über Key Vault (empfohlen, NICHT als Klartext)

1. Secret im Key Vault anlegen — **Name ohne Punkte**, z. B. `World-4-0-SingKey`
   (Key-Vault-Secret-Namen erlauben keine Punkte).
2. Der Function App eine **Managed Identity** geben und im Key Vault die Rolle
   *Key Vault Secrets User* (Get) zuweisen.
3. App Setting als **Key-Vault-Referenz** setzen:
   ```
   World_4.0_SingKey = @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/World-4-0-SingKey/)
   ```
   Im Code bleibt es `process.env["World_4.0_SingKey"]`, der Wert kommt aus dem Vault.

> ⚠️ **Linux-Hosting:** Env-Var-Namen mit Punkt (`World_4.0_SingKey`) sind unter Linux
> problematisch. Läuft die Function auf Linux, das Setting auf `World_4_0_SingKey`
> umbenennen (der Code liest beide Varianten).

## Verbindung zur C#-App

Nach dem Deploy die Function-URL (inkl. `?code=<function-key>`) in der C#-App setzen:

```json
"WorldId": {
  "AppId": "app_xxx",
  "RpId": "rp_xxx",
  "Action": "login-delikatessendrehbuch",
  "Environment": "production",
  "NodeSignerUrl": "https://<functionapp>.azurewebsites.net/api/worldid/rp-context?code=<function-key>"
}
```

## Hinweis zum Signier-Paket

Der Code importiert `signRequest` aus `@worldcoin/idkit-core/signing`. Falls dieser
Pfad beim installierten Paketstand nicht existiert, liegt die Funktion in
`@worldcoin/idkit-server` — dann den Import in `src/functions/rpContext.js` anpassen
und die Dependency in `package.json` entsprechend ergänzen. Die RP-Signatur ist
WASM-basiert und darf **nicht** selbst nachgebaut werden.
