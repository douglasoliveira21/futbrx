import type { Career } from "../../services/api";

// Zones of the Série A table: the places in the Libertadores, Pré-Libertadores and Sul-Americana come from the server,
// which redistributes them as the competitions are decided.
export type Zone = "libertadores" | "pre-libertadores" | "sul-americana" | "stay" | "relegation";
export const zoneLabels: Record<Zone, string> = {
  libertadores: "Libertadores · fase de grupos",
  "pre-libertadores": "Pré-Libertadores",
  "sul-americana": "Sul-Americana",
  stay: "Permanência na divisão",
  relegation: "Rebaixamento",
};
export const zoneColors: Record<Zone, string> = {
  libertadores: "#14803f",
  "pre-libertadores": "#7bd88f",
  "sul-americana": "#3b82d6",
  stay: "#8a93a3",
  relegation: "#dc4a54",
};
export type ZoneInfo = { zone: Zone; origin: string; other: boolean };

export function zoneOf(c: Pick<Career, "state">, clubId: string): ZoneInfo | undefined {
  return c.state.qualification?.zones?.[clubId] as ZoneInfo | undefined;
}
// Only a career in the Série A has zones.
export const hasZones = (c: Pick<Career, "state">) => Object.keys(c.state.qualification?.zones ?? {}).length > 0;
// A club that earned its place in another competition (cup, continental title) shows a star with the origin.
export const zoneBadge = (info?: ZoneInfo) => (info?.other ? `★ ${info.origin}` : "");
