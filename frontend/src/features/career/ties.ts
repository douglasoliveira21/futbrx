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

// Stage names of the Série D knockout; any other competition just numbers its phases.
const serieDStages = ["Primeira fase", "Segunda fase", "Oitavas de final", "Quartas de final", "Semifinais", "Final"];
export const stageLabel = (competitionId: string, stage: number) =>
  competitionId === "serie-d" ? (serieDStages[stage] ?? `Fase ${stage + 1}`) : `Fase ${stage + 1}`;

// Competitions marked `light` only broadcast the club's own matches; the others are summarised.
export function visibleMatches<T extends { fixture: { home: string; away: string } }>(light: boolean | undefined, matches: T[], clubId: string) {
  return light ? matches.filter((m) => m.fixture.home === clubId || m.fixture.away === clubId) : matches;
}

// Standings of one group of a group-stage competition (Série C second phase), by points, wins, goal difference and goals.
export function groupStandings(matches: CupMatchView[], tie: string) {
  const rows = new Map<string, { club: string; played: number; wins: number; points: number; goals: number; against: number }>();
  const row = (club: string) => {
    if (!rows.has(club)) rows.set(club, { club, played: 0, wins: 0, points: 0, goals: 0, against: 0 });
    return rows.get(club)!;
  };
  for (const m of matches.filter((x) => x.tie === tie)) {
    const home = row(m.fixture.home);
    const away = row(m.fixture.away);
    if (m.fixture.homeGoals == null || m.fixture.awayGoals == null) continue;
    const [h, a] = [m.fixture.homeGoals, m.fixture.awayGoals];
    home.played++; away.played++;
    home.goals += h; home.against += a; away.goals += a; away.against += h;
    if (h > a) { home.points += 3; home.wins++; } else if (a > h) { away.points += 3; away.wins++; } else { home.points++; away.points++; }
  }
  return [...rows.values()].sort((x, y) => y.points - x.points || y.wins - x.wins || (y.goals - y.against) - (x.goals - x.against) || y.goals - x.goals);
}
