import { useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeftRight,
  Bell,
  ChartColumn,
  ChartNoAxesCombined,
  ClipboardList,
  Maximize,
  Play,
  Settings,
  Target,
} from "lucide-react";
import { FutbrxLogo } from "../../components/FutbrxLogo";
import type { BroadcastPacket, Club } from "../../services/api";
import { tacticalDeskModel } from "./tacticalDeskModel";
import "./tacticalDesk.css";

const tabs = [
  ["Estatísticas", ChartColumn],
  ["Táticas", Target],
  ["Substituições", ArrowLeftRight],
  ["Análise", ChartNoAxesCombined],
  ["Relatório", ClipboardList],
] as const;
const metrics = [
  ["Gols", "goal"],
  ["Finalizações sem gol", "shot"],
  ["Passes completos", "pass"],
  ["Interceptações", "interception"],
  ["Faltas", "foul"],
  ["Cartões amarelos", "yellow_card"],
  ["Expulsões", "red_card"],
  ["Substituições", "substitution"],
];

function TeamBadge({ club, path }: { club: Club; path?: string }) {
  const [failed, setFailed] = useState(false);
  return path && !failed ? (
    <img
      className="desk-badge"
      src={path}
      alt=""
      onError={() => setFailed(true)}
    />
  ) : (
    <span
      className="desk-badge desk-monogram"
      style={{ borderColor: club.color }}
    >
      {club.short}
    </span>
  );
}

