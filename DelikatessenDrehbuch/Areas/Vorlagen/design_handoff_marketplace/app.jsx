/* Marketplace App — main */

const { useState, useEffect } = React;

const TWEAKS_DEFAULTS = /*EDITMODE-BEGIN*/{
  "variant": "A",
  "showAll": true,
  "background": "cream",
  "accentGradient": true
}/*EDITMODE-END*/;

function MarketplaceShell() {
  const [tweaks, setTweak] = window.useTweaks ? window.useTweaks(TWEAKS_DEFAULTS) : [TWEAKS_DEFAULTS, () => {}];
  const [activeFilter, setActiveFilter] = useState('all');

  const filters = [
    { id: 'all', label: 'Alle', count: PLANS.length },
    { id: 'meal', label: 'Ernährung', icon: '🥑' },
    { id: 'workout', label: 'Workout', icon: '💪' },
    { id: 'combo', label: 'Kombi', icon: '🌿' },
    { id: 'premium', label: 'Premium' },
  ];

  let plans = PLANS;
  if (!tweaks.showAll) {
    plans = [PLANS[0]];
  }

  const renderCard = (plan) => {
    if (tweaks.variant === 'A') return <CardVariantA key={plan.id} plan={plan} />;
    if (tweaks.variant === 'B') return <CardVariantB key={plan.id} plan={plan} />;
    if (tweaks.variant === 'C') return <CardVariantC key={plan.id} plan={plan} />;
    return null;
  };

  const bgMap = {
    cream: 'var(--bg-cream)',
    cream2: '#ede5d2',
    white: '#ffffff',
    sage: '#e8ede0',
  };

  return (
    <div className="mp-screen" style={{ background: bgMap[tweaks.background] || 'var(--bg-cream)' }}>
      {/* Top app bar (in-page) */}
      <div className="mp-header">
        <button className="mp-back" aria-label="Zurück">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <path d="M15 18l-6-6 6-6"/>
          </svg>
        </button>
        <div style={{ flex: 1, marginLeft: 4 }}>
          <h1>Marktplatz</h1>
          <div className="sub">Pläne kaufen & verkaufen</div>
        </div>
        <button className="mp-sell-btn">
          <span className="plus">+</span> Verkaufen
        </button>
      </div>

      {/* Wallet */}
      <div className="mp-wallet">
        <div className="mp-wallet-row">
          <div>
            <div className="mp-wallet-label">World Wallet</div>
            <div className="mp-wallet-status">
              <span style={{ display: 'inline-block', width: 6, height: 6, borderRadius: '50%', background: '#5fa052', marginRight: 5, verticalAlign: 'middle' }}></span>
              Verbunden · 2.84 WLD
            </div>
          </div>
          <div className="mp-wallet-btns">
            <button className="mp-wallet-btn primary">Meine Pläne</button>
            <button className="mp-wallet-btn">Aktivität</button>
          </div>
        </div>
      </div>

      {/* Filter chips */}
      <div className="mp-filters">
        {filters.map(f => (
          <button
            key={f.id}
            className={`mp-chip ${activeFilter === f.id ? 'active' : ''}`}
            onClick={() => setActiveFilter(f.id)}>
            {f.icon && <span>{f.icon}</span>}
            {f.label}
            {f.count != null && <span style={{ opacity: 0.6, marginLeft: 2 }}>({f.count})</span>}
          </button>
        ))}
      </div>

      {/* Feed */}
      <div className="mp-feed">
        {plans.map(renderCard)}
      </div>

      {/* Bottom nav */}
      <div className="mp-bottom">
        <button className="mp-bottom-btn">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z"/></svg>
          Home
        </button>
        <button className="mp-bottom-btn">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.7 21a2 2 0 01-3.4 0"/></svg>
          <span className="badge">2</span>
          Aktiv
        </button>
        <button className="mp-fab">+</button>
        <button className="mp-bottom-btn active">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35"/></svg>
          Markt
        </button>
        <button className="mp-bottom-btn">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
          Profil
        </button>
      </div>

      {/* Tweaks panel */}
      {window.TweaksPanel && (
        <window.TweaksPanel title="Tweaks">
          <window.TweakSection title="Karten-Variante">
            <window.TweakRadio
              value={tweaks.variant}
              onChange={(v) => setTweak('variant', v)}
              options={[
                { value: 'A', label: 'A · Magazine' },
                { value: 'B', label: 'B · Tabbed' },
                { value: 'C', label: 'C · Premium' },
              ]} />
          </window.TweakSection>
          <window.TweakSection title="Anzeige">
            <window.TweakToggle label="Alle Pläne zeigen" value={tweaks.showAll} onChange={(v) => setTweak('showAll', v)} />
          </window.TweakSection>
          <window.TweakSection title="Hintergrund">
            <window.TweakSelect
              value={tweaks.background}
              onChange={(v) => setTweak('background', v)}
              options={[
                { value: 'cream', label: 'Cream' },
                { value: 'cream2', label: 'Warm Sand' },
                { value: 'sage', label: 'Sage' },
                { value: 'white', label: 'White' },
              ]} />
          </window.TweakSection>
        </window.TweaksPanel>
      )}
    </div>
  );
}

window.MarketplaceShell = MarketplaceShell;
