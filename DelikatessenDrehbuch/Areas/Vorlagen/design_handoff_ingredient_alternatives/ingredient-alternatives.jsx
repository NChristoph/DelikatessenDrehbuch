/* Ingredient Alternatives — Avocado-Stil
   Restyle des "Alternativen fuer …" Pop-ups aus den Screenshots.
   cream bg · forest-green ink · Fraunces für Titel/Zahlen · weiche getönte Macro-Pills
   weißer Sheet · grüner Verlaufs-Header · Waldgrün-CTA statt Blau
*/

const { useState: altUseState } = React;

// ─── Sample data (aus den Screenshots) ────────────────────────
const ALT_ORIGINAL = {
  name: 'Carrot',
  amount: '2 Stk.',
  macros: { kcal: 41, protein: 0.9, carbs: 10.0, fat: 0.2 },
};

const ALT_META = { model: 'GPT-4o-mini', engine: 'ChatGPT', ms: 9799 };

const ALT_OPTIONS = [
  {
    name: 'Brussels sprouts',
    amount: '180g',
    match: 100,
    delta: { kcal: '+4', protein: '+4.5', carbs: '-1.8', fat: '+0.2' },
    desc: 'Brussels sprouts provide a crunchy texture and depth of flavor.',
    prep: 'Halve and sauté until caramelized.',
    pros: ['Rich in nutrients', 'Makes the dish more interesting', 'Good texture'],
    cons: ['Strong flavor might overshadow others', 'Requires more cooking care'],
    taste: 'A nutty flavor profile when caramelized.',
  },
  {
    name: 'Portobello mushroom',
    amount: '180g',
    match: 100,
    delta: { kcal: '-34', protein: '+2.2', carbs: '-11.0', fat: '+0.2' },
    desc: 'Mushrooms provide a meaty texture and earthy flavor.',
    prep: 'Slice and sauté until tender.',
    pros: ['Adds depth to the recipe', 'Low in calories and fats', 'Rich in antioxidants'],
    cons: ['Texture may differ from original', 'Flavor profile shift from sweet to savory'],
    taste: 'Earthy and umami flavor enhances the dish.',
  },
];

// Macro-Pill Töne — weiche getönte Pills statt knalliger Vollfarben
const ALT_TONES = {
  kcal:    { bg: 'rgba(20,57,31,0.07)',   fg: '#2a4a32', ring: 'rgba(20,57,31,0.10)' },
  protein: { bg: 'rgba(95,160,82,0.16)',  fg: '#3f7a36', ring: 'rgba(95,160,82,0.28)' },
  carbs:   { bg: 'rgba(60,108,158,0.13)', fg: '#3a6390', ring: 'rgba(60,108,158,0.24)' },
  fat:     { bg: 'rgba(245,185,66,0.20)', fg: '#9a7415', ring: 'rgba(245,185,66,0.40)' },
};

// ─── Icons ────────────────────────────────────────────────────
function AltIcon({ name, size = 16, color = 'currentColor' }) {
  const p = { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: color, strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' };
  if (name === 'close')   return <svg {...p}><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>;
  if (name === 'sparkle') return <svg width={size} height={size} viewBox="0 0 24 24" fill={color}><path d="M12 2l1.6 5.2L19 9l-5.4 1.8L12 16l-1.6-5.2L5 9l5.4-1.8L12 2z"/><path d="M19 14l.7 2.3L22 17l-2.3.7L19 20l-.7-2.3L16 17l2.3-.7L19 14z"/></svg>;
  if (name === 'check')   return <svg {...p}><polyline points="20 6 9 17 4 12"/></svg>;
  if (name === 'plus')    return <svg {...p}><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>;
  if (name === 'clock')   return <svg {...p}><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>;
  return null;
}

// ─── Macro pill (Original-Werte) ──────────────────────────────
function AltMacroPill({ tone, value, suffix }) {
  const t = ALT_TONES[tone];
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'baseline', gap: 3,
      background: t.bg,
      boxShadow: `inset 0 0 0 1px ${t.ring}`,
      borderRadius: 999,
      padding: '5px 11px',
      whiteSpace: 'nowrap',
    }}>
      <span style={{
        fontFamily: "'Fraunces', serif",
        fontSize: 13, fontWeight: 600, color: t.fg,
        letterSpacing: '-0.01em',
      }}>{value}</span>
      {suffix && <span style={{ fontSize: 10, fontWeight: 700, color: t.fg, opacity: 0.78, letterSpacing: '0.02em' }}>{suffix}</span>}
    </span>
  );
}

