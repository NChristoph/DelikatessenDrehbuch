/* Home Screen · Avocado-Stil
   Aus den Screenshots: Delikatessen Drehbuch Mini-App Home
   - Logo + Branding
   - 4 Action-Cards: Wochenplan · Entdecken · Marktplatz · Rezept hochladen
   - Mein Bereich (List Item)
   - Login + Debug-Block (Test-Hashes)
*/

const { useState: hUseState } = React;

// ─── Icons ────────────────────────────────────────────────────
function HIcon({ name, size = 22, color = 'currentColor', fill = 'none' }) {
  const props = { width: size, height: size, viewBox: '0 0 24 24', fill, stroke: color, strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' };
  if (name === 'wand')     return <svg {...props}><path d="M15 4l2 2M4 20l9-9M12.5 3.5l3 3M16 8l3 3M20.5 12.5l1.5 1.5M3 21l2-2"/><path d="M9 15l1 1" strokeWidth="2"/><path d="M19 5l1 1" strokeWidth="2"/></svg>;
  if (name === 'sparkles') return <svg {...props}><path d="M12 3l1.8 4.2L18 9l-4.2 1.8L12 15l-1.8-4.2L6 9l4.2-1.8L12 3z"/><path d="M19 14l.9 2.1L22 17l-2.1.9L19 20l-.9-2.1L16 17l2.1-.9L19 14z"/></svg>;
  if (name === 'shop')     return <svg {...props}><path d="M3 9l1.5-5h15L21 9"/><path d="M3 9v11a1 1 0 0 0 1 1h16a1 1 0 0 0 1-1V9"/><path d="M3 9h18M9 13a3 3 0 0 1-6 0M15 13a3 3 0 0 1-6 0M21 13a3 3 0 0 1-6 0"/></svg>;
  if (name === 'finger')   return <svg {...props}><path d="M12 11v5a3 3 0 0 1-6 0v-5a6 6 0 0 1 12 0v3"/><path d="M9 11a3 3 0 0 1 6 0"/><path d="M18 14v2a6 6 0 0 1-12 0"/></svg>;
  if (name === 'user')     return <svg {...props}><circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 4-7 8-7s8 3 8 7"/></svg>;
  if (name === 'chev')     return <svg {...props}><polyline points="6 9 12 15 18 9"/></svg>;
  if (name === 'chevR')    return <svg {...props}><polyline points="9 6 15 12 9 18"/></svg>;
  if (name === 'login')    return <svg {...props}><path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4"/><polyline points="10 17 15 12 10 7"/><line x1="15" y1="12" x2="3" y2="12"/></svg>;
  if (name === 'logout')   return <svg {...props}><path d="M9 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>;
  if (name === 'orb')      return <svg {...props}><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"/></svg>;
  if (name === 'idCard')   return <svg {...props}><rect x="3" y="6" width="18" height="14" rx="2"/><circle cx="9" cy="12" r="2.5"/><path d="M14 11h5M14 14h3"/><path d="M5 17c.8-1.4 2.3-2.3 4-2.3s3.2.9 4 2.3"/></svg>;
  if (name === 'shieldCheck') return <svg {...props}><path d="M12 3l8 3v6c0 4-3 8-8 9-5-1-8-5-8-9V6l8-3z"/><polyline points="9 12 11 14 15 10"/></svg>;
  return null;
}

// ─── Action Card (colored hero card) ──────────────────────────
function HActionCard({ icon, title, subtitle, palette, badge, decorIcon }) {
  return (
    <button style={{
      display: 'block', width: '100%',
      background: palette.bg,
      border: 'none',
      borderRadius: 24,
      padding: '18px 18px',
      cursor: 'pointer', fontFamily: 'inherit',
      textAlign: 'left',
      position: 'relative', overflow: 'hidden',
      color: palette.fg,
      boxShadow: palette.shadow,
    }}>
      {/* Decorative oversized icon (behind content) */}
      {decorIcon && (
        <div style={{
          position: 'absolute', right: -10, top: '50%',
          transform: 'translateY(-50%)',
          opacity: 0.18,
          pointerEvents: 'none',
        }}>
          <HIcon name={decorIcon} size={150} color={palette.fg} />
        </div>
      )}
      {/* Optional badge in top-right */}
      {badge && (
        <div style={{
          position: 'absolute',
          top: 14, right: 12,
          display: 'inline-flex', alignItems: 'center', gap: 5,
          background: 'rgba(255,255,255,0.92)',
          color: '#14391f',
          padding: '4px 10px 4px 8px',
          borderRadius: 999,
          fontSize: 10, fontWeight: 700,
          letterSpacing: '0.04em',
        }}>
          <HIcon name="shieldCheck" size={11} color="#5fa052" />
          {badge}
        </div>
      )}
      <div style={{ display: 'flex', alignItems: 'center', gap: 14, position: 'relative', zIndex: 1 }}>
        <div style={{
          width: 54, height: 54, borderRadius: 16,
          background: palette.iconBg,
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          flexShrink: 0,
          color: palette.iconFg,
        }}>
          <HIcon name={icon} size={24} color={palette.iconFg} />
        </div>
        <div style={{ flex: 1, minWidth: 0, paddingRight: badge ? 8 : 0 }}>
          <div style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 22, fontWeight: 700,
            letterSpacing: '-0.02em',
            lineHeight: 1.1,
          }}>{title}</div>
          <div style={{
            marginTop: 4,
            fontSize: 13, fontWeight: 500,
            opacity: 0.86,
            lineHeight: 1.3,
          }}>{subtitle}</div>
        </div>
      </div>
    </button>
  );
}

