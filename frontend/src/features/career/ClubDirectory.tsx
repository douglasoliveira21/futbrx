import { useMemo, useState } from "react";
import { Check, Search } from "lucide-react";
import { Crest } from "../../components/Crest";
import {
  countBy,
  filterClubs,
  groupBySeries,
  isPlayable,
  seriesList,
  stateNames,
  versionLabel,
  type Directory,
  type DirectoryClub,
  type SeriesId,
} from "./directory";
import "./clubDirectory.css";

const leagueText: Record<SeriesId, string> = {
  A: "Série A · 20 clubes",
  B: "Série B · 20 clubes",
  C: "Série C · 20 clubes",
  D: "Série D · 96 clubes, em 16 grupos de 6",
};

// Club picker: every club of Séries A to D, filtered by division, state or name.
export default function ClubDirectory({
  directory,
  selectedId,
  onSelect,
  version,
  onVersion,
  disabled = false,
}: {
  directory: Directory;
  selectedId: string;
  onSelect: (club: DirectoryClub) => void;
  version: string;
  onVersion: (version: string) => void;
  disabled?: boolean;
}) {
  const [series, setSeries] = useState<SeriesId | "all">("all");
  const [uf, setUf] = useState("");
  const [search, setSearch] = useState("");
  const shown = useMemo(
    () => filterClubs(directory.clubs, { series, uf, search }),
    [directory.clubs, series, uf, search],
  );
  const seriesCounts = useMemo(
    () => countBy(filterClubs(directory.clubs, { series: "all", uf, search }), "series"),
    [directory.clubs, uf, search],
  );
  const stateCounts = useMemo(
    () => countBy(filterClubs(directory.clubs, { series, uf: "", search }), "uf"),
    [directory.clubs, series, search],
  );
  const states = Object.keys(stateNames)
    .filter((code) => stateCounts[code] || code === uf)
    .sort((a, b) => stateNames[a].localeCompare(stateNames[b], "pt-BR"));
  const selected = directory.clubs.find((club) => club.id === selectedId);
  const versions = selected ? (directory.versions[selected.series] ?? []) : [];

  return (
    <div className="club-directory">
      <div className="club-filters">
        <div className="club-series" role="group" aria-label="Divisão">
          {(["all", ...seriesList] as const).map((id) => (
            <button
              type="button"
              key={id}
              aria-pressed={series === id}
              onClick={() => setSeries(id)}
              disabled={disabled}
            >
              {id === "all" ? "Todas" : `Série ${id}`}
              <small>
                {id === "all"
                  ? Object.values(seriesCounts).reduce((a, b) => a + b, 0)
                  : (seriesCounts[id] ?? 0)}
              </small>
            </button>
          ))}
        </div>
        <label>
          Estado
          <select value={uf} onChange={(e) => setUf(e.target.value)} disabled={disabled}>
            <option value="">Todos os estados</option>
            {states.map((code) => (
              <option key={code} value={code}>
                {stateNames[code]} ({stateCounts[code] ?? 0})
              </option>
            ))}
          </select>
        </label>
        <label className="club-search">
          Buscar
          <span>
            <Search size={15} aria-hidden />
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Clube, cidade ou estado"
              disabled={disabled}
            />
          </span>
        </label>
      </div>

      {selected && (
        <div className="club-selected" role="status">
          <Crest club={selected} />
          <div>
            <strong>{selected.name}</strong>
            <small>
              {selected.city}/{selected.uf} · {leagueText[selected.series]}
            </small>
          </div>
          {versions.length > 1 && (
            <label>
              Base de dados
              <select value={version} onChange={(e) => onVersion(e.target.value)} disabled={disabled}>
                {versions.map((v) => (
                  <option key={v} value={v}>
                    {versionLabel(v)}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
      )}

      {shown.length === 0 ? (
        <p className="club-empty">Nenhum clube com esses filtros.</p>
      ) : (
        groupBySeries(shown).map((group) => (
          <section key={group.series} className="club-series-group">
            <h3>
              {leagueText[group.series]}
              <small>
                {group.clubs.length === 1 ? "1 clube" : `${group.clubs.length} clubes`}
                {!isPlayable(group.clubs[0], directory) && " · em breve"}
              </small>
            </h3>
            <div className="club-grid">
              {group.clubs.map((club) => {
                const playable = isPlayable(club, directory);
                const chosen = club.id === selectedId;
                return (
                  <button
                    type="button"
                    key={club.id}
                    className={"club-option" + (chosen ? " selected" : "") + (playable ? "" : " soon")}
                    onClick={() => playable && onSelect(club)}
                    aria-pressed={chosen}
                    aria-disabled={!playable}
                    disabled={disabled}
                    title={
                      playable
                        ? undefined
                        : "A Série D ainda não é jogável: seu formato (grupos e mata-mata de ida e volta) vem em uma próxima etapa."
                    }
                  >
                    <Crest club={club} />
                    <span>
                      {club.name}
                      <small>
                        {club.city} · {club.uf}
                      </small>
                    </span>
                    {chosen && <Check size={15} />}
                    {!playable && <em>Em breve</em>}
                  </button>
                );
              })}
            </div>
          </section>
        ))
      )}
    </div>
  );
}
