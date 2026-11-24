import { useState } from "react";
import { seasonRounds } from "./season";
import { aggregate, legLabel, played } from "./ties";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  api,
  money,
  type Career,
  type Fixture,
  type Player,
  type Club,
} from "../../services/api";
import { Button } from "../../components/ui/button";
type Mind = {
  morale: number;
  ambition: number;
  leadership: number;
  relationship: number;
  personality: string;
  promiseDueRound: number;
  promisedMinutes: number;
};
type Staff = {
  id: string;
  name: string;
  role: string;
  quality: number;
  wage: number;
};
type Potential = { min: number; max: number };
type Cup = {
  id: string;
  name: string;
  format: string;
  stage: number;
  champion: string;
  prize: number;
  legs?: number;
  matches: { id: string; stage: number; tie?: string; leg?: number; winner: string; fixture: Fixture }[];
};
type Life = {
  board: {
    confidence: number;
    targetPosition: number;
    wageBudget: number;
    transferBudget: number;
    assessment: string;
    captainId: number;
    dismissed: boolean;
    warnings: number;
  };
  fans: {
    popularity: number;
    support: number;
    mood: string;
    rivalId: string;
    lossStreak: number;
  };
  economy: {
    ticketPrice: number;
    sponsor: string;
    sponsorWeekly: number;
    sponsorTarget: number;
    debt: number;
    debtRounds: number;
    instalment: number;
    attendance: number;
    lastGate: number;
  };
  staff: Staff[];
  candidates: Staff[];
  players: {
    id: number;
    name: string;
    position: string;
    age: number;
    fitness: number;
    injuryDays: number;
    injuryName: string;
    mind: Mind;
    relapseRisk: number;
    medical: { treatment: string; relapses: number };
  }[];
  academy: { player: Player; category: string; potential: Potential }[];
  trials: {
    player: Player;
    category: string;
    signingFee: number;
    potential: Potential;
  }[];
  lastTrialRound: number;
  press: {
    id: string;
    season: number;
    round: number;
    kind: string;
    title: string;
    text: string;
    answer: string;
  }[];
  reports: {
    playerId: number;
    dueRound: number;
    season: number;
    complete: boolean;
    player: {
      name: string;
      position: string;
      rating: number;
      potential: Potential | null;
    } | null;
  }[];
  scoutCandidates: {
    id: number;
    name: string;
    position: string;
    rating: number;
  }[];
  agents: {
    id: number;
    name: string;
    agent: string;
    commission: number;
    wageDemand: number;
  }[];
  competitions: Cup[];
  extraClubs: Club[];
  honours: string[];
  calendar: {
    notice: string;
    breaks: { round: number; label: string; date: string }[];
    trips: {
      round: number;
      date: string;
      destination: string;
      kilometres: number;
    }[];
    postponed: { round: number; date: string; home: string; away: string }[];
  };
};
const tabs = [
  ["board", "Diretoria"],
  ["players", "Vestiário"],
  ["agents", "Empresários"],
  ["medical", "Médico"],
  ["academy", "Base"],
  ["scouts", "Olheiros"],
  ["finance", "Finanças"],
  ["fans", "Torcida"],
  ["press", "Imprensa"],
  ["cups", "Copas"],
  ["calendar", "Agenda"],
  ["staff", "Comissão"],
] as const;
function Meter({ label, value }: { label: string; value: number }) {
  return (
    <div className="life-meter">
      <span>
        {label}
        <b>{value}/100</b>
      </span>
      <progress aria-label={label} value={value} max={100} />
    </div>
  );
}
function Metric({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="life-metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
export default function ClubLifeCenter({
  c,
  initialTab = "board",
  onReplay,
  showTabs = true,
}: {
  c: Career;
  initialTab?: string;
  onReplay?: (f: Fixture) => void;
  // The career shell routes each system to its own page; it hides this internal tab strip.
  showTabs?: boolean;
}) {
  const [tab, setTab] = useState(initialTab),
    [ticket, setTicket] = useState(55),
    [loan, setLoan] = useState(5_000_000),
    [scout, setScout] = useState(0),
    [category, setCategory] = useState("Sub-15"),
    [round, setRound] = useState(c.state.round),
    [notice, setNotice] = useState("");
  const qc = useQueryClient(),
    query = useQuery({
      queryKey: ["life", c.id, c.revision],
      queryFn: () => api<Life>(`/careers/${c.id}/life`),
    });
  const mutation = useMutation({
    mutationFn: (body: object) =>
      api<{ result: { message: string } }>(`/careers/${c.id}/life/actions`, {
        revision: c.revision,
        ...body,
      }),
    onSuccess: async (d) => {
      setNotice(d.result.message);
      await Promise.all([
        qc.invalidateQueries({ queryKey: ["career"] }),
        qc.invalidateQueries({ queryKey: ["life", c.id] }),
        qc.invalidateQueries({ queryKey: ["youth"] }),
        qc.invalidateQueries({ queryKey: ["training"] }),
      ]);
    },
    onError: () => {
      void qc.invalidateQueries({ queryKey: ["career"] });
    },
  });
  const act = (type: string, value = "", playerId = 0, amount = 0) => {
    setNotice("");
    mutation.mutate({ type, value, playerId, amount });
  };
  const life = query.data,
    busy = mutation.isPending || !!c.state.liveMatch;
  if (query.isPending)
    return <p className="empty">Abrindo os bastidores do clube…</p>;
  if (!life)
    return (
      <div role="alert" className="error">
        {query.error?.message}
        <Button onClick={() => void query.refetch()}>Tentar novamente</Button>
      </div>
    );
  const clubs = [...c.state.clubs, ...life.extraClubs],
    club = (id: string) => clubs.find((c) => c.id === id)?.name ?? id;
  const pendingInterview = life.press.find(
    (s) => s.kind === "Coletiva" && !s.answer && s.season === c.state.season,
  )?.id;
  const playerActions = (id: number) => (
    <div className="life-actions">
      {["Apoiar", "Cobrar", "Prometer espaço"].map((t) => (
        <Button
          key={t}
          variant="outline"
          disabled={busy}
          onClick={() => act("talk", t, id)}
        >
          {t}
        </Button>
      ))}
      <Button
        variant="ghost"
        disabled={busy}
        onClick={() => act("captain", "", id)}
      >
        Dar a faixa
      </Button>
    </div>
  );
  return (
    <section className="club-life">
      <header className="life-hero">
        <div>
          <span className="eyebrow">ALÉM DAS QUATRO LINHAS</span>
          <h2>
            {life.board.dismissed
              ? "Um novo capítulo espera por você"
              : "Um clube vivo. Suas decisões."}
          </h2>
          <p>{life.board.assessment}</p>
        </div>
        <div className="life-confidence">
          <strong>{life.board.confidence}</strong>
          <span>confiança da diretoria</span>
        </div>
      </header>
      {showTabs && (
        <nav className="life-tabs" aria-label="Sistemas da carreira">
          {tabs.map(([id, label]) => (
            <button
              key={id}
              className={tab === id ? "active" : ""}
              onClick={() => setTab(id)}
            >
              {label}
            </button>
          ))}
        </nav>
      )}
      {c.state.liveMatch && (
        <p role="status" className="notice">
          Conclua a transmissão para tomar decisões fora do campo.
        </p>
      )}
      {mutation.error && (
        <p role="alert" className="error">
          {mutation.error.message}
        </p>
      )}
      {notice && (
        <p role="status" className="notice">
          {notice}
        </p>
      )}
      {tab === "board" && (
        <>
          <div className="life-metrics">
            <Metric
              label="Meta esportiva"
              value={`Top ${life.board.targetPosition}`}
            />
            <Metric
              label="Orçamento de transferências"
              value={money(life.board.transferBudget)}
            />
            <Metric
              label="Teto salarial por rodada"
              value={money(life.board.wageBudget)}
            />
            <Metric label="Cobranças formais" value={life.board.warnings} />
          </div>
          <article className="life-card">
            <h3>Seu trabalho está em avaliação</h3>
            <p>
              Resultados, posição, caixa e folha salarial alteram a confiança.
              A avaliação contextual, os ultimatos e seu contrato estão no painel
              da carreira de treinador acima.
            </p>
            <Button
              disabled={busy || life.board.dismissed}
              onClick={() => act("funding")}
            >
              Solicitar aporte de R$ 5 milhões
            </Button>
            <p className="muted">
              Exige confiança de 70 e reserva da diretoria. Um aporte por
              temporada.
            </p>
          </article>

        </>
      )}
      {tab === "players" && (
        <>
          <p className="life-intro">
            Conversas afetam moral e relacionamento. Prometer espaço exige 120
            minutos nas próximas três rodadas. O capitão influencia o grupo
            conforme sua liderança.
          </p>
          <div className="life-grid">
            {life.players.map((p) => (
              <article key={p.id} className="life-card player-dossier">
                <span className="life-badge">
                  {p.position} · {p.age} anos · {p.mind.personality}
                </span>
                <h3>
                  {p.name}
                  {life.board.captainId === p.id ? " · C" : ""}
                </h3>
                <Meter label="Moral" value={p.mind.morale} />
                <Meter label="Relacionamento" value={p.mind.relationship} />
                <div className="life-traits">
                  <span>
                    Ambição <b>{p.mind.ambition}</b>
                  </span>
                  <span>
                    Liderança <b>{p.mind.leadership}</b>
                  </span>
                </div>
                {p.mind.promiseDueRound > 0 && (
                  <p className="gold-text">
                    Promessa: {p.mind.promisedMinutes} minutos até a rodada{" "}
                    {p.mind.promiseDueRound}.
                  </p>
                )}
                {playerActions(p.id)}
              </article>
            ))}
          </div>
        </>
      )}
      {tab === "agents" && (
        <>
          <p className="life-intro">
            Ambição influencia a exigência salarial. A comissão empresarial é
            paga uma vez na conclusão da negociação e aparece na revisão dos
            termos e no livro caixa.
          </p>
          <div className="life-grid">
            {life.agents.map((a) => (
              <article className="life-card" key={a.id}>
                <span className="eyebrow">{a.agent}</span>
                <h3>{a.name}</h3>
                <p>
                  Referência para renovação: <b>{money(a.wageDemand)}/rodada</b>
                </p>
                <p>
                  Comissão estimada sem taxa: <b>{money(a.commission)}</b>
                </p>
                <a href="/contratos">Abrir negociação de contrato →</a>
              </article>
            ))}
          </div>
        </>
      )}
      {tab === "medical" && (
        <>
          <p className="life-intro">
            Tratamento intensivo custa R$ 150 mil e pode abreviar a recuperação.
            O conservador custa R$ 50 mil e reduz o risco de recaída. Uma
            escolha por atleta a cada rodada.
          </p>
          <div className="life-grid">
            {life.players
              .filter((p) => p.injuryDays > 0 || p.relapseRisk > 0)
              .map((p) => (
                <article className="life-card" key={p.id}>
                  <span className="life-badge">
                    {p.injuryDays > 0 ? "Em tratamento" : "Retorno ao campo"}
                  </span>
                  <h3>{p.name}</h3>
                  <p>
                    {p.injuryName || "Liberado com acompanhamento"} ·{" "}
                    {p.injuryDays} dias
                  </p>
                  <p>
                    Condição {p.fitness}/100 · risco simulado de recaída{" "}
                    {p.relapseRisk}%
                  </p>
                  <p>
                    Plano: {p.medical.treatment} · recaídas:{" "}
                    {p.medical.relapses}
                  </p>
                  {p.injuryDays > 0 && (
                    <div className="life-actions">
                      {["Padrão", "Conservador", "Intensivo"].map((t) => (
                        <Button
                          key={t}
                          disabled={busy}
                          variant="outline"
                          onClick={() => act("treatment", t, p.id)}
                        >
                          {t}
                        </Button>
                      ))}
                    </div>
                  )}
                </article>
              ))}
          </div>
          {!life.players.some((p) => p.injuryDays > 0 || p.relapseRisk > 0) && (
            <article className="life-card">
              <h3>Elenco sem pendências médicas</h3>
              <p>
                Tratamentos e acompanhamento de retorno aparecerão aqui quando
                houver lesões.
              </p>
            </article>
          )}
        </>
      )}
      {tab === "academy" && (
        <>
          <div className="life-metrics">
            {["Sub-15", "Sub-17", "Sub-20"].map((cat) => (
              <Metric
                key={cat}
                label={cat}
                value={
                  life.academy.filter((p) => p.category === cat).length +
                  " atletas"
                }
              />
            ))}
          </div>
          <article className="life-card">
            <h3>Abra as portas para novos talentos</h3>
            <p>
              Peneira: R$ 180 mil. Quatro candidatos; captação individual de R$
              100 mil. Intervalo de quatro rodadas entre peneiras. Promoção ao
              profissional a partir dos 16 anos.
            </p>
            <div className="life-actions">
              <select
                aria-label="Categoria da peneira"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
              >
                {["Sub-15", "Sub-17", "Sub-20"].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
              <Button disabled={busy} onClick={() => act("trial", category)}>
                Realizar peneira
              </Button>
            </div>
          </article>
          <div className="life-grid">
            {life.trials.map((p) => (
              <article className="life-card" key={p.player.id}>
                <span className="life-badge">Peneira · {p.category}</span>
                <h3>{p.player.name}</h3>
                <p>
                  {p.player.position} · {p.player.age} anos · atual{" "}
                  {p.player.rating}
                </p>
                <p>
                  Potencial estimado: {p.potential.min}–{p.potential.max}
                </p>
                <Button
                  disabled={busy}
                  onClick={() => act("recruit", "", p.player.id)}
                >
                  Captar · {money(p.signingFee)}
                </Button>
              </article>
            ))}
            {life.academy.map((p) => (
              <article className="life-card" key={p.player.id}>
                <span className="life-badge">{p.category}</span>
                <h3>{p.player.name}</h3>
                <p>
                  {p.player.position} · {p.player.age} anos · atual{" "}
                  {p.player.rating}
                </p>
                <p>
                  Potencial estimado: {p.potential.min}–{p.potential.max}
                </p>
              </article>
            ))}
          </div>
          <p className="muted">
            O valor exato do potencial permanece oculto. O coordenador melhora
            as estimativas; a promoção está no painel de captação abaixo.
          </p>
        </>
      )}
      {tab === "scouts" && (
        <>
          <article className="life-card">
            <h3>Conheça o atleta antes de negociar</h3>
            <p>
              Relatório em duas rodadas, por R$ 120 mil. A qualidade do olheiro
              reduz a incerteza sobre o potencial.
            </p>
            <div className="life-actions">
              <select
                aria-label="Atleta para observar"
                value={scout}
                onChange={(e) => setScout(Number(e.target.value))}
              >
                <option value={0}>Escolha um atleta</option>
                {life.scoutCandidates.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name} · {p.position} · {p.rating}
                  </option>
                ))}
              </select>
              <Button
                disabled={busy || !scout}
                onClick={() => act("scout", "", scout)}
              >
                Enviar olheiro
              </Button>
            </div>
          </article>
          <div className="life-grid">
            {life.reports.map((r, i) => (
              <article className="life-card" key={i}>
                <span className="life-badge">
                  {r.complete
                    ? "Relatório disponível"
                    : `Chega na rodada ${r.dueRound}`}
                </span>
                <h3>{r.player?.name ?? "Atleta indisponível"}</h3>
                {r.complete && r.player?.potential ? (
                  <p>
                    Potencial estimado:{" "}
                    <b>
                      {r.player.potential.min}–{r.player.potential.max}
                    </b>
                  </p>
                ) : (
                  <p>A observação ainda está em andamento.</p>
                )}
              </article>
            ))}
          </div>
          <p className="muted">
            A descoberta regional de jovens continua disponível nas Categorias
            de base.
          </p>
        </>
      )}
      {tab === "finance" && (
        <>
          <div className="life-metrics">
            <Metric
              label="Patrocínio por rodada"
              value={money(life.economy.sponsorWeekly)}
            />
            <Metric
              label="Última bilheteria"
              value={money(life.economy.lastGate)}
            />
            <Metric
              label="Ingressos na última rodada"
              value={life.economy.attendance}
            />
            <Metric label="Dívida a pagar" value={money(life.economy.debt)} />
          </div>
          <div className="life-grid">
            <article className="life-card">
              <h3>Bilheteria</h3>
              <p>
                Preço atual: {money(life.economy.ticketPrice)}. A procura
                depende da popularidade e do preço.
              </p>
              <label>
                Novo ingresso
                <input
                  aria-label="Novo preço do ingresso"
                  type="number"
                  min={20}
                  max={150}
                  value={ticket}
                  onChange={(e) => setTicket(Number(e.target.value))}
                />
              </label>
              <Button
                disabled={busy}
                onClick={() => act("ticket", "", 0, ticket)}
              >
                Definir preço
              </Button>
            </article>
            <article className="life-card">
              <h3>Patrocínio {life.economy.sponsor}</h3>
              <p>
                Meta: top {life.economy.sponsorTarget}. Se não for cumprida, há
                multa de uma receita semanal ao encerrar a liga. Uma escolha por
                temporada.
              </p>
              {[
                ["Regional", "R$ 1 mi · top 16"],
                ["Nacional", "R$ 1,4 mi · top 10"],
                ["Ambicioso", "R$ 1,8 mi · top 6"],
              ].map(([id, terms]) => (
                <Button
                  key={id}
                  disabled={busy}
                  variant="outline"
                  onClick={() => act("sponsor", id)}
                >
                  {id} · {terms}
                </Button>
              ))}
            </article>
            <article className="life-card">
              <h3>Crédito para o clube</h3>
              <p>
                Doze parcelas por rodada, custo total de 12%. Dívida atual:{" "}
                {money(life.economy.debt)} · {life.economy.debtRounds} parcelas
                de até {money(life.economy.instalment)}.
              </p>
              <label>
                Valor do crédito
                <input
                  aria-label="Valor do crédito"
                  type="number"
                  min={1_000_000}
                  max={20_000_000}
                  step={1_000_000}
                  value={loan}
                  onChange={(e) => setLoan(Number(e.target.value))}
                />
              </label>
              <Button
                disabled={busy || life.economy.debt > 0}
                onClick={() => act("loan", "", 0, loan)}
              >
                Contratar crédito
              </Button>
            </article>
          </div>
        </>
      )}
      {tab === "fans" && (
        <>
          <div className="life-metrics">
            <Metric
              label="Popularidade"
              value={life.fans.popularity + "/100"}
            />
            <Metric label="Apoio" value={life.fans.support + "/100"} />
            <Metric label="Derrotas seguidas" value={life.fans.lossStreak} />
          </div>
          <article className="life-card">
            <span className="eyebrow">A VOZ DA ARQUIBANCADA</span>
            <h3>{life.fans.mood}</h3>
            <p>
              Rival nesta carreira: <b>{club(life.fans.rivalId)}</b>. Este
              vínculo é gerado na carreira. Confrontos com esse rival têm
              repercussão maior na moral e na torcida.
            </p>
            <p>
              Resultados e entrevistas alteram o apoio. Popularidade e preço
              afetam a receita de bilheteria; a apresentação do estádio continua
              com arquibancadas cheias.
            </p>
          </article>
        </>
      )}
      {tab === "press" && (
        <div className="life-news">
          {life.press.map((story) => (
            <article className="life-card" key={story.id}>
              <span className="eyebrow">
                {story.kind} · {story.season} · R{story.round}
              </span>
              <h3>{story.title}</h3>
              <p>{story.text}</p>
              {story.answer && <blockquote>“{story.answer}”</blockquote>}
              {story.kind === "Coletiva" &&
                story.id === pendingInterview &&
                !story.answer &&
                story.season === c.state.season && (
                  <div className="life-actions">
                    {[
                      "Proteger elenco",
                      "Cobrar reação",
                      "Assumir responsabilidade",
                    ].map((t) => (
                      <Button
                        key={t}
                        disabled={busy}
                        variant="outline"
                        onClick={() => act("interview", t)}
                      >
                        {t}
                      </Button>
                    ))}
                  </div>
                )}
            </article>
          ))}
          {!life.press.length && (
            <article className="life-card">
              <h3>Os holofotes estão se acendendo</h3>
              <p>
                Coletivas e repercussões aparecem após as partidas. Rumores
                acompanham as negociações abertas.
              </p>
            </article>
          )}
        </div>
      )}
      {tab === "cups" && (
        <>
          <p className="life-intro">
            As partidas do seu clube entram na fila de transmissão antes da
            liga. Não há avanço automático dessas partidas. Os formatos abaixo
            são demonstrativos e gerados para esta carreira.
          </p>
          {life.competitions.map((cup) => (
            <article className="life-card cup-dossier" key={cup.id}>
              <div className="life-card-heading">
                <div>
                  <span className="eyebrow">
                    {cup.champion
                      ? "COMPETIÇÃO ENCERRADA"
                      : `FASE ${cup.stage + 1}`}
                  </span>
                  <h3>{cup.name}</h3>
                </div>
                <strong>{money(cup.prize)}</strong>
              </div>
              <p>{cup.format}</p>
              {cup.champion && <h4>Campeão: {club(cup.champion)}</h4>}
              <div className="life-grid">
                {cup.matches.map((m) => (
                  <div
                    key={m.id}
                    className={
                      "life-match " +
                      (m.fixture.home === c.state.clubId ||
                      m.fixture.away === c.state.clubId
                        ? "managed"
                        : "")
                    }
                  >
                    <span>
                      Fase {m.stage + 1}
                      {legLabel(cup, m) && ` · ${legLabel(cup, m)}`} · R
                      {m.fixture.round} · {m.fixture.date}
                    </span>
                    <h4>
                      {club(m.fixture.home)}{" "}
                      <b>
                        {m.fixture.homeGoals ?? "×"}
                        {m.fixture.homeGoals !== null
                          ? `–${m.fixture.awayGoals}`
                          : ""}
                      </b>{" "}
                      {club(m.fixture.away)}
                    </h4>
                    {m.fixture.homePenalties != null && (
                      <p>
                        Pênaltis: {m.fixture.homePenalties}–
                        {m.fixture.awayPenalties}
                      </p>
                    )}
                    {(() => {
                      const total = aggregate(cup, m);
                      return (
                        total && (
                          <p>
                            Agregado: {club(m.fixture.home)} {total.home}–
                            {total.away} {club(m.fixture.away)}
                            {m.winner && ` · classificado: ${club(m.winner)}`}
                          </p>
                        )
                      );
                    })()}
                    {played(m) ? (
                      <Button
                        variant="ghost"
                        onClick={() => onReplay?.(m.fixture)}
                      >
                        Ver replay
                      </Button>
                    ) : (
                      <small>
                        {m.fixture.home === c.state.clubId ||
                        m.fixture.away === c.state.clubId
                          ? "Aguardando sua transmissão"
                          : "Adversários jogam ao concluir a rodada"}
                      </small>
                    )}
                  </div>
                ))}
              </div>
            </article>
          ))}
          {life.honours.map((h, i) => (
            <p key={i}>{h}</p>
          ))}
        </>
      )}
      {tab === "calendar" && (
        <>
          <p className="life-intro">{life.calendar.notice}</p>
          <div className="life-grid">
            <article className="life-card">
              <h3>Pausas de seleções</h3>
              {life.calendar.breaks.map((b) => (
                <p key={b.round}>
                  Após R{b.round} · {b.label} · {b.date}
                </p>
              ))}
            </article>
            <article className="life-card">
              <h3>Próximas viagens</h3>
              {life.calendar.trips.map((t) => (
                <p key={t.round}>
                  R{t.round} · {t.destination} · cerca de {t.kilometres} km
                </p>
              ))}
            </article>
            <article className="life-card">
              <h3>Remarcação logística</h3>
              <p>
                Remarca em dois dias um jogo de liga ainda não preparado.
                Compromissos próximos aumentam o desgaste.
              </p>
              <input
                aria-label="Rodada para remarcar"
                type="number"
                min={c.state.round}
                max={seasonRounds(c.state)}
                value={round}
                onChange={(e) => setRound(Number(e.target.value))}
              />
              <Button
                disabled={busy}
                onClick={() => act("postpone", "", 0, round)}
              >
                Solicitar remarcação
              </Button>
              {life.calendar.postponed.map((f) => (
                <p key={f.round}>
                  R{f.round} · nova data {f.date}
                </p>
              ))}
            </article>
          </div>
        </>
      )}
      {tab === "staff" && (
        <>
          <p className="life-intro">
            Auxiliares ajudam a recuperar a moral. Preparadores melhoram treino
            e condição; analistas ajudam no desempenho; fisioterapeutas reduzem
            recaídas. Olheiros e coordenação da base reduzem a incerteza dos
            relatórios.
          </p>
          <div className="life-grid">
            {life.staff.map((s) => (
              <article className="life-card" key={s.role}>
                <span className="life-badge">{s.role}</span>
                <h3>{s.name}</h3>
                <Meter label="Qualidade" value={s.quality} />
                <p>{money(s.wage)}/rodada</p>
                {life.candidates
                  .filter(
                    (candidate) =>
                      candidate.role === s.role && candidate.id !== s.id,
                  )
                  .map((candidate) => (
                    <Button
                      key={candidate.id}
                      variant="outline"
                      disabled={busy}
                      onClick={() => act("staff", candidate.id)}
                    >
                      {candidate.name} · {candidate.quality} · contratar por{" "}
                      {money(candidate.wage * 4 + s.wage * 2)}
                      <small>Salário {money(candidate.wage)}/rodada</small>
                    </Button>
                  ))}
              </article>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
