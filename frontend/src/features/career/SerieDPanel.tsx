import { useState } from "react";
import type { Career } from "../../services/api";

// Série D: the final tables of the 16 groups (the user's group is played, the others are simulated from
// club strength) and the clubs that qualified for the knockout phase.
export function SerieDPanel({ c, club }: { c: Career; club: (id: string) => string }) {
  const serie = c.state.serieD;
  const [group, setGroup] = useState(serie?.group ?? 1);
  if (!serie) return null;
  const result = serie.results.find((r) => r.group === group);
  const mine = c.state.clubId;
  return (
    <article className="life-card serie-d-panel">
      <div className="life-card-heading">
        <div>
          <span className="eyebrow">SÉRIE D · FASE DE GRUPOS</span>
          <h3>Seu grupo: {serie.group} de 16</h3>
        </div>
        {serie.promoted.length > 0 && <strong>{serie.promoted.length} acessos definidos</strong>}
      </div>
      {serie.results.length === 0 ? (
        <p>
          O seu grupo tem seis clubes e dez rodadas. Os quatro primeiros de cada um dos 16 grupos avançam aos mata-matas
          de ida e volta. As tabelas dos outros 15 grupos aparecem quando a fase de grupos termina; os grupos são
          formados por proximidade geográfica, enquanto os oficiais não foram conferidos.
        </p>
      ) : (
        <>
          <label>
            Grupo
            <select value={group} onChange={(e) => setGroup(Number(e.target.value))}>
              {serie.results.map((r) => (
                <option key={r.group} value={r.group}>
                  {r.group === serie.group ? `Grupo ${r.group} (seu clube)` : `Grupo ${r.group}`}
                </option>
              ))}
            </select>
          </label>
          <table className="life-table">
            <thead>
              <tr>
                <th>#</th>
                <th>Clube</th>
                <th>P</th>
                <th>J</th>
                <th>V</th>
                <th>E</th>
                <th>D</th>
                <th>SG</th>
              </tr>
            </thead>
            <tbody>
              {result?.table.map((row, i) => (
                <tr key={row.clubId} className={row.clubId === mine ? "managed" : i < 4 ? "qualified" : ""}>
                  <td>{i + 1}</td>
                  <td>{club(row.clubId)}</td>
                  <td>{row.points}</td>
                  <td>{row.played}</td>
                  <td>{row.wins}</td>
                  <td>{row.draws}</td>
                  <td>{row.losses}</td>
                  <td>{row.difference}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="muted">
            {serie.qualified.includes(mine)
              ? "Seu clube está na fase final."
              : "Seu clube não avançou: as semanas seguintes passam sem jogos."}
          </p>
        </>
      )}
      {serie.promoted.length > 0 && (
        <p>
          <b>Sobem para a Série C:</b> {serie.promoted.map(club).join(", ")}.
        </p>
      )}
    </article>
  );
}
