import { useQuery } from "@tanstack/react-query";
import { DataTable } from "../../components/DataTable";
type SourceData = {
  season: number;
  asOf: string;
  source: string;
  status: string;
  providerNotice: string;
  clubs: {
    id: string;
    name: string;
    wikidata: string | null;
    evidenceCount: number;
    rosterStatus: string;
    crestStatus: string;
  }[];
};
type Credits = {
  assets: {
    name: string;
    author: string;
    license: string;
    notes: string;
    source?: string;
    path?: string;
    licenseFile?: string;
    bytes?: number;
  }[];
};
export default function DataSources() {
  const crests = useQuery<{
    clubs: {
      clubId: string;
      name: string;
      status: string;
      path: string | null;
      source?: string;
      author?: string;
      license?: string;
      licenseUrl?: string;
      reason?: string;
    }[];
    notice: string;
  }>({
    queryKey: ["crest-credits"],
    queryFn: async () => {
      const r = await fetch("/data/crest-credits.json");
      if (!r.ok) throw new Error("Créditos indisponíveis");
      return r.json();
    },
    staleTime: Infinity,
  });
  const data = useQuery({
    queryKey: ["real-data-2026"],
    queryFn: async () => {
      const r = await fetch("/data/real-2026.json");
      if (!r.ok) throw new Error("Não foi possível carregar o levantamento.");
      return r.json() as Promise<SourceData>;
    },
    staleTime: Infinity,
  });
  const credits = useQuery({
    queryKey: ["asset-credits"],
    queryFn: async () => {
      const r = await fetch("/data/asset-credits.json");
      if (!r.ok) throw new Error("Não foi possível carregar os créditos.");
      return r.json() as Promise<Credits>;
    },
    staleTime: Infinity,
  });
  return (
    <div className="form-stack">
      <section className="panel" style={{ padding: 24 }}>
        <h2>Brasileirão 2026 · fontes e cobertura</h2>
        <p>
          Os participantes abaixo foram conferidos na CBF. Os elencos reais
          ainda estão em revisão e não substituem os atletas fictícios das
          carreiras atuais.
        </p>
        {data.error && (
          <p role="alert" className="error">
            {data.error.message}
          </p>
        )}
        {data.data && (
          <>
            <p className="notice">
              {data.data.status} Referência: {data.data.asOf}.{" "}
              {data.data.providerNotice}
            </p>
            <p>
              <a href={data.data.source} target="_blank" rel="noreferrer">
                Participantes na CBF
              </a>{" "}
              ·{" "}
              <a
                href="https://www.wikidata.org/wiki/Wikidata:Licensing"
                target="_blank"
                rel="noreferrer"
              >
                Dados estruturados Wikidata: CC0
              </a>
            </p>
            <DataTable
              data={data.data.clubs}
              columns={[
                { accessorKey: "name", header: "Clube" },
                {
                  accessorKey: "evidenceCount",
                  header: "Atletas com evidência encontrada",
                  cell: (x) =>
                    x.getValue()
                      ? `${x.getValue()} · revisão pendente`
                      : "Coleta pendente/incompleta",
                },
                { accessorKey: "rosterStatus", header: "Elenco de 2026" },
                { accessorKey: "crestStatus", header: "Escudo" },
                {
                  id: "source",
                  header: "Fonte aberta",
                  cell: (x) =>
                    x.row.original.wikidata ? (
                      <a
                        href={x.row.original.wikidata}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Wikidata
                      </a>
                    ) : (
                      "Identificador a conferir"
                    ),
                },
              ]}
            />
            <p>
              Não há preenchimento automático com jogadores inventados nesta
              base. A importação de um catálogo jogável continua exigindo os
              vinte elencos completos e metadados de origem/licença no editor
              administrativo.
            </p>
          </>
        )}
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Calendário das temporadas</h2>
        <p>
          O jogo gera confrontos, datas e horários próprios para cada ano. A
          próxima temporada recebe uma nova tabela, sem depender de jogos reais
          ou remarcações da CBF.
        </p>
        <p>
          Cada clube disputa 38 rodadas, com 19 jogos em casa e 19 fora. Os
          jogos ficam distribuídos pelos fins de semana, com descanso entre
          rodadas e última rodada simultânea. O mesmo vale para as duas
          divisões.
        </p>
        <p>
          As datas fazem parte da simulação. A partida do seu clube exige acompanhar a transmissão até o apito final.
          Partidas já salvas mantêm confrontos, resultados e replays.
        </p>
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Sons do estádio</h2>
        <p>Gravações de ambiente, gol e quase gol por Sandermotions; chute por bittermelonheart; apito por Rosa-Orenes256. Cinco gravações sob CC0 1.0, com espacialização e mixagem no jogo.</p>
        <p><a href="/assets/audio/ATTRIBUTION.txt" target="_blank" rel="noreferrer">Autores, fontes e licenças dos sons</a>{" · "}<a href="/assets/audio/credits.json" target="_blank" rel="noreferrer">Manifesto e hashes dos arquivos</a></p>
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Créditos e materiais 3D</h2>
        {credits.error && (
          <p role="alert" className="error">
            {credits.error.message}
          </p>
        )}
        {credits.data?.assets.map((a) => (
          <div
            className="training-attribute"
            key={a.name}
            style={{ marginBottom: 16 }}
          >
            <h3>{a.name}</h3>
            <p>
              {a.author} · {a.license}
              {a.bytes ? ` · ${(a.bytes / 1024 / 1024).toFixed(2)} MB` : ""}
            </p>
            <p>{a.notes}</p>
            {a.source && (
              <p>
                <a href={a.source} target="_blank" rel="noreferrer">
                  Fonte do autor
                </a>{" "}
                ·{" "}
                <a href={a.licenseFile} target="_blank" rel="noreferrer">
                  Licença incluída
                </a>
              </p>
            )}
          </div>
        ))}
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Escudos e licenças individuais</h2>
        <p>{crests.data?.notice}</p>
        <p>
          <a
            href="/assets/crests/ATTRIBUTION.txt"
            target="_blank"
            rel="noreferrer"
          >
            Atribuição completa dos arquivos incluídos
          </a>
        </p>
        {crests.data?.clubs.map((c) => (
          <div
            key={c.clubId}
            className="training-attribute"
            style={{ marginBottom: 12 }}
          >
            {c.path && (
              <img
                src={c.path}
                alt={c.name}
                width={36}
                height={40}
                style={{ objectFit: "contain" }}
              />
            )}
            <h3>
              {c.name} · {c.status === "included" ? "Incluído" : "Pendente"}
            </h3>
            <p>
              {c.author} · {c.license ?? "Origem/licença ainda não aprovada"}
            </p>
            {c.source && (
              <p>
                <a href={c.source} target="_blank" rel="noreferrer">
                  Arquivo e condições
                </a>
                {" · "}
                <a href={c.licenseUrl} target="_blank" rel="noreferrer">
                  Licença
                </a>
              </p>
            )}
            {c.reason && <p>{c.reason}</p>}
          </div>
        ))}
      </section>
    </div>
  );
}
