/* Variant 1 — Editorial Stack
 * Each meal is a full-width image card with caption-over-image (like the feed)
 * Day = editorial chapter with oversize serif number
 * Header: sticky cream with the Avocado brand pill
 */

const MEALS_V1 = {
  p1: { img: "https://images.unsplash.com/photo-1544025162-d76694265947?w=800&q=80", title: "Schweinebraten mit dunkler Biersauce", kcal: 612, min: 120, type: "Vorspeise" },
  p2: { img: "https://images.unsplash.com/photo-1512058564366-18510be2db19?w=800&q=80", title: "Asiatische Rindfleischpfanne", kcal: 488, min: 25, type: "Hauptspeise" },
  p3: { img: "https://images.unsplash.com/photo-1551024506-0bccd828d307?w=800&q=80", title: "Beeren-Pavlova mit Vanillecreme", kcal: 310, min: 40, type: "Dessert" },
  p4: { img: "https://images.unsplash.com/photo-1540189549336-e6e99c3679fe?w=800&q=80", title: "Grüner Salat mit Ziegenkäse", kcal: 245, min: 15, type: "Vorspeise" },
  p5: { img: "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=800&q=80", title: "Mediterrane Quinoa Bowl", kcal: 520, min: 30, type: "Hauptspeise" },
};

const DAYS_V1 = [
  { name: "Montag", num: "01", meals: ["p1", "p2", "p3"] },
  { name: "Dienstag", num: "02", meals: ["p4", "p2", null] },
  { name: "Mittwoch", num: "03", meals: [null, "p5", null] },
  { name: "Donnerstag", num: "04", meals: [null, null, null] },
  { name: "Freitag", num: "05", meals: [null, null, null] },
  { name: "Samstag", num: "06", meals: [null, null, null] },
  { name: "Sonntag", num: "07", meals: ["p1", null, null] },
];