// ─── Delta pill (Alternative-Werte, +/-) ──────────────────────
function AltDeltaPill({ tone, value, suffix }) {
  const t = ALT_TONES[tone];
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'baseline', gap: 2,
      background: t.bg,
      boxShadow: `inset 0 0 0 1px ${t.ring}`,
      borderRadius: 999,
      padding: '4px 9px',
      whiteSpace: 'nowrap',
    }}>
      <span style={{
        fontFamily: "'Fraunces', serif",
        fontSize: 12, fontWeight: 600, color: t.fg,
        letterSpacing: '-0.01em',
      }}>{value}</span>
      <span style={{ fontSize: 9.5, fontWeight: 700, color: t.fg, opacity: 0.78 }}>{suffix}</span>
    </span>
  );
}

// ─── Original-Karte ───────────────────────────────────────────
function AltOriginalCard() {
  const m = ALT_ORIGINAL.macros;
  return (
    <div style={{
      background: '#faf6ec',
      border: '1px solid rgba(20,57,31,0.07)',
      borderRadius: 18,
      padding: '14px 16px',
    }}>
      <div style={{
        fontSize: 10.5, fontWeight: 700, letterSpacing: '0.16em',
        textTransform: 'uppercase', color: '#9aa295', marginBottom: 8,
      }}>Original</div>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 7, marginBottom: 12 }}>
        <span style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 21, fontWeight: 600, color: '#14391f', letterSpacing: '-0.02em',
        }}>{ALT_ORIGINAL.name}</span>
        <span style={{ fontSize: 13, color: '#6b7868', fontWeight: 500 }}>{ALT_ORIGINAL.amount}</span>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
        <AltMacroPill tone="kcal"    value={m.kcal}    suffix="kcal" />
        <AltMacroPill tone="protein" value={m.protein} suffix="P" />
        <AltMacroPill tone="carbs"   value={m.carbs.toFixed(1)} suffix="C" />
        <AltMacroPill tone="fat"     value={m.fat.toFixed(1)}   suffix="F" />
      </div>
    </div>
  );
}