// ─── Square Card (1:1 tile, for Upload & My Bereich) ──────────
// ─── Hero Card (Feed — main button) ───────────────────────────
function HHeroCard({ icon, title, subtitle }) {
  return (
    <button style={{
      display: 'block', width: '100%',
      background: 'linear-gradient(135deg, #5fa052 0%, #2f6a3a 55%, #14391f 100%)',
      border: 'none',
      borderRadius: 26,
      padding: '24px 22px',
      cursor: 'pointer', fontFamily: 'inherit',
      textAlign: 'left',
      position: 'relative', overflow: 'hidden',
      color: 'white',
      boxShadow: '0 16px 36px rgba(20,57,31,0.30)',
    }}>
      <div style={{
        position: 'absolute', top: -60, right: -40,
        width: 220, height: 220,
        borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(245,233,160,0.32) 0%, rgba(245,233,160,0) 70%)',
        pointerEvents: 'none',
      }} />
      <svg width="120" height="120" viewBox="0 0 120 120" style={{
        position: 'absolute', right: 10, bottom: -10,
        opacity: 0.55, pointerEvents: 'none',
      }}>
        <g fill="rgba(255,255,255,0.85)">
          <path d="M40 18 L43 28 L53 31 L43 34 L40 44 L37 34 L27 31 L37 28 Z" />
          <path d="M82 50 L84 58 L92 60 L84 62 L82 70 L80 62 L72 60 L80 58 Z" opacity="0.7" />
          <path d="M60 90 L62 96 L68 98 L62 100 L60 106 L58 100 L52 98 L58 96 Z" opacity="0.5" />
          <circle cx="20" cy="60" r="2" opacity="0.6" />
          <circle cx="100" cy="22" r="1.5" opacity="0.7" />
          <circle cx="105" cy="85" r="1.5" opacity="0.5" />
        </g>
      </svg>
      <div style={{
        display: 'inline-flex', alignItems: 'center', gap: 6,
        background: 'rgba(255,255,255,0.16)',
        color: 'white',
        padding: '4px 10px',
        borderRadius: 999,
        fontSize: 10, fontWeight: 700,
        letterSpacing: '0.12em', textTransform: 'uppercase',
        position: 'relative', zIndex: 1,
        backdropFilter: 'blur(6px)',
      }}>
        <span style={{
          width: 6, height: 6, borderRadius: '50%',
          background: '#f5e9a0',
          boxShadow: '0 0 8px #f5e9a0',
        }} />
        Live
      </div>
      <div style={{
        display: 'flex', alignItems: 'flex-end', gap: 16,
        marginTop: 14, position: 'relative', zIndex: 1,
      }}>
        <div style={{
          width: 62, height: 62, borderRadius: 18,
          background: 'rgba(255,255,255,0.18)',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          flexShrink: 0,
          backdropFilter: 'blur(8px)',
          border: '1px solid rgba(255,255,255,0.22)',
        }}>
          <HIcon name={icon} size={30} color="#f5e9a0" />
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 30, fontWeight: 700,
            letterSpacing: '-0.025em',
            lineHeight: 1,
          }}>{title}</div>
          <div style={{
            marginTop: 6,
            fontSize: 13, fontWeight: 500,
            opacity: 0.88,
            lineHeight: 1.3,
          }}>{subtitle}</div>
        </div>
        <HIcon name="chevR" size={20} color="rgba(255,255,255,0.85)" />
      </div>
    </button>
  );
}

