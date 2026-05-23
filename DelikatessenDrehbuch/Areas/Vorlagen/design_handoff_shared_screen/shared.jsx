/* Shared / Gruppen-Feed — Avocado-Stil, social-media feel
   - Stories-Row der Gruppen (Avatar-Carousel)
   - Hero-Banner der aktiven Gruppe (forest gradient + Member-Stack)
   - Segmentierte Tab-Bar (Feed · Chat · Aufgaben · Mitglieder)
   - Activity-Feed mit Post-Cards
   - WhatsApp-style Chat mit sticky Composer
   - Neue-Gruppe-Bottom-Sheet
*/

const { useState: sUseState } = React;

// ─── Icons ───────────────────────────────────────────────────
function SIcon({ name, size = 22, color = 'currentColor', strokeWidth = 2, fill = 'none' }) {
  const p = { width: size, height: size, viewBox: '0 0 24 24', fill, stroke: color, strokeWidth, strokeLinecap: 'round', strokeLinejoin: 'round' };
  if (name === 'back')      return <svg {...p}><polyline points="15 18 9 12 15 6"/></svg>;
  if (name === 'chev')      return <svg {...p}><polyline points="6 9 12 15 18 9"/></svg>;
  if (name === 'chevR')     return <svg {...p}><polyline points="9 6 15 12 9 18"/></svg>;
  if (name === 'plus')      return <svg {...p}><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>;
  if (name === 'shield')    return <svg {...p}><path d="M12 3l8 3v6c0 4-3 8-8 9-5-1-8-5-8-9V6l8-3z"/><polyline points="9 12 11 14 15 10"/></svg>;
  if (name === 'link')      return <svg {...p}><path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1"/><path d="M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1"/></svg>;
  if (name === 'download')  return <svg {...p}><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>;
  if (name === 'send')      return <svg {...p}><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg>;
  if (name === 'send-fill') return <svg width={size} height={size} viewBox="0 0 24 24" fill={color}><path d="M2.5 11.5L21 3 12.5 21.5l-2-9-8-1z"/></svg>;
  if (name === 'check')     return <svg {...p}><polyline points="20 6 9 17 4 12"/></svg>;
  if (name === 'circle')    return <svg {...p}><circle cx="12" cy="12" r="9"/></svg>;
  if (name === 'list')      return <svg {...p}><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><circle cx="3.5" cy="6" r="1.5"/><circle cx="3.5" cy="12" r="1.5"/><circle cx="3.5" cy="18" r="1.5"/></svg>;
  if (name === 'msg')       return <svg {...p}><path d="M21 11.5a8.4 8.4 0 0 1-9 8.4 8.5 8.5 0 0 1-3.5-.7L3 21l1.8-5.5A8.4 8.4 0 0 1 21 11.5z"/></svg>;
  if (name === 'feed')      return <svg {...p}><rect x="3" y="4" width="18" height="6" rx="2"/><rect x="3" y="14" width="18" height="6" rx="2"/></svg>;
  if (name === 'users')     return <svg {...p}><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>;
  if (name === 'heart')     return <svg {...p}><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>;
  if (name === 'comment')   return <svg {...p}><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>;
  if (name === 'more')      return <svg {...p}><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/><circle cx="5" cy="12" r="1"/></svg>;
  if (name === 'copy')      return <svg {...p}><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>;
  if (name === 'emoji')     return <svg {...p}><circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" y1="9" x2="9.01" y2="9"/><line x1="15" y1="9" x2="15.01" y2="9"/></svg>;
  if (name === 'attach')    return <svg {...p}><path d="M21.44 11.05L12.25 20.24a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg>;
  return null;
}

// ─── Avatar with initials ────────────────────────────────────
const AVATAR_COLORS = [
  ['#5fa052', '#3f7536'], ['#f5b942', '#d99a3c'],
  ['#ff7849', '#c44e4e'], ['#2f6a3a', '#14391f'],
  ['#7a4a18', '#3a2510'], ['#8a6418', '#5a3d12'],
];
function SAvatar({ name = '?', size = 36, ring }) {
  const initials = name.split(' ').map(s => s[0]).slice(0,2).join('').toUpperCase();
  const idx = (name.charCodeAt(0) + (name.charCodeAt(1)||0)) % AVATAR_COLORS.length;
  const [a, b] = AVATAR_COLORS[idx];
  return (
    <div style={{
      width: size, height: size, borderRadius: '50%',
      background: `linear-gradient(135deg, ${a}, ${b})`,
      color: 'white',
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: "'Fraunces', serif",
      fontWeight: 700, fontSize: size * 0.4,
      letterSpacing: '-0.02em',
      flexShrink: 0,
      boxShadow: ring ? `0 0 0 2px white, 0 0 0 4px ${ring}` : 'inset 0 0 0 1px rgba(255,255,255,0.2)',
    }}>{initials}</div>
  );
}

