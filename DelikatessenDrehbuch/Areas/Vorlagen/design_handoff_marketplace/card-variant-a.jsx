/* Marketplace Card Components — 3 variants */

// Sample data — Pläne
const PLANS = [
  {
    id: 'p1',
    type: 'meal',
    title: 'High-Protein Cut',
    creator: 'Avocado',
    creatorAvatar: '🥑',
    badge: 'Premium',
    duration: '14 Tage',
    recipes: 42,
    workouts: null,
    kcalDay: 1850,
    rating: 4.8,
    sold: 214,
    planning: 87,
    priceWLD: 2.4,
    priceUSDC: 12,
    tags: ['High-Protein', 'Cut', 'Low-Carb'],
    macros: { protein: 180, carbs: 95, fat: 65 },
    color: '#14391f',
    dishes: [
      { name: 'Rinderfilet mit Spargel', img: 'https://images.unsplash.com/photo-1544025162-d76694265947?w=400&q=80', kcal: 520, time: '25 Min' },
      { name: 'Lachs Bowl', img: 'https://images.unsplash.com/photo-1467003909585-2f8a72700288?w=400&q=80', kcal: 480, time: '20 Min' },
      { name: 'Hähnchen Curry', img: 'https://images.unsplash.com/photo-1565557623262-b51c2513a641?w=400&q=80', kcal: 510, time: '30 Min' },
      { name: 'Eier-Pfanne', img: 'https://images.unsplash.com/photo-1525351484163-7529414344d8?w=400&q=80', kcal: 380, time: '12 Min' },
      { name: 'Tuna Steak', img: 'https://images.unsplash.com/photo-1535473895227-bdecb20fb157?w=400&q=80', kcal: 460, time: '18 Min' },
      { name: 'Protein Bowl', img: 'https://images.unsplash.com/photo-1490645935967-10de6ba17061?w=400&q=80', kcal: 540, time: '15 Min' },
    ],
  },
  {
    id: 'p2',
    type: 'workout',
    title: 'Push Pull Legs · 6 Wochen',
    creator: 'CoachMax',
    creatorAvatar: '💪',
    badge: null,
    duration: '6 Wochen',
    recipes: null,
    workouts: 24,
    kcalDay: null,
    rating: 4.9,
    sold: 512,
    planning: 234,
    priceWLD: 1.8,
    priceUSDC: 9,
    tags: ['Hypertrophie', 'Gym', 'Fortgeschritten'],
    macros: null,
    color: '#1a2e3a',
    dishes: [
      { name: 'Bench Press', img: 'https://images.unsplash.com/photo-1581009146145-b5ef050c2e1e?w=400&q=80', kcal: '4×8', time: 'Push' },
      { name: 'Deadlift', img: 'https://images.unsplash.com/photo-1517836357463-d25dfeac3438?w=400&q=80', kcal: '5×5', time: 'Pull' },
      { name: 'Squat', img: 'https://images.unsplash.com/photo-1574680096145-d05b474e2155?w=400&q=80', kcal: '4×10', time: 'Legs' },
      { name: 'Pull Ups', img: 'https://images.unsplash.com/photo-1599058917765-a780eda07a3e?w=400&q=80', kcal: '4×max', time: 'Pull' },
      { name: 'Shoulder Press', img: 'https://images.unsplash.com/photo-1532029837206-abbe2b7620e3?w=400&q=80', kcal: '4×8', time: 'Push' },
    ],
  },
  {
    id: 'p3',
    type: 'combo',
    title: 'Keto Kombi · Train & Eat',
    creator: 'Bea Health',
    creatorAvatar: '🌿',
    badge: 'Premium',
    duration: '21 Tage',
    recipes: 63,
    workouts: 12,
    kcalDay: 2100,
    rating: 4.7,
    sold: 89,
    planning: 41,
    priceWLD: 3.2,
    priceUSDC: 16,
    tags: ['Keto', 'Vegan-friendly', 'Beginner'],
    macros: { protein: 145, carbs: 35, fat: 165 },
    color: '#2a1f3a',
    dishes: [
      { name: 'Avocado Bowl', img: 'https://images.unsplash.com/photo-1512621776951-a57141f2eefd?w=400&q=80', kcal: 620, time: '15 Min' },
      { name: 'Steak Salad', img: 'https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=400&q=80', kcal: 580, time: '25 Min' },
      { name: 'Käse-Omelett', img: 'https://images.unsplash.com/photo-1607532941433-304659e8198a?w=400&q=80', kcal: 490, time: '10 Min' },
      { name: 'Buddha Bowl', img: 'https://images.unsplash.com/photo-1543339308-43e59d6b73a6?w=400&q=80', kcal: 550, time: '20 Min' },
    ],
  },
];