export default function TacticalDesk({
  packet,
  home,
  away,
  tactics,
  pitch,
  players,
  substitutions,
  quick,
  message,
  error,
  busy,
  onResume,
  onSpeed,
}: {
  packet: BroadcastPacket;
  home: Club;
  away: Club;
  tactics: ReactNode;
  pitch: ReactNode;
  players: ReactNode;
  substitutions: ReactNode;
  quick: ReactNode;
  message: string;
  error: string;
  busy: boolean;
  onResume: () => void;
  onSpeed: (speed: number) => void;
}) {
  const [tab, setTab] = useState<string>("Táticas");
  const [focusField, setFocusField] = useState(false);
  const [mobilePanel, setMobilePanel] = useState("Campo");
  const mobilePanels = ["Plano", "Campo", "Jogadores", "Trocas"];
  const [fullError, setFullError] = useState("");
  const credits = useQuery({
    queryKey: ["crest-credits"],
    queryFn: async () => {
      const response = await fetch("/data/crest-credits.json");
      if (!response.ok) throw new Error("Escudos indisponíveis");
      return response.json() as Promise<{
        clubs: { clubId: string; path?: string }[];
      }>;
    },
  });
  const badge = (club: Club) => (
    <TeamBadge
      key={club.id}
      club={club}
      path={credits.data?.clubs.find((c) => c.clubId === club.id)?.path}
    />
  );
  const view = tacticalDeskModel(packet);
  const clock = [Math.floor(view.second / 60), view.second % 60]
    .map((n) => String(n).padStart(2, "0"))
    .join(":");
  const extra =
    (view.half === "1º tempo" && view.second >= 2700) ||
    (view.half === "2º tempo" && view.second >= 5400);
  const last = view.events
    .filter((e) => !["pass", "interception", "play_pattern"].includes(e.type))
    .at(-1);
  const tactical = tab === "Táticas" || tab === "Substituições";
  const managed = packet.session.managedTeam === home.id ? home : away;
  const tired = view.players
    .flatMap(({ pose, player }) =>
      player && pose.position !== "GOL"
        ? [
            {
              name: player.name,
              fitness: pose.fitness ?? player.fitness,
              live: pose.fitness != null,
            },
          ]
        : [],
    )
    .sort((a, b) => a.fitness - b.fitness)[0];
  const alerts = view.events.filter(
    (e) =>
      ["injury", "red_card", "second_yellow"].includes(e.type) &&
      e.team === managed.id,
  );
  const history =
    tab === "Análise"
      ? view.events.filter((e) =>
          ["play_pattern", "tactics_change"].includes(e.type),
        )
      : view.events.filter((e) => !["pass", "interception"].includes(e.type));

  return (
    <div className="tactical-desk">
      <header className="desk-toolbar">
        <FutbrxLogo />
        <nav aria-label="Painéis da partida">
          <button className="desk-live" disabled={busy} onClick={onResume}>
            <Play size={17} />
            Ao Vivo
          </button>
          {tabs.map(([label, Icon]) => (
            <button
              key={label}
              aria-pressed={tab === label}
              onClick={() => {
                setTab(label);
                setFocusField(false);
                setMobilePanel(
                  label === "Substituições" ? "Jogadores" : "Campo",
                );
              }}
            >
              <Icon size={17} />
              {label}
            </button>
          ))}
        </nav>
        <div className="desk-tools">
          <details>
            <summary aria-label="Notificações" title="Notificações">
              <Bell size={18} />
            </summary>
            <div>
              {alerts.length ? (
                alerts
                  .slice(-6)
                  .reverse()
                  .map((e, i) => (
                    <p key={i}>
                      {e.minute}′ {e.text}
                    </p>
                  ))
              ) : (
                <p>Sem cartões vermelhos ou lesões registrados no seu time.</p>
              )}
            </div>
          </details>
          <details>
            <summary
              aria-label="Configurações da partida"
              title="Configurações"
            >
              <Settings size={18} />
            </summary>
            <div>
              <label>
                Velocidade{" "}
                <select
                  disabled={busy}
                  value={packet.session.speed}
                  onChange={(e) => onSpeed(Number(e.target.value))}
                >
                  {[1, 2, 4, 6, 8, 12, 24, 48, 96].map((speed) => (
                    <option key={speed} value={speed}>
                      {speed}×
                    </option>
                  ))}
                </select>
              </label>
            </div>
          </details>
          <span>{view.half}</span>
          <button disabled={busy} onClick={onResume}>
            {packet.session.halfTimeAwaiting ? "Ir ao 2º tempo" : "Retomar"}
          </button>
        </div>
      </header>
      <section className="desk-score" aria-label="Placar da partida">
        <h2>
          {badge(home)}
          {home.name}
        </h2>
        <div className="desk-score-center">
          <strong>
            {view.score[0]} - {view.score[1]}
          </strong>
          <span>
            {view.half} · {clock}
            {extra && " · Acréscimos"}
          </span>
        </div>
        <h2>
          {away.name}
          {badge(away)}
        </h2>
      </section>
      {tactical && (
        <div
          className="desk-tabs desk-mobile-tabs"
          aria-label="Painéis táticos no celular"
        >
          {mobilePanels.map((panel) => (
            <button
              key={panel}
              aria-pressed={mobilePanel === panel}
              onClick={() => {
                setFocusField(false);
                setMobilePanel(panel);
              }}
            >
              {panel}
            </button>
          ))}
        </div>
      )}
      {tactical && (
        <div
          className={"desk-layout" + (focusField ? " desk-expanded" : "")}
          data-mobile={mobilePanel}
        >
          <aside className="desk-panel desk-tactics">
            <h3>Plano Tático</h3>
            {tactics}
          </aside>
          <section className="desk-panel desk-field" aria-label="Campo tático">
            {pitch}
          </section>
          <section
            className="desk-panel desk-players"
            aria-label="Lista de jogadores"
          >
            {players}
          </section>
          <aside className="desk-exchanges" aria-label="Trocas e confirmação">
            {substitutions}
          </aside>
        </div>
      )}
      {tab === "Estatísticas" && (
        <section className="desk-panel desk-statistics">
          <h3>
            {home.name} × {away.name}
          </h3>
          {metrics.map(([label, type]) => (
            <div key={type}>
              <strong>
                {
                  view.events.filter(
                    (e) => e.type === type && e.team === home.id,
                  ).length
                }
              </strong>
              <span>{label}</span>
              <strong>
                {
                  view.events.filter(
                    (e) => e.type === type && e.team === away.id,
                  ).length
                }
              </strong>
            </div>
          ))}
        </section>
      )}
      {(tab === "Análise" || tab === "Relatório") && (
        <section className="desk-panel desk-history">
          <h3>
            {tab === "Análise" ? "Análise da partida" : "Histórico da partida"}
          </h3>
          {tab === "Análise" && (
            <>
              {tired && tired.fitness < 85 && (
                <p>
                  Auxiliar: considere poupar {tired.name}. Condição{" "}
                  {tired.live ? "em jogo" : "inicial"} de {tired.fitness}%.
                </p>
              )}
              {alerts.at(-1) && (
                <p>
                  Auxiliar: revise a escalação após o lance aos{" "}
                  {alerts.at(-1)!.minute}′.
                </p>
              )}
            </>
          )}
          {history.length ? (
            history
              .slice()
              .reverse()
              .map((e, i) => (
                <p key={i}>
                  <b>{e.minute}′</b> {e.text}
                </p>
              ))
          ) : (
            <p>Aguardando acontecimentos da partida.</p>
          )}
        </section>
      )}
      <div className="desk-feed">
        <ArrowLeftRight size={18} aria-hidden />
        <b>{last ? last.minute + "′" : "—"}</b>
        <span role={error ? "alert" : "status"}>
          {error ||
            fullError ||
            message ||
            last?.text ||
            "Aguardando o início da partida."}
        </span>
        <button
          onClick={() => setTab(tab === "Relatório" ? "Táticas" : "Relatório")}
        >
          {tab === "Relatório" ? "Voltar às táticas" : "Histórico"}
        </button>
      </div>
      <footer className="desk-bottom">
        {quick}
        <div className="desk-visual">
          <button
            aria-pressed={focusField}
            onClick={() => {
              setTab("Táticas");
              setFocusField(!focusField);
            }}
          >
            <Target size={18} />
            {focusField ? "Ver painéis" : "Expandir campo"}
          </button>
          <button
            onClick={async (e) => {
              const dialog = e.currentTarget.closest("dialog");
              try {
                if (document.fullscreenElement) await document.exitFullscreen();
                else if (dialog?.requestFullscreen)
                  await dialog.requestFullscreen();
                else setFullError("Tela cheia indisponível neste navegador.");
              } catch {
                setFullError("Não foi possível alterar a tela cheia.");
              }
            }}
          >
            <Maximize size={18} />
            <span>Tela cheia</span>
          </button>
        </div>
      </footer>
    </div>
  );
}
