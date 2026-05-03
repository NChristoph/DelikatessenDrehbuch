/* Create Posting — Redesign im Marketplace-Stil
   3 Varianten:
     A) Scroll-Stack (Light, alles auf einer Seite, Sticky Tabs als Anchors)
     B) Hybrid (Dark Hero / Light Body)
     C) Wizard (vollbild ein Step pro Screen)
*/

const { useState: cpUseState, useRef: cpUseRef, useEffect: cpUseEffect, useMemo: cpUseMemo } = React;

// ─── Sample data ──────────────────────────────────────────────
const CP_CATEGORIES = [
  { id: 'main',  label: 'Hauptgericht',  emoji: '🍽️' },
  { id: 'soup',  label: 'Suppe',         emoji: '🥣' },
  { id: 'salad', label: 'Salat',         emoji: '🥗' },
  { id: 'snack', label: 'Snack',         emoji: '🍿' },
  { id: 'sweet', label: 'Süß',           emoji: '🍰' },
  { id: 'drink', label: 'Drink',         emoji: '🥤' },
];

const CP_PREFS = [
  { id: 'veg',  label: 'Vegetarisch', emoji: '🥦' },
  { id: 'vegan',label: 'Vegan',       emoji: '🌱' },
  { id: 'meat', label: 'Fleisch',     emoji: '🥩' },
  { id: 'fish', label: 'Fisch',       emoji: '🐟' },
  { id: 'glut', label: 'Glutenfrei',  emoji: '🌾' },
  { id: 'lact', label: 'Laktosefrei', emoji: '🥛' },
];

const CP_INGREDIENT_SUGGESTIONS = [
  { name: 'Zwiebel',     emoji: '🧅' },
  { name: 'Knoblauch',   emoji: '🧄' },
  { name: 'Tomate',      emoji: '🍅' },
  { name: 'Paprika',     emoji: '🫑' },
  { name: 'Karotte',     emoji: '🥕' },
  { name: 'Pasta',       emoji: '🍝' },
  { name: 'Reis',        emoji: '🍚' },
  { name: 'Olivenöl',    emoji: '🫒' },
  { name: 'Salz',        emoji: '🧂' },
  { name: 'Pfeffer',     emoji: '🌶️' },
  { name: 'Basilikum',   emoji: '🌿' },
  { name: 'Parmesan',    emoji: '🧀' },
];

const CP_DEFAULT_INGREDIENTS = [
  { name: 'Tomatenmark', emoji: '🥫', amount: 1, unit: 'EL' },
  { name: 'Honig',       emoji: '🍯', amount: 1, unit: 'TL' },
  { name: 'Zwiebel',     emoji: '🧅', amount: 2, unit: 'Stk.' },
];

const CP_DETECTED = [
  { pct: 8, label: 'Suppe',       emoji: '🥣', best: true },
  { pct: 7, label: 'Eintopf',     emoji: '🍲' },
  { pct: 5, label: 'Curry',       emoji: '🍛' },
  { pct: 4, label: 'Pasta-Gericht', emoji: '🍝' },
];

const CP_DETECTED_STEPS = [
  { action: 'Wasche', objects: ['die Zwiebeln (rot)', 'die Karotten'], hint: 'gründlich unter kaltem Wasser' },
  { action: 'Schäle', objects: ['die Zwiebeln (rot)', 'die Karotten'], hint: 'mit einem Werkzeug' },
  { action: 'Schneide', objects: ['die Zwiebeln'], hint: 'in feine Würfel' },
];

const CP_STEP_TEMPLATES = [
  { id: 't1', cat: 'Vorbereitung', emoji: '🥕', title: 'Schneiden / Zerkleinern', body: 'Schneide [Zutat] in [Größe] [Form].', stars: 3 },
  { id: 't2', cat: 'Vorbereitung', emoji: '🥕', title: 'Schälen', body: 'Schäle [Zutat] mit einem Werkzeug.', stars: 3 },
  { id: 't3', cat: 'Vorbereitung', emoji: '🥕', title: 'Waschen & Trocknen', body: 'Wasche [Zutat] gründlich und tupfe sie trocken.', stars: 3 },
  { id: 't4', cat: 'Kochen', emoji: '🔥', title: 'Anbraten', body: 'Brate [Zutat] in der Pfanne scharf an.', stars: 3 },
  { id: 't5', cat: 'Kochen', emoji: '🔥', title: 'Köcheln lassen', body: 'Lasse [Gericht] für [Zeit] köcheln.', stars: 2 },
];

const CP_KEYWORDS = [
  'Abendessen','Einfach','Familienfreundlich','Frühstück','Gesund','Glutenfrei',
  'Gourmet','Grill','Günstig','Herzhaft','High Carb','Kalorienarm',
  'Kinderfreundlich','Laktosefrei','Low Carb','Low Fat','Meal Bowl','Meal Prep',
  'Mittagessen','Ofengericht','One Pot','Proteinreich','Saisonal','Scharf',
  'Schnell','Snack','Süß','Vegan','Vegetarisch','Zuckerfrei',
];

// Sample uploaded media (one tile already filled to show "filled" state)
const CP_SAMPLE_MEDIA = 'https://images.unsplash.com/photo-1565557623262-b51c2513a641?w=800&q=80';

// ─── Building blocks ──────────────────────────────────────────

function CPLabel({ children, badge }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      fontFamily: "'Inter', sans-serif",
      fontSize: 11, fontWeight: 600,
      color: 'var(--ink-mute, #6b7868)',
      letterSpacing: '0.06em',
      textTransform: 'uppercase',
      marginBottom: 8,
    }}>
      <span>{children}</span>
      {badge && <span style={{ color: 'var(--coral, #ff7849)' }}>{badge}</span>}
    </div>
  );
}

function CPInput({ icon, placeholder, value, onChange, suffix }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      background: 'white',
      border: '1px solid rgba(20,57,31,0.1)',
      borderRadius: 14,
      padding: '12px 14px',
      transition: 'all .15s',
    }}>
      {icon && <span style={{ fontSize: 16, opacity: 0.6 }}>{icon}</span>}
      <input
        value={value || ''}
        onChange={(e) => onChange && onChange(e.target.value)}
        placeholder={placeholder}
        style={{
          flex: 1, border: 'none', outline: 'none',
          background: 'transparent',
          fontSize: 15, fontWeight: 500,
          color: '#14391f',
          fontFamily: 'inherit',
        }}
      />
      {suffix && <span style={{ fontSize: 12, color: '#6b7868', fontWeight: 600 }}>{suffix}</span>}
    </div>
  );
}

