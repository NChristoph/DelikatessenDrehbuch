/* User Profile · Avocado-Stil
   Profil-Header + Action-Buttons + Tab-Bar (Likes · AI · Abos · Erstellt · Gekauft · Videos)
   inkl. ausgeklapptem Creator Dashboard.
*/

const { useState: pUseState } = React;

// ─── Sample data ──────────────────────────────────────────────
const P_USER = {
  name: 'Avocado',
  handle: '0x2da33d',
  hashShort: '0x2da3...4839',
  initials: 'AV',
  verified: true,
  stats: { following: 0, likes: 0 },
};

const P_LIKES = [
  null,
  'https://images.unsplash.com/photo-1604908176997-125f25cc6f3d?w=600&q=80',
  'https://images.unsplash.com/photo-1547496502-affa22d38842?w=600&q=80',
  'https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=600&q=80',
  'https://images.unsplash.com/photo-1565299624946-b28f40a0ae38?w=600&q=80',
  'https://images.unsplash.com/photo-1567620905732-2d1ec7ab7445?w=600&q=80',
];

const P_AI_VARIANTS = Array.from({ length: 6 }).map(() => ({
  img: 'https://images.unsplash.com/photo-1544025162-d76694265947?w=600&q=80',
  title: 'Schweinebra…',
  badge: 'Variante',
}));

const P_ABOS = [
  { name: 'Avocado', initials: 'AV' },
];

const P_ERSTELLT = [
  { name: 'Feed-Plan',       date: '25.04.2026' },
  { name: 'Feed-Plan neu 2', date: '23.02.2026' },
  { name: 'Test',            date: '19.02.2026' },
];

const P_GEKAUFT = [
  { name: 'Feed-Plan neu 2', date: '12.03.2026 18:56', price: '1 WLD', tx: '0xa76d…5ec3' },
  { name: 'Feed-Plan neu 2', date: '10.03.2026 23:47', price: '1 WLD', tx: '0x55cd…05a9' },
];

const P_SALES = [
  { name: 'Feed-Plan neu 2', date: '12.03.2026 18:56', amount: '+0,8 WLD' },
  { name: 'Feed-Plan neu 2', date: '10.03.2026 23:47', amount: '+0,8 WLD' },
  { name: 'Feed-Plan neu 2', date: '07.03.2026 15:03', amount: '+0,8 WLD' },
  { name: 'Feed-Plan neu 2', date: '07.03.2026 15:03', amount: '+0,8 WLD' },
  { name: 'Feed-Plan neu 2', date: '24.02.2026 22:03', amount: '+0,8 WLD' },
  { name: 'Feed-Plan neu 2', date: '24.02.2026 17:06', amount: '+0,8 WLD' },
];

const P_TOP_CLIPS = [
  { title: 'Schweinebraten mit dunkler Biersauce und Ofengemüse', views: 85, mins: '45,35', avg: '32,01s' },
  { title: 'Klassisches Curry',     views: 24, mins: '13,12', avg: '28,54s' },
  { title: 'Fisch 2',               views: 12, mins: '9,31',  avg: '21,80s' },
];

