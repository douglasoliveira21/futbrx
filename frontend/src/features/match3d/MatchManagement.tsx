import { useEffect, useRef, useState } from "react";
import TacticalDesk from "./TacticalDesk";
import {
  api,
  type BroadcastPacket,
  type Club,
  type TacticalPlan,
} from "../../services/api";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowDown,
  ArrowUp,
  Check,
  Crosshair,
  ArrowLeftRight,
  MoveHorizontal,
  Plus,
  Target,
  Zap,
} from "lucide-react";
import TacticalPitch, { Kit } from "./TacticalPitch";
import { tacticalDeskModel } from "./tacticalDeskModel";
import { matchKitColors } from "./kitColors";

type Catalog = {
  formations: {
    id: string;
    label: string;
    category: string;
    positions: { x: number; z: number }[];
  }[];
  plans: { id: string; name: string; formationId: string }[];
};
const mentalities = [
  "Ultradefensiva",
  "Muito defensiva",
  "Defensiva",
  "Cautelosa",
  "Equilibrada",
  "Positiva",
  "Ofensiva",
  "Muito ofensiva",
  "Ataque total",
];
const defaultTemplates: Record<string, string> = {
  "4-3-3": "433",
  "4-4-2": "442",
  "3-5-2": "352",
  "4-2-3-1": "4231-wide",
};
const roleOrder: Record<string, number> = {
  GOL: 0,
  ZAG: 1,
  LAT: 2,
  MEI: 3,
  ATA: 4,
};
import { DialogShell } from "../../components/ui/dialog";
import { Button } from "../../components/ui/button";
import { defaultTacticalPlan } from "../sports/TacticalInstructions";

