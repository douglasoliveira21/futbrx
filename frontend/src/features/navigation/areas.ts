// Six areas organise the career screens. Each page keeps its own route (links elsewhere in the
// game keep working); the sidebar shows only the areas and each area shows its pages as tabs.
export type AreaId = "inicio" | "elenco" | "jogo" | "mercado" | "clube" | "carreira";
export type AreaPage = { path: string; label: string };
export type Area = { id: AreaId; label: string; pages: AreaPage[] };

export const areas: Area[] = [
  { id: "inicio", label: "Início", pages: [{ path: "", label: "Visão geral" }] },
  {
    id: "elenco",
    label: "Elenco",
    pages: [
      { path: "elenco", label: "Jogadores" },
      { path: "treinamentos", label: "Treinamentos" },
      { path: "medico", label: "Médico" },
      { path: "base", label: "Base" },
      { path: "vestiario", label: "Vestiário" },
      { path: "estatisticas", label: "Estatísticas" },
    ],
  },
  {
    id: "jogo",
    label: "Jogo",
    pages: [
      { path: "taticas", label: "Táticas" },
      { path: "calendario", label: "Calendário" },
      { path: "competicoes", label: "Competições" },
    ],
  },
  {
    id: "mercado",
    label: "Mercado",
    pages: [
      { path: "transferencias", label: "Transferências" },
      { path: "contratos", label: "Contratos" },
      { path: "empresarios", label: "Empresários" },
      { path: "olheiros", label: "Olheiros" },
      { path: "gestao-avancada", label: "Gestão avançada" },
    ],
  },
  {
    id: "clube",
    label: "Clube",
    pages: [
      { path: "financas", label: "Finanças" },
      { path: "diretoria", label: "Diretoria" },
      { path: "torcida", label: "Torcida" },
      { path: "imprensa", label: "Imprensa" },
      { path: "comissao", label: "Comissão técnica" },
    ],
  },
  {
    id: "carreira",
    label: "Carreira",
    pages: [
      { path: "minha-carreira", label: "Minha carreira" },
      { path: "mercado-tecnicos", label: "Mercado de técnicos" },
      { path: "historico", label: "Histórico" },
      { path: "visual", label: "Estúdio do jogador" },
      { path: "fontes", label: "Dados e licenças" },
    ],
  },
];

// Accepts a location pathname ("/elenco", "/elenco/", "/") or a route path ("elenco").
export function areaOf(pathname: string): Area | undefined {
  const path = pathname.replace(/^\/+|\/+$/g, "");
  return areas.find((a) => a.pages.some((p) => p.path === path));
}
