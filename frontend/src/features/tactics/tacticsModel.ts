import type { Player, TacticalPlan } from "../../services/api";

// Pure helpers of the pre-match tactics desk (the "Táticas" page). They mirror what the
// in-match desk does so a plan prepared here plays out the same way once the match starts.
export const mentalities = [
  "Ultradefensiva",
  "Muito defensiva",
  "Defensiva",
  "Cautelosa",
  "Equilibrada",
  "Positiva",
  "Ofensiva",
  "Muito ofensiva",
  "Ataque total",
];
// Formations the career stores; every other catalog layout is expressed through plan.positions.
export const defaultTemplates: Record<string, string> = {
  "4-3-3": "433",
  "4-4-2": "442",
  "3-5-2": "352",
  "4-2-3-1": "4231-wide",
};
const roleRank: Record<string, number> = { GOL: 0, ZAG: 1, LAT: 2, MEI: 3 };
export const roleOrder = (position: string) => roleRank[position] ?? 4;

export type Point = { x: number; z: number };

// Same order the server uses to build the match team: role, then id.
export function startersOf(squad: Player[]) {
  return squad
    .filter((p) => p.starter)
    .sort((a, b) => roleOrder(a.position) - roleOrder(b.position) || a.id - b.id);
}
// Shirt numbers follow the match roster: starters first, then the bench in bench order.
export function rosterOf(squad: Player[], benchIds: number[]) {
  const bench = benchIds
    .map((id) => squad.find((p) => p.id === id))
    .filter((p): p is Player => !!p && !p.starter);
  const rest = squad.filter((p) => !p.starter && !bench.includes(p));
  return [...startersOf(squad), ...bench, ...rest];
}
// A catalog layout lists points in role order; assign them to the starters in that order.
export function mapPositions(points: Point[], starters: Pick<Player, "id">[]) {
  return Object.fromEntries(
    starters.flatMap((p, i) => (points[i] ? [[p.id, points[i]]] : [])),
  ) as Record<number, Point>;
}
// Tactical coordinates (x along the pitch, z across) to the pitch drawing in percent.
export const toPercent = (point: Point) => ({
  x: (point.z + 1) * 50,
  y: (1 - point.x) * 50,
});
// Which catalog layout the saved positions come from: "" when the positions were moved by hand,
// `fallback` when no custom positions are saved.
export function templateFor(
  positions: Record<number, Point> | undefined,
  formations: { id: string; positions: Point[] }[],
  starters: Pick<Player, "id">[],
  fallback: string,
) {
  if (!positions || Object.keys(positions).length === 0) return fallback;
  const close = (a: Point, b: Point) =>
    Math.abs(a.x - b.x) < 1e-3 && Math.abs(a.z - b.z) < 1e-3;
  const hit = formations.find((f) => {
    const mapped = mapPositions(f.positions, starters);
    return starters.every((p) => positions[p.id] && mapped[p.id] && close(positions[p.id], mapped[p.id]));
  });
  return hit?.id ?? "";
}
export function quickPlans(plan: TacticalPlan, mentality: string) {
  return [
    { label: "Mais ofensivo", mentality: "Ofensiva", plan: { ...plan, overlap: true } },
    {
      label: "Equilibrado",
      mentality: "Equilibrada",
      plan: { ...plan, pressure: "Média", width: "Normal" },
    },
    {
      label: "Mais defensivo",
      mentality: "Defensiva",
      plan: { ...plan, pressure: "Baixa", width: "Fechada", overlap: false },
    },
    { label: "Pressão alta", mentality, plan: { ...plan, pressure: "Alta" } },
    {
      label: "Contra-ataque",
      mentality,
      plan: { ...plan, style: "Contra-ataque", buildShort: false },
    },
    {
      label: "Manter posse",
      mentality,
      plan: { ...plan, style: "Posse", buildShort: true },
    },
  ];
}
// What the "Salvar" button compares: the plan, formation and mentality as the server holds them.
export const planKey = (plan: TacticalPlan, formation: string, mentality: string) =>
  JSON.stringify([plan, formation, mentality]);
