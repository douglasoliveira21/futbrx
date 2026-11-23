import type { ReplayFrame } from "../../services/api";

export function replayBallHeight(version:string,height:number) {
  // V11+ records the physical centre in metres. Older replays used a .24 m
  // visual origin and retain their existing presentation correction.
  return ["positional-v11","positional-v12","positional-v13","positional-v14"].includes(version) ? Math.max(.11,height) : Math.max(.11,height-.13*Math.max(0,1-(height-.24)/.5));
}

export function framePair(frames: ReplayFrame[], second: number) {
  if (!frames.length) return null;
  if (second <= frames[0].second)
    return { a: frames[0], b: frames[0], alpha: 0 };
  if (second >= frames[frames.length - 1].second)
    return {
      a: frames[frames.length - 1],
      b: frames[frames.length - 1],
      alpha: 0,
    };
  let low = 0,
    high = frames.length - 1;
  while (high - low > 1) {
    const middle = (low + high) >>> 1;
    if (frames[middle].second <= second) low = middle;
    else high = middle;
  }
  const a = frames[low],
    b = frames[high];
  return {
    a,
    b,
    alpha: Math.max(
      0,
      Math.min(1, (second - a.second) / (b.second - a.second || 1)),
    ),
  };
}
export function lerp(a: number, b: number, t: number) {
  return a + (b - a) * t;
}
export function lerpYaw(a: number, b: number, t: number) {
  const delta = Math.atan2(Math.sin(b - a), Math.cos(b - a));
  return a + delta * t;
}

export function broadcastPosition(
  current: number,
  available: number,
  delta: number,
  speed: number,
  active: boolean,
) {
  return Math.min(
    available,
    Math.max(0, current) +
      (active ? Math.max(0, Math.min(delta, 0.2)) * speed : 0),
  );
}

export function broadcastClock(second: number, secondHalf?: number) {
  const value = Math.max(0, Math.floor(second));
  const normal =
    secondHalf !== undefined && value >= secondHalf
      ? 2700 + value - secondHalf
      : value;
  const limit = secondHalf !== undefined && value >= secondHalf ? 5400 : 2700;
  const added = normal > limit;
  const display = added ? normal - limit : normal;
  const clock = `${String(Math.floor(display / 60)).padStart(2, "0")}:${String(display % 60).padStart(2, "0")}`;
  return added ? `${limit / 60}+${clock}` : clock;
}
