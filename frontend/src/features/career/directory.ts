// Directory of the clubs of Séries A to D (with state and city) used by the career picker.
export type SeriesId = "A" | "B" | "C" | "D";
export type DirectoryClub = {
  id: string;
  series: SeriesId;
  name: string;
  short: string;
  color: string;
  strength: number;
  city: string;
  uf: string;
  check?: string;
  // Série D clubs start in the data version of their group of six.
  group?: number;
  version?: string;
};
export type Directory = {
  season: number;
  asOf: string;
  status: string;
  versions: Partial<Record<SeriesId, string[]>>;
  clubs: DirectoryClub[];
};
export type ReleaseRef = { id: string; version: string };

export const seriesList: SeriesId[] = ["A", "B", "C", "D"];
export const stateNames: Record<string, string> = {
  AC: "Acre", AL: "Alagoas", AP: "Amapá", AM: "Amazonas", BA: "Bahia", CE: "Ceará",
  DF: "Distrito Federal", ES: "Espírito Santo", GO: "Goiás", MA: "Maranhão", MT: "Mato Grosso",
  MS: "Mato Grosso do Sul", MG: "Minas Gerais", PA: "Pará", PB: "Paraíba", PR: "Paraná",
  PE: "Pernambuco", PI: "Piauí", RJ: "Rio de Janeiro", RN: "Rio Grande do Norte",
  RS: "Rio Grande do Sul", RO: "Rondônia", RR: "Roraima", SC: "Santa Catarina",
  SP: "São Paulo", SE: "Sergipe", TO: "Tocantins",
};

// A series can be played when the server has a data version for it.
export const isPlayable = (club: DirectoryClub, directory: Pick<Directory, "versions">) =>
  (directory.versions[club.series]?.length ?? 0) > 0;

const plain = (text: string) =>
  text.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();

export type Filter = { series: SeriesId | "all"; uf: string; search: string };

export function filterClubs(clubs: DirectoryClub[], filter: Filter) {
  const words = plain(filter.search).split(/\s+/).filter(Boolean);
  return clubs
    .filter((club) => filter.series === "all" || club.series === filter.series)
    .filter((club) => !filter.uf || club.uf === filter.uf)
    .filter((club) => {
      const haystack = plain(`${club.name} ${club.city} ${stateNames[club.uf] ?? ""} ${club.short}`);
      return words.every((word) => haystack.includes(word));
    })
    .sort(
      (a, b) =>
        seriesList.indexOf(a.series) - seriesList.indexOf(b.series) ||
        a.name.localeCompare(b.name, "pt-BR"),
    );
}
// How many clubs each state/series would show with the other filters applied.
export function countBy(clubs: DirectoryClub[], key: "uf" | "series") {
  const counts: Record<string, number> = {};
  for (const club of clubs) counts[club[key]] = (counts[club[key]] ?? 0) + 1;
  return counts;
}
export function groupBySeries(clubs: DirectoryClub[]) {
  return seriesList
    .map((series) => ({ series, clubs: clubs.filter((club) => club.series === series) }))
    .filter((group) => group.clubs.length > 0);
}
// The data version a new career of this club starts from. `preferred` lets the player choose among
// several versions of the same series (Série A has two); an unknown choice falls back to the first.
export function releaseFor<R extends ReleaseRef>(
  club: DirectoryClub,
  directory: Pick<Directory, "versions">,
  releases: R[],
  preferred = "",
): R | undefined {
  const versions = club.version ? [club.version] : (directory.versions[club.series] ?? []);
  const version = versions.includes(preferred) ? preferred : versions.find((v) => releases.some((r) => r.version === v));
  return releases.find((release) => release.version === version);
}
export const versionLabel = (version: string) =>
  version === "serie-a-2026-misto-candidatos-v2"
    ? "Jogadores reais parciais, vínculos não verificados"
    : version === "serie-a-2026-clubes-reais-v1"
      ? "Clubes reais, elencos simulados"
      : version;

// The league the career plays in: Série B and C careers start from their own data version.
export function leagueName(state: { dataVersion: string; division?: string }) {
  const series = state.dataVersion.startsWith("serie-b-")
    ? "B"
    : state.dataVersion.startsWith("serie-c-")
      ? "C"
      : state.dataVersion.startsWith("serie-d-")
        ? "D"
        : (state.division ?? "A");
  return `Brasileirão Série ${series}`;
}
