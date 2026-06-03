/* Recipe Detail — Avocado-Stil
   Drei Tabs: Ingredients · Steps · Nutrition
   Aus den Screenshots übernommen, neu eingekleidet im Marketplace-Look:
   cream bg · forest green ink · Fraunces serif für Titel/Zahlen · weiße Cards
*/

const { useState: rdUseState } = React;

// ─── Sample data ──────────────────────────────────────────────
const RD_RECIPE = {
  title: 'Schweinebraten mit dunkler Biersauce und Ofengemüse',
  hero: 'https://images.unsplash.com/photo-1544025162-d76694265947?w=1200&q=80',
  servings: 4,
  duration: 120,
  kcal: 545,
  category: 'Hauptgericht',
  pref: 'Fleisch',
  author: { name: 'Lina Becker', handle: '@linakocht' },
};

const RD_INGREDIENT_GROUPS = [
  {
    label: 'Fleisch',
    items: [
      { name: 'Pork Shoulder', amount: 1000, unit: 'g',    emoji: '🥩' },
    ],
  },
  {
    label: 'Gemüse',
    items: [
      { name: 'Leek',     amount: 1,    unit: 'Stk.', emoji: '🧅' },
      { name: 'Celeriac', amount: 0.25, unit: 'Stk.', emoji: '🥬' },
      { name: 'Garlic',   amount: 2,    unit: 'Stk.', emoji: '🧄' },
      { name: 'Onion',    amount: 2,    unit: 'Stk.', emoji: '🧅' },
      { name: 'Carrot',   amount: 3,    unit: 'Stk.', emoji: '🥕' },
    ],
  },
  {
    label: 'Sonstiges',
    items: [
      { name: 'Beef broth',     amount: 500, unit: 'ml',   emoji: '🍲' },
      { name: 'Dunkles Bier',   amount: 330, unit: 'ml',   emoji: '🍺' },
      { name: 'Tomatenmark',    amount: 2,   unit: 'EL',   emoji: '🥫' },
      { name: 'Honig',          amount: 1,   unit: 'TL',   emoji: '🍯' },
    ],
  },
];

const RD_STEPS = [
  { n: 1, body: 'Heize den Backofen auf 180°C Ober-/Unterhitze vor.' },
  { n: 2, body: 'Schneide das Schweinefleisch rautenförmig ein.' },
  { n: 3, body: 'Schäle Knoblauch, Karotten, Sellerie und Zwiebel.' },
  { n: 4, body: 'Schneide den Lauch in grobe Scheiben.' },
  { n: 5, body: 'Schneide Zwiebel, Karotten und Sellerie in grobe Würfel.' },
  { n: 6, body: 'Brate das Fleisch in einem Bräter rundherum scharf an.' },
  { n: 7, body: 'Lösche mit dunklem Bier und Brühe ab und gib die Gemüse dazu.' },
  { n: 8, body: 'Schiebe den Bräter für 90 Min in den Ofen, immer wieder begießen.' },
];

const RD_NUTRITION = {
  total: 2181,
  macros: [
    { label: 'Protein', value: '193' },
    { label: 'Carbs',   value: '18'  },
    { label: 'Fat',     value: '151' },
    { label: 'Sugar',   value: '1.3' },
  ],
};

// ─── Building blocks ──────────────────────────────────────────

function RDStatPill({ icon, value, unit, color = '#14391f' }) {
  return (
    <div style={{
      display: 'inline-flex', alignItems: 'center', gap: 8,
      background: 'white',
      borderRadius: 999,
      padding: '8px 14px',
      boxShadow: '0 2px 8px rgba(20,57,31,0.06)',
      border: '1px solid rgba(20,57,31,0.06)',
    }}>
      <span style={{ display: 'inline-flex', color }}>{icon}</span>
      <span style={{
        fontFamily: "'Fraunces', serif",
        fontSize: 15, fontWeight: 600,
        color: '#14391f',
        letterSpacing: '-0.01em',
      }}>{value}</span>
      {unit && <span style={{ fontSize: 12, color: '#6b7868', fontWeight: 500 }}>{unit}</span>}
    </div>
  );
}