function HSquareCard({ icon, title, subtitle, palette, badge, decorIcon }) {
  return (
    <button style={{
      display: 'flex', flexDirection: 'column', justifyContent: 'space-between',
      width: '100%', aspectRatio: '1 / 1',
      background: palette.bg,
      border: 'none',
      borderRadius: 24,
      padding: 18,
      cursor: 'pointer', fontFamily: 'inherit',
      textAlign: 'left',
      position: 'relative', overflow: 'hidden',
      color: palette.fg,
      boxShadow: palette.shadow,
    }}>
      {decorIcon && (
        <div style={{
          position: 'absolute', right: -22, bottom: -22,
          opacity: 0.16, pointerEvents: 'none',
        }}>
          <HIcon name={decorIcon} size={140} color={palette.fg} />
        </div>
      )}
      {badge && (
        <div style={{
          position: 'absolute',
          top: 12, right: 12,
          display: 'inline-flex', alignItems: 'center', gap: 4,
          background: 'rgba(255,255,255,0.92)',
          color: '#14391f',
          padding: '3px 8px 3px 7px',
          borderRadius: 999,
          fontSize: 9, fontWeight: 700,
          letterSpacing: '0.04em',
        }}>
          <HIcon name="shieldCheck" size={10} color="#5fa052" />
          {badge}
        </div>
      )}
      <div style={{
        width: 48, height: 48, borderRadius: 14,
        background: palette.iconBg,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        color: palette.iconFg,
        position: 'relative', zIndex: 1,
      }}>
        <HIcon name={icon} size={22} color={palette.iconFg} />
      </div>
      <div style={{ position: 'relative', zIndex: 1 }}>
        <div style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 20, fontWeight: 700,
          letterSpacing: '-0.02em',
          lineHeight: 1.05,
        }}>{title}</div>
        <div style={{
          marginTop: 4,
          fontSize: 12, fontWeight: 500,
          opacity: 0.85,
          lineHeight: 1.25,
        }}>{subtitle}</div>
      </div>
    </button>
  );
}

