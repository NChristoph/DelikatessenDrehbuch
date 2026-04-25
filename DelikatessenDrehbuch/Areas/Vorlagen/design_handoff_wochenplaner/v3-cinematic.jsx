/* Variant 3 — Dark Cinematic
 * Full-bleed hero like the video feed, dark modal that slides up.
 * Large food cards, coral FAB, frosted overlay elements.
 */

const MEALS_V3 = {
  p1: { img: "https://images.unsplash.com/photo-1544025162-d76694265947?w=900&q=80", title: "Schweinebraten mit Biersauce", kcal: 612, min: 120 },
  p2: { img: "https://images.unsplash.com/photo-1512058564366-18510be2db19?w=900&q=80", title: "Asia-Rindfleischpfanne", kcal: 488, min: 25 },
  p3: { img: "https://images.unsplash.com/photo-1551024506-0bccd828d307?w=900&q=80", title: "Beeren-Pavlova", kcal: 310, min: 40 },
  p4: { img: "https://images.unsplash.com/photo-1540189549336-e6e99c3679fe?w=900&q=80", title: "Ziegenkäse-Salat", kcal: 245, min: 15 },
  p5: { img: "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=900&q=80", title: "Quinoa Bowl", kcal: 520, min: 30 },
};

const DAYS_V3 = [
  { name: "Mo", long: "Montag", meals: ["p1", "p2", "p3"] },
  { name: "Di", long: "Dienstag", meals: ["p4", "p2", null] },
  { name: "Mi", long: "Mittwoch", meals: [null, "p5", null] },
  { name: "Do", long: "Donnerstag", meals: [null, null, null] },
  { name: "Fr", long: "Freitag", meals: [null, null, null] },
  { name: "Sa", long: "Samstag", meals: [null, null, null] },
  { name: "So", long: "Sonntag", meals: ["p1", null, null] },
];

function V3() {
  const [active, setActive] = React.useState(0);
  const d = DAYS_V3[active];
  const dayKcal = d.meals.reduce((s, m) => s + (m ? MEALS_V3[m].kcal : 0), 0);
  const totalKcal = DAYS_V3.reduce((s, dd) => s + dd.meals.reduce((ss, m) => ss + (m ? MEALS_V3[m].kcal : 0), 0), 0);

  return (
    <div className="v3">
      <div className="v3-bg">
        <img src={MEALS_V3.p1.img} alt=""/>
        <div className="v3-bg-grad"/>
      </div>

      <div className="v3-top">
        <button className="v3-icon btn-reset" aria-label="Zurück">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><path d="M15 18l-6-6 6-6"/></svg>
        </button>
        <div className="v3-brand-pill">
          <span>🥑</span> Wochenplaner
        </div>
        <button className="v3-icon btn-reset" aria-label="Mehr">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><circle cx="5" cy="12" r="1.8"/><circle cx="12" cy="12" r="1.8"/><circle cx="19" cy="12" r="1.8"/></svg>
        </button>
      </div>

      <div className="v3-hero">
        <div className="v3-eyebrow">KW 17 · 22.–28. April</div>
        <h1>Feed-Plan.</h1>
        <div className="v3-hero-stats">
          <div>
            <div className="v3-hs-big">{(totalKcal/1000).toFixed(1)}k</div>
            <div className="v3-hs-lbl">kcal</div>
          </div>
          <div className="v3-hs-div"/>
          <div>
            <div className="v3-hs-big">12<span>/21</span></div>
            <div className="v3-hs-lbl">Rezepte</div>
          </div>
          <div className="v3-hs-div"/>
          <div>
            <div className="v3-hs-big">2</div>
            <div className="v3-hs-lbl">Personen</div>
          </div>
        </div>
      </div>

      <div className="v3-sheet">
        {/* Day pills */}
        <div className="v3-days">
          {DAYS_V3.map((dd, i) => {
            const filled = dd.meals.filter(Boolean).length;
            return (
              <button key={i} className={`v3-day-pill btn-reset ${i === active ? "is-active" : ""}`} onClick={() => setActive(i)}>
                <span className="v3-day-pill-name">{dd.name}</span>
                <span className="v3-day-pill-count">{filled}/3</span>
              </button>
            );
          })}
        </div>

        <div className="v3-day-title">
          <h2>{d.long}</h2>
          <div className="v3-day-kcal-pill">
            <span className="v3-pulse"/>
            {dayKcal > 0 ? `${dayKcal} kcal` : 'leer'}
          </div>
        </div>

        <div className="v3-meals">
          {d.meals.map((mid, i) => {
            const types = ["Vorspeise", "Hauptspeise", "Dessert"];
            if (!mid) {
              return (
                <button key={i} className="v3-empty btn-reset">
                  <span className="v3-empty-plus">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><path d="M12 5v14M5 12h14"/></svg>
                  </span>
                  <span className="v3-empty-type">{types[i]}</span>
                  <span className="v3-empty-add">hinzufügen</span>
                </button>
              );
            }
            const m = MEALS_V3[mid];
            return (
              <div key={i} className="v3-meal">
                <div className="v3-meal-num">{String(i+1).padStart(2,'0')}</div>
                <img src={m.img} alt="" className="v3-meal-img"/>
                <div className="v3-meal-info">
                  <div className="v3-meal-type">{types[i]}</div>
                  <div className="v3-meal-title">{m.title}</div>
                  <div className="v3-meal-stats">
                    <span>{m.kcal} kcal</span>
                    <span className="v3-dot-sep">·</span>
                    <span>{m.min} Min</span>
                  </div>
                </div>
                <button className="v3-meal-act btn-reset" aria-label="Optionen">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><circle cx="5" cy="12" r="1.8"/><circle cx="12" cy="12" r="1.8"/><circle cx="19" cy="12" r="1.8"/></svg>
                </button>
              </div>
            );
          })}
        </div>

        {/* action rail */}
        <div className="v3-actions scroll-x">
          <button className="v3-act-pill btn-reset v3-act-primary">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><path d="M20 6L9 17l-5-5"/></svg>
            Plan speichern
          </button>
          <button className="v3-act-pill btn-reset">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d="M16 3h5v5M4 20L21 3M21 16v5h-5"/></svg>
            Shuffle
          </button>
          <button className="v3-act-pill btn-reset">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><path d="M8.6 13.5l6.8 4M15.4 6.5l-6.8 4"/></svg>
            Teilen
          </button>
          <button className="v3-act-pill btn-reset">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 6h18l-2 13H5L3 6zM8 6V4a4 4 0 0 1 8 0v2"/></svg>
            Einkauf
          </button>
        </div>
      </div>

      <button className="v3-fab btn-reset" aria-label="Rezept hinzufügen">
        <div className="v3-fab-glow"/>
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.8" strokeLinecap="round"><path d="M12 5v14M5 12h14"/></svg>
      </button>
    </div>
  );
}

window.V3 = V3;