function RDIcon({ name, size = 16, color = 'currentColor' }) {
  const props = { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: color, strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' };
  if (name === 'people') return <svg {...props}><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="8.5" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>;
  if (name === 'clock')  return <svg {...props}><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>;
  if (name === 'flame')  return <svg {...props}><path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"/></svg>;
  if (name === 'back')   return <svg {...props}><path d="M15 18l-6-6 6-6"/></svg>;
  if (name === 'edit')   return <svg {...props}><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>;
  if (name === 'swap')   return <svg {...props}><polyline points="17 1 21 5 17 9"/><path d="M3 11V9a4 4 0 0 1 4-4h14"/><polyline points="7 23 3 19 7 15"/><path d="M21 13v2a4 4 0 0 1-4 4H3"/></svg>;
  if (name === 'chev')   return <svg {...props}><polyline points="6 9 12 15 18 9"/></svg>;
  if (name === 'heart')  return <svg {...props}><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>;
  if (name === 'share')  return <svg {...props}><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><line x1="8.59" y1="13.51" x2="15.42" y2="17.49"/><line x1="15.41" y1="6.51" x2="8.59" y2="10.49"/></svg>;
  return null;
}

// ─── Header (image + back/lang) ──────────────────────────────
function RDHeader() {
  return (
    <div style={{
      position: 'relative',
      height: 320,
      flexShrink: 0,
      background: `linear-gradient(180deg, rgba(0,0,0,0.18) 0%, rgba(0,0,0,0) 35%, rgba(0,0,0,0) 60%, rgba(245,239,225,1) 100%), url(${RD_RECIPE.hero}) center/cover`,
    }}>
      {/* Top controls */}
      <div style={{
        position: 'absolute', top: 12, left: 14, right: 14,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      }}>
        <button style={{
          width: 38, height: 38, borderRadius: '50%',
          background: 'rgba(255,255,255,0.92)',
          border: 'none', cursor: 'pointer',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          boxShadow: '0 2px 10px rgba(0,0,0,0.15)',
          backdropFilter: 'blur(6px)',
        }}>
          <RDIcon name="back" size={16} color="#14391f" />
        </button>
        <div style={{ display: 'flex', gap: 8 }}>
          <button style={{
            width: 38, height: 38, borderRadius: '50%',
            background: 'rgba(255,255,255,0.92)',
            border: 'none', cursor: 'pointer',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            boxShadow: '0 2px 10px rgba(0,0,0,0.15)',
          }}>
            <RDIcon name="heart" size={16} color="#14391f" />
          </button>
          <button style={{
            background: 'rgba(255,255,255,0.92)',
            border: 'none', borderRadius: 999,
            padding: '0 12px', height: 38,
            fontSize: 12, fontWeight: 700, color: '#14391f',
            cursor: 'pointer', fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', gap: 4,
            boxShadow: '0 2px 10px rgba(0,0,0,0.15)',
          }}>
            DE <RDIcon name="chev" size={11} color="#14391f" />
          </button>
        </div>
      </div>

      {/* Author tag */}
      <div style={{
        position: 'absolute', left: 18, bottom: 60,
        display: 'inline-flex', alignItems: 'center', gap: 8,
        background: 'rgba(20,57,31,0.55)',
        backdropFilter: 'blur(8px)',
        WebkitBackdropFilter: 'blur(8px)',
        padding: '5px 5px 5px 5px',
        borderRadius: 999,
        color: 'white',
        fontSize: 11, fontWeight: 600,
      }}>
        <span style={{
          width: 22, height: 22, borderRadius: '50%',
          background: 'linear-gradient(135deg, #ff9a3c, #ff7849)',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 11, fontWeight: 700,
        }}>L</span>
        <span style={{ paddingRight: 10 }}>{RD_RECIPE.author.handle}</span>
      </div>
    </div>
  );
}

// ─── Title block + stats + edit CTA ─────────────────────────
function RDTitleBlock() {
  return (
    <div style={{
      position: 'relative',
      marginTop: -28,
      background: '#f5efe1',
      borderTopLeftRadius: 28,
      borderTopRightRadius: 28,
      padding: '14px 18px 0',
    }}>
      {/* drag handle */}
      <div style={{
        width: 38, height: 4,
        borderRadius: 2,
        background: 'rgba(20,57,31,0.18)',
        margin: '0 auto 14px',
      }} />

      {/* category eyebrow */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6,
        marginBottom: 8,
        fontSize: 11, fontWeight: 700,
        color: '#6b7868',
        letterSpacing: '0.08em',
        textTransform: 'uppercase',
      }}>
        <span style={{
          width: 6, height: 6, borderRadius: '50%',
          background: '#5fa052',
        }} />
        {RD_RECIPE.category} · {RD_RECIPE.pref}
      </div>

      <h1 style={{
        fontFamily: "'Fraunces', serif",
        fontSize: 26, fontWeight: 600,
        letterSpacing: '-0.02em',
        color: '#14391f',
        margin: 0, lineHeight: 1.1,
        textWrap: 'pretty',
      }}>{RD_RECIPE.title}</h1>

      {/* stats row */}
      <div style={{ display: 'flex', gap: 8, marginTop: 14 }}>
        <RDStatPill
          icon={<RDIcon name="people" size={15} />}
          value={RD_RECIPE.servings}
        />
        <RDStatPill
          icon={<RDIcon name="clock" size={15} color="#5fa052" />}
          value={RD_RECIPE.duration}
          unit="Min."
        />
        <RDStatPill
          icon={<RDIcon name="flame" size={15} color="#ff7849" />}
          value={RD_RECIPE.kcal}
          unit="kcal"
        />
      </div>

      {/* edit feed posting */}
      <button style={{
        marginTop: 14,
        background: 'transparent',
        border: '1.5px solid rgba(20,57,31,0.18)',
        borderRadius: 999,
        padding: '9px 14px',
        fontSize: 13, fontWeight: 600,
        color: '#14391f',
        fontFamily: 'inherit',
        cursor: 'pointer',
        display: 'inline-flex', alignItems: 'center', gap: 7,
      }}>
        <RDIcon name="edit" size={13} color="#14391f" />
        Feed-Posting bearbeiten
      </button>
    </div>
  );
}