// ─── Top bar ─────────────────────────────────────────────────
function STopBar() {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '14px 16px 8px',
    }}>
      <button style={{
        display: 'inline-flex', alignItems: 'center', gap: 4,
        background: 'white',
        border: '1px solid rgba(20,57,31,0.08)',
        borderRadius: 999,
        padding: '7px 14px 7px 10px',
        fontSize: 13, fontWeight: 600, color: '#14391f',
        cursor: 'pointer', fontFamily: 'inherit',
        boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
      }}>
        <SIcon name="back" size={15} color="#14391f" strokeWidth={2.5} />
        Home
      </button>
      <div style={{
        textAlign: 'center', flex: 1, marginTop: 2,
      }}>
        <div style={{
          fontSize: 10, fontWeight: 700,
          letterSpacing: '0.18em', textTransform: 'uppercase',
          color: '#9aa295',
        }}>Gruppen</div>
        <div style={{
          fontFamily: "'Fraunces', serif",
          fontSize: 18, fontWeight: 700,
          color: '#14391f',
          letterSpacing: '-0.02em',
          lineHeight: 1.1, marginTop: 1,
        }}>Geteilte Inhalte</div>
      </div>
      <button style={{
        display: 'inline-flex', alignItems: 'center', gap: 4,
        background: 'white',
        border: '1px solid rgba(20,57,31,0.08)',
        borderRadius: 999,
        padding: '7px 10px',
        fontSize: 11, fontWeight: 700, color: '#14391f',
        letterSpacing: '0.06em',
        cursor: 'pointer', fontFamily: 'inherit',
        boxShadow: '0 2px 8px rgba(20,57,31,0.05)',
      }}>
        DE <SIcon name="chev" size={11} color="#14391f" strokeWidth={2.5} />
      </button>
    </div>
  );
}

// ─── Stories-Row (Gruppen-Carousel) ──────────────────────────
function SStoriesRow({ groups, activeId, onSelect, onCreate }) {
  return (
    <div style={{
      padding: '8px 0 14px',
      overflowX: 'auto',
      WebkitOverflowScrolling: 'touch',
    }}>
      <div style={{
        display: 'flex', gap: 14,
        padding: '0 16px',
        width: 'max-content',
      }}>
        {/* Create-new tile */}
        <button onClick={onCreate} style={{
          display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
          background: 'none', border: 'none', cursor: 'pointer', fontFamily: 'inherit',
          width: 64, flexShrink: 0,
        }}>
          <div style={{
            width: 56, height: 56, borderRadius: '50%',
            background: '#faf6ec',
            border: '2px dashed rgba(20,57,31,0.25)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            color: '#14391f',
          }}>
            <SIcon name="plus" size={22} color="#14391f" strokeWidth={2.5} />
          </div>
          <div style={{
            fontSize: 10, fontWeight: 600, color: '#6b7868',
            textAlign: 'center', lineHeight: 1.1,
          }}>Neu</div>
        </button>

        {groups.map(g => {
          const active = g.id === activeId;
          return (
            <button key={g.id} onClick={() => onSelect(g.id)} style={{
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
              background: 'none', border: 'none', cursor: 'pointer', fontFamily: 'inherit',
              width: 64, flexShrink: 0,
            }}>
              <div style={{
                position: 'relative',
                padding: active ? 2.5 : 0,
                borderRadius: '50%',
                background: active ? 'linear-gradient(135deg, #5fa052, #14391f)' : 'transparent',
              }}>
                <div style={{
                  padding: active ? 2 : 0,
                  borderRadius: '50%',
                  background: active ? 'white' : 'transparent',
                }}>
                  <SAvatar name={g.name} size={active ? 50 : 56} />
                </div>
                {g.unread > 0 && (
                  <div style={{
                    position: 'absolute',
                    top: -2, right: -2,
                    minWidth: 18, height: 18, padding: '0 5px',
                    borderRadius: 999,
                    background: '#ff7849',
                    color: 'white', fontSize: 10, fontWeight: 700,
                    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                    border: '2px solid white',
                  }}>{g.unread}</div>
                )}
              </div>
              <div style={{
                fontSize: 10, fontWeight: active ? 700 : 500,
                color: active ? '#14391f' : '#6b7868',
                textAlign: 'center', lineHeight: 1.1,
                maxWidth: 64,
                overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
              }}>{g.name}</div>
            </button>
          );
        })}
      </div>
    </div>
  );
}

