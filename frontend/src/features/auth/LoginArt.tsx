// Original vector art for the login screen: the wordmark and one emblem per competition. These are the game's own
// representations, not official logos of the competitions.
export function LoginLogo() {
  return (
    <svg viewBox="0 0 472 152" role="img" aria-label="FUTBRX" className="login-logo-art">
      <defs>
        <linearGradient id="lg-metal" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset=".55" stopColor="#d9dee3" />
          <stop offset="1" stopColor="#8e98a2" />
        </linearGradient>
        <linearGradient id="lg-neon" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#a6ff3b" />
          <stop offset="1" stopColor="#19c200" />
        </linearGradient>
        <linearGradient id="lg-swoosh" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#3dff00" stopOpacity="0" />
          <stop offset=".55" stopColor="#3dff00" />
          <stop offset="1" stopColor="#1e7bff" />
        </linearGradient>
      </defs>
      <path d="M92 52 C170 24 262 14 346 22 C292 28 196 38 112 66 Z" fill="url(#lg-swoosh)" />
      <g transform="translate(366 4)">
        <circle cx="30" cy="30" r="28" fill="#f4f6f8" stroke="#10181b" strokeWidth="2.4" />
        <polygon points="30,14 41,22 37,35 23,35 19,22" fill="#10181b" />
        <path d="M30 14 L30 4 M41 22 L52 18 M37 35 L45 46 M23 35 L15 46 M19 22 L8 18" stroke="#10181b" strokeWidth="2" fill="none" />
      </g>
      <g fontFamily="Montserrat, 'Arial Black', Impact, sans-serif" fontWeight="900" fontStyle="italic" fontSize="104" transform="skewX(-8)">
        <text x="40" y="132" textLength="312" lengthAdjust="spacingAndGlyphs" fill="#000" opacity=".4" transform="translate(5 6)">FUTBR</text>
        <text x="40" y="132" textLength="312" lengthAdjust="spacingAndGlyphs" fill="url(#lg-metal)" stroke="#222c30" strokeWidth="1.2">FUTBR</text>
        <text x="362" y="132" textLength="70" lengthAdjust="spacingAndGlyphs" fill="#000" opacity=".4" transform="translate(5 6)">X</text>
        <text x="362" y="132" textLength="70" lengthAdjust="spacingAndGlyphs" fill="url(#lg-neon)" stroke="#0b5a00" strokeWidth="1.2">X</text>
      </g>
    </svg>
  );
}

export type Emblem = "brasileirao" | "copa" | "libertadores" | "sulamericana" | "estaduais";

export function CompetitionEmblem({ name }: { name: Emblem }) {
  const common = { viewBox: "0 0 54 58", width: 54, height: 58, "aria-hidden": true as const, className: "login-emblem" };
  switch (name) {
    case "brasileirao":
      return (
        <svg {...common}>
          <path d="M27 3 L48 10 V30 C48 43 38 51 27 55 C16 51 6 43 6 30 V10 Z" fill="#14803f" stroke="#e9b949" strokeWidth="3" />
          <path d="M27 9 L42 14 V30 C42 39 35 45 27 48 C19 45 12 39 12 30 V14 Z" fill="#0d5a2b" />
          <path d="M14 33 L27 21 L40 33 L27 45 Z" fill="#e9b949" />
          <circle cx="27" cy="33" r="6" fill="#1d4fa8" />
        </svg>
      );
    case "copa":
      return (
        <svg {...common}>
          <path d="M14 6 H40 V22 C40 32 34 38 27 38 C20 38 14 32 14 22 Z" fill="#d7dde3" stroke="#8e98a2" strokeWidth="2" />
          <path d="M14 10 C5 10 5 24 15 26 M40 10 C49 10 49 24 39 26" fill="none" stroke="#d7dde3" strokeWidth="3" />
          <rect x="24" y="38" width="6" height="9" fill="#b4bdc6" />
          <rect x="16" y="47" width="22" height="6" rx="2" fill="#e9b949" />
          <circle cx="27" cy="21" r="5" fill="#14803f" />
        </svg>
      );
    case "libertadores":
      return (
        <svg {...common}>
          <path d="M16 4 H38 L34 30 C33 36 30 38 27 38 C24 38 21 36 20 30 Z" fill="#c9962b" stroke="#8a6414" strokeWidth="2" />
          <path d="M22 8 V30 M27 8 V34 M32 8 V30" stroke="#f3d77a" strokeWidth="2" fill="none" />
          <rect x="23" y="38" width="8" height="8" fill="#a97b1e" />
          <rect x="14" y="46" width="26" height="7" rx="2" fill="#2a2a2a" stroke="#c9962b" strokeWidth="1.5" />
        </svg>
      );
    case "sulamericana":
      return (
        <svg {...common}>
          <path d="M15 5 H39 V20 C39 30 34 35 27 35 C20 35 15 30 15 20 Z" fill="#e8edf2" stroke="#9aa6b2" strokeWidth="2" />
          <path d="M27 35 V43" stroke="#9aa6b2" strokeWidth="4" />
          <ellipse cx="27" cy="46" rx="12" ry="5" fill="#3b82d6" />
          <circle cx="27" cy="15" r="4" fill="#3b82d6" />
        </svg>
      );
    case "estaduais":
      return (
        <svg {...common}>
          <path d="M27 3 L47 10 V30 C47 43 37 51 27 55 C17 51 7 43 7 30 V10 Z" fill="#f2f4f6" stroke="#10181b" strokeWidth="3" />
          <path d="M18 18 L30 14 L38 22 L35 32 L40 40 L28 46 L22 38 L16 30 Z" fill="#14803f" stroke="#0b5a2b" strokeWidth="1.5" />
        </svg>
      );
  }
}
