import type { BroadcastPacket, MatchPlayer } from "../../services/api";

// What the coach can do when a player of the managed team is hurt (engine v15): bring a reserve on, or, with no change left,
// play on with ten. Pure functions so the rules can be tested without the 3D scene.
export type InjuryDecisionModel = {
  playerId: number;
  name: string;
  position: string;
  description: string;
  days: number;
  injuryName: string;
  changesLeft: number;
  reserves: MatchPlayer[];
  suggested?: MatchPlayer;
};

export function injuryDecision(packet: BroadcastPacket): InjuryDecisionModel | null {
  const s = packet.session;
  const setup = s.fixture.setup;
  if (!s.injuryAwaiting || !s.injuryPlayerId || !s.managedTeam || !setup) return null;
  const team = s.managedTeam === setup.home.id ? setup.home : setup.away;
  const frame = packet.replay.frames.at(-1);
  const pose = frame?.players.find((p) => p.id === s.injuryPlayerId);
  const hurt = [...packet.replay.events].reverse().find((e) => e.type === "injury" && e.player === s.injuryPlayerId);
  const known = team.players.find((p) => p.id === s.injuryPlayerId) ?? team.bench?.find((p) => p.id === s.injuryPlayerId);
  const done = packet.replay.events.filter((e) => e.type === "substitution" && e.team === s.managedTeam);
  const used = new Set(done.map((e) => e.relatedPlayer));
  const position = pose?.position ?? known?.position ?? "";
  const keeper = position === "GOL";
  const changesLeft = Math.max(0, 5 - done.length);
  const reserves = (team.bench ?? [])
    .filter((b) => !used.has(b.id) && (b.position === "GOL") === keeper)
    .sort((a, b) => Number(b.position === position) - Number(a.position === position) || b.rating - a.rating);
  return {
    playerId: s.injuryPlayerId,
    name: pose?.name ?? known?.name ?? "Atleta",
    position,
    description: hurt?.text ?? "Atleta lesionado.",
    days: hurt?.injuryDays ?? 0,
    injuryName: hurt?.injuryName ?? "Lesão",
    changesLeft,
    reserves: changesLeft > 0 ? reserves : [],
    suggested: changesLeft > 0 ? reserves[0] : undefined,
  };
}
