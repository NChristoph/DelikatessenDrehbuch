(function () {
    async function startWalletVerifyForPurchase() {
        if (!window.MiniKit?.commandsAsync?.walletAuth) {
            return '';
        }

        const nonceResp = await fetch('/WorldMiniApp/Auth/Nonce', { method: 'GET' });
        if (!nonceResp.ok) throw new Error('Nonce konnte nicht geladen werden.');

        const nonceData = await nonceResp.json();
        const nonce = nonceData?.nonce;
        if (!nonce) throw new Error('Nonce fehlt.');

        const { finalPayload } = await window.MiniKit.commandsAsync.walletAuth({
            nonce,
            statement: 'Sign in to Delikatessen Drehbuch',
            expirationTime: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)
        });

        if (finalPayload?.status !== 'success') {
            return '';
        }

        const rememberLogin = (localStorage.getItem('remember_login') || 'false') === 'true';

        const completeResp = await fetch('/WorldMiniApp/Auth/CompleteSiwe', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                payload: {
                    status: finalPayload.status,
                    message: finalPayload.message,
                    signature: finalPayload.signature,
                    address: finalPayload.address,
                    version: finalPayload.version || 1
                },
                nonce,
                rememberLogin
            })
        });

        if (!completeResp.ok) {
            const errText = await completeResp.text();
            throw new Error((errText || 'Wallet-Auth fehlgeschlagen').substring(0, 120));
        }

        const result = await completeResp.json();
        const walletAddress = result?.walletAddress || finalPayload.address || '';
        if (!walletAddress) {
            return '';
        }

        if (rememberLogin) {
            localStorage.setItem('WalletAddress', walletAddress);
        } else {
            localStorage.removeItem('WalletAddress');
        }
        sessionStorage.setItem('WalletAddress', walletAddress);

        return walletAddress;
    }

    window.marketplaceWalletAuth = {
        startWalletVerifyForPurchase
    };
})();