// ─── Segmented Tab Bar (matches Create-Posting tabs) ─────────
function RDTabBar({ active, onChange }) {
  const tabs = [
    { id: 'ingredients', label: 'Ingredients' },
    { id: 'steps',       label: 'Steps' },
    { id: 'nutrition',   label: 'Nutrition' },
  ];
  return (
    <div style={{ padding: '14px 18px 6px' }}>
      <div style={{
        display: 'flex',
        background: 'rgba(20,57,31,0.06)',
        borderRadius: 999,
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
                padding: '10px 6px',
                borderRadius: 999,
                fontSize: 13, fontWeight: 600,
                fontFamily: 'inherit',
                cursor: 'pointer',
                transition: 'all .2s',
                background: isActive ? 'white' : 'transparent',
                color:      isActive ? '#14391f' : '#6b7868',
                border: 'none',
                boxShadow: isActive ? '0 4px 12px rgba(20,57,31,0.10)' : 'none',
              }}>
              {t.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}

// ─── Servings stepper ─────────────────────────────────────────
function RDServingsStepper({ value, setValue }) {
  return (
    <div style={{
      display: 'flex', justifyContent: 'center',
      padding: '10px 18px 14px',
    }}>
      <div style={{
        display: 'inline-flex', alignItems: 'center',
        background: 'white',
        borderRadius: 999,
        padding: '4px 6px 4px 16px',
        boxShadow: '0 2px 8px rgba(20,57,31,0.06)',
        gap: 12,
      }}>
        <span style={{ fontSize: 13, color: '#6b7868', fontWeight: 500 }}>Servings for</span>
        <button onClick={() => setValue(Math.max(1, value - 1))} style={{
          width: 26, height: 26, borderRadius: '50%',
          border: '1px solid rgba(20,57,31,0.12)',
          background: 'white', color: '#14391f',
          fontSize: 14, fontWeight: 700, cursor: 'pointer',
          fontFamily: 'inherit',
        }}>−</button>
        <span style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 18, fontWeight: 700, color: '#14391f',
          minWidth: 18, textAlign: 'center',
        }}>{value}</span>
        <button onClick={() => setValue(value + 1)} style={{
          width: 26, height: 26, borderRadius: '50%',
          border: 'none',
          background: '#14391f', color: 'white',
          fontSize: 14, fontWeight: 700, cursor: 'pointer',
          fontFamily: 'inherit',
        }}>+</button>
      </div>
    </div>
  );
}