// ─── Logo block ───────────────────────────────────────────────
function HLogoBlock() {
  return (
    <div style={{ textAlign: 'center', paddingTop: 16 }}>
      <div style={{
        width: 88, height: 88, borderRadius: '50%',
        background: 'white',
        margin: '0 auto',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        boxShadow: '0 8px 24px rgba(20,57,31,0.10), inset 0 0 0 1px rgba(20,57,31,0.04)',
        position: 'relative',
      }}>
        {/* Avocado SVG mark */}
        <svg width="48" height="56" viewBox="0 0 48 56">
          <defs>
            <linearGradient id="avoSkin" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#5fa052"/>
              <stop offset="100%" stopColor="#3f7536"/>
            </linearGradient>
            <linearGradient id="avoFlesh" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#f5e8b8"/>
              <stop offset="100%" stopColor="#d9c98a"/>
            </linearGradient>
          </defs>
          {/* outer skin */}
          <path d="M24 2 C12 2 6 18 6 32 C6 46 14 54 24 54 C34 54 42 46 42 32 C42 18 36 2 24 2 Z"
                fill="url(#avoSkin)"/>
          {/* flesh */}
          <path d="M24 8 C16 8 11 20 11 32 C11 42 17 49 24 49 C31 49 37 42 37 32 C37 20 32 8 24 8 Z"
                fill="url(#avoFlesh)"/>
          {/* pit */}
          <circle cx="24" cy="33" r="7" fill="#7a4a26"/>
          <circle cx="22" cy="31" r="2" fill="#9a6234" opacity="0.6"/>
        </svg>
      </div>
      <h1 style={{
        margin: '14px 0 0',
        fontFamily: "'Fraunces', serif",
        fontSize: 30, fontWeight: 700,
        letterSpacing: '-0.025em',
        color: '#14391f', lineHeight: 1.05,
      }}>Avocado</h1>
      <div style={{
        marginTop: 8,
        fontSize: 11, fontWeight: 700,
        letterSpacing: '0.24em', textTransform: 'uppercase',
        color: '#9aa295',
        display: 'inline-flex', alignItems: 'center', gap: 8,
      }}>
        <span style={{ width: 16, height: 1, background: '#9aa295' }} />
        Mini App
        <span style={{ width: 16, height: 1, background: '#9aa295' }} />
      </div>
    </div>
  );
}

// ─── Top language switcher ────────────────────────────────────
function HTopBar() {
  return (
    <div style={{
      display: 'flex', justifyContent: 'flex-end',
      padding: '12px 18px 0',
    }}>
      <button style={{
        background: 'white',
        border: '1px solid rgba(20,57,31,0.08)',
        borderRadius: 999,
        padding: '7px 12px',
        fontSize: 11, fontWeight: 700, color: '#14391f',
        letterSpacing: '0.08em',
        cursor: 'pointer', fontFamily: 'inherit',
        display: 'inline-flex', alignItems: 'center', gap: 5,
        boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
      }}>
        DE <HIcon name="chev" size={12} color="#14391f" />
      </button>
    </div>
  );
}

// ─── Debug block (only visible in debug mode) ─────────────────
function HDebugBlock() {
  return (
    <div style={{
      margin: '24px 14px 0',
      padding: '18px 14px',
      background: '#faf6ec',
      border: '1px dashed rgba(20,57,31,0.18)',
      borderRadius: 18,
      textAlign: 'center',
    }}>
      <div style={{
        fontSize: 10, fontWeight: 700,
        color: '#9aa295',
        letterSpacing: '0.18em', textTransform: 'uppercase',
      }}>Debug-Modus</div>
      <div style={{ marginTop: 6, fontSize: 12, color: '#6b7868' }}>Aktiver Debug-Hash:</div>
      <code style={{
        display: 'inline-block', marginTop: 6,
        padding: '5px 10px',
        background: 'white',
        border: '1px solid rgba(20,57,31,0.08)',
        borderRadius: 8,
        fontFamily: 'ui-monospace, SFMono-Regular, monospace',
        fontSize: 10,
        color: '#c44e4e',
        maxWidth: '100%',
        overflow: 'hidden', textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        width: '85%',
      }}>0x2da33d4d7152caf4dad616bffa6fed2a7…ff48</code>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 14 }}>
        <button style={{
          background: 'white',
          border: '1px solid rgba(20,57,31,0.12)',
          borderRadius: 999,
          padding: '10px 14px',
          fontSize: 13, fontWeight: 600, color: '#14391f',
          cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 8,
        }}>
          <HIcon name="orb" size={14} color="#14391f" />
          Mit Test-Hash A anmelden
        </button>
        <button style={{
          background: 'white',
          border: '1px solid rgba(20,57,31,0.12)',
          borderRadius: 999,
          padding: '10px 14px',
          fontSize: 13, fontWeight: 600, color: '#14391f',
          cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 8,
        }}>
          <HIcon name="user" size={14} color="#14391f" />
          Mit Test-Hash B anmelden
        </button>
        <button style={{
          background: 'rgba(196,78,78,0.06)',
          border: '1px solid rgba(196,78,78,0.3)',
          borderRadius: 999,
          padding: '10px 14px',
          fontSize: 13, fontWeight: 600, color: '#c44e4e',
          cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 8,
        }}>
          <HIcon name="logout" size={14} color="#c44e4e" />
          Debug-Login abmelden
        </button>
      </div>
      <div style={{ marginTop: 12, fontSize: 11, color: '#9aa295', fontStyle: 'italic' }}>
        Nur im Debug-Modus sichtbar.
      </div>
    </div>
  );
}