// ─── Helpers ──────────────────────────────────────────────────
function Stars({ rating }) {
  return (
    <div style={{ display: 'inline-flex', alignItems: 'center', gap: 3 }}>
      <svg width="12" height="12" viewBox="0 0 24 24" fill="#f5b942">
        <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>
      </svg>
      <span style={{ fontSize: 11, fontWeight: 600, color: '#1a2e20' }}>{rating}</span>
    </div>
  );
}

function MacroBar({ label, value, max, color }) {
  const pct = Math.min(100, (value / max) * 100);
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 11 }}>
      <span style={{ width: 50, color: 'rgba(255,255,255,0.7)' }}>{label}</span>
      <div style={{ flex: 1, height: 6, background: 'rgba(255,255,255,0.12)', borderRadius: 3, overflow: 'hidden' }}>
        <div style={{ width: `${pct}%`, height: '100%', background: color, borderRadius: 3 }} />
      </div>
      <span style={{ width: 36, textAlign: 'right', color: 'white', fontWeight: 600 }}>{value}g</span>
    </div>
  );
}

function CurrencyToggle({ currency, onChange, size = 'sm' }) {
  const small = size === 'sm';
  return (
    <button
      onClick={() => onChange(currency === 'WLD' ? 'USDC' : 'WLD')}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
        background: 'rgba(20,57,31,0.06)',
        border: '1px solid rgba(20,57,31,0.1)',
        borderRadius: 999,
        padding: small ? '3px 4px' : '4px 5px',
        fontSize: small ? 10 : 11,
        fontWeight: 600,
        cursor: 'pointer',
        color: '#14391f',
      }}>
      <span style={{
        background: currency === 'WLD' ? '#14391f' : 'transparent',
        color: currency === 'WLD' ? 'white' : '#14391f',
        padding: '3px 8px',
        borderRadius: 999,
        transition: 'all .2s',
      }}>WLD</span>
      <span style={{
        background: currency === 'USDC' ? '#14391f' : 'transparent',
        color: currency === 'USDC' ? 'white' : '#14391f',
        padding: '3px 8px',
        borderRadius: 999,
        transition: 'all .2s',
      }}>USDC</span>
    </button>
  );
}

