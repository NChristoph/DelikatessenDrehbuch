/* Notification Overlay — Bottom Sheet (2/3 height) over Feed background */

const { useState: useStateN } = React;

const NOTIFICATIONS = [
  { id: 1, type: 'ai', title: 'AI Vorschau ist fertig', body: 'Schweinebraten mit Linsenpüree und Ofengemüse', time: '09:16', unread: true, kcal: 680, img: 'https://images.unsplash.com/photo-1544025162-d76694265947?w=200&q=80' },
  { id: 2, type: 'ai', title: 'AI Vorschau ist fertig', body: 'Linsen-Spinat Suppe mit Eiern', time: '19:20', unread: true, kcal: 420, img: 'https://images.unsplash.com/photo-1547592180-85f173990554?w=200&q=80' },
  { id: 3, type: 'ai', title: 'AI Vorschau ist fertig', body: 'Rinderbraten mit dunkler Biersauce und Ofengemüse', time: '21:32', unread: false, kcal: 720, img: 'https://images.unsplash.com/photo-1546964124-0cce460f38ef?w=200&q=80' },
  { id: 4, type: 'plan', title: 'Wochenplan aktualisiert', body: '3 neue Rezepte für deinen High-Protein Cut', time: 'Gestern', unread: false },
  { id: 5, type: 'system', title: 'Streak gehalten — 7 Tage', body: 'Du hast 7 Tage in Folge geplant.', time: 'Gestern', unread: false },
];