// ─── Screen ───────────────────────────────────────────────────
function HScreen({ showDebug = true } = {}) {
  return (
    <div className="mp-screen" style={{ background: 'white' }}>
      <div className="mp-feed" style={{ flex: 1, overflowY: 'auto', padding: '0 0 32px' }}>
        <HTopBar />
        <HLogoBlock />

        {/* Action cards */}
        <div style={{
          display: 'flex', flexDirection: 'column', gap: 12,
          padding: '24px 14px 0',
        }}>
          {/* 1 · Feed (Hero — full width, top) */}
          <HHeroCard
            icon="sparkles"
            title="Feed"
            subtitle="Community, Trends & neue Rezepte"
          />

          {/* 2 · Two squares side by side: Upload + Mein Bereich */}
          <div style={{
            display: 'grid',
            gridTemplateColumns: '1fr 1fr',
            gap: 12,
          }}>
            <HSquareCard
              icon="finger"
              title="Hochladen"
              subtitle="Werde Creator"
              badge="Orb"
              decorIcon="shieldCheck"
              palette={{
                bg: 'linear-gradient(135deg, #5fa052 0%, #3f7536 100%)',
                fg: 'white',
                iconBg: 'rgba(255,255,255,0.16)',
                iconFg: 'white',
                shadow: '0 12px 28px rgba(63,117,54,0.32)',
              }}
            />
            <HSquareCard
              icon="user"
              title="Mein Bereich"
              subtitle="Pläne & Listen"
              decorIcon="idCard"
              palette={{
                bg: 'linear-gradient(135deg, #2f6a3a 0%, #1a3a23 100%)',
                fg: 'white',
                iconBg: 'rgba(255,255,255,0.16)',
                iconFg: 'white',
                shadow: '0 12px 28px rgba(20,57,31,0.22)',
              }}
            />
          </div>

          {/* 3 · Marketplace (full width, bottom) */}
          <HActionCard
            icon="shop"
            title="Marktplatz"
            subtitle="Diätpläne kaufen & verkaufen"
            decorIcon="shop"
            palette={{
              bg: 'linear-gradient(135deg, #f5b942 0%, #d99a3c 100%)',
              fg: '#3a2510',
              iconBg: 'rgba(255,255,255,0.7)',
              iconFg: '#7a4a18',
              shadow: '0 10px 24px rgba(217,154,60,0.32)',
            }}
          />
        </div>

        {/* Login CTA */}
        <div style={{ display: 'flex', justifyContent: 'center', marginTop: 24 }}>
          <button style={{
            background: 'linear-gradient(135deg, #1a3a23, #0e2415)',
            color: 'white',
            border: 'none',
            borderRadius: 999,
            padding: '12px 20px',
            fontSize: 14, fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', gap: 8,
            boxShadow: '0 8px 22px rgba(20,57,31,0.28)',
          }}>
            <HIcon name="login" size={15} color="white" />
            Jetzt einloggen
          </button>
        </div>

        {showDebug && <HDebugBlock />}

        <div style={{
          marginTop: 18, padding: '0 18px',
          textAlign: 'center',
          fontSize: 10, color: '#c8cec5',
          fontFamily: 'ui-monospace, monospace',
        }}>
          v2.4.1 · build 2026.05.14
        </div>
      </div>
    </div>
  );
}

window.HScreen = HScreen;
