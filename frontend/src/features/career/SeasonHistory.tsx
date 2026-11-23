import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Trophy, Play, CalendarDays } from "lucide-react";
import {
  api,
  type Career,
  type Fixture,
  type PastSeason,
  type SeasonStanding,
} from "../../services/api";
import { Button } from "../../components/ui/button";
import { DataTable } from "../../components/DataTable";
import type { ColumnDef } from "@tanstack/react-table";

export default function SeasonHistory({
  c,
  onReplay,
}: {
  c: Career;
  onReplay: (f: Fixture) => void;
}) {
  const seasons = c.state.pastSeasons;
  const [year, setYear] = useState(seasons.at(-1)?.season ?? 0);
  const [onlyMine, setOnlyMine] = useState(true);
  useEffect(() => {
    if (!seasons.some((s) => s.season === year))
      setYear(seasons.at(-1)?.season ?? 0);
  }, [seasons, year]);
  const q = useQuery({
    queryKey: ["season-history", c.id, year],
    queryFn: () => api<PastSeason>(`/careers/${c.id}/history/${year}`),
    enabled: !!year,
    staleTime: Infinity,
    gcTime: 30000,
  });
  const name = (id: string) =>
    (q.data?.clubs ?? c.state.clubs)
      .concat(q.data?.divisions?.otherClubs ?? [])
      .find((x) => x.id === id)?.name ?? id;
  const short = (id: string) =>
    (q.data?.clubs ?? c.state.clubs)
      .concat(q.data?.divisions?.otherClubs ?? [])
      .find((x) => x.id === id)?.short ?? id;
  const columns: ColumnDef<SeasonStanding, any>[] = [
    {
      id: "rank",
      header: "#",
      cell: (x) =>
        (q.data?.table.findIndex((r) => r.clubId === x.row.original.clubId) ??
          0) + 1,
    },
    {
      accessorKey: "clubId",
      header: "Clube",
      cell: (x) => (
        <strong className={x.getValue() === c.state.clubId ? "green" : ""}>
          {name(x.getValue())}
        </strong>
      ),
    },
    { accessorKey: "points", header: "PTS" },
    { accessorKey: "played", header: "J" },
    { accessorKey: "wins", header: "V" },
    { accessorKey: "draws", header: "E" },
    { accessorKey: "losses", header: "D" },
    { accessorKey: "goals", header: "GP" },
    { accessorKey: "against", header: "GC" },
    { accessorKey: "difference", header: "SG" },
  ];
  return (
    <>
      <section className="panel">
        <div className="panel-heading">
          <h2>Memória da carreira · {c.name}</h2>
          <span className="tag">{seasons.length} TEMPORADAS ARQUIVADAS</span>
        </div>
        <div className="form-stack">
          <p>
            Temporada atual: {c.state.season}. O arquivo preserva a tabela final
            e os jogos dos anos anteriores.
          </p>
          {seasons.length ? (
            <label>
              Temporada arquivada
              <select
                value={year}
                onChange={(e) => setYear(Number(e.target.value))}
              >
                {seasons
                  .slice()
                  .reverse()
                  .map((s) => (
                    <option key={s.season} value={s.season}>
                      {s.season} · Campeão: {name(s.champion)}
                    </option>
                  ))}
              </select>
            </label>
          ) : (
            <p className="muted">
              O arquivo será criado quando você concluir as 38 rodadas e iniciar
              a temporada seguinte. Anos anteriores à versão 0.3 não podem ter
              seus jogos recuperados se o save antigo já os descartou.
            </p>
          )}
        </div>
      </section>
      {q.isFetching && <p className="empty">Abrindo a temporada arquivada…</p>}
      {q.error && (
        <p className="error-banner" role="alert">
          {q.error.message}
        </p>
      )}
      {q.data && (
        <>
          <section className="panel history-table">
            <div className="panel-heading">
              <h2>
                <Trophy size={17} /> {name(q.data.champion)} · Campeão de {year}
              </h2>
            </div>
            {q.data.divisions && (
              <div>
                <p>
                  Divisão {q.data.divisions.managedDivision} · Promovidos:{" "}
                  {q.data.divisions.promoted.map(name).join(", ")} · Rebaixados:{" "}
                  {q.data.divisions.relegated.map(name).join(", ")}.
                </p>
                {q.data.divisions.playoffs.map((f, i) => (
                  <p key={i}>
                    {name(f.home)} {f.homeGoals} × {f.awayGoals} {name(f.away)}{" "}
                    <Button
                      variant="ghost"
                      onClick={() =>
                        onReplay({
                          ...f,
                          season: year,
                          archiveClubs: (q.data?.clubs ?? []).concat(
                            q.data?.divisions?.otherClubs ?? [],
                          ),
                        })
                      }
                    >
                      Rever playoff
                    </Button>
                  </p>
                ))}
              </div>
            )}
            <DataTable data={q.data.table} columns={columns} />
          </section>
          {!!q.data.playerStats?.length && (
            <section className="panel" style={{ padding: 24 }}>
              <h2>Artilharia de {year}</h2>
              <p>
                Cobertura desde a rodada {q.data.playerStatsFromRound ?? 1}.
              </p>
              <DataTable
                data={[...q.data.playerStats]
                  .sort((a, b) => b.goals - a.goals)
                  .slice(0, 20)}
                columns={[
                  { accessorKey: "playerName", header: "Atleta" },
                  { accessorKey: "goals", header: "Gols" },
                  { accessorKey: "appearances", header: "Jogos" },
                  { accessorKey: "minutes", header: "Minutos" },
                ]}
              />
            </section>
          )}
          <section className="panel">
            <div className="panel-heading">
              <h2>
                <CalendarDays size={17} /> Partidas e replays · {year}
              </h2>
              <label className="inline-checkbox">
                <input
                  type="checkbox"
                  checked={onlyMine}
                  onChange={(e) => setOnlyMine(e.target.checked)}
                />
                Meu clube
              </label>
            </div>
            <div className="historical-fixtures">
              {q.data.fixtures
                ?.filter(
                  (f) =>
                    !onlyMine ||
                    f.home === c.state.clubId ||
                    f.away === c.state.clubId,
                )
                .map((f) => (
                  <div key={`${f.round}-${f.home}`} className="fixture-line">
                    <span className="muted">R{f.round}</span>
                    <span>{short(f.home)}</span>
                    <b>
                      {f.homeGoals} : {f.awayGoals}
                    </b>
                    <span>{short(f.away)}</span>
                    <Button
                      variant="ghost"
                      aria-label={`Rever ${name(f.home)} e ${name(f.away)} em ${year}, rodada ${f.round}`}
                      onClick={() =>
                        onReplay({
                          ...f,
                          season: year,
                          archiveClubs: (q.data?.clubs ?? c.state.clubs).concat(
                            q.data?.divisions?.otherClubs ?? [],
                          ),
                        })
                      }
                    >
                      <Play size={15} />
                      Rever
                    </Button>
                  </div>
                ))}
            </div>
          </section>
        </>
      )}
      <section className="panel history-journal">
        <div className="panel-heading">
          <h2>Diário do clube</h2>
        </div>
        <div className="form-stack">
          {c.state.news.slice(0, 12).map((n, i) => (
            <p key={i} className="history-line">
              {n}
            </p>
          ))}
          {c.state.history.map((n, i) => (
            <p key={"season" + i}>{n}</p>
          ))}
        </div>
      </section>
    </>
  );
}
