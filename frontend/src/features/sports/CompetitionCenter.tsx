import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { seasonRounds } from "../career/season";
import {
  api,
  type Career,
  type Club,
  type Fixture,
  type Row,
} from "../../services/api";
import { Button } from "../../components/ui/button";
type Divisions = {
  managedDivision: string;
  otherClubs: Club[];
  otherFixtures: Fixture[];
  playoffs: Fixture[];
  promoted: string[];
  relegated: string[];
};
export default function CompetitionCenter({ c }: { c: Career }) {
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: ["divisions", c.id, c.revision],
    queryFn: () =>
      api<{
        divisions: Divisions | null;
        managedTable: Row[];
        otherTable: Row[] | null;
      }>(`/careers/${c.id}/divisions`),
  });
  const m = useMutation({
    mutationFn: (operation: string) =>
      api(`/careers/${c.id}/divisions/${operation}`, { revision: c.revision }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["career", c.id] });
      qc.invalidateQueries({ queryKey: ["divisions", c.id] });
    },
  });
  const d = q.data?.divisions;
  const name = (id: string) =>
    c.state.clubs.concat(d?.otherClubs ?? []).find((x) => x.id === id)?.name ??
    id;
  return (
    <div className="form-stack">
      <section className="panel" style={{ padding: 24 }}>
        <h2>Competição e acesso</h2>
        <p>
          {seasonRounds(c.state)} rodadas, turno e returno. Desempate: pontos, vitórias, saldo, gols
          pró, confronto direto entre dois clubes, menos vermelhos, menos
          amarelos e sorteio persistente na carreira.
        </p>
        {q.isPending && <p>Carregando competição…</p>}
        {q.error && <p role="alert">{q.error.message}</p>}
        {!d ? (
          <>
            <p>
              A carreira está em uma divisão. Para experimentar acesso e
              descenso, ative a expansão antes da primeira rodada em uma
              carreira demonstrativa: ela acrescenta vinte clubes regionais e
              atletas fictícios.
            </p>
            <Button
              disabled={
                c.state.round !== 1 ||
                !c.state.dataVersion.startsWith("fictional-") ||
                m.isPending
              }
              onClick={() => m.mutate("enable")}
            >
              Ativar duas divisões demonstrativas
            </Button>
          </>
        ) : (
          <>
            <p>
              Sua divisão: <strong>{d.managedDivision}</strong>. Série A: quatro
              descensos. Série B: dois acessos diretos; 3º×6º e 4º×5º disputam
              ida e volta, com a volta na casa da melhor campanha. Empate em
              pontos e saldo favorece a melhor classificação. Modelo de disputa
              de 2026 mantido nesta simulação.
            </p>
            <Button
              disabled={
                c.state.round <= seasonRounds(c.state) || d.promoted.length > 0 || m.isPending
              }
              onClick={() => m.mutate("complete")}
            >
              Disputar playoffs e definir acessos
            </Button>
            {d.promoted.length > 0 && (
              <>
                <p>Promovidos: {d.promoted.map(name).join(", ")}.</p>
                <p>Rebaixados: {d.relegated.map(name).join(", ")}.</p>
                {d.playoffs.map((f, i) => (
                  <p key={i}>
                    {name(f.home)} {f.homeGoals} × {f.awayGoals} {name(f.away)}
                    {f.events.some((e) => e.type === "playoff_tiebreak")
                      ? " · melhor campanha decidiu o acesso"
                      : ""}
                  </p>
                ))}
              </>
            )}
          </>
        )}
        {m.error && (
          <p className="error" role="alert">
            {m.error.message}
          </p>
        )}
      </section>
      {[
        {
          title: d ? `Sua divisão · ${d.managedDivision}` : "Classificação",
          rows: q.data?.managedTable ?? c.table,
        },
        {
          title:
            d?.managedDivision === "A"
              ? "Série B demonstrativa"
              : "Série A demonstrativa",
          rows: q.data?.otherTable ?? [],
        },
      ]
        .filter((x) => x.rows.length > 0)
        .map((t) => (
          <section key={t.title} className="panel" style={{ padding: 24 }}>
            <h2>{t.title}</h2>
            <div style={{ overflow: "auto" }}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Clube</th>
                    <th>PTS</th>
                    <th>J</th>
                    <th>V</th>
                    <th>SG</th>
                    <th>GP</th>
                  </tr>
                </thead>
                <tbody>
                  {t.rows.map((r, i) => (
                    <tr key={r.club.id}>
                      <td>{i + 1}</td>
                      <td>{r.club.name}</td>
                      <td>{r.points}</td>
                      <td>{r.played}</td>
                      <td>{r.wins}</td>
                      <td>{r.difference}</td>
                      <td>{r.goals}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ))}
    </div>
  );
}
