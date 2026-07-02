/* Create Offer v2 — "Angebot teilen" im Social-Media-Upload-Stil
   - Bild-zuerst: große Foto-/Video-Upload-Zone oben
   - Angebotstyp als horizontale Pills
   - Caption-Stil-Beschreibung
   - Live-Vorschau, wie der Post im Feed aussieht
   - "Teilen"-Topbar statt Formular-CTA
*/

const { useState } = React;

/* ---------- Icons ---------- */
const Ic = {
  close: (p) => <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" {...p}><path d="M18 6L6 18M6 6l12 12"/></svg>,
  camera: (p) => <svg width="30" height="30" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M3 8.5A2.5 2.5 0 015.5 6h1.2a2 2 0 001.7-1l.5-.8a2 2 0 011.7-1h2.8a2 2 0 011.7 1l.5.8a2 2 0 001.7 1h1.2A2.5 2.5 0 0121 8.5V17a2.5 2.5 0 01-2.5 2.5h-13A2.5 2.5 0 013 17z"/><circle cx="12" cy="12.5" r="3.2"/></svg>,
  plus: (p) => <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" {...p}><path d="M12 5v14M5 12h14"/></svg>,
  x: (p) => <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" {...p}><path d="M18 6L6 18M6 6l12 12"/></svg>,
  wallet: (p) => <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M3 7a2 2 0 012-2h12a2 2 0 012 2v1"/><path d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2v-6a2 2 0 00-2-2H5"/><circle cx="16" cy="13" r="1.2" fill="currentColor" stroke="none"/></svg>,
  chevron: (p) => <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M9 6l6 6-6 6"/></svg>,
  chevDown: (p) => <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M6 9l6 6 6-6"/></svg>,
  check: (p) => <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M20 6L9 17l-5-5"/></svg>,
  // type icons (small)
  calendar: (p) => <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" {...p}><rect x="3" y="4.5" width="18" height="16" rx="2.5"/><path d="M3 9h18M8 2.5v4M16 2.5v4"/></svg>,
  doc: (p) => <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" {...p}><rect x="4" y="3" width="16" height="18" rx="2.5"/><path d="M8 8h8M8 12h8M8 16h5"/></svg>,
  cube: (p) => <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M21 7.5l-9-5-9 5 9 5 9-5z"/><path d="M3 7.5v9l9 5 9-5v-9M12 12.5v9"/></svg>,
  download: (p) => <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" {...p}><path d="M19 16.5A4 4 0 0017.5 9h-1.3A6 6 0 104 14.5"/><path d="M12 11v8M8.5 15.5L12 19l3.5-3.5"/></svg>,
  service: (p) => <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" {...p}><rect x="3" y="4" width="18" height="16" rx="2.5"/><circle cx="12" cy="10" r="2.4"/><path d="M8 16.5a4 4 0 018 0"/></svg>,
  imageGlyph: (p) => <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...p}><rect x="3" y="4" width="18" height="16" rx="2.5"/><circle cx="8.5" cy="9.5" r="1.6"/><path d="M4 17l5-4 4 3 3-2.5 4 3.5"/></svg>,
};

const TYPES = [
  { id: 'plan',    label: 'Essensplan',    icon: 'calendar' },
  { id: 'recipe',  label: 'Rezept',        icon: 'doc' },
  { id: 'object',  label: 'Objekt',        icon: 'cube' },
  { id: 'digital', label: 'Digital',       icon: 'download' },
  { id: 'service', label: 'Service',       icon: 'service' },
];

const PLANS = [
  { id: 'p1', name: 'Feed-Plan', created: '17.05.2026' },
  { id: 'p2', name: 'Teste das', created: '22.05.2026' },
];
const RECIPES = ['Avocado Toast Deluxe', 'Protein Power Bowl', 'Green Detox Smoothie', 'Overnight Oats'];

/* sample "photos" — tinted placeholder tiles */
const PHOTO_TINTS = [
  'linear-gradient(135deg,#3f7a36,#1f4329)',
  'linear-gradient(135deg,#e9c46a,#d99441)',
  'linear-gradient(135deg,#8bbf7d,#5fa052)',
  'linear-gradient(135deg,#c98b6b,#9a5a3d)',
];