// ─── Active Group Hero ───────────────────────────────────────
function SGroupHero({ group }) {
  return (
    <div style={{
      margin: '0 14px',
      background: 'linear-gradient(135deg, #5fa052 0%, #2f6a3a 55%, #14391f 100%)',
      borderRadius: 24,
      padding: '20px 18px',
      color: 'white',
      position: 'relative', overflow: 'hidden',
      boxShadow: '0 14px 32px rgba(20,57,31,0.28)',
    }}>
      {/* Goldglow decorative */}
      <div style={{
        position: 'absolute', top: -50, right: -30,
        width: 180, height: 180,
        borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(245,233,160,0.28) 0%, rgba(245,233,160,0) 70%)',
        pointerEvents: 'none',
      }} />
      <div style={{
        display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
        gap: 12, position: 'relative', zIndex: 1,
      }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 6,
            background: 'rgba(255,255,255,0.16)',
            padding: '3px 9px 3px 7px',
            borderRadius: 999,
            fontSize: 9.5, fontWeight: 700,
            letterSpacing: '0.12em', textTransform: 'uppercase',
            backdropFilter: 'blur(6px)',
          }}>
            <SIcon name="shield" size={11} color="#f5e9a0" strokeWidth={2.4} />
            Aktive Gruppe
          </div>
          <h2 style={{
            margin: '10px 0 0',
            fontFamily: "'Fraunces', serif",
            fontSize: 26, fontWeight: 700,
            letterSpacing: '-0.025em',
            lineHeight: 1.05,
          }}>{group.name}</h2>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 8 }}>
            {/* Stacked avatars */}
            <div style={{ display: 'flex' }}>
              {group.members.slice(0,4).map((m,i) => (
                <div key={i} style={{ marginLeft: i === 0 ? 0 : -8 }}>
                  <SAvatar name={m} size={22} ring="rgba(20,57,31,0.4)" />
                </div>
              ))}
            </div>
            <div style={{ fontSize: 12, opacity: 0.9, fontWeight: 500 }}>
              {group.members.length} Mitglieder · {group.posts} Beiträge
            </div>
          </div>
        </div>
        <button style={{
          background: 'rgba(255,255,255,0.95)',
          border: 'none', borderRadius: 14,
          padding: '10px 12px',
          fontSize: 11.5, fontWeight: 700, color: '#14391f',
          cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', gap: 6,
          flexShrink: 0,
          boxShadow: '0 4px 12px rgba(0,0,0,0.18)',
        }}>
          <SIcon name="link" size={13} color="#14391f" strokeWidth={2.4} />
          Einladen
        </button>
      </div>
      {/* Quick-stats row */}
      <div style={{
        display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)',
        gap: 8, marginTop: 14,
        position: 'relative', zIndex: 1,
      }}>
        {[
          { label: 'Mitglieder', value: group.members.length },
          { label: 'Beiträge', value: group.posts },
          { label: 'Aufgaben', value: group.tasks ?? 0 },
        ].map((s,i) => (
          <div key={i} style={{
            background: 'rgba(255,255,255,0.10)',
            border: '1px solid rgba(255,255,255,0.14)',
            borderRadius: 12, padding: '8px 10px',
            backdropFilter: 'blur(6px)',
          }}>
            <div style={{
              fontFamily: "'Fraunces', serif",
              fontSize: 18, fontWeight: 700, lineHeight: 1,
            }}>{s.value}</div>
            <div style={{
              fontSize: 9, fontWeight: 600, opacity: 0.75,
              letterSpacing: '0.08em', textTransform: 'uppercase',
              marginTop: 3,
            }}>{s.label}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Tab Bar ─────────────────────────────────────────────────
function STabBar({ tab, setTab }) {
  const tabs = [
    { id: 'feed', label: 'Feed', icon: 'feed' },
    { id: 'chat', label: 'Chat', icon: 'msg', badge: 4 },
    { id: 'tasks', label: 'Aufgaben', icon: 'list' },
    { id: 'members', label: 'Mitglieder', icon: 'users' },
  ];
  return (
    <div style={{
      margin: '20px 14px 16px',
      background: 'rgba(20,57,31,0.06)',
      borderRadius: 14,
      padding: 4,
      display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)',
      gap: 2,
    }}>
      {tabs.map(t => {
        const active = t.id === tab;
        return (
          <button key={t.id} onClick={() => setTab(t.id)} style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 3,
            background: active ? 'white' : 'transparent',
            border: 'none', borderRadius: 11,
            padding: '8px 4px',
            cursor: 'pointer', fontFamily: 'inherit',
            color: active ? '#14391f' : '#6b7868',
            boxShadow: active ? '0 2px 8px rgba(20,57,31,0.10)' : 'none',
            position: 'relative',
          }}>
            <SIcon name={t.icon} size={17} color={active ? '#14391f' : '#6b7868'} strokeWidth={active ? 2.4 : 2} />
            <div style={{
              fontSize: 10, fontWeight: active ? 700 : 600,
              letterSpacing: '0.04em',
            }}>{t.label}</div>
            {t.badge && (
              <div style={{
                position: 'absolute',
                top: 4, right: 8,
                minWidth: 14, height: 14, padding: '0 4px',
                borderRadius: 999,
                background: '#ff7849', color: 'white',
                fontSize: 8.5, fontWeight: 700,
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              }}>{t.badge}</div>
            )}
          </button>
        );
      })}
    </div>
  );
}

