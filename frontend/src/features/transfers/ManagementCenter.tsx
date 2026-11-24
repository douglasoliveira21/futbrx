import { useEffect, useMemo, useState } from "react";
import { seasonRounds } from "../career/season";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import {
  ArrowLeftRight,
  Clock,
  FilePenLine,
  Search,
  Wallet,
  Check,
  X,
  UserRound,
  CalendarDays,
} from "lucide-react";
import {
  api,
  moneyExact as money,
  type Career,
  type Player,
  type PlayerQuote,
  type TransferOffer,
  type MarketCandidate,
} from "../../services/api";
import { Button } from "../../components/ui/button";
import { DialogShell } from "../../components/ui/dialog";
import { Shirt } from "../sports/SquadRoom";

type MutationResult = {
  revision: number;
  result: { message?: string; id?: string; status?: string };
};
type MarketPage = {
  total: number;
  page: number;
  pageSize: number;
  players: MarketCandidate[];
};
const statusNames: Record<string, string> = {
  pending: "Aguardando resposta",
  agreed: "Termos aceitos",
  countered: "Contraproposta",
  completed: "Concluída",
  rejected: "Recusada",
  withdrawn: "Encerrada",
  expired: "Expirada",
};
const kindNames: Record<string, string> = {
  academy: "Promoção da base",
  inbound: "Contratação",
  outbound: "Venda",
  renewal: "Renovação",
  expired: "Fim do contrato",
};
const open = (o: TransferOffer) =>
  ["pending", "agreed", "countered"].includes(o.status);
function clubName(c: Career, id: string) {
  return (
    c.state.clubs.concat(c.state.otherClubs ?? []).find((x) => x.id === id)
      ?.name ?? "Agente livre"
  );
}

