import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowDown,
  ArrowLeftRight,
  ArrowUp,
  Crosshair,
  Maximize2,
  MoveHorizontal,
  Target,
  Zap,
} from "lucide-react";
import { Button } from "../../components/ui/button";
import { Crest } from "../../components/Crest";
import {
  api,
  type Career,
  type MatchPlayer,
  type Player,
  type TacticalPlan,
  type TrainingData,
} from "../../services/api";
import { defaultTacticalPlan } from "../sports/TacticalInstructions";
import TacticalPitch, { Kit } from "../match3d/TacticalPitch";
import { matchKitColors } from "../match3d/kitColors";
import type { DeskPlayer } from "../match3d/tacticalDeskModel";
import { clubOf, nextFixture } from "../home/inbox";
import {
  defaultTemplates,
  mapPositions,
  mentalities,
  planKey,
  quickPlans,
  rosterOf,
  startersOf,
  templateFor,
  toPercent,
} from "./tacticsModel";
import "../match3d/tacticalDesk.css";
import "./tacticsDesk.css";

type Catalog = {
  formations: {
    id: string;
    label: string;
    category: string;
    positions: { x: number; z: number }[];
  }[];
  plans: { id: string; name: string; formationId: string }[];
};

const unavailable = (p: Player) => p.injuryDays > 0 || p.suspensionMatches > 0;
const status = (p: Player) =>
  p.injuryDays ? "Lesão" : p.suspensionMatches ? "Susp." : `${p.fitness}%`;