// ─── Icons ────────────────────────────────────────────────────
function PIcon({ name, size = 18, color = 'currentColor', fill = 'none', stroke = true }) {
  const props = { width: size, height: size, viewBox: '0 0 24 24', fill, stroke: stroke ? color : 'none', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' };
  if (name === 'back')     return <svg {...props}><path d="M15 18l-6-6 6-6"/></svg>;
  if (name === 'chev')     return <svg {...props}><polyline points="6 9 12 15 18 9"/></svg>;
  if (name === 'chevR')    return <svg {...props}><polyline points="9 6 15 12 9 18"/></svg>;
  if (name === 'check')    return <svg width={size} height={size} viewBox="0 0 24 24" fill={color} stroke="none"><circle cx="12" cy="12" r="10"/><path d="M9 12l2 2 4-4" stroke="white" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" fill="none"/></svg>;
  if (name === 'cog')      return <svg {...props}><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/></svg>;
  if (name === 'shop')     return <svg {...props}><path d="M3 9h18l-1 11H4L3 9z"/><path d="M8 9V6a4 4 0 0 1 8 0v3"/></svg>;
  if (name === 'bag')      return <svg {...props}><path d="M6 2l-2 4v15a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6l-2-4z"/><line x1="3" y1="6" x2="21" y2="6"/><path d="M16 10a4 4 0 0 1-8 0"/></svg>;
  if (name === 'flag')     return <svg {...props}><path d="M4 21V4M4 4h13l-2 4 2 4H4"/></svg>;
  if (name === 'gauge')    return <svg {...props}><path d="M12 14l4-4"/><circle cx="12" cy="13" r="9"/><path d="M3 13a9 9 0 0 1 18 0"/></svg>;
  if (name === 'heart')    return <svg {...props}><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>;
  if (name === 'heartF')   return <svg width={size} height={size} viewBox="0 0 24 24" fill={color} stroke="none"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>;
  if (name === 'sparkle')  return <svg {...props}><path d="M12 3l2 5 5 2-5 2-2 5-2-5-5-2 5-2 2-5z"/></svg>;
  if (name === 'people')   return <svg {...props}><circle cx="9" cy="8" r="3"/><circle cx="17" cy="9" r="2.5"/><path d="M3 20c0-3 2.7-5 6-5s6 2 6 5"/><path d="M14 14c2.5 0 6 1.5 6 4"/></svg>;
  if (name === 'cal')      return <svg {...props}><rect x="3" y="4" width="18" height="18" rx="3"/><path d="M3 9h18M8 2v4M16 2v4"/></svg>;
  if (name === 'play')     return <svg {...props}><circle cx="12" cy="12" r="10"/><polygon points="10 8 16 12 10 16 10 8" fill={color} stroke="none"/></svg>;
  if (name === 'trash')    return <svg {...props}><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/></svg>;
  if (name === 'pen')      return <svg {...props}><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>;
  if (name === 'eye-off')  return <svg {...props}><path d="M17.94 17.94A10 10 0 0 1 12 20c-7 0-11-8-11-8a18 18 0 0 1 5.06-5.94M9.9 4.24A10 10 0 0 1 12 4c7 0 11 8 11 8a18 18 0 0 1-2.16 3.19M14.12 14.12a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>;
  if (name === 'video')    return <svg {...props}><polygon points="23 7 16 12 23 17 23 7"/><rect x="1" y="5" width="15" height="14" rx="2"/></svg>;
  if (name === 'swap')     return <svg {...props}><polyline points="17 1 21 5 17 9"/><path d="M3 11V9a4 4 0 0 1 4-4h14"/><polyline points="7 23 3 19 7 15"/><path d="M21 13v2a4 4 0 0 1-4 4H3"/></svg>;
  if (name === 'extLink')  return <svg {...props}><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg>;
  if (name === 'cash')     return <svg {...props}><rect x="2" y="6" width="20" height="12" rx="2"/><circle cx="12" cy="12" r="3"/></svg>;
  if (name === 'view')     return <svg {...props}><rect x="2" y="4" width="20" height="16" rx="3"/><polygon points="10 9 16 12 10 15 10 9" fill={color} stroke="none"/></svg>;
  if (name === 'crowd')    return <svg {...props}><circle cx="9" cy="8" r="3"/><circle cx="17" cy="9" r="2.5"/><path d="M3 20c0-3 2.7-5 6-5s6 2 6 5"/></svg>;
  if (name === 'clock')    return <svg {...props}><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>;
  if (name === 'stopwatch')return <svg {...props}><circle cx="12" cy="13" r="8"/><line x1="9" y1="2" x2="15" y2="2"/><line x1="12" y1="9" x2="12" y2="13"/></svg>;
  if (name === 'trend')    return <svg {...props}><polyline points="22 7 13 16 8 11 2 17"/><polyline points="16 7 22 7 22 13"/></svg>;
  if (name === 'send')     return <svg {...props}><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg>;
  if (name === 'docPlus')  return <svg {...props}><path d="M9 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-9"/><path d="M14 3v5h5"/><path d="M14 3l5 5"/><line x1="9" y1="15" x2="15" y2="15"/><line x1="12" y1="12" x2="12" y2="18"/></svg>;
  if (name === 'shield')   return <svg {...props}><path d="M12 2l8 4v6c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V6l8-4z"/></svg>;
  if (name === 'arrowR')   return <svg {...props}><line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/></svg>;
  if (name === 'megaphone')return <svg {...props}><path d="M3 11l18-7v16L3 13v-2z"/><path d="M7 13v5a2 2 0 0 0 2 2h1v-6"/></svg>;
  return null;
}

// ─── Top bar ──────────────────────────────────────────────────
function PTopBar() {
  const [lang, setLang] = pUseState('EN');
  return (
    <div style={{
      flexShrink: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '12px 14px 8px',
    }}>
      <button style={{
        background: 'white', border: 'none', borderRadius: 999,
        padding: '8px 14px',
        fontSize: 13, fontWeight: 700, color: '#14391f',
        display: 'inline-flex', alignItems: 'center', gap: 6, cursor: 'pointer',
        boxShadow: '0 2px 8px rgba(20,57,31,0.06)', fontFamily: 'inherit',
      }}>
        <PIcon name="back" size={14} color="#14391f" />
        Zurück zum Feed
      </button>
      <button
        onClick={() => setLang(l => l === 'EN' ? 'DE' : 'EN')}
        style={{
          background: 'white', border: 'none', borderRadius: 999,
          padding: '6px 11px',
          fontSize: 12, fontWeight: 600, color: '#14391f',
          display: 'inline-flex', alignItems: 'center', gap: 6, cursor: 'pointer',
          boxShadow: '0 2px 8px rgba(20,57,31,0.06)', fontFamily: 'inherit',
        }}>
        <span style={{
          fontSize: 9, fontWeight: 700, color: '#6b7868',
          background: '#f1ebd9', padding: '2px 4px', borderRadius: 4,
        }}>GB</span>
        {lang}
        <PIcon name="chev" size={11} color="#14391f" />
      </button>
    </div>
  );
}

// ─── Profile card (header) ────────────────────────────────────
function PProfileCard() {
  return (
    <div style={{
      margin: '0 14px',
      background: 'linear-gradient(160deg, #eaf6ec 0%, #e2f2e5 100%)',
      borderRadius: 24,
      padding: '20px 18px 20px',
      border: '1px solid rgba(20,57,31,0.06)',
    }}>
      {/* Identity */}
      <div style={{ display: 'flex', gap: 14 }}>
        <div style={{
          width: 76, height: 76,
          borderRadius: 22,
          background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
          color: 'white',
          fontFamily: "'Fraunces', serif",
          fontSize: 30, fontWeight: 700,
          letterSpacing: '-0.02em',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          flexShrink: 0,
          boxShadow: '0 8px 22px rgba(20,57,31,0.18)',
        }}>{P_USER.initials}</div>
        <div style={{ flex: 1, minWidth: 0, paddingTop: 4 }}>
          <h1 style={{
            margin: '0 0 2px',
            fontFamily: "'Fraunces', serif",
            fontSize: 26, fontWeight: 700,
            letterSpacing: '-0.02em',
            color: '#14391f', lineHeight: 1.05,
          }}>{P_USER.name}</h1>
          <div style={{ fontSize: 13, color: '#6b7868', fontWeight: 500 }}>@{P_USER.handle}</div>
        </div>
      </div>

      {/* Stats */}
      <div style={{ display: 'flex', gap: 28, marginTop: 18, paddingLeft: 4 }}>
        {[
          { v: P_USER.stats.following, l: 'Following' },
          { v: P_USER.stats.likes,     l: 'Likes' },
        ].map(s => (
          <div key={s.l}>
            <div style={{
              fontFamily: "'Fraunces', serif",
              fontSize: 22, fontWeight: 700,
              color: '#14391f', lineHeight: 1,
              letterSpacing: '-0.02em',
            }}>{s.v}</div>
            <div style={{
              marginTop: 4,
              fontSize: 10, fontWeight: 700,
              letterSpacing: '0.14em', textTransform: 'uppercase',
              color: '#9aa295',
            }}>{s.l}</div>
          </div>
        ))}
      </div>

      {/* Action buttons */}
      <button style={{
        width: '100%', marginTop: 16,
        background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
        color: 'white', border: 'none', borderRadius: 16,
        padding: '13px',
        fontSize: 14, fontWeight: 700,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 8,
        cursor: 'pointer', fontFamily: 'inherit',
        boxShadow: '0 6px 20px rgba(20,57,31,0.24)',
      }}>
        <PIcon name="cog" size={15} color="white" />
        Einstellungen
      </button>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 8 }}>
        <PMiniBtn icon="send" label="Geteilte Inhalte" />
        <PMiniBtn icon="shop" label="Marketplace" />
      </div>
      <div style={{ marginTop: 8, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
        <PMiniBtn icon="bag"     label="My listings" />
        <PMiniBtn icon="docPlus" label={'Verkaufsplan\nerstellen'} multiline />
      </div>
    </div>
  );
}

function PMiniBtn({ icon, label, active, multiline, onClick }) {
  return (
    <button
      onClick={onClick}
      style={{
        background: active ? '#14391f' : '#faf6ec',
        color: active ? 'white' : '#14391f',
        border: '1px solid rgba(20,57,31,0.08)',
        borderRadius: 16,
        padding: multiline ? '10px 12px' : '12px',
        fontSize: 13, fontWeight: 700,
        cursor: 'pointer',
        fontFamily: 'inherit',
        display: 'inline-flex', alignItems: 'center',
        justifyContent: multiline ? 'flex-start' : 'center',
        gap: 8,
        whiteSpace: 'pre-line',
        textAlign: 'left',
        lineHeight: 1.2,
        boxShadow: '0 2px 8px rgba(20,57,31,0.04)',
      }}>
      <PIcon name={icon} size={15} color={active ? 'white' : '#14391f'} />
      {label}
    </button>
  );
}

// Admin access banner + Ad Manager pill
function PAdminBanner() {
  const [pin, setPin] = pUseState('');
  return (
    <div style={{ margin: '10px 14px 0', display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{
        background: 'rgba(20,20,20,0.045)',
        borderRadius: 22,
        padding: '14px 16px 16px',
      }}>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 7,
          color: '#14391f', fontSize: 12, fontWeight: 700,
          letterSpacing: '0.08em', textTransform: 'uppercase',
        }}>
          <PIcon name="shield" size={14} color="#14391f" />
          Admin-Zugang freischalten
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10 }}>
          <input
            value={pin}
            onChange={e => setPin(e.target.value)}
            placeholder="PIN eingeben..."
            style={{
              flex: 1,
              background: 'white',
              border: 'none', borderRadius: 16,
              padding: '12px 14px',
              fontSize: 14, fontWeight: 600, color: '#14391f',
              fontFamily: 'inherit', outline: 'none',
              boxShadow: '0 2px 8px rgba(20,57,31,0.06)',
            }}
          />
          <button style={{
            width: 40, height: 40, borderRadius: '50%', flexShrink: 0,
            background: '#14391f', border: 'none', cursor: 'pointer',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            boxShadow: '0 2px 8px rgba(20,57,31,0.10)',
          }}>
            <PIcon name="arrowR" size={17} color="white" />
          </button>
        </div>
      </div>
      <button style={{
        alignSelf: 'flex-start',
        background: 'white',
        color: '#14391f', border: 'none', borderRadius: 999,
        padding: '11px 20px',
        fontSize: 13.5, fontWeight: 700,
        display: 'inline-flex', alignItems: 'center', gap: 8, cursor: 'pointer',
        fontFamily: 'inherit',
        boxShadow: '0 2px 8px rgba(20,57,31,0.06)',
      }}>
        <PIcon name="megaphone" size={15} color="#14391f" />
        Ad Manager
      </button>
    </div>
  );
}

