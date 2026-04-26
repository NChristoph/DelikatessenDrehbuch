/* Card Variant B — Tabbed with Macro Donut */

function MacroDonut({ macros, kcal, size = 110 }) {
  // Calculate kcal contribution
  const pKcal = macros.protein * 4;
  const cKcal = macros.carbs * 4;
  const fKcal = macros.fat * 9;
  const total = pKcal + cKcal + fKcal;
  const pPct = pKcal / total;
  const cPct = cKcal / total;
  const fPct = fKcal / total;

  const r = (size - 18) / 2;
  const cx = size / 2;
  const cy = size / 2;
  const circumference = 2 * Math.PI * r;
  const stroke = 14;

  return (
    <div style={{ position: 'relative', width: size, height: size }}>
      <svg width={size} height={size} style={{ transform: 'rotate(-90deg)' }}>
        <circle cx={cx} cy={cy} r={r} fill="none" stroke="rgba(20,57,31,0.06)" strokeWidth={stroke} />
        <circle
          cx={cx} cy={cy} r={r}
          fill="none" stroke="#ff7849" strokeWidth={stroke}
          strokeDasharray={`${pPct * circumference} ${circumference}`}
          strokeDashoffset={0}
          strokeLinecap="butt"
        />
        <circle
          cx={cx} cy={cy} r={r}
          fill="none" stroke="#f5b942" strokeWidth={stroke}
          strokeDasharray={`${fPct * circumference} ${circumference}`}
          strokeDashoffset={-pPct * circumference}
          strokeLinecap="butt"
        />
        <circle
          cx={cx} cy={cy} r={r}
          fill="none" stroke="#5fa052" strokeWidth={stroke}
          strokeDasharray={`${cPct * circumference} ${circumference}`}
          strokeDashoffset={-(pPct + fPct) * circumference}
          strokeLinecap="butt"
        />
      </svg>
      <div style={{
        position: 'absolute', inset: 0,
        display: 'flex', flexDirection: 'column',
        alignItems: 'center', justifyContent: 'center',
      }}>
        <div style={{ fontFamily: "'Fraunces', serif", fontSize: 20, fontWeight: 600, color: '#14391f', lineHeight: 1 }}>{kcal}</div>
        <div style={{ fontSize: 9, color: '#6b7868', marginTop: 1, textTransform: 'uppercase', letterSpacing: '0.04em' }}>kcal/Tag</div>
      </div>
    </div>
  );
}

