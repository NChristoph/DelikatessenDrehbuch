/* Create Offer — "Angebot erstellen" im Avocado/Marketplace-Stil
   Interaktiver Verkaufsflow:
     - 5 Angebotstypen mit je eigenen Feldern
     - schaltbare Auszahlungs-Wallets (WLD + USDT/USDC)
     - Live-Preissplit 80/20
     - Sprach-Toggle
*/

const { useState } = React;

/* ---------- Icons (24x24 stroke) ---------- */
const Ico = {
  back: (p) => <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M15 18l-6-6 6-6"/></svg>,
  chevron: (p) => <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M6 9l6 6 6-6"/></svg>,
  wallet: (p) => <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M3 7a2 2 0 012-2h12a2 2 0 012 2v1"/><path d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2v-6a2 2 0 00-2-2H5"/><circle cx="16" cy="13" r="1.2" fill="currentColor" stroke="none"/></svg>,
  info: (p) => <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...p}><circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8h.01"/></svg>,
  tag: (p) => <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M20.6 13.4l-7.2 7.2a2 2 0 01-2.8 0l-7.1-7.1A2 2 0 013 12.1V5a2 2 0 012-2h7.1a2 2 0 011.4.6l7.1 7.1a2 2 0 010 2.7z"/><circle cx="7.5" cy="7.5" r="1.4" fill="currentColor" stroke="none"/></svg>,
  check: (p) => <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M20 6L9 17l-5-5"/></svg>,
  // type icons
  calendar: (p) => <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...p}><rect x="3" y="4.5" width="18" height="16" rx="2.5"/><path d="M3 9h18M8 2.5v4M16 2.5v4"/><circle cx="8" cy="13" r="1" fill="currentColor" stroke="none"/><circle cx="12" cy="13" r="1" fill="currentColor" stroke="none"/><circle cx="16" cy="13" r="1" fill="currentColor" stroke="none"/></svg>,
  doc: (p) => <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...p}><rect x="4" y="3" width="16" height="18" rx="2.5"/><path d="M8 8h8M8 12h8M8 16h5"/></svg>,
  cube: (p) => <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M21 7.5l-9-5-9 5 9 5 9-5z"/><path d="M3 7.5v9l9 5 9-5v-9M12 12.5v9"/></svg>,
  download: (p) => <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M19 16.5A4 4 0 0017.5 9h-1.3A6 6 0 104 14.5"/><path d="M12 11v8M8.5 15.5L12 19l3.5-3.5"/></svg>,
  service: (p) => <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...p}><rect x="3" y="4" width="18" height="16" rx="2.5"/><circle cx="12" cy="10" r="2.4"/><path d="M8 16.5a4 4 0 018 0"/></svg>,
};

/* ---------- Data ---------- */
const OFFER_TYPES = [
  { id: 'plan',    label: 'Essensplan',    icon: 'calendar' },
  { id: 'recipe',  label: 'Einzelrezept',  icon: 'doc' },
  { id: 'object',  label: 'Objekt',        icon: 'cube' },
  { id: 'digital', label: 'Digital',       icon: 'download' },
  { id: 'service', label: 'Dienstleistung', icon: 'service' },
];

const PLANS = [
  { id: 'p1', name: 'Feed-Plan', created: '17.05.2026' },
  { id: 'p2', name: 'Teste das', created: '22.05.2026' },
];

const RECIPES = ['Avocado Toast Deluxe', 'Protein Power Bowl', 'Green Detox Smoothie', 'Overnight Oats'];

/* ---------- Small UI helpers ---------- */
function CoField({ children, ...rest }) {
  return <input className="co-input" {...rest} />;
}

function CoSection({ n, title, children }) {
  return (
    <div className="co-block">
      <div className="co-block-head">
        <span className="co-num">{n}</span>
        <span className="co-block-title">{title}</span>
      </div>
      {children}
    </div>
  );
}