/* ---------- Per-type detail fields (social-styled) ---------- */
function DetailCard({ label, hint, children }) {
  return (
    <div className="c2-detail">
      <div className="c2-detail-label">{label}</div>
      {children}
      {hint && <div className="c2-detail-hint">{hint}</div>}
    </div>
  );
}

function TypeFields({ type, planId, setPlanId, recipe, setRecipe, fields, set }) {
  if (type === 'plan') return (
    <DetailCard label="Welchen Plan möchtest du anbieten?">
      <div className="c2-plans">
        {PLANS.map(p => (
          <button key={p.id} className={'c2-plan' + (planId === p.id ? ' sel' : '')} onClick={() => setPlanId(p.id)}>
            <span className="c2-plan-name">{p.name}</span>
            <span className="c2-plan-meta">Erstellt: {p.created}</span>
            {planId === p.id && <span className="c2-plan-check"><Ic.check /></span>}
          </button>
        ))}
      </div>
    </DetailCard>
  );
  if (type === 'recipe') return (
    <DetailCard label="Welches Rezept möchtest du anbieten?">
      <div className="c2-select-wrap">
        <select className="c2-select" value={recipe} onChange={e => setRecipe(e.target.value)}>
          <option value="">– Rezept wählen –</option>
          {RECIPES.map(r => <option key={r} value={r}>{r}</option>)}
        </select>
        <span className="c2-select-chev"><Ic.chevDown /></span>
      </div>
    </DetailCard>
  );
  if (type === 'object') return (
    <DetailCard label="Produktdetails" hint="Bei Versand fragen wir die Lieferadresse beim Kauf ab.">
      <input className="c2-inp" placeholder="Bestand (leer = unbegrenzt)" value={fields.stock || ''} onChange={e => set('stock', e.target.value)} />
      <button className="c2-check" onClick={() => set('shipping', !fields.shipping)}>
        <span className={'c2-box' + (fields.shipping ? ' on' : '')}>{fields.shipping && <Ic.check />}</span>
        <span>Versand nötig</span>
      </button>
    </DetailCard>
  );
  if (type === 'digital') return (
    <DetailCard label="Digitales Produkt" hint="Der Download-Link wird erst nach dem Kauf freigeschaltet.">
      <input className="c2-inp" placeholder="Download-/Datei-URL (https://…)" value={fields.fileUrl || ''} onChange={e => set('fileUrl', e.target.value)} />
    </DetailCard>
  );
  if (type === 'service') return (
    <DetailCard label="Dienstleistung" hint="Nach dem Kauf wirst du benachrichtigt, um Kontakt aufzunehmen.">
      <input className="c2-inp" placeholder="Verfügbarkeit / Ort (optional)" value={fields.avail || ''} onChange={e => set('avail', e.target.value)} />
    </DetailCard>
  );
  return null;
}

/* ---------- Live preview card (feed look) ---------- */
function PreviewCard({ photo, title, price, type }) {
  const typeLabel = (TYPES.find(t => t.id === type) || {}).label || 'Angebot';
  return (
    <div className="c2-preview">
      <div className="c2-preview-media" style={{ background: photo || '#e8e1d0' }}>
        {!photo && <span className="c2-preview-empty"><Ic.imageGlyph /></span>}
        <span className="c2-preview-type">{typeLabel}</span>
      </div>
      <div className="c2-preview-body">
        <div className="c2-preview-seller">
          <span className="c2-avatar">M</span> maxmustermann
        </div>
        <div className="c2-preview-title">{title || 'Dein Angebots­titel'}</div>
        <div className="c2-preview-foot">
          <span className="c2-preview-price">{price ? `${price} WLD` : '— WLD'}</span>
          <span className="c2-preview-buy">Kaufen</span>
        </div>
      </div>
    </div>
  );
}

