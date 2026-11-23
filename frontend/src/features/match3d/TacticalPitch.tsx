import { useMemo, useRef, useState, type PointerEvent } from "react";
import type { CSSProperties } from "react";
import type {
  MatchPlayer,
  ReplayFrame,
  TacticalPlan,
} from "../../services/api";
import type { DeskPlayer } from "./tacticalDeskModel";

const positionViews = [
  "Com bola",
  "Sem bola",
  "Transição ofensiva",
  "Transição defensiva",
  "Posicionamento médio",
];
const overlays = [
  ...positionViews,
  "Zonas de atuação",
  "Cobertura defensiva",
  "Linhas de passe",
  "Mapa de calor",
];

export const roles = [
  "Livre",
  "Apoio",
  "Infiltrar",
  "Pivô",
  "Falso nove",
  "Ultrapassar",
  "Marcar",
  "Cobertura",
  "Subir mais",
  "Recuar",
  "Manter posição",
  "Movimentar livremente",
  "Cortar para dentro",
  "Atacar profundidade",
  "Aproximar para receber",
  "Pressionar mais",
  "Pressionar menos",
  "Marcar jogador",
  "Marcar por zona",
  "Passar curto",
  "Arriscar passes",
  "Chutar mais",
  "Chutar menos",
  "Driblar mais",
  "Driblar menos",
  "Cruzar mais",
  "Atacar a área",
  "Fazer ultrapassagens",
  "Preservar energia",
];

export function Kit({ color, number }: { color: string; number?: number }) {
  return (
    <span
      className="desk-kit"
      style={{ "--kit": color } as CSSProperties}
      aria-hidden
    >
      {number ?? "•"}
    </span>
  );
}