// Pre-match tactics: the same desk used during the match (plan, field, squad, changes and
// quick tactics), fed by the saved career instead of a live broadcast.
export default function TacticsDesk({
  c,
  busy: parentBusy,
}: {
  c: Career;
  busy: boolean;
}) {
  const g = c.state;
  const qc = useQueryClient();
  const club = clubOf(c, g.clubId)!;
  const next = nextFixture(c);
  const opponent = next
    ? clubOf(c, next.home === g.clubId ? next.away : next.home)
    : undefined;
  const serverPlan = g.tacticalPlan ?? defaultTacticalPlan;
  const serverKey = planKey(serverPlan, g.formation, g.mentality);

  const [formation, setFormation] = useState(g.formation);
  const [mentality, setMentality] = useState(g.mentality);
  const [plan, setPlan] = useState<TacticalPlan>(serverPlan);
  const [templateId, setTemplateId] = useState(defaultTemplates[g.formation] ?? "");
  const [list, setList] = useState<"starters" | "bench">("starters");
  const [positionFilter, setPositionFilter] = useState("");
  const [order, setOrder] = useState("Escalação");
  const [category, setCategory] = useState("");
  const [search, setSearch] = useState("");
  const [outgoing, setOutgoing] = useState(-1);
  const [incoming, setIncoming] = useState(-1);
  const [planName, setPlanName] = useState("");
  const [notice, setNotice] = useState("");
  const [focusField, setFocusField] = useState(false);
  const [mobilePanel, setMobilePanel] = useState("Campo");

  // The server is the source of truth: when a saved plan or preset changes it, adopt it.
  useEffect(() => {
    setPlan(serverPlan);
    setFormation(g.formation);
    setMentality(g.mentality);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [serverKey]);
  const dirty = planKey(plan, formation, mentality) !== serverKey;

  const catalog = useQuery({
    queryKey: ["tactical-catalog", c.id],
    queryFn: () => api<Catalog>(`/careers/${c.id}/sports/tactical-catalog`),
  });
  // The career stores positions, not the catalog id: recognise the layout they came from.
  useEffect(() => {
    if (!catalog.data) return;
    setTemplateId(
      templateFor(
        serverPlan.positions,
        catalog.data.formations,
        startersOf(g.squad),
        defaultTemplates[g.formation] ?? "",
      ),
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [serverKey, catalog.data]);
  const training = useQuery({
    queryKey: ["training", c.id, c.revision],
    queryFn: () => api<TrainingData>(`/careers/${c.id}/training/`),
  });
  const attributes = useMemo(
    () =>
      new Map(
        (training.data?.players ?? []).map((x) => [
          x.player.id,
          x.development.attributes,
        ]),
      ),
    [training.data],
  );

  const starters = useMemo(() => startersOf(g.squad), [g.squad]);
  const roster = useMemo(() => rosterOf(g.squad, g.benchIds), [g.squad, g.benchIds]);
  const bench = useMemo(
    () => g.squad.filter((p) => !p.starter).sort((a, b) => b.rating - a.rating),
    [g.squad],
  );
  const matchPlayers: MatchPlayer[] = roster.map((p) => ({
    id: p.id,
    name: p.name,
    position: p.position,
    rating: p.rating,
    fitness: p.fitness,
    skills: attributes.get(p.id),
  }));
  const number = (id: number) => roster.findIndex((p) => p.id === id) + 1 || undefined;
  const kits = matchKitColors(club.color, opponent?.color ?? "#ffffff");
  const kit = (position?: string) =>
    position === "GOL" ? kits.keepers[0] : kits.outfield[0];

  const template = catalog.data?.formations.find((f) => f.id === templateId);
  const reference = template ? mapPositions(template.positions, starters) : undefined;
  const deskPlayers: DeskPlayer[] = starters.map((p) => {
    const where = toPercent(
      reference?.[p.id] ?? plan.positions?.[p.id] ?? { x: 0, z: 0 },
    );
    return {
      pose: {
        id: p.id,
        team: 0,
        x: 0,
        z: 0,
        yaw: 0,
        action: "idle",
        active: true,
        name: p.name,
        position: p.position,
        fitness: p.fitness,
      },
      player: matchPlayers.find((m) => m.id === p.id),
      yellow: p.yellowCards,
      red: false,
      injured: p.injuryDays > 0,
      x: where.x,
      y: where.y,
    };
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["career"] });
  };
  const save = useMutation({
    mutationFn: (override?: { plan: TacticalPlan; mentality: string }) =>
      api(`/careers/${c.id}/tactical-plan`, {
        revision: c.revision,
        plan: override?.plan ?? plan,
        formation,
        mentality: override?.mentality ?? mentality,
      }),
    onSuccess: () => {
      setNotice("Plano de jogo salvo para a próxima partida.");
      refresh();
    },
  });
  const applySaved = useMutation({
    mutationFn: (saved: Catalog["plans"][number]) =>
      api(`/careers/${c.id}/sports/tactical-presets/apply`, {
        revision: c.revision,
        id: saved.id,
      }),
    onSuccess: (_data, saved) => {
      setTemplateId(saved.formationId);
      setNotice(`Plano “${saved.name}” aplicado à próxima partida.`);
      refresh();
    },
  });
  const saveNew = useMutation({
    mutationFn: () =>
      api(`/careers/${c.id}/sports/tactical-presets`, {
        revision: c.revision,
        id: "",
        name: planName,
        formationId: templateId,
        mentality,
        plan,
      }),
    onSuccess: () => {
      setPlanName("");
      setNotice("Plano salvo na carreira.");
      refresh();
      void qc.invalidateQueries({ queryKey: ["tactical-catalog", c.id] });
    },
  });
  const swap = useMutation({
    mutationFn: async ({ out, inn }: { out: number; inn: number }) => {
      // Unsaved plan edits are saved first so the incoming player inherits the slot correctly.
      let revision = c.revision;
      if (dirty) {
        const saved = await api<{ revision: number }>(
          `/careers/${c.id}/tactical-plan`,
          { revision, plan, formation, mentality },
        );
        revision = saved.revision;
      }
      await api(`/careers/${c.id}/actions`, {
        type: "swap",
        value: String(inn),
        extra: "",
        playerId: out,
        revision,
      });
    },
    onSuccess: () => {
      setOutgoing(-1);
      setIncoming(-1);
      setList("starters");
      setNotice("Escalação atualizada.");
      refresh();
    },
  });
  const busy =
    parentBusy ||
    save.isPending ||
    applySaved.isPending ||
    saveNew.isPending ||
    swap.isPending;
  const error =
    save.error?.message ??
    applySaved.error?.message ??
    saveNew.error?.message ??
    swap.error?.message ??
    "";

  function selectTemplate(id: string) {
    if (!id) return;
    setTemplateId(id);
    const base = Object.entries(defaultTemplates).find(([, value]) => value === id);
    if (base) setFormation(base[0]);
    const chosen = catalog.data?.formations.find((f) => f.id === id);
    if (chosen) setPlan({ ...plan, positions: mapPositions(chosen.positions, starters) });
  }
  const setOption = <K extends keyof TacticalPlan>(key: K, value: TacticalPlan[K]) =>
    setPlan({ ...plan, [key]: value });

  const outPlayer = starters.find((p) => p.id === outgoing);
  const benchChoices = bench.filter(
    (p) => !outPlayer || (outPlayer.position === "GOL") === (p.position === "GOL"),
  );
  const inPlayer = bench.find((p) => p.id === incoming);
  const canSwap = !!outPlayer && !!inPlayer && !unavailable(inPlayer) && !busy;
  const listed = (list === "starters" ? starters : benchChoices)
    .filter((p) => list === "starters" || !positionFilter || p.position === positionFilter)
    .sort((a, b) =>
      list === "starters"
        ? 0
        : order === "Condição"
          ? b.fitness - a.fitness
          : order === "Overall"
            ? b.rating - a.rating
            : 0,
    );
  const formationOptions = (catalog.data?.formations ?? []).filter(
    (f) =>
      f.id === templateId ||
      ((!category || f.category === category) &&
        f.label.toLowerCase().includes(search.toLowerCase())),
  );
  const averageFitness = starters.length
    ? Math.round(starters.reduce((sum, p) => sum + p.fitness, 0) / starters.length)
    : 0;
  const ready = g.squad.filter((p) => !unavailable(p)).length;

  const controls = (
    <div className="desk-tactic-controls">
      <select
        aria-label="Formação"
        value={templateId}
        disabled={busy || !catalog.data}
        onChange={(e) => selectTemplate(e.target.value)}
      >
        {templateId === "" && <option value="">Personalizada</option>}
        {formationOptions.map((f) => (
          <option key={f.id} value={f.id}>
            {f.label}
          </option>
        ))}
      </select>
      {catalog.data && (
        <details>
          <summary>Buscar e filtrar formações</summary>
          <input
            aria-label="Buscar formação"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Ex.: 4-2-3-1"
          />
          <select
            aria-label="Categoria da formação"
            value={category}
            onChange={(e) => setCategory(e.target.value)}
          >
            <option value="">Todas</option>
            {[...new Set(catalog.data.formations.map((f) => f.category))].map((name) => (
              <option key={name}>{name}</option>
            ))}
          </select>
        </details>
      )}
      <label className="desk-mentality">
        Mentalidade
        <select
          aria-label="Mentalidade"
          value={mentality}
          disabled={busy}
          onChange={(e) => setMentality(e.target.value)}
        >
          {mentalities.map((value) => (
            <option key={value}>{value}</option>
          ))}
        </select>
      </label>
      <h4>Estilo de Jogo</h4>
      <div className="desk-style">
        <button
          aria-pressed={plan.style === "Posse"}
          onClick={() => setOption("style", plan.style === "Posse" ? "Equilibrado" : "Posse")}
        >
          <Target size={15} />
          Posse de Bola
        </button>
        <button
          aria-pressed={plan.buildShort}
          onClick={() => setOption("buildShort", !plan.buildShort)}
        >
          <ArrowLeftRight size={15} />
          Passes Curtos
        </button>
        <button
          aria-pressed={plan.width === "Aberta"}
          onClick={() => setOption("width", plan.width === "Aberta" ? "Normal" : "Aberta")}
        >
          <MoveHorizontal size={15} />
          Amplitude
        </button>
        <button
          aria-pressed={plan.pressure === "Alta"}
          onClick={() => setOption("pressure", plan.pressure === "Alta" ? "Média" : "Alta")}
        >
          <Crosshair size={15} />
          Pressão Alta
        </button>
      </div>
      <h4>Instruções da Equipe</h4>
      {(
        [
          ["buildShort", "Sair Jogando Curto"],
          ["overlap", "Ultrapassar nas Pontas"],
          ["shootMore", "Chutar Mais"],
          ["avoidFouls", "Evitar Faltas"],
          ["offsideTrap", "Linha de Impedimento"],
        ] as const
      ).map(([key, label]) => (
        <label className="desk-check" key={key}>
          <input
            type="checkbox"
            checked={plan[key]}
            onChange={(e) => setOption(key, e.target.checked)}
          />
          {label}
        </label>
      ))}
      <details>
        <summary>Mais instruções</summary>
        <label>
          Ritmo
          <select value={plan.tempo ?? "Normal"} onChange={(e) => setOption("tempo", e.target.value)}>
            <option>Baixo</option>
            <option>Normal</option>
            <option>Alto</option>
          </select>
        </label>
        <label>
          Ao perder a bola
          <select
            value={plan.defensiveTransition ?? ""}
            onChange={(e) => setOption("defensiveTransition", e.target.value || undefined)}
          >
            <option value="">Conforme a pressão</option>
            <option>Recompor</option>
            <option>Pressionar após perda</option>
          </select>
        </label>
        {(
          [
            ["style", "Estilo", ["Equilibrado", "Posse", "Contra-ataque", "Direto"]],
            ["width", "Amplitude", ["Fechada", "Normal", "Aberta"]],
            ["pressure", "Pressão", ["Baixa", "Média", "Alta"]],
            ["attackSide", "Corredor preferencial", ["Ambos", "Esquerda", "Direita"]],
          ] as const
        ).map(([key, label, options]) => (
          <label key={key}>
            {label}
            <select value={plan[key]} onChange={(e) => setOption(key, e.target.value)}>
              {options.map((option) => (
                <option key={option}>{option}</option>
              ))}
            </select>
          </label>
        ))}
      </details>
      <details>
        <summary>Planos salvos</summary>
        {catalog.data?.plans.length === 0 && <p>Nenhum plano salvo ainda.</p>}
        {catalog.data?.plans.map((saved) => (
          <button
            className="desk-saved"
            key={saved.id}
            disabled={busy}
            onClick={() => applySaved.mutate(saved)}
          >
            {saved.name}
          </button>
        ))}
        <label>
          Nome do novo plano
          <input maxLength={60} value={planName} onChange={(e) => setPlanName(e.target.value)} />
        </label>
        <Button
          disabled={busy || !planName.trim() || !template}
          title={template ? undefined : "Escolha uma formação do catálogo para salvar o plano"}
          onClick={() => saveNew.mutate()}
        >
          Salvar novo plano
        </Button>
      </details>
      <Button disabled={busy || !dirty} onClick={() => save.mutate(undefined)}>
        {dirty ? "Salvar plano de jogo" : "Plano de jogo salvo"}
      </Button>
    </div>
  );

  const roster_ = (
    <>
      <h3>Elenco</h3>
      <div className="desk-tabs">
        <button
          aria-pressed={list === "starters"}
          onClick={() => {
            setList("starters");
            setPositionFilter("");
          }}
        >
          Titulares
        </button>
        <button
          aria-pressed={list === "bench"}
          onClick={() => {
            setList("bench");
            setPositionFilter("");
          }}
        >
          Banco de Reservas
        </button>
      </div>
      {list === "bench" && (
        <div className="desk-filters">
          <select
            aria-label="Filtrar banco por posição"
            value={positionFilter}
            onChange={(e) => setPositionFilter(e.target.value)}
          >
            <option value="">Todas as posições</option>
            {[...new Set(benchChoices.map((p) => p.position))].map((p) => (
              <option key={p}>{p}</option>
            ))}
          </select>
          <select
            aria-label="Ordenar reservas"
            value={order}
            onChange={(e) => setOrder(e.target.value)}
          >
            {["Escalação", "Condição", "Overall"].map((o) => (
              <option key={o}>{o}</option>
            ))}
          </select>
        </div>
      )}
      <div className="desk-roster">
        <div className="desk-roster-head">
          <span>Jogador</span>
          <span>Pos.</span>
          <span>Condição</span>
          <span>Geral</span>
        </div>
        {listed.map((p) => (
          <button
            className="desk-roster-row"
            key={p.id}
            disabled={busy || (list === "bench" && unavailable(p))}
            aria-pressed={(list === "starters" ? outgoing : incoming) === p.id}
            onClick={() => {
              if (list === "bench") setIncoming(p.id);
              else {
                setOutgoing(p.id);
                setIncoming(-1);
                setList("bench");
                setPositionFilter("");
              }
            }}
          >
            <span>
              <Kit color={kit(p.position)} number={number(p.id)} />
              <strong>{p.name}</strong>
            </span>
            <span className="desk-position" data-position={p.position}>
              {p.position}
            </span>
            <span className="desk-condition">
              <i style={{ borderColor: !unavailable(p) && p.fitness >= 85 ? "#46ed61" : "#efbd31" }} />
              {status(p)}
            </span>
            <span>{p.rating}</span>
          </button>
        ))}
        {listed.length === 0 && <p>Nenhum jogador disponível para esta seleção.</p>}
      </div>
    </>
  );

  const changes = (
    <>
      <section className="desk-panel desk-changes">
        <h3>Trocas na escalação</h3>
        <label>
          <span className="desk-out">
            <ArrowDown size={17} /> Sai
          </span>
          <select
            aria-label="Titular que sai"
            value={outgoing}
            disabled={busy}
            onChange={(e) => {
              setOutgoing(Number(e.target.value));
              setIncoming(-1);
            }}
          >
            <option value={-1}>Escolha quem sai</option>
            {starters.map((p) => (
              <option key={p.id} value={p.id}>
                {number(p.id)} · {p.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span className="desk-in">
            <ArrowUp size={17} /> Entra
          </span>
          <select
            aria-label="Reserva que entra"
            value={incoming}
            disabled={busy}
            onChange={(e) => setIncoming(Number(e.target.value))}
          >
            <option value={-1}>Escolha quem entra</option>
            {benchChoices
              .filter((p) => !unavailable(p))
              .map((p) => (
                <option key={p.id} value={p.id}>
                  {number(p.id)} · {p.name}
                </option>
              ))}
          </select>
        </label>
        <Button
          disabled={!canSwap}
          onClick={() => swap.mutate({ out: outgoing, inn: incoming })}
        >
          <ArrowLeftRight size={16} /> Trocar jogadores
        </Button>
        {outPlayer && inPlayer && (
          <details open>
            <summary>Comparar jogadores</summary>
            <div className="coach-comparison">
              <div className="coach-comparison-row">
                <strong>Atributo</strong>
                <strong>{outPlayer.name}</strong>
                <strong>{inPlayer.name}</strong>
              </div>
              <div className="coach-comparison-row">
                <span>Condição física</span>
                <span>{outPlayer.fitness}%</span>
                <span>{inPlayer.fitness}%</span>
              </div>
              <div className="coach-comparison-row">
                <span>Overall</span>
                <span>{outPlayer.rating}</span>
                <span>{inPlayer.rating}</span>
              </div>
              {Object.keys(attributes.get(outPlayer.id) ?? {}).map((key) => (
                <div className="coach-comparison-row" key={key}>
                  <span>{key}</span>
                  <span>{attributes.get(outPlayer.id)?.[key] ?? "—"}</span>
                  <span>{attributes.get(inPlayer.id)?.[key] ?? "—"}</span>
                </div>
              ))}
            </div>
          </details>
        )}
      </section>
      <section className="desk-panel desk-confirm">
        <h3>Situação do elenco</h3>
        <p>
          <b>{starters.length}/11</b> titulares · <b>{ready}</b> de {g.squad.length} jogadores
          disponíveis
        </p>
        <p>
          Condição média dos titulares: <b>{averageFitness}%</b>
        </p>
        <p className="muted">
          Trocas durante o jogo e reposição automática de lesionados ficam logo abaixo da
          prancheta.
        </p>
      </section>
    </>
  );

  const mobilePanels = ["Plano", "Campo", "Jogadores", "Trocas"];
  return (
    <div className="tactical-desk desk-embedded">
      <header className="desk-plan-head">
        <div className="desk-plan-match">
          <Crest club={club} />
          <strong>{club.name}</strong>
          {opponent && (
            <>
              <span>×</span>
              <Crest club={opponent} />
              <strong>{opponent.name}</strong>
            </>
          )}
        </div>
        <small>
          {next
            ? `Próximo jogo · rodada ${next.round}${next.competitionName ? " · " + next.competitionName : ""}`
            : "Sem jogos pendentes nesta temporada"}
        </small>
        <span className={"desk-plan-state" + (dirty ? " is-dirty" : "")} role="status">
          {dirty ? "Alterações não salvas" : "Plano salvo"}
        </span>
      </header>
      <div className="desk-tabs desk-mobile-tabs" aria-label="Painéis táticos no celular">
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
      <div
        className={"desk-layout" + (focusField ? " desk-expanded" : "")}
        data-mobile={mobilePanel}
      >
        <aside className="desk-panel desk-tactics">
          <h3>Plano Tático</h3>
          {controls}
        </aside>
        <section className="desk-panel desk-field" aria-label="Campo tático">
          {catalog.data ? (
            <TacticalPitch
              players={deskPlayers}
              frames={[]}
              teamIndex={0}
              squad={matchPlayers}
              color={kit()}
              keeper={kit("GOL")}
              plan={plan}
              referencePositions={reference}
              setPlan={setPlan}
              editable
              disabled={busy}
              liveOverlays={false}
            />
          ) : (
            <p>{catalog.error ? catalog.error.message : "Carregando formações…"}</p>
          )}
        </section>
        <section className="desk-panel desk-players" aria-label="Lista de jogadores">
          {roster_}
        </section>
        <aside className="desk-exchanges" aria-label="Trocas e situação do elenco">
          {changes}
        </aside>
      </div>
      <div className="desk-feed">
        <ArrowLeftRight size={18} aria-hidden />
        <b>—</b>
        <span role={error ? "alert" : "status"}>
          {error ||
            notice ||
            "Monte o plano de jogo: formação, mentalidade, estilo e posições valem para a próxima partida."}
        </span>
      </div>
      <footer className="desk-bottom">
        <div className="desk-quick">
          <strong>Táticas Rápidas</strong>
          {quickPlans(plan, mentality).map((q) => (
            <Button
              key={q.label}
              variant="outline"
              disabled={busy}
              onClick={() => {
                setPlan(q.plan);
                setMentality(q.mentality);
                save.mutate({ plan: q.plan, mentality: q.mentality });
              }}
            >
              <Zap size={15} />
              {q.label}
            </Button>
          ))}
        </div>
        <div className="desk-visual">
          <button aria-pressed={focusField} onClick={() => setFocusField(!focusField)}>
            <Maximize2 size={18} />
            {focusField ? "Ver painéis" : "Expandir campo"}
          </button>
        </div>
      </footer>
    </div>
  );
}