export default function MatchManagement({
  packet,
  busy,
  error,
  control,
  onClose,
  home,
  away,
  careerId,
}: {
  packet: BroadcastPacket;
  busy: boolean;
  error: string;
  control: (body: object) => Promise<boolean>;
  onClose: () => void;
  home: Club;
  away: Club;
  careerId: string;
}) {
  const session = packet.session,
    setup = session.fixture.setup ?? packet.replay.setup;
  const team = session.managedTeam === setup.home.id ? setup.home : setup.away,
    index = team.id === setup.home.id ? 0 : 1;
  const events = packet.replay.events,
    commands = setup.instructions?.filter((c) => c.team === team.id) ?? [];
  const pending = commands.filter(
    (c) =>
      c.type === "substitution" &&
      !events.some(
        (e) =>
          ["substitution", "substitution_cancelled"].includes(e.type) &&
          e.team === team.id &&
          e.player === c.outPlayerId &&
          e.relatedPlayer === c.inPlayerId,
      ),
  );
  const used = events.filter(
      (e) => e.team === team.id && e.type === "substitution",
    ),
    entered = new Set([
      ...used.map((e) => e.relatedPlayer),
      ...pending.map((c) => c.inPlayerId),
    ]);
  const field = (packet.replay.frames.at(-1)?.players ?? []).filter(
    (p) =>
      p.team === index &&
      p.active !== false &&
      !pending.some((c) => c.outPlayerId === p.id),
  );
  const last = commands.filter((c) => c.type === "tactics").at(-1);
  const [formation, setFormation] = useState(last?.formation ?? team.formation),
    [mentality, setMentality] = useState(last?.mentality ?? team.mentality);
  const [outgoing, setOutgoing] = useState(-1),
    [incoming, setIncoming] = useState(-1),
    [notice, setNotice] = useState("");
  const [draft, setDraft] = useState<
    { outPlayerId: number; inPlayerId: number }[]
  >([]);
  const [sending, setSending] = useState(false);
  const advanced = ["positional-v9", "positional-v10", "positional-v11", "positional-v12", "positional-v13", "positional-v14"].includes(
    setup.engineVersion,
  );
  const [plan, setPlan] = useState(
    last?.plan ?? team.plan ?? defaultTacticalPlan,
  );
  const receivedPlan = useRef(JSON.stringify(last));
  useEffect(() => {
    const key = JSON.stringify(last);
    if (receivedPlan.current === key) return;
    receivedPlan.current = key;
    if (last?.plan) {
      setPlan(last.plan);
      setFormation(last.formation);
      setMentality(last.mentality);
    }
  }, [last]);
  const [list, setList] = useState<"field" | "bench">("field");
  const [order, setOrder] = useState("Escalação");
  const [positionFilter, setPositionFilter] = useState("");
  const [autoConfirm, setAutoConfirm] = useState(false);
  const [category, setCategory] = useState("");
  const [search, setSearch] = useState("");
  const [templateId, setTemplateId] = useState(
    defaultTemplates[formation] ?? "",
  );
  const [planName, setPlanName] = useState("");
  const queryClient = useQueryClient();
  const catalog = useQuery({
    queryKey: ["tactical-catalog", careerId],
    queryFn: () => api<Catalog>(`/careers/${careerId}/sports/tactical-catalog`),
    enabled: advanced,
  });
  const save = useMutation({
    mutationFn: async () => {
      await api(`/careers/${careerId}/live/tactical-presets`, {
        matchId: session.id,
        name: planName,
        formationId: templateId,
        mentality,
        plan,
      });
    },
    onSuccess: async () => {
      setNotice("Plano salvo na carreira.");
      setPlanName("");
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["tactical-catalog", careerId],
        }),
        queryClient.invalidateQueries({ queryKey: ["career"] }),
      ]);
    },
  });
  const roster = [...team.players, ...(team.bench ?? [])];
  const teamKits = matchKitColors(home.color, away.color);
  const kit = (position?: string) =>
    position === "GOL" ? teamKits.keepers[index] : teamKits.outfield[index];
  const number = (id: number) =>
    roster.findIndex((p) => p.id === id) + 1 || undefined;
  const old = field.find((p) => p.id === outgoing),
    bench = (team.bench ?? []).filter(
      (p) =>
        !entered.has(p.id) &&
        !draft.some((d) => d.inPlayerId === p.id) &&
        (!old || (old.position === "GOL") === (p.position === "GOL")),
    );
  const outgoingPlayer = [...team.players, ...(team.bench ?? [])].find(
      (p) => p.id === outgoing,
    ),
    incomingPlayer = (team.bench ?? []).find((p) => p.id === incoming);
  const quickPlans = [
    {
      label: "Mais ofensivo",
      mentality: "Ofensiva",
      plan: { ...plan, overlap: true },
    },
    {
      label: "Equilibrado",
      mentality: "Equilibrada",
      plan: { ...plan, pressure: "Média", width: "Normal" },
    },
    {
      label: "Mais defensivo",
      mentality: "Defensiva",
      plan: { ...plan, pressure: "Baixa", width: "Fechada", overlap: false },
    },
    { label: "Pressão alta", mentality, plan: { ...plan, pressure: "Alta" } },
    {
      label: "Contra-ataque",
      mentality,
      plan: { ...plan, style: "Contra-ataque", buildShort: false },
    },
    {
      label: "Manter posse",
      mentality,
      plan: { ...plan, style: "Posse", buildShort: true },
    },
  ];
  const halftime = !!session.halfTimeAwaiting;
  const name = (id: number) =>
    [...team.players, ...(team.bench ?? [])].find((p) => p.id === id)?.name ??
    String(id);
  const latestEngine = ["positional-v10","positional-v11","positional-v12","positional-v13","positional-v14"].includes(setup.engineVersion);
  const resume = async () => {
    if (busy || sending || draft.length > 0) return;
    if (halftime && !(await control({ type: "second_half" }))) return;
    if (session.stoppagePaused && !(await control({ type: "resume_stoppage" })))
      return;
    onClose();
  };
  const view = tacticalDeskModel(packet);
  const managedPlayers = view.players;
  const liveFitness = (id: number) =>
    managedPlayers.find((player) => player.pose.id === id)?.pose.fitness;
  const fitness = (id: number, initial?: number) => liveFitness(id) ?? initial;
  const pointCatalog = catalog.data?.formations;
  function mapPositions(points: { x: number; z: number }[]) {
    const active = [...managedPlayers].sort(
      (a, b) =>
        (roleOrder[a.pose.position ?? ""] ?? 5) -
          (roleOrder[b.pose.position ?? ""] ?? 5) || a.pose.id - b.pose.id,
    );
    return Object.fromEntries(
      active.flatMap(({ pose }, i) =>
        points[i] ? [[pose.id, points[i]]] : [],
      ),
    );
  }
  const activeTemplate = pointCatalog?.find((f) => f.id === templateId);
  const referencePositions = activeTemplate
    ? mapPositions(activeTemplate.positions)
    : undefined;
  function selectTemplate(id: string) {
    setTemplateId(id);
    const base = Object.entries(defaultTemplates).find(
      ([, value]) => value === id,
    );
    if (base) setFormation(base[0]);
    const template = pointCatalog?.find((f) => f.id === id);
    if (!template) {
      if (!advanced && base) setPlan({ ...plan, positions: undefined });
      return;
    }
    setPlan({ ...plan, positions: mapPositions(template.positions) });
  }
  const setOption = <K extends keyof TacticalPlan>(
    key: K,
    value: TacticalPlan[K],
  ) => setPlan({ ...plan, [key]: value });
  const tacticBusy = busy || sending || save.isPending;
  async function apply(next = plan, nextMentality = mentality) {
    if (
      await control({
        type: "tactics",
        formation,
        mentality: nextMentality,
        ...(advanced ? { plan: next } : {}),
      })
    ) {
      setMentality(nextMentality);
      setPlan(next);
      setNotice("Estratégia enviada para os próximos lances.");
    }
  }

  const tacticsPanel = (
    <div className="desk-tactic-controls">
      <select
        aria-label="Formação"
        value={templateId}
        disabled={tacticBusy}
        onChange={(e) => selectTemplate(e.target.value)}
      >
        {(
          pointCatalog ??
          Object.entries(defaultTemplates)
            .filter(([label]) => advanced || label !== "4-2-3-1")
            .map(([label, id]) => ({ id, label, category: "" }))
        )
          .filter(
            (f) =>
              f.id === templateId ||
              ((!category || f.category === category) &&
                f.label.toLowerCase().includes(search.toLowerCase())),
          )
          .map((f) => (
            <option key={f.id} value={f.id}>
              {f.label}
            </option>
          ))}
      </select>
      {pointCatalog && (
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
            {[...new Set(pointCatalog.map((f) => f.category))].map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
        </details>
      )}
      <label className="desk-mentality">
        Mentalidade
        <select
          aria-label="Mentalidade durante a partida"
          value={mentality}
          disabled={tacticBusy}
          onChange={(e) => setMentality(e.target.value)}
        >
          {(latestEngine
            ? mentalities
            : ["Defensiva", "Equilibrada", "Ofensiva"]
          ).map((value) => (
            <option key={value}>{value}</option>
          ))}
        </select>
      </label>
      {advanced && (
        <>
          <h4>Estilo de Jogo</h4>
          <div className="desk-style">
            <button
              aria-pressed={plan.style === "Posse"}
              onClick={() =>
                setOption(
                  "style",
                  plan.style === "Posse" ? "Equilibrado" : "Posse",
                )
              }
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
              onClick={() =>
                setOption(
                  "width",
                  plan.width === "Aberta" ? "Normal" : "Aberta",
                )
              }
            >
              <MoveHorizontal size={15} />
              Amplitude
            </button>
            <button
              aria-pressed={plan.pressure === "Alta"}
              onClick={() =>
                setOption(
                  "pressure",
                  plan.pressure === "Alta" ? "Média" : "Alta",
                )
              }
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
            {latestEngine && (
              <>
                <label>
                  Ritmo
                  <select
                    value={plan.tempo ?? "Normal"}
                    onChange={(e) => setOption("tempo", e.target.value)}
                  >
                    <option>Baixo</option>
                    <option>Normal</option>
                    <option>Alto</option>
                  </select>
                </label>
                <label>
                  Ao perder a bola
                  <select
                    value={plan.defensiveTransition ?? ""}
                    onChange={(e) =>
                      setOption(
                        "defensiveTransition",
                        e.target.value || undefined,
                      )
                    }
                  >
                    <option value="">Conforme a pressão</option>
                    <option>Recompor</option>
                    <option>Pressionar após perda</option>
                  </select>
                </label>
              </>
            )}
            {(
              [
                [
                  "style",
                  "Estilo",
                  ["Equilibrado", "Posse", "Contra-ataque", "Direto"],
                ],
                ["width", "Amplitude", ["Fechada", "Normal", "Aberta"]],
                ["pressure", "Pressão", ["Baixa", "Média", "Alta"]],
                [
                  "attackSide",
                  "Corredor preferencial",
                  ["Ambos", "Esquerda", "Direita"],
                ],
              ] as const
            ).map(([key, label, options]) => (
              <label key={key}>
                {label}
                <select
                  value={plan[key]}
                  onChange={(e) => setOption(key, e.target.value)}
                >
                  {options.map((option) => (
                    <option key={option}>{option}</option>
                  ))}
                </select>
              </label>
            ))}
          </details>
          <details>
            <summary>Planos salvos</summary>
            {catalog.error && <p role="alert">{catalog.error.message}</p>}
            {catalog.data?.plans.map((saved) => (
              <button
                className="desk-saved"
                key={saved.id}
                disabled={tacticBusy}
                onClick={async () => {
                  if (await control({ type: "preset", presetId: saved.id })) {
                    setTemplateId(saved.formationId);
                    setNotice(`Plano “${saved.name}” enviado para a partida.`);
                  }
                }}
              >
                {saved.name}
              </button>
            ))}
            <label>
              Nome do novo plano
              <input
                maxLength={60}
                value={planName}
                onChange={(e) => setPlanName(e.target.value)}
              />
            </label>
            <Button
              disabled={
                tacticBusy ||
                !planName.trim() ||
                !pointCatalog?.some((f) => f.id === templateId)
              }
              onClick={() => save.mutate()}
            >
              Salvar novo plano
            </Button>
            {save.error && <p role="alert">{save.error.message}</p>}
          </details>
        </>
      )}
      <Button disabled={tacticBusy} onClick={() => void apply()}>
        Aplicar estratégia
      </Button>
    </div>
  );

  const fieldOptions = field.filter(
    (p) => !draft.some((d) => d.outPlayerId === p.id),
  );
  const listed = (
    list === "field"
      ? fieldOptions.map((p) => ({
          id: p.id,
          name: p.name,
          position: p.position,
          player: roster.find((s) => s.id === p.id),
        }))
      : bench.map((p) => ({
          id: p.id,
          name: p.name,
          position: p.position,
          player: p,
        }))
  )
    .filter(
      (p) =>
        !positionFilter ||
        (p.position ?? p.player?.position) === positionFilter,
    )
    .sort((a, b) =>
      order === "Condição"
        ? (fitness(b.id, b.player?.fitness) ?? 0) -
          (fitness(a.id, a.player?.fitness) ?? 0)
        : order === "Overall"
          ? (b.player?.rating ?? 0) - (a.player?.rating ?? 0)
          : 0,
    );
  const playersPanel = (
    <>
      <h3>Substituições</h3>
      <div className="desk-tabs">
        <button
          aria-pressed={list === "field"}
          onClick={() => {
            setList("field");
            setPositionFilter("");
          }}
        >
          Substituições
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
            {[...new Set(bench.map((p) => p.position))].map((p) => (
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
          <span>Nota</span>
        </div>
        {listed.map((p) => (
          <button
            className="desk-roster-row"
            key={p.id}
            disabled={busy || sending}
            aria-pressed={(list === "field" ? outgoing : incoming) === p.id}
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
              <strong>{p.name ?? p.player?.name}</strong>
            </span>
            <span className="desk-position" data-position={p.position}>
              {p.position}
            </span>
            <span
              className="desk-condition"
              title={
                liveFitness(p.id) !== undefined
                  ? "Condição em jogo"
                  : "Condição inicial"
              }
            >
              {p.player ? (
                <>
                  <i
                    style={{
                      borderColor:
                        (fitness(p.id, p.player.fitness) ?? 0) >= 85
                          ? "#46ed61"
                          : "#efbd31",
                    }}
                  />
                  {fitness(p.id, p.player.fitness)}%
                </>
              ) : (
                "—"
              )}
            </span>
            <span title="Nota calculada a partir dos eventos da partida">
              {managedPlayers
                .find((player) => player.pose.id === p.id)
                ?.matchRating?.toFixed(1) ?? "—"}
            </span>
          </button>
        ))}
        {listed.length === 0 && (
          <p>Nenhum jogador disponível para esta seleção.</p>
        )}
      </div>
    </>
  );

  async function submit(changes: typeof draft) {
    setSending(true);
    try {
      if (await control({ type: "substitutions", substitutions: changes })) {
        setDraft([]);
        setOutgoing(-1);
        setIncoming(-1);
        setNotice("Trocas confirmadas e enviadas ao motor da partida.");
      }
    } finally {
      setSending(false);
    }
  }
  const canAdd =
    !!old &&
    bench.some((p) => p.id === incoming) &&
    used.length + pending.length + draft.length < 5;
  const substitutionPanel = (
    <>
      <section className="desk-panel desk-changes">
        <h3>Substituições ({used.length + pending.length}/5)</h3>
        <label>
          <span className="desk-out">
            <ArrowDown size={17} /> Sai
          </span>
          <select
            aria-label="Jogador que sai"
            value={outgoing}
            disabled={tacticBusy}
            onChange={(e) => {
              setOutgoing(Number(e.target.value));
              setIncoming(-1);
            }}
          >
            <option value={-1}>Escolha quem sai</option>
            {fieldOptions.map((p) => (
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
            disabled={tacticBusy}
            onChange={(e) => setIncoming(Number(e.target.value))}
          >
            <option value={-1}>Escolha quem entra</option>
            {bench.map((p) => (
              <option key={p.id} value={p.id}>
                {number(p.id)} · {p.name}
              </option>
            ))}
          </select>
        </label>
        <Button
          disabled={
            tacticBusy || used.length + pending.length + draft.length >= 5
          }
          onClick={() => {
            if (!canAdd) {
              setList(old ? "bench" : "field");
              setPositionFilter("");
              setNotice(
                old
                  ? "Escolha um reserva para entrar."
                  : "Escolha um jogador para sair.",
              );
              return;
            }
            const next = [
              ...draft,
              { outPlayerId: outgoing, inPlayerId: incoming },
            ];
            if (autoConfirm) {
              void submit(next);
              return;
            }
            setDraft(next);
            setOutgoing(-1);
            setIncoming(-1);
            setList("field");
            setNotice(
              "Troca preparada. Confirme a lista para enviá-la à partida.",
            );
          }}
        >
          <Plus size={17} /> Adicionar Substituição
        </Button>
        {draft.map((d) => (
          <article key={d.outPlayerId} className="desk-change">
            <div>
              <span className="desk-out">↓ {name(d.outPlayerId)}</span>
              <span className="desk-in">↑ {name(d.inPlayerId)}</span>
            </div>
            <button
              disabled={tacticBusy}
              aria-label={`Remover troca de ${name(d.outPlayerId)}`}
              onClick={() =>
                setDraft((items) => items.filter((item) => item !== d))
              }
            >
              ×
            </button>
          </article>
        ))}
        {pending.map((c) => (
          <article key={c.outPlayerId} className="desk-change">
            <span>
              {name(c.inPlayerId)} por {name(c.outPlayerId)} · na fila
            </span>
            <button
              disabled={tacticBusy}
              aria-label={`Cancelar entrada de ${name(c.inPlayerId)}`}
              onClick={async () => {
                if (
                  await control({
                    type: "cancel_substitution",
                    outPlayerId: c.outPlayerId,
                    inPlayerId: c.inPlayerId,
                  })
                )
                  setNotice("Troca pendente cancelada.");
              }}
            >
              ×
            </button>
          </article>
        ))}
        {outgoingPlayer && incomingPlayer && (
          <details>
            <summary>Comparar jogadores</summary>
            <div className="coach-comparison">
              <div className="coach-comparison-row">
                <strong>Atributo</strong>
                <strong>{outgoingPlayer.name}</strong>
                <strong>{incomingPlayer.name}</strong>
              </div>
              <div className="coach-comparison-row">
                <span>Condição física</span>
                <span>
                  {fitness(outgoingPlayer.id, outgoingPlayer.fitness)}%
                </span>
                <span>{incomingPlayer.fitness}%</span>
              </div>
              <div className="coach-comparison-row">
                <span>Overall</span>
                <span>{outgoingPlayer.rating}</span>
                <span>{incomingPlayer.rating}</span>
              </div>
              {Object.keys(outgoingPlayer.skills ?? {}).map((key) => (
                <div className="coach-comparison-row" key={key}>
                  <span>{key}</span>
                  <span>{outgoingPlayer.skills?.[key] ?? "—"}</span>
                  <span>{incomingPlayer.skills?.[key] ?? "—"}</span>
                </div>
              ))}
            </div>
          </details>
        )}
      </section>
      <section className="desk-panel desk-confirm">
        <h3>Confirmação</h3>
        {!halftime && !session.stoppagePaused && (
          <label className="desk-check">
            <input
              type="checkbox"
              checked={session.pauseAfterSecond != null}
              disabled={tacticBusy}
              onChange={(e) =>
                void control({
                  type: e.target.checked ? "arm_stoppage" : "cancel_stoppage",
                })
              }
            />
            Pausar na próxima bola parada
          </label>
        )}
        <p>
          {halftime
            ? "Trocas executadas na volta do intervalo."
            : "Trocas aplicadas pelo motor quando a jogada permite."}
        </p>
        <label className="desk-check">
          <input
            type="checkbox"
            checked={autoConfirm}
            onChange={(e) => setAutoConfirm(e.target.checked)}
          />
          Confirmar automaticamente
        </label>
        <div>
          <Button
            variant="outline"
            disabled={tacticBusy || (!draft.length && outgoing < 0 && incoming < 0)}
            onClick={() => {
              setDraft([]);
              setOutgoing(-1);
              setIncoming(-1);
              setList("field");
              setPositionFilter("");
              setNotice("Seleção de trocas cancelada.");
            }}
          >
            Cancelar
          </Button>
          <Button
            disabled={tacticBusy || !draft.length}
            onClick={() => void submit(draft)}
          >
            <Check size={16} />
            Confirmar Trocas
          </Button>
        </div>
      </section>
    </>
  );

  const quickPanel = advanced && (
    <div className="desk-quick">
      <strong>Táticas Rápidas</strong>
      {quickPlans.map((q) => (
        <Button
          key={q.label}
          variant="outline"
          disabled={tacticBusy}
          onClick={() => void apply(q.plan, q.mentality)}
        >
          <Zap size={15} />
          {q.label}
        </Button>
      ))}
    </div>
  );
  const locked = tacticBusy || draft.length > 0;
  return (
    <DialogShell
      label={halftime ? "Intervalo da partida" : "Comando do treinador"}
      busy={locked}
      onClose={halftime || locked ? () => {} : () => void resume()}
    >
      <TacticalDesk
        packet={packet}
        home={home}
        away={away}
        tactics={tacticsPanel}
        players={playersPanel}
        pitch={
          <TacticalPitch
            players={managedPlayers}
            frames={packet.replay.frames}
            teamIndex={index}
            secondHalf={
              view.events.find((event) => event.type === "second_half")?.second
            }
            squad={roster}
            color={kit()}
            keeper={kit("GOL")}
            plan={plan}
            referencePositions={referencePositions}
            setPlan={setPlan}
            editable={advanced}
            disabled={tacticBusy}
          />
        }
        substitutions={substitutionPanel}
        quick={quickPanel}
        error={error}
        message={
          draft.length
            ? "Confirme ou cancele as trocas preparadas antes de retomar."
            : notice
        }
        busy={locked}
        onResume={() => void resume()}
        onSpeed={(speed) => void control({ type: "speed", speed })}
      />
    </DialogShell>
  );
}
