import "./loader.css";

// Loading indicator of the whole game: neon rings around a football, a scan line and a progress shimmer.
//  - screen: fills the window (opening the game, a career, a match)
//  - match:  like screen, with the pitch drawing itself, used before the 3D simulator
//  - panel:  fills the area of a page while its content loads
//  - inline: a compact line inside a card or list
export type LoaderVariant = "screen" | "match" | "panel" | "inline";

function Ball() {
  return (
    <svg className="fx-ball" viewBox="0 0 48 48" aria-hidden>
      <circle cx="24" cy="24" r="20" fill="#f4f6f8" stroke="#0b1316" strokeWidth="2.2" />
      <polygon points="24,13 32,19 29,29 19,29 16,19" fill="#0b1316" />
      <path d="M24 13V5M32 19l8-3M29 29l5 8M19 29l-5 8M16 19l-8-3" stroke="#0b1316" strokeWidth="2" fill="none" strokeLinecap="round" />
    </svg>
  );
}

function Pitch() {
  return (
    <svg className="fx-pitch" viewBox="0 0 320 200" aria-hidden>
      <rect x="6" y="6" width="308" height="188" rx="6" pathLength="1" />
      <line x1="160" y1="6" x2="160" y2="194" pathLength="1" />
      <circle cx="160" cy="100" r="30" pathLength="1" />
      <rect x="6" y="56" width="52" height="88" pathLength="1" />
      <rect x="262" y="56" width="52" height="88" pathLength="1" />
      <rect x="6" y="80" width="18" height="40" pathLength="1" />
      <rect x="296" y="80" width="18" height="40" pathLength="1" />
    </svg>
  );
}

export function Loader({ label = "Carregando…", variant = "panel", hint }: { label?: string; variant?: LoaderVariant; hint?: string }) {
  const big = variant === "screen" || variant === "match";
  return (
    <div className={`fx-loader fx-${variant}`} role="status" aria-live="polite" aria-busy="true">
      {big && <div className="fx-grid" aria-hidden />}
      {variant === "match" && <Pitch />}
      <div className="fx-core">
        <svg className="fx-rings" viewBox="0 0 120 120" aria-hidden>
          <circle className="fx-ring a" cx="60" cy="60" r="54" pathLength="100" />
          <circle className="fx-ring b" cx="60" cy="60" r="44" pathLength="100" />
          <circle className="fx-ring c" cx="60" cy="60" r="34" pathLength="100" />
        </svg>
        <Ball />
        {big && <span className="fx-scan" aria-hidden />}
      </div>
      <p className="fx-label">
        <span>{label.replace(/…$|\.\.\.$/, "")}</span>
        <b aria-hidden>
          <i />
          <i />
          <i />
        </b>
      </p>
      {hint && <small className="fx-hint">{hint}</small>}
      {variant !== "inline" && (
        <div className="fx-bar" aria-hidden>
          <i />
        </div>
      )}
    </div>
  );
}
