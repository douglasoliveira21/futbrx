import { queryClient } from "../services/queryClient";
import React, { lazy, Suspense, useState } from "react";
import {
  NavLink,
  Routes,
  Route,
  useNavigate,
  useLocation,
} from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { FutbrxLogo } from "../components/FutbrxLogo";

import {
  LayoutDashboard,
  Users,
  Waypoints,
  CalendarDays,
  Trophy,
  ArrowLeftRight,
  Wallet,
  Dumbbell,
  HeartPulse,
  Sprout,
  Building2,
  ChartNoAxesCombined,
  History,
  Bell,
  Menu,
  LogOut,
  Plus,
  Search,
  Shield,
  Check,
  Play,
  Flag,
} from "lucide-react";
import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import {
  api,
  setToken,
  money,
  moneyExact,
  type Career,
  type Club,
  type Player,
  type Fixture,
  type DataRelease,
  type BroadcastSession,
} from "../services/api";
import { StadiumAudio } from "../features/match3d/StadiumAudio";
import { useIdentity } from "../hooks/useIdentity";
import { useUI } from "../stores/ui";
import { Button } from "../components/ui/button";
import { Crest } from "../components/Crest";
import CoachHub from "../features/home/CoachHub";
import "../styles/index.css";
i18n.use(initReactI18next).init({
  lng: "pt-BR",
  fallbackLng: "pt-BR",
  resources: {
    "pt-BR": {
      translation: {
        dashboard: "Visão geral",
        continue: "Avançar rodada",
        welcome: "O próximo capítulo começa aqui.",
      },
    },
  },
  interpolation: { escapeValue: false },
});
const LazyFinanceChart = lazy(() => import("../features/finance/FinanceChart"));
const AdvancedMarketCenter = lazy(
  () => import("../features/transfers/AdvancedMarketCenter"),
);
const CompetitionCenter = lazy(
  () => import("../features/sports/CompetitionCenter"),
);
const PlayerStudio = lazy(() => import("../features/match3d/PlayerStudio"));
const DataSources = lazy(() => import("../features/catalog/DataSources"));
const YouthCenter = lazy(() => import("../features/youth/YouthCenter"));
const TrainingCenter = lazy(
  () => import("../features/training/TrainingCenter"),
);
const SquadRoom = lazy(() => import("../features/sports/SquadRoom"));
const TacticsRoom = lazy(() => import("../features/sports/TacticsRoom"));
const SportsCenter = lazy(() => import("../features/sports/SportsCenter"));
const Match3D = lazy(() => import("../features/match3d/Match3D"));
const AdminCatalog = lazy(() => import("../features/catalog/AdminCatalog"));
const ManagementCenter = lazy(
  () => import("../features/transfers/ManagementCenter"),
);
const SeasonHistory = lazy(() => import("../features/career/SeasonHistory"));
const AccountSessions = lazy(
  () => import("../features/account/AccountSessions"),
);
const ClubLifeCenter = lazy(() => import("../features/career/ClubLifeCenter"));
const CoachCareerCenter = lazy(
  () => import("../features/career/CoachCareerCenter"),
);
const nav = [
  ["", "Visão geral", LayoutDashboard],
  ["elenco", "Elenco", Users],
  ["contratos", "Contratos", Shield],
  ["taticas", "Táticas", Waypoints],
  ["calendario", "Calendário", CalendarDays],
  ["competicoes", "Competições", Trophy],
  ["transferencias", "Transferências", ArrowLeftRight],
  ["financas", "Finanças", Wallet],
  ["treinamentos", "Treinamentos", Dumbbell],
  ["medico", "Departamento médico", HeartPulse],
  ["base", "Categorias de base", Sprout],
  ["diretoria", "Diretoria", Building2],
  ["minha-carreira", "Minha carreira", History],
  ["mercado-tecnicos", "Mercado de técnicos", ArrowLeftRight],
  ["vestiario", "Vestiário", Users],
  ["empresarios", "Empresários", Shield],
  ["olheiros", "Olheiros", Search],
  ["torcida", "Torcida", Flag],
  ["imprensa", "Imprensa", Bell],
  ["comissao", "Comissão técnica", Users],
  ["estatisticas", "Estatísticas", ChartNoAxesCombined],
  ["historico", "Histórico", History],
  ["fontes", "Dados e licenças", Shield],
  ["visual", "Estúdio do jogador", Users],
  ["gestao-avancada", "Gestão avançada", ArrowLeftRight],
] as const;
function Panel({
  title,
  tag,
  children,
  className = "",
}: {
  title?: string;
  tag?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <section className={"panel " + className}>
      {title && (
        <div className="panel-heading">
          <h2>{title}</h2>
          {tag}
        </div>
      )}
      {children}
    </section>
  );
}
function Auth({ onLogin }: { onLogin: (name: string) => void }) {
  const [register, setRegister] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [name, setName] = useState("");
  const m = useMutation({
    mutationFn: () =>
      api<{ token: string; user: { name: string } }>(
        "/auth/" + (register ? "register" : "login"),
        { email, password, name: register ? name : "Treinador" },
      ),
    onSuccess: (r) => {
      setToken(r.token);
      onLogin(r.user.name);
    },
  });
  return (
    <main className="auth">
      <div className="auth-story">
        <div className="brand">
          <FutbrxLogo />
        </div>
        <span className="eyebrow">SEU CLUBE. SUA HISTÓRIA.</span>
        <h1>
          O futebol acontece em campo.
          <br />
          <span>A conquista começa com você.</span>
        </h1>
        <p>
          Do primeiro treino ao último apito. Assuma o comando e escreva uma
          nova história no futebol brasileiro.
        </p>
        <div className="auth-pitch">
          <div className="center-circle" />
          <div className="half-line" />
        </div>
        <small>Edição de desenvolvimento · dados demonstrativos</small>
      </div>
      <form
        className="auth-form panel"
        onSubmit={(e) => {
          e.preventDefault();
          m.mutate();
        }}
      >
        <Trophy className="green" size={32} />
        <h2>
          {register
            ? "Seu lugar no banco de comando."
            : "Bem-vindo ao vestiário."}
        </h2>
        <p>
          {register
            ? "Crie sua conta para iniciar uma carreira."
            : "Entre para continuar sua carreira."}
        </p>
        {register && (
          <label>
            Nome do treinador
            <input
              required
              maxLength={60}
              value={name}
              onChange={(e) => setName(e.target.value)}
              autoComplete="name"
            />
          </label>
        )}
        <label>
          E-mail
          <input
            required
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
          />
        </label>
        <label>
          Senha
          <input
            required
            type="password"
            minLength={register ? 12 : 1}
            maxLength={128}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete={register ? "new-password" : "current-password"}
          />
        </label>
        {register && <small>Mínimo de 12 caracteres.</small>}
        {m.error && (
          <p role="alert" className="error">
            {m.error.message}
          </p>
        )}
        <Button disabled={m.isPending}>
          {m.isPending
            ? "Conectando…"
            : register
              ? "Criar conta"
              : "Entrar no clube"}
        </Button>
        <Button
          type="button"
          variant="ghost"
          onClick={() => {
            setRegister(!register);
            m.reset();
          }}
        >
          {register ? "Já tenho uma conta" : "Criar minha conta"}
        </Button>
      </form>
    </main>
  );
}
function CareerPicker({
  onClose,
  onLogout,
}: {
  onClose?: () => void;
  onLogout?: () => void;
}) {
  const ui = useUI();
  const identity = useIdentity();
  const [accountView, setAccountView] = useState("");
  const [releaseId, setReleaseId] = useState("");
  const catalogs = useQuery({
    queryKey: ["catalogs"],
    queryFn: () => api<DataRelease[]>("/catalogs"),
  });
  const selectedReleaseId = releaseId || catalogs.data?.[0]?.id || "";
  const release = catalogs.data?.find((r) => r.id === selectedReleaseId);
  const q = useQuery({
    queryKey: ["careers"],
    queryFn: () => api<{ id: string; name: string }[]>("/careers/"),
  });
  const clubs = useQuery({
    queryKey: ["clubs", selectedReleaseId],
    queryFn: () => api<Club[]>("/clubs?releaseId=" + selectedReleaseId),
    enabled: !!selectedReleaseId,
  });
  const [name, setName] = useState("Minha primeira carreira");
  const [club, setClub] = useState("pal");
  React.useEffect(() => {
    if (clubs.data && !clubs.data.some((c) => c.id === club))
      setClub(clubs.data[0]?.id ?? "");
  }, [clubs.data, club]);
  const qc = useQueryClient();
  const m = useMutation({
    mutationFn: () =>
      api<{ id: string }>("/careers/", {
        name,
        clubId: club,
        releaseId: selectedReleaseId,
      }),
    onSuccess: (r) => {
      ui.select(r.id);
      qc.invalidateQueries({ queryKey: ["careers"] });
      onClose?.();
    },
  });
  if (accountView)
    return (
      <div className="picker-wrap">
        <div className="account-full">
          <Button variant="outline" onClick={() => setAccountView("")}>
            Voltar às carreiras
          </Button>
          <Suspense fallback={<p className="empty">Carregando…</p>}>
            {accountView === "admin" ? (
              <AdminCatalog />
            ) : (
              <AccountSessions onLogout={onLogout ?? (() => {})} />
            )}
          </Suspense>
        </div>
      </div>
    );
  return (
    <div className="picker-wrap">
      <section className="panel picker">
        <span className="eyebrow">UM NOVO COMEÇO</span>
        <h1>Qual será o seu clube?</h1>
        <p>Uma nova carreira. Vinte clubes. Uma história para escrever.</p>
        <div className="account-links">
          {identity.data?.isAdmin && (
            <Button variant="outline" onClick={() => setAccountView("admin")}>
              Administrar dados
            </Button>
          )}
          {onLogout && (
            <Button variant="ghost" onClick={() => setAccountView("sessions")}>
              Conta e sessões
            </Button>
          )}
        </div>
        {q.data && q.data.length > 0 && (
          <div className="saved">
            <h3>Retomar carreira</h3>
            {q.data.map((c) => (
              <Button
                variant="outline"
                key={c.id}
                onClick={() => {
                  ui.select(c.id);
                  onClose?.();
                }}
              >
                {c.name}
                <Play size={15} />
              </Button>
            ))}
          </div>
        )}
        <form
          onSubmit={(e) => {
            e.preventDefault();
            m.mutate();
          }}
        >
          <label>
            Base de dados da temporada
            <select
              value={selectedReleaseId}
              onChange={(e) => {
                setReleaseId(e.target.value);
                m.reset();
              }}
              disabled={catalogs.isPending || m.isPending}
            >
              {catalogs.data?.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.version === "serie-a-2026-misto-candidatos-v2" ? "Série A 2026 · jogadores reais parciais, vínculos não verificados" : r.version === "serie-a-2026-clubes-reais-v1" ? "Série A 2026 · clubes reais, elencos simulados" : `${r.season} · ${r.version}`}
                </option>
              ))}
            </select>
          </label>
          <label>
            Nome da carreira
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              maxLength={60}
            />
          </label>
          <div className="club-grid">
            {clubs.data?.map((c) => (
              <button
                type="button"
                key={c.id}
                className={
                  club === c.id ? "club-option selected" : "club-option"
                }
                onClick={() => setClub(c.id)}
                aria-pressed={club === c.id}
              >
                <Crest club={c} />
                <span>{c.name}</span>
                {club === c.id && <Check size={15} />}
              </button>
            ))}
          </div>
          <p className="muted">
            {release?.source} {release?.license}
          </p>
          {(m.error || q.error || clubs.error || catalogs.error) && (
            <p className="error" role="alert">
              {(m.error || q.error || clubs.error || catalogs.error)?.message}
            </p>
          )}
          <div className="actions">
            <Button
              disabled={
                m.isPending ||
                !clubs.data ||
                clubs.isFetching ||
                !selectedReleaseId ||
                !club
              }
            >
              Assumir o comando
            </Button>
            {onClose && (
              <Button type="button" variant="outline" onClick={onClose}>
                Voltar
              </Button>
            )}
          </div>
        </form>
      </section>
    </div>
  );
}
type Action = (
  type: string,
  value?: string,
  extra?: string,
  playerId?: number,
) => void;
function FixtureLine({
  f,
  c,
  onReplay,
}: {
  f: Fixture;
  c: Career;
  onReplay?: (f: Fixture) => void;
}) {
  const h = c.state.clubs.find((x) => x.id === f.home)!;
  const a = c.state.clubs.find((x) => x.id === f.away)!;
  return (
    <div className="fixture-line">
      <span
        className="muted"
        title={f.kickoff ?? f.date ?? "Data não registrada"}
      >
        {f.round}ª
        {f.date && (
          <small style={{ display: "block", fontSize: 10 }}>
            {f.date.slice(5).split("-").reverse().join("/")}
            {f.kickoff && (
              <span style={{ display: "block" }}>
                {f.kickoff.slice(11, 16)}
              </span>
            )}
          </small>
        )}
      </span>
      <span className="fixture-team">
        {h.short}
        <Crest club={h} />
      </span>
      <b>
        {f.homeGoals ?? "–"} <span className="muted">:</span>{" "}
        {f.awayGoals ?? "–"}
      </b>
      <span className="fixture-team">
        <Crest club={a} />
        {a.short}
      </span>
      {onReplay && f.homeGoals !== null && (
        <Button
          variant="ghost"
          onClick={() => onReplay(f)}
          aria-label={"Rever " + h.name + " e " + a.name}
        >
          <Play size={15} />
        </Button>
      )}
    </div>
  );
}
function FinanceChart({ c }: { c: Career }) {
  return (
    <Suspense
      fallback={
        <div className="chart-area" style={{ height: 180 }}>
          Carregando evolução financeira…
        </div>
      }
    >
      <LazyFinanceChart c={c} />
    </Suspense>
  );
}
function Module({
  path,
  c,
  action,
  busy,
  onReplay,
}: {
  path: string;
  c: Career;
  action: Action;
  busy: boolean;
  onReplay: (f: Fixture) => void;
}) {
  const [filter, setFilter] = useState("meu");
  const g = c.state;
  if (["diretoria", "minha-carreira", "mercado-tecnicos"].includes(path))
    return (
      <Suspense fallback={<p>Abrindo carreira…</p>}>
        <CoachCareerCenter
          c={c}
          initialTab={
            path === "diretoria"
              ? "board"
              : path === "minha-carreira"
                ? "career"
                : "market"
          }
        />
      </Suspense>
    );
  if (path === "gestao-avancada")
    return (
      <Suspense fallback={<p>Carregando gestão…</p>}>
        <AdvancedMarketCenter c={c} />
      </Suspense>
    );
  if (path === "visual")
    return (
      <Suspense fallback={<p>Carregando jogador…</p>}>
        <PlayerStudio clubs={g.clubs} />
      </Suspense>
    );
  if (path === "fontes")
    return (
      <Suspense fallback={<p>Carregando fontes…</p>}>
        <DataSources />
      </Suspense>
    );
  if (path === "elenco")
    return (
      <Suspense fallback={<p className="empty">Abrindo vestiário…</p>}>
        <SquadRoom c={c} action={action} busy={busy} />
      </Suspense>
    );
  if (path === "transferencias" || path === "contratos")
    return (
      <Suspense
        fallback={<p className="empty">Preparando central de negociações…</p>}
      >
        <ManagementCenter
          c={c}
          initialTab={path === "contratos" ? "contratos" : "mercado"}
        />
      </Suspense>
    );
  if (path === "historico")
    return (
      <Suspense fallback={<p className="empty">Abrindo histórico…</p>}>
        <SeasonHistory c={c} onReplay={onReplay} />
      </Suspense>
    );
  if (path === "taticas")
    return (
      <>
        <Suspense fallback={<p className="empty">Abrindo prancheta…</p>}>
          <TacticsRoom c={c} action={action} busy={busy} />
        </Suspense>
        <details className="scheduled-substitutions">
          <summary>Substituições programadas e reposição automática</summary>
          <Suspense fallback={<p>Carregando banco…</p>}>
            <SportsCenter key={c.revision} c={c} mode="plan" />
          </Suspense>
        </details>
      </>
    );
  if (path === "competicoes")
    return (
      <Suspense fallback={<p>Carregando competição…</p>}>
        <ClubLifeCenter c={c} initialTab="cups" onReplay={onReplay} />
        <CompetitionCenter c={c} />
      </Suspense>
    );
  if (path === "calendario")
    return (
      <>
        <Suspense fallback={<p>Abrindo agenda…</p>}>
          <ClubLifeCenter c={c} initialTab="calendar" onReplay={onReplay} />
        </Suspense>
        <div className="toolbar">
          <select
            aria-label="Filtrar calendário"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          >
            <option value="meu">Jogos do meu clube</option>
            <option value="todos">Todos os jogos</option>
          </select>
        </div>
        <p className="notice">
          {g.calendarSource ?? "Calendário da carreira"}. Horários de Brasília.
          Avance as rodadas no seu ritmo.
        </p>
        <div className="calendar-grid">
          {Array.from({ length: 38 }, (_, i) => (
            <Panel
              key={i}
              title={"Rodada " + (i + 1)}
              tag={
                <span className="muted">
                  {i + 1 < g.round
                    ? "Concluída"
                    : i + 1 === g.round
                      ? "Próxima"
                      : "Agendada"}
                </span>
              }
            >
              {g.fixtures
                .filter(
                  (f) =>
                    f.round === i + 1 &&
                    (filter === "todos" ||
                      f.home === g.clubId ||
                      f.away === g.clubId),
                )
                .map((f) => (
                  <FixtureLine key={f.home} f={f} c={c} onReplay={onReplay} />
                ))}
            </Panel>
          ))}
        </div>
      </>
    );
  if (path === "financas")
    return (
      <>
        <Suspense fallback={<p>Abrindo finanças…</p>}>
          <ClubLifeCenter c={c} initialTab="finance" onReplay={onReplay} />
        </Suspense>
        <div className="financial-summary">
          <div className="stat">
            <span>Saldo em caixa</span>
            <strong>{moneyExact(g.balance)}</strong>
          </div>
          <div className="stat">
            <span>Folha por rodada</span>
            <strong>
              {moneyExact(g.squad.reduce((a, p) => a + p.wage, 0))}
            </strong>
          </div>
          <div className="stat">
            <span>Valor do elenco</span>
            <strong>{money(g.squad.reduce((a, p) => a + p.value, 0))}</strong>
          </div>
        </div>
        <Panel title="Evolução financeira">
          <FinanceChart c={c} />
        </Panel>
        <Panel title="Livro caixa">
          <div className="transfer-journal">
            {g.ledger
              .slice()
              .reverse()
              .map((entry, i) => (
                <article key={`${entry.season}-${entry.round}-${i}`}>
                  <span className="journal-date">
                    R{entry.round}
                    <small>{entry.season || "Legado"}</small>
                  </span>
                  <div>
                    <span className="eyebrow">
                      {entry.amount > 0
                        ? "Receita do clube"
                        : "Compromisso financeiro"}
                    </span>
                    <h3>{entry.description}</h3>
                  </div>
                  <strong
                    className={entry.amount > 0 ? "positive" : "gold-text"}
                  >
                    {money(entry.amount)}
                  </strong>
                </article>
              ))}
          </div>
          {!g.ledger.length && (
            <p className="empty">
              As receitas e despesas da sua gestão aparecerão aqui.
            </p>
          )}
        </Panel>
      </>
    );
  if (path === "treinamentos")
    return (
      <Suspense fallback={<p>Carregando planejamento semanal…</p>}>
        <TrainingCenter c={c} />
      </Suspense>
    );
  if (path === "medico")
    return (
      <Suspense fallback={<p>Carregando disponibilidade…</p>}>
        <ClubLifeCenter c={c} initialTab="medical" onReplay={onReplay} />
        <SportsCenter key={c.revision} c={c} mode="medical" />
      </Suspense>
    );
  if (path === "base")
    return (
      <Suspense fallback={<p>Carregando categorias de base…</p>}>
        <ClubLifeCenter c={c} initialTab="academy" onReplay={onReplay} />
        <YouthCenter c={c} />
      </Suspense>
    );
  const lifeTabs: Record<string, string> = {
    diretoria: "board",
    vestiario: "players",
    empresarios: "agents",
    olheiros: "scouts",
    torcida: "fans",
    imprensa: "press",
    comissao: "staff",
  };
  if (lifeTabs[path])
    return (
      <Suspense fallback={<p>Abrindo bastidores…</p>}>
        <ClubLifeCenter c={c} initialTab={lifeTabs[path]} onReplay={onReplay} />
      </Suspense>
    );
  if (path === "estatisticas")
    return (
      <Suspense fallback={<p>Carregando estatísticas…</p>}>
        <SportsCenter c={c} mode="statistics" />
      </Suspense>
    );
  return (
    <Panel title="Memória da carreira">
      <div className="form-stack">
        <h3>{c.name}</h3>
        <p>
          Temporada atual: {g.season} · {g.round - 1} rodadas concluídas.
        </p>
        {g.history.length ? (
          g.history.map((x, i) => <p key={i}>{x}</p>)
        ) : (
          <p>
            Sua primeira temporada está em andamento. As temporadas concluídas
            aparecerão aqui.
          </p>
        )}
        <h3>Diário do clube</h3>
        {g.news.map((x, i) => (
          <p key={i} className="history-line">
            {x}
          </p>
        ))}
      </div>
    </Panel>
  );
}
function GameApp({ user, onLogout }: { user: string; onLogout: () => void }) {
  const location = useLocation();
  const identity = useIdentity();
  const ui = useUI();
  const qc = useQueryClient();
  const [picker, setPicker] = useState(false);
  const [notifications, setNotifications] = useState(false);
  const [replay, setReplay] = useState<Fixture | null>(null);
  const [liveSession, setLiveSession] = useState<BroadcastSession | null>(null);
  const [toast, setToast] = useState("");
  const q = useQuery({
    queryKey: ["career", ui.career],
    queryFn: () => api<Career>("/careers/" + ui.career),
    enabled: !!ui.career,
  });
  const startMatch = useMutation({
    mutationFn: () =>
      api<BroadcastSession>("/careers/" + ui.career + "/live/start", {
        revision: q.data!.revision,
      }),
    onSuccess: (session) => {
      setReplay(null);
      setLiveSession(session);
      qc.invalidateQueries({ queryKey: ["career", ui.career] });
    },
    onError: () => {
      StadiumAudio.releasePrepared();
      qc.invalidateQueries({ queryKey: ["career", ui.career] });
    },
  });
  const m = useMutation({
    mutationFn: (r: object) => api("/careers/" + ui.career + "/actions", r),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["career", ui.career] });
      setToast("Alterações salvas na carreira.");
    },
    onError: () => qc.invalidateQueries({ queryKey: ["career", ui.career] }),
  });
  const action: Action = (type, value = "", extra = "", playerId = -1) => {
    if (type === "advance") {
      StadiumAudio.prepare();
      startMatch.mutate();
      return;
    }
    if (q.data) {
      setToast("");
      m.mutate({ type, value, extra, playerId, revision: q.data.revision });
    }
  };
  if (!ui.career) return <CareerPicker onLogout={onLogout} />;
  if (q.isPending)
    return <div className="loading">Preparando o centro de comando…</div>;
  if (q.error || !q.data)
    return (
      <div className="picker-wrap">
        <Panel title="Não foi possível abrir a carreira">
          <p className="notice">{q.error?.message}</p>
          <Button
            onClick={() => {
              ui.select("");
            }}
          >
            Voltar às carreiras
          </Button>
        </Panel>
      </div>
    );
  const c = q.data;
  const live = liveSession ?? c.state.liveMatch;
  const club = c.state.clubs.find((x) => x.id === c.state.clubId)!;
  return (
    <div className={"app-shell " + (ui.collapsed ? "nav-open" : "")}>
      <aside className="sidebar" inert={!!live}>
        <div className="brand">
          <FutbrxLogo />
        </div>
        <div className="current-club" aria-label="Clube da carreira atual">
          {c.state.coach?.employed === false ? (
            <Users size={24} />
          ) : (
            <Crest club={club} />
          )}
          <span>
            <b>
              {c.state.coach?.employed === false
                ? "Treinador livre"
                : club.name}
            </b>
            <small>{c.name}</small>
          </span>
        </div>
        <small className="nav-caption">SEU VESTIÁRIO</small>
        <nav>
          {nav
            .filter(
              ([path]) =>
                c.state.coach?.employed !== false ||
                ["minha-carreira", "mercado-tecnicos"].includes(path),
            )
            .map(([path, label, Icon], i) => (
              <React.Fragment key={path}>
                {i === 7 && <div className="nav-divider" />}
                <NavLink
                  to={"/" + path}
                  end
                  onClick={() => ui.collapsed && ui.toggle()}
                >
                  <Icon size={18} />
                  <span>{label}</span>
                  {path === "" && <span className="active-mark" />}
                </NavLink>
              </React.Fragment>
            ))}
        </nav>
        <div className="sidebar-bottom">
          <span className="save-state">
            <Check size={14} /> Salvamento na conta
          </span>
          <button onClick={() => setPicker(true)}>
            <Plus size={17} /> Nova carreira
          </button>
          <NavLink to="/conta">
            <Shield size={17} />
            Conta e sessões
          </NavLink>
          {identity.data?.isAdmin && (
            <NavLink to="/administracao">
              <Building2 size={17} />
              Dados de temporada
            </NavLink>
          )}
          <button onClick={onLogout}>
            <LogOut size={17} /> Sair da conta
          </button>
          <small>BUILD 0.17 · FUTEBOL INTELIGENTE</small>
        </div>
      </aside>
      <div className="main-shell" inert={!!live}>
        <header className="topbar">
          <div className="top-context">
            <button
              className="icon-button mobile-menu"
              onClick={ui.toggle}
              aria-label="Abrir menu"
            >
              <Menu />
            </button>
            <span className="league-symbol">
              <Trophy size={19} />
            </span>
            <span>
              {c.state.coach?.employed === false
                ? "Mercado de treinadores"
                : `Brasileirão Série ${c.state.division ?? "A"}`}
              <small>Temporada {c.state.season}</small>
            </span>
          </div>
          <div className="top-actions">
            <span className="game-date">
              <CalendarDays size={16} />
              <span>
                Rodada {Math.min(c.state.round, 38)} de 38
                <small>
                  {c.state.coach?.employed === false
                    ? "Treinador livre"
                    : c.state.round > 38
                      ? "Temporada encerrada"
                      : "Planejamento semanal"}
                </small>
              </span>
            </span>
            <button
              className="icon-button notification-button"
              onClick={() => setNotifications(!notifications)}
              aria-label="Notificações"
            >
              <Bell size={19} />
              <i />
            </button>
            <span className="manager-avatar">
              {user.slice(0, 2).toUpperCase()}
            </span>
            <Button
              disabled={
                m.isPending ||
                startMatch.isPending ||
                (c.state.round > 38 && !c.state.nextCup) ||
                c.state.board?.dismissed
              }
              onClick={() => action("advance")}
            >
              {startMatch.isPending ? "Abrindo estádio…" : "Jogar partida"}
              <Play size={15} fill="currentColor" />
            </Button>
          </div>
        </header>
        {notifications && (
          <div className="notification-panel panel">
            <h3>Caixa de entrada</h3>
            {c.state.news.slice(0, 5).map((n, i) => (
              <p key={i}>{n}</p>
            ))}
            <Button variant="ghost" onClick={() => setNotifications(false)}>
              Fechar
            </Button>
          </div>
        )}
        <main className="workspace">
          {(m.error || startMatch.error || toast) && (
            <div
              role={m.error || startMatch.error ? "alert" : "status"}
              className={m.error || startMatch.error ? "error-banner" : "toast"}
            >
              {m.error?.message ?? startMatch.error?.message ?? toast}
              <button
                aria-label="Fechar mensagem"
                onClick={() => {
                  m.reset();
                  startMatch.reset();
                  setToast("");
                }}
              >
                ×
              </button>
            </div>
          )}
          {c.state.coach?.employed === false ||
          c.state.coach?.unreadDismissal ? (
            <Suspense fallback={<p>Abrindo mercado de treinadores…</p>}>
              {location.pathname === "/conta" ? (
                <AccountSessions onLogout={onLogout} />
              ) : (
                <CoachCareerCenter
                  key={c.id + location.pathname}
                  c={c}
                  initialTab={
                    location.pathname === "/minha-carreira"
                      ? "career"
                      : "market"
                  }
                />
              )}
            </Suspense>
          ) : (
            <Routes>
              <Route
                path="/administracao"
                element={
                  <Suspense
                    fallback={<p className="empty">Carregando editor…</p>}
                  >
                    <AdminCatalog />
                  </Suspense>
                }
              />
              <Route
                path="/conta"
                element={
                  <Suspense
                    fallback={<p className="empty">Carregando sessões…</p>}
                  >
                    <AccountSessions onLogout={onLogout} />
                  </Suspense>
                }
              />
              <Route
                path="/"
                element={
                  <CoachHub
                    c={c}
                    onReplay={setReplay}
                    onPlay={() => {
                      StadiumAudio.prepare();
                      startMatch.mutate();
                    }}
                    busy={startMatch.isPending}
                  />
                }
              />
              {nav.slice(1).map(([path, label]) => (
                <Route
                  key={path}
                  path={"/" + path}
                  element={
                    <>
                      <div className="page-heading">
                        <div>
                          <span className="eyebrow">SEU VESTIÁRIO</span>
                          <h1>{label}</h1>
                          <p>
                            {club.name} · Temporada {c.state.season}
                          </p>
                        </div>
                      </div>
                      <Module
                        key={path + c.id}
                        path={path}
                        c={c}
                        action={action}
                        busy={m.isPending}
                        onReplay={setReplay}
                      />
                    </>
                  }
                />
              ))}
              <Route
                path="*"
                element={
                  <Panel title="Página não encontrada">
                    <NavLink to="/">Voltar à visão geral</NavLink>
                  </Panel>
                }
              />
            </Routes>
          )}
          <footer>
            <span>futbrx</span>
            <span>
              Seu clube. Sua história. <i /> Edição de desenvolvimento
            </span>
          </footer>
        </main>
      </div>
      {picker && (
        <div className="modal-backdrop">
          <div role="dialog" aria-modal="true" aria-label="Gerenciar carreiras">
            <CareerPicker onClose={() => setPicker(false)} />
          </div>
        </div>
      )}
      {live && (
        <div
          className="broadcast-takeover"
          role="dialog"
          aria-modal="true"
          aria-label="Transmissão ao vivo"
        >
          <Suspense
            fallback={<div className="loading">Acendendo os refletores…</div>}
          >
            <Match3D
              key={live.id}
              fixture={live.fixture}
              clubs={[...c.state.clubs, ...(c.state.competitionClubs ?? [])]}
              careerId={c.id}
              season={live.season}
              live={live}
              onComplete={async () => {
                const updated = await api<Career>(
                  `/careers/${c.id}/live/finish`,
                  { id: live.id },
                );
                qc.setQueryData(["career", ui.career], updated);
                setLiveSession(null);
                qc.invalidateQueries({ queryKey: ["career", ui.career] });
              }}
            />
          </Suspense>
        </div>
      )}
      {replay && !live && (
        <div className="modal-backdrop">
          <section
            className="panel replay-modal"
            role="dialog"
            aria-modal="true"
            aria-label="Reprodução da partida"
          >
            <div className="panel-heading">
              <h2>
                Melhores momentos · {replay.season ?? c.state.season} · Rodada{" "}
                {replay.round}
              </h2>
              <Button variant="outline" onClick={() => setReplay(null)}>
                Fechar
              </Button>
            </div>
            <Suspense
              fallback={<p className="empty">Carregando estádio 3D…</p>}
            >
              <Match3D
                fixture={replay}
                clubs={[
                  ...(replay.archiveClubs ?? c.state.clubs),
                  ...(c.state.competitionClubs ?? []),
                  ...(c.state.otherClubs ?? []),
                ]}
                careerId={c.id}
                season={replay.season ?? c.state.season}
              />
            </Suspense>
          </section>
        </div>
      )}
    </div>
  );
}
export default function App() {
  const [user, setUser] = useState("");
  const [ready, setReady] = useState(false);
  React.useEffect(() => {
    api<{ token: string; user: { name: string } }>("/auth/refresh", {}, false)
      .then((r) => {
        setToken(r.token);
        setUser(r.user.name);
      })
      .catch(() => {})
      .finally(() => setReady(true));
  }, []);
  async function logout() {
    try {
      await api("/auth/logout", {});
      setToken("");
      setUser("");
      queryClient.clear();
      useUI.getState().select("");
    } catch (e) {
      alert(e instanceof Error ? e.message : "Não foi possível sair.");
    }
  }
  if (!ready) return <div className="loading">Abrindo o vestiário…</div>;
  return user ? (
    <GameApp user={user} onLogout={logout} />
  ) : (
    <Auth
      onLogin={(name) => {
        useUI.getState().select("");
        setUser(name);
      }}
    />
  );
}
