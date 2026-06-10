# World ID 4.0 / IDKit – Node-Signer (rp_context)

IDKit 4.0 verlangt ein **backend-signiertes `rp_context`** (RP-Signatur), sonst öffnet
der Verify-Flow nicht. Die offizielle Signierfunktion `signRequest` liegt **nur als
Node/JS-Paket** vor (`@worldcoin/idkit-core/signing`). Da unser App-Backend C# ist,
signieren wir im **bestehenden Node.js-Service** und reichen das Ergebnis über
`AuthController.WorldIdRequestContext` ans Frontend durch.

## 1. Paket im Node-Service installieren

```bash
npm i @worldcoin/idkit-core
```

## 2. Signing-Endpoint hinzufügen (Express-Beispiel)

```js
import { signRequest } from "@worldcoin/idkit-core/signing";

// POST /api/worldid/rp-context   Body: { action: "login-delikatessendrehbuch" }
app.post("/api/worldid/rp-context", (req, res) => {
  // Optionaler Shared-Secret-Schutz (gleiches Secret wie X-Agent-Api-Key in C#).
  const expected = process.env.INTERNAL_API_KEY;
  if (expected && req.header("X-Agent-Api-Key") !== expected) {
    return res.status(401).json({ error: "Unauthorized" });
  }

  const action = (req.body && req.body.action) || "login-delikatessendrehbuch";

  // Signing-Key + rp_id stammen aus dem Worldcoin Developer Portal.
  // Env-Variablen-Name wie bei euch festgelegt: World_4.0_SingKey
  // (Bracket-Zugriff, weil der Name einen Punkt enthält.)
  const { sig, nonce, createdAt, expiresAt } = signRequest({
    signingKeyHex: process.env["World_4.0_SingKey"],
    action,
  });

  // Exakt dieses Format erwartet das C#-Backend (-> wird 1:1 an IDKit.request weitergereicht).
  res.json({
    rp_id: process.env.WORLD_ID_RP_ID,
    nonce,
    created_at: createdAt,
    expires_at: expiresAt,
    signature: sig,
  });
});
```

## 3. Env-Variablen im Node-Service

| Variable            | Quelle                                   |
|---------------------|------------------------------------------|
| `World_4.0_SingKey` | Developer Portal (RP-Signing-Key, geheim)|
| `WORLD_ID_RP_ID`    | Developer Portal (`rp_xxx`)              |
| `INTERNAL_API_KEY`  | optionaler Shared-Secret-Schutz          |

## 4. Gegenstück im C#-Backend

`appsettings.json`:

```json
"WorldId": {
  "AppId": "app_xxx",                       // World ID app_id (NICHT die Mini-App-App-ID)
  "RpId": "rp_xxx",                          // wie im Node-Service
  "Action": "login-delikatessendrehbuch",
  "Environment": "production",
  "NodeSignerUrl": "https://<node-service>/api/worldid/rp-context"
}
```

- `AuthController.WorldIdRequestContext` ruft `NodeSignerUrl` auf und gibt
  `{ app_id, action, environment, allow_legacy_proofs, rp_context }` ans Frontend.
- `AuthController.VerifyWorldId` leitet das IDKit-Result an
  `https://developer.world.org/api/v4/verify/{rp_id}` weiter und meldet bei Erfolg
  den Nutzer an (Identität = `nullifier`).

> Hinweis: Alle Werte stammen aus dem Worldcoin Developer Portal (World ID 4.0 / RP
> registrieren). Ohne `RP_SIGNING_KEY`, `rp_id` und `app_id` ist der Flow nicht lauffähig.