export default function TacticalPitch({
  players,
  frames,
  teamIndex,
  secondHalf,
  squad,
  plan,
  referencePositions,
  color,
  keeper,
  editable,
  disabled,
  setPlan,
  liveOverlays = true,
}: {
  players: DeskPlayer[];
  frames: ReplayFrame[];
  teamIndex: number;
  secondHalf?: number;
  squad: MatchPlayer[];
  plan: TacticalPlan;
  referencePositions?: TacticalPlan["positions"];
  color: string;
  keeper: string;
  editable: boolean;
  disabled: boolean;
  setPlan: (plan: TacticalPlan) => void;
  // Before the match there are no recorded frames, so only the overlays drawn from the plan apply.
  liveOverlays?: boolean;
}) {
  const [mode, setMode] = useState("Plano Tático");
  const [selected, select] = useState<number>();
  const [overlay, setOverlay] = useState("");
  const averages = useMemo(() => {
    const sums = new Map<number, { x: number; y: number; count: number }>();
    if (!positionViews.includes(overlay)) return sums;
    let previous: number | undefined;
    let changedAt: number | undefined;
    for (const frame of frames) {
      if (frame.possession !== previous) {
        changedAt = previous === undefined ? undefined : frame.second;
        previous = frame.possession;
      }
      if (overlay !== "Posicionamento médio" && frame.possession === undefined)
        continue;
      if (
        (overlay === "Com bola" || overlay === "Transição ofensiva") &&
        frame.possession !== teamIndex
      )
        continue;
      if (
        (overlay === "Sem bola" || overlay === "Transição defensiva") &&
        frame.possession === teamIndex
      )
        continue;
      if (
        overlay.startsWith("Transição") &&
        (changedAt === undefined || frame.second - changedAt > 72)
      )
        continue;
      const side =
        (teamIndex === 0 ? 1 : -1) *
        (secondHalf !== undefined && frame.second > secondHalf ? -1 : 1);
      for (const player of frame.players) {
        if (player.team !== teamIndex || player.active === false) continue;
        const sum = sums.get(player.id) ?? { x: 0, y: 0, count: 0 };
        sum.x += ((player.z * side + 34) / 68) * 100;
        sum.y += ((52.5 - player.x * side) / 105) * 100;
        sum.count++;
        sums.set(player.id, sum);
      }
    }
    return sums;
  }, [frames, overlay, secondHalf, teamIndex]);
  const heat = useMemo(() => {
    if (overlay !== "Mapa de calor") return [];
    const cells = new Map<number, number>();
    for (const frame of frames)
      for (const pose of frame.players) {
        if (
          pose.team !== teamIndex ||
          pose.active === false ||
          (selected !== undefined && pose.id !== selected)
        )
          continue;
        const side =
          (teamIndex === 0 ? 1 : -1) *
          (secondHalf !== undefined && frame.second > secondHalf ? -1 : 1);
        const col = Math.max(
          0,
          Math.min(7, Math.floor(((pose.z * side + 34) / 68) * 8)),
        );
        const row = Math.max(
          0,
          Math.min(9, Math.floor(((52.5 - pose.x * side) / 105) * 10)),
        );
        const key = row * 8 + col;
        cells.set(key, (cells.get(key) ?? 0) + 1);
      }
    const max = Math.max(1, ...cells.values());
    return [...cells].map(([key, count]) => ({
      key,
      x: (key % 8) * 12.5,
      y: Math.floor(key / 8) * 10,
      opacity: 0.15 + (0.7 * count) / max,
    }));
  }, [overlay, frames, teamIndex, selected, secondHalf]);
  const surface = useRef<HTMLDivElement>(null);
  const drag = useRef<{ id: number; x: number; y: number } | null>(null);
  const current = players.find((p) => p.pose.id === selected);
  const origin = current ?? players.find((p) => p.pose.position !== "GOL");
  const receivers =
    overlay === "Linhas de passe" && origin
      ? players.filter(
          (p) =>
            p !== origin &&
            Math.hypot((p.x - origin.x) * 0.68, (p.y - origin.y) * 1.05) < 35,
        )
      : [];
  const adjust = (id: number, x: number, y: number) => {
    const clamp = (value: number, max: number) =>
      Math.max(-max, Math.min(max, value));
    setPlan({
      ...plan,
      positions: {
        ...plan.positions,
        [id]: { x: clamp(1 - y / 50, 0.88), z: clamp(x / 50 - 1, 0.83) },
      },
    });
  };
  function move(event: PointerEvent) {
    const start = drag.current;
    if (
      disabled ||
      !start ||
      !surface.current ||
      Math.hypot(event.clientX - start.x, event.clientY - start.y) < 5
    )
      return;
    const rect = surface.current.getBoundingClientRect();
    adjust(
      start.id,
      ((event.clientX - rect.left) / rect.width) * 100,
      ((event.clientY - rect.top) / rect.height) * 100,
    );
  }
  return (
    <>
      <div className="desk-tabs" aria-label="Visualização do campo">
        {[
          "Escalação",
          "Plano Tático",
          ...(editable ? ["Instruções Individuais"] : []),
        ].map((label) => (
          <button
            type="button"
            key={label}
            aria-pressed={mode === label}
            onClick={() => setMode(label)}
          >
            {label}
          </button>
        ))}
      </div>
      {mode === "Instruções Individuais" ? (
        <div className="desk-individual">
          {players.map(({ pose }) => (
            <label key={pose.id}>
              {pose.name} · {pose.position}
              <select
                disabled={disabled}
                value={plan.roles?.[pose.id] ?? "Livre"}
                onChange={(e) =>
                  setPlan({
                    ...plan,
                    roles: { ...plan.roles, [pose.id]: e.target.value },
                  })
                }
              >
                {(pose.position === "GOL" ? roles.slice(0, 2) : roles).map(
                  (role) => (
                    <option key={role}>{role}</option>
                  ),
                )}
              </select>
            </label>
          ))}
        </div>
      ) : (
        <>
          <div
            ref={surface}
            className="desk-pitch"
            aria-label={
              mode === "Plano Tático"
                ? "Posições táticas editáveis"
                : "Escalação em campo"
            }
            onPointerMove={move}
            onPointerUp={() => {
              drag.current = null;
            }}
            onPointerCancel={() => {
              drag.current = null;
            }}
          >
            {heat.map((cell) => (
              <div
                key={cell.key}
                className="desk-heat"
                style={{
                  left: cell.x + "%",
                  top: cell.y + "%",
                  opacity: cell.opacity,
                }}
              />
            ))}
            <div className="desk-center-circle" />
            {overlay === "Zonas de atuação" && (
              <div className="desk-zones" aria-hidden>
                {["Ataque", "Meio", "Defesa"].map((zone) => (
                  <span key={zone}>{zone}</span>
                ))}
              </div>
            )}
            {overlay === "Cobertura defensiva" &&
              players
                .filter(
                  (p) => p.pose.position === "ZAG" || p.pose.position === "LAT",
                )
                .map((p) => (
                  <i
                    key={p.pose.id}
                    className="desk-cover"
                    style={{ left: p.x + "%", top: p.y + "%" }}
                  />
                ))}
            {receivers.length > 0 && origin && (
              <svg
                className="desk-lanes"
                viewBox="0 0 100 100"
                preserveAspectRatio="none"
                aria-hidden
              >
                {receivers.map((p) => (
                  <line
                    key={p.pose.id}
                    x1={origin.x}
                    y1={origin.y}
                    x2={p.x}
                    y2={p.y}
                  />
                ))}
              </svg>
            )}
            <div className="desk-area top" />
            <div className="desk-area bottom" />
            <div className="desk-small-area top" />
            <div className="desk-small-area bottom" />
            {players.map((p) => {
              const average = averages.get(p.pose.id);
              const point =
                mode === "Plano Tático"
                  ? (plan.positions?.[p.pose.id] ??
                    referencePositions?.[p.pose.id])
                  : undefined;
              const x = Math.max(
                10,
                Math.min(
                  90,
                  average
                    ? average.x / average.count
                    : point && !overlay
                      ? (point.z + 1) * 50
                      : p.x,
                ),
              );
              const y = Math.max(
                8,
                Math.min(
                  92,
                  average
                    ? average.y / average.count
                    : point && !overlay
                      ? (1 - point.x) * 50
                      : p.y,
                ),
              );
              const canMove =
                editable &&
                !disabled &&
                !overlay &&
                mode === "Plano Tático" &&
                p.pose.position !== "GOL";
              return (
                <button
                  key={p.pose.id}
                  className="desk-player"
                  style={{
                    left: x + "%",
                    top: y + "%",
                    touchAction: canMove ? "none" : undefined,
                  }}
                  aria-pressed={selected === p.pose.id}
                  onClick={() => select(p.pose.id)}
                  onPointerDown={(e) => {
                    if (!canMove || e.button !== 0) return;
                    drag.current = {
                      id: p.pose.id,
                      x: e.clientX,
                      y: e.clientY,
                    };
                    e.currentTarget.setPointerCapture(e.pointerId);
                    select(p.pose.id);
                  }}
                  onKeyDown={(e) => {
                    const offset = {
                      ArrowUp: [0, -3],
                      ArrowDown: [0, 3],
                      ArrowLeft: [-3, 0],
                      ArrowRight: [3, 0],
                    }[e.key];
                    if (!canMove || !offset) return;
                    e.preventDefault();
                    adjust(p.pose.id, x + offset[0], y + offset[1]);
                  }}
                >
                  <Kit
                    color={p.pose.position === "GOL" ? keeper : color}
                    number={
                      squad.findIndex((s) => s.id === p.pose.id) + 1 ||
                      undefined
                    }
                  />
                  <span className="desk-player-name">
                    {p.pose.name ?? p.player?.name}
                    {p.yellow > 0 && <i title="Cartão amarelo" />}
                    {p.injured && <b title="Lesão">+</b>}
                    {p.matchRating !== undefined && (
                      <small
                        data-band={
                          p.matchRating >= 7
                            ? "good"
                            : p.matchRating < 6
                              ? "low"
                              : "mid"
                        }
                        title="Nota pelos eventos da partida"
                      >
                        {p.matchRating.toFixed(1)}
                      </small>
                    )}
                  </span>
                </button>
              );
            })}
          </div>
          <label className="desk-overlay">
            Visualização
            <select
              value={overlay}
              onChange={(event) => setOverlay(event.target.value)}
            >
              <option value="">Sem sobreposição</option>
              {(liveOverlays ? overlays : overlays.slice(positionViews.length, -1)).map((view) => (
                <option key={view}>{view}</option>
              ))}
            </select>
            {selected !== undefined && overlay && (
              <button onClick={() => select(undefined)}>Ver equipe</button>
            )}
          </label>
          {positionViews.includes(overlay) && averages.size === 0 && (
            <p className="desk-overlay-caption">
              Aguardando amostras desta fase.
            </p>
          )}
          {overlay === "Cobertura defensiva" && (
            <p className="desk-overlay-caption">
              Raio de referência ao redor dos defensores.
            </p>
          )}
          {overlay === "Linhas de passe" && (
            <p className="desk-overlay-caption">
              Opções a até 35 m. Selecione um jogador para mudar a origem.
            </p>
          )}
          {current && (
            <div className="desk-selection">
              <strong>{current.player?.name ?? current.pose.name}</strong>
              <p>
                {current.pose.position} ·{" "}
                {plan.roles?.[current.pose.id] ?? "Livre"}
                {current.player && (
                  <>
                    {" "}
                    · Condição {current.pose.fitness ?? current.player.fitness}%
                    · Overall {current.player.rating}
                  </>
                )}
              </p>
              {current.player?.skills && (
                <details>
                  <summary>Ver atributos</summary>
                  <dl>
                    {Object.entries(current.player.skills).map(
                      ([key, value]) => (
                        <div key={key}>
                          <dt>{key}</dt>
                          <dd>{value}</dd>
                        </div>
                      ),
                    )}
                  </dl>
                </details>
              )}
            </div>
          )}
        </>
      )}
    </>
  );
}
