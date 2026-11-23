import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Database,
  Download,
  FileCheck,
  Upload,
  Plus,
  Copy,
  Search,
  Check,
} from "lucide-react";
import { api, type DataRelease, type SeasonCatalog } from "../../services/api";
import { useIdentity } from "../../hooks/useIdentity";
import { Button } from "../../components/ui/button";

function download(name: string, content: string, type: string) {
  const url = URL.createObjectURL(new Blob([content], { type }));
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export default function AdminCatalog() {
  const identity = useIdentity();
  const qc = useQueryClient();
  const [selected, setSelected] = useState("");
  const [version, setVersion] = useState("custom-2026-v1");
  const [season, setSeason] = useState(2026);
  const [source, setSource] = useState("");
  const [license, setLicense] = useState("");
  const [format, setFormat] = useState("json");
  const [content, setContent] = useState("");
  const [revision, setRevision] = useState(0);
  const [published, setPublished] = useState(false);
  const [notice, setNotice] = useState("");
  const [tab, setTab] = useState("arquivo");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(0);
  const [publishDialog, setPublishDialog] = useState(false);
  const allowed = identity.data?.isAdmin === true;
  const releases = useQuery({
    queryKey: ["admin-catalogs"],
    queryFn: () => api<DataRelease[]>("/admin/catalogs/"),
    enabled: allowed,
  });
  const template = useQuery({
    queryKey: ["catalog-template"],
    queryFn: () =>
      api<{ json: string; csv: string }>("/admin/catalogs/template"),
    enabled: allowed,
  });
  const detail = useQuery({
    queryKey: ["admin-catalog", selected],
    queryFn: () =>
      api<{ release: DataRelease; catalog: SeasonCatalog }>(
        "/admin/catalogs/" + selected,
      ),
    enabled: allowed && !!selected,
  });
  useEffect(() => {
    if (detail.data) {
      const { release: r, catalog: d } = detail.data;
      setVersion(r.version);
      setSeason(r.season);
      setSource(r.source);
      setLicense(r.license);
      setRevision(r.revision);
      setPublished(r.published);
      setContent(JSON.stringify(d, null, 2));
      setFormat("json");
      setPage(0);
    }
  }, [detail.data]);
  const parsed = useMemo(() => {
    if (format !== "json") return null;
    try {
      const d = JSON.parse(content) as SeasonCatalog;
      return Array.isArray(d.clubs) &&
        Array.isArray(d.players) &&
        d.clubs.every(
          (c) =>
            c &&
            typeof c.id === "string" &&
            typeof c.name === "string" &&
            typeof c.short === "string" &&
            typeof c.color === "string" &&
            typeof c.strength === "number",
        ) &&
        d.players.every(
          (e) =>
            e &&
            typeof e.clubId === "string" &&
            e.player &&
            typeof e.player.name === "string" &&
            typeof e.player.position === "string" &&
            ["id", "age", "rating", "value", "wage"].every(
              (k) =>
                typeof (e.player as unknown as Record<string, unknown>)[k] ===
                "number",
            ),
        )
        ? d
        : null;
    } catch {
      return null;
    }
  }, [content, format]);
  const body = { format, content, version, season, source, license, revision };
  const dirty =
    !!selected &&
    !!detail.data &&
    (format !== "json" ||
      content !== JSON.stringify(detail.data.catalog, null, 2) ||
      season !== detail.data.release.season ||
      source !== detail.data.release.source ||
      license !== detail.data.release.license);
  const validate = useMutation({
    mutationFn: () =>
      api<{ message: string; clubs: number; players: number }>(
        "/admin/catalogs/validate",
        body,
      ),
    onSuccess: (r) =>
      setNotice(`${r.message} ${r.clubs} clubes e ${r.players} jogadores.`),
  });
  const save = useMutation({
    mutationFn: () =>
      api<DataRelease>(
        selected ? `/admin/catalogs/${selected}/edit` : "/admin/catalogs/",
        body,
      ),
    onSuccess: (r) => {
      setSelected(r.id);
      setRevision(r.revision);
      setPublished(r.published);
      setNotice("Rascunho salvo. Carreiras existentes foram preservadas.");
      qc.invalidateQueries({ queryKey: ["admin-catalogs"] });
      qc.invalidateQueries({ queryKey: ["admin-catalog", r.id] });
    },
  });
  const publish = useMutation({
    mutationFn: () =>
      api<DataRelease>(`/admin/catalogs/${selected}/publish`, { revision }),
    onSuccess: (r) => {
      setPublished(true);
      setRevision(r.revision);
      setPublishDialog(false);
      setNotice("Versão publicada para novas carreiras.");
      qc.invalidateQueries({ queryKey: ["admin-catalogs"] });
      qc.invalidateQueries({ queryKey: ["catalogs"] });
      qc.invalidateQueries({ queryKey: ["clubs"] });
    },
  });
  const busy = save.isPending || publish.isPending || validate.isPending;
  const error =
    save.error ??
    publish.error ??
    validate.error ??
    releases.error ??
    template.error ??
    detail.error;
  function clearErrors() {
    save.reset();
    publish.reset();
    validate.reset();
    setNotice("");
  }
  function newDraft(copy = false) {
    clearErrors();
    setSelected("");
    setRevision(0);
    setPublished(false);
    setTab("arquivo");
    if (copy) {
      setVersion(version.slice(0, 65) + "-copia");
    } else {
      setVersion("custom-2026-v1");
      setContent("");
      setSource("");
      setLicense("");
    }
  }
  async function loadFile(file: File | undefined) {
    if (!file) return;
    clearErrors();
    if (file.size > 1_000_000) {
      setNotice("O arquivo deve ter no máximo 1 MB.");
      return;
    }
    const text = await file.text();
    setFormat(file.name.toLowerCase().endsWith(".csv") ? "csv" : "json");
    setContent(text);
    setTab("arquivo");
    if (file.name.toLowerCase().endsWith(".json")) {
      try {
        const d = JSON.parse(text);
        if (!selected) {
          if (typeof d.version === "string") setVersion(d.version);
          if (typeof d.season === "number") setSeason(d.season);
          if (typeof d.source === "string") setSource(d.source);
          if (typeof d.license === "string") setLicense(d.license);
        }
      } catch {
        setNotice("Arquivo carregado, mas o JSON precisa ser corrigido.");
      }
    }
  }
  function updateClub(index: number, key: string, value: string | number) {
    if (!parsed) return;
    const next = structuredClone(parsed);
    Object.assign(next.clubs[index], { [key]: value });
    setContent(JSON.stringify(next, null, 2));
  }
  function updatePlayer(index: number, key: string, value: string | number) {
    if (!parsed) return;
    const next = structuredClone(parsed);
    Object.assign(next.players[index].player, { [key]: value });
    setContent(JSON.stringify(next, null, 2));
  }
  const filtered =
    parsed?.players
      .map((p, index) => ({ ...p, index }))
      .filter((p) =>
        `${p.player.name} ${p.clubId} ${p.player.position}`
          .toLowerCase()
          .includes(search.toLowerCase()),
      ) ?? [];
  if (identity.isPending)
    return <p className="empty">Verificando acesso administrativo…</p>;
  if (!allowed)
    return (
      <section className="panel">
        <div className="form-stack">
          <h2>Acesso administrativo restrito</h2>
          <p>
            O operador do servidor precisa autorizar o ID da sua conta na
            configuração Admin__UserIds.
          </p>
          <code className="account-id">
            {identity.data?.id ?? identity.error?.message}
          </code>
        </div>
      </section>
    );
  return (
    <div className="catalog-admin">
      <div className="page-heading">
        <div>
          <span className="eyebrow">ADMINISTRAÇÃO DE DADOS</span>
          <h1>Uma base para cada temporada.</h1>
          <p>
            Importe, confira e publique novas versões sem modificar saves
            existentes.
          </p>
        </div>
        <Database size={30} className="green" />
      </div>
      <div className="catalog-layout">
        <aside className="panel release-list">
          <div className="panel-heading">
            <h2>Versões</h2>
            <Button variant="ghost" onClick={() => newDraft()}>
              <Plus size={15} />
              Novo
            </Button>
          </div>
          {releases.data?.map((r) => (
            <button
              key={r.id}
              className={selected === r.id ? "release selected" : "release"}
              onClick={() => {
                clearErrors();
                setSelected(r.id);
              }}
            >
              <b>{r.version}</b>
              <span>
                {r.season}
                <small className={r.published ? "positive" : "gold-text"}>
                  {r.published ? "Publicada" : "Rascunho"} · rev. {r.revision}
                </small>
              </span>
            </button>
          ))}
          {releases.isPending && <p className="notice">Carregando versões…</p>}
          <div className="notice">
            Versões publicadas são imutáveis. Duplique uma versão para atualizar
            elencos.
          </div>
        </aside>
        <section className="panel catalog-editor">
          <div className="panel-heading">
            <h2>
              {selected
                ? published
                  ? "Versão publicada"
                  : "Editar rascunho"
                : "Importar nova versão"}
            </h2>
            <Button
              variant="ghost"
              disabled={!content || busy}
              onClick={() => newDraft(true)}
            >
              <Copy size={15} />
              Duplicar
            </Button>
          </div>
          <div className="catalog-fields">
            <label>
              Identificador da versão
              <input
                maxLength={80}
                disabled={!!selected}
                value={version}
                onChange={(e) => {
                  clearErrors();
                  setVersion(e.target.value);
                }}
              />
            </label>
            <label>
              Temporada
              <input
                type="number"
                min={1900}
                max={2200}
                disabled={published}
                value={season}
                onChange={(e) => setSeason(Number(e.target.value))}
              />
            </label>
            <label className="wide">
              Fonte dos dados
              <input
                maxLength={500}
                disabled={published}
                value={source}
                onChange={(e) => setSource(e.target.value)}
                placeholder="Origem e data do levantamento"
              />
            </label>
            <label className="wide">
              Licença ou autorização
              <input
                maxLength={500}
                disabled={published}
                value={license}
                onChange={(e) => setLicense(e.target.value)}
                placeholder="Informe os direitos de utilização que você verificou"
              />
            </label>
          </div>
          <div className="catalog-filebar">
            <label
              className={
                "button outline file-picker" + (published ? " disabled" : "")
              }
            >
              <Upload size={15} />
              Abrir CSV ou JSON
              <input
                type="file"
                accept=".json,.csv"
                disabled={published || busy}
                onChange={(e) => {
                  void loadFile(e.target.files?.[0]);
                  e.target.value = "";
                }}
              />
            </label>
            <Button
              variant="ghost"
              disabled={!template.data}
              onClick={() =>
                download(
                  "catalogo-exemplo.json",
                  template.data!.json,
                  "application/json",
                )
              }
            >
              <Download size={15} />
              Modelo JSON
            </Button>
            <Button
              variant="ghost"
              disabled={!template.data}
              onClick={() =>
                download(
                  "catalogo-exemplo.csv",
                  template.data!.csv,
                  "text/csv;charset=utf-8",
                )
              }
            >
              <Download size={15} />
              Modelo CSV
            </Button>
          </div>
          <div
            className="catalog-tabs"
            role="tablist"
            aria-label="Editor de catálogo"
          >
            {[
              ["arquivo", "Arquivo"],
              ["clubes", "Clubes"],
              ["jogadores", "Jogadores"],
            ].map(([key, label]) => (
              <button
                key={key}
                role="tab"
                aria-selected={tab === key}
                onClick={() => {
                  setTab(key);
                  setPage(0);
                }}
              >
                {label}
              </button>
            ))}
          </div>
          {tab === "arquivo" ? (
            <div className="catalog-source">
              <div className="toolbar">
                <select
                  aria-label="Formato de importação"
                  disabled={published}
                  value={format}
                  onChange={(e) => setFormat(e.target.value)}
                >
                  <option value="json">JSON</option>
                  <option value="csv">CSV</option>
                </select>
                <span className="muted">
                  {content.length.toLocaleString("pt-BR")} caracteres · limite
                  de 1 milhão
                </span>
                <Button
                  variant="ghost"
                  disabled={!content}
                  onClick={() =>
                    download(
                      version + "." + format,
                      content,
                      format === "json"
                        ? "application/json"
                        : "text/csv;charset=utf-8",
                    )
                  }
                >
                  <Download size={15} />
                  Exportar
                </Button>
              </div>
              <label>
                Conteúdo do catálogo
                <textarea
                  maxLength={1_000_000}
                  spellCheck={false}
                  readOnly={published}
                  value={content}
                  onChange={(e) => {
                    clearErrors();
                    setContent(e.target.value);
                  }}
                  placeholder="Cole os dados ou carregue um arquivo usando os modelos acima."
                />
              </label>
            </div>
          ) : !parsed ? (
            <p className="notice">
              Abra uma versão JSON válida para editar as tabelas. Para CSV,
              valide e salve o rascunho: ele será aberto como JSON.
            </p>
          ) : tab === "clubes" ? (
            <div className="table-scroll">
              <table className="editor-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Nome</th>
                    <th>Sigla</th>
                    <th>Cor</th>
                    <th>Força</th>
                  </tr>
                </thead>
                <tbody>
                  {parsed.clubs.map((c, i) => (
                    <tr key={c.id}>
                      <td>{c.id}</td>
                      <td>
                        <input
                          aria-label={"Nome " + c.id}
                          disabled={published}
                          value={c.name}
                          maxLength={80}
                          onChange={(e) =>
                            updateClub(i, "name", e.target.value)
                          }
                        />
                      </td>
                      <td>
                        <input
                          aria-label={"Sigla " + c.id}
                          disabled={published}
                          value={c.short}
                          maxLength={5}
                          onChange={(e) =>
                            updateClub(i, "short", e.target.value)
                          }
                        />
                      </td>
                      <td>
                        <input
                          type="color"
                          aria-label={"Cor " + c.id}
                          disabled={published}
                          value={c.color}
                          onChange={(e) =>
                            updateClub(i, "color", e.target.value)
                          }
                        />
                      </td>
                      <td>
                        <input
                          type="number"
                          min={1}
                          max={99}
                          aria-label={"Força " + c.id}
                          disabled={published}
                          value={c.strength}
                          onChange={(e) =>
                            updateClub(i, "strength", Number(e.target.value))
                          }
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <>
              <div className="catalog-source">
                <label className="search">
                  <Search size={17} />
                  <input
                    value={search}
                    onChange={(e) => {
                      setSearch(e.target.value);
                      setPage(0);
                    }}
                    placeholder="Buscar jogador, clube ou posição…"
                  />
                </label>
              </div>
              <div className="table-scroll">
                <table className="editor-table">
                  <thead>
                    <tr>
                      <th>Clube</th>
                      <th>Jogador</th>
                      <th>Pos.</th>
                      <th>Idade</th>
                      <th>GER</th>
                      <th>Valor (R$)</th>
                      <th>Salário (R$)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.slice(page * 20, page * 20 + 20).map((e) => (
                      <tr key={e.player.id}>
                        <td>{e.clubId || "Livre"}</td>
                        <td>
                          <input
                            aria-label={"Nome do jogador " + e.player.id}
                            disabled={published}
                            value={e.player.name}
                            maxLength={80}
                            onChange={(ev) =>
                              updatePlayer(e.index, "name", ev.target.value)
                            }
                          />
                        </td>
                        <td>
                          <select
                            aria-label={"Posição do jogador " + e.player.id}
                            disabled={published}
                            value={e.player.position}
                            onChange={(ev) =>
                              updatePlayer(e.index, "position", ev.target.value)
                            }
                          >
                            {["GOL", "ZAG", "LAT", "MEI", "ATA"].map((p) => (
                              <option key={p}>{p}</option>
                            ))}
                          </select>
                        </td>
                        {(["age", "rating", "value", "wage"] as const).map(
                          (key) => (
                            <td key={key}>
                              <input
                                type="number"
                                aria-label={key + " do jogador " + e.player.id}
                                disabled={published}
                                value={e.player[key]}
                                onChange={(ev) =>
                                  updatePlayer(
                                    e.index,
                                    key,
                                    Number(ev.target.value),
                                  )
                                }
                              />
                            </td>
                          ),
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="pagination">
                <Button
                  variant="ghost"
                  disabled={page === 0}
                  onClick={() => setPage(page - 1)}
                >
                  Anterior
                </Button>
                <span>
                  {filtered.length} jogadores · página {page + 1} de{" "}
                  {Math.max(1, Math.ceil(filtered.length / 20))}
                </span>
                <Button
                  variant="ghost"
                  disabled={(page + 1) * 20 >= filtered.length}
                  onClick={() => setPage(page + 1)}
                >
                  Próxima
                </Button>
              </div>
            </>
          )}
          {error && (
            <p className="error notice" role="alert">
              {error.message}
            </p>
          )}
          {notice && (
            <p className="notice positive" role="status">
              {notice}
            </p>
          )}
          <div className="catalog-footer">
            <Button
              variant="outline"
              disabled={!content || busy}
              onClick={() => {
                clearErrors();
                validate.mutate();
              }}
            >
              <FileCheck size={16} />
              Validar dados
            </Button>
            <Button
              disabled={published || !content || busy || detail.isFetching}
              onClick={() => {
                clearErrors();
                save.mutate();
              }}
            >
              {save.isPending ? "Salvando…" : "Salvar rascunho"}
            </Button>
            <Button
              variant="outline"
              disabled={
                !selected ||
                published ||
                busy ||
                dirty ||
                detail.isFetching ||
                revision === 0
              }
              onClick={() => setPublishDialog(true)}
            >
              <Check size={16} />
              Publicar versão
            </Button>
            {dirty && (
              <small className="muted">
                Salve as alterações antes de publicar.
              </small>
            )}
          </div>
        </section>
      </div>
      {publishDialog && (
        <div className="modal-backdrop">
          <section
            className="panel modal"
            role="dialog"
            aria-modal="true"
            aria-label="Publicar versão de dados"
          >
            <h2>Publicar {version}?</h2>
            <p>
              A última revisão salva ficará disponível para novas carreiras.
              Depois de publicada, essa versão não poderá ser editada.
              Alterações ainda não salvas não serão publicadas.
            </p>
            {publish.error && (
              <p className="error" role="alert">
                {publish.error.message}
              </p>
            )}
            <div className="actions">
              <Button disabled={busy} onClick={() => publish.mutate()}>
                Publicar revisão {revision}
              </Button>
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => setPublishDialog(false)}
              >
                Voltar
              </Button>
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
