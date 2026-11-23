import { mocapTimings } from "./mocapTimings.ts";
export type Quality = "low" | "balanced" | "high";
export const qualityProfiles = {
  low: { label: "Leve", dpr: 1, shadows: false, rigged: true },
  balanced: {
    label: "Equilibrada",
    dpr: 1.25,
    shadows: false,
    rigged: true,
  },
  high: { label: "Alta", dpr: 1.6, shadows: true, rigged: true },
} as const;
export function initialQuality(memory?: number, cores?: number): Quality {
  return (memory !== undefined && memory <= 4) ||
    (cores !== undefined && cores <= 4)
    ? "low"
    : "balanced";
}
export const footballAnimations = [
  { action: "idle", clip: "Football_Idle", label: "Repouso" },
  { action: "run", clip: "Football_Run", label: "Corrida" },
  { action: "sprint", clip: "Football_Sprint", label: "Arrancada" },
  { action: "dribble", clip: "Football_Dribble", label: "Condução e drible" },
  { action: "pass", clip: "Football_Pass", label: "Passe por dentro" },
  { action: "shot", clip: "Football_Shot", label: "Chute com o peito do pé" },
  { action: "cross", clip: "Football_Cross", label: "Cruzamento" },
  { action: "header", clip: "Football_Header", label: "Cabeceio" },
  {
    action: "keeper_ready",
    clip: "Football_KeeperReady",
    label: "Prontidão do goleiro",
  },
  { action: "catch", clip: "Football_Catch", label: "Encaixe do goleiro" },
  {
    action: "dive_left",
    clip: "Football_SaveLeft",
    label: "Defesa à esquerda",
  },
  {
    action: "dive_right",
    clip: "Football_SaveRight",
    label: "Defesa à direita",
  },
  { action: "celebrate", clip: "Football_Celebrate", label: "Comemoração" },
  { action: "enter", clip: "Football_Walk", label: "Entrada em campo" },
  {
    action: "tackle",
    clip: "Football_Tackle",
    label: "Carrinho e recuperação",
  },
  { action: "fall", clip: "Football_Fall", label: "Queda e levantar" },
  {
    action: "stumble",
    clip: "Football_Stumble",
    label: "Desequilíbrio no contato",
  },
  { action: "shield", clip: "Football_Shield", label: "Proteção de bola" },
  { action: "challenge", clip: "Football_Challenge", label: "Desarme em pé" },
  { action: "air_duel", clip: "Football_AirDuel", label: "Disputa aérea" },
  {
    action: "throw_in",
    clip: "Football_ThrowIn",
    label: "Cobrança de lateral",
  },
  {
    action: "penalty_kick",
    clip: "Football_Penalty",
    label: "Cobrança de pênalti",
  },
  {
    action: "celebrate_2",
    clip: "Football_Celebrate2",
    label: "Comemoração 2",
  },
  {
    action: "celebrate_3",
    clip: "Football_Celebrate3",
    label: "Comemoração 3",
  },
  { action: "dive_low", clip: "Football_SaveLow", label: "Defesa rasteira" },
  { action: "defend", clip: "Football_Defend", label: "Marcação" },
  {
    action: "yellow_card",
    clip: "Football_YellowCard",
    label: "Árbitro: amarelo",
  },
  { action: "red_card", clip: "Football_RedCard", label: "Árbitro: vermelho" },
  {
    action: "second_yellow_card",
    clip: "Football_SecondYellow",
    label: "Árbitro: segundo amarelo",
  },
] as const;
export function animationForAction(action: string, keeper = false) {
  return (
    footballAnimations.find((a) => a.action === action)?.clip ??
    (action === "kick"
      ? "Football_Pass"
      : action === "save"
        ? "Football_Catch"
        : keeper
          ? "Football_KeeperReady"
          : "Football_Idle")
  );
}
export const contactOffsets: Record<string, number> = {
  pass: 0.432,
  shot: 0.572,
  cross: 0.598,
  header: 0.696,
  catch: 0.65,
  dive_left: 0.752,
  dive_right: 0.752,
  kick: 0.432,
  save: 0.65,
  tackle: 0,
  fall: 0,
  stumble: 0,
  shield: 0,
  challenge: 0,
  air_duel: 0.55,
};
Object.entries(mocapTimings)
  .filter(
    ([a]) => !["defend", "celebrate", "celebrate_2", "celebrate_3"].includes(a),
  )
  .forEach(([a, t]) => {
    contactOffsets[a] = t.contact;
  });
export function isOneShot(action: string) {
  return action in contactOffsets;
}
export type PresentationPose = {
  id: number;
  action: string;
  actionSecond?: number;
  goalkeeper?: boolean;
  locomotionDistance?: number;
  skinIndex?: number;
  hairIndex?: number;
  shirtNumber?: number;
  exactTime?: boolean;
  contactOffset?: number;
  flag?: boolean;
  card?: "yellow" | "red";
};
type Pose = {
  id: number;
  action: string;
  actionSecond?: number;
  position?: string;
};
export function resolvePresentationPose(
  a: Pose,
  b: Pose,
  second: number,
  frameSecond: number,
  events: { player: number; type: string; second?: number; minute: number }[],
  keeper: boolean,
  timeScale = 1,
): PresentationPose {
  if (timeScale !== 1) {
    a = {
      ...a,
      actionSecond:
        a.actionSecond === undefined ? undefined : a.actionSecond / timeScale,
    };
    b = {
      ...b,
      actionSecond:
        b.actionSecond === undefined ? undefined : b.actionSecond / timeScale,
    };
    second /= timeScale;
    frameSecond /= timeScale;
  }
  let chosen = a;
  if (
    a.id === b.id &&
    a.action !== b.action &&
    isOneShot(b.action) &&
    b.actionSecond !== undefined &&
    b.actionSecond >= 0 &&
    second >= b.actionSecond - (contactOffsets[b.action] ?? 0)
  )
    chosen = b;
  let action = chosen.action,
    actionSecond =
      chosen.actionSecond !== undefined && chosen.actionSecond >= 0
        ? chosen.actionSecond
        : frameSecond;
  if (action === "kick") {
    const e = events.find(
      (e) =>
        e.player === chosen.id &&
        ["pass", "shot", "goal"].includes(e.type) &&
        (e.second ?? e.minute * 60) >= second &&
        (e.second ?? e.minute * 60) - second <= 8,
    );
    action = e && e.type !== "pass" ? "shot" : "pass";
    if (e) actionSecond = (e.second ?? e.minute * 60) - 6;
  }
  return {
    id: chosen.id,
    action,
    actionSecond,
    goalkeeper: chosen.position === "GOL" || keeper,
  };
}