// Creator dashboard toggle (full-width pill)
function PDashboardToggle({ open, onClick }) {
  return (
    <button
      onClick={onClick}
      style={{
        margin: '8px 14px 0',
        width: 'calc(100% - 28px)',
        background: open ? '#14391f' : '#faf6ec',
        color: open ? 'white' : '#14391f',
        border: open ? 'none' : '1px solid rgba(20,57,31,0.08)',
        borderRadius: 16,
        padding: '13px',
        fontSize: 14, fontWeight: 700,
        cursor: 'pointer', fontFamily: 'inherit',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 8,
        boxShadow: open ? '0 6px 20px rgba(20,57,31,0.24)' : '0 2px 8px rgba(20,57,31,0.04)',
      }}>
      <PIcon name="gauge" size={15} color={open ? 'white' : '#14391f'} />
      Creator Dashboard
      <span style={{ marginLeft: 4, opacity: 0.7, fontSize: 11 }}>
        {open ? '▴' : '▾'}
      </span>
    </button>
  );
}

// ─── Tab bar (segmented w/ icons) ─────────────────────────────
function PTabBar({ active, onChange }) {
  const tabs = [
    { id: 'likes',     label: 'Likes',    icon: 'heartF' },
    { id: 'ai',        label: 'AI',       icon: 'sparkle' },
    { id: 'abos',      label: 'Creators', icon: 'people' },
    { id: 'erstellt',  label: 'Created',  icon: 'cal' },
    { id: 'gekauft',   label: 'Bought',   icon: 'bag' },
  ];
  return (
    <div style={{ padding: '14px 14px 8px' }}>
      <div style={{
        display: 'flex',
        background: 'rgba(20,20,20,0.045)',
        borderRadius: 22,
        padding: 4,
        gap: 2,
      }}>
        {tabs.map(t => {
          const isActive = active === t.id;
          return (
            <button
              key={t.id}
              onClick={() => onChange(t.id)}
              style={{
                flex: 1,
                padding: '8px 4px 6px',
                borderRadius: 16,
                background: isActive ? 'white' : 'transparent',
                border: 'none', cursor: 'pointer',
                fontFamily: 'inherit',
                color: isActive ? '#14391f' : '#9aa295',
                display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 4,
                boxShadow: isActive ? '0 4px 12px rgba(20,57,31,0.10)' : 'none',
                transition: 'all .2s',
              }}>
              <PIcon name={t.icon} size={18} color={isActive ? '#14391f' : '#9aa295'} />
              <span style={{
                fontSize: 9.5, fontWeight: 700,
                letterSpacing: '0.08em', textTransform: 'uppercase',
              }}>{t.label}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

// ─── Tab bodies ───────────────────────────────────────────────
function PMediaGrid({ items }) {
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: '1fr 1fr 1fr',
      gap: 8, padding: '0 14px',
    }}>
      {items.map((src, i) => (
        <div key={i} style={{
          aspectRatio: '9/14',
          borderRadius: 14,
          background: src ? `url(${src}) center/cover` : '#191917',
          position: 'relative',
          overflow: 'hidden',
          boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
        }}>
          <span style={{
            position: 'absolute', bottom: 8, left: 8,
            width: 22, height: 22, borderRadius: '50%',
            background: 'rgba(0,0,0,0.55)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            color: 'white', fontSize: 11,
          }}>▶</span>
        </div>
      ))}
    </div>
  );
}

function PLikesTab() {
  return <PMediaGrid items={[...P_LIKES, ...P_LIKES.slice(0, 3)]} />;
}

function PAITab() {
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: '1fr 1fr 1fr',
      gap: 8, padding: '0 14px',
    }}>
      {P_AI_VARIANTS.map((v, i) => (
        <div key={i} style={{
          position: 'relative',
          aspectRatio: '9/14',
          borderRadius: 14, overflow: 'hidden',
          background: `url(${v.img}) center/cover`,
          boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
        }}>
          <div style={{
            position: 'absolute', top: 8, left: 8,
            display: 'inline-flex', alignItems: 'center', gap: 4,
            padding: '4px 8px 4px 6px',
            background: 'rgba(255,255,255,0.95)',
            borderRadius: 999,
            fontSize: 10, fontWeight: 700, color: '#14391f',
          }}>
            <PIcon name="swap" size={10} color="#14391f" />
            Angepasst
          </div>
          <div style={{
            position: 'absolute', left: 0, right: 0, bottom: 0,
            padding: '24px 8px 8px',
            background: 'linear-gradient(180deg, rgba(0,0,0,0) 0%, rgba(0,0,0,0.7) 100%)',
            color: 'white',
          }}>
            <div style={{ fontFamily: "'Fraunces',serif", fontSize: 13, fontWeight: 600, lineHeight: 1.1 }}>{v.title}</div>
            <span style={{
              display: 'inline-block', marginTop: 6,
              padding: '3px 8px',
              background: 'rgba(255,255,255,0.18)', backdropFilter: 'blur(6px)',
              borderRadius: 999,
              fontSize: 9.5, fontWeight: 700,
              letterSpacing: '0.08em', textTransform: 'uppercase',
            }}>{v.badge}</span>
          </div>
        </div>
      ))}
    </div>
  );
}

