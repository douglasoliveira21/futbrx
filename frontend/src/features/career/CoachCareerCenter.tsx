import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, money, type Career } from "../../services/api";
import { Button } from "../../components/ui/button";

type Result = {
  contractId: string;
  key: string;
  clubId: string;
  date: string;
  season: number;
  scored: number;
  against: number;
  points: number;
  youthMinutes: number;
};
type Contract = {
  id: string;
  clubId: string;
  start: string;
  end: string;
  closed: string | null;
  status: string;
  wage: number;
  severanceMonths: number;
  targetPosition: number;
  transferBudget: number;
  endReason: string;
};
type Ultimatum = {
  description: string;
  gamesLeft: number;
  progress: number;
  required: number;
  resolved: boolean;
  success: boolean;
};
type Board = {
  patience: number;
  ambition: number;
  titleDemand: number;
  lossTolerance: number;
  experiencePreference: number;
  youthPreference: number;
  academyValue: number;
  preferredStyle: string;
  confidence: number;
  targetPosition: number;
  ultimatum: Ultimatum | null;
  assessments: {
    date: string;
    confidence: number;
    reason: string;
    factors: Record<string, number>;
  }[];
};
type Offer = {
  id: string;
  clubId: string;
  kind: string;
  status: string;
  created: string;
  expires: string;
  wage: number;
  years: number;
  targetPosition: number;
  transferBudget: number;
  severanceMonths: number;
  buyout: number;
  negotiationRound: number;
  message: string;
  negotiationHistory: string[];
};
type Event = {
  id: string;
  date: string;
  kind: string;
  coachId: string;
  clubId: string;
  title: string;
  text: string;
};
type CoachView = {
  financeBudget: { wageBudget: number; transferBudget: number };
  date: string;
  employed: boolean;
  reputationLabel: string;
  confidenceLabel: string;
  notice: string;
  contract: Contract | null;
  board: Board | null;
  player: {
    id: string;
    name: string;
    birthDate: string;
    nationality: string;
    clubId: string;
    reputation: number;
    formation: string;
    style: string;
    results: Result[];
    titles: string[];
    awards: string[];
    reputationHistory: {
      date: string;
      season: number;
      value: number;
      reason: string;
    }[];
  };
  contracts: Contract[];
  offers: Offer[];
  applications: {
    id: string;
    vacancyId: string;
    status: string;
    reason: string;
    answerDate: string;
  }[];
  vacancies: {
    id: string;
    clubId: string;
    opened: string;
    deadline: string;
    reason: string;
    candidateCount: number;
    compatible: boolean;
    position: number;
    board: Board;
    budget: number;
    maxWage: number;
  }[];
  dismissals: {
    id: string;
    coachName: string;
    clubId: string;
    date: string;
    days: number;
    played: number;
    wins: number;
    draws: number;
    losses: number;
    position: number;
    titles: string[];
    reason: string;
    compensation: number;
    reaction: string;
    acknowledged: boolean;
  }[];
  clubs: {
    id: string;
    name: string;
    short: string;
    color: string;
    strength: number;
    position: number;
    coach: {
      id: string;
      name: string;
      reputation: number;
      formation: string;
      style: string;
      confidence: number;
    } | null;
  }[];
  aiCoaches: {
    id: string;
    name: string;
    clubId: string;
    reputation: number;
    retired: boolean;
    formation: string;
    style: string;
    titles: number;
    age: number;
  }[];
  timeline: Event[];
  marketNews: Event[];
};
type Action = {
  type: string;
  id?: string;
  value?: string;
  days?: number;
  wage?: number;
  years?: number;
  target?: number;
  budget?: number;
  severanceMonths?: number;
};
const date = (s: string) =>
  new Date(s + "T12:00:00").toLocaleDateString("pt-BR");
const open = (o: Offer) =>
  [
    "Interesse informal",
    "Sondagem",
    "Entrevista",
    "Proposta oficial",
    "Contraproposta",
  ].includes(o.status);
const formal = (o: Offer) =>
  ["Proposta oficial", "Contraproposta"].includes(o.status);