function CPSelect({ icon, value, placeholder, onClick }) {
  return (
    <button
      onClick={onClick}
      style={{
        width: '100%',
        display: 'flex', alignItems: 'center', gap: 10,
        background: 'white',
        border: '1px solid rgba(20,57,31,0.1)',
        borderRadius: 14,
        padding: '12px 14px',
        cursor: 'pointer',
        fontFamily: 'inherit',
        textAlign: 'left',
      }}>
      {icon && <span style={{ fontSize: 16 }}>{icon}</span>}
      <span style={{ flex: 1, fontSize: 14, fontWeight: 600, color: value ? '#14391f' : '#9ba59c' }}>{value || placeholder}</span>
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#6b7868" strokeWidth="2.5"><path d="M6 9l6 6 6-6"/></svg>
    </button>
  );
}

function CPPill({ active, onClick, children, color = 'ink' }) {
  const palette = {
    ink:    { bg: '#14391f', fg: 'white', border: '#14391f' },
    coral:  { bg: '#ff7849', fg: 'white', border: '#ff7849' },
    gold:   { bg: '#f5b942', fg: '#14391f', border: '#f5b942' },
  };
  const c = palette[color] || palette.ink;
  return (
    <button
      onClick={onClick}
      style={{
        padding: '7px 14px',
        borderRadius: 999,
        background: active ? c.bg : 'white',
        color: active ? c.fg : '#14391f',
        border: `1px solid ${active ? c.border : 'rgba(20,57,31,0.12)'}`,
        fontSize: 12, fontWeight: 600,
        cursor: 'pointer', fontFamily: 'inherit',
        whiteSpace: 'nowrap',
        transition: 'all .15s',
      }}>
      {children}
    </button>
  );
}

// Section card chrome — used by all variants
function CPSection({ idx, total, title, subtitle, icon, complete, children, id, accent }) {
  return (
    <section
      id={id}
      style={{
        background: 'white',
        borderRadius: 22,
        boxShadow: '0 8px 24px rgba(20,57,31,0.06)',
        marginBottom: 14,
        overflow: 'hidden',
        scrollMarginTop: 88,
      }}>
      <div style={{
        display: 'flex', alignItems: 'flex-start', gap: 12,
        padding: '18px 18px 12px',
        borderBottom: '1px solid rgba(20,57,31,0.06)',
      }}>
        <div style={{
          width: 38, height: 38,
          borderRadius: 12,
          background: complete ? '#14391f' : (accent || '#faf6ec'),
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 18,
          flexShrink: 0,
          color: complete ? 'white' : '#14391f',
        }}>
          {complete ? (
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3">
              <path d="M5 13l4 4L19 7"/>
            </svg>
          ) : icon}
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <h3 style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 20, fontWeight: 600,
            letterSpacing: '-0.01em',
            color: '#14391f',
            margin: 0, lineHeight: 1.1,
          }}>{title}</h3>
          {subtitle && <div style={{ fontSize: 12, color: '#6b7868', marginTop: 3 }}>{subtitle}</div>}
        </div>
        <span style={{
          background: complete ? '#5fa052' : '#faf6ec',
          color: complete ? 'white' : '#14391f',
          fontSize: 11, fontWeight: 700,
          padding: '4px 10px', borderRadius: 999,
          flexShrink: 0,
          fontFamily: "'Fraunces', serif",
        }}>{idx}/{total}</span>
      </div>
      <div style={{ padding: 18 }}>{children}</div>
    </section>
  );
}