// ─── Feed Post Card ──────────────────────────────────────────
function SPostCard({ post }) {
  return (
    <article style={{
      margin: '0 14px 12px',
      background: 'white',
      borderRadius: 18,
      padding: 14,
      boxShadow: '0 4px 14px rgba(20,57,31,0.06)',
      border: '1px solid rgba(20,57,31,0.04)',
    }}>
      <header style={{
        display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10,
      }}>
        <SAvatar name={post.author} size={36} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6,
            fontFamily: "'Fraunces', serif",
            fontSize: 14, fontWeight: 700, color: '#14391f',
          }}>
            {post.author}
            {post.role === 'OWNER' && (
              <span style={{
                fontSize: 8, fontWeight: 700, color: '#5fa052',
                letterSpacing: '0.12em',
                background: 'rgba(95,160,82,0.12)',
                padding: '2px 6px', borderRadius: 4,
              }}>OWNER</span>
            )}
          </div>
          <div style={{ fontSize: 11, color: '#9aa295', marginTop: 1 }}>
            {post.time} · {post.action}
          </div>
        </div>
        <button style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#9aa295', padding: 4 }}>
          <SIcon name="more" size={18} color="#9aa295" />
        </button>
      </header>

      {/* Post payload */}
      {post.type === 'mealplan' && (
        <div style={{
          background: '#faf6ec',
          borderRadius: 14,
          padding: 12,
          display: 'flex', alignItems: 'center', gap: 12,
        }}>
          <div style={{
            width: 48, height: 48, borderRadius: 12,
            background: 'linear-gradient(135deg, #5fa052, #3f7536)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            color: 'white',
          }}>
            <SIcon name="download" size={22} color="white" strokeWidth={2.2} />
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{
              fontFamily: "'Fraunces', serif",
              fontSize: 15, fontWeight: 600, color: '#14391f',
              letterSpacing: '-0.01em',
            }}>{post.title}</div>
            <div style={{ fontSize: 11, color: '#6b7868', marginTop: 2 }}>
              {post.subtitle}
            </div>
          </div>
          <button style={{
            background: '#14391f',
            color: 'white',
            border: 'none', borderRadius: 999,
            padding: '7px 14px',
            fontSize: 11, fontWeight: 700,
            cursor: 'pointer', fontFamily: 'inherit',
          }}>Öffnen</button>
        </div>
      )}
      {post.type === 'text' && (
        <div style={{
          fontSize: 14, color: '#1a2e20', lineHeight: 1.45,
          textWrap: 'pretty',
        }}>{post.text}</div>
      )}
      {post.type === 'task' && (
        <div style={{
          background: '#faf6ec',
          borderRadius: 14,
          padding: 12,
          display: 'flex', alignItems: 'center', gap: 12,
        }}>
          <div style={{
            width: 22, height: 22, borderRadius: 6,
            background: post.done ? '#5fa052' : 'white',
            border: post.done ? 'none' : '2px solid rgba(20,57,31,0.2)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            flexShrink: 0,
          }}>
            {post.done && <SIcon name="check" size={14} color="white" strokeWidth={3} />}
          </div>
          <div style={{
            flex: 1,
            fontSize: 13, fontWeight: 600,
            color: post.done ? '#9aa295' : '#14391f',
            textDecoration: post.done ? 'line-through' : 'none',
          }}>{post.task}</div>
        </div>
      )}

      {/* Reactions */}
      <footer style={{
        display: 'flex', alignItems: 'center', gap: 14,
        marginTop: 12,
        paddingTop: 10,
        borderTop: '1px solid rgba(20,57,31,0.06)',
      }}>
        <button style={{
          display: 'inline-flex', alignItems: 'center', gap: 5,
          background: 'none', border: 'none', cursor: 'pointer',
          color: post.liked ? '#ff7849' : '#6b7868',
          fontFamily: 'inherit', padding: 0,
        }}>
          <SIcon name="heart" size={16} color={post.liked ? '#ff7849' : '#6b7868'} fill={post.liked ? '#ff7849' : 'none'} strokeWidth={2.2} />
          <span style={{ fontSize: 12, fontWeight: 600 }}>{post.likes}</span>
        </button>
        <button style={{
          display: 'inline-flex', alignItems: 'center', gap: 5,
          background: 'none', border: 'none', cursor: 'pointer',
          color: '#6b7868',
          fontFamily: 'inherit', padding: 0,
        }}>
          <SIcon name="comment" size={16} color="#6b7868" strokeWidth={2.2} />
          <span style={{ fontSize: 12, fontWeight: 600 }}>{post.comments}</span>
        </button>
        {post.reactions && (
          <div style={{
            marginLeft: 'auto',
            display: 'flex',
            background: '#faf6ec',
            borderRadius: 999,
            padding: '3px 8px',
            fontSize: 12,
            gap: 2,
          }}>
            {post.reactions.map((r,i) => <span key={i}>{r}</span>)}
            <span style={{ fontSize: 10, color: '#6b7868', fontWeight: 600, marginLeft: 3 }}>
              {post.reactionCount}
            </span>
          </div>
        )}
      </footer>
    </article>
  );
}