function Metric({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="life-metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
function Negotiation({
  offer,
  busy,
  onAct,
}: {
  offer: Offer;
  busy: boolean;
  onAct: (a: Action) => void;
}) {
  const [wage, setWage] = useState(offer.wage),
    [years, setYears] = useState(offer.years),
    [target, setTarget] = useState(offer.targetPosition),
    [budget, setBudget] = useState(offer.transferBudget),
    [severance, setSeverance] = useState(offer.severanceMonths);
  return (
    <form
      className="coach-negotiation"
      onSubmit={(e) => {
        e.preventDefault();
        onAct({
          type: "negotiate",
          id: offer.id,
          wage,
          years,
          target,
          budget,
          severanceMonths: severance,
        });
      }}
    >
      <label>
        Salário semanal (R$)
        <input
          type="number"
          min={5000}
          max={2000000}
          step={1000}
          value={wage}
          onChange={(e) => setWage(+e.target.value)}
          required
        />
      </label>
      <label>
        Duração (anos)
        <input
          type="number"
          min={1}
          max={5}
          value={years}
          onChange={(e) => setYears(+e.target.value)}
          required
        />
      </label>
      <label>
        Meta de classificação
        <input
          type="number"
          min={1}
          max={20}
          value={target}
          onChange={(e) => setTarget(+e.target.value)}
          required
        />
      </label>
      <label>
        Orçamento de transferências (R$)
        <input
          type="number"
          min={0}
          step={100000}
          value={budget}
          onChange={(e) => setBudget(+e.target.value)}
          required
        />
      </label>
      <label>
        Rescisão (meses de salário)
        <input
          type="number"
          min={0}
          max={12}
          value={severance}
          onChange={(e) => setSeverance(+e.target.value)}
          required
        />
      </label>
      <Button disabled={busy || offer.negotiationRound >= 3}>
        Enviar negociação · {offer.negotiationRound}/3
      </Button>
    </form>
  );
}
export default function CoachCareerCenter({
  c,
  initialTab = "board",
}: {
  c: Career;
  initialTab?: string;
}) {
  const [tab, setTab] = useState(initialTab),
    [notice, setNotice] = useState(""),
    [resign, setResign] = useState(false),
    [name, setName] = useState("");
  const qc = useQueryClient();
  const crests = useQuery<{
    clubs: { clubId: string; status: string; path: string | null }[];
  }>({
    queryKey: ["crest-credits"],
    queryFn: async () => {
      const r = await fetch("/data/crest-credits.json");
      if (!r.ok) throw new Error("Créditos indisponíveis");
      return r.json();
    },
    staleTime: Infinity,
  });
  const query = useQuery({
    queryKey: ["coach", c.id, c.revision],
    queryFn: () => api<CoachView>(`/careers/${c.id}/coach`),
  });
  const mutation = useMutation({
    mutationFn: (body: Action) =>
      api<{ result: { message: string } }>(`/careers/${c.id}/coach/actions`, {
        revision: c.revision,
        ...body,
      }),
    onSuccess: async (r) => {
      setNotice(r.result.message);
      setResign(false);
      await Promise.all([
        qc.invalidateQueries({ queryKey: ["career"] }),
        qc.invalidateQueries({ queryKey: ["coach", c.id] }),
        qc.invalidateQueries({ queryKey: ["life", c.id] }),
      ]);
    },
    onError: () => {
      void qc.invalidateQueries({ queryKey: ["career"] });
    },
  });
  const act = (a: Action) => {
      setNotice("");
      mutation.mutate(a);
    },
    busy = mutation.isPending || !!c.state.liveMatch;
  if (query.isPending)
    return <p className="empty">Abrindo sua carreira de treinador…</p>;
  const v = query.data;
  if (!v)
    return (
      <p role="alert" className="notice error">
        {query.error?.message ?? "Não foi possível carregar a carreira."}
      </p>
    );
  const club = (id: string) => v.clubs.find((x) => x.id === id)?.name ?? id;
  const clubBadge = (id: string) => {
    const entry = crests.data?.clubs.find(
        (x) => x.clubId === id && x.status === "included",
      ),
      team = v.clubs.find((x) => x.id === id);
    return (
      <span className="coach-club-badge">
        {entry?.path ? (
          <img
            src={entry.path}
            alt={`Escudo de ${club(id)}`}
            title="Créditos em Dados e licenças"
          />
        ) : (
          <b
            style={{ color: team?.color }}
            aria-label={`Identificação de ${club(id)}`}
          >
            {team?.short ?? "BM"}
          </b>
        )}
        <span>{club(id)}</span>
      </span>
    );
  };
  const p = v.player,
    unread = v.dismissals.find((d) => !d.acknowledged),
    current = v.contract,
    results = p.results;
  const stat = (rows: Result[]) => {
    const wins = rows.filter((r) => r.scored > r.against).length,
      draws = rows.filter((r) => r.scored === r.against).length;
    return {
      played: rows.length,
      wins,
      draws,
      losses: rows.length - wins - draws,
      rate: rows.length
        ? (((wins * 3 + draws) / (rows.length * 3)) * 100).toFixed(1)
        : "0,0",
    };
  };
  const totals = stat(results);
  let streak = 0,
    best = 0;
  for (const r of results) {
    streak = r.scored >= r.against ? streak + 1 : 0;
    best = Math.max(best, streak);
  }
  const greatest = [...results].sort(
      (a, b) => b.scored - b.against - (a.scored - a.against),
    )[0],
    worst = [...results].sort(
      (a, b) => a.scored - a.against - (b.scored - b.against),
    )[0];
  const repPoints = p.reputationHistory.filter(
    (_, i, a) =>
      i === 0 ||
      i === a.length - 1 ||
      i % Math.max(1, Math.floor(a.length / 40)) === 0,
  );
  return (
    <section className="club-life coach-career">
      <div className="life-hero">
        <div>
          <span className="eyebrow">SUA HISTÓRIA À BEIRA DO CAMPO</span>
          <h2>
            {v.employed ? p.name : "Mercado de treinadores — em busca de clube"}
          </h2>
          <p>
            {v.employed
              ? club(p.clubId)
              : "Treinador livre · Você decide quando aceitar uma oportunidade."}{" "}
            · {date(v.date)}
          </p>
        </div>
        <div className="life-confidence">
          <strong>{Math.round(p.reputation)}</strong>
          <span>{v.reputationLabel}</span>
        </div>
      </div>
      {unread ? (
        <article
          className="life-card coach-dismissal"
          role="alertdialog"
          aria-labelledby="dismissal-title"
        >
          <span className="eyebrow">COMUNICADO OFICIAL</span>
          <h2 id="dismissal-title">Rescisão de contrato</h2>
          <h3>
            {club(unread.clubId)} · {unread.coachName}
          </h3>
          <p>
            {date(unread.date)} · {unread.days} dias no cargo
          </p>
          <div className="life-metrics">
            <Metric label="Partidas" value={unread.played} />
            <Metric
              label="V / E / D"
              value={`${unread.wins} / ${unread.draws} / ${unread.losses}`}
            />
            <Metric
              label="Aproveitamento"
              value={
                unread.played
                  ? `${(((unread.wins * 3 + unread.draws) / (unread.played * 3)) * 100).toFixed(1)}%`
                  : "0%"
              }
            />
            <Metric label="Posição" value={`${unread.position}º`} />
            <Metric
              label="Rescisão recebida"
              value={money(unread.compensation)}
            />
          </div>
          <p>{unread.reason}</p>
          <p>{unread.reaction}</p>
          <p>
            Títulos: {unread.titles.join(" · ") || "Nenhum título neste clube."}
          </p>
          <Button
            disabled={busy}
            onClick={() => {
              setTab("market");
              act({ type: "acknowledge", id: unread.id });
            }}
          >
            Confirmar comunicado e seguir como treinador livre
          </Button>
        </article>
      ) : (
        <>
          {!v.employed && (
            <div className="life-actions coach-free">
              <Button
                disabled={busy}
                onClick={() => act({ type: "advance", days: 1 })}
              >
                Avançar 1 dia
              </Button>
              <Button
                disabled={busy}
                onClick={() => act({ type: "advance", days: 7 })}
              >
                Avançar 7 dias
              </Button>
              <span>Clubes e competições continuam sob comando da IA.</span>
            </div>
          )}
          <nav className="life-tabs" aria-label="Carreira do treinador">
            {[
              ["board", "Diretoria"],
              ["market", "Mercado de técnicos"],
              ["offers", "Propostas recebidas"],
              ["career", "Minha carreira"],
              ["world", "Técnicos da IA"],
              ["news", "Notícias"],
            ].map(([id, label]) => (
              <button
                key={id}
                className={tab === id ? "active" : ""}
                onClick={() => setTab(id)}
              >
                {label}
                {id === "offers" && v.offers.some(open)
                  ? ` · ${v.offers.filter(open).length}`
                  : ""}
              </button>
            ))}
          </nav>
          {tab === "board" &&
            (v.board && current ? (
              <>
                <div className="life-metrics">
                  <Metric label="Confiança" value={`${v.board.confidence}%`} />
                  <Metric label="Situação" value={v.confidenceLabel} />
                  <Metric
                    label="Meta"
                    value={`Top ${current.targetPosition}`}
                  />
                  <Metric label="Salário semanal" value={money(current.wage)} />
                  <Metric label="Contrato até" value={date(current.end)} />
                  <Metric
                    label="Teto salarial do elenco"
                    value={money(v.financeBudget.wageBudget)}
                  />
                  <Metric
                    label="Transferências disponíveis"
                    value={money(v.financeBudget.transferBudget)}
                  />
                </div>
                <p>
                  <Button
                    disabled={busy}
                    variant="outline"
                    onClick={() => act({ type: "funding" })}
                  >
                    Solicitar aporte de R$ 5 milhões
                  </Button>
                </p>
                <p className="muted">
                  O aporte exige confiança mínima de 70, reserva da diretoria e
                  só pode ser solicitado uma vez por temporada.
                </p>
                {v.board.ultimatum && (
                  <article className="life-card coach-ultimatum">
                    <span className="eyebrow">
                      {v.board.ultimatum.resolved
                        ? v.board.ultimatum.success
                          ? "ULTIMATO CUMPRIDO"
                          : "ULTIMATO ENCERRADO"
                        : "ULTIMATO DA DIRETORIA"}
                    </span>
                    <h3>{v.board.ultimatum.description}</h3>
                    <p>
                      Progresso: {v.board.ultimatum.progress}/
                      {v.board.ultimatum.required} · Restam{" "}
                      {v.board.ultimatum.gamesLeft} jogos.
                    </p>
                  </article>
                )}
                <div className="life-grid">
                  <article className="life-card">
                    <h3>O projeto da diretoria</h3>
                    <p>
                      Paciência {v.board.patience}/100 · Ambição{" "}
                      {v.board.ambition}/100 · Exigência por títulos{" "}
                      {v.board.titleDemand}/100
                    </p>
                    <p>
                      Tolerância a derrotas {v.board.lossTolerance}/100 ·
                      Valorização da base {v.board.academyValue}/100
                    </p>
                    <p>
                      Preferência: futebol{" "}
                      {v.board.preferredStyle.toLowerCase()}. Experiência{" "}
                      {v.board.experiencePreference}/100; treinadores jovens{" "}
                      {v.board.youthPreference}/100.
                    </p>
                    <p>
                      Orçamento negociado: {money(current.transferBudget)}.
                      Rescisão: {current.severanceMonths} meses de salário,
                      limitada ao restante do vínculo.
                    </p>
                  </article>
                  <article className="life-card">
                    <h3>Última avaliação</h3>
                    {v.board.assessments.at(-1) ? (
                      <>
                        <p>{v.board.assessments.at(-1)!.reason}</p>
                        {Object.entries(
                          v.board.assessments.at(-1)!.factors,
                        ).map(([label, value]) => (
                          <div className="coach-factor" key={label}>
                            <span>{label}</span>
                            <b>
                              {value >= 0 ? "+" : ""}
                              {value.toFixed(2)}
                            </b>
                          </div>
                        ))}
                      </>
                    ) : (
                      <p>
                        A primeira avaliação detalhada será calculada após sua
                        próxima partida.
                      </p>
                    )}
                  </article>
                </div>
                <article className="life-card">
                  <h3>Seu compromisso com o clube</h3>
                  <p>
                    Uma mudança exige vaga aberta, seleção, proposta oficial e
                    pagamento de eventual multa pelo clube contratante.
                  </p>
                  {!resign ? (
                    <Button
                      variant="outline"
                      disabled={busy}
                      onClick={() => setResign(true)}
                    >
                      Pedir demissão…
                    </Button>
                  ) : (
                    <>
                      <p>
                        Você perderá o controle do clube e seguirá como
                        treinador livre, sem indenização. Seu histórico será
                        preservado.
                      </p>
                      <div className="life-actions">
                        <Button
                          disabled={busy}
                          onClick={() =>
                            act({ type: "resign", id: current.id })
                          }
                        >
                          Confirmar pedido de demissão
                        </Button>
                        <Button
                          variant="outline"
                          onClick={() => setResign(false)}
                        >
                          Continuar no clube
                        </Button>
                      </div>
                    </>
                  )}
                </article>
              </>
            ) : (
              <p className="empty">
                Você está livre no mercado. Consulte vagas e propostas para
                retomar o trabalho.
              </p>
            ))}
          {tab === "market" && (
            <>
              <p className="life-intro">
                As diretorias comparam reputação, experiência, títulos,
                resultados, estilo e trabalho com jovens. Candidatar-se não
                garante entrevista ou contratação.
              </p>
              <div className="life-grid">
                {v.vacancies.length ? (
                  v.vacancies.map((x) => {
                    const app = v.applications.find(
                      (a) => a.vacancyId === x.id,
                    );
                    return (
                      <article key={x.id} className="life-card">
                        <span className="eyebrow">
                          VAGA ABERTA · {x.candidateCount} CANDIDATOS
                        </span>
                        <h3>{clubBadge(x.clubId)}</h3>
                        <p>
                          {x.position}º na liga · Meta Top{" "}
                          {x.board.targetPosition} · Paciência{" "}
                          {x.board.patience}/100
                        </p>
                        <p>
                          Seleção até {date(x.deadline)} · {x.reason}
                        </p>
                        <p>
                          Orçamento {money(x.budget)} · Teto do treinador{" "}
                          {money(x.maxWage)}/semana
                        </p>
                        {!x.compatible && (
                          <p className="muted">
                            Seu perfil está fora dos critérios desta seleção
                            (reputação, orçamento ou saída recente do clube).
                          </p>
                        )}
                        {app ? (
                          <p role="status">
                            {app.status} ·{" "}
                            {app.reason ||
                              `Resposta prevista em ${date(app.answerDate)}`}
                          </p>
                        ) : (
                          <Button
                            disabled={busy || x.clubId === p.clubId}
                            onClick={() => act({ type: "apply", id: x.id })}
                          >
                            Candidatar-se
                          </Button>
                        )}
                      </article>
                    );
                  })
                ) : (
                  <p className="empty">
                    Nenhuma vaga aberta neste momento. O mercado muda conforme
                    os resultados e os contratos.
                  </p>
                )}
              </div>
              <h3>Treinadores pressionados</h3>
              <div className="life-grid">
                {v.clubs
                  .filter((x) => x.coach && x.coach.confidence < 45)
                  .map((x) => (
                    <article className="life-card" key={x.id}>
                      <h3>{x.name}</h3>
                      <p>
                        {x.coach!.name} · Confiança {x.coach!.confidence}%
                      </p>
                      <p>
                        O cargo ainda está ocupado. Uma sondagem não transfere o
                        controle do clube.
                      </p>
                    </article>
                  ))}
              </div>
            </>
          )}
          {tab === "offers" && (
            <>
              <p className="life-intro">
                O prazo usa a data do jogo. Você pode aguardar até a validade
                indicada, negociar ou recusar. Apenas uma proposta oficial
                válida permite assinar.
              </p>
              <div className="life-grid">
                {v.offers.length ? (
                  v.offers.map((o) => (
                    <article className="life-card coach-offer" key={o.id}>
                      <span className="eyebrow">
                        {o.kind} · {o.status}
                      </span>
                      <h3>{clubBadge(o.clubId)}</h3>
                      <p>Validade: {date(o.expires)}</p>
                      <p>{o.message}</p>
                      <div className="life-metrics">
                        <Metric
                          label="Salário / semana"
                          value={money(o.wage)}
                        />
                        <Metric label="Duração" value={`${o.years} anos`} />
                        <Metric
                          label="Meta"
                          value={`Top ${o.targetPosition}`}
                        />
                        <Metric
                          label="Transferências"
                          value={money(o.transferBudget)}
                        />
                        <Metric
                          label="Multa paga pelo novo clube"
                          value={money(o.buyout)}
                        />
                        <Metric
                          label="Cláusula de rescisão"
                          value={`${o.severanceMonths} meses`}
                        />
                      </div>
                      {o.status === "Entrevista" && (
                        <div className="life-actions">
                          {["Base", "Resultados", "Equilíbrio"].map(
                            (project) => (
                              <Button
                                disabled={busy}
                                key={project}
                                onClick={() =>
                                  act({
                                    type: "interview",
                                    id: o.id,
                                    value: project,
                                  })
                                }
                              >
                                Apresentar projeto: {project}
                              </Button>
                            ),
                          )}
                        </div>
                      )}
                      {formal(o) && (
                        <>
                          <details>
                            <summary>Negociar condições</summary>
                            <Negotiation
                              key={`${o.id}-${o.negotiationRound}`}
                              offer={o}
                              busy={busy}
                              onAct={act}
                            />
                          </details>
                          <Button
                            disabled={busy}
                            onClick={() => act({ type: "accept", id: o.id })}
                          >
                            Aceitar termos e assinar{" "}
                            {o.kind === "Contratação"
                              ? `com ${club(o.clubId)}`
                              : "renovação"}
                          </Button>
                        </>
                      )}
                      {open(o) && (
                        <Button
                          variant="outline"
                          disabled={busy}
                          onClick={() => act({ type: "decline", id: o.id })}
                        >
                          Recusar abordagem
                        </Button>
                      )}
                      {o.negotiationHistory.length > 0 && (
                        <details>
                          <summary>Histórico da negociação</summary>
                          {o.negotiationHistory.map((s, i) => (
                            <p key={i}>{s}</p>
                          ))}
                        </details>
                      )}
                    </article>
                  ))
                ) : (
                  <p className="empty">
                    Nenhuma proposta recebida. O interesse depende do desempenho
                    e de vagas compatíveis.
                  </p>
                )}
              </div>
            </>
          )}
          {tab === "career" && (
            <>
              <div className="life-metrics">
                <Metric label="Treinador" value={p.name} />
                <Metric
                  label="Idade / nacionalidade"
                  value={`${new Date(v.date).getFullYear() - new Date(p.birthDate).getFullYear()} · ${p.nationality}`}
                />
                <Metric
                  label="Situação"
                  value={v.employed ? club(p.clubId) : "Treinador livre"}
                />
                <Metric
                  label="Temporadas"
                  value={
                    Math.max(1,Number(v.date.slice(0,4))-Math.min(...v.contracts.map(x=>Number(x.start.slice(0,4))))+1)
                  }
                />
                <Metric
                  label="Clubes treinados"
                  value={new Set(v.contracts.map((x) => x.clubId)).size}
                />
                <Metric label="Aproveitamento" value={`${totals.rate}%`} />
                <Metric
                  label="V / E / D"
                  value={`${totals.wins} / ${totals.draws} / ${totals.losses}`}
                />
                <Metric
                  label="Demissões / pedidos de saída"
                  value={`${v.contracts.filter((x) => x.status === "Demissão").length} / ${v.contracts.filter((x) => x.status === "Pedido de demissão").length}`}
                />
                <Metric label="Maior invencibilidade" value={`${best} jogos`} />
                <Metric
                  label="Maior vitória"
                  value={
                    greatest ? `${greatest.scored} × ${greatest.against}` : "—"
                  }
                />
                <Metric
                  label="Pior resultado"
                  value={worst ? `${worst.scored} × ${worst.against}` : "—"}
                />
              </div>
              {!results.length && (
                <form
                  className="life-card"
                  onSubmit={(e) => {
                    e.preventDefault();
                    act({ type: "profile", value: name });
                  }}
                >
                  <label>
                    Nome do treinador
                    <input
                      required
                      minLength={2}
                      maxLength={60}
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                    />
                  </label>
                  <Button disabled={busy}>Definir nome</Button>
                </form>
              )}
              <article className="life-card">
                <h3>Evolução da reputação · {v.reputationLabel}</h3>
                <svg
                  className="coach-reputation"
                  viewBox="0 0 600 120"
                  role="img"
                  aria-label="Evolução da reputação de zero a cem"
                >
                  <line x1="0" y1="110" x2="600" y2="110" stroke="#ffffff30" />
                  <polyline
                    fill="none"
                    stroke="#d2fa69"
                    strokeWidth="3"
                    points={repPoints
                      .map(
                        (x, i) =>
                          `${(i * 600) / Math.max(1, repPoints.length - 1)},${110 - x.value}`,
                      )
                      .join(" ")}
                  />
                </svg>
                <p>
                  Primeiro registro:{" "}
                  {repPoints[0]
                    ? `${date(repPoints[0].date)} · ${repPoints[0].value.toFixed(1)}`
                    : "—"}{" "}
                  · Atual: {p.reputation.toFixed(1)}/100
                </p>
                <p>{p.titles.join(" · ") || "Ainda sem títulos."}</p>
                <p>
                  {p.awards.join(" · ") || "Ainda sem prêmios individuais."}
                </p>
              </article>
              <h3>Histórico de contratos</h3>
              <div className="life-grid">
                {[...v.contracts].reverse().map((x) => {
                  const rows = results.filter((r) => r.contractId === x.id);
                  const s = stat(rows);
                  return (
                    <article className="life-card" key={x.id}>
                      <span className="eyebrow">{x.status}</span>
                      <h3>{club(x.clubId)}</h3>
                      <p>
                        {date(x.start)} → {date(x.closed ?? x.end)}
                      </p>
                      <p>
                        {money(x.wage)}/semana · Meta Top {x.targetPosition}
                      </p>
                      <p>
                        {s.played} jogos · {s.wins}V {s.draws}E {s.losses}D ·{" "}
                        {s.rate}%
                      </p>
                      <p>{x.endReason}</p>
                    </article>
                  );
                })}
              </div>
              <h3>Linha do tempo</h3>
              <div className="coach-timeline">
                {v.timeline.map((e) => (
                  <article key={e.id}>
                    <small>
                      {date(e.date)} · {e.kind}
                    </small>
                    <h3>{e.title}</h3>
                    <p>{e.text}</p>
                  </article>
                ))}
              </div>
            </>
          )}
          {tab === "world" && (
            <>
              <p className="life-intro">
                Cada profissional mantém vínculos, reputação e estilo próprios.
                Vagas são disputadas; técnicos podem trocar de clube, ficar
                livres ou se aposentar.
              </p>
              <div className="life-grid">
                {v.aiCoaches.map((x) => (
                  <article className="life-card" key={x.id}>
                    <span className="eyebrow">
                      {x.retired
                        ? "APOSENTADO"
                        : x.clubId
                          ? club(x.clubId)
                          : "TREINADOR LIVRE"}
                    </span>
                    <h3>{x.name}</h3>
                    <p>
                      {x.age} anos · Reputação {Math.round(x.reputation)} ·{" "}
                      {x.titles} títulos
                    </p>
                    <p>
                      {x.formation} · {x.style}
                    </p>
                  </article>
                ))}
              </div>
            </>
          )}
          {tab === "news" && (
            <div className="coach-timeline">
              {v.marketNews.map((e) => (
                <article key={e.id}>
                  <small>
                    {date(e.date)} · {e.kind}
                  </small>
                  <h3>{e.title}</h3>
                  <p>{e.text}</p>
                </article>
              ))}
            </div>
          )}
        </>
      )}
      {(notice || mutation.error) && (
        <p role="status" className={mutation.error ? "notice error" : "notice"}>
          {mutation.error?.message || notice}
        </p>
      )}
      <p className="muted coach-disclaimer">{v.notice}</p>
    </section>
  );
}