function PAbosTab() {
  return (
    <div style={{ padding: '0 14px', display: 'flex', flexDirection: 'column', gap: 8 }}>
      {P_ABOS.map(a => (
        <div key={a.name} style={{
          background: 'white',
          borderRadius: 18,
          padding: '12px 14px',
          display: 'flex', alignItems: 'center', gap: 12,
          boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
          cursor: 'pointer',
        }}>
          <div style={{
            width: 38, height: 38, borderRadius: '50%',
            background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
            color: 'white', fontSize: 12, fontWeight: 700,
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            fontFamily: "'Fraunces',serif",
          }}>{a.initials}</div>
          <div style={{ flex: 1, fontSize: 14, fontWeight: 700, color: '#14391f' }}>{a.name}</div>
          <PIcon name="chevR" size={16} color="#9aa295" />
        </div>
      ))}
    </div>
  );
}

function PErstelltTab() {
  return (
    <div style={{ padding: '0 14px', display: 'flex', flexDirection: 'column', gap: 8 }}>
      {P_ERSTELLT.map((e, i) => (
        <div key={i} style={{
          background: 'white',
          borderRadius: 18,
          padding: '12px 12px 12px 16px',
          display: 'flex', alignItems: 'center', gap: 10,
          boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
        }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 14, fontWeight: 700, color: '#14391f' }}>{e.name}</div>
            <div style={{ fontSize: 11, color: '#9aa295', marginTop: 2 }}>{e.date}</div>
          </div>
          <button style={{
            background: '#14391f', color: 'white', border: 'none',
            borderRadius: 999, padding: '6px 14px',
            fontSize: 11, fontWeight: 700, letterSpacing: '0.08em',
            cursor: 'pointer', fontFamily: 'inherit',
          }}>OPEN</button>
          <button style={{
            width: 32, height: 32, borderRadius: '50%',
            background: 'rgba(255,120,73,0.08)',
            border: '1px solid rgba(255,120,73,0.3)',
            cursor: 'pointer',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            color: '#ff7849',
          }}>
            <PIcon name="trash" size={13} color="#ff7849" />
          </button>
        </div>
      ))}
    </div>
  );
}