// ─── Feed Body ───────────────────────────────────────────────
function SFeedBody({ posts }) {
  return (
    <div>
      {/* Composer trigger */}
      <button style={{
        margin: '0 14px 16px',
        width: 'calc(100% - 28px)',
        display: 'flex', alignItems: 'center', gap: 12,
        background: 'white',
        border: '1px solid rgba(20,57,31,0.08)',
        borderRadius: 16,
        padding: '12px 14px',
        cursor: 'pointer', fontFamily: 'inherit',
        textAlign: 'left',
        boxShadow: '0 2px 8px rgba(20,57,31,0.04)',
      }}>
        <SAvatar name="Avocado" size={34} />
        <div style={{ flex: 1, fontSize: 13, color: '#9aa295' }}>
          Was möchtest du teilen?
        </div>
        <div style={{
          display: 'inline-flex', alignItems: 'center', gap: 4,
          fontSize: 11, fontWeight: 700,
          color: '#5fa052',
          background: 'rgba(95,160,82,0.10)',
          padding: '5px 10px', borderRadius: 999,
        }}>
          <SIcon name="download" size={12} color="#5fa052" strokeWidth={2.4} />
          Plan
        </div>
      </button>

      {posts.map((p,i) => <SPostCard key={i} post={p} />)}
    </div>
  );
}

// ─── Chat Body ───────────────────────────────────────────────
function SChatMessage({ msg }) {
  const isSelf = msg.self;
  return (
    <div style={{
      display: 'flex', flexDirection: isSelf ? 'row-reverse' : 'row',
      gap: 8, alignItems: 'flex-end',
      marginBottom: 12,
      paddingLeft: isSelf ? 40 : 0, paddingRight: isSelf ? 0 : 40,
    }}>
      {!isSelf && <SAvatar name={msg.author} size={30} />}
      <div style={{ minWidth: 0, maxWidth: '100%' }}>
        {!isSelf && (
          <div style={{
            fontSize: 11, fontWeight: 700, color: '#14391f',
            marginBottom: 3,
            marginLeft: 10,
          }}>{msg.author} <span style={{ fontWeight: 500, color: '#9aa295', marginLeft: 4 }}>{msg.time}</span></div>
        )}
        <div style={{
          background: isSelf ? 'linear-gradient(135deg, #2f6a3a, #14391f)' : 'white',
          color: isSelf ? 'white' : '#1a2e20',
          padding: msg.kind === 'sticker' ? '8px 12px' : '10px 14px',
          borderRadius: isSelf ? '18px 18px 4px 18px' : '18px 18px 18px 4px',
          boxShadow: isSelf ? '0 4px 12px rgba(20,57,31,0.22)' : '0 2px 8px rgba(20,57,31,0.06)',
          fontSize: msg.kind === 'sticker' ? 22 : 13.5,
          fontWeight: 500,
          lineHeight: 1.35,
          display: 'inline-block',
          maxWidth: '100%',
          wordBreak: 'break-word',
        }}>
          {msg.text}
        </div>
        {msg.reaction && (
          <div style={{
            marginTop: -8,
            marginLeft: isSelf ? 'auto' : 10,
            marginRight: isSelf ? 4 : 0,
            display: 'inline-block',
            background: 'white',
            border: '1px solid rgba(20,57,31,0.06)',
            borderRadius: 999,
            padding: '2px 8px',
            fontSize: 12,
            boxShadow: '0 2px 6px rgba(20,57,31,0.08)',
            position: 'relative',
          }}>{msg.reaction} <span style={{ fontSize: 10, color: '#6b7868', fontWeight: 600 }}>1</span></div>
        )}
        {isSelf && (
          <div style={{
            fontSize: 10, color: '#9aa295',
            textAlign: 'right', marginTop: 3, marginRight: 4,
            display: 'inline-flex', alignItems: 'center', gap: 3,
            float: 'right',
          }}>
            {msg.time}
            <SIcon name="check" size={11} color="#5fa052" strokeWidth={2.5} />
          </div>
        )}
      </div>
    </div>
  );
}

function SDayDivider({ label }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      margin: '8px 0 14px',
    }}>
      <div style={{ flex: 1, height: 1, background: 'rgba(20,57,31,0.08)' }} />
      <div style={{
        fontSize: 9.5, fontWeight: 700, color: '#9aa295',
        letterSpacing: '0.16em', textTransform: 'uppercase',
      }}>{label}</div>
      <div style={{ flex: 1, height: 1, background: 'rgba(20,57,31,0.08)' }} />
    </div>
  );
}

function SChatBody({ messages }) {
  return (
    <div style={{ padding: '0 14px 8px' }}>
      <SDayDivider label="Heute" />
      {messages.map((m,i) => <SChatMessage key={i} msg={m} />)}
    </div>
  );
}