function CardVariantB({ plan }) {
  const [currency, setCurrency] = React.useState('WLD');
  const [tab, setTab] = React.useState('overview'); // overview | dishes | macros
  const [carouselIdx, setCarouselIdx] = React.useState(0);
  const trackRef = React.useRef(null);
  const dishes = plan.dishes;
  const price = currency === 'WLD' ? plan.priceWLD : plan.priceUSDC;

  const handleScroll = () => {
    if (!trackRef.current) return;
    const scrollLeft = trackRef.current.scrollLeft;
    const itemWidth = trackRef.current.children[0]?.offsetWidth + 8 || 200;
    const idx = Math.round(scrollLeft / itemWidth);
    setCarouselIdx(idx);
  };

  const tabs = [
    { id: 'overview', label: 'Überblick' },
    { id: 'dishes', label: plan.type === 'workout' ? 'Trainings' : 'Gerichte' },
  ];
  if (plan.macros) tabs.push({ id: 'macros', label: 'Makros' });

  return (
    <article style={{
      background: 'white',
      borderRadius: 22,
      overflow: 'hidden',
      boxShadow: '0 8px 24px rgba(20, 57, 31, 0.08)',
      marginBottom: 20,
    }}>
      {/* Compact hero */}
      <div style={{
        position: 'relative',
        height: 180,
        background: `url(${dishes[0].img}) center/cover`,
      }}>
        <div style={{
          position: 'absolute', inset: 0,
          background: 'linear-gradient(180deg, transparent 50%, rgba(0,0,0,0.85) 100%)',
        }} />
        {/* Badges */}
        <div style={{ position: 'absolute', top: 12, left: 12, right: 12, display: 'flex', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', gap: 6 }}>
            {plan.badge && (
              <span style={{
                background: 'linear-gradient(135deg, #f5b942, #c8a45c)',
                color: 'white',
                padding: '4px 10px',
                borderRadius: 999,
                fontSize: 10,
                fontWeight: 700,
                letterSpacing: '0.04em',
                textTransform: 'uppercase',
              }}>★ {plan.badge}</span>
            )}
            <span style={{
              background: 'rgba(20,57,31,0.85)',
              backdropFilter: 'blur(8px)',
              color: 'white',
              padding: '4px 10px',
              borderRadius: 999,
              fontSize: 10,
              fontWeight: 600,
              textTransform: 'uppercase',
              letterSpacing: '0.04em',
            }}>{plan.type === 'meal' ? 'Ernährung' : plan.type === 'workout' ? 'Workout' : 'Kombi'}</span>
          </div>
          <button style={{
            width: 32, height: 32,
            borderRadius: '50%',
            background: 'rgba(255,255,255,0.18)',
            backdropFilter: 'blur(10px)',
            border: '1px solid rgba(255,255,255,0.25)',
            cursor: 'pointer',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2">
              <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
            </svg>
          </button>
        </div>

        {/* Title overlay */}
        <div style={{ position: 'absolute', bottom: 12, left: 14, right: 14, color: 'white' }}>
          <h3 style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 20,
            fontWeight: 600,
            margin: 0,
            letterSpacing: '-0.02em',
            lineHeight: 1.15,
            textShadow: '0 2px 8px rgba(0,0,0,0.4)',
          }}>{plan.title}</h3>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6, fontSize: 11 }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
              <span>{plan.creatorAvatar}</span>
              <span style={{ fontWeight: 600 }}>{plan.creator}</span>
            </span>
            <span style={{ opacity: 0.5 }}>·</span>
            <span style={{ opacity: 0.85 }}>{plan.duration}</span>
            <span style={{ opacity: 0.5 }}>·</span>
            <Stars rating={plan.rating} />
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div style={{
        display: 'flex',
        borderBottom: '1px solid rgba(20,57,31,0.08)',
        padding: '0 14px',
      }}>
        {tabs.map(t => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            style={{
              flex: 1,
              background: 'none',
              border: 'none',
              padding: '12px 4px',
              fontSize: 12,
              fontWeight: 600,
              color: tab === t.id ? '#14391f' : '#6b7868',
              cursor: 'pointer',
              position: 'relative',
              fontFamily: 'inherit',
              transition: 'color .2s',
            }}>
            {t.label}
            {tab === t.id && (
              <div style={{
                position: 'absolute',
                bottom: -1, left: '50%',
                transform: 'translateX(-50%)',
                width: '70%',
                height: 2,
                background: '#14391f',
                borderRadius: 2,
              }} />
            )}
          </button>
        ))}
      </div>

      {/* Tab content */}
      <div style={{ minHeight: 180 }}>
        {tab === 'overview' && (
          <div style={{ padding: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
              {plan.macros ? (
                <MacroDonut macros={plan.macros} kcal={plan.kcalDay} />
              ) : (
                <div style={{
                  width: 110, height: 110, borderRadius: '50%',
                  background: 'linear-gradient(135deg, #14391f, #1a4a28)',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  flexDirection: 'column',
                  color: 'white',
                }}>
                  <div style={{ fontFamily: "'Fraunces', serif", fontSize: 24, fontWeight: 600, lineHeight: 1 }}>{plan.workouts}</div>
                  <div style={{ fontSize: 9, marginTop: 2, opacity: 0.7, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Workouts</div>
                </div>
              )}
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 8 }}>
                {plan.macros ? (
                  <>
                    <MiniStat color="#ff7849" label="Protein" value={`${plan.macros.protein}g`} />
                    <MiniStat color="#f5b942" label="Fett" value={`${plan.macros.fat}g`} />
                    <MiniStat color="#5fa052" label="KH" value={`${plan.macros.carbs}g`} />
                  </>
                ) : (
                  <>
                    <MiniStat color="#ff7849" label="Trainings" value={plan.workouts} />
                    <MiniStat color="#f5b942" label="Dauer" value={plan.duration} />
                    <MiniStat color="#5fa052" label="Level" value="Fortgeschritten" />
                  </>
                )}
              </div>
            </div>
            {/* Tags */}
            <div style={{ display: 'flex', gap: 6, marginTop: 14, flexWrap: 'wrap' }}>
              {plan.tags.map(t => (
                <span key={t} style={{
                  background: '#faf6ec',
                  color: '#14391f',
                  padding: '4px 10px',
                  borderRadius: 999,
                  fontSize: 11,
                  fontWeight: 500,
                  border: '1px solid rgba(20,57,31,0.08)',
                }}># {t}</span>
              ))}
            </div>
          </div>
        )}

        {tab === 'dishes' && (
          <div style={{ padding: '14px 0 12px' }}>
            <div
              ref={trackRef}
              onScroll={handleScroll}
              style={{
                display: 'flex',
                gap: 10,
                overflowX: 'auto',
                scrollSnapType: 'x mandatory',
                padding: '0 14px',
              }}>
              {dishes.map((d, i) => (
                <div key={i} style={{
                  flexShrink: 0,
                  width: 200,
                  scrollSnapAlign: 'start',
                  borderRadius: 14,
                  overflow: 'hidden',
                  background: '#faf6ec',
                  position: 'relative',
                }}>
                  <div style={{
                    height: 130,
                    background: `url(${d.img}) center/cover`,
                    position: 'relative',
                  }}>
                    <span style={{
                      position: 'absolute',
                      top: 8, left: 8,
                      background: 'rgba(255,255,255,0.95)',
                      color: '#14391f',
                      padding: '3px 8px',
                      borderRadius: 999,
                      fontSize: 10,
                      fontWeight: 600,
                    }}>
                      {plan.type === 'workout' ? d.kcal : `${d.kcal} kcal`}
                    </span>
                  </div>
                  <div style={{ padding: '10px 12px' }}>
                    <div style={{ fontSize: 12, fontWeight: 600, color: '#14391f', lineHeight: 1.2 }}>{d.name}</div>
                    <div style={{ fontSize: 10, color: '#6b7868', marginTop: 3, display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                      <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>
                      </svg>
                      {d.time}
                    </div>
                  </div>
                </div>
              ))}
            </div>
            <div style={{ display: 'flex', justifyContent: 'center', gap: 4, paddingTop: 10 }}>
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
        )}

        {tab === 'macros' && plan.macros && (
          <div style={{ padding: 16 }}>
            <div style={{
              background: 'linear-gradient(135deg, #14391f, #0e2415)',
              borderRadius: 14,
              padding: 16,
              color: 'white',
            }}>
              <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 12 }}>
                <div>
                  <div style={{ fontSize: 10, color: 'rgba(255,255,255,0.6)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Tagesziel</div>
                  <div style={{ fontFamily: "'Fraunces',serif", fontSize: 26, fontWeight: 600, lineHeight: 1 }}>
                    {plan.kcalDay}<span style={{ fontSize: 12, fontWeight: 400, opacity: 0.6, marginLeft: 4 }}>kcal</span>
                  </div>
                </div>
                <div style={{
                  background: 'rgba(255,255,255,0.08)',
                  padding: '6px 10px',
                  borderRadius: 999,
                  fontSize: 11,
                }}>{plan.recipes} Rezepte</div>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
                <MacroBar label="Protein" value={plan.macros.protein} max={250} color="#ff7849" />
                <MacroBar label="Fett" value={plan.macros.fat} max={250} color="#f5b942" />
                <MacroBar label="KH" value={plan.macros.carbs} max={250} color="#5fa052" />
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Footer */}
      <div style={{
        borderTop: '1px solid rgba(20,57,31,0.08)',
        padding: '12px 14px',
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        background: '#faf6ec',
      }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <CurrencyToggle currency={currency} onChange={setCurrency} />
          <span style={{ fontSize: 10, color: '#6b7868', paddingLeft: 4 }}>
            <strong style={{ color: '#14391f' }}>{plan.sold}</strong> verkauft
          </span>
        </div>
        <button style={{
          marginLeft: 'auto',
          background: 'linear-gradient(135deg, #ff9a3c, #ff7849)',
          border: 'none',
          borderRadius: 12,
          padding: '12px 20px',
          fontSize: 13,
          fontWeight: 700,
          color: 'white',
          cursor: 'pointer',
          display: 'inline-flex',
          alignItems: 'center',
          gap: 8,
          boxShadow: '0 4px 14px rgba(255, 120, 73, 0.35)',
          fontFamily: 'inherit',
        }}>
          <span style={{ fontFamily: "'Fraunces', serif", fontSize: 17 }}>{price}</span>
          <span style={{ opacity: 0.85, fontSize: 11 }}>{currency}</span>
          <span style={{ width: 1, height: 16, background: 'rgba(255,255,255,0.3)' }} />
          Freischalten
        </button>
      </div>
    </article>
  );
}

function MiniStat({ color, label, value }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
      <span style={{ width: 8, height: 8, borderRadius: '50%', background: color }} />
      <span style={{ fontSize: 11, color: '#6b7868', flex: 1 }}>{label}</span>
      <span style={{ fontSize: 12, fontWeight: 600, color: '#14391f' }}>{value}</span>
    </div>
  );
}

window.CardVariantB = CardVariantB;
window.MacroDonut = MacroDonut;
