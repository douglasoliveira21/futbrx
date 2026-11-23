import { useId, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Search, Shield, HeartPulse, Trophy } from "lucide-react";
import { Button } from "../../components/ui/button";
import { money, type Career, type Player } from "../../services/api";

export type TeamAction = (
  type: string,
  value?: string,
  extra?: string,
  playerId?: number,
) => void;
export const available = (p: Player) => !p.injuryDays && !p.suspensionMatches;
export const roleOrder = (p: Player) =>
  ({ GOL: 0, ZAG: 1, LAT: 2, MEI: 3, ATA: 4 })[p.position] ?? 5;
export function Shirt({
  color,
  number,
  short,
}: {
  color: string;
  number: number;
  short: string;
}) {
  const id = useId();
  return (
    <svg className="kit-portrait" viewBox="0 0 180 160" aria-hidden="true">
      <defs>
        <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
          <stop stopColor={color} />
          <stop offset="1" stopColor="#111c2d" />
        </linearGradient>
      </defs>
      <ellipse cx="90" cy="150" rx="47" ry="6" fill="#0004" />
      <path
        d="M62 15L35 25 9 62l29 16 15-20v84q37 10 74 0V58l15 20 29-16-26-37-27-10q-28 15-56 0"
        fill={`url(#${id})`}
        stroke="#ffffff25"
        strokeWidth="1"
      />
      <path d="M62 15q28 24 56 0l-7-5q-21 13-42 0z" fill="#e8f1dd" />
      <path d="M54 52v83M126 52v83" stroke="#ffffff12" strokeWidth="2" />
      <path d="M37 27l18 23M143 27l-18 23" stroke="#ffffff30" strokeWidth="3" />
      <text
        x="90"
        y="59"
        textAnchor="middle"
        fill="white"
        fontFamily="Arial"
        fontWeight="700"
        fontSize="10"
        letterSpacing="2"
      >
        {short}
      </text>
      <text
        x="90"
        y="115"
        textAnchor="middle"
        fill="#f5f8eb"
        fontFamily="Impact, sans-serif"
        fontSize="50"
      >
        {number > 0 ? number : ""}
      </text>
    </svg>
  );
}