function SChatComposer() {
  return (
    <div style={{
      position: 'sticky', bottom: 0,
      background: 'rgba(245, 239, 225, 0.92)',
      backdropFilter: 'blur(12px)',
      padding: '10px 14px 14px',
      borderTop: '1px solid rgba(20,57,31,0.06)',
      display: 'flex', alignItems: 'center', gap: 8,
      zIndex: 5,
    }}>
      <button style={{
        width: 38, height: 38, borderRadius: '50%',
        background: 'white',
        border: '1px solid rgba(20,57,31,0.08)',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        color: '#14391f', cursor: 'pointer', flexShrink: 0,
        boxShadow: '0 2px 8px rgba(20,57,31,0.04)',
      }}>
        <SIcon name="attach" size={17} color="#14391f" strokeWidth={2} />
      </button>
      <div style={{
        flex: 1,
        background: 'white',
        border: '1px solid rgba(20,57,31,0.08)',
        borderRadius: 999,
        padding: '9px 14px 9px 16px',
        display: 'flex', alignItems: 'center', gap: 8,
        boxShadow: '0 2px 8px rgba(20,57,31,0.04)',
      }}>
        <input
          placeholder="Nachricht eingeben…"
          style={{
            flex: 1, border: 'none', background: 'none', outline: 'none',
            fontFamily: 'inherit', fontSize: 13.5, color: '#1a2e20',
            minWidth: 0,
          }}
        />
        <SIcon name="emoji" size={20} color="#9aa295" strokeWidth={2} />
      </div>
      <button style={{
        width: 42, height: 42, borderRadius: '50%',
        background: 'linear-gradient(135deg, #5fa052, #14391f)',
        border: 'none',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        color: 'white', cursor: 'pointer', flexShrink: 0,
        boxShadow: '0 6px 16px rgba(20,57,31,0.28)',
      }}>
        <SIcon name="send-fill" size={18} color="white" />
      </button>
    </div>
  );
}