function PGekauftTab() {
  return (
    <div style={{ padding: '0 14px', display: 'flex', flexDirection: 'column', gap: 8 }}>
      {P_GEKAUFT.map((g, i) => (
        <div key={i} style={{
          background: 'white',
          borderRadius: 18,
          padding: '14px 16px',
          boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
        }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 10 }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 14, fontWeight: 700, color: '#14391f' }}>{g.name}</div>
              <div style={{ fontSize: 11, color: '#9aa295', marginTop: 2 }}>{g.date}</div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <div style={{ fontFamily: "'Fraunces',serif", fontSize: 16, fontWeight: 700, color: '#14391f' }}>{g.price}</div>
              <div style={{ display: 'inline-flex', alignItems: 'center', gap: 4, fontSize: 10, color: '#9aa295', marginTop: 2 }}>
                <PIcon name="extLink" size={10} color="#9aa295" />
                {g.tx}
              </div>
            </div>
          </div>
          <button style={{
            marginTop: 10, width: '100%',
            background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
            color: 'white', border: 'none', borderRadius: 14,
            padding: '10px',
            fontSize: 13, fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
          }}>
            <PIcon name="cal" size={13} color="white" />
            Plan öffnen
          </button>
        </div>
      ))}
    </div>
  );
}

function PVideosTab() {
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: '1fr 1fr 1fr',
      gap: 8, padding: '0 14px',
    }}>
      {[
        'https://images.unsplash.com/photo-1565958011703-44f9829ba187?w=600&q=80',
        null, null,
      ].map((src, i) => (
        <div key={i} style={{
          aspectRatio: '9/14',
          borderRadius: 14,
          position: 'relative',
          overflow: 'hidden',
          background: src ? `url(${src}) center/cover` : '#191917',
          boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
        }}>
          {/* Action col */}
          <div style={{
            position: 'absolute', top: 8, right: 8,
            display: 'flex', flexDirection: 'column', gap: 6,
          }}>
            <span style={{
              width: 26, height: 26, borderRadius: '50%',
              background: '#14391f', color: 'white',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            }}>
              <PIcon name="pen" size={12} color="white" />
            </span>
            <span style={{
              width: 26, height: 26, borderRadius: '50%',
              background: '#d99a3c', color: 'white',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            }}>
              <PIcon name="eye-off" size={12} color="white" />
            </span>
            <span style={{
              width: 26, height: 26, borderRadius: '50%',
              background: '#c44e4e', color: 'white',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            }}>
              <PIcon name="trash" size={12} color="white" />
            </span>
          </div>
          {/* Camera badge */}
          <span style={{
            position: 'absolute', bottom: 8, left: 8,
            width: 22, height: 22, borderRadius: '50%',
            background: 'rgba(0,0,0,0.55)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            color: 'white',
          }}>
            <PIcon name="video" size={12} color="white" />
          </span>
        </div>
      ))}
    </div>
  );
}

