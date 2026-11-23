import React from "react";
import { useNavigate } from "react-router-dom";
import {
  ArrowRight,
  BriefcaseBusiness,
  Building2,
  ChevronRight,
  Dumbbell,
  HeartPulse,
  Inbox,
  Megaphone,
  Play,
  Scale,
  ShieldCheck,
  Trophy,
  Users,
  Waypoints,
} from "lucide-react";
import { Button } from "../../components/ui/button";
import { Crest } from "../../components/Crest";
import { money, type Career, type Fixture } from "../../services/api";
import {
  buildInbox,
  clubOf,
  nextFixture,
  recentForm,
  tableAround,
  type InboxSender,
} from "./inbox";
import "./coachHub.css";

const senderIcon: Record<InboxSender, React.ComponentType<{ size?: number }>> = {
  president: Building2,
  director: BriefcaseBusiness,
  assistant: Users,
  medical: HeartPulse,
  fitness: Dumbbell,
  legal: Scale,
  press: Megaphone,
};

const matchDate = (f: Fixture) =>
  f.date
    ? new Date(f.date + "T12:00:00Z").toLocaleDateString("pt-BR", {
        weekday: "long",
        day: "2-digit",
        month: "long",
        timeZone: "UTC",
      })
    : "Data a definir";

function FormChips({ c, clubId }: { c: Career; clubId: string }) {
  const form = recentForm(c, clubId);
  if (!form.length) return <span className="hub-form muted">Estreia na temporada</span>;
  return (
    <span className="hub-form" aria-label="Últimos resultados">
      {form.map(({ fixture, result }) => (
        <i key={fixture.round + fixture.home} className={"form-" + result} title={result}>
          {result}
        </i>
      ))}
    </span>
  );
}