// ─── Tasks Body ──────────────────────────────────────────────
function STasksBody({ tasks }) {
  return (
    <div style={{ padding: '0 14px' }}>
      {/* Add task */}
      <div style={{
        display: 'flex', gap: 8, marginBottom: 14,
      }}>
        <input
          placeholder="Neue Aufgabe…"
          style={{
            flex: 1,
            background: 'white',
            border: '1px solid rgba(20,57,31,0.08)',
            borderRadius: 14,
            padding: '11px 14px',
            fontFamily: 'inherit', fontSize: 13, color: '#14391f',
            outline: 'none',
            boxShadow: '0 2px 8px rgba(20,57,31,0.04)',
          }}
        />
        <button style={{
          width: 44, height: 44, borderRadius: 14,
          background: 'linear-gradient(135deg, #5fa052, #14391f)',
          border: 'none',
          color: 'white',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          cursor: 'pointer', flexShrink: 0,
          boxShadow: '0 6px 14px rgba(20,57,31,0.22)',
        }}>
          <SIcon name="plus" size={18} color="white" strokeWidth={2.6} />
        </button>
      </div>

      {/* Progress card */}
      <div style={{
        background: 'white',
        border: '1px solid rgba(20,57,31,0.04)',
        borderRadius: 18, padding: 14,
        marginBottom: 12,
        boxShadow: '0 4px 14px rgba(20,57,31,0.05)',
      }}>
        <div style={{
          display: 'flex', alignItems: 'baseline', justifyContent: 'space-between',
        }}>
          <div>
            <div style={{
              fontSize: 10, fontWeight: 700, color: '#9aa295',
              letterSpacing: '0.16em', textTransform: 'uppercase',
            }}>Diese Woche</div>
            <div style={{
              fontFamily: "'Fraunces', serif",
              fontSize: 24, fontWeight: 700, color: '#14391f',
              letterSpacing: '-0.02em', marginTop: 3,
            }}>
              {tasks.filter(t => t.done).length}
              <span style={{ fontSize: 16, color: '#9aa295', fontWeight: 500 }}>/{tasks.length} erledigt</span>
            </div>
          </div>
          <div style={{
            fontSize: 22, fontFamily: "'Fraunces', serif",
            fontWeight: 700, color: '#5fa052',
          }}>{Math.round(tasks.filter(t=>t.done).length / tasks.length * 100)}%</div>
        </div>
        <div style={{
          marginTop: 10, height: 8, borderRadius: 999,
          background: 'rgba(20,57,31,0.08)',
          overflow: 'hidden',
        }}>
          <div style={{
            height: '100%',
            width: `${tasks.filter(t=>t.done).length / tasks.length * 100}%`,
            background: 'linear-gradient(90deg, #5fa052, #14391f)',
            borderRadius: 999,
          }} />
        </div>
      </div>

      {tasks.map((t,i) => (
        <div key={i} style={{
          display: 'flex', alignItems: 'center', gap: 12,
          background: 'white',
          border: '1px solid rgba(20,57,31,0.04)',
          borderRadius: 14,
          padding: '12px 14px',
          marginBottom: 8,
          boxShadow: '0 2px 8px rgba(20,57,31,0.04)',
        }}>
          <div style={{
            width: 22, height: 22, borderRadius: 7,
            background: t.done ? 'linear-gradient(135deg, #5fa052, #3f7536)' : 'white',
            border: t.done ? 'none' : '2px solid rgba(20,57,31,0.2)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            flexShrink: 0,
            cursor: 'pointer',
          }}>
            {t.done && <SIcon name="check" size={14} color="white" strokeWidth={3} />}
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{
              fontSize: 13.5, fontWeight: 600,
              color: t.done ? '#9aa295' : '#14391f',
              textDecoration: t.done ? 'line-through' : 'none',
            }}>{t.text}</div>
            {t.assignee && (
              <div style={{
                display: 'flex', alignItems: 'center', gap: 6, marginTop: 4,
              }}>
                <SAvatar name={t.assignee} size={16} />
                <div style={{ fontSize: 10.5, color: '#6b7868', fontWeight: 500 }}>
                  {t.assignee} · {t.due}
                </div>
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── Members Body ────────────────────────────────────────────
function SMembersBody({ members }) {
  return (
    <div style={{ padding: '0 14px' }}>
      {/* Invite hero */}
      <div style={{
        background: 'white',
        borderRadius: 18, padding: 14,
        display: 'flex', alignItems: 'center', gap: 12,
        marginBottom: 14,
        border: '1px dashed rgba(20,57,31,0.18)',
      }}>
        <div style={{
          width: 44, height: 44, borderRadius: 12,
          background: 'rgba(95,160,82,0.12)',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          color: '#5fa052',
          flexShrink: 0,
        }}>
          <SIcon name="link" size={22} color="#5fa052" strokeWidth={2.2} />
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontFamily: "'Fraunces', serif",
            fontSize: 14, fontWeight: 700, color: '#14391f',
          }}>Einladungslink</div>
          <div style={{ fontSize: 11, color: '#6b7868', marginTop: 1 }}>
            avocado.app/g/x7k2…m9p
          </div>
        </div>
        <button style={{
          background: '#14391f', color: 'white',
          border: 'none', borderRadius: 12,
          padding: '8px 12px',
          fontSize: 11, fontWeight: 700,
          cursor: 'pointer', fontFamily: 'inherit',
          display: 'inline-flex', alignItems: 'center', gap: 4,
        }}>
          <SIcon name="copy" size={12} color="white" strokeWidth={2.4} />
          Kopieren
        </button>
      </div>

      {members.map((m,i) => (
        <div key={i} style={{
          display: 'flex', alignItems: 'center', gap: 12,
          background: 'white',
          borderRadius: 14, padding: '10px 14px',
          marginBottom: 8,
          border: '1px solid rgba(20,57,31,0.04)',
          boxShadow: '0 2px 8px rgba(20,57,31,0.04)',
        }}>
          <SAvatar name={m.name} size={42} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{
              display: 'flex', alignItems: 'center', gap: 6,
              fontFamily: "'Fraunces', serif",
              fontSize: 15, fontWeight: 700, color: '#14391f',
              letterSpacing: '-0.01em',
            }}>
              {m.name}
              {m.role === 'OWNER' && (
                <span style={{
                  fontSize: 8, fontWeight: 700, color: '#5fa052',
                  letterSpacing: '0.12em',
                  background: 'rgba(95,160,82,0.12)',
                  padding: '2px 6px', borderRadius: 4,
                }}>OWNER</span>
              )}
              {m.online && (
                <span style={{
                  width: 8, height: 8, borderRadius: '50%',
                  background: '#5fa052',
                  boxShadow: '0 0 0 2px white',
                }} />
              )}
            </div>
            <div style={{ fontSize: 11, color: '#6b7868', marginTop: 1 }}>
              {m.activity}
            </div>
          </div>
          <button style={{
            background: 'none', border: 'none', cursor: 'pointer',
            color: '#9aa295', padding: 4,
          }}>
            <SIcon name="more" size={18} color="#9aa295" />
          </button>
        </div>
      ))}
    </div>
  );
}

// ─── Sample Data ─────────────────────────────────────────────
const SAMPLE_GROUPS = [
  { id: 'g1', name: 'Geteilte Inhalte', members: ['Avocado', 'Lena Berger', 'Tom Becker', 'Mira Klein'], posts: 8, tasks: 3, unread: 4 },
  { id: 'g2', name: 'WG Küche', members: ['Avocado', 'Jonas'], posts: 12, tasks: 5, unread: 2 },
  { id: 'g3', name: 'Familienplan', members: ['Avocado', 'Mama', 'Papa', 'Sarah'], posts: 24, tasks: 8, unread: 0 },
  { id: 'g4', name: 'Test', members: ['Avocado'], posts: 0, tasks: 0, unread: 0 },
  { id: 'g5', name: 'Sport-Crew', members: ['Avocado', 'Max', 'Eli'], posts: 6, tasks: 2, unread: 1 },
];

const SAMPLE_POSTS = [
  {
    type: 'mealplan',
    author: 'Avocado', role: 'OWNER', time: '14:22',
    action: 'hat einen Plan geteilt',
    title: 'High-Protein Cut · 14 Tage',
    subtitle: 'Meal Plan Token · 1.823 kcal/Tag',
    likes: 5, comments: 2,
    reactions: ['🥑','🔥'], reactionCount: 3,
  },
  {
    type: 'text',
    author: 'Lena Berger', role: 'MEMBER', time: '13:50',
    action: 'hat geschrieben',
    text: 'Ich übernehme morgen die Einkaufstour 🛒. Wer braucht noch was?',
    likes: 3, comments: 4,
    liked: true,
  },
  {
    type: 'task',
    author: 'Tom Becker', role: 'MEMBER', time: '11:30',
    action: 'hat eine Aufgabe erstellt',
    task: 'Gemüse-Box bei Hofmann abholen',
    done: true,
    likes: 2, comments: 0,
  },
  {
    type: 'text',
    author: 'Mira Klein', role: 'MEMBER', time: 'Gestern',
    action: 'hat geschrieben',
    text: 'Habe ein neues Rezept aus dem Marktplatz gespeichert — Buddha-Bowl mit Avocado-Tahini-Dressing. Probieren wir das diese Woche?',
    likes: 7, comments: 5,
    reactions: ['👍','❤️','😋'], reactionCount: 6,
  },
];

const SAMPLE_MESSAGES = [
  { author: 'Lena Berger', text: 'Moin Crew! Habt ihr den Plan schon gesehen?', time: '09:12', self: false },
  { author: 'Tom Becker', text: 'Sieht stark aus, importiere ich gleich.', time: '09:14', self: false },
  { author: 'Avocado', text: 'Wenn ihr Anpassungen wollt, sagt Bescheid — hab den Token zur Bearbeitung freigegeben.', time: '09:18', self: true },
  { author: 'Mira Klein', text: 'Mehr Hülsenfrüchte bitte 🙏', time: '09:22', self: false, reaction: '🥑' },
  { author: 'Avocado', text: '🥑🌿', time: '09:24', self: true, kind: 'sticker' },
  { author: 'Lena Berger', text: 'Können wir die Einkaufsliste teilen?', time: '09:30', self: false },
  { author: 'Avocado', text: 'Klar, lade ich gleich als geteilten Token hoch.', time: '09:31', self: true },
];

const SAMPLE_TASKS = [
  { text: 'Wochenplan importieren', done: true, assignee: 'Avocado', due: 'Mo, 19.05' },
  { text: 'Einkaufsliste teilen', done: true, assignee: 'Avocado', due: 'Mo, 19.05' },
  { text: 'Gemüse-Box bei Hofmann abholen', done: true, assignee: 'Tom Becker', due: 'Di, 20.05' },
  { text: 'Sonntag-Brunch planen', done: false, assignee: 'Lena Berger', due: 'Fr, 23.05' },
  { text: 'Restaurant-Liste durchgehen', done: false, assignee: 'Mira Klein', due: 'Sa, 24.05' },
];

const SAMPLE_MEMBERS = [
  { name: 'Avocado', role: 'OWNER', online: true, activity: 'Aktiv jetzt · Beigetreten 14.04' },
  { name: 'Lena Berger', role: 'MEMBER', online: true, activity: 'Aktiv jetzt · Beigetreten 18.04' },
  { name: 'Tom Becker', role: 'MEMBER', online: false, activity: 'Zuletzt vor 2 Std · Beigetreten 20.04' },
  { name: 'Mira Klein', role: 'MEMBER', online: false, activity: 'Zuletzt gestern · Beigetreten 02.05' },
];

// ─── Screen Root ─────────────────────────────────────────────
function SScreen({ initialTab = 'feed' } = {}) {
  const [tab, setTab] = sUseState(initialTab);
  const [activeGroup, setActiveGroup] = sUseState('g1');
  const group = SAMPLE_GROUPS.find(g => g.id === activeGroup) || SAMPLE_GROUPS[0];

  return (
    <div style={{
      background: '#f5efe1',
      minHeight: '100%',
      fontFamily: "'Inter', system-ui, sans-serif",
      color: '#1a2e20',
      display: 'flex', flexDirection: 'column',
      height: '100%',
      overflow: 'hidden',
    }}>
      <STopBar />
      <SStoriesRow groups={SAMPLE_GROUPS} activeId={activeGroup} onSelect={setActiveGroup} onCreate={() => {}} />

      <div style={{ flex: 1, overflowY: 'auto', paddingBottom: tab === 'chat' ? 0 : 24 }}>
        <SGroupHero group={group} />
        <STabBar tab={tab} setTab={setTab} />
        {tab === 'feed' && <SFeedBody posts={SAMPLE_POSTS} />}
        {tab === 'chat' && <SChatBody messages={SAMPLE_MESSAGES} />}
        {tab === 'tasks' && <STasksBody tasks={SAMPLE_TASKS} />}
        {tab === 'members' && <SMembersBody members={SAMPLE_MEMBERS} />}
      </div>

      {tab === 'chat' && <SChatComposer />}
    </div>
  );
}

window.SScreen = SScreen;