function V1() {
  const [activeDay, setActiveDay] = React.useState(0);
  const [navOpen, setNavOpen] = React.useState(false);
  const [personCount, setPersonCount] = React.useState(2);
  const [planName, setPlanName] = React.useState("Feed-Plan KW 17");

  const totalKcal = DAYS_V1.reduce((s, d) => s + d.meals.reduce((ss, m) => ss + (m ? MEALS_V1[m].kcal : 0), 0), 0);
  const filledCount = DAYS_V1.reduce((s, d) => s + d.meals.filter(Boolean).length, 0);
  const totalSlots = DAYS_V1.length * 3;

  return (
    <div className="v1">
      {/* Cream header */}
      <div className="v1-topbar">
        <button className="v1-back btn-reset" aria-label="Zurück">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d="M15 18l-6-6 6-6"/></svg>
        </button>
        <div className="v1-brand-pill">
          <span className="v1-brand-dot">🥑</span>
          <span>Wochenplaner</span>
        </div>
        <button className="v1-more btn-reset" aria-label="Mehr">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><circle cx="5" cy="12" r="2"/><circle cx="12" cy="12" r="2"/><circle cx="19" cy="12" r="2"/></svg>
        </button>
      </div>

      <div className="v1-scroll scroll-area">
        {/* Hero */}
        <div className="v1-hero">
          <div className="v1-hero-eyebrow">Dein Plan · KW 17</div>
          <h1 className="v1-hero-title">{planName}</h1>
          <div className="v1-hero-meta">
            <div className="v1-chip">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 4-6 8-6s8 2 8 6"/></svg>
              {personCount} Personen
            </div>
            <div className="v1-chip">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"/></svg>
              {(totalKcal/1000).toFixed(1)}k kcal
            </div>
            <div className="v1-chip v1-chip-progress">
              {filledCount}/{totalSlots}
            </div>
          </div>

          {/* Progress bar */}
          <div className="v1-progress">
            <div className="v1-progress-fill" style={{width: `${(filledCount/totalSlots)*100}%`}} />
          </div>

          {/* Action row — the 3 big actions from the feed */}
          <div className="v1-actions">
            <button className="v1-action-btn v1-action-primary btn-reset">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><path d="M20 6L9 17l-5-5"/></svg>
              Speichern
            </button>
            <button className="v1-action-btn btn-reset" aria-label="Shuffle">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d="M16 3h5v5M4 20L21 3M21 16v5h-5M15 15l6 6M4 4l5 5"/></svg>
            </button>
            <button className="v1-action-btn btn-reset" aria-label="Teilen">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><path d="M8.6 13.5l6.8 4M15.4 6.5l-6.8 4"/></svg>
            </button>
            <button className="v1-action-btn btn-reset" aria-label="Kalender">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 11h18"/></svg>
            </button>
          </div>
        </div>

        {/* Pending recipe callout */}
        <div className="v1-pending">
          <div className="v1-pending-bar"/>
          <img src={MEALS_V1.p1.img} alt="" className="v1-pending-img"/>
          <div className="v1-pending-text">
            <div className="v1-pending-label">
              <span className="v1-pulse-dot"/>
              wird hinzugefügt
            </div>
            <div className="v1-pending-title">{MEALS_V1.p1.title}</div>
          </div>
          <button className="v1-pending-close btn-reset" aria-label="Abbrechen">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg>
          </button>
        </div>

        {/* Macro hero block */}
        <div className="v1-macro">
          <div className="v1-macro-head">
            <div>
              <div className="v1-macro-eyebrow">Nährwerte gesamt</div>
              <div className="v1-macro-kcal">{totalKcal.toLocaleString('de-DE')} <span>kcal</span></div>
            </div>
            <div className="v1-macro-goal">
              <div className="v1-macro-goal-ring">
                <svg width="56" height="56" viewBox="0 0 56 56">
                  <circle cx="28" cy="28" r="24" fill="none" stroke="rgba(255,255,255,0.15)" strokeWidth="4"/>
                  <circle cx="28" cy="28" r="24" fill="none" stroke="var(--avo-coral)" strokeWidth="4"
                    strokeDasharray={`${(totalKcal/14000)*150.8} 150.8`} strokeLinecap="round"
                    transform="rotate(-90 28 28)"/>
                </svg>
                <div className="v1-macro-goal-pct">{Math.round((totalKcal/14000)*100)}%</div>
              </div>
              <div className="v1-macro-goal-label">Wochenziel</div>
            </div>
          </div>
          <div className="v1-macro-bars">
            <div className="v1-bar-row">
              <div className="v1-bar-label"><span className="v1-bar-dot protein"/>Protein</div>
              <div className="v1-bar-track"><div className="v1-bar-fill" style={{width: "43%", background: "var(--macro-protein)"}}/></div>
              <div className="v1-bar-val">245g</div>
            </div>
            <div className="v1-bar-row">
              <div className="v1-bar-label"><span className="v1-bar-dot fat"/>Fett</div>
              <div className="v1-bar-track"><div className="v1-bar-fill" style={{width: "21%", background: "var(--macro-fat)"}}/></div>
              <div className="v1-bar-val">128g</div>
            </div>
            <div className="v1-bar-row">
              <div className="v1-bar-label"><span className="v1-bar-dot carbs"/>KH</div>
              <div className="v1-bar-track"><div className="v1-bar-fill" style={{width: "36%", background: "var(--macro-carbs)"}}/></div>
              <div className="v1-bar-val">310g</div>
            </div>
          </div>
        </div>

        {/* Sticky day nav */}
        <div className="v1-daynav">
          {DAYS_V1.map((d, i) => {
            const filled = d.meals.filter(Boolean).length;
            return (
              <button
                key={i}
                className={`v1-daynav-btn btn-reset ${activeDay === i ? "is-active" : ""}`}
                onClick={() => setActiveDay(i)}
              >
                <span className="v1-daynav-short">{d.name.slice(0,2)}</span>
                <span className="v1-daynav-dots">
                  {[0,1,2].map(s => <span key={s} className={`v1-daynav-dot ${s < filled ? "is-filled" : ""}`}/>)}
                </span>
              </button>
            );
          })}
        </div>

        {/* Days */}
        {DAYS_V1.map((d, dIdx) => (
          <div key={dIdx} className="v1-day">
            <div className="v1-day-head">
              <div className="v1-day-num">{d.num}</div>
              <div className="v1-day-name">{d.name}</div>
              <button className="v1-day-dup btn-reset" aria-label="Duplizieren">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><rect x="9" y="9" width="11" height="11" rx="2"/><path d="M5 15V5a2 2 0 0 1 2-2h10"/></svg>
              </button>
            </div>

            {d.meals.map((mid, mIdx) => {
              const mealType = ["Vorspeise", "Hauptspeise", "Dessert"][mIdx];
              if (!mid) {
                return (
                  <button key={mIdx} className="v1-empty btn-reset">
                    <div className="v1-empty-icon">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M12 5v14M5 12h14"/></svg>
                    </div>
                    <div className="v1-empty-text">
                      <div className="v1-empty-label">{mealType}</div>
                      <div className="v1-empty-sub">Rezept wählen</div>
                    </div>
                  </button>
                );
              }
              const m = MEALS_V1[mid];
              return (
                <div key={mIdx} className="v1-meal">
                  <div className="v1-meal-img">
                    <img src={m.img} alt=""/>
                    <div className="v1-meal-type-pill">{mealType}</div>
                    <button className="v1-meal-fav btn-reset" aria-label="Favorit">
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="#fff" stroke="#fff" strokeWidth="2"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>
                    </button>
                    <div className="v1-meal-grad"/>
                    <div className="v1-meal-caption">
                      <h3>{m.title}</h3>
                      <div className="v1-meal-stats">
                        <span>🔥 {m.kcal} kcal</span>
                        <span>⏱ {m.min} Min</span>
                      </div>
                    </div>
                  </div>
                  <div className="v1-meal-actions">
                    <button className="v1-meal-act btn-reset">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M11 4h3M7 21l-3-3 12-12 3 3-12 12zM13 6l5 5"/></svg>
                      Bearbeiten
                    </button>
                    <button className="v1-meal-act btn-reset">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M16 3h5v5M4 20L21 3"/></svg>
                      Ersetzen
                    </button>
                    <button className="v1-meal-act v1-meal-act-danger btn-reset" aria-label="Entfernen">
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M6 6l1 14a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-14"/></svg>
                    </button>
                  </div>
                </div>
              );
            })}

            <button className="v1-day-nutrition btn-reset">
              <span className="v1-day-nutrition-kcal">
                {d.meals.reduce((s,m) => s + (m ? MEALS_V1[m].kcal : 0), 0)} kcal
              </span>
              <span className="v1-day-nutrition-label">Tages-Nährwerte</span>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M6 9l6 6 6-6"/></svg>
            </button>
          </div>
        ))}

        {/* Shopping list CTA */}
        <div className="v1-shopping">
          <div className="v1-shopping-icon">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 6h18l-2 13H5L3 6zM8 6V4a4 4 0 0 1 8 0v2"/></svg>
          </div>
          <div className="v1-shopping-text">
            <div className="v1-shopping-title">Einkaufsliste laden</div>
            <div className="v1-shopping-sub">32 Zutaten aus {filledCount} Rezepten</div>
          </div>
          <button className="v1-shopping-go btn-reset">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
          </button>
        </div>

        <div style={{height: 100}}/>
      </div>

      {/* Floating add */}
      <button className="v1-fab btn-reset" aria-label="Rezept hinzufügen">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.8" strokeLinecap="round"><path d="M12 5v14M5 12h14"/></svg>
      </button>
    </div>
  );
}

window.V1 = V1;