// Sticky tab navigator (with progress)
function CPTabBar({ active, onChange, completion, theme = 'light' }) {
  const tabs = [
    { id: 'basis',    label: 'Basis',    emoji: '🧩' },
    { id: 'media',    label: 'Media',    emoji: '🎬' },
    { id: 'zutaten',  label: 'Zutaten',  emoji: '🛒' },
    { id: 'steps',    label: 'Steps',    emoji: '📝' },
    { id: 'keywords', label: 'Keywords', emoji: '#' },
  ];
  const trackRef = cpUseRef(null);

  return (
    <div style={{
      display: 'flex', gap: 6,
      overflowX: 'auto',
      padding: '4px 14px 8px',
      WebkitOverflowScrolling: 'touch',
    }} ref={trackRef}>
      {tabs.map(t => {
        const done = completion[t.id];
        const isActive = active === t.id;
        return (
          <button
            key={t.id}
            onClick={() => onChange(t.id)}
            style={{
              flexShrink: 0,
              padding: '8px 14px',
              borderRadius: 999,
              fontSize: 13, fontWeight: 600,
              fontFamily: 'inherit',
              cursor: 'pointer',
              display: 'inline-flex', alignItems: 'center', gap: 6,
              transition: 'all .2s',
              background: isActive ? '#14391f' : 'white',
              color:      isActive ? 'white'   : '#14391f',
              border: `1px solid ${isActive ? '#14391f' : 'rgba(20,57,31,0.1)'}`,
              boxShadow: isActive ? '0 4px 14px rgba(20,57,31,0.18)' : 'none',
            }}>
            <span style={{ fontSize: t.id === 'keywords' ? 14 : 13 }}>{t.emoji}</span>
            {t.label}
            {done && (
              <span style={{
                width: 14, height: 14, borderRadius: '50%',
                background: isActive ? '#5fa052' : '#5fa052',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                marginLeft: 2,
              }}>
                <svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="4">
                  <path d="M5 13l4 4L19 7"/>
                </svg>
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}

// Sticky publish bar
function CPPublishBar({ doneCount, total = 5, onPublish }) {
  const pct = Math.round((doneCount / total) * 100);
  const ready = doneCount === total;
  return (
    <div style={{
      position: 'absolute',
      bottom: 0, left: 0, right: 0,
      padding: '12px 16px 28px',
      background: 'linear-gradient(180deg, rgba(245,239,225,0) 0%, rgba(245,239,225,0.95) 30%, #f5efe1 100%)',
      backdropFilter: 'blur(10px)',
      WebkitBackdropFilter: 'blur(10px)',
      zIndex: 4,
    }}>
      <div style={{
        background: 'white',
        borderRadius: 18,
        padding: '8px 8px 8px 16px',
        boxShadow: '0 12px 30px rgba(20,57,31,0.16)',
        display: 'flex', alignItems: 'center', gap: 10,
        border: '1px solid rgba(20,57,31,0.08)',
      }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontFamily: "'Fraunces', serif", fontSize: 16, fontWeight: 600, color: '#14391f', lineHeight: 1.1 }}>
            {ready ? 'Bereit zum Posten ✨' : `${doneCount}/${total} Schritte fertig`}
          </div>
          <div style={{
            position: 'relative', height: 4,
            background: 'rgba(20,57,31,0.08)',
            borderRadius: 2, marginTop: 6,
          }}>
            <div style={{
              position: 'absolute', left: 0, top: 0, bottom: 0,
              width: `${pct}%`,
              background: ready ? '#5fa052' : 'linear-gradient(90deg, #ff9a3c, #ff7849)',
              borderRadius: 2,
              transition: 'width .3s',
            }} />
          </div>
        </div>
        <button
          onClick={onPublish}
          disabled={!ready}
          style={{
            background: ready ? 'linear-gradient(135deg,#ff9a3c,#ff7849)' : 'rgba(20,57,31,0.08)',
            color: ready ? 'white' : '#6b7868',
            border: 'none',
            borderRadius: 14,
            padding: '12px 18px',
            fontSize: 14, fontWeight: 700,
            display: 'inline-flex', alignItems: 'center', gap: 6,
            cursor: ready ? 'pointer' : 'not-allowed',
            boxShadow: ready ? '0 6px 20px rgba(255,120,73,0.4)' : 'none',
            fontFamily: 'inherit',
            flexShrink: 0,
          }}>
          Veröffentlichen
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z"/>
          </svg>
        </button>
      </div>
    </div>
  );
}

// ─── Section bodies ───────────────────────────────────────────

function CPBasisBody({ data, setData }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div>
        <CPLabel>Titel</CPLabel>
        <CPInput
          icon="✏️"
          value={data.title}
          onChange={(v) => setData({ ...data, title: v })}
          placeholder="z.B. Cremige Tomatensuppe"
        />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <div>
          <CPLabel>Kategorie</CPLabel>
          <CPSelect icon={data.category?.emoji || '🍽️'} value={data.category?.label} placeholder="Wählen" />
        </div>
        <div>
          <CPLabel>Präferenz</CPLabel>
          <CPSelect icon={data.pref?.emoji || '💚'} value={data.pref?.label} placeholder="Wählen" />
        </div>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <div>
          <CPLabel>Personen</CPLabel>
          <CPInput icon="👥" value={data.persons} onChange={(v) => setData({ ...data, persons: v })} placeholder="2" suffix="Pers." />
        </div>
        <div>
          <CPLabel>Dauer</CPLabel>
          <CPInput icon="⏱" value={data.duration} onChange={(v) => setData({ ...data, duration: v })} placeholder="30" suffix="Min" />
        </div>
      </div>
    </div>
  );
}

function CPMediaBody({ media, setMedia, theme = 'light' }) {
  if (media) {
    return (
      <div>
        <div style={{
          position: 'relative',
          borderRadius: 18,
          overflow: 'hidden',
          aspectRatio: '9/14',
          background: `url(${media}) center/cover`,
          boxShadow: '0 12px 30px rgba(20,57,31,0.18)',
        }}>
          {/* TikTok-like overlay buttons */}
          <div style={{
            position: 'absolute',
            right: 10, bottom: 12,
            display: 'flex', flexDirection: 'column', gap: 14,
            alignItems: 'center',
          }}>
            {[
              { icon: '❤️', count: '—' },
              { icon: '💬', count: '—' },
              { icon: '🔖', count: '—' },
            ].map((b, i) => (
              <div key={i} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2 }}>
                <span style={{
                  width: 36, height: 36, borderRadius: '50%',
                  background: 'rgba(0,0,0,0.35)',
                  backdropFilter: 'blur(8px)',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 16,
                }}>{b.icon}</span>
                <span style={{ color: 'white', fontSize: 9, fontWeight: 600, textShadow: '0 1px 4px rgba(0,0,0,0.5)' }}>{b.count}</span>
              </div>
            ))}
          </div>
          <div style={{
            position: 'absolute',
            left: 12, bottom: 12, right: 70,
            color: 'white',
            textShadow: '0 2px 8px rgba(0,0,0,0.5)',
          }}>
            <div style={{ fontSize: 11, fontWeight: 600, opacity: 0.85 }}>VORSCHAU · FEED</div>
            <div style={{ fontFamily: "'Fraunces',serif", fontSize: 18, fontWeight: 600, lineHeight: 1.15, marginTop: 2 }}>
              So sieht dein Posting im Feed aus
            </div>
          </div>
          {/* Replace overlay control */}
          <button
            onClick={() => setMedia(null)}
            style={{
              position: 'absolute',
              top: 10, right: 10,
              background: 'rgba(255,255,255,0.95)',
              border: 'none',
              borderRadius: 999,
              padding: '6px 12px',
              fontSize: 11, fontWeight: 700,
              color: '#14391f',
              cursor: 'pointer',
              fontFamily: 'inherit',
              display: 'inline-flex', alignItems: 'center', gap: 4,
            }}>
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M7 10l5-5 5 5M12 5v14"/></svg>
            Ersetzen
          </button>
        </div>
        <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
          <button style={{
            flex: 1, border: '1px solid rgba(20,57,31,0.12)',
            background: 'white', borderRadius: 12, padding: '10px',
            fontSize: 12, fontWeight: 600, color: '#14391f', cursor: 'pointer',
            fontFamily: 'inherit',
          }}>✂️ Trimmen</button>
          <button style={{
            flex: 1, border: '1px solid rgba(20,57,31,0.12)',
            background: 'white', borderRadius: 12, padding: '10px',
            fontSize: 12, fontWeight: 600, color: '#14391f', cursor: 'pointer',
            fontFamily: 'inherit',
          }}>🎵 Sound</button>
          <button style={{
            flex: 1, border: '1px solid rgba(20,57,31,0.12)',
            background: 'white', borderRadius: 12, padding: '10px',
            fontSize: 12, fontWeight: 600, color: '#14391f', cursor: 'pointer',
            fontFamily: 'inherit',
          }}>📐 Format</button>
        </div>
      </div>
    );
  }

  return (
    <div>
      <button
        onClick={() => setMedia(CP_SAMPLE_MEDIA)}
        style={{
          width: '100%',
          aspectRatio: '9/14',
          border: '2px dashed rgba(20,57,31,0.2)',
          background: 'linear-gradient(180deg, #faf6ec 0%, #f5efe1 100%)',
          borderRadius: 18,
          cursor: 'pointer',
          display: 'flex', flexDirection: 'column',
          alignItems: 'center', justifyContent: 'center',
          gap: 12, padding: 20,
          fontFamily: 'inherit',
          position: 'relative',
          overflow: 'hidden',
        }}>
        <div style={{
          width: 64, height: 64, borderRadius: '50%',
          background: 'linear-gradient(135deg, #ff9a3c, #ff7849)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          boxShadow: '0 10px 24px rgba(255,120,73,0.4)',
        }}>
          <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.4">
            <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M7 10l5-5 5 5M12 5v14"/>
          </svg>
        </div>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontFamily: "'Fraunces',serif", fontSize: 22, fontWeight: 600, color: '#14391f' }}>Upload starten</div>
          <div style={{ fontSize: 13, color: '#6b7868', marginTop: 4 }}>Wähle ein Video oder Bild für den Feed</div>
        </div>
        <div style={{ display: 'flex', gap: 6, marginTop: 4 }}>
          <span style={{ fontSize: 10, fontWeight: 600, color: '#6b7868', background: 'white', padding: '4px 10px', borderRadius: 999, border: '1px solid rgba(20,57,31,0.08)' }}>9:16</span>
          <span style={{ fontSize: 10, fontWeight: 600, color: '#6b7868', background: 'white', padding: '4px 10px', borderRadius: 999, border: '1px solid rgba(20,57,31,0.08)' }}>MP4 · MOV</span>
          <span style={{ fontSize: 10, fontWeight: 600, color: '#6b7868', background: 'white', padding: '4px 10px', borderRadius: 999, border: '1px solid rgba(20,57,31,0.08)' }}>≤ 60s</span>
        </div>
      </button>
      <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
        <button style={{
          flex: 1, background: 'white', border: '1px solid rgba(20,57,31,0.12)',
          borderRadius: 12, padding: '10px', fontSize: 12, fontWeight: 600,
          color: '#14391f', cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
        }}>📷 Kamera</button>
        <button style={{
          flex: 1, background: 'white', border: '1px solid rgba(20,57,31,0.12)',
          borderRadius: 12, padding: '10px', fontSize: 12, fontWeight: 600,
          color: '#14391f', cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
        }}>🖼 Galerie</button>
        <button style={{
          flex: 1, background: 'white', border: '1px solid rgba(20,57,31,0.12)',
          borderRadius: 12, padding: '10px', fontSize: 12, fontWeight: 600,
          color: '#14391f', cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
        }}>✨ AI-Gen</button>
      </div>
    </div>
  );
}

function CPZutatenBody({ ingredients, setIngredients }) {
  const [search, setSearch] = cpUseState('');
  const filtered = cpUseMemo(() => {
    if (!search) return CP_INGREDIENT_SUGGESTIONS;
    return CP_INGREDIENT_SUGGESTIONS.filter(s => s.name.toLowerCase().includes(search.toLowerCase()));
  }, [search]);

  const adjust = (idx, delta) => {
    const next = [...ingredients];
    next[idx].amount = Math.max(0, next[idx].amount + delta);
    if (next[idx].amount === 0) next.splice(idx, 1);
    setIngredients(next);
  };

  const add = (s) => {
    if (ingredients.find(i => i.name === s.name)) return;
    setIngredients([...ingredients, { ...s, amount: 1, unit: 'Stk.' }]);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <CPInput
        icon="🔍"
        value={search}
        onChange={setSearch}
        placeholder="Zutat suchen..."
      />

      {/* Selected list */}
      {ingredients.length > 0 && (
        <div>
          <CPLabel>Deine Zutaten <span style={{ color: '#14391f', textTransform: 'none', letterSpacing: 0, fontWeight: 700 }}> · {ingredients.length}</span></CPLabel>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {ingredients.map((ing, idx) => (
              <div key={ing.name} style={{
                display: 'flex', alignItems: 'center', gap: 10,
                background: '#faf6ec',
                border: '1px solid rgba(20,57,31,0.08)',
                borderRadius: 14,
                padding: '10px 12px',
              }}>
                <span style={{ fontSize: 22 }}>{ing.emoji}</span>
                <span style={{ flex: 1, fontSize: 14, fontWeight: 600, color: '#14391f' }}>{ing.name}</span>
                <div style={{
                  display: 'inline-flex', alignItems: 'center',
                  background: 'white',
                  borderRadius: 999,
                  padding: 2,
                  border: '1px solid rgba(20,57,31,0.08)',
                }}>
                  <button onClick={() => adjust(idx, -1)} style={{
                    width: 22, height: 22, border: 'none', background: 'transparent',
                    cursor: 'pointer', fontSize: 14, color: '#14391f', fontWeight: 700,
                  }}>−</button>
                  <span style={{ fontSize: 12, fontWeight: 700, color: '#14391f', padding: '0 6px', minWidth: 38, textAlign: 'center' }}>{ing.amount} {ing.unit}</span>
                  <button onClick={() => adjust(idx, 1)} style={{
                    width: 22, height: 22, border: 'none', background: '#14391f', borderRadius: '50%',
                    cursor: 'pointer', fontSize: 13, color: 'white', fontWeight: 700,
                    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  }}>+</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Suggestions */}
      <div>
        <CPLabel badge="✨ AI vorgeschlagen">Vorschläge zum Antippen</CPLabel>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
          {filtered.slice(0, 10).map(s => {
            const added = !!ingredients.find(i => i.name === s.name);
            return (
              <button key={s.name} onClick={() => add(s)} disabled={added}
                style={{
                  display: 'inline-flex', alignItems: 'center', gap: 6,
                  padding: '7px 12px',
                  borderRadius: 999,
                  background: added ? 'rgba(95,160,82,0.14)' : 'white',
                  color: added ? '#3f7536' : '#14391f',
                  border: `1px solid ${added ? 'rgba(95,160,82,0.3)' : 'rgba(20,57,31,0.12)'}`,
                  fontSize: 12, fontWeight: 600,
                  cursor: added ? 'default' : 'pointer',
                  fontFamily: 'inherit',
                }}>
                <span style={{ fontSize: 14 }}>{s.emoji}</span>
                {s.name}
                {added ? (
                  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="#5fa052" strokeWidth="3"><path d="M5 13l4 4L19 7"/></svg>
                ) : (
                  <span style={{ fontSize: 14, fontWeight: 700, color: '#ff7849' }}>+</span>
                )}
              </button>
            );
          })}
        </div>
      </div>

      <button style={{
        width: '100%',
        background: 'linear-gradient(180deg, #faf6ec, #f5efe1)',
        border: '1px dashed rgba(20,57,31,0.18)',
        borderRadius: 14,
        padding: '12px',
        fontSize: 13, fontWeight: 600,
        color: '#14391f',
        cursor: 'pointer',
        fontFamily: 'inherit',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
      }}>✨ Zutat fehlt? AI bitten</button>
    </div>
  );
}

// Section 4 · Steps  (Rezepttyp + erkannte Steps)
function CPStepsBody({ steps, setSteps }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* What could it be */}
      <div style={{
        background: '#faf6ec',
        border: '1px solid rgba(20,57,31,0.08)',
        borderRadius: 16,
        padding: 14,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
          <span style={{ fontSize: 18 }}>🔮</span>
          <div>
            <div style={{ fontSize: 13, fontWeight: 700, color: '#14391f' }}>Was könnte es sein?</div>
            <div style={{ fontSize: 11, color: '#6b7868' }}>Rezepttyp & passende Steps</div>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {CP_DETECTED.map(d => (
            <span key={d.label} style={{
              display: 'inline-flex', alignItems: 'center', gap: 5,
              padding: '6px 11px',
              borderRadius: 999,
              background: d.best ? '#14391f' : 'white',
              color: d.best ? 'white' : '#14391f',
              border: `1px solid ${d.best ? '#14391f' : 'rgba(20,57,31,0.12)'}`,
              fontSize: 11, fontWeight: 600,
            }}>
              {d.best && <svg width="10" height="10" viewBox="0 0 24 24" fill="#f5b942"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg>}
              <span>{d.emoji}</span>
              {d.label}
              <span style={{ opacity: d.best ? 0.7 : 0.5, fontWeight: 700 }}>{d.pct}%</span>
            </span>
          ))}
        </div>
      </div>

      {/* Detected steps to accept */}
      <div>
        <CPLabel>Erkannte Steps · zum Antippen</CPLabel>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {CP_DETECTED_STEPS.map((s, i) => (
            <div key={i} style={{
              background: 'white',
              border: '1px solid rgba(20,57,31,0.1)',
              borderRadius: 14,
              padding: 14,
            }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                <span style={{ fontSize: 10, fontWeight: 700, color: '#6b7868', letterSpacing: '0.06em', textTransform: 'uppercase' }}>Erkannter Step #{i + 1}</span>
                <div style={{ display: 'flex', gap: 5 }}>
                  <button style={{
                    background: 'rgba(20,57,31,0.06)',
                    border: 'none', borderRadius: 999,
                    padding: '5px 10px', fontSize: 11, fontWeight: 600, color: '#14391f',
                    cursor: 'pointer', fontFamily: 'inherit',
                  }}>✕</button>
                  <button style={{
                    background: '#14391f',
                    color: 'white',
                    border: 'none', borderRadius: 999,
                    padding: '5px 12px', fontSize: 11, fontWeight: 700,
                    cursor: 'pointer', fontFamily: 'inherit',
                    display: 'inline-flex', alignItems: 'center', gap: 4,
                  }}>
                    <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="3"><path d="M5 13l4 4L19 7"/></svg>
                    Akzeptieren
                  </button>
                </div>
              </div>
              <div style={{ fontSize: 14, color: '#14391f', lineHeight: 1.45 }}>
                <strong style={{ color: '#ff7849' }}>{s.action}</strong>{' '}
                <em style={{ color: '#14391f', fontWeight: 600, fontStyle: 'normal', borderBottom: '2px dotted #ff9a3c', paddingBottom: 1 }}>
                  {s.objects.join(' und ')}
                </em>{' '}
                <span style={{ color: '#6b7868' }}>{s.hint}</span>.
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Smart Step Creator - inline */}
      <CPSmartStepBody />
    </div>
  );
}

// Section 5 · Smart Step Creator (eigenständige Karte)
function CPSmartStepBody() {
  const [activeCat, setActiveCat] = cpUseState('Beste');
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      {/* Dark filter panel */}
      <div style={{
        background: 'linear-gradient(135deg, #14391f, #0e2415)',
        borderRadius: 16,
        padding: 16,
        color: 'white',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
          <span style={{ fontSize: 16 }}>✨</span>
          <div style={{ fontFamily: "'Fraunces',serif", fontSize: 16, fontWeight: 600 }}>Smart Step Creator</div>
        </div>
        <div style={{ fontSize: 12, opacity: 0.7, marginBottom: 12 }}>Zutat + Template wie im Creator-Flow</div>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 12 }}>
          {[
            { id: 'Alle', label: 'Alle' },
            { id: 'Beste', label: '⭐ Beste' },
            { id: 'Vorbereitung', label: '🥕 Vorbereitung' },
            { id: 'Kochen', label: '🔥 Kochen' },
            { id: 'Finishing', label: '✨ Finishing' },
            { id: 'Servieren', label: '🍽️ Servieren' },
          ].map(c => (
            <button key={c.id} onClick={() => setActiveCat(c.id)} style={{
              padding: '6px 11px',
              borderRadius: 999,
              background: activeCat === c.id ? 'white' : 'rgba(255,255,255,0.08)',
              color: activeCat === c.id ? '#14391f' : 'white',
              border: activeCat === c.id ? 'none' : '1px solid rgba(255,255,255,0.2)',
              fontSize: 11, fontWeight: 600,
              cursor: 'pointer', fontFamily: 'inherit',
            }}>{c.label}</button>
          ))}
        </div>
        {/* Search inside dark panel */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8,
          background: 'rgba(255,255,255,0.08)',
          border: '1px solid rgba(255,255,255,0.16)',
          borderRadius: 12,
          padding: '9px 12px',
        }}>
          <span style={{ fontSize: 12, opacity: 0.7 }}>🔍</span>
          <input placeholder="Step suchen (z.B. anbraten, schneiden)…"
            style={{
              flex: 1, border: 'none', outline: 'none', background: 'transparent',
              fontSize: 12, color: 'white', fontFamily: 'inherit',
            }} />
        </div>
      </div>

      {/* Templates list */}
      <div>
        <CPLabel>Templates · zum Antippen</CPLabel>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {CP_STEP_TEMPLATES.slice(0, 3).map(t => (
            <button key={t.id} style={{
              background: 'white',
              border: '1px solid rgba(20,57,31,0.1)',
              borderRadius: 14,
              padding: 12,
              display: 'flex', alignItems: 'center', gap: 10,
              cursor: 'pointer', fontFamily: 'inherit', textAlign: 'left',
              width: '100%',
            }}>
              <div style={{
                width: 38, height: 38, borderRadius: 10,
                background: '#faf6ec',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: 18, flexShrink: 0,
              }}>{t.emoji}</div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: '#14391f' }}>{t.title}</div>
                <div style={{ fontSize: 11, color: '#6b7868', marginTop: 2 }}>{t.body}</div>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 4 }}>
                <div style={{ fontSize: 10, color: '#f5b942' }}>{'★'.repeat(t.stars)}</div>
                <span style={{
                  width: 22, height: 22, borderRadius: '50%',
                  background: '#ff7849', color: 'white',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 13, fontWeight: 700,
                }}>+</span>
              </div>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

function CPKeywordsBody({ selected, setSelected }) {
  const toggle = (k) => {
    setSelected(selected.includes(k) ? selected.filter(s => s !== k) : [...selected, k]);
  };
  return (
    <div>
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        marginBottom: 10,
      }}>
        <CPLabel>{selected.length} ausgewählt · max 8 sichtbar im Feed</CPLabel>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
        {CP_KEYWORDS.map(k => (
          <CPPill key={k} active={selected.includes(k)} onClick={() => toggle(k)} color="ink">
            {k}
          </CPPill>
        ))}
      </div>
      {selected.length > 0 && (
        <div style={{
          marginTop: 14,
          padding: '12px 14px',
          background: '#faf6ec',
          border: '1px solid rgba(20,57,31,0.08)',
          borderRadius: 14,
          fontSize: 12, color: '#14391f',
        }}>
          <span style={{ fontWeight: 700 }}>Vorschau:</span>{' '}
          {selected.slice(0, 6).map(s => <span key={s} style={{ color: '#ff7849', fontWeight: 600 }}>#{s.replace(/\s+/g,'')} </span>)}
          {selected.length > 6 && <span style={{ color: '#6b7868' }}>+{selected.length - 6}</span>}
        </div>
      )}
    </div>
  );
}

// ─── VARIANT A · Scroll-Stack (Light, Sticky-Tabs) ──────────────────────────
function CreatePostingA() {
  const [active, setActive] = cpUseState('basis');
  const [data, setData] = cpUseState({
    title: 'Cremige Tomatensuppe',
    category: { label: 'Suppe', emoji: '🥣' },
    pref: { label: 'Vegetarisch', emoji: '🥦' },
    persons: '4',
    duration: '30',
  });
  const [media, setMedia] = cpUseState(CP_SAMPLE_MEDIA);
  const [ingredients, setIngredients] = cpUseState(CP_DEFAULT_INGREDIENTS);
  const [keywords, setKeywords] = cpUseState(['Einfach','Familienfreundlich','Herzhaft','Vegetarisch','Mittagessen']);
  const [steps] = cpUseState([]);
  const feedRef = cpUseRef(null);

  const completion = {
    basis: !!data.title && !!data.category && !!data.persons && !!data.duration,
    media: !!media,
    zutaten: ingredients.length >= 2,
    steps: false,
    keywords: keywords.length >= 3,
  };
  const doneCount = Object.values(completion).filter(Boolean).length;

  const goTo = (id) => {
    setActive(id);
    const el = feedRef.current?.querySelector(`#cp-${id}`);
    if (el && feedRef.current) {
      feedRef.current.scrollTo({ top: el.offsetTop - 12, behavior: 'smooth' });
    }
  };

  // Scroll-spy
  cpUseEffect(() => {
    const root = feedRef.current;
    if (!root) return;
    const onScroll = () => {
      const ids = ['basis','media','zutaten','steps','keywords'];
      const top = root.scrollTop + 90;
      let cur = ids[0];
      for (const id of ids) {
        const el = root.querySelector(`#cp-${id}`);
        if (el && el.offsetTop <= top) cur = id;
      }
      setActive(cur);
    };
    root.addEventListener('scroll', onScroll);
    return () => root.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <div className="mp-screen" style={{ background: '#f5efe1' }}>
      {/* Top bar */}
      <div style={{
        flexShrink: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '14px 16px 6px',
      }}>
        <button className="mp-back" style={{ width: 36, height: 36, background: 'white', borderRadius: '50%', border: 'none', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 2px 8px rgba(20,57,31,0.06)' }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#14391f" strokeWidth="2.5"><path d="M15 18l-6-6 6-6"/></svg>
        </button>
        <div style={{ textAlign: 'center', flex: 1 }}>
          <div style={{ fontSize: 11, color: '#6b7868', fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase' }}>Posting erstellen</div>
          <div style={{ fontFamily: "'Fraunces',serif", fontSize: 15, fontWeight: 600, color: '#14391f', marginTop: 1 }}>
            {doneCount}/5 bereit
          </div>
        </div>
        <button style={{
          background: 'white', border: 'none', borderRadius: 999,
          padding: '6px 10px', fontSize: 11, fontWeight: 700, color: '#14391f',
          display: 'inline-flex', alignItems: 'center', gap: 3, cursor: 'pointer',
          boxShadow: '0 2px 8px rgba(20,57,31,0.06)', fontFamily: 'inherit',
        }}>
          DE <svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="#14391f" strokeWidth="3"><path d="M6 9l6 6 6-6"/></svg>
        </button>
      </div>

      {/* Sticky tab bar */}
      <div style={{ flexShrink: 0, position: 'relative', zIndex: 3 }}>
        <CPTabBar active={active} onChange={goTo} completion={completion} />
      </div>

      {/* Scrolling sections */}
      <div ref={feedRef} className="mp-feed" style={{ paddingBottom: 110, paddingLeft: 14, paddingRight: 14, paddingTop: 4 }}>
        <CPSection id="cp-basis" idx={1} total={5} title="Basis" subtitle="Titel, Kategorie, Personen, Dauer" icon="🧩" complete={completion.basis}>
          <CPBasisBody data={data} setData={setData} />
        </CPSection>
        <CPSection id="cp-media" idx={2} total={5} title="Media" subtitle="Video oder Bild für den Feed" icon="🎬" complete={completion.media} accent="#fff0e6">
          <CPMediaBody media={media} setMedia={setMedia} />
        </CPSection>
        <CPSection id="cp-zutaten" idx={3} total={5} title="Zutaten" subtitle="Suche & konfiguriere" icon="🛒" complete={completion.zutaten}>
          <CPZutatenBody ingredients={ingredients} setIngredients={setIngredients} />
        </CPSection>
        <CPSection id="cp-steps" idx={4} total={5} title="Steps" subtitle="Rezepttyp, erkannte & eigene Steps" icon="📝" complete={completion.steps}>
          <CPStepsBody steps={steps} setSteps={() => {}} />
        </CPSection>
        <CPSection id="cp-keywords" idx={5} total={5} title="Keywords" subtitle="Hilft beim Feed & bei der Suche" icon="#" complete={completion.keywords}>
          <CPKeywordsBody selected={keywords} setSelected={setKeywords} />
        </CPSection>
      </div>

      <CPPublishBar doneCount={doneCount} total={5} />
    </div>
  );
}

// ─── VARIANT B · Hybrid Dark Hero / Light Body ─────────────────────────────
function CreatePostingB() {
  const [active, setActive] = cpUseState('media');
  const [data, setData] = cpUseState({
    title: 'Cremige Tomatensuppe',
    category: { label: 'Suppe', emoji: '🥣' },
    pref: { label: 'Vegetarisch', emoji: '🥦' },
    persons: '4',
    duration: '30',
  });
  const [media, setMedia] = cpUseState(CP_SAMPLE_MEDIA);
  const [ingredients, setIngredients] = cpUseState(CP_DEFAULT_INGREDIENTS);
  const [keywords, setKeywords] = cpUseState(['Einfach','Vegetarisch','Mittagessen']);
  const feedRef = cpUseRef(null);

  const completion = {
    basis: true,
    media: !!media,
    zutaten: ingredients.length >= 2,
    steps: false,
    keywords: keywords.length >= 3,
  };
  const doneCount = Object.values(completion).filter(Boolean).length;

  const goTo = (id) => {
    setActive(id);
    const el = feedRef.current?.querySelector(`#cpb-${id}`);
    if (el && feedRef.current) feedRef.current.scrollTo({ top: el.offsetTop - 12, behavior: 'smooth' });
  };

  cpUseEffect(() => {
    const root = feedRef.current;
    if (!root) return;
    const onScroll = () => {
      const ids = ['basis','media','zutaten','steps','keywords'];
      const top = root.scrollTop + 90;
      let cur = ids[0];
      for (const id of ids) {
        const el = root.querySelector(`#cpb-${id}`);
        if (el && el.offsetTop <= top) cur = id;
      }
      setActive(cur);
    };
    root.addEventListener('scroll', onScroll);
    return () => root.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <div className="mp-screen" style={{ background: '#f5efe1' }}>
      {/* Dark Hero */}
      <div style={{
        flexShrink: 0,
        background: 'linear-gradient(180deg, #14391f 0%, #0e2415 100%)',
        padding: '14px 16px 18px',
        color: 'white',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14 }}>
          <button style={{
            width: 34, height: 34, borderRadius: '50%',
            background: 'rgba(255,255,255,0.1)', border: 'none', cursor: 'pointer',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5"><path d="M15 18l-6-6 6-6"/></svg>
          </button>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontSize: 10, opacity: 0.6, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase' }}>Neues Posting</div>
          </div>
          <button style={{
            background: 'rgba(255,255,255,0.1)', color: 'white', border: 'none', borderRadius: 999,
            padding: '6px 10px', fontSize: 11, fontWeight: 700,
            display: 'inline-flex', alignItems: 'center', gap: 3, cursor: 'pointer', fontFamily: 'inherit',
          }}>
            DE <svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3"><path d="M6 9l6 6 6-6"/></svg>
          </button>
        </div>
        <h1 style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 32, fontWeight: 600,
          letterSpacing: '-0.02em',
          margin: 0, lineHeight: 1.05,
        }}>{data.title || 'Neues Posting'}</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10, fontSize: 12 }}>
          <span style={{
            background: 'rgba(255,120,73,0.22)', color: '#ff9a3c',
            padding: '4px 10px', borderRadius: 999,
            fontWeight: 700, display: 'inline-flex', alignItems: 'center', gap: 4,
          }}>
            {data.category.emoji} {data.category.label}
          </span>
          <span style={{ opacity: 0.6 }}>· {data.persons} Pers.</span>
          <span style={{ opacity: 0.6 }}>· {data.duration} Min</span>
          <span style={{ marginLeft: 'auto', display: 'inline-flex', alignItems: 'baseline', gap: 4 }}>
            <span style={{ fontFamily: "'Fraunces',serif", fontSize: 22, fontWeight: 600, color: '#ff9a3c' }}>{doneCount}</span>
            <span style={{ fontSize: 11, opacity: 0.5 }}>/ 5</span>
          </span>
        </div>
        <div style={{
          marginTop: 12, height: 4,
          background: 'rgba(255,255,255,0.1)', borderRadius: 2, overflow: 'hidden',
        }}>
          <div style={{
            width: `${(doneCount/5)*100}%`, height: '100%',
            background: 'linear-gradient(90deg, #ff9a3c, #ff7849)',
            transition: 'width .3s',
          }} />
        </div>
      </div>

      {/* Sticky tabs (negative-margin into hero) */}
      <div style={{
        flexShrink: 0, position: 'relative', zIndex: 3,
        marginTop: -10,
      }}>
        <CPTabBar active={active} onChange={goTo} completion={completion} />
      </div>

      <div ref={feedRef} className="mp-feed" style={{ paddingBottom: 110, paddingLeft: 14, paddingRight: 14, paddingTop: 4 }}>
        <CPSection id="cpb-basis" idx={1} total={5} title="Basis" subtitle="Titel, Kategorie, Personen, Dauer" icon="🧩" complete={completion.basis}>
          <CPBasisBody data={data} setData={setData} />
        </CPSection>
        <CPSection id="cpb-media" idx={2} total={5} title="Media" subtitle="Video oder Bild für den Feed" icon="🎬" complete={completion.media} accent="#fff0e6">
          <CPMediaBody media={media} setMedia={setMedia} />
        </CPSection>
        <CPSection id="cpb-zutaten" idx={3} total={5} title="Zutaten" subtitle="Suche & konfiguriere" icon="🛒" complete={completion.zutaten}>
          <CPZutatenBody ingredients={ingredients} setIngredients={setIngredients} />
        </CPSection>
        <CPSection id="cpb-steps" idx={4} total={5} title="Steps" subtitle="Rezepttyp, erkannte & eigene Steps" icon="📝" complete={completion.steps}>
          <CPStepsBody steps={[]} setSteps={() => {}} />
        </CPSection>
        <CPSection id="cpb-keywords" idx={5} total={5} title="Keywords" subtitle="Hilft beim Feed & bei der Suche" icon="#" complete={completion.keywords}>
          <CPKeywordsBody selected={keywords} setSelected={setKeywords} />
        </CPSection>
      </div>

      <CPPublishBar doneCount={doneCount} total={5} />
    </div>
  );
}

// ─── VARIANT C · Wizard (vollbild ein Step pro Screen) ──────────────────────
function CreatePostingC() {
  const [stepIdx, setStepIdx] = cpUseState(1);  // start on Media (the hero step)
  const [data, setData] = cpUseState({
    title: 'Cremige Tomatensuppe',
    category: { label: 'Suppe', emoji: '🥣' },
    pref: { label: 'Vegetarisch', emoji: '🥦' },
    persons: '4',
    duration: '30',
  });
  const [media, setMedia] = cpUseState(CP_SAMPLE_MEDIA);
  const [ingredients, setIngredients] = cpUseState(CP_DEFAULT_INGREDIENTS);
  const [keywords, setKeywords] = cpUseState(['Einfach','Vegetarisch']);

  const completion = {
    basis: !!data.title,
    media: !!media,
    zutaten: ingredients.length >= 2,
    steps: false,
    keywords: keywords.length >= 3,
  };
  const doneCount = Object.values(completion).filter(Boolean).length;

  const STEPS = [
    { id: 'basis',    title: 'Basis',    sub: 'Titel, Kategorie, Personen, Dauer', body: <CPBasisBody data={data} setData={setData} /> },
    { id: 'media',    title: 'Media',    sub: 'Video oder Bild für den Feed',      body: <CPMediaBody media={media} setMedia={setMedia} /> },
    { id: 'zutaten',  title: 'Zutaten',  sub: 'Suche & konfiguriere',              body: <CPZutatenBody ingredients={ingredients} setIngredients={setIngredients} /> },
    { id: 'steps',    title: 'Steps',    sub: 'Rezepttyp, erkannte & eigene Steps', body: <CPStepsBody steps={[]} setSteps={() => {}} /> },
    { id: 'keywords', title: 'Keywords', sub: 'Hilft beim Feed & bei der Suche',   body: <CPKeywordsBody selected={keywords} setSelected={setKeywords} /> },
  ];
  const cur = STEPS[stepIdx];

  return (
    <div className="mp-screen" style={{ background: '#f5efe1' }}>
      {/* Top bar w/ progress dots */}
      <div style={{
        flexShrink: 0,
        padding: '14px 16px 4px',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <button onClick={() => setStepIdx(Math.max(0, stepIdx - 1))} style={{
            width: 36, height: 36, borderRadius: '50%',
            background: 'white', border: 'none',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            cursor: 'pointer', boxShadow: '0 2px 8px rgba(20,57,31,0.06)',
          }}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#14391f" strokeWidth="2.5"><path d="M15 18l-6-6 6-6"/></svg>
          </button>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontSize: 11, color: '#6b7868', fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase' }}>
              Schritt {stepIdx + 1} von 5
            </div>
            <div style={{ fontFamily: "'Fraunces',serif", fontSize: 16, fontWeight: 600, color: '#14391f', marginTop: 1 }}>
              {cur.title}
            </div>
          </div>
          <button style={{
            background: 'transparent', border: 'none', cursor: 'pointer',
            fontSize: 12, fontWeight: 600, color: '#6b7868', fontFamily: 'inherit',
          }}>Speichern</button>
        </div>

        {/* Segmented progress */}
        <div style={{ display: 'flex', gap: 4 }}>
          {STEPS.map((s, i) => (
            <button key={s.id} onClick={() => setStepIdx(i)} style={{
              flex: 1, height: 4, borderRadius: 2, border: 'none', padding: 0,
              background: i < stepIdx ? '#5fa052' : i === stepIdx ? '#14391f' : 'rgba(20,57,31,0.1)',
              cursor: 'pointer',
            }} />
          ))}
        </div>
      </div>

      {/* Body */}
      <div className="mp-feed" style={{ padding: '12px 14px 110px' }}>
        <div style={{ marginBottom: 16 }}>
          <h2 style={{
            fontFamily: "'Fraunces',serif",
            fontSize: 30, fontWeight: 600,
            letterSpacing: '-0.02em',
            color: '#14391f', margin: 0, lineHeight: 1,
          }}>{cur.title}</h2>
          <div style={{ fontSize: 13, color: '#6b7868', marginTop: 4 }}>{cur.sub}</div>
        </div>
        <div style={{
          background: 'white',
          borderRadius: 22,
          boxShadow: '0 8px 24px rgba(20,57,31,0.06)',
          padding: 18,
        }}>
          {cur.body}
        </div>
      </div>

      {/* Bottom: prev / next or publish */}
      <div style={{
        position: 'absolute', bottom: 0, left: 0, right: 0,
        padding: '12px 16px 28px',
        background: 'linear-gradient(180deg, rgba(245,239,225,0) 0%, #f5efe1 60%)',
        zIndex: 4,
      }}>
        <div style={{ display: 'flex', gap: 8 }}>
          <button onClick={() => setStepIdx(Math.max(0, stepIdx - 1))} disabled={stepIdx === 0} style={{
            background: 'white',
            border: '1px solid rgba(20,57,31,0.12)',
            borderRadius: 14, padding: '14px 18px',
            fontSize: 13, fontWeight: 700, color: '#14391f',
            cursor: stepIdx === 0 ? 'not-allowed' : 'pointer',
            opacity: stepIdx === 0 ? 0.5 : 1,
            fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M15 18l-6-6 6-6"/></svg>
            Zurück
          </button>
          {stepIdx < STEPS.length - 1 ? (
            <button onClick={() => setStepIdx(stepIdx + 1)} style={{
              flex: 1,
              background: 'linear-gradient(135deg, #14391f, #1a4a2a)',
              border: 'none', borderRadius: 14, padding: '14px 18px',
              fontSize: 14, fontWeight: 700, color: 'white',
              cursor: 'pointer', fontFamily: 'inherit',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
              boxShadow: '0 6px 18px rgba(20,57,31,0.2)',
            }}>
              Weiter
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
            </button>
          ) : (
            <button disabled={doneCount < 5} style={{
              flex: 1,
              background: doneCount === 5 ? 'linear-gradient(135deg, #ff9a3c, #ff7849)' : 'rgba(20,57,31,0.1)',
              color: doneCount === 5 ? 'white' : '#6b7868',
              border: 'none', borderRadius: 14, padding: '14px 18px',
              fontSize: 14, fontWeight: 700,
              cursor: doneCount === 5 ? 'pointer' : 'not-allowed',
              fontFamily: 'inherit',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
              boxShadow: doneCount === 5 ? '0 6px 20px rgba(255,120,73,0.4)' : 'none',
            }}>
              Veröffentlichen
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z"/></svg>
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

Object.assign(window, {
  CreatePostingA, CreatePostingB, CreatePostingC,
  CPBasisBody, CPMediaBody, CPZutatenBody, CPStepsBody, CPSmartStepBody, CPKeywordsBody,
  CPSection, CPTabBar, CPPublishBar,
});