// ─── Ingredient Card ──────────────────────────────────────────
function RDIngredientCard({ item, scale = 1, onSwap }) {
  const amount = +(item.amount * scale).toFixed(2);
  const display = Number.isInteger(amount) ? amount : amount.toString().replace(/\.?0+$/, '');
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 12,
      background: 'white',
      borderRadius: 18,
      padding: '12px 14px',
      boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
    }}>
      <div style={{
        width: 38, height: 38, borderRadius: '50%',
        background: '#ede7d4',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 18, flexShrink: 0,
      }}>{item.emoji}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontSize: 14, fontWeight: 700,
          color: '#14391f',
        }}>{item.name}</div>
      </div>
      <div style={{ display: 'inline-flex', alignItems: 'baseline', gap: 3 }}>
        <span style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 16, fontWeight: 600,
          color: '#14391f',
        }}>{display}</span>
        <span style={{ fontSize: 11, color: '#6b7868', fontWeight: 500 }}>{item.unit}</span>
      </div>
      <button onClick={() => onSwap && onSwap(item)} style={{
        width: 30, height: 30, borderRadius: '50%',
        background: 'transparent',
        border: '1.5px solid rgba(20,57,31,0.18)',
        cursor: 'pointer', flexShrink: 0,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        color: '#14391f',
      }}>
        <RDIcon name="swap" size={13} color="#14391f" />
      </button>
    </div>
  );
}

// ─── Body: Ingredients ────────────────────────────────────────
function RDIngredientsBody({ servings, setServings, onSwap }) {
  const scale = servings / RD_RECIPE.servings;
  return (
    <>
      <RDServingsStepper value={servings} setValue={setServings} />
      <div style={{ padding: '0 18px 100px', display: 'flex', flexDirection: 'column', gap: 14 }}>
        {RD_INGREDIENT_GROUPS.map(g => (
          <div key={g.label}>
            <div style={{
              fontSize: 11, fontWeight: 700,
              color: '#9aa295',
              letterSpacing: '0.14em',
              textTransform: 'uppercase',
              padding: '0 4px 8px',
            }}>{g.label}</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {g.items.map(item => <RDIngredientCard key={item.name} item={item} scale={scale} onSwap={onSwap} />)}
            </div>
          </div>
        ))}
      </div>
    </>
  );
}

// ─── Body: Steps ──────────────────────────────────────────────
function RDStepsBody() {
  return (
    <div style={{ padding: '14px 18px 100px', display: 'flex', flexDirection: 'column', gap: 10 }}>
      {RD_STEPS.map(s => (
        <div key={s.n} style={{
          background: 'white',
          borderRadius: 18,
          padding: '14px 16px 16px',
          boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
        }}>
          <span style={{
            display: 'inline-block',
            background: 'rgba(95,160,82,0.14)',
            color: '#14391f',
            fontSize: 11, fontWeight: 700,
            padding: '4px 11px', borderRadius: 999,
            fontFamily: "'Fraunces', serif",
            letterSpacing: '-0.01em',
          }}>Step {s.n}</span>
          <div style={{
            marginTop: 10,
            fontSize: 14.5, lineHeight: 1.5,
            color: '#14391f',
            textWrap: 'pretty',
          }}>{s.body}</div>
        </div>
      ))}
    </div>
  );
}