export default function SquadRoom({
  c,
  action,
  busy,
}: {
  c: Career;
  action: TeamAction;
  busy: boolean;
}) {
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [position, setPosition] = useState("");
  const [group, setGroup] = useState("todos");
  const [selected, setSelected] = useState(c.state.squad[0]?.id);
  const club = c.state.clubs.find((x) => x.id === c.state.clubId)!;
  const starters = c.state.squad.filter((p) => p.starter);
  const players = c.state.squad
    .filter(
      (p) =>
        p.name.toLowerCase().includes(search.toLowerCase()) &&
        (!position || p.position === position) &&
        (group === "todos" ||
          (group === "titulares" && p.starter) ||
          (group === "reservas" && !p.starter) ||
          (group === "indisponiveis" && !available(p))),
    )
    .sort((a, b) => b.rating - a.rating || a.name.localeCompare(b.name));
  const player = c.state.squad.find((p) => p.id === selected) ?? players[0];
  const number = (p: Player) =>
    c.state.squad.findIndex((x) => x.id === p.id) + 1;
  return (
    <div
      className="squad-room"
      style={{ "--team-color": club.color } as React.CSSProperties}
    >
      <div className="locker-summary">
        <div>
          <Shield />
          <span>
            SEU GRUPO
            <strong>
              {starters.length}
              <small>/11 titulares</small>
            </strong>
          </span>
        </div>
        <div>
          <Trophy />
          <span>
            FORÇA DO XI
            <strong>
              {Math.round(
                starters.reduce((s, p) => s + p.rating, 0) /
                  Math.max(1, starters.length),
              )}
              <small>GER</small>
            </strong>
          </span>
        </div>
        <div>
          <HeartPulse />
          <span>
            PRONTOS PARA JOGAR
            <strong>
              {c.state.squad.filter(available).length}
              <small>atletas</small>
            </strong>
          </span>
        </div>
        <Button variant="outline" onClick={() => navigate("/taticas")}>
          Abrir prancheta
        </Button>
      </div>
      <div className="locker-toolbar">
        <label className="search">
          <Search size={18} />
          <input
            aria-label="Buscar jogador no vestiário"
            placeholder="Encontre seu jogador…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <select
          aria-label="Posição no vestiário"
          value={position}
          onChange={(e) => setPosition(e.target.value)}
        >
          <option value="">Todas as posições</option>
          {["GOL", "ZAG", "LAT", "MEI", "ATA"].map((x) => (
            <option key={x}>{x}</option>
          ))}
        </select>
      </div>
      <div className="locker-tabs" role="tablist" aria-label="Grupo do elenco">
        {[
          ["todos", "Todo o elenco"],
          ["titulares", "Onze inicial"],
          ["reservas", "Banco"],
          ["indisponiveis", "Indisponíveis"],
        ].map(([key, label]) => (
          <button
            key={key}
            role="tab"
            aria-selected={group === key}
            onClick={() => setGroup(key)}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="locker-layout">
        <div className="player-card-grid">
          {players.map((p) => (
            <button
              className={
                "athlete-card " + (player?.id === p.id ? "selected" : "")
              }
              key={p.id}
              aria-pressed={player?.id === p.id}
              aria-label={`${p.name}, ${p.position}, geral ${p.rating}`}
              onClick={() => setSelected(p.id)}
            >
              <div className="athlete-top">
                <span>{p.position}</span>
                <strong>
                  {p.rating}
                  <small>GER</small>
                </strong>
              </div>
              <Shirt color={club.color} number={number(p)} short={club.short} />
              <div className="athlete-name">
                <small>{p.name.split(" ").slice(0, -1).join(" ")}</small>
                <b>{p.name.split(" ").at(-1)}</b>
              </div>
              <div className="athlete-bottom">
                <span>{p.starter ? "TITULAR" : "RESERVA"}</span>
                <span className={!available(p) ? "unavailable" : ""}>
                  {p.injuryDays
                    ? "LESIONADO"
                    : p.suspensionMatches
                      ? "SUSPENSO"
                      : `${p.fitness}% FÔLEGO`}
                </span>
              </div>
              <div className="athlete-fitness">
                <i style={{ width: `${p.fitness}%` }} />
              </div>
            </button>
          ))}
          {players.length === 0 && (
            <p className="empty">Nenhum atleta neste grupo.</p>
          )}
        </div>
        {player && (
          <aside className="athlete-detail">
            <span className="eyebrow">NO VESTIÁRIO</span>
            <Shirt
              color={club.color}
              number={number(player)}
              short={club.short}
            />
            <h2>{player.name}</h2>
            <p>
              {player.position} · {player.age} anos · {club.name}
            </p>
            <div className="attribute-pair">
              <div>
                <strong>{player.rating}</strong>
                <span>GERAL</span>
              </div>
              <div>
                <strong>{player.fitness}%</strong>
                <span>CONDIÇÃO</span>
              </div>
            </div>
            <p className={!available(player) ? "error" : "availability"}>
              {player.injuryDays
                ? `${player.injuryName} · ${player.injuryDays} dias`
                : player.suspensionMatches
                  ? `Suspenso por ${player.suspensionMatches} jogo(s)`
                  : "Disponível para a próxima partida"}
            </p>
            <dl>
              <div>
                <dt>Valor estimado</dt>
                <dd>{money(player.value)}</dd>
              </div>
              <div>
                <dt>Salário por rodada</dt>
                <dd>{money(player.wage)}</dd>
              </div>
              <div>
                <dt>Contrato</dt>
                <dd>Até {c.state.contracts[player.id]?.endSeason ?? "—"}</dd>
              </div>
              <div>
                <dt>Cartões amarelos</dt>
                <dd>{player.yellowCards}/3</dd>
              </div>
            </dl>
            <Button
              disabled={
                busy ||
                (!player.starter &&
                  (!available(player) || starters.length >= 11))
              }
              onClick={() => action("starter", "", "", player.id)}
            >
              {player.starter
                ? "Retirar do onze inicial"
                : "Escalar no onze inicial"}
            </Button>
            {!player.starter && starters.length >= 11 && (
              <small className="muted">
                Retire um titular ou faça uma troca na prancheta.
              </small>
            )}
            <Button
              variant="outline"
              onClick={() => navigate("/contratos?player=" + player.id)}
            >
              Contrato e negociação
            </Button>
            <Button variant="ghost" onClick={() => navigate("/treinamentos")}>
              Desenvolver jogador
            </Button>
          </aside>
        )}
      </div>
    </div>
  );
}