function NegotiationDialog({
  c,
  playerId,
  kind,
  onClose,
  onSubmit,
  busy,
  error,
}: {
  c: Career;
  playerId: number;
  kind: "inbound" | "renewal";
  onClose: () => void;
  onSubmit: (
    body: {
      playerId: number;
      fee: number;
      wage: number;
      years: number;
      installments: number;
      releaseClause: number;
    },
    path: string,
  ) => void;
  busy: boolean;
  error: string;
}) {
  const contract = c.state.contracts[playerId];
  const minYears =
    kind === "renewal"
      ? Math.max(
          1,
          (contract?.endSeason ?? c.state.season) - c.state.season + 2,
        )
      : c.state.round > seasonRounds(c.state)
        ? 2
        : 1;
  const [years, setYears] = useState(Math.min(5, Math.max(3, minYears)));
  const [installments, setInstallments] = useState(1);
  const [releaseClause, setReleaseClause] = useState(0);
  const [fee, setFee] = useState("");
  const [wage, setWage] = useState("");
  const q = useQuery({
    queryKey: ["player-quote", c.id, c.revision, playerId, kind, years],
    queryFn: () =>
      api<PlayerQuote>(
        `/careers/${c.id}/market/quote/${playerId}?kind=${kind}&years=${years}`,
      ),
    retry: false,
  });
  useEffect(() => {
    if (q.data) {
      setFee(String(q.data.fee));
      setWage(String(q.data.wage));
    }
  }, [q.data]);
  const existing = c.state.offers.some(
    (o) => o.playerId === playerId && open(o),
  );
  return (
    <DialogShell
      label={kind === "renewal" ? "Propor renovação" : "Propor contratação"}
      onClose={onClose}
      busy={busy}
    >
      <section className="panel contract-dialog">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">
              {kind === "renewal"
                ? "CONTINUIDADE DO ELENCO"
                : "UMA NOVA CONTRATAÇÃO"}
            </span>
            <h2>{q.data?.playerName ?? "Preparando negociação…"}</h2>
          </div>
          <Button
            variant="ghost"
            aria-label="Fechar negociação"
            onClick={onClose}
          >
            <X size={18} />
          </Button>
        </div>
        <form
          className="form-stack"
          onSubmit={(e) => {
            e.preventDefault();
            onSubmit(
              {
                playerId,
                fee: Number(fee),
                wage: Number(wage),
                years,
                installments:
                  kind === "inbound" && Number(fee) > 0 ? installments : 1,
                releaseClause,
              },
              kind === "renewal"
                ? `/contracts/${playerId}/renew`
                : "/market/offers",
            );
          }}
        >
          <p>
            {kind === "renewal"
              ? "Proponha um novo vínculo para continuar contando com o atleta."
              : `Clube de origem: ${q.data ? clubName(c, q.data.fromClub) : "…"}.`}
          </p>
          <div className="contract-inputs">
            <label>
              Duração
              <select
                value={years}
                onChange={(e) => setYears(Number(e.target.value))}
                disabled={busy}
              >
                {[1, 2, 3, 4, 5].map((n) => (
                  <option key={n} value={n} disabled={n < minYears}>
                    {n} {n === 1 ? "temporada" : "temporadas"} · até{" "}
                    {c.state.season + n - 1}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Salário proposto / rodada
              <input
                autoFocus
                type="number"
                required
                min={1000}
                max={10000000}
                step={1000}
                disabled={busy || q.isFetching}
                value={wage}
                onChange={(e) => setWage(e.target.value)}
              />
            </label>
            {kind === "inbound" && q.data?.fromClub && (
              <label>
                Taxa de transferência (R$)
                <input
                  type="number"
                  required
                  min={0}
                  max={5000000000}
                  step={1000}
                  disabled={busy || q.isFetching}
                  value={fee}
                  onChange={(e) => setFee(e.target.value)}
                />
              </label>
            )}
          </div>
          <div className="contract-inputs">
            {kind === "inbound" && Number(fee) > 0 && (
              <label>
                Parcelas da taxa
                <select
                  value={installments}
                  onChange={(e) => setInstallments(Number(e.target.value))}
                >
                  {[1, 2, 3, 4, 6, 12, 24].map((n) => (
                    <option key={n} value={n}>
                      {n === 1 ? "À vista" : `${n} parcelas por rodada`}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <label>
              Multa rescisória do novo contrato (R$; zero desativa)
              <input
                type="number"
                min={0}
                max={5000000000}
                step={1000}
                value={releaseClause}
                onChange={(e) => setReleaseClause(Number(e.target.value))}
              />
            </label>
          </div>
          {q.data && (
            <div className="terms-box">
              <div>
                <span>Taxa pedida pelo clube</span>
                <b>{money(q.data.fee)}</b>
              </div>
              <div>
                <span>Salário de referência / rodada</span>
                <b>{money(q.data.wage)}</b>
              </div>
              <div>
                <span>Luvas nos termos sugeridos</span>
                <b>{money(q.data.signingBonus)}</b>
              </div>
              <div>
                <span>Comissão de {q.data.agentName || "empresário"}</span>
                <b>{money(q.data.agentCommission ?? 0)}</b>
              </div>
              <div>
                <span>Caixa atual</span>
                <b className="green">{money(c.state.balance)}</b>
              </div>
            </div>
          )}
          <p className="muted">
            Enviar a proposta não movimenta o caixa. Você revisará taxa, salário
            e luvas na resposta antes de confirmar o contrato. O salário é pago
            uma vez por rodada.
          </p>
          {kind === "inbound" && q.data?.fromClub && (
            <p className="muted">
              Resposta ao avançar uma rodada. No fechamento da janela, a
              resposta é imediata.
            </p>
          )}
          {(q.error || error) && (
            <p className="error" role="alert">
              {error || q.error?.message}
            </p>
          )}
          {q.data && !q.data.available && (
            <p className="gold-text">{q.data.reason}</p>
          )}
          {existing && (
            <p className="gold-text">
              Há uma negociação aberta por este atleta. Conclua ou encerre a
              proposta anterior.
            </p>
          )}
          <div className="actions">
            <Button
              type="submit"
              disabled={
                busy ||
                q.isFetching ||
                !q.data?.available ||
                existing ||
                !wage ||
                !fee
              }
            >
              {busy ? "Enviando…" : "Enviar proposta"}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={onClose}
              disabled={busy}
            >
              Voltar
            </Button>
          </div>
        </form>
      </section>
    </DialogShell>
  );
}

export default function ManagementCenter({
  c,
  initialTab = "mercado",
}: {
  c: Career;
  initialTab?: string;
}) {
  const qc = useQueryClient();
  const [params, setParams] = useSearchParams();
  const [tab, setTab] = useState(initialTab);
  const [search, setSearch] = useState("");
  const [position, setPosition] = useState("");
  const [club, setClub] = useState("");
  const [maxFee, setMaxFee] = useState("");
  const [sort, setSort] = useState("rating");
  const [page, setPage] = useState(1);
  const [debounced, setDebounced] = useState("");
  const [negotiation, setNegotiation] = useState<{
    id: number;
    kind: "inbound" | "renewal";
  } | null>(() =>
    params.get("player") &&
    c.state.squad.some((p) => p.id === Number(params.get("player")))
      ? { id: Number(params.get("player")), kind: "renewal" }
      : null,
  );
  const [confirm, setConfirm] = useState<TransferOffer | null>(null);
  const [activeOnly, setActiveOnly] = useState(true);
  const [notice, setNotice] = useState("");
  const [contractFilter, setContractFilter] = useState("all");
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebounced(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);
  const query = new URLSearchParams({ page: String(page), sort });
  if (debounced) query.set("search", debounced);
  if (position) query.set("position", position);
  if (club) query.set("club", club);
  if (maxFee) query.set("maxFee", maxFee);
  const market = useQuery({
    queryKey: ["market", c.id, c.revision, query.toString()],
    queryFn: () => api<MarketPage>(`/careers/${c.id}/market?${query}`),
    enabled: tab === "mercado",
    gcTime: 30000,
  });
  const change = useMutation({
    mutationFn: ({ path, body }: { path: string; body: object }) =>
      api<MutationResult>(`/careers/${c.id}${path}`, {
        ...body,
        revision: c.revision,
      }),
    onSuccess: (r, variables) => {
      qc.invalidateQueries({ queryKey: ["career", c.id] });
      setNotice(r.result.message ?? "Operação salva.");
      setConfirm(null);
      if (
        variables.path.includes("/renew") ||
        variables.path === "/market/offers"
      ) {
        setNegotiation(null);
        setParams({}, { replace: true });
        setTab("negociacoes");
      }
    },
    onError: () => qc.invalidateQueries({ queryKey: ["career", c.id] }),
  });
  function mutate(path: string, body: object = {}) {
    change.reset();
    setNotice("");
    change.mutate({ path, body });
  }
  function begin(id: number, kind: "inbound" | "renewal") {
    change.reset();
    setNotice("");
    setNegotiation({ id, kind });
  }
  const g = c.state;
  const active = g.offers.filter(open);
  const contracts = g.squad.filter(
    (p) =>
      (!search || p.name.toLowerCase().includes(search.toLowerCase())) &&
      (contractFilter !== "expiring" ||
        g.contracts[p.id]?.endSeason === g.season),
  );
  const tabs = [
    ["mercado", "Mercado"],
    ["negociacoes", `Negociações (${active.length})`],
    ["contratos", "Contratos"],
    ["movimentacoes", "Movimentações"],
  ];
  const offerList = g.offers.filter((o) => !activeOnly || open(o));
  const sortedOffers = useMemo(
    () => offerList.slice().sort((a, b) => Number(open(b)) - Number(open(a))),
    [g.offers, activeOnly],
  );
  return (
    <div className="management-center">
      <div className="management-stats">
        <div className="stat">
          <span>
            Caixa disponível
            <Wallet size={17} />
          </span>
          <strong>{money(g.balance)}</strong>
          <p>Taxas e luvas são pagas ao confirmar</p>
        </div>
        <div className="stat">
          <span>
            Folha por rodada
            <UserRound size={17} />
          </span>
          <strong>{money(c.management.wageBill)}</strong>
          <p>{g.squad.length} contratos ativos</p>
        </div>
        <div className="stat">
          <span>
            Vencem em {g.season}
            <Clock size={17} />
          </span>
          <strong>{c.management.expiringContracts}</strong>
          <p>Atletas que podem sair sem taxa</p>
        </div>
      </div>
      <section className="panel window-banner">
        <CalendarDays size={23} />
        <div>
          <h2 className={c.management.window.isOpen ? "green" : "gold-text"}>
            {c.management.window.name}
            {c.management.window.closingRound
              ? ` · até a rodada ${c.management.window.closingRound}`
              : ""}
          </h2>
          <p>{c.management.window.description}</p>
        </div>
        <span className="tag">RODADA {g.round}</span>
      </section>
      <div
        className="management-tabs"
        role="tablist"
        aria-label="Gestão de contratos e mercado"
      >
        {tabs.map(([key, label]) => (
          <button
            key={key}
            role="tab"
            aria-selected={tab === key}
            onClick={() => {
              setTab(key);
              setPage(1);
              setSearch("");
              change.reset();
            }}
          >
            {label}
          </button>
        ))}
      </div>
      {change.error && !negotiation && !confirm && (
        <p className="error-banner" role="alert">
          {change.error.message}
        </p>
      )}
      {notice && (
        <p className="toast" role="status">
          <Check size={17} />
          {notice}
        </p>
      )}
      {tab === "mercado" && (
        <>
          <div className="toolbar market-filters">
            <label className="search">
              <Search size={17} />
              <input
                aria-label="Buscar jogador no mercado"
                placeholder="Buscar jogador…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </label>
            <select
              aria-label="Posição no mercado"
              value={position}
              onChange={(e) => {
                setPosition(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Todas as posições</option>
              {["GOL", "ZAG", "LAT", "MEI", "ATA"].map((p) => (
                <option key={p}>{p}</option>
              ))}
            </select>
            <select
              aria-label="Clube de origem"
              value={club}
              onChange={(e) => {
                setClub(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Todos os clubes</option>
              <option value="__free">Agentes livres</option>
              {g.clubs
                .filter((x) => x.id !== g.clubId)
                .map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
            </select>
            <select
              aria-label="Ordem do mercado"
              value={sort}
              onChange={(e) => {
                setSort(e.target.value);
                setPage(1);
              }}
            >
              <option value="rating">Maior habilidade</option>
              <option value="fee">Menor taxa</option>
              <option value="name">Nome do atleta</option>
            </select>
            <label className="market-budget">
              Taxa máxima (R$)
              <input
                type="number"
                min={0}
                step={500000}
                value={maxFee}
                onChange={(e) => {
                  setMaxFee(e.target.value);
                  setPage(1);
                }}
                placeholder="Sem limite"
              />
            </label>
          </div>
          <section className="panel">
            <div className="panel-heading">
              <h2>Radar de contratações</h2>
              <span className="muted">
                {market.data?.total ?? "…"} atletas · dados da carreira
              </span>
            </div>
            {market.isPending ? (
              <p className="empty">Consultando o mercado…</p>
            ) : market.error ? (
              <p className="error notice" role="alert">
                {market.error.message}
              </p>
            ) : (
              <>
                <div className="market-card-grid">
                  {market.data?.players.map(({ player: p, ...candidate }) => {
                    const club = c.state.clubs
                      .concat(c.state.otherClubs ?? [])
                      .find((x) => x.id === candidate.clubId);
                    return (
                      <article className="scout-card" key={p.id}>
                        <div className="scout-card-top">
                          <span>
                            {p.position} · {p.age} ANOS
                          </span>
                          <strong>
                            {p.rating}
                            <small>GER</small>
                          </strong>
                        </div>
                        <Shirt
                          color={club?.color ?? "#53687d"}
                          number={0}
                          short={club?.short ?? "LIVRE"}
                        />
                        <h3>{p.name}</h3>
                        <p>{clubName(c, candidate.clubId)}</p>
                        <dl>
                          <div>
                            <dt>Taxa pedida</dt>
                            <dd>
                              {candidate.askingFee
                                ? money(candidate.askingFee)
                                : "Sem taxa"}
                            </dd>
                          </div>
                          <div>
                            <dt>Salário / rodada</dt>
                            <dd>{money(candidate.wageDemand)}</dd>
                          </div>
                          <div>
                            <dt>Vínculo atual</dt>
                            <dd>
                              {candidate.contractEndSeason
                                ? `Até ${candidate.contractEndSeason}`
                                : "Agente livre"}
                            </dd>
                          </div>
                        </dl>
                        <Button
                          variant="outline"
                          disabled={!candidate.available || change.isPending}
                          title={candidate.reason}
                          onClick={() => begin(p.id, "inbound")}
                        >
                          {candidate.available
                            ? "Abrir negociação"
                            : candidate.reason.includes("janela")
                              ? "Janela fechada"
                              : "Indisponível"}
                        </Button>
                      </article>
                    );
                  })}
                  {market.data?.total === 0 && (
                    <p className="empty">
                      Nenhum jogador corresponde aos filtros.
                    </p>
                  )}
                </div>
                <div className="pagination">
                  <Button
                    variant="ghost"
                    disabled={page <= 1 || market.isFetching}
                    onClick={() => setPage(page - 1)}
                  >
                    Anterior
                  </Button>
                  <span>
                    Página {page} de{" "}
                    {Math.max(1, Math.ceil((market.data?.total ?? 0) / 20))}
                  </span>
                  <Button
                    variant="ghost"
                    disabled={
                      page * 20 >= (market.data?.total ?? 0) ||
                      market.isFetching
                    }
                    onClick={() => setPage(page + 1)}
                  >
                    Próxima
                  </Button>
                </div>
              </>
            )}
          </section>
        </>
      )}
      {tab === "contratos" && (
        <>
          <div className="toolbar">
            <label className="search">
              <Search size={17} />
              <input
                placeholder="Buscar jogador…"
                aria-label="Buscar jogador nos contratos"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </label>
            <select
              aria-label="Filtrar contratos"
              value={contractFilter}
              onChange={(e) => setContractFilter(e.target.value)}
            >
              <option value="all">Todos os contratos</option>
              <option value="expiring">Vencem nesta temporada</option>
            </select>
          </div>
          <section className="panel">
            <div className="panel-heading">
              <h2>Vínculos do elenco</h2>
              <span className="muted">Renove antes de avançar a temporada</span>
            </div>
            <div className="contract-cards">
              {contracts.map((p) => {
                const loan = g.loans?.find(
                  (l) => !l.returned && l.playerId === p.id,
                );
                const end =
                  loan?.endSeason ?? g.contracts[p.id]?.endSeason ?? g.season;
                return (
                  <article
                    className={`contract-card ${end === g.season ? "expiring" : ""}`}
                    key={p.id}
                  >
                    <div className="contract-person">
                      <Shirt
                        color={g.clubs.find((c) => c.id === g.clubId)!.color}
                        number={0}
                        short={p.position}
                      />
                      <div>
                        <span className="eyebrow">
                          {p.position} ·{" "}
                          {loan
                            ? "Empréstimo"
                            : p.transferListed
                              ? "À venda"
                              : p.starter
                                ? "Titular"
                                : "Reserva"}
                        </span>
                        <h3>{p.name}</h3>
                        <span
                          className={end === g.season ? "gold-text" : "muted"}
                        >
                          {end === g.season
                            ? "Último ano de contrato"
                            : `Vínculo até ${end}`}
                        </span>
                      </div>
                    </div>
                    <div className="contract-terms">
                      <div>
                        <small>Salário por rodada</small>
                        <strong>{money(p.wage)}</strong>
                      </div>
                      <div>
                        <small>Prazo restante</small>
                        <strong>
                          {Math.max(0, (end - g.season) * seasonRounds(g) + seasonRounds(g) + 1 - g.round)}{" "}
                          rodadas
                        </strong>
                      </div>
                    </div>
                    <div className="actions">
                      <Button
                        variant="outline"
                        disabled={change.isPending}
                        onClick={() => begin(p.id, "renewal")}
                      >
                        Renovar
                      </Button>
                      <Button
                        variant="ghost"
                        disabled={change.isPending}
                        onClick={() =>
                          mutate(`/market/list/${p.id}`, {
                            listed: !p.transferListed,
                          })
                        }
                      >
                        {p.transferListed
                          ? "Retirar da lista"
                          : "Colocar à venda"}
                      </Button>
                    </div>
                  </article>
                );
              })}
            </div>
            {!contracts.length && (
              <p className="empty">Nenhum vínculo encontrado neste filtro.</p>
            )}
          </section>
          <p className="notice">
            Ao fim da temporada, contratos vencidos são encerrados sem taxa e os
            atletas tornam-se agentes livres. Colocar um jogador à venda permite
            receber propostas durante a janela; não garante uma oferta.
          </p>
        </>
      )}
      {tab === "negociacoes" && (
        <>
          <div className="toolbar">
            <label className="inline-checkbox">
              <input
                type="checkbox"
                checked={activeOnly}
                onChange={(e) => setActiveOnly(e.target.checked)}
              />
              Somente negociações em aberto
            </label>
            <span className="muted">
              Propostas não reservam dinheiro. O caixa é conferido novamente na
              confirmação.
            </span>
          </div>
          <div className="offer-grid">
            {sortedOffers.map((o) => (
              <section className="panel offer-card" key={o.id}>
                <div className="panel-heading">
                  <span className={"offer-status " + o.status}>
                    {statusNames[o.status] ?? o.status}
                  </span>
                  <small className="muted">
                    {o.season} · R{o.createdRound}
                  </small>
                </div>
                <div className="offer-content">
                  <small className="eyebrow">
                    {o.kind === "renewal"
                      ? "RENOVAÇÃO"
                      : o.kind === "outbound"
                        ? "PROPOSTA RECEBIDA"
                        : "PROPOSTA ENVIADA"}
                  </small>
                  <h2>{o.playerName}</h2>
                  <p>
                    {clubName(c, o.fromClub)} <ArrowLeftRight size={13} />{" "}
                    {clubName(c, o.toClub)}
                  </p>
                  <div className="terms-box">
                    <div>
                      <span>
                        {o.kind === "outbound"
                          ? "Receita de venda"
                          : "Taxa de transferência"}
                      </span>
                      <b>{money(o.counterFee ?? o.fee)}</b>
                    </div>
                    <div>
                      <span>Salário / rodada</span>
                      <b>{money(o.counterWage ?? o.wage)}</b>
                    </div>
                    <div>
                      <span>
                        Luvas{" "}
                        {o.kind === "outbound" ? "pagas pelo comprador" : ""}
                      </span>
                      <b>{money(o.signingBonus)}</b>
                    </div>
                    <div>
                      <span>Vencimento do novo contrato</span>
                      <b>Fim de {o.season + o.years - 1}</b>
                    </div>
                  </div>
                  <p className="offer-message">{o.message}</p>
                  {open(o) && (
                    <small className="muted">
                      {o.status === "pending"
                        ? `Resposta prevista: rodada ${o.responseRound}. `
                        : ""}
                      Prazo: rodada {o.expiresRound} de {o.season}.
                    </small>
                  )}
                  <div className="actions">
                    {["agreed", "countered"].includes(o.status) && (
                      <Button
                        disabled={change.isPending}
                        onClick={() => {
                          change.reset();
                          setConfirm(o);
                        }}
                      >
                        {o.kind === "outbound"
                          ? "Aceitar venda"
                          : o.status === "countered"
                            ? "Aceitar contraproposta"
                            : "Confirmar contrato"}
                      </Button>
                    )}
                    {open(o) && (
                      <Button
                        variant="ghost"
                        disabled={change.isPending}
                        onClick={() =>
                          mutate(`/market/offers/${o.id}/withdraw`)
                        }
                      >
                        Encerrar
                      </Button>
                    )}
                  </div>
                </div>
              </section>
            ))}
          </div>
          {!sortedOffers.length && (
            <section className="panel">
              <div className="empty">
                <FilePenLine size={30} />
                <h3>Nenhuma negociação nesta seleção.</h3>
                <p>
                  Envie uma proposta no mercado ou coloque um atleta à venda nos
                  contratos.
                </p>
                <Button variant="outline" onClick={() => setTab("mercado")}>
                  Explorar mercado
                </Button>
              </div>
            </section>
          )}
        </>
      )}
      {tab === "movimentacoes" && (
        <section className="panel">
          <div className="panel-heading">
            <h2>Registro de movimentações</h2>
            <span className="muted">Histórico preservado por temporada</span>
          </div>
          <div className="transfer-journal">
            {[...g.transfers].reverse().map((t, i) => (
              <article key={`${t.season}-${t.round}-${i}`}>
                <span className="journal-date">
                  {t.season}
                  <small>Rodada {t.round}</small>
                </span>
                <div>
                  <span className="eyebrow">{kindNames[t.kind] ?? t.kind}</span>
                  <h3>{t.playerName}</h3>
                  <p>
                    {clubName(c, t.fromClub)} · {clubName(c, t.toClub)}
                  </p>
                  <small>
                    Contrato até {t.endSeason} · {money(t.wage)} por rodada
                  </small>
                </div>
                <strong>{money(t.fee)}</strong>
              </article>
            ))}
          </div>
          {!g.transfers.length && (
            <p className="empty">
              As próximas negociações concluídas aparecerão aqui.
            </p>
          )}
        </section>
      )}
      {negotiation && (
        <NegotiationDialog
          key={`${negotiation.kind}-${negotiation.id}`}
          c={c}
          playerId={negotiation.id}
          kind={negotiation.kind}
          busy={change.isPending}
          error={change.error?.message ?? ""}
          onClose={() => {
            setNegotiation(null);
            setParams({}, { replace: true });
            change.reset();
          }}
          onSubmit={(body, path) => mutate(path, body)}
        />
      )}
      {confirm && (
        <DialogShell
          label="Confirmar termos do contrato"
          busy={change.isPending}
          onClose={() => setConfirm(null)}
        >
          <section className="panel contract-dialog">
            <div className="panel-heading">
              <h2>
                {confirm.kind === "outbound"
                  ? "Concluir venda"
                  : "Registrar contrato"}{" "}
                · {confirm.playerName}
              </h2>
            </div>
            <div className="form-stack">
              <div className="terms-box">
                <div>
                  <span>Taxa de transferência</span>
                  <b>{money(confirm.counterFee ?? confirm.fee)}</b>
                </div>
                <div>
                  <span>
                    Luvas {confirm.kind === "outbound" ? "do comprador" : ""}
                  </span>
                  <b>{money(confirm.signingBonus)}</b>
                </div>
                <div>
                  <span>Salário por rodada</span>
                  <b>{money(confirm.counterWage ?? confirm.wage)}</b>
                </div>
                <div>
                  <span>Comissão empresarial {confirm.agentName}</span>
                  <b>{money(confirm.agentCommission ?? 0)}</b>
                </div>
                <div className="terms-total">
                  <span>
                    {confirm.kind === "outbound"
                      ? "Entrada no caixa"
                      : "Saída imediata do caixa"}
                  </span>
                  <b>
                    {money(
                      confirm.kind === "outbound"
                        ? (confirm.counterFee ?? confirm.fee)
                        : Math.ceil(
                            (confirm.counterFee ?? confirm.fee) /
                              (confirm.installments ?? 1),
                          ) +
                            confirm.signingBonus +
                            (confirm.agentCommission ?? 0),
                    )}
                  </b>
                </div>
              </div>
              <p>
                Contrato até o fim de {confirm.season + confirm.years - 1}. Taxa
                em {confirm.installments ?? 1} parcela(s); luvas à vista. Multa
                do contrato: {money(confirm.releaseClause ?? 0)}.{" "}
                {confirm.kind === "outbound"
                  ? "O salário passa a ser responsabilidade do comprador. Confira a escalação se o atleta era titular."
                  : "O salário será incluído na folha das próximas rodadas."}
              </p>
              {change.error && (
                <p className="error" role="alert">
                  {change.error.message}
                </p>
              )}
              <div className="actions">
                <Button
                  disabled={change.isPending}
                  onClick={() => mutate(`/market/offers/${confirm.id}/accept`)}
                >
                  {change.isPending ? "Registrando…" : "Confirmar operação"}
                </Button>
                <Button
                  variant="outline"
                  disabled={change.isPending}
                  onClick={() => setConfirm(null)}
                >
                  Voltar
                </Button>
              </div>
            </div>
          </section>
        </DialogShell>
      )}
    </div>
  );
}