// ─── Body: Nutrition ──────────────────────────────────────────
function RDNutritionBody() {
  return (
    <div style={{ padding: '14px 18px 100px' }}>
      {/* Big total */}
      <div style={{
        background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
        borderRadius: 22,
        padding: '24px 18px 20px',
        textAlign: 'center',
        color: 'white',
        boxShadow: '0 12px 30px rgba(20,57,31,0.18)',
      }}>
        <div style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 56, fontWeight: 700,
          letterSpacing: '-0.03em',
          lineHeight: 1,
        }}>{RD_NUTRITION.total}</div>
        <div style={{
          fontSize: 11, fontWeight: 700,
          letterSpacing: '0.16em',
          textTransform: 'uppercase',
          color: 'rgba(255,255,255,0.75)',
          marginTop: 8,
        }}>Gesamt kcal</div>
      </div>

      {/* Macros 2x2 */}
      <div style={{
        marginTop: 12,
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 10,
      }}>
        {RD_NUTRITION.macros.map(m => (
          <div key={m.label} style={{
            background: 'white',
            borderRadius: 18,
            padding: '20px 14px 16px',
            textAlign: 'center',
            boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
          }}>
            <div style={{
              fontFamily: "'Fraunces', serif",
              fontSize: 38, fontWeight: 700,
              color: '#14391f',
              letterSpacing: '-0.02em',
              lineHeight: 1,
            }}>{m.value}</div>
            <div style={{
              fontSize: 10, fontWeight: 700,
              letterSpacing: '0.16em',
              textTransform: 'uppercase',
              color: '#9aa295',
              marginTop: 8,
            }}>{m.label}</div>
          </div>
        ))}
      </div>

      <div style={{
        marginTop: 18,
        textAlign: 'center',
        fontSize: 11, color: '#9aa295',
        fontStyle: 'italic',
        padding: '0 12px',
      }}>
        Nährwerte sind Schätzungen und können variieren.
      </div>
    </div>
  );
}

// ─── Bottom action bar ────────────────────────────────────────
function RDBottomBar() {
  return (
    <div style={{
      position: 'absolute',
      bottom: 0, left: 0, right: 0,
      padding: '12px 16px 28px',
      background: 'linear-gradient(180deg, rgba(245,239,225,0) 0%, rgba(245,239,225,0.95) 30%, #f5efe1 100%)',
      backdropFilter: 'blur(10px)',
      WebkitBackdropFilter: 'blur(10px)',
      zIndex: 4,
      display: 'flex', gap: 8,
    }}>
      <button style={{
        flex: 1,
        background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
        color: 'white', border: 'none',
        borderRadius: 16,
        padding: '14px',
        fontSize: 14, fontWeight: 700,
        fontFamily: 'inherit',
        cursor: 'pointer',
        boxShadow: '0 6px 20px rgba(20,57,31,0.28)',
      }}>Jetzt kochen</button>
      <button style={{
        background: 'white',
        border: '1px solid rgba(20,57,31,0.12)',
        borderRadius: 16,
        padding: '0 16px',
        fontSize: 13, fontWeight: 600,
        color: '#14391f',
        fontFamily: 'inherit',
        cursor: 'pointer',
        display: 'inline-flex', alignItems: 'center', gap: 6,
      }}>
        <RDIcon name="share" size={14} color="#14391f" />
      </button>
    </div>
  );
}

// ─── Screen ───────────────────────────────────────────────────
function RDScreen({ initialTab = 'ingredients' }) {
  const [tab, setTab] = rdUseState(initialTab);
  const [servings, setServings] = rdUseState(RD_RECIPE.servings);
  const [swapOpen, setSwapOpen] = rdUseState(false);
  const Modal = (typeof window !== 'undefined') ? window.AltModal : null;

  return (
    <div className="mp-screen" style={{ background: '#f5efe1', position: 'relative' }}>
      <div style={{ flex: 1, overflowY: 'auto' }} className="mp-feed">
        <RDHeader />
        <RDTitleBlock />
        <RDTabBar active={tab} onChange={setTab} />
        {tab === 'ingredients' && <RDIngredientsBody servings={servings} setServings={setServings} onSwap={() => setSwapOpen(true)} />}
        {tab === 'steps' && <RDStepsBody />}
        {tab === 'nutrition' && <RDNutritionBody />}
      </div>
      <RDBottomBar />
      {Modal && <Modal open={swapOpen} onClose={() => setSwapOpen(false)} />}
    </div>
  );
}

window.RDScreen = RDScreen;