// Home screen: one decision at a time. The next match and a single "Continuar" button lead;
// the inbox explains what changed; the side cards keep the club's pulse at a glance.
export default function CoachHub({
  c,
  onPlay,
  onReplay,
  busy,
}: {
  c: Career;
  onPlay: () => void;
  onReplay: (f: Fixture) => void;
  busy: boolean;
}) {
  const navigate = useNavigate();
  const g = c.state;
  const club = clubOf(c, g.clubId)!;
  const next = nextFixture(c);
  const home = next ? clubOf(c, next.home) : undefined;
  const away = next ? clubOf(c, next.away) : undefined;
  const opponent = next ? (next.home === g.clubId ? away : home) : undefined;
  const inbox = buildInbox(c, money);
  const urgent = inbox.filter((m) => m.tone === "urgent").length;
  const position = (id?: string) => {
    const i = c.table.findIndex((r) => r.club.id === id);
    return i < 0 ? null : { rank: i + 1, points: c.table[i].points };
  };
  const ownRow = c.table.find((r) => r.club.id === g.clubId);
  const available = g.squad.filter(
    (p) => p.injuryDays === 0 && p.suspensionMatches === 0,
  ).length;
  const fitness = g.squad.length
    ? Math.round(g.squad.reduce((a, p) => a + p.fitness, 0) / g.squad.length)
    : 0;
  const confidence = g.board?.confidence;
  const lastResults = recentForm(c, g.clubId, 3).reverse();

  return (
    <div
      className="hub"
      style={
        {
          "--home": home?.color ?? club.color,
          "--away": away?.color ?? club.color,
        } as React.CSSProperties
      }
    >
      <section className="hub-match" aria-label="Próximo jogo">
        <div className="hub-match-top">
          <span className="hub-eyebrow">
            {next
              ? `${next.competitionName || `Brasileirão Série ${g.division ?? "A"}`} · Rodada ${next.round}`
              : `Temporada ${g.season}`}
          </span>
          {next && <span className="hub-date">{matchDate(next)}</span>}
        </div>
        {next && home && away ? (
          <div className="hub-versus">
            {[home, away].map((team, i) => {
              const p = position(team.id);
              return (
                <div
                  key={team.id}
                  className={"hub-team" + (team.id === g.clubId ? " is-own" : "")}
                >
                  <Crest club={team} size="xl" />
                  <strong>{team.name}</strong>
                  <small>
                    {i === 0 ? "Mandante" : "Visitante"}
                    {p && ` · ${p.rank}º · ${p.points} pts`}
                  </small>
                  <FormChips c={c} clubId={team.id} />
                </div>
              );
            })}
            <div className="hub-kickoff">
              <span>VS</span>
              <small>{next.kickoff ? next.kickoff.slice(11, 16) : "Dia de jogo"}</small>
            </div>
          </div>
        ) : (
          <div className="hub-season-end">
            <Trophy size={42} />
            <strong>Temporada encerrada</strong>
            <p>Confira o campeão, renove contratos e prepare o próximo ano.</p>
          </div>
        )}
        <div className="hub-actions">
          <button
            type="button"
            className="hub-continue"
            disabled={busy}
            onClick={() => (next ? onPlay() : navigate("/competicoes"))}
          >
            <span>
              <b>{busy ? "Abrindo o estádio…" : "Continuar"}</b>
              <small>
                {next && opponent
                  ? `Jogar contra ${opponent.name}`
                  : "Ver classificação final"}
              </small>
            </span>
            <Play size={22} fill="currentColor" />
          </button>
          {next && (
            <Button variant="outline" onClick={() => navigate("/taticas")}>
              <Waypoints size={16} /> Preparar time
            </Button>
          )}
          {urgent > 0 && (
            <a className="hub-pending" href="#caixa-de-entrada">
              {urgent === 1
                ? "1 assunto aguarda sua decisão"
                : `${urgent} assuntos aguardam sua decisão`}
            </a>
          )}
        </div>
      </section>

      <div className="hub-body">
        <section className="hub-inbox" id="caixa-de-entrada" aria-label="Caixa de entrada">
          <header>
            <h2>
              <Inbox size={18} /> Caixa de entrada
            </h2>
            <span className="hub-count">{inbox.length}</span>
          </header>
          {inbox.length ? (
            <ul>
              {inbox.map((m) => {
                const Icon = senderIcon[m.sender];
                return (
                  <li key={m.id} className={"hub-message tone-" + m.tone}>
                    <span className={"hub-avatar sender-" + m.sender}>
                      <Icon size={18} />
                    </span>
                    <div>
                      <span className="hub-from">
                        {m.from} <small>{m.role}</small>
                      </span>
                      <h3>{m.title}</h3>
                      {m.body && <p>{m.body}</p>}
                    </div>
                    {m.action && (
                      <button
                        type="button"
                        className="hub-message-action"
                        onClick={() => navigate(m.action!.path)}
                      >
                        {m.action.label}
                        <ArrowRight size={14} />
                      </button>
                    )}
                  </li>
                );
              })}
            </ul>
          ) : (
            <p className="hub-empty">Nenhuma novidade. Siga em frente.</p>
          )}
        </section>

        <aside className="hub-side">
          <section className="hub-card hub-club">
            <header>
              <Crest club={club} />
              <div>
                <strong>{club.name}</strong>
                <small>
                  {ownRow ? `${c.table.indexOf(ownRow) + 1}º lugar · ${ownRow.points} pts` : `Temporada ${g.season}`}
                </small>
              </div>
            </header>
            <dl>
              {confidence !== undefined && (
                <div>
                  <dt>
                    <ShieldCheck size={14} /> Diretoria
                  </dt>
                  <dd>
                    <meter min={0} max={100} low={45} high={75} optimum={90} value={confidence} />
                    <b>{confidence}%</b>
                  </dd>
                </div>
              )}
              <div>
                <dt>
                  <HeartPulse size={14} /> Condição
                </dt>
                <dd>
                  <meter min={0} max={100} low={70} high={85} optimum={100} value={fitness} />
                  <b>{fitness}%</b>
                </dd>
              </div>
              <div>
                <dt>
                  <Users size={14} /> Disponíveis
                </dt>
                <dd>
                  <b>
                    {available}/{g.squad.length}
                  </b>
                </dd>
              </div>
              <div>
                <dt>
                  <BriefcaseBusiness size={14} /> Caixa
                </dt>
                <dd>
                  <b>{money(g.balance)}</b>
                </dd>
              </div>
            </dl>
          </section>

          <section className="hub-card">
            <header>
              <h2>Classificação</h2>
              <button type="button" className="hub-link" onClick={() => navigate("/competicoes")}>
                Tabela <ChevronRight size={14} />
              </button>
            </header>
            <ol className="hub-table">
              {tableAround(c).map(({ row, position: rank }, i, rows) => (
                <li
                  key={row.club.id}
                  className={
                    (row.club.id === g.clubId ? "is-own" : "") +
                    (i > 0 && rank - rows[i - 1].position > 1 ? " gap" : "")
                  }
                >
                  <span>{rank}</span>
                  <Crest club={row.club} />
                  <b>{row.club.name}</b>
                  <small>{row.played} j</small>
                  <strong>{row.points}</strong>
                </li>
              ))}
            </ol>
          </section>

          <section className="hub-card">
            <header>
              <h2>Últimos jogos</h2>
              <button type="button" className="hub-link" onClick={() => navigate("/calendario")}>
                Calendário <ChevronRight size={14} />
              </button>
            </header>
            {lastResults.length ? (
              <ul className="hub-results">
                {lastResults.map(({ fixture: f, result }) => (
                  <li key={f.round + f.home}>
                    <i className={"form-" + result}>{result}</i>
                    <span>
                      {clubOf(c, f.home)?.short} {f.homeGoals} × {f.awayGoals}{" "}
                      {clubOf(c, f.away)?.short}
                    </span>
                    <button
                      type="button"
                      className="hub-link"
                      onClick={() => onReplay(f)}
                      aria-label="Rever partida"
                    >
                      <Play size={13} /> Rever
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="hub-empty">A bola ainda não rolou nesta temporada.</p>
            )}
          </section>
        </aside>
      </div>
    </div>
  );
}
