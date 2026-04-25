/* Variant 2 — Story Rail
 * Days stack vertically, but meals are horizontal story-cards
 * per day. Feels like scrolling through a feed.
 * Dark cream bg with framed cards.
 */

const MEALS_V2 = {
  p1: { img: "https://images.unsplash.com/photo-1544025162-d76694265947?w=800&q=80", title: "Schweinebraten · Biersauce", kcal: 612, min: 120, pro: 50, fat: 38, carbs: 17 },
  p2: { img: "https://images.unsplash.com/photo-1512058564366-18510be2db19?w=800&q=80", title: "Asia-Rindfleischpfanne", kcal: 488, min: 25, pro: 42, fat: 8, carbs: 60 },
  p3: { img: "https://images.unsplash.com/photo-1551024506-0bccd828d307?w=800&q=80", title: "Beeren-Pavlova", kcal: 310, min: 40, pro: 6, fat: 12, carbs: 45 },
  p4: { img: "https://images.unsplash.com/photo-1540189549336-e6e99c3679fe?w=800&q=80", title: "Ziegenkäse-Salat", kcal: 245, min: 15, pro: 14, fat: 18, carbs: 10 },
  p5: { img: "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=800&q=80", title: "Quinoa Power Bowl", kcal: 520, min: 30, pro: 22, fat: 18, carbs: 65 },
};

const DAYS_V2 = [
  { name: "Montag", date: "22. Apr", meals: [{type: "Vorspeise", id: "p1"}, {type: "Hauptspeise", id: "p2"}, {type: "Dessert", id: "p3"}] },
  { name: "Dienstag", date: "23. Apr", meals: [{type: "Vorspeise", id: "p4"}, {type: "Hauptspeise", id: "p2"}, {type: "Dessert", id: null}] },
  { name: "Mittwoch", date: "24. Apr", meals: [{type: "Vorspeise", id: null}, {type: "Hauptspeise", id: "p5"}, {type: "Dessert", id: null}] },
  { name: "Donnerstag", date: "25. Apr", meals: [{type: "Vorspeise", id: null}, {type: "Hauptspeise", id: null}, {type: "Dessert", id: null}] },
  { name: "Freitag", date: "26. Apr", meals: [{type: "Vorspeise", id: null}, {type: "Hauptspeise", id: null}, {type: "Dessert", id: null}] },
  { name: "Samstag", date: "27. Apr", meals: [{type: "Vorspeise", id: null}, {type: "Hauptspeise", id: null}, {type: "Dessert", id: null}] },
  { name: "Sonntag", date: "28. Apr", meals: [{type: "Vorspeise", id: "p1"}, {type: "Hauptspeise", id: null}, {type: "Dessert", id: null}] },
];

