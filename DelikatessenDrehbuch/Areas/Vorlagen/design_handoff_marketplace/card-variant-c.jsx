/* Card Variant C — Premium Stacked / Editorial */

function CardVariantC({ plan }) {
  const [currency, setCurrency] = React.useState('WLD');
  const [activeImg, setActiveImg] = React.useState(0);
  const [showMacros, setShowMacros] = React.useState(false);
  const dishes = plan.dishes;
  const price = currency === 'WLD' ? plan.priceWLD : plan.priceUSDC;

  return (
    <article style={{
      background: 'linear-gradient(180deg, #14391f 0%, #0d2614 100%)',
      borderRadius: 24,
      overflow: 'hidden',
      boxShadow: '0 20px 50px rgba(20, 57, 31, 0.20)',
      marginBottom: 20,
      color: 'white',
      position: 'relative',
    }}>
      {/* Premium texture */}
      <div style={{
        position: 'absolute', top: 0, right: 0, width: 200, height: 200,
        background: 'radial-gradient(circle at top right, rgba(245, 185, 66, 0.18), transparent 65%)',
        pointerEvents: 'none',
      }} />

      {/* Header row */}
      <div style={{ padding: '16px 18px 12px', display: 'flex', alignItems: 'center', gap: 10, position: 'relative' }}>
        <div style={{
          width: 36, height: 36, borderRadius: '50%',
          background: 'linear-gradient(135deg, #5fa052, #3a7a30)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 18,
          border: '1.5px solid rgba(255,255,255,0.15)',
        }}>{plan.creatorAvatar}</div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 12, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 5 }}>
            {plan.creator}
            <svg width="11" height="11" viewBox="0 0 24 24" fill="#5fa052">
              <path d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
          </div>
          <div style={{ fontSize: 10, color: 'rgba(255,255,255,0.55)', marginTop: 1 }}>Verifizierter Creator · {plan.sold} Verkäufe</div>
        </div>
        {plan.badge && (
          <span style={{
            background: 'linear-gradient(135deg, #f5b942, #c8a45c)',
            color: '#3a2a08',
            padding: '5px 11px',
            borderRadius: 999,
            fontSize: 10,
            fontWeight: 700,
            letterSpacing: '0.04em',
            textTransform: 'uppercase',
            display: 'inline-flex', alignItems: 'center', gap: 4,
          }}>
            <svg width="10" height="10" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 2l2.4 7.4H22l-6.2 4.5 2.4 7.4L12 16.8l-6.2 4.5 2.4-7.4L2 9.4h7.6z"/>
            </svg>
            {plan.badge}
          </span>
        )}
      </div>

      {/* Hero stack: large left + small right */}
      <div style={{ padding: '0 18px', display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: 8, height: 200 }}>
        <div style={{
          borderRadius: 16,
          background: `url(${dishes[activeImg].img}) center/cover`,
          position: 'relative',
          overflow: 'hidden',
          cursor: 'pointer',
        }}>
          <div style={{
            position: 'absolute', bottom: 0, left: 0, right: 0,
            padding: '20px 12px 10px',
            background: 'linear-gradient(180deg, transparent, rgba(0,0,0,0.85))',
          }}>
            <div style={{ fontSize: 11, fontWeight: 600 }}>{dishes[activeImg].name}</div>
            <div style={{ fontSize: 9, opacity: 0.7, marginTop: 1 }}>
              {plan.type === 'workout' ? dishes[activeImg].kcal : `${dishes[activeImg].kcal} kcal`} · {dishes[activeImg].time}
            </div>
          </div>
        </div>
        <div style={{ display: 'grid', gridTemplateRows: '1fr 1fr', gap: 8 }}>
          {dishes.slice(1, 3).map((d, i) => (
            <div key={i}
              onClick={() => setActiveImg(i + 1)}
              style={{
                borderRadius: 14,
                background: `url(${d.img}) center/cover`,
                cursor: 'pointer',
                position: 'relative',
                border: activeImg === i + 1 ? '2px solid #f5b942' : '2px solid transparent',
              }}>
              {i === 1 && dishes.length > 3 && (
                <div style={{
                  position: 'absolute', inset: 0,
                  background: 'rgba(20,57,31,0.7)',
                  borderRadius: 12,
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 14, fontWeight: 700,
                  fontFamily: "'Fraunces', serif",
                }}>+{dishes.length - 3}</div>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Title */}
      <div style={{ padding: '16px 18px 8px' }}>
        <h3 style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 24,
          fontWeight: 600,
          letterSpacing: '-0.02em',
          margin: 0,
          lineHeight: 1.1,
        }}>{plan.title}</h3>
        <div style={{ display: 'flex', gap: 6, marginTop: 8, flexWrap: 'wrap' }}>
          {plan.tags.map(t => (
            <span key={t} style={{
              background: 'rgba(255,255,255,0.08)',
              color: 'rgba(255,255,255,0.85)',
              padding: '3px 9px',
              borderRadius: 999,
              fontSize: 10,
              fontWeight: 500,
              border: '1px solid rgba(255,255,255,0.1)',
            }}>{t}</span>
          ))}
        </div>
      </div>

      {/* Stat row */}
      <div style={{
        margin: '8px 18px 0',
        padding: '12px 14px',
        background: 'rgba(255,255,255,0.04)',
        border: '1px solid rgba(255,255,255,0.06)',
        borderRadius: 14,
        display: 'grid',
        gridTemplateColumns: 'repeat(4, 1fr)',
        gap: 8,
      }}>
        <StatCol icon="⭐" value={plan.rating} label="Rating" gold />
        {plan.recipes != null && <StatCol icon="🍽" value={plan.recipes} label="Rezepte" />}
        {plan.workouts != null && <StatCol icon="💪" value={plan.workouts} label="Workouts" />}
        {plan.kcalDay != null && <StatCol icon="🔥" value={`${plan.kcalDay}`} label="kcal/Tag" />}
        <StatCol icon="📆" value={plan.duration.split(' ')[0]} label={plan.duration.split(' ')[1] || ''} />
      </div>

      {/* Macros expandable */}
      {plan.macros && (
        <div style={{ padding: '12px 18px 0' }}>
          <button
            onClick={() => setShowMacros(!showMacros)}
            style={{
              width: '100%',
              background: 'rgba(255,255,255,0.04)',
              border: '1px solid rgba(255,255,255,0.08)',
              borderRadius: 12,
              padding: '10px 14px',
              fontSize: 12,
              fontWeight: 600,
              color: 'white',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              fontFamily: 'inherit',
            }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ display: 'inline-flex', gap: 3 }}>
                <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#ff7849' }} />
                <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#f5b942' }} />
                <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#5fa052' }} />
              </span>
              Makros · P {plan.macros.protein}g · F {plan.macros.fat}g · KH {plan.macros.carbs}g
            </span>
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"
              style={{ transform: showMacros ? 'rotate(180deg)' : 'rotate(0)', transition: 'transform .2s' }}>
              <path d="M6 9l6 6 6-6"/>
            </svg>
          </button>
          {showMacros && (
            <div style={{ marginTop: 8, padding: '6px 4px 2px', display: 'flex', flexDirection: 'column', gap: 9 }}>
              <MacroBar label="Protein" value={plan.macros.protein} max={250} color="#ff7849" />
              <MacroBar label="Fett" value={plan.macros.fat} max={250} color="#f5b942" />
              <MacroBar label="KH" value={plan.macros.carbs} max={250} color="#5fa052" />
            </div>
          )}
        </div>
      )}

      {/* Footer */}
      <div style={{
        margin: '14px 18px 18px',
        background: 'rgba(255,255,255,0.04)',
        border: '1px solid rgba(255,255,255,0.08)',
        borderRadius: 16,
        padding: '12px 14px',
        display: 'flex',
        alignItems: 'center',
        gap: 10,
      }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <div style={{ fontSize: 9, color: 'rgba(255,255,255,0.55)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Preis</div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
            <span style={{ fontFamily: "'Fraunces', serif", fontSize: 22, fontWeight: 600, color: 'white', lineHeight: 1 }}>{price}</span>
            <button
              onClick={() => setCurrency(currency === 'WLD' ? 'USDC' : 'WLD')}
              style={{
                background: 'rgba(255,255,255,0.08)',
                border: '1px solid rgba(255,255,255,0.15)',
                color: 'white',
                borderRadius: 999,
                padding: '3px 9px',
                fontSize: 10,
                fontWeight: 600,
                cursor: 'pointer',
                display: 'inline-flex', alignItems: 'center', gap: 4,
                fontFamily: 'inherit',
              }}>
              {currency}
              <svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M7 10l5 5 5-5"/>
              </svg>
            </button>
          </div>
        </div>
        <button style={{
          marginLeft: 'auto',
          background: 'linear-gradient(135deg, #ff9a3c, #ff7849)',
          border: 'none',
          borderRadius: 12,
          padding: '12px 22px',
          fontSize: 13,
          fontWeight: 700,
          color: 'white',
          cursor: 'pointer',
          display: 'inline-flex',
          alignItems: 'center',
          gap: 6,
          boxShadow: '0 4px 14px rgba(255, 120, 73, 0.4)',
          fontFamily: 'inherit',
        }}>
          Freischalten
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5">
            <path d="M5 12h14M13 5l7 7-7 7"/>
          </svg>
        </button>
      </div>

      {/* Live activity strip */}
      <div style={{
        borderTop: '1px solid rgba(255,255,255,0.06)',
        padding: '10px 18px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        fontSize: 10,
        color: 'rgba(255,255,255,0.6)',
      }}>
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
          <span style={{
            width: 6, height: 6, borderRadius: '50%',
            background: '#5fa052',
            boxShadow: '0 0 0 0 rgba(95,160,82,0.6)',
            animation: 'mp-pulse 2s infinite',
          }} />
          <strong style={{ color: 'white' }}>{plan.planning}</strong> planen jetzt
        </span>
        <span>↗ Trending diese Woche</span>
      </div>
    </article>
  );
}

function StatCol({ icon, value, label, gold }) {
  return (
    <div style={{ textAlign: 'center', padding: '0 4px' }}>
      <div style={{ fontSize: 10, marginBottom: 2 }}>{icon}</div>
      <div style={{
        fontFamily: "'Fraunces', serif",
        fontSize: 14,
        fontWeight: 600,
        color: gold ? '#f5b942' : 'white',
        lineHeight: 1,
      }}>{value}</div>
      <div style={{ fontSize: 9, color: 'rgba(255,255,255,0.55)', marginTop: 3, textTransform: 'uppercase', letterSpacing: '0.04em' }}>{label}</div>
    </div>
  );
}

window.CardVariantC = CardVariantC;