// ─── Pro / Contra Spalte ──────────────────────────────────────
function AltProCon({ title, color, dotColor, items }) {
  return (
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        fontSize: 11, fontWeight: 700, letterSpacing: '0.10em',
        textTransform: 'uppercase', color, marginBottom: 8,
      }}>{title}</div>
      <ul style={{ margin: 0, padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 7 }}>
        {items.map((it, i) => (
          <li key={i} style={{ display: 'flex', gap: 8, fontSize: 13, lineHeight: 1.4, color: '#2a4a32' }}>
            <span style={{
              width: 5, height: 5, borderRadius: '50%', background: dotColor,
              flexShrink: 0, marginTop: 6,
            }} />
            <span style={{ textWrap: 'pretty' }}>{it}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

// ─── Alternative-Karte ────────────────────────────────────────
function AltOptionCard({ opt }) {
  const d = opt.delta;
  return (
    <div style={{
      position: 'relative',
      background: 'white',
      borderRadius: 22,
      padding: '16px 16px 16px 18px',
      boxShadow: '0 2px 10px rgba(20,57,31,0.06)',
      overflow: 'hidden',
    }}>
      {/* avocado accent strip */}
      <div style={{ position: 'absolute', left: 0, top: 14, bottom: 14, width: 3, borderRadius: 3, background: '#5fa052' }} />

      {/* Header row: name/amount + match + deltas */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
        <div style={{ minWidth: 0 }}>
          <div style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 20, fontWeight: 600, color: '#14391f',
            letterSpacing: '-0.02em', lineHeight: 1.12, textWrap: 'balance',
          }}>{opt.name}</div>
          <div style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 14, color: '#6b7868', fontWeight: 500, marginTop: 3,
          }}>{opt.amount}</div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 7, flexShrink: 0 }}>
          <span style={{
            display: 'inline-flex', alignItems: 'center', gap: 4,
            background: '#5fa052', color: 'white',
            borderRadius: 999, padding: '5px 11px 5px 9px',
            fontSize: 11.5, fontWeight: 700, letterSpacing: '0.01em',
            boxShadow: '0 2px 8px rgba(95,160,82,0.30)',
            whiteSpace: 'nowrap',
          }}>
            <AltIcon name="check" size={13} color="white" />
            {opt.match}% Match
          </span>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5, justifyContent: 'flex-end', maxWidth: 150 }}>
            <AltDeltaPill tone="kcal"    value={d.kcal}    suffix="kcal" />
            <AltDeltaPill tone="protein" value={d.protein} suffix="P" />
            <AltDeltaPill tone="carbs"   value={d.carbs}   suffix="C" />
            <AltDeltaPill tone="fat"     value={d.fat}     suffix="F" />
          </div>
        </div>
      </div>

      {/* Description */}
      <p style={{
        margin: '12px 0 0', fontSize: 14, lineHeight: 1.5,
        color: '#2a4a32', textWrap: 'pretty',
      }}>{opt.desc}</p>

      {/* Zubereitung */}
      <div style={{
        marginTop: 12,
        background: 'rgba(95,160,82,0.10)',
        border: '1px solid rgba(95,160,82,0.18)',
        borderRadius: 14,
        padding: '11px 13px',
        fontSize: 13.5, lineHeight: 1.45, color: '#2a4a32', textWrap: 'pretty',
      }}>
        <span style={{ fontWeight: 700, color: '#14391f' }}>Zubereitung:</span> {opt.prep}
      </div>

      {/* Vorteile / Nachteile */}
      <div style={{ display: 'flex', gap: 16, marginTop: 16 }}>
        <AltProCon title="Vorteile" color="#3f7a36" dotColor="#5fa052" items={opt.pros} />
        <AltProCon title="Nachteile" color="#c2502f" dotColor="#ff7849" items={opt.cons} />
      </div>

      {/* Geschmack */}
      <div style={{
        marginTop: 14,
        background: '#faf6ec',
        border: '1px solid rgba(20,57,31,0.06)',
        borderRadius: 14,
        padding: '11px 13px',
        fontSize: 13, lineHeight: 1.45, color: '#6b7868', textWrap: 'pretty',
      }}>
        <span style={{ fontWeight: 700, color: '#14391f' }}>Geschmack:</span> {opt.taste}
      </div>

      {/* Aktionen */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 16 }}>
        <button style={{
          width: '100%',
          background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
          color: 'white', border: 'none', borderRadius: 14,
          padding: '13px', fontSize: 14, fontWeight: 700,
          fontFamily: 'inherit', cursor: 'pointer',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 7,
          boxShadow: '0 6px 18px rgba(20,57,31,0.24)',
        }}>
          <AltIcon name="check" size={15} color="white" />
          Diese Alternative verwenden
        </button>
        <button style={{
          width: '100%',
          background: 'white', color: '#14391f',
          border: '1.5px solid rgba(20,57,31,0.18)', borderRadius: 14,
          padding: '12px', fontSize: 13.5, fontWeight: 600,
          fontFamily: 'inherit', cursor: 'pointer',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 7,
        }}>
          <AltIcon name="plus" size={14} color="#5fa052" />
          Zum Batch hinzufügen
        </button>
      </div>
    </div>
  );
}

