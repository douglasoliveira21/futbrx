export type StadiumEvent = {
  type: string;
  second?: number;
  minute: number;
  text?: string;
  team?: string;
};
export type StadiumMoment = {
  danger: number;
  energy: number;
  reaction: "normal" | "goal" | "near-miss" | "save";
  age: number;
  goalSide: number;
};
export function stadiumMoment(
  second: number,
  ball: { x: number; y: number; z: number },
  events: StadiumEvent[],
  rate = 12,
  homeId = "",
): StadiumMoment {
  const time = (e: StadiumEvent) =>
    e.second !== undefined && e.second >= 0 ? e.second : e.minute * 60;
  const last = [...events]
    .reverse()
    .find((e) => time(e) <= second && (e.type === "goal" || e.type === "shot"));
  const age = last ? (second - time(last)) / Math.max(1, rate) : 100;
  const danger =
    Math.max(0, Math.min(1, (Math.abs(ball.x) - 25) / 23)) *
    Math.max(0.1, 1 - Math.abs(ball.z) / 30) *
    (ball.y > 6 ? 0.5 : 1);
  const reaction =
    age < 7 && last?.type === "goal"
      ? "goal"
      : age < 3.5 && last?.type === "shot"
        ? last.text?.includes("fora")
          ? "near-miss"
          : "save"
        : "normal";
  const secondHalf = events.some(
    (e) => e.type === "second_half" && time(e) <= (last ? time(last) : second),
  );
  const goalSide =
    last?.team === homeId ? (secondHalf ? -1 : 1) : secondHalf ? 1 : -1;
  return {
    danger,
    energy:
      reaction === "goal"
        ? Math.max(0.3, 1 - age / 10)
        : reaction === "near-miss"
          ? 0.65
          : reaction === "save"
            ? 0.7
            : 0.18 + danger * 0.62,
    reaction,
    age,
    goalSide,
  };
}
export function bowlPoint(angle: number, row: number) {
  const halfX = 62 + row * 1.12,
    halfZ = 43 + row * 1.12,
    radius = 14 + row * 0.2;
  const xStraight = halfX - radius,
    zStraight = halfZ - radius,
    quarter = (Math.PI * radius) / 2;
  const lengths = [
    zStraight,
    quarter,
    2 * xStraight,
    quarter,
    2 * zStraight,
    quarter,
    2 * xStraight,
    quarter,
    zStraight,
  ];
  let distance =
      ((((angle / (Math.PI * 2)) % 1) + 1) % 1) *
      lengths.reduce((a, b) => a + b, 0),
    segment = 0;
  while (segment < 8 && distance > lengths[segment])
    distance -= lengths[segment++];
  let x = 0,
    z = 0,
    theta = 0;
  switch (segment) {
    case 0:
      x = halfX;
      z = distance;
      break;
    case 1:
      theta = distance / radius;
      x = xStraight + radius * Math.cos(theta);
      z = zStraight + radius * Math.sin(theta);
      break;
    case 2:
      x = xStraight - distance;
      z = halfZ;
      break;
    case 3:
      theta = Math.PI / 2 + distance / radius;
      x = -xStraight + radius * Math.cos(theta);
      z = zStraight + radius * Math.sin(theta);
      break;
    case 4:
      x = -halfX;
      z = zStraight - distance;
      break;
    case 5:
      theta = Math.PI + distance / radius;
      x = -xStraight + radius * Math.cos(theta);
      z = -zStraight + radius * Math.sin(theta);
      break;
    case 6:
      x = -xStraight + distance;
      z = -halfZ;
      break;
    case 7:
      theta = Math.PI * 1.5 + distance / radius;
      x = xStraight + radius * Math.cos(theta);
      z = -zStraight + radius * Math.sin(theta);
      break;
    default:
      x = halfX;
      z = -zStraight + distance;
  }
  return {
    x,
    y: 0.6 + row * 0.66 + (row >= 9 ? 1.4 : 0),
    z,
    yaw: Math.atan2(-x, -z),
  };
}
export function bowlSeats(count: number) {
  const result: ReturnType<typeof bowlPoint>[] = [];
  const columns = Math.ceil(count / 16);
  for (let col = 0; col < columns; col++) {
    const fraction = (col + 0.5) / columns;
    if ((fraction * 12) % 1 < 0.025) continue;
    for (let row = 0; row < 16; row++) {
      const p = bowlPoint(fraction * Math.PI * 2, row);
      if (row < 7 && Math.abs(p.x) < 4 && p.z > 0) continue;
      result.push(p);
    }
  }
  return result;
}
