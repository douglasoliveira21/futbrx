import type { StadiumEvent } from "./stadiumDynamics";
export type AudioCue = {
  type:
    | "kick"
    | "header"
    | "goal"
    | "near-miss"
    | "save"
    | "whistle"
    | "halftime"
    | "fulltime";
  x?: number;
  z?: number;
};
export function matchAudioCues(
  previous: number,
  second: number,
  events: StadiumEvent[],
  players: {
    id: number;
    action: string;
    actionSecond?: number;
    x: number;
    z: number;
  }[],
): AudioCue[] {
  if (second < previous || second - previous > 30) return [];
  const cues: AudioCue[] = [];
  if (previous < 0 && second >= 0) cues.push({ type: "whistle" });
  for (const p of players)
    if (
      p.actionSecond !== undefined &&
      p.actionSecond >= 0 &&
      p.actionSecond > previous &&
      p.actionSecond <= second &&
      ["pass", "shot", "cross", "header", "penalty_kick"].includes(p.action)
    )
      cues.push({
        type: p.action === "header" ? "header" : "kick",
        x: p.x,
        z: p.z,
      });
  for (const e of events) {
    const at =
      e.second !== undefined && e.second >= 0 ? e.second : e.minute * 60;
    if (at <= previous || at > second) continue;
    if ((e.type === "goal"||e.type==="shootout_goal")) cues.push({ type: "goal" });
    else if (e.type === "shot")
      cues.push({ type: e.text?.includes("fora") ? "near-miss" : "save" });
    else if (e.type === "fulltime") cues.push({ type: "fulltime" });
    else if (e.type === "second_half") cues.push({ type: "halftime" });
    else if (
      ["foul", "offside", "penalty", "handball", "advantage_recalled"].includes(e.type) &&
      !cues.some((c) => c.type === "whistle")
    )
      cues.push({ type: "whistle" });
  }
  return cues;
}