/* ---------- Conditional field groups ---------- */
function PlanPicker({ value, onPick }) {
  return (
    <div className="co-sub">
      <div className="co-sublabel">Plan auswählen</div>
      <div className="co-stack">
        {PLANS.map(p => (
          <button key={p.id}
            className={'co-pick' + (value === p.id ? ' sel' : '')}
            onClick={() => onPick(p.id)}>
            <div className="co-pick-name">{p.name}</div>
            <div className="co-pick-meta">Erstellt: {p.created}</div>
            {value === p.id && <span className="co-pick-check"><Ico.check /></span>}
          </button>
        ))}
      </div>
    </div>
  );
}

function RecipePicker({ value, onPick }) {
  return (
    <div className="co-sub">
      <div className="co-sublabel">Rezept auswählen</div>
      <div className="co-select-wrap">
        <select className="co-select" value={value} onChange={e => onPick(e.target.value)}>
          <option value="">– Rezept wählen –</option>
          {RECIPES.map(r => <option key={r} value={r}>{r}</option>)}
        </select>
        <span className="co-select-chev"><Ico.chevron /></span>
      </div>
    </div>
  );
}

function ObjectFields({ data, set }) {
  return (
    <div className="co-sub">
      <div className="co-sublabel">Produktdetails</div>
      <div className="co-stack">
        <CoField placeholder="Bild-URL (https://…)" value={data.image || ''} onChange={e => set('image', e.target.value)} />
        <CoField placeholder="Bestand (leer = unbegrenzt)" value={data.stock || ''} onChange={e => set('stock', e.target.value)} />
        <button className="co-checkrow" onClick={() => set('shipping', !data.shipping)}>
          <span className={'co-box' + (data.shipping ? ' on' : '')}>{data.shipping && <Ico.check />}</span>
          <span>Versand nötig (Lieferadresse vom Käufer abfragen)</span>
        </button>
      </div>
    </div>
  );
}

function DigitalFields({ data, set }) {
  return (
    <div className="co-sub">
      <div className="co-sublabel">Digitales Produkt</div>
      <div className="co-stack">
        <CoField placeholder="Download-/Datei-URL (https://…)" value={data.fileUrl || ''} onChange={e => set('fileUrl', e.target.value)} />
        <CoField placeholder="Titelbild-URL (optional)" value={data.cover || ''} onChange={e => set('cover', e.target.value)} />
      </div>
      <div className="co-hint">Der Link wird erst nach dem Kauf freigeschaltet.</div>
    </div>
  );
}

function ServiceFields({ data, set }) {
  return (
    <div className="co-sub">
      <div className="co-sublabel">Dienstleistung</div>
      <CoField placeholder="Titelbild-URL (optional)" value={data.cover || ''} onChange={e => set('cover', e.target.value)} />
      <div className="co-hint">Beschreibe deine Dienstleistung unten. Nach dem Kauf wirst du benachrichtigt, um Kontakt aufzunehmen.</div>
    </div>
  );
}

/* ---------- Wallet card ---------- */
function WalletCard({ title, connected, onToggle, addr, hintConnect }) {
  return (
    <div className="co-wallet">
      <div className="co-wallet-text">
        <div className="co-wallet-title">{title}</div>
        {connected ? (
          <div className="co-wallet-ok">
            <span className="co-dot" /> Verbunden · {addr}
          </div>
        ) : (
          <div className="co-wallet-warn">{hintConnect}</div>
        )}
      </div>
      <button className={'co-wallet-btn' + (connected ? ' is-on' : '')} onClick={onToggle}>
        <Ico.wallet />
        {connected ? 'Trennen' : 'Verbinden'}
      </button>
    </div>
  );
}

