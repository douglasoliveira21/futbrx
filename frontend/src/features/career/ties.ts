import type { Fixture } from "../../services/api";

// Matches of a knockout competition as the club screen receives them. Ties with two legs share `tie`.
export type CupMatchView = { id: string; stage: number; tie?: string; leg?: number; winner: string; fixture: Fixture };
export type CupView = { legs?: number; matches: CupMatchView[] };

export const legLabel = (cup: CupView, match: CupMatchView) =>
  (cup.legs ?? 1) === 2 ? (match.leg === 2 ? "Volta" : "Ida") : "";

// Goals of both clubs over the legs already played, from the point of view of the match's home and away club.
export function aggregate(cup: CupView, match: CupMatchView) {
  if ((cup.legs ?? 1) !== 2 || match.leg !== 2) return null;
  const first = cup.matches.find((m) => m.tie === match.tie && m.leg === 1);
  const a = first?.fixture;
  const b = match.fixture;
  if (!a || a.homeGoals == null || a.awayGoals == null || b.homeGoals == null || b.awayGoals == null)
    return null;
  return { home: b.homeGoals + a.awayGoals, away: b.awayGoals + a.homeGoals };
}
export const played = (match: CupMatchView) => match.fixture.homeGoals != null;
