export type Club = {
  id: string;
  name: string;
  short: string;
  color: string;
  strength: number;
};
export type Player = {
  id: number;
  name: string;
  position: string;
  age: number;
  rating: number;
  fitness: number;
  value: number;
  wage: number;
  starter: boolean;
  injuryDays: number;
  injuryName: string;
  yellowCards: number;
  suspensionMatches: number;
  transferListed: boolean;
};
export type MatchEvent = {
  minute: number;
  type: string;
  team: string;
  player: number;
  text: string;
  second?: number;
  relatedPlayer?: number | null;
  injuryDays?: number;
  injuryName?: string;
};
export type Fixture = {
  leg?: number;
  competitionMatchId?: string;
  competitionName?: string;
  homePenalties?: number | null;
  awayPenalties?: number | null;
  archiveClubs?: Club[];
  date?: string | null;
  kickoff?: string | null;
  season?: number;
  round: number;
  home: string;
  away: string;
  homeGoals: number | null;
  awayGoals: number | null;
  events: MatchEvent[];
  seed: number;
  setup?: MatchSetup | null;
};
export type MatchPlayer = {
  skills?: Record<string,number>;
  preferredFoot?:string;
  id: number;
  name: string;
  position: string;
  rating: number;
  fitness: number;
};
export type MatchTeam = {
  plan?: TacticalPlan;
  computerControlled?: boolean;
  id: string;
  formation: string;
  mentality: string;
  players: MatchPlayer[];
  bench?: MatchPlayer[];
  substitutions?: SubstitutionPlan[];
};
export type MatchSetup = {
  engineVersion: string;
  home: MatchTeam;
  away: MatchTeam;
  instructions?: {
    second: number;
    team: string;
    type: string;
    formation: string;
    mentality: string;
    outPlayerId: number;
    inPlayerId: number;
    atHalfTime: boolean;
    plan?: TacticalPlan;
  }[];
};
export type PlayerPose = {
  fitness?: number;
  actionSecond?: number;
  id: number;
  team: number;
  x: number;
  z: number;
  yaw: number;
  action: string;
  active?: boolean;
  name?: string;
  position?: string;
};
export type ReplayFrame = {
  diagnostics?: {state:string;restart:string|null;eventIndex:number|null};
  lastContact?: {id?:number;playerId:number;team:number;type:string;second:number;x:number;y:number;z:number;origin:string};
  ballVelocity?: {x:number;y:number;z:number};
  possession?: number;
  second: number;
  players: PlayerPose[];
  ball: { x: number; y: number; z: number };
};
export type MatchReplay = {
  engineVersion: string;
  duration: number;
  setup: MatchSetup;
  events: MatchEvent[];
  frames: ReplayFrame[];
  homeGoals: number;
  awayGoals: number;
};
export type DataRelease = {
  id: string;
  version: string;
  season: number;
  source: string;
  license: string;
  published: boolean;
  revision: number;
  created: string;
  publishedAt: string | null;
};
export type SeasonCatalog = {
  version: string;
  season: number;
  source: string;
  license: string;
  clubs: Club[];
  players: { clubId: string; player: Player }[];
};
export type Row = {
  club: Club;
  played: number;
  wins: number;
  draws: number;
  losses: number;
  goals: number;
  against: number;
  difference: number;
  points: number;
};
export type BroadcastSession = {
  competitionName?: string;
  managedTeam?: string;
  controlsEnabled?: boolean;
  pauseAfterSecond?: number | null;
  stoppagePaused?: boolean;
  halfTimeAwaiting?: boolean;
  halfTimeAccepted?: boolean;
  // V15: an injured player of the managed team waits for the coach (the broadcast stays paused).
  injuryAwaiting?: boolean;
  injuryPlayerId?: number;
  matchSecond?: number;
  incremental?: boolean;
  simulatedSecond?: number;
  id: string;
  season: number;
  watchedSeconds: number;
  duration: number;
  entranceSeconds: number;
  speed: number;
  fixture: Fixture;
};
export type BroadcastPacket = {
  session: BroadcastSession;
  complete: boolean;
  replay: MatchReplay;
};
export type State = {
  seasonRounds?: number;
  leagueStart?: number;
  tacticalPlan?: TacticalPlan;
  coach?: { employed: boolean; name: string; reputation: number; date: string; unreadDismissal: boolean };
  competitionClubs?: Club[];
  nextCup?: Fixture | null;
  board?: { dismissed: boolean; confidence: number };
  liveMatch?: BroadcastSession | null;
  loans?: { playerId: number; endSeason: number; returned: boolean }[];
  calendarSource?: string;
  division?: string;
  otherClubs?: Club[];
  // Clubs of the state championship that have no squad until the club meets them.
  knownClubs?: Club[];
  // International places of a Série A career: zone of every club of the table, the places of the season and the history.
  qualification?: {
    zones: Record<string, { zone: string; origin: string; other: boolean }>;
    berths: { clubId: string; competition: string; origin: string }[];
    history: { season: number; berths: { clubId: string; competition: string; origin: string }[] }[];
  } | null;
  // Série D: the 90 clubs of the other groups, the final tables of the 16 groups and the six promoted clubs.
  serieD?: {
    group: number;
    others: Club[];
    qualified: string[];
    promoted: string[];
    results: {
      group: number;
      table: { clubId: string; points: number; played: number; wins: number; draws: number; losses: number; goals: number; against: number; difference: number }[];
    }[];
  } | null;
  sportsVersion: number;
  benchIds: number[];
  substitutions: SubstitutionPlan[];
  autoSubstitute: boolean;
  autoReplaceUnavailable: boolean;
  playerStatsFromRound: number;
  playerStats: Record<string, PlayerStats>;
  managementVersion: number;
  contracts: Record<string, PlayerContract>;
  offers: TransferOffer[];
  transfers: TransferRecord[];
  pastSeasons: PastSeason[];
  dataVersion: string;
  season: number;
  round: number;
  clubId: string;
  formation: string;
  mentality: string;
  training: string;
  balance: number;
  clubs: Club[];
  squad: Player[];
  market: Player[];
  fixtures: Fixture[];
  ledger: {
    round: number;
    description: string;
    amount: number;
    season: number;
  }[];
  news: string[];
  history: string[];
};
export type Career = {
  id: string;
  name: string;
  revision: number;
  state: State;
  table: Row[];
  management: {
    window: TransferWindow;
    wageBill: number;
    expiringContracts: number;
  };
};
export type PlayerContract = {
  playerId: number;
  clubId: string;
  startSeason: number;
  startRound: number;
  endSeason: number;
  wage: number;
  signingBonus: number;
  revision: number;
  active: boolean;
};
export type TransferWindow = {
  isOpen: boolean;
  name: string;
  closingRound: number | null;
  nextOpeningRound: number | null;
  description: string;
};
export type TransferOffer = {
  agentCommission?: number;
  agentName?: string;
  installments?: number;
  releaseClause?: number;
  id: string;
  playerId: number;
  playerName: string;
  kind: "inbound" | "outbound" | "renewal";
  fromClub: string;
  toClub: string;
  fee: number;
  wage: number;
  years: number;
  signingBonus: number;
  season: number;
  createdRound: number;
  responseRound: number;
  expiresRound: number;
  contractRevision: number;
  status: string;
  message: string;
  counterFee: number | null;
  counterWage: number | null;
};
export type TransferRecord = {
  season: number;
  round: number;
  playerId: number;
  playerName: string;
  fromClub: string;
  toClub: string;
  fee: number;
  signingBonus: number;
  wage: number;
  endSeason: number;
  kind: string;
};
export type PlayerQuote = {
  agentCommission?: number;
  agentName?: string;
  playerId: number;
  playerName: string;
  fromClub: string;
  kind: string;
  fee: number;
  wage: number;
  signingBonus: number;
  years: number;
  minYears: number;
  available: boolean;
  reason: string;
};
export type MarketCandidate = {
  player: Player;
  clubId: string;
  askingFee: number;
  wageDemand: number;
  contractEndSeason: number | null;
  available: boolean;
  reason: string;
};
export type SeasonStanding = {
  clubId: string;
  points: number;
  played: number;
  wins: number;
  draws: number;
  losses: number;
  goals: number;
  against: number;
  difference: number;
};
export type PastSeason = {
  clubs?: Club[];
  divisions?: {
    managedDivision: string;
    otherClubs: Club[];
    promoted: string[];
    relegated: string[];
    playoffs: Fixture[];
  } | null;
  playerStatsFromRound?: number;
  playerStats?: PlayerStats[];
  season: number;
  champion: string;
  table: SeasonStanding[];
  fixtures?: Fixture[];
};
let token = "";
let refresh: Promise<unknown> | null = null;
export function setToken(value: string) {
  token = value;
}
export async function api<T>(
  path: string,
  body?: unknown,
  retry = true,
  method?: "DELETE",
): Promise<T> {
  const r = await fetch("/api" + path, {
    method: method ?? (body === undefined ? "GET" : "POST"),
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (
    r.status === 401 &&
    retry &&
    ![
      "/auth/register",
      "/auth/login",
      "/auth/refresh",
      "/auth/logout",
    ].includes(path)
  ) {
    refresh ??= api<{ token: string }>("/auth/refresh", {}, false)
      .then((v) => setToken(v.token))
      .finally(() => {
        refresh = null;
      });
    await refresh;
    return api(path, body, false, method);
  }
  if (!r.ok) {
    const e = await r.json().catch(() => ({}));
    throw new Error(
      e.message ??
        (r.status === 401
          ? "Sessão expirada. Entre novamente."
          : `Não foi possível concluir (${r.status}).`),
    );
  }
  return r.status === 204 ? (undefined as T) : await r.json();
}
export const money = (n: number) =>
  new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: "BRL",
    maximumFractionDigits: n >= 1000000 ? 2 : 0,
    notation: n >= 1000000 ? "compact" : "standard",
  }).format(n);