/* ---------- Main ---------- */
function CreateOfferSocial() {
  const [photos, setPhotos] = useState([]);
  const [type, setType] = useState('object');
  const [title, setTitle] = useState('');
  const [caption, setCaption] = useState('');
  const [price, setPrice] = useState('');
  const [wldOn, setWldOn] = useState(false);
  const [planId, setPlanId] = useState('p1');
  const [recipe, setRecipe] = useState('');
  const [fields, setFields] = useState({});
  const set = (k, v) => setFields(f => ({ ...f, [k]: v }));

  const addPhoto = () => setPhotos(p => p.length >= 4 ? p : [...p, PHOTO_TINTS[p.length % PHOTO_TINTS.length]]);
  const removePhoto = (i) => setPhotos(p => p.filter((_, idx) => idx !== i));

  const priceNum = parseFloat(price) || 0;
  const youGet = (priceNum * 0.8).toFixed(2);
  const typeOk = type === 'plan' ? !!planId : type === 'recipe' ? !!recipe : true;
  const valid = wldOn && title.trim() && priceNum > 0 && photos.length > 0 && typeOk;

  return (
    <div className="mp-screen c2-root">
      {/* top bar */}
      <div className="c2-bar">
        <button className="c2-bar-icon"><Ic.close /></button>
        <span className="c2-bar-title">Neues Angebot</span>
        <button className={'c2-share' + (valid ? '' : ' off')} disabled={!valid}>Teilen</button>
      </div>

      <div className="c2-scroll">
        {/* media upload */}
        {photos.length === 0 ? (
          <button className="c2-drop" onClick={addPhoto}>
            <span className="c2-drop-ico"><Ic.camera /></span>
            <span className="c2-drop-title">Fotos oder Video hinzufügen</span>
            <span className="c2-drop-sub">Zeig dein Angebot von seiner besten Seite</span>
          </button>
        ) : (
          <div className="c2-media-grid">
            {photos.map((g, i) => (
              <div className="c2-thumb" key={i} style={{ background: g }}>
                {i === 0 && <span className="c2-cover">Cover</span>}
                <button className="c2-thumb-x" onClick={() => removePhoto(i)}><Ic.x /></button>
              </div>
            ))}
            {photos.length < 4 && (
              <button className="c2-thumb c2-add" onClick={addPhoto}><Ic.plus /></button>
            )}
          </div>
        )}

        {/* type menu (grid) */}
        <div className="c2-typewrap">
          <div className="c2-type-head">Was möchtest du anbieten?</div>
          <div className="c2-types">
            {TYPES.map(t => {
              const I = Ic[t.icon];
              return (
                <button key={t.id} className={'c2-type' + (type === t.id ? ' on' : '')} onClick={() => setType(t.id)}>
                  <span className="c2-type-ico"><I /></span>
                  <span className="c2-type-label">{t.label}</span>
                </button>
              );
            })}
          </div>
        </div>

        {/* per-type detail fields */}
        <TypeFields type={type} planId={planId} setPlanId={setPlanId} recipe={recipe} setRecipe={setRecipe} fields={fields} set={set} />

        {/* title + caption */}
        <div className="c2-field">
          <input className="c2-title-input" placeholder="Titel deines Angebots" value={title} onChange={e => setTitle(e.target.value)} />
          <div className="c2-divider" />
          <textarea className="c2-caption" rows="3" placeholder="Schreib eine Beschreibung… Was macht dein Angebot besonders?" value={caption} onChange={e => setCaption(e.target.value)} />
        </div>

        {/* price */}
        <div className="c2-price">
          <span className="c2-price-label">Preis</span>
          <div className="c2-price-right">
            <input className="c2-price-input" inputMode="decimal" placeholder="0" value={price} onChange={e => setPrice(e.target.value.replace(/[^0-9.]/g, ''))} />
            <span className="c2-price-cur">WLD</span>
          </div>
        </div>
        {priceNum > 0 && (
          <div className="c2-split">Du bekommst <b>{youGet} WLD</b> · 20% Plattform-Gebühr</div>
        )}

        {/* wallet strip */}
        <button className={'c2-wallet' + (wldOn ? ' on' : '')} onClick={() => setWldOn(v => !v)}>
          <span className="c2-wallet-ico"><Ic.wallet /></span>
          <span className="c2-wallet-text">
            <span className="c2-wallet-title">Auszahlungs-Wallet (WLD)</span>
            <span className="c2-wallet-sub">{wldOn ? 'Verbunden · 2.84 WLD' : 'Tippe zum Verbinden'}</span>
          </span>
          <span className="c2-wallet-state">{wldOn ? 'Verbunden' : 'Verbinden'}</span>
        </button>

        {/* live preview */}
        <div className="c2-preview-head">
          <span>Vorschau</span>
          <span className="c2-preview-hint">So sieht dein Angebot im Feed aus</span>
        </div>
        <PreviewCard photo={photos[0]} title={title} price={price} type={type} />

        <div className="c2-spacer" />
      </div>
    </div>
  );
}

window.CreateOfferSocial = CreateOfferSocial;
