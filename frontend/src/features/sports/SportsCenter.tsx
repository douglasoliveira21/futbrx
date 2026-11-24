import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  api,
  availability,
  type Career,
  type PlayerStats,
  type SubstitutionPlan,
} from "../../services/api";
import { Button } from "../../components/ui/button";
import { Shirt } from "./SquadRoom";
import { Loader } from "../../components/Loader";

export default function SportsCenter({
  c,
  mode,
}: {
  c: Career;
  mode: "plan" | "medical" | "statistics";
}) {
  const g = c.state;
  const qc = useQueryClient();
  const [plans, setPlans] = useState<SubstitutionPlan[]>(g.substitutions ?? []);
  const [automatic, setAutomatic] = useState(g.autoSubstitute);
  const [replace, setReplace] = useState(g.autoReplaceUnavailable);
  const [sort, setSort] = useState("goals");
  const [own, setOwn] = useState(false);
  const [message, setMessage] = useState("");
  const change = useMutation({
    mutationFn: ({ path, body }: { path: string; body?: object }) =>
      api<{ result: { message: string } }>(`/careers/${c.id}/sports/${path}`, {
        revision: c.revision,
        ...body,
      }),
    onSuccess: (r) => {
      setMessage(r.result.message);
      void qc.invalidateQueries({ queryKey: ["career", c.id] });
      void qc.invalidateQueries({ queryKey: ["sports", c.id] });
    },
    onError: () => {
      void qc.invalidateQueries({ queryKey: ["career", c.id] });
    },
  });
  const stats = useQuery({
    queryKey: ["sports", c.id, c.revision, sort, own],
    queryFn: () =>
      api<{ season: number; fromRound: number; players: PlayerStats[] }>(
        `/careers/${c.id}/sports/statistics?sort=${sort}${own ? `&club=${g.clubId}` : ""}`,
      ),
    enabled: mode === "statistics",
  });
  const starters = g.squad.filter((p) => p.starter);
  const bench = (g.benchIds ?? [])
    .map((id) => g.squad.find((p) => p.id === id))
    .filter((p) => !!p);
  const update = (i: number, value: Partial<SubstitutionPlan>) =>
    setPlans(plans.map((p, j) => (i === j ? { ...p, ...value } : p)));
  return (
    <section className="panel" style={{ marginTop: 20, padding: 24 }}>
      <h2>
        {mode === "plan"
          ? "Banco e substituições"
          : mode === "medical"
            ? "Disponibilidade e disciplina"
            : "Estatísticas individuais"}
      </h2>
      {change.error && (
        <p className="error" role="alert">
          {change.error.message}
        </p>
      )}
      {message && <p role="status">{message}</p>}
      {mode !== "statistics" && (
        <>
          <p>
            Lesionados e suspensos ficam fora da seleção. Cada rodada recupera
            sete dias de lesão; suspensões são cumpridas em partidas do clube.
          </p>
          <Button
            disabled={change.isPending}
            variant="outline"
            onClick={() => change.mutate({ path: "lineup/auto" })}
          >
            Escalar os disponíveis automaticamente
          </Button>
        </>
      )}
      {mode === "medical" && (
        <>
          <p>
            Três amarelos acumulados ou expulsão geram um jogo de suspensão.
            Novas punições passam a valer na rodada seguinte.
          </p>
          <div className="health-overview">
            <div><strong>{g.squad.filter(p => !p.injuryDays && !p.suspensionMatches).length}</strong><span>Prontos para o jogo</span></div>
            <div><strong>{g.squad.filter(p => !!p.injuryDays).length}</strong><span>Em recuperação</span></div>
            <div><strong>{g.squad.filter(p => !!p.suspensionMatches).length}</strong><span>Suspensos</span></div>
          </div>
          <div className="medical-roster">{[...g.squad].sort((a,b) => (b.injuryDays+b.suspensionMatches*7)-(a.injuryDays+a.suspensionMatches*7)).map(p => <article className={`medical-athlete ${p.injuryDays || p.suspensionMatches ? "unavailable" : ""}`} key={p.id}>
            <Shirt color={g.clubs.find(c => c.id===g.clubId)!.color} number={0} short={p.position} />
            <div><span className="eyebrow">{p.position} · {p.starter ? "Titular" : "Reserva"}</span><h3>{p.name}</h3><p>{availability(p)}</p><meter min={0} max={100} value={p.fitness} aria-label={`Condição de ${p.name}`} /><small>{p.fitness}% de condição · {p.yellowCards}/3 amarelos{p.suspensionMatches ? ` · ${p.suspensionMatches} jogo(s) de suspensão` : ""}</small></div>
          </article>)}</div>
        </>
      )}
      {mode === "plan" && (
        <div className="form-stack">
          <p>
            {starters.length}/11 titulares · {bench.length}/12 reservas. Até
            cinco trocas, compartilhadas entre planos e decisões automáticas. O
            plano é usado apenas na próxima partida.
          </p>
          <p className="muted">
            Banco:{" "}
            {bench.map((p) => `${p.name} (${p.position})`).join(" · ") ||
              "Nenhum reserva disponível"}
          </p>
          <label>
            <input
              type="checkbox"
              checked={automatic}
              onChange={(e) => setAutomatic(e.target.checked)}
            />{" "}
            Trocar automaticamente por desgaste ou lesão
          </label>
          <label>
            <input
              type="checkbox"
              checked={replace}
              onChange={(e) => setReplace(e.target.checked)}
            />{" "}
            Recompor titulares indisponíveis antes da rodada
          </label>
          {plans.map((p, i) => (
            <div className="toolbar" key={i}>
              <label>
                Minuto
                <input
                  type="number"
                  min={1}
                  max={89}
                  value={p.minute}
                  onChange={(e) =>
                    update(i, { minute: Number(e.target.value) })
                  }
                />
              </label>
              <label>
                Sai
                <select
                  value={p.outPlayerId}
                  onChange={(e) =>
                    update(i, { outPlayerId: Number(e.target.value) })
                  }
                >
                  {starters.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name} · {x.position}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Entra
                <select
                  value={p.inPlayerId}
                  onChange={(e) =>
                    update(i, { inPlayerId: Number(e.target.value) })
                  }
                >
                  {bench.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name} · {x.position}
                    </option>
                  ))}
                </select>
              </label>
              <Button
                variant="ghost"
                onClick={() => setPlans(plans.filter((_, j) => i !== j))}
              >
                Remover troca {i + 1}
              </Button>
            </div>
          ))}
          <div className="actions">
            <Button
              variant="outline"
              disabled={plans.length >= 5 || !bench.length || !starters.length}
              onClick={() =>
                setPlans([
                  ...plans,
                  {
                    minute: 60,
                    outPlayerId:
                      starters.find((p) => p.position !== "GOL")?.id ??
                      starters[0].id,
                    inPlayerId:
                      bench.find((p) => p.position !== "GOL")?.id ??
                      bench[0].id,
                  },
                ])
              }
            >
              Adicionar troca
            </Button>
            <Button
              disabled={change.isPending}
              onClick={() =>
                change.mutate({
                  path: "plan",
                  body: {
                    substitutions: plans,
                    autoSubstitute: automatic,
                    autoReplaceUnavailable: replace,
                  },
                })
              }
            >
              Salvar plano de substituições
            </Button>
          </div>
          <p className="muted">
            Trocas são realizadas na primeira oportunidade sem bola em voo após
            o minuto programado. Reservas já utilizados, expulsões ou lesões
            podem impedir uma troca.
          </p>
        </div>
      )}
      {mode === "statistics" && (
        <>
          <div className="club-form-strip">{[...c.table].sort((a,b)=>b.goals-a.goals).slice(0,4).map(row=><article key={row.club.id}><span>{row.club.name}</span><strong>{row.goals}</strong><small>gols · {row.wins} vitórias</small></article>)}</div>
          <div className="toolbar">
            <select
              aria-label="Ordenar estatísticas"
              value={sort}
              onChange={(e) => setSort(e.target.value)}
            >
              <option value="goals">Artilharia</option>
              <option value="minutes">Minutos</option>
              <option value="passes">Passes completos</option>
              <option value="cards">Cartões</option>
            </select>
            <label>
              <input
                type="checkbox"
                checked={own}
                onChange={(e) => setOwn(e.target.checked)}
              />{" "}
              Meu clube
            </label>
          </div>
          {stats.error && (
            <p className="error" role="alert">
              {stats.error.message}
            </p>
          )}
          {stats.isPending ? (
            <Loader variant="panel" label="Carregando…" />
          ) : (
            <>
              <p>
                Temporada {stats.data?.season} · cobertura a partir da rodada{" "}
                {stats.data?.fromRound}. Até 100 atletas. Partidas legadas não
                geram estatísticas retroativas.
              </p>
              <div className="player-leaderboard">{(stats.data?.players ?? []).map((p,i)=><article key={p.playerId+"-"+p.clubId}>
                <span className="leader-position">{i+1}</span><div><h3>{p.playerName}</h3><span>{g.clubs.find(c=>c.id===p.clubId)?.name ?? p.clubId}</span><small>{p.appearances} jogos · {p.minutes} min · {p.shots} chutes · {p.passes} passes · {p.yellowCards} amarelos / {p.redCards} vermelhos</small></div><strong>{sort==="minutes" ? p.minutes : sort==="passes" ? p.passes : sort==="cards" ? p.yellowCards+p.redCards : p.goals}<small>{sort==="minutes" ? "minutos" : sort==="passes" ? "passes" : sort==="cards" ? "cartões" : "gols"}</small></strong>
              </article>)}</div>
              {!stats.data?.players.length && <p className="empty">A temporada ainda não tem atuações registradas.</p>}
            </>
          )}
        </>
      )}
    </section>
  );
}