export const moneyExact = (n: number) =>
  new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: "BRL",
    maximumFractionDigits: 0,
  }).format(n);

export type SubstitutionPlan = {
  minute: number;
  outPlayerId: number;
  inPlayerId: number;
};
export type PlayerStats = {
  playerId: number;
  playerName: string;
  clubId: string;
  appearances: number;
  starts: number;
  minutes: number;
  goals: number;
  shots: number;
  passes: number;
  interceptions: number;
  fouls: number;
  yellowCards: number;
  redCards: number;
};
export const availability = (p: Player) =>
  p.injuryDays > 0
    ? `${p.injuryName || "Lesão"} · ${p.injuryDays} dias`
    : p.suspensionMatches > 0
      ? `Suspenso · ${p.suspensionMatches} jogo(s)`
      : "Disponível";

export type TrainingPlan = { days: string[]; intensity: string };
export type PlayerDevelopment = {
  playerId: number;
  focus: string;
  attributes: Record<string, number>;
  experience: Record<string, number>;
};
export type TrainingPlayerReport = {
  group?: string;
  playerId: number;
  name: string;
  injured: boolean;
  fitnessBefore: number;
  fitnessAfter: number;
  ratingBefore: number;
  ratingAfter: number;
  experience: Record<string, number>;
  gains: { attribute: string; before: number; after: number }[];
};
export type TrainingReport = {
  season: number;
  round: number;
  plan: TrainingPlan;
  players: TrainingPlayerReport[];
};
export type TrainingData = {
  revision: number;
  season: number;
  round: number;
  plan: TrainingPlan;
  players: { player: Player; development: PlayerDevelopment; group?: string }[];
  reports: TrainingReport[];
  sessions: string[];
  intensities: string[];
  presets: { name: string; plan: TrainingPlan }[];
};

export type YouthProspect = {
  player: Player;
  region: string;
  signingFee: number;
};
export type ScoutingMission = {
  season: number;
  startedRound: number;
  completionRound: number;
  expiresRound: number;
  region: string;
  position: string;
  status: string;
};
export type YouthData = {
  revision: number;
  season: number;
  round: number;
  academy: Player[];
  scouting: ScoutingMission | null;
  prospects: YouthProspect[];
  history: string[];
  regions: string[];
  positions: string[];
  capacity: number;
  searchFee: number;
  promotionFee: number;
  weeklyCost: number;
  contractEndSeason: number;
  professionalCount: number;
  balance: number;
};

export type TacticalPlan={tempo?: string; defensiveTransition?: string;positions?:Record<number,{x:number;z:number}>;style:string;pressure:string;width:string;buildShort:boolean;overlap:boolean;shootMore:boolean;avoidFouls:boolean;offsideTrap:boolean;attackSide:string;roles?:Record<number,string>};

