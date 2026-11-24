import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, type Career } from "../../services/api";

type Country = { code: string; name: string; format: string; clubs: { id: string; name: string; short: string; city: string; color: string; strength: number }[] };

// The other nine South American championships of the last season: champions, aggregate table and the clubs that go to the Libertadores
// and the Sudamericana. The clubs are real; the results are simulated from club strength.
export function SouthAmericaPanel({ c }: { c: Career }) {
  const data = c.state.southAmerica;
  const countries = useQuery({ queryKey: ["south-america"], queryFn: () => api<Country[]>("/south-america"), staleTime: Infinity });
  const [code, setCode] = useState("ARG");
  if (!data || !countries.data) return null;
  const country = countries.data.find((x) => x.code === code) ?? countries.data[0];
  const result = data.results.find((r) => r.code === country.code);
  const club = (id: string) => country.clubs.find((x) => x.id === id);
  const name = (id: string) => club(id)?.name ?? id;
  return (
    <section className="panel" style={{ padding: 24 }}>
      <h2>América do Sul · campeonatos nacionais</h2>
      <p className="muted">
        Temporada {data.season - 1}: resultados simulados dos nove campeonatos que classificam clubes para a Libertadores e a Sul-Americana de {data.season}. Escudos genéricos nas cores do clube.
      </p>
      <label>
        País
        <select value={country.code} onChange={(e) => setCode(e.target.value)}>
          {countries.data.map((x) => (
            <option key={x.code} value={x.code}>
              {x.name}
            </option>
          ))}
        </select>
      </label>
      <p>
        <b>{country.name}</b> · {country.format}
      </p>
      {result ? (
        <>
          <ul className="sa-champions" style={{ listStyle: "none", padding: 0, display: "flex", flexWrap: "wrap", gap: 16 }}>
            <li>
              <b>Campeão:</b> {name(result.champion)}
            </li>
            {result.tournaments.map((t) => (
              <li key={t.name}>
                {t.name}: {name(t.champion)}
              </li>
            ))}
          </ul>
          <p>
            <b>Libertadores:</b> {result.libertadores.map(name).join(", ")}
            <br />
            <b>Sul-Americana:</b> {result.sudamericana.map(name).join(", ")}
          </p>
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
                  <th>Vaga</th>
                </tr>
              </thead>
              <tbody>
                {result.table.map((r, i) => (
                  <tr key={r.clubId}>
                    <td>{i + 1}</td>
                    <td>
                      <i style={{ display: "inline-block", width: 10, height: 10, borderRadius: 2, marginRight: 8, background: club(r.clubId)?.color ?? "#888" }} />
                      {name(r.clubId)}
                    </td>
                    <td>{r.points}</td>
                    <td>{r.played}</td>
                    <td>{r.wins}</td>
                    <td>{r.difference}</td>
                    <td>{result.libertadores.includes(r.clubId) ? "Libertadores" : result.sudamericana.includes(r.clubId) ? "Sul-Americana" : ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <p>Sem resultados para este país.</p>
      )}
    </section>
  );
}
