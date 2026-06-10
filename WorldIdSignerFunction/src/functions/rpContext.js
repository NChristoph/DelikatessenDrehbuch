const { app } = require('@azure/functions');

// Offizielle World-ID-Signierfunktion. Hinweis: Die Doku zeigt den Import aus
// "@worldcoin/idkit-core/signing". Sollte das beim installierten Paketstand nicht
// existieren, ist die Funktion stattdessen in "@worldcoin/idkit-server" zu finden
// (dann diese Zeile entsprechend ändern). Beide kapseln dieselbe (WASM-basierte)
// RP-Signatur – NICHT selbst nachbauen.
const { signRequest } = require('@worldcoin/idkit-core/signing');

/**
 * POST /api/worldid/rp-context
 * Body:    { "action": "login-delikatessendrehbuch" }
 * Returns: { rp_id, nonce, created_at, expires_at, signature }
 *
 * Dieser Endpunkt signiert das rp_context serverseitig (RP-Signatur ist in
 * IDKit 4.0 Pflicht). Der geheime Signing-Key verlässt die Function nie – er
 * kommt aus den App Settings (idealerweise als Key-Vault-Referenz).
 */
app.http('rpContext', {
    methods: ['POST'],
    // 'function' => Aufruf benötigt den Function-Key (?code=...). Zusätzlich unten
    // ein optionaler Shared-Secret-Header als zweite Schranke (Defense in Depth).
    authLevel: 'function',
    route: 'worldid/rp-context',
    handler: async (request, context) => {
        try {
            // Optionaler Shared-Secret-Schutz zwischen C#-Backend und Signer
            // (gleiches Secret wie X-Agent-Api-Key / INTERNAL_API_KEY).
            const expectedKey = process.env.INTERNAL_API_KEY;
            if (expectedKey && request.headers.get('x-agent-api-key') !== expectedKey) {
                return { status: 401, jsonBody: { error: 'Unauthorized' } };
            }

            // Konfiguration. ACHTUNG zum Namen "World_4.0_SingKey":
            // Env-Var-Namen mit Punkt sind unter Linux problematisch (POSIX erlaubt
            // keine Punkte in Env-Namen; Azure-Linux kann sie verändern). Falls die
            // Function unter Linux läuft, besser auf "World_4_0_SingKey" umbenennen.
            const signingKeyHex = process.env['World_4.0_SingKey'] || process.env['World_4_0_SingKey'];
            const rpId = process.env['WORLD_ID_RP_ID'];

            if (!signingKeyHex || !rpId) {
                context.error('Signer not configured: World_4.0_SingKey oder WORLD_ID_RP_ID fehlt.');
                return { status: 500, jsonBody: { error: 'Signer not configured' } };
            }

            let body = {};
            try { body = await request.json(); } catch (_) { /* leerer Body ok */ }
            const action = (body && body.action) || 'login-delikatessendrehbuch';

            // RP-Signatur erzeugen.
            const { sig, nonce, createdAt, expiresAt } = signRequest({
                signingKeyHex,
                action
            });

            // Exakt dieses Format erwartet das C#-Backend (AuthController.WorldIdRequestContext),
            // das es 1:1 als rp_context ans Frontend (IDKit.request) weiterreicht.
            return {
                jsonBody: {
                    rp_id: rpId,
                    nonce,
                    created_at: createdAt,
                    expires_at: expiresAt,
                    signature: sig
                }
            };
        } catch (err) {
            context.error('rp-context signing failed', err);
            // Keine internen Details an den Aufrufer.
            return { status: 500, jsonBody: { error: 'Signing failed' } };
        }
    }
});
