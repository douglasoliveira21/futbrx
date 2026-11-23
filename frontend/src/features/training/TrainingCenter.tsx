import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  api,
  availability,
  type Career,
  type TrainingData,
  type TrainingPlan,
} from "../../services/api";
import { Button } from "../../components/ui/button";

function TrainingEditor({
  data,
  busy,
  save,
}: {
  data: TrainingData;
  busy: boolean;
  save: (plan: TrainingPlan) => void;
}) {
  const [plan, setPlan] = useState<TrainingPlan>(() =>
    structuredClone(data.plan),
  );
  const saved = JSON.stringify(data.plan);
  useEffect(() => {
    setPlan(JSON.parse(saved));
  }, [saved]);
  const dirty = JSON.stringify(plan) !== saved;
  return (
    <section className="panel" style={{ padding: 24 }}>
      <h2>Planejamento da semana</h2>
      <p>
        Seis dias de preparação e uma partida por rodada. O plano se repete até
        você alterá-lo. Inclua ao menos um dia de descanso ou recuperação.
      </p>
      <div className="toolbar">
        {data.presets.map((p) => (
          <Button
            key={p.name}
            variant="outline"
            disabled={busy}
            onClick={() => setPlan(structuredClone(p.plan))}
          >
            Modelo {p.name.toLowerCase()}
          </Button>
        ))}
        <label>
          Intensidade
          <select
            value={plan.intensity}
            disabled={busy}
            onChange={(e) => setPlan({ ...plan, intensity: e.target.value })}
          >
            {data.intensities.map((i) => (
              <option key={i}>{i}</option>
            ))}
          </select>
        </label>
      </div>
      <div className="training-week">
        {plan.days.map((day, i) => (
          <label className="training-day" key={i}>
            <span>Dia {i + 1}</span>
            <select
              aria-label={`Sessão do dia ${i + 1}`}
              value={day}
              disabled={busy}
              onChange={(e) =>
                setPlan({
                  ...plan,
                  days: plan.days.map((s, j) => (i === j ? e.target.value : s)),
                })
              }
            >
              {data.sessions.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </label>
        ))}
        <div className="training-day">
          <span>Dia 7</span>
          <strong>Partida</strong>
        </div>
      </div>
      <p className="muted">
        Intensidade alta rende mais experiência e consome mais condição.
        Descanso e recuperação repõem condição. Lesionados não treinam;
        suspensos podem participar. Treinos não encurtam dias de lesão.
      </p>
      <div className="actions">
        <Button
          disabled={busy || !dirty || data.round > 38}
          onClick={() => save(plan)}
        >
          Salvar planejamento semanal
        </Button>
        <span role="status">
          {dirty ? "Alterações ainda não salvas" : "Planejamento salvo"}
        </span>
      </div>
    </section>
  );
}
export default function TrainingCenter({ c }: { c: Career }) {
  const qc = useQueryClient();
  const [message, setMessage] = useState("");
  const [playerId, setPlayerId] = useState<number | null>(null);
  const [reportKey, setReportKey] = useState("");
  const query = useQuery({
    queryKey: ["training", c.id, c.revision],
    placeholderData: (previous) => previous,
    queryFn: () => api<TrainingData>(`/careers/${c.id}/training/`),
  });
  const change = useMutation({
    mutationFn: ({ path, body }: { path: string; body: object }) =>
      api<{ result: { message: string } }>(
        `/careers/${c.id}/training/${path}`,
        { revision: query.data!.revision, ...body },
      ),
    onSuccess: async (r) => {
      setMessage(r.result.message);
      await Promise.all([
        qc.invalidateQueries({ queryKey: ["training", c.id] }),
        qc.invalidateQueries({ queryKey: ["career", c.id] }),
      ]);
    },
    onError: () => {
      void qc.invalidateQueries({ queryKey: ["training", c.id] });
      void qc.invalidateQueries({ queryKey: ["career", c.id] });
    },
  });
  if (query.isPending) return <p>Carregando treinos…</p>;
  if (query.error)
    return (
      <p className="error" role="alert">
        {query.error.message}
      </p>
    );
  const data = query.data;
  const selected =
    data.players.find((p) => p.player.id === playerId) ?? data.players[0];
  const attrs = data.sessions.filter(
    (s) => !["Recuperação", "Descanso"].includes(s),
  );
  const report =
    data.reports.find((r) => `${r.season}-${r.round}` === reportKey) ??
    data.reports[0];
  return (
    <div className="form-stack">
      <div className="notice">
        Rodada {data.round} · {data.season}. O treino é aplicado uma única vez,
        antes da partida. Cada 100 XP em um atributo gera +1 ponto, até 99;
        idade e condição afetam a velocidade de evolução.
      </div>
      {message && <p role="status">{message}</p>}
      {change.error && (
        <p className="error" role="alert">
          {change.error.message}
        </p>
      )}
      <TrainingEditor
        data={data}
        busy={change.isPending || query.isFetching}
        save={(plan) => change.mutate({ path: "plan", body: { plan } })}
      />
      <section className="panel" style={{ padding: 24 }}>
        <h2>Desenvolvimento individual</h2>
        <p>
          O foco individual direciona as sessões de atributo daquele atleta.
          Dias de descanso e recuperação continuam valendo. A habilidade geral
          usa os atributos com pesos por posição e influencia as próximas
          partidas.
        </p>
        <label>
          Atleta
          <select
            value={selected?.player.id ?? ""}
            onChange={(e) => setPlayerId(Number(e.target.value))}
          >
            {data.players.map((p) => (
              <option key={p.player.id} value={p.player.id}>
                {p.player.name} · {p.player.position} ·{" "}
                {p.group ?? "Profissional"}
              </option>
            ))}
          </select>
        </label>
        {selected && (
          <>
            <div className="toolbar">
              <strong>
                Habilidade {selected.player.rating} · condição{" "}
                {selected.player.fitness}%
              </strong>
              <span>{availability(selected.player)}</span>
              <label>
                Foco individual
                <select
                  disabled={change.isPending || query.isFetching}
                  value={selected.development.focus}
                  onChange={(e) =>
                    change.mutate({
                      path: `players/${selected.player.id}/focus`,
                      body: { focus: e.target.value },
                    })
                  }
                >
                  {[
                    "Equipe",
                    ...attrs.filter(
                      (a) =>
                        a !== "Goleiro" || selected.player.position === "GOL",
                    ),
                  ].map((a) => (
                    <option key={a}>{a}</option>
                  ))}
                </select>
              </label>
            </div>
            <div className="training-attributes">
              {attrs.map((a) => (
                <div className="training-attribute" key={a}>
                  <strong>
                    {a} <span>{selected.development.attributes[a]}/99</span>
                  </strong>
                  <progress
                    aria-label={`Experiência em ${a}`}
                    value={selected.development.experience[a]}
                    max={100}
                  />
                  <small>
                    {selected.development.attributes[a] === 99
                      ? "Atributo no limite"
                      : `${selected.development.experience[a]}/100 XP para o próximo ponto`}
                  </small>
                </div>
              ))}
            </div>
          </>
        )}
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Relatórios de treino</h2>
        {!report ? (
          <p>O primeiro relatório aparecerá após avançar uma rodada.</p>
        ) : (
          <>
            <label>
              Semana aplicada
              <select
                value={`${report.season}-${report.round}`}
                onChange={(e) => setReportKey(e.target.value)}
              >
                {data.reports.map((r) => (
                  <option
                    key={`${r.season}-${r.round}`}
                    value={`${r.season}-${r.round}`}
                  >
                    {r.season} · rodada {r.round}
                  </option>
                ))}
              </select>
            </label>
            <p>
              Intensidade {report.plan.intensity.toLowerCase()} ·{" "}
              {report.plan.days.join(" → ")}. A condição abaixo compara o início
              e o fim do treino, antes do desgaste da partida. Últimos 12
              relatórios.
            </p>
            <div className="training-reports">{report.players.map(p=><article className="training-report" key={p.playerId}><div className="training-report-heading"><div><span className="eyebrow">{p.group ?? "Profissional"}</span><h3>{p.name}</h3></div><strong>{p.ratingAfter}<small>habilidade{p.ratingAfter>p.ratingBefore ? ` · +${p.ratingAfter-p.ratingBefore}` : ""}</small></strong></div><p>Condição pré-jogo: {p.fitnessBefore}% para {p.fitnessAfter}%</p><p className={p.injured ? "gold-text" : "muted"}>{p.injured ? "Fora do treino por lesão" : Object.entries(p.experience).map(([a,v])=>`${a}: +${v} XP`).join(" · ") || "Recuperação e descanso"}</p><div className="training-gains">{p.gains.length ? p.gains.map(g=><span key={g.attribute}>{g.attribute} <b>{g.before} → {g.after}</b></span>) : <span>Em desenvolvimento</span>}</div></article>)}</div>
          </>
        )}
      </section>
    </div>
  );
}