function NotificationPanel() {
  const [filter, setFilter] = useStateN('all');
  const [grouping, setGrouping] = useStateN(true);

  const filtered = filter === 'ai' ? NOTIFICATIONS.filter(n => n.type === 'ai') : NOTIFICATIONS;
  const unreadCount = NOTIFICATIONS.filter(n => n.unread).length;

  return (
    <div style={{
      width: '100%',
      height: '100%',
      position: 'relative',
      overflow: 'hidden',
      fontFamily: "'Inter', sans-serif",
      color: '#1a2e20',
    }}>
      {/* Background: simulated feed (Reel-style food video card) */}
      <FakeFeedBg />

      {/* Dim/scrim layer */}
      <div style={{
        position: 'absolute', inset: 0,
        background: 'linear-gradient(180deg, rgba(20,57,31,0.18) 0%, rgba(20,57,31,0.35) 60%, rgba(20,57,31,0.55) 100%)',
        backdropFilter: 'blur(2px)',
        WebkitBackdropFilter: 'blur(2px)',
      }}/>

      {/* Bottom-sheet overlay — ~67% of height */}
      <div style={{
        position: 'absolute',
        bottom: 0, left: 0, right: 0,
        height: '67%',
        background: 'var(--bg-cream)',
        borderTopLeftRadius: 28,
        borderTopRightRadius: 28,
        boxShadow: '0 -12px 40px rgba(20,57,31,0.25), 0 -2px 0 rgba(255,255,255,0.5) inset',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
      }}>
        {/* Drag handle */}
        <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 8, paddingBottom: 4, flexShrink: 0 }}>
          <span style={{
            width: 38, height: 4, borderRadius: 999,
            background: 'rgba(20,57,31,0.18)',
          }}/>
        </div>

        {/* Header */}
        <div style={{ padding: '8px 20px 14px', display: 'flex', alignItems: 'flex-start', gap: 10, flexShrink: 0 }}>
          <div style={{ flex: 1 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <h1 style={{
                fontFamily: "'Fraunces', serif",
                fontSize: 22,
                fontWeight: 600,
                letterSpacing: '-0.02em',
                margin: 0,
                color: '#14391f',
                lineHeight: 1.1,
              }}>Benachrichtigungen</h1>
              {unreadCount > 0 && (
                <span style={{
                  background: 'linear-gradient(135deg, #ff9a3c, #ff7849)',
                  color: 'white',
                  fontSize: 10,
                  fontWeight: 700,
                  padding: '2px 7px',
                  borderRadius: 999,
                }}>{unreadCount} neu</span>
              )}
            </div>
            <div style={{ fontSize: 11, color: '#6b7868', marginTop: 3 }}>AI Vorschauen & wichtige Hinweise</div>
          </div>
          <button style={{
            width: 28, height: 28, borderRadius: '50%',
            background: 'rgba(20,57,31,0.06)',
            border: 'none',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            cursor: 'pointer',
            flexShrink: 0,
          }}>
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#14391f" strokeWidth="2.4"><path d="M18 6L6 18M6 6l12 12"/></svg>
          </button>
        </div>

        {/* Action row */}
        <div style={{
          margin: '0 16px 10px',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          flexShrink: 0,
        }}>
          <button style={{
            background: 'white',
            border: '1px solid rgba(20,57,31,0.12)',
            borderRadius: 10,
            padding: '7px 11px',
            fontSize: 11,
            fontWeight: 600,
            color: '#14391f',
            cursor: 'pointer',
            fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', gap: 5,
          }}>
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M5 13l4 4L19 7"/></svg>
            Alles gelesen
          </button>
          <button style={{
            background: 'white',
            border: '1px solid rgba(255,120,73,0.28)',
            borderRadius: 10,
            padding: '7px 11px',
            fontSize: 11,
            fontWeight: 600,
            color: '#ff7849',
            cursor: 'pointer',
            fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', gap: 5,
          }}>
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M3 6h18M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2m3 0v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6"/></svg>
            Leeren
          </button>
          <button style={{
            marginLeft: 'auto',
            background: 'linear-gradient(135deg, #ff9a3c, #ff7849)',
            border: 'none',
            borderRadius: 10,
            padding: '7px 12px',
            fontSize: 11,
            fontWeight: 700,
            color: 'white',
            cursor: 'pointer',
            fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', gap: 5,
            boxShadow: '0 3px 10px rgba(255,120,73,0.3)',
          }}>
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5"><rect x="3" y="4" width="18" height="18" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/></svg>
            Plan öffnen
          </button>
        </div>

        {/* Filter / Group row */}
        <div style={{
          margin: '0 16px 10px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexShrink: 0,
        }}>
          <div style={{
            display: 'inline-flex',
            background: 'rgba(20,57,31,0.06)',
            border: '1px solid rgba(20,57,31,0.1)',
            borderRadius: 999,
            padding: 3,
            gap: 2,
          }}>
            {[
              { id: 'all', label: `Alle · ${NOTIFICATIONS.length}` },
              { id: 'ai', label: 'Nur AI' },
            ].map(t => (
              <button key={t.id}
                onClick={() => setFilter(t.id)}
                style={{
                  background: filter === t.id ? '#14391f' : 'transparent',
                  color: filter === t.id ? 'white' : '#14391f',
                  border: 'none',
                  borderRadius: 999,
                  padding: '5px 11px',
                  fontSize: 11,
                  fontWeight: 600,
                  cursor: 'pointer',
                  fontFamily: 'inherit',
                  transition: 'all .2s',
                }}>{t.label}</button>
            ))}
          </div>

          <button
            onClick={() => setGrouping(!grouping)}
            style={{
              background: 'transparent',
              border: 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 7,
              cursor: 'pointer',
              fontFamily: 'inherit',
              color: '#14391f',
              fontSize: 11,
              fontWeight: 600,
            }}>
            <span style={{
              width: 28, height: 16, borderRadius: 999,
              background: grouping ? '#14391f' : 'rgba(20,57,31,0.2)',
              position: 'relative',
              transition: 'background .2s',
            }}>
              <span style={{
                position: 'absolute',
                top: 2, left: grouping ? 14 : 2,
                width: 12, height: 12,
                borderRadius: '50%',
                background: 'white',
                transition: 'left .2s',
                boxShadow: '0 1px 3px rgba(0,0,0,0.15)',
              }}/>
            </span>
            Gruppieren
          </button>
        </div>

        {/* List */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '0 14px 20px' }}>
          {grouping && filter === 'all' && (
            <SectionHeader label="Heute" count={filtered.filter(n => /^\d/.test(n.time)).length} />
          )}
          {filtered.filter(n => grouping && filter === 'all' ? /^\d/.test(n.time) : true).map(n => (
            <NotificationCard key={n.id} n={n} />
          ))}
          {grouping && filter === 'all' && (
            <>
              <SectionHeader label="Früher" count={filtered.filter(n => !/^\d/.test(n.time)).length} />
              {filtered.filter(n => !/^\d/.test(n.time)).map(n => (
                <NotificationCard key={n.id} n={n} />
              ))}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function FakeFeedBg() {
  return (
    <div style={{
      position: 'absolute', inset: 0,
      background: '#000',
      overflow: 'hidden',
    }}>
      {/* Reel video bg */}
      <div style={{
        position: 'absolute', inset: 0,
        background: `url(https://images.unsplash.com/photo-1547592180-85f173990554?w=600&q=80) center/cover`,
      }}/>
      {/* Top tabs */}
      <div style={{
        position: 'absolute', top: 18, left: 0, right: 0,
        display: 'flex', justifyContent: 'center', gap: 14,
        color: 'rgba(255,255,255,0.85)',
        fontSize: 13, fontWeight: 600,
      }}>
        <span style={{ opacity: 0.6 }}>Creator</span>
        <span style={{ borderBottom: '2px solid white', paddingBottom: 2 }}>Feed</span>
        <span style={{ opacity: 0.6 }}>Marktplatz</span>
      </div>
      {/* Like button */}
      <div style={{
        position: 'absolute', top: 60, left: 16,
        width: 44, height: 44, borderRadius: '50%',
        background: 'rgba(255,255,255,0.18)',
        backdropFilter: 'blur(10px)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <svg width="20" height="20" viewBox="0 0 24 24" fill="#ff7849" stroke="#ff7849" strokeWidth="2"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>
      </div>
    </div>
  );
}

function SectionHeader({ label, count }) {
  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      padding: '4px 4px 8px',
    }}>
      <span style={{
        fontSize: 10,
        fontWeight: 700,
        color: '#14391f',
        textTransform: 'uppercase',
        letterSpacing: '0.06em',
      }}>{label}</span>
      <span style={{
        fontSize: 10,
        fontWeight: 600,
        color: '#6b7868',
        background: 'rgba(20,57,31,0.06)',
        padding: '1px 7px',
        borderRadius: 999,
      }}>{count}</span>
      <span style={{ flex: 1, height: 1, background: 'rgba(20,57,31,0.08)' }}/>
    </div>
  );
}

function NotificationCard({ n }) {
  const isAI = n.type === 'ai';
  const isPlan = n.type === 'plan';

  return (
    <article style={{
      background: 'white',
      borderRadius: 16,
      padding: 12,
      marginBottom: 8,
      boxShadow: '0 3px 10px rgba(20, 57, 31, 0.05)',
      position: 'relative',
      overflow: 'hidden',
      display: 'flex',
      gap: 11,
      cursor: 'pointer',
    }}>
      {n.unread && (
        <div style={{
          position: 'absolute',
          top: 0, bottom: 0, left: 0,
          width: 3,
          background: 'linear-gradient(180deg, #ff9a3c, #ff7849)',
        }}/>
      )}

      {n.img ? (
        <div style={{
          width: 50, height: 50,
          borderRadius: 11,
          background: `url(${n.img}) center/cover`,
          flexShrink: 0,
          position: 'relative',
        }}>
          {isAI && (
            <span style={{
              position: 'absolute',
              top: -3, right: -3,
              width: 18, height: 18,
              borderRadius: '50%',
              background: 'linear-gradient(135deg, #14391f, #1a4a28)',
              border: '2px solid white',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 9,
              color: '#f5b942',
            }}>✦</span>
          )}
        </div>
      ) : (
        <div style={{
          width: 50, height: 50,
          borderRadius: 11,
          background: isPlan ? 'linear-gradient(135deg, #5fa052, #3a7a30)' : 'linear-gradient(135deg, #f5b942, #c8a45c)',
          flexShrink: 0,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: 'white',
        }}>
          {isPlan ? (
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2"><rect x="3" y="4" width="18" height="18" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/></svg>
          ) : <span style={{ fontSize: 18 }}>🔥</span>}
        </div>
      )}

      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 3 }}>
          {isAI && (
            <span style={{
              background: 'linear-gradient(135deg, #14391f, #1a4a28)',
              color: 'white',
              padding: '2px 7px',
              borderRadius: 999,
              fontSize: 9,
              fontWeight: 700,
              letterSpacing: '0.04em',
              textTransform: 'uppercase',
              display: 'inline-flex', alignItems: 'center', gap: 3,
            }}>
              <span style={{ color: '#f5b942' }}>✦</span> AI
            </span>
          )}
          {isPlan && (
            <span style={{
              background: 'rgba(95,160,82,0.12)',
              color: '#3a7a30',
              padding: '2px 7px',
              borderRadius: 999,
              fontSize: 9,
              fontWeight: 700,
              letterSpacing: '0.04em',
              textTransform: 'uppercase',
            }}>Plan</span>
          )}
          <span style={{
            fontSize: 11,
            fontWeight: 600,
            color: '#14391f',
            flex: 1,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
          }}>{n.title}</span>
          <span style={{ fontSize: 10, color: '#6b7868', flexShrink: 0 }}>{n.time}</span>
        </div>
        <div style={{
          fontSize: 12,
          color: '#1a2e20',
          fontWeight: 500,
          lineHeight: 1.35,
          marginBottom: 8,
        }}>{n.body}</div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
          {n.kcal && (
            <span style={{
              fontSize: 10,
              fontWeight: 600,
              color: '#6b7868',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              background: '#faf6ec',
              padding: '3px 8px',
              borderRadius: 999,
            }}>
              <span style={{ color: '#ff7849' }}>🔥</span>
              {n.kcal} kcal
            </span>
          )}
          <button style={{
            marginLeft: 'auto',
            background: 'linear-gradient(135deg, #ff9a3c, #ff7849)',
            border: 'none',
            borderRadius: 9,
            padding: '5px 12px',
            fontSize: 10,
            fontWeight: 700,
            color: 'white',
            cursor: 'pointer',
            fontFamily: 'inherit',
            display: 'inline-flex', alignItems: 'center', gap: 4,
            boxShadow: '0 2px 6px rgba(255,120,73,0.3)',
          }}>
            Öffnen
            <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
          </button>
        </div>
      </div>
    </article>
  );
}

window.NotificationPanel = NotificationPanel;