function V2() {
  const totalKcal = DAYS_V2.reduce((s, d) => s + d.meals.reduce((ss, m) => ss + (m.id ? MEALS_V2[m.id].kcal : 0), 0), 0);
  const filled = DAYS_V2.reduce((s, d) => s + d.meals.filter(m => m.id).length, 0);
  const total = DAYS_V2.length * 3;

  return (
    <div className="v2">
      {/* Top bar: pull handle + title */}
      <div className="v2-topbar">
        <div className="v2-grip"/>
        <div className="v2-top-row">
          <div className="v2-brand">
            <span className="v2-avo">🥑</span>
            <span>AVOCADO</span>
          </div>
          <button className="v2-close btn-reset" aria-label="Schließen">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg>
          </button>
        </div>
      </div>

      <div className="v2-scroll scroll-area">
        {/* Big display title */}
        <div className="v2-headline">
          <h1>Dein<br/>Wochenplan.</h1>
          <div className="v2-headline-meta">
            <span>KW 17</span>
            <span className="v2-headline-dot">•</span>
            <span>22.–28. April</span>
          </div>
        </div>

        {/* Summary cards row */}
        <div className="v2-summary">
          <div className="v2-summary-card">
            <div className="v2-sc-big">{filled}<span>/{total}</span></div>
            <div className="v2-sc-label">Rezepte</div>
            <div className="v2-sc-bar">
              <div className="v2-sc-bar-fill" style={{width: `${(filled/total)*100}%`}}/>
            </div>
          </div>
          <div className="v2-summary-card v2-summary-dark">
            <div className="v2-sc-big">{(totalKcal/1000).toFixed(1)}<span>k</span></div>
            <div className="v2-sc-label" style={{color: "rgba(255,255,255,0.7)"}}>kcal gesamt</div>
            <div className="v2-sc-macros">
              <span style={{background: "var(--macro-protein)"}}/>
              <span style={{background: "var(--macro-fat)"}}/>
              <span style={{background: "var(--macro-carbs)"}}/>
            </div>
          </div>
        </div>

        {/* Quick actions — feed-style pill bar */}
        <div className="v2-quickbar scroll-x">
          <button className="v2-qb-item v2-qb-primary btn-reset">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><path d="M20 6L9 17l-5-5"/></svg>
            Speichern
          </button>
          <button className="v2-qb-item btn-reset">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d="M16 3h5v5M4 20L21 3M21 16v5h-5M15 15l6 6M4 4l5 5"/></svg>
            Shuffle
          </button>
          <button className="v2-qb-item btn-reset">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><path d="M8.6 13.5l6.8 4M15.4 6.5l-6.8 4"/></svg>
            Teilen
          </button>
          <button className="v2-qb-item btn-reset">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 11h18"/></svg>
            Kalender
          </button>
          <button className="v2-qb-item btn-reset">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 6h18l-2 13H5L3 6zM8 6V4a4 4 0 0 1 8 0v2"/></svg>
            Einkauf
          </button>
        </div>

        {/* Macro hero card */}
        <div className="v2-macro">
          <div className="v2-macro-ring-wrap">
            <svg viewBox="0 0 120 120" className="v2-macro-ring">
              <circle cx="60" cy="60" r="52" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="10"/>
              <circle cx="60" cy="60" r="52" fill="none" stroke="var(--macro-protein)" strokeWidth="10"
                strokeDasharray="140 327" strokeDashoffset="0" strokeLinecap="round" transform="rotate(-90 60 60)"/>
              <circle cx="60" cy="60" r="52" fill="none" stroke="var(--macro-fat)" strokeWidth="10"
                strokeDasharray="70 327" strokeDashoffset="-140" strokeLinecap="round" transform="rotate(-90 60 60)"/>
              <circle cx="60" cy="60" r="52" fill="none" stroke="var(--macro-carbs)" strokeWidth="10"
                strokeDasharray="117 327" strokeDashoffset="-210" strokeLinecap="round" transform="rotate(-90 60 60)"/>
            </svg>
            <div className="v2-macro-center">
              <div className="v2-macro-center-kcal">{totalKcal.toLocaleString('de-DE')}</div>
              <div className="v2-macro-center-label">kcal · Woche</div>
            </div>
          </div>
          <div className="v2-macro-side">
            <div className="v2-macro-row">
              <span className="v2-macro-dot" style={{background: "var(--macro-protein)"}}/>
              <div>
                <div className="v2-macro-val">245<span>g</span></div>
                <div className="v2-macro-k">Protein · 43%</div>
              </div>
            </div>
            <div className="v2-macro-row">
              <span className="v2-macro-dot" style={{background: "var(--macro-fat)"}}/>
              <div>
                <div className="v2-macro-val">128<span>g</span></div>
                <div className="v2-macro-k">Fett · 21%</div>
              </div>
            </div>
            <div className="v2-macro-row">
              <span className="v2-macro-dot" style={{background: "var(--macro-carbs)"}}/>
              <div>
                <div className="v2-macro-val">310<span>g</span></div>
                <div className="v2-macro-k">Kohlenhydrate · 36%</div>
              </div>
            </div>
          </div>
        </div>

        {/* Days */}
        {DAYS_V2.map((d, dIdx) => {
          const dayKcal = d.meals.reduce((s, m) => s + (m.id ? MEALS_V2[m.id].kcal : 0), 0);
          const dayFilled = d.meals.filter(m => m.id).length;
          return (
            <div key={dIdx} className="v2-day">
              <div className="v2-day-head">
                <div className="v2-day-left">
                  <div className="v2-day-name">{d.name}</div>
                  <div className="v2-day-date">{d.date}</div>
                </div>
                <div className="v2-day-right">
                  {dayKcal > 0 && (
                    <div className="v2-day-kcal">
                      <span>{dayKcal}</span>kcal
                    </div>
                  )}
                  <button className="v2-day-dots btn-reset" aria-label="Optionen">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><circle cx="5" cy="12" r="1.8"/><circle cx="12" cy="12" r="1.8"/><circle cx="19" cy="12" r="1.8"/></svg>
                  </button>
                </div>
              </div>

              <div className="v2-rail scroll-x">
                {d.meals.map((meal, mIdx) => {
                  if (!meal.id) {
                    return (
                      <button key={mIdx} className="v2-card v2-card-empty btn-reset">
                        <div className="v2-card-empty-icon">
                          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M12 5v14M5 12h14"/></svg>
                        </div>
                        <div className="v2-card-empty-type">{meal.type}</div>
                        <div className="v2-card-empty-sub">Rezept wählen</div>
                      </button>
                    );
                  }
                  const m = MEALS_V2[meal.id];
                  return (
                    <div key={mIdx} className="v2-card">
                      <div className="v2-card-img">
                        <img src={m.img} alt=""/>
                        <div className="v2-card-type">{meal.type}</div>
                        <button className="v2-card-edit btn-reset" aria-label="Bearbeiten">
                          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><path d="M11 4h3M7 21l-3-3 12-12 3 3-12 12z"/></svg>
                        </button>
                      </div>
                      <div className="v2-card-body">
                        <div className="v2-card-title">{m.title}</div>
                        <div className="v2-card-stats">
                          <span className="v2-card-stat"><svg width="11" height="11" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C10 6 6 8 6 13a6 6 0 1 0 12 0c0-5-4-7-6-11z"/></svg>{m.kcal}</span>
                          <span className="v2-card-stat"><svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>{m.min}'</span>
                        </div>
                        <div className="v2-card-macrobar">
                          <span style={{flex: m.pro, background: "var(--macro-protein)"}}/>
                          <span style={{flex: m.fat, background: "var(--macro-fat)"}}/>
                          <span style={{flex: m.carbs, background: "var(--macro-carbs)"}}/>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          );
        })}

        <div style={{height: 120}}/>
      </div>

      {/* Bottom action bar */}
      <div className="v2-bottombar">
        <button className="v2-bb-secondary btn-reset">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 6h18l-2 13H5L3 6zM8 6V4a4 4 0 0 1 8 0v2"/></svg>
          <span>32 Zutaten</span>
        </button>
        <button className="v2-bb-primary btn-reset">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><path d="M12 5v14M5 12h14"/></svg>
          Rezept hinzufügen
        </button>
      </div>
    </div>
  );
}

window.V2 = V2;