/* ---------- Main screen ---------- */
function CreateOffer() {
  const [type, setType] = useState('plan');
  const [planId, setPlanId] = useState('p1');
  const [recipe, setRecipe] = useState('');
  const [fields, setFields] = useState({});
  const [title, setTitle] = useState('');
  const [desc, setDesc] = useState('');
  const [price, setPrice] = useState('10');
  const [wldOn, setWldOn] = useState(false);
  const [usdOn, setUsdOn] = useState(false);
  const [lang, setLang] = useState('EN');

  const set = (k, v) => setFields(f => ({ ...f, [k]: v }));

  const priceNum = parseFloat(price) || 0;
  const youGet = (priceNum * 0.8).toFixed(2);
  const platform = (priceNum * 0.2).toFixed(2);

  // validity
  const typeOk =
    type === 'plan'   ? !!planId :
    type === 'recipe' ? !!recipe :
    true;
  const valid = wldOn && title.trim() && priceNum > 0 && typeOk;

  return (
    <div className="mp-screen co-root">
      {/* floating language toggle */}
      <button className="co-lang" onClick={() => setLang(l => l === 'EN' ? 'DE' : 'EN')}>
        <span className="co-lang-flag">GB</span> {lang} <Ico.chevron />
      </button>

      <div className="co-scroll">
        {/* back */}
        <button className="co-back">
          <Ico.back /> Zurück zum Marktplatz
        </button>

        {/* intro card */}
        <div className="co-intro">
          <h1>Angebot erstellen</h1>
          <p>Wähle einen Angebotstyp, beschreibe ihn und leg einen Preis in WLD fest. Optional kannst du zusätzlich eine USDT/USDC-Auszahlungsadresse speichern.</p>
        </div>

        {/* wallets */}
        <WalletCard
          title="Deine Auszahlungs-Wallet (WLD)"
          connected={wldOn}
          onToggle={() => setWldOn(v => !v)}
          addr="2.84 WLD"
          hintConnect={<><b>Nicht verbunden</b> — Wallet wird für Auszahlung benötigt</>}
        />
        <WalletCard
          title="Deine Auszahlungs-Wallet (USDT/USDC)"
          connected={usdOn}
          onToggle={() => setUsdOn(v => !v)}
          addr="0x8f…a3d"
          hintConnect={<><b>Nicht verbunden</b> — optional für USDT/USDC-Auszahlungen</>}
        />

        {/* split banner */}
        <div className="co-banner">
          <span className="co-banner-ico"><Ico.info /></span>
          <div><b>80% für dich</b> — Bei jedem Verkauf bekommst du automatisch 80% des Preises direkt auf deine Wallet. 20% gehen als Plattform-Gebühr an uns.</div>
        </div>

        {/* 1. type */}
        <CoSection n="1" title="Was möchtest du anbieten?">
          <div className="co-types">
            {OFFER_TYPES.map(t => {
              const Icon = Ico[t.icon];
              return (
                <button key={t.id}
                  className={'co-type' + (type === t.id ? ' sel' : '')}
                  onClick={() => setType(t.id)}>
                  <span className="co-type-ico"><Icon /></span>
                  <span className="co-type-label">{t.label}</span>
                </button>
              );
            })}
          </div>

          {type === 'plan'    && <PlanPicker value={planId} onPick={setPlanId} />}
          {type === 'recipe'  && <RecipePicker value={recipe} onPick={setRecipe} />}
          {type === 'object'  && <ObjectFields data={fields} set={set} />}
          {type === 'digital' && <DigitalFields data={fields} set={set} />}
          {type === 'service' && <ServiceFields data={fields} set={set} />}
        </CoSection>

        {/* 2. describe */}
        <CoSection n="2" title="Angebot beschreiben">
          <div className="co-stack">
            <CoField placeholder="Titel für dein Angebot" value={title} onChange={e => setTitle(e.target.value)} />
            <textarea className="co-input co-area" placeholder="Kurze Beschreibung (optional)" rows="3" value={desc} onChange={e => setDesc(e.target.value)} />
          </div>
        </CoSection>

        {/* 3. price */}
        <CoSection n="3" title="Preis festlegen">
          <div className="co-price-row">
            <div className="co-price-box">
              <input className="co-price-input" inputMode="decimal" value={price} onChange={e => setPrice(e.target.value.replace(/[^0-9.]/g, ''))} />
            </div>
            <span className="co-price-cur">WLD</span>
          </div>
          <div className="co-split">
            Du bekommst: <b>{youGet} WLD</b> (80%) <span className="co-split-sep">—</span> Plattform: {platform} WLD (20%)
          </div>
        </CoSection>

        {/* CTA */}
        <button className={'co-cta' + (valid ? '' : ' disabled')} disabled={!valid}>
          <Ico.tag /> Jetzt verkaufen
        </button>
        {!wldOn && (
          <div className="co-cta-note">Verbinde zuerst deine WLD-Wallet, um zu verkaufen.</div>
        )}
      </div>
    </div>
  );
}

window.CreateOffer = CreateOffer;