// ─── Creator Dashboard ────────────────────────────────────────
function PStatTile({ icon, label, value, unit, accent = '#5fa052' }) {
  return (
    <div style={{
      background: 'white',
      borderRadius: 18,
      padding: '14px',
      boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
    }}>
      <div style={{
        width: 34, height: 34, borderRadius: 12,
        background: `${accent}22`,
        color: accent,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <PIcon name={icon} size={16} color={accent} />
      </div>
      <div style={{
        marginTop: 12,
        fontSize: 11, fontWeight: 700,
        color: '#6b7868',
        letterSpacing: '0.04em',
      }}>{label}</div>
      <div style={{
        marginTop: 4,
        display: 'flex', alignItems: 'baseline', gap: 5,
      }}>
        <span style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 22, fontWeight: 700, color: '#14391f',
          letterSpacing: '-0.02em', lineHeight: 1,
        }}>{value}</span>
        {unit && <span style={{ fontSize: 11, fontWeight: 600, color: '#9aa295' }}>{unit}</span>}
      </div>
    </div>
  );
}

function PCreatorDashboard() {
  return (
    <div style={{ padding: '8px 14px 24px', display: 'flex', flexDirection: 'column', gap: 12 }}>
      {/* Header card */}
      <div style={{
        background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
        color: 'white',
        borderRadius: 22,
        padding: '20px 18px',
        boxShadow: '0 12px 30px rgba(20,57,31,0.18)',
        position: 'relative', overflow: 'hidden',
      }}>
        <div style={{
          fontSize: 10, fontWeight: 700,
          color: 'rgba(168,211,157,0.85)',
          letterSpacing: '0.18em', textTransform: 'uppercase',
        }}>Premium Creator Area</div>
        <h2 style={{
          margin: '4px 0 12px',
          fontFamily: "'Fraunces', serif",
          fontSize: 26, fontWeight: 700, letterSpacing: '-0.02em',
        }}>Creator Dashboard</h2>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12 }}>
          <span style={{ opacity: 0.7 }}>User Hash</span>
          <code style={{
            fontFamily: 'ui-monospace, SFMono-Regular, monospace',
            background: 'rgba(255,255,255,0.1)',
            padding: '3px 8px', borderRadius: 6,
            fontSize: 11,
          }}>{P_USER.hashShort}</code>
          <button style={{
            marginLeft: 'auto',
            background: 'rgba(255,255,255,0.12)', color: 'white',
            border: 'none', borderRadius: 999,
            padding: '5px 12px',
            fontSize: 11, fontWeight: 600, cursor: 'pointer',
            fontFamily: 'inherit',
          }}>Kopieren</button>
        </div>
      </div>

      {/* Available + payout */}
      <div style={{
        background: 'white',
        borderRadius: 22,
        padding: '18px',
        boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
      }}>
        <div style={{
          fontSize: 11, fontWeight: 700,
          color: '#9aa295',
          letterSpacing: '0.12em', textTransform: 'uppercase',
        }}>Verfügbar</div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginTop: 4 }}>
          <span style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 44, fontWeight: 700, color: '#14391f',
            letterSpacing: '-0.03em', lineHeight: 1,
          }}>4,8</span>
          <span style={{ fontSize: 14, fontWeight: 700, color: '#5fa052' }}>WLD</span>
        </div>
        <button style={{
          marginTop: 14,
          background: 'linear-gradient(135deg, #5fa052, #3f7536)',
          color: 'white', border: 'none', borderRadius: 14,
          padding: '11px 16px',
          fontSize: 13, fontWeight: 700,
          cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', gap: 7,
          boxShadow: '0 6px 18px rgba(95,160,82,0.32)',
        }}>
          <PIcon name="cash" size={14} color="white" />
          Geld anfordern
        </button>
      </div>

      {/* 2x2 Stats */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
        <PStatTile icon="bag"   label="Verkäufe"        value="6"   accent="#5fa052" />
        <PStatTile icon="cash"  label="Verfügbar"       value="4,8" unit="WLD" accent="#5fa052" />
        <PStatTile icon="clock" label="Ausstehend"      value="0"   unit="USDC" accent="#d99a3c" />
        <PStatTile icon="check" label="Bereits erhalten" value="4,8" unit="WLD" accent="#3f7536" />
      </div>

      {/* Watch analytics */}
      <div style={{
        background: 'white',
        borderRadius: 22,
        padding: 16,
        boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
      }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 10, marginBottom: 12 }}>
          <h3 style={{
            margin: 0,
            fontFamily: "'Fraunces',serif",
            fontSize: 18, fontWeight: 700,
            color: '#14391f', letterSpacing: '-0.01em',
          }}>Watch Analytics</h3>
          <span style={{ fontSize: 11, color: '#9aa295', textAlign: 'right', maxWidth: 160 }}>Nur qualifizierte Views ab 8 Sekunden</span>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          <PStatTile icon="view"      label="Qualifizierte Views" value="156"    accent="#5fa052" />
          <PStatTile icon="crowd"     label="Echte Viewer"        value="2"      accent="#5fa052" />
          <PStatTile icon="clock"     label="Watch-Minuten"       value="114,05" accent="#5fa052" />
          <PStatTile icon="stopwatch" label="Ø Watch-Sekunden"    value="43,87"  accent="#5fa052" />
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 8 }}>
          <div style={{ background: '#faf6ec', borderRadius: 16, padding: 14 }}>
            <div style={{ fontSize: 11, fontWeight: 600, color: '#6b7868' }}>Von dir angesehen</div>
            <div style={{ fontFamily: "'Fraunces',serif", fontSize: 22, fontWeight: 700, color: '#14391f', marginTop: 4 }}>155 <span style={{ fontSize: 12, color: '#9aa295' }}>Clips</span></div>
          </div>
          <div style={{ background: '#faf6ec', borderRadius: 16, padding: 14 }}>
            <div style={{ fontSize: 11, fontWeight: 600, color: '#6b7868' }}>Deine Watch-Time</div>
            <div style={{ fontFamily: "'Fraunces',serif", fontSize: 22, fontWeight: 700, color: '#14391f', marginTop: 4 }}>113,88 <span style={{ fontSize: 12, color: '#9aa295' }}>Min</span></div>
          </div>
        </div>
      </div>

      {/* Top Clips */}
      <div style={{
        background: 'white',
        borderRadius: 22,
        padding: 16,
        boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
          <h3 style={{ margin: 0, fontFamily: "'Fraunces',serif", fontSize: 18, fontWeight: 700, color: '#14391f' }}>Top Clips</h3>
          <PIcon name="trend" size={16} color="#5fa052" />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {P_TOP_CLIPS.map((c, i) => (
            <div key={i} style={{
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '10px 4px',
              borderTop: i === 0 ? 'none' : '1px solid rgba(20,57,31,0.06)',
            }}>
              <div style={{
                width: 32, height: 32, borderRadius: 10,
                background: 'rgba(95,160,82,0.14)', color: '#5fa052',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                fontFamily: "'Fraunces',serif", fontSize: 13, fontWeight: 700,
              }}>{i + 1}</div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: '#14391f', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{c.title}</div>
                <div style={{ fontSize: 11, color: '#9aa295', marginTop: 2 }}>{c.views} Views · {c.mins} Min</div>
              </div>
              <div style={{ fontSize: 11, color: '#5fa052', fontWeight: 700 }}>Ø {c.avg}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Sales Activity */}
      <div style={{
        background: 'white',
        borderRadius: 22,
        padding: 16,
        boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
          <h3 style={{ margin: 0, fontFamily: "'Fraunces',serif", fontSize: 18, fontWeight: 700, color: '#14391f' }}>Sales Activity</h3>
          <span style={{ fontSize: 11, color: '#9aa295' }}>{P_SALES.length} Einträge</span>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {P_SALES.map((s, i) => (
            <div key={i} style={{
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '10px 12px',
              background: '#faf6ec',
              borderRadius: 14,
            }}>
              <span style={{
                width: 30, height: 30, borderRadius: '50%',
                background: 'rgba(95,160,82,0.18)',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              }}>
                <PIcon name="check" size={18} color="#5fa052" />
              </span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: '#14391f' }}>{s.name}</div>
                <div style={{ fontSize: 11, color: '#9aa295', marginTop: 2 }}>{s.date}</div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ fontFamily: "'Fraunces',serif", fontSize: 14, fontWeight: 700, color: '#5fa052' }}>{s.amount}</div>
                <button style={{
                  background: 'transparent', border: 'none',
                  fontSize: 10, color: '#9aa295', cursor: 'pointer',
                  fontFamily: 'inherit', padding: 0, marginTop: 2,
                }}>Details ▾</button>
              </div>
            </div>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
          <button style={{
            flex: 1,
            background: 'linear-gradient(135deg, #5fa052, #3f7536)',
            color: 'white', border: 'none', borderRadius: 14,
            padding: '11px',
            fontSize: 13, fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
          }}>
            <PIcon name="cash" size={13} color="white" />
            Geld anfordern
          </button>
          <button style={{
            background: 'white',
            color: '#14391f',
            border: '1px solid rgba(20,57,31,0.12)',
            borderRadius: 14,
            padding: '11px 16px',
            fontSize: 13, fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
          }}>
            <PIcon name="shop" size={13} color="#14391f" />
            Listings
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── Screen ───────────────────────────────────────────────────
function PScreen({ initialTab = 'likes', dashboardOpen = false }) {
  const [tab, setTab] = pUseState(initialTab);
  const [open, setOpen] = pUseState(dashboardOpen);

  return (
    <div className="mp-screen" style={{ background: 'white' }}>
      <div className="mp-feed" style={{ flex: 1, overflowY: 'auto', paddingBottom: 24 }}>
        <PTopBar />
        <PProfileCard />
        <PAdminBanner />
        <PDashboardToggle open={open} onClick={() => setOpen(!open)} />
        {open && <PCreatorDashboard />}
        <PTabBar active={tab} onChange={setTab} />
        {tab === 'likes'    && <PLikesTab />}
        {tab === 'ai'       && <PAITab />}
        {tab === 'abos'     && <PAbosTab />}
        {tab === 'erstellt' && <PErstelltTab />}
        {tab === 'gekauft'  && <PGekauftTab />}
      </div>
    </div>
  );
}

window.PScreen = PScreen;