// ─── Vollständiges Pop-up (Bottom-Sheet über Recipe Detail) ───
function AltModal({ open = true, onClose = () => {} }) {
  if (!open) return null;
  return (
    <div style={{ position: 'absolute', inset: 0, zIndex: 50, display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
      {/* Backdrop */}
      <div onClick={onClose} style={{
        position: 'absolute', inset: 0,
        background: 'rgba(10,28,17,0.42)',
        backdropFilter: 'blur(3px)', WebkitBackdropFilter: 'blur(3px)',
      }} />

      {/* Sheet */}
      <div style={{
        position: 'relative',
        background: '#f5efe1',
        borderTopLeftRadius: 28, borderTopRightRadius: 28,
        height: '92%',
        display: 'flex', flexDirection: 'column',
        overflow: 'hidden',
        boxShadow: '0 -20px 60px rgba(10,28,17,0.30)',
      }}>
        {/* Header — grüner Verlauf statt lila */}
        <div style={{
          background: 'linear-gradient(135deg, #1f4329, #0e2415)',
          padding: '16px 18px 18px',
          color: 'white',
          flexShrink: 0,
          position: 'relative',
        }}>
          {/* drag handle */}
          <div style={{
            width: 38, height: 4, borderRadius: 2,
            background: 'rgba(255,255,255,0.30)',
            margin: '0 auto 14px',
          }} />
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
            <div style={{ minWidth: 0 }}>
              <div style={{
                fontSize: 10.5, fontWeight: 700, letterSpacing: '0.14em',
                textTransform: 'uppercase', color: 'rgba(255,255,255,0.62)', marginBottom: 5,
              }}>Alternativen für</div>
              <h2 style={{
                fontFamily: "'Fraunces', serif",
                fontSize: 26, fontWeight: 600, letterSpacing: '-0.02em',
                margin: 0, lineHeight: 1.05,
              }}>{ALT_ORIGINAL.name}</h2>
              {/* AI meta */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginTop: 10 }}>
                <span style={{
                  display: 'inline-flex', alignItems: 'center', gap: 5,
                  background: 'rgba(255,255,255,0.12)',
                  border: '1px solid rgba(255,255,255,0.16)',
                  borderRadius: 999, padding: '4px 10px',
                  fontSize: 11, fontWeight: 600, color: 'rgba(255,255,255,0.92)',
                  whiteSpace: 'nowrap',
                }}>
                  <span style={{ color: '#f5b942', display: 'inline-flex' }}><AltIcon name="sparkle" size={12} color="#f5b942" /></span>
                  {ALT_META.engine} · {ALT_META.model}
                </span>
                <span style={{
                  display: 'inline-flex', alignItems: 'center', gap: 4,
                  fontSize: 11, fontWeight: 600, color: 'rgba(255,255,255,0.55)',
                }}>
                  <AltIcon name="clock" size={11} color="rgba(255,255,255,0.55)" />
                  {(ALT_META.ms / 1000).toFixed(1)}s
                </span>
              </div>
            </div>
            <button onClick={onClose} style={{
              width: 34, height: 34, borderRadius: '50%',
              background: 'rgba(255,255,255,0.14)',
              border: '1px solid rgba(255,255,255,0.18)',
              color: 'white', cursor: 'pointer', flexShrink: 0,
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            }}>
              <AltIcon name="close" size={16} color="white" />
            </button>
          </div>
        </div>

        {/* Scroll-Inhalt */}
        <div className="mp-feed" style={{ flex: 1, overflowY: 'auto', padding: '14px 16px 100px', display: 'flex', flexDirection: 'column', gap: 12 }}>
          <AltOriginalCard />
          {ALT_OPTIONS.map((opt, i) => <AltOptionCard key={i} opt={opt} />)}
        </div>

        {/* Footer — Abbrechen */}
        <div style={{
          flexShrink: 0,
          padding: '12px 16px 22px',
          background: 'linear-gradient(180deg, rgba(245,239,225,0) 0%, #f5efe1 36%)',
          marginTop: -28, pointerEvents: 'none',
        }}>
          <button onClick={onClose} style={{
            pointerEvents: 'auto',
            width: '100%',
            background: 'white',
            border: '1px solid rgba(20,57,31,0.12)',
            borderRadius: 16, padding: '13px',
            fontSize: 14, fontWeight: 600, color: '#6b7868',
            fontFamily: 'inherit', cursor: 'pointer',
            boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
          }}>Abbrechen</button>
        </div>
      </div>
    </div>
  );
}

// Standalone-Variante: nur der Scroll-Inhalt (für Detail-Artboards)
function AltScrollContent() {
  return (
    <div style={{ background: '#f5efe1', padding: '16px 16px 24px', display: 'flex', flexDirection: 'column', gap: 12 }}>
      {ALT_OPTIONS.map((opt, i) => <AltOptionCard key={i} opt={opt} />)}
    </div>
  );
}

window.AltModal = AltModal;
window.AltOptionCard = AltOptionCard;
window.AltScrollContent = AltScrollContent;
window.ALT_OPTIONS = ALT_OPTIONS;
