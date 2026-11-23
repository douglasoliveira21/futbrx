import type {
  BroadcastPacket,
  MatchEvent,
  MatchPlayer,
  PlayerPose,
} from "../../services/api";

/** View model for the future tactical desk. All values come from the received
 * broadcast packet; it does not infer results from the fixture or future data. */
export type DeskPlayer = {
  pose: PlayerPose;
  player?: MatchPlayer;
  yellow: number;
  red: boolean;
  injured: boolean;
  matchRating?: number;
  x: number;
  y: number;
};
export type TacticalDeskModel = {
  second: number;
  half: "Entrada" | "1º tempo" | "Intervalo" | "2º tempo" | "Fim";
  score: [number, number];
  players: DeskPlayer[];
  events: MatchEvent[];
};

const impact: Record<string, number> = {
  goal: 0.8,
  pass: 0.018,
  interception: 0.12,
  save: 0.24,
  shot: 0.05,
  foul: -0.12,
  yellow_card: -0.22,
  red_card: -0.85,
  second_yellow: -0.55,
};

// A nota é um indicador simples de desempenho durante o jogo; não usa o overall.
export function tacticalDeskModel(packet: BroadcastPacket): TacticalDeskModel {
  const setup = packet.session.fixture.setup ?? packet.replay.setup;
  const teamIndex = packet.session.managedTeam === setup.home.id ? 0 : 1;
  const team = teamIndex === 0 ? setup.home : setup.away;
  const frame = packet.replay.frames.at(-1);
  const second = Math.max(0, frame?.second ?? 0);
  const events = packet.replay.events.filter(
    (e) =>
      (e.second !== undefined && e.second >= 0 ? e.second : e.minute * 60) <=
      second,
  );
  const half = packet.complete
    ? "Fim"
    : packet.session.halfTimeAwaiting
      ? "Intervalo"
      : events.some((e) => e.type === "second_half")
        ? "2º tempo"
        : second > 0
          ? "1º tempo"
          : "Entrada";
  const side = (teamIndex === 0 ? 1 : -1) * (half === "2º tempo" ? -1 : 1);
  const squad = [...team.players, ...(team.bench ?? [])];
  const players = (frame?.players ?? [])
    .filter((p) => p.team === teamIndex && p.active !== false)
    .map((p) => {
      const related = events.filter(
        (e) => e.team === team.id && e.player === p.id,
      );
      return {
        pose: p,
        player: squad.find((s) => s.id === p.id),
        matchRating: related.some((e) => e.type in impact)
          ? Math.min(
              10,
              Math.max(
                1,
                6 + related.reduce((sum, e) => sum + (impact[e.type] ?? 0), 0),
              ),
            )
          : undefined,
        yellow: related.filter(
          (e) => e.type === "yellow_card" || e.type === "second_yellow",
        ).length,
        red: related.some((e) => e.type === "red_card"),
        injured: related.some((e) => e.type === "injury"),
        // Pitch coordinates are in metres; clamp only the drawing, never the simulation.
        x: Math.max(0, Math.min(100, ((p.z * side + 34) / 68) * 100)),
        y: Math.max(0, Math.min(100, ((52.5 - p.x * side) / 105) * 100)),
      };
    });
  return {
    second,
    half,
    score: [
      events.filter((e) => e.type === "goal" && e.team === setup.home.id)
        .length,
      events.filter((e) => e.type === "goal" && e.team === setup.away.id)
        .length,
    ],
    players,
    events,
  };
}