// ─── VARIANT A: Immersive Magazine Card ───────────────────────
function CardVariantA({ plan }) {
  const [currency, setCurrency] = React.useState('WLD');
  const [carouselIdx, setCarouselIdx] = React.useState(0);
  const [showMacros, setShowMacros] = React.useState(false);
  const trackRef = React.useRef(null);

  const dishes = plan.dishes;
  const price = currency === 'WLD' ? plan.priceWLD : plan.priceUSDC;

  const scrollTo = (idx) => {
    setCarouselIdx(idx);
    if (trackRef.current) {
      const card = trackRef.current.children[idx];
      if (card) card.scrollIntoView({ behavior: 'smooth', inline: 'start', block: 'nearest' });
    }
  };

  const handleScroll = () => {
    if (!trackRef.current) return;
    const scrollLeft = trackRef.current.scrollLeft;
    const itemWidth = 92;
    const idx = Math.round(scrollLeft / itemWidth);
    setCarouselIdx(idx);
  };

  return (
    <article style={{
      background: 'white',
      borderRadius: 22,
      overflow: 'hidden',
      boxShadow: '0 8px 24px rgba(20, 57, 31, 0.08)',
      marginBottom: 20,
    }}>
      {/* Hero */}
      <div style={{
        position: 'relative',
        aspectRatio: '4/3',
        background: `url(${dishes[carouselIdx].img}) center/cover`,
        overflow: 'hidden',
      }}>
        {/* Gradient overlay */}
        <div style={{
          position: 'absolute', inset: 0,
          background: 'linear-gradient(180deg, rgba(0,0,0,0.45) 0%, transparent 35%, transparent 50%, rgba(0,0,0,0.7) 100%)',
        }} />

        {/* Top row */}
        <div style={{ position: 'absolute', top: 12, left: 12, right: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          {plan.badge && (
            <span style={{
              background: 'rgba(255,255,255,0.95)',
              color: '#8a6418',
              padding: '4px 10px',
              borderRadius: 999,
              fontSize: 10,
              fontWeight: 700,
              letterSpacing: '0.04em',
              textTransform: 'uppercase',
              display: 'inline-flex', alignItems: 'center', gap: 4,
            }}>
              <svg width="10" height="10" viewBox="0 0 24 24" fill="#c8a45c">
                <path d="M12 2l2.4 7.4H22l-6.2 4.5 2.4 7.4L12 16.8l-6.2 4.5 2.4-7.4L2 9.4h7.6z"/>
              </svg>
              {plan.badge}
            </span>
          )}
          <div style={{ marginLeft: 'auto', display: 'flex', gap: 6 }}>
            <button style={{ width: 32, height: 32, borderRadius: '50%', background: 'rgba(255,255,255,0.95)', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#14391f" strokeWidth="2">
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
              </svg>
            </button>
          </div>
        </div>

        {/* Bottom: Title */}
        <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, padding: 16, color: 'white' }}>
          <div style={{ display: 'flex', gap: 6, marginBottom: 8 }}>
            {plan.tags.slice(0, 3).map(t => (
              <span key={t} style={{
                background: 'rgba(255,255,255,0.16)',
                backdropFilter: 'blur(8px)',
                WebkitBackdropFilter: 'blur(8px)',
                color: 'white',
                padding: '3px 8px',
                borderRadius: 999,
                fontSize: 10,
                fontWeight: 500,
                border: '1px solid rgba(255,255,255,0.22)',
              }}>{t}</span>
            ))}
          </div>
          <h3 style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 22,
            fontWeight: 600,
            letterSpacing: '-0.02em',
            margin: 0,
            lineHeight: 1.1,
            textShadow: '0 2px 12px rgba(0,0,0,0.3)',
          }}>{plan.title}</h3>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 8, fontSize: 12 }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, opacity: 0.95 }}>
              <span style={{ fontSize: 14 }}>{plan.creatorAvatar}</span>
              <span style={{ fontWeight: 600 }}>{plan.creator}</span>
            </span>
            <span style={{ opacity: 0.5 }}>·</span>
            <span style={{ opacity: 0.85 }}>{plan.duration}</span>
          </div>
        </div>
      </div>

      {/* Stats strip */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        background: '#faf6ec',
        borderBottom: '1px solid rgba(20,57,31,0.08)',
      }}>
        {plan.recipes != null && (
          <div style={{ padding: '12px 8px', textAlign: 'center', borderRight: '1px solid rgba(20,57,31,0.08)' }}>
            <div style={{ fontFamily: "'Fraunces', serif", fontSize: 18, fontWeight: 600, color: '#14391f', lineHeight: 1 }}>{plan.recipes}</div>
            <div style={{ fontSize: 10, color: '#6b7868', marginTop: 3, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Rezepte</div>
          </div>
        )}
        {plan.workouts != null && (
          <div style={{ padding: '12px 8px', textAlign: 'center', borderRight: '1px solid rgba(20,57,31,0.08)' }}>
            <div style={{ fontFamily: "'Fraunces', serif", fontSize: 18, fontWeight: 600, color: '#14391f', lineHeight: 1 }}>{plan.workouts}</div>
            <div style={{ fontSize: 10, color: '#6b7868', marginTop: 3, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Workouts</div>
          </div>
        )}
        {plan.kcalDay != null && (
          <div style={{ padding: '12px 8px', textAlign: 'center', borderRight: '1px solid rgba(20,57,31,0.08)' }}>
            <div style={{ fontFamily: "'Fraunces', serif", fontSize: 18, fontWeight: 600, color: '#14391f', lineHeight: 1 }}>{(plan.kcalDay/1000).toFixed(1)}k</div>
            <div style={{ fontSize: 10, color: '#6b7868', marginTop: 3, textTransform: 'uppercase', letterSpacing: '0.04em' }}>kcal/Tag</div>
          </div>
        )}
        <div style={{ padding: '12px 8px', textAlign: 'center' }}>
          <div style={{ fontFamily: "'Fraunces', serif", fontSize: 18, fontWeight: 600, color: '#14391f', lineHeight: 1, display: 'inline-flex', alignItems: 'center', gap: 3 }}>
            <svg width="12" height="12" viewBox="0 0 24 24" fill="#f5b942">
              <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>
            </svg>
            {plan.rating}
          </div>
          <div style={{ fontSize: 10, color: '#6b7868', marginTop: 3, textTransform: 'uppercase', letterSpacing: '0.04em' }}>{plan.sold} verk.</div>
        </div>
      </div>

      {/* Carousel */}
      <div style={{ padding: '14px 0 4px' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 16px 10px' }}>
          <span style={{ fontSize: 11, fontWeight: 600, color: '#14391f', textTransform: 'uppercase', letterSpacing: '0.06em' }}>
            {plan.type === 'workout' ? 'Trainings im Plan' : 'Gerichte im Plan'}
          </span>
          <span style={{ fontSize: 10, color: '#6b7868' }}>{carouselIdx + 1} / {dishes.length}</span>
        </div>
        <div
          ref={trackRef}
          onScroll={handleScroll}
          style={{
            display: 'flex',
            gap: 8,
            overflowX: 'auto',
            scrollSnapType: 'x mandatory',
            padding: '0 16px',
            scrollPaddingLeft: 16,
          }}>
          {dishes.map((d, i) => (
            <div key={i} style={{
              flexShrink: 0,
              width: 84,
              scrollSnapAlign: 'start',
              cursor: 'pointer',
              opacity: i === carouselIdx ? 1 : 0.7,
              transition: 'opacity .25s, transform .25s',
              transform: i === carouselIdx ? 'scale(1)' : 'scale(0.95)',
            }}
            onClick={() => scrollTo(i)}>
              <div style={{
                width: 84, height: 84,
                borderRadius: 12,
                background: `url(${d.img}) center/cover`,
                border: i === carouselIdx ? '2px solid #14391f' : '2px solid transparent',
                boxSizing: 'border-box',
              }} />
              <div style={{ fontSize: 10, color: '#1a2e20', marginTop: 5, fontWeight: 500, lineHeight: 1.2, height: 24, overflow: 'hidden' }}>{d.name}</div>
              <div style={{ fontSize: 9, color: '#6b7868', marginTop: 1 }}>{d.kcal}{plan.type !== 'workout' ? ' kcal' : ''} · {d.time}</div>
            </div>
          ))}
        </div>
        {/* Dots */}
        <div style={{ display: 'flex', justifyContent: 'center', gap: 4, paddingTop: 8 }}>
          {dishes.map((_, i) => (
            <span key={i} style={{
              width: i === carouselIdx ? 16 : 5,
              height: 5,
              borderRadius: 999,
              background: i === carouselIdx ? '#14391f' : 'rgba(20,57,31,0.2)',
              transition: 'all .25s',
            }} />
          ))}
        </div>
      </div>

      {/* Macros (collapsible) */}
      {plan.macros && (
        <div style={{ padding: '12px 16px 0' }}>
          <button
            onClick={() => setShowMacros(!showMacros)}
            style={{
              width: '100%',
              background: showMacros ? '#14391f' : '#faf6ec',
              color: showMacros ? 'white' : '#14391f',
              border: '1px solid rgba(20,57,31,0.1)',
              borderRadius: 12,
              padding: '10px 14px',
              fontSize: 12,
              fontWeight: 600,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              cursor: 'pointer',
              transition: 'all .2s',
              fontFamily: 'inherit',
            }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="10"/>
                <path d="M12 2 a10 10 0 0 1 8.66 5"/>
              </svg>
              Makros & Nährwerte
            </span>
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" style={{ transform: showMacros ? 'rotate(180deg)' : 'rotate(0)', transition: 'transform .2s' }}>
              <path d="M6 9l6 6 6-6"/>
            </svg>
          </button>
          {showMacros && (
            <div style={{
              marginTop: 8,
              background: '#14391f',
              borderRadius: 14,
              padding: '14px 16px',
              animation: 'mp-fadeIn .25s ease',
            }}>
              <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 14 }}>
                <div>
                  <div style={{ fontSize: 10, color: 'rgba(255,255,255,0.6)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Pro Tag</div>
                  <div style={{ fontFamily: "'Fraunces',serif", fontSize: 28, fontWeight: 600, color: 'white', lineHeight: 1 }}>{plan.kcalDay}<span style={{ fontSize: 13, fontWeight: 400, opacity: 0.6, marginLeft: 4 }}>kcal</span></div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontSize: 10, color: 'rgba(255,255,255,0.6)' }}>{plan.duration}</div>
                  <div style={{ fontSize: 11, color: 'white', fontWeight: 600 }}>{plan.recipes} Rezepte</div>
                </div>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
                <MacroBar label="Protein" value={plan.macros.protein} max={250} color="#ff7849" />
                <MacroBar label="Fett" value={plan.macros.fat} max={250} color="#f5b942" />
                <MacroBar label="KH" value={plan.macros.carbs} max={250} color="#5fa052" />
              </div>
            </div>
          )}
        </div>
      )}

      {/* Footer: price + buy */}
      <div style={{ padding: 16, display: 'flex', alignItems: 'center', gap: 10 }}>
        <CurrencyToggle currency={currency} onChange={setCurrency} />
        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
          <button style={{
            background: 'white',
            border: '1px solid rgba(20,57,31,0.15)',
            borderRadius: 12,
            padding: '11px 16px',
            fontSize: 13,
            fontWeight: 600,
            color: '#14391f',
            cursor: 'pointer',
            fontFamily: 'inherit',
          }}>Vorschau</button>
          <button style={{
            background: 'linear-gradient(135deg, #ff9a3c, #ff7849)',
            border: 'none',
            borderRadius: 12,
            padding: '11px 18px',
            fontSize: 13,
            fontWeight: 700,
            color: 'white',
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 6,
            boxShadow: '0 4px 14px rgba(255, 120, 73, 0.35)',
            fontFamily: 'inherit',
          }}>
            {price} {currency}
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5">
              <path d="M5 12h14M13 5l7 7-7 7"/>
            </svg>
          </button>
        </div>
      </div>

      {/* Social proof footer */}
      <div style={{
        borderTop: '1px solid rgba(20,57,31,0.08)',
        padding: '10px 16px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        fontSize: 11,
        color: '#6b7868',
        background: '#faf6ec',
      }}>
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
          <span style={{ display: 'inline-flex' }}>
            {[0,1,2].map(i => (
              <span key={i} style={{
                width: 18, height: 18, borderRadius: '50%',
                background: ['#ff7849', '#5fa052', '#f5b942'][i],
                border: '2px solid #faf6ec',
                marginLeft: i === 0 ? 0 : -6,
              }} />
            ))}
          </span>
          <strong style={{ color: '#14391f' }}>{plan.planning}</strong> planen jetzt
        </span>
        <span>📅 21.04.26</span>
      </div>
    </article>
  );
}

window.CardVariantA = CardVariantA;
window.PLANS = PLANS;
window.Stars = Stars;
window.MacroBar = MacroBar;
window.CurrencyToggle = CurrencyToggle;
