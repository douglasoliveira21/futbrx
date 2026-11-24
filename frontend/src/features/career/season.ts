// Length of the season in weekly rounds. Older saves do not store it and always had 38.
export const seasonRounds = (state: { seasonRounds?: number }) => state.seasonRounds ?? 38;
export const seasonOver = (state: { round: number; seasonRounds?: number }) =>
  state.round > seasonRounds(state);
