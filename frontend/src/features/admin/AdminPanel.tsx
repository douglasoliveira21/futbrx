import { lazy, Suspense, useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Save, Search, Trash2, Upload, Users } from "lucide-react";
import {
  api,
  type Club,
  type DataRelease,
  type Player,
  type SeasonCatalog,
} from "../../services/api";
import { useIdentity } from "../../hooks/useIdentity";
import { Button } from "../../components/ui/button";
import { Crest } from "../../components/Crest";
import type { Directory, DirectoryClub, SeriesId } from "../career/directory";

const AdminCatalog = lazy(() => import("../catalog/AdminCatalog"));
const POSITIONS = ["GOL", "ZAG", "LAT", "MEI", "ATA"];
const PAGE = 15;

type AdminAccount = {
  id: string;
  name: string;
  email: string;
  careers: number;
  sessions: number;
  lastAccess: string | null;
  isAdmin: boolean;
};
type CatalogDetail = { release: DataRelease; catalog: SeasonCatalog };

function useReleases(enabled: boolean) {
  return useQuery({
    queryKey: ["admin-catalogs"],
    queryFn: () => api<DataRelease[]>("/admin/catalogs/"),
    enabled,
  });
}
function useCatalog(rid: string) {
  return useQuery({
    queryKey: ["admin-catalog", rid],
    queryFn: () => api<CatalogDetail>("/admin/catalogs/" + rid),
    enabled: !!rid,
  });
}
function ReleasePicker({
  value,
  onChange,
}: {
  value: string;
  onChange: (id: string) => void;
}) {
  const releases = useReleases(true);
  useEffect(() => {
    if (!value && releases.data?.length)
      onChange((releases.data.find((r) => r.published) ?? releases.data[0]).id);
  }, [value, releases.data, onChange]);
  return (
    <label>
      Base de dados
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        {releases.data?.map((r) => (
          <option key={r.id} value={r.id}>
            {r.version} {r.published ? "" : "(rascunho)"}
          </option>
        ))}
      </select>
    </label>
  );
}
const dateText = (v: string | null) =>
  v ? new Date(v).toLocaleString("pt-BR") : "—";

// ---------------------------------------------------------------- Contas
type Dialog = {
  kind: "rename" | "password" | "revoke" | "delete";
  account: AdminAccount;
} | null;

function AccountsTab() {
  const qc = useQueryClient();
  const [q, setQ] = useState("");
  const [term, setTerm] = useState("");
  const [dialog, setDialog] = useState<Dialog>(null);
  const [value, setValue] = useState("");
  const [notice, setNotice] = useState("");
  useEffect(() => {
    const t = setTimeout(() => setTerm(q), 300);
    return () => clearTimeout(t);
  }, [q]);
  const accounts = useQuery({
    queryKey: ["admin-accounts", term],
    queryFn: () =>
      api<AdminAccount[]>("/admin/accounts?q=" + encodeURIComponent(term)),
  });
  const act = useMutation({
    mutationFn: async () => {
      if (!dialog) return;
      const base = "/admin/accounts/" + dialog.account.id;
      if (dialog.kind === "rename")
        await api(base + "/rename", { name: value });
      else if (dialog.kind === "password")
        await api(base + "/reset-password", { password: value });
      else if (dialog.kind === "revoke")
        await api(base + "/revoke-sessions", {});
      else await api(base, undefined, true, "DELETE");
    },
    onSuccess: () => {
      setNotice(
        {
          rename: "Nome alterado.",
          password: "Senha redefinida. As sessões da conta foram encerradas.",
          revoke: "Sessões encerradas.",
          delete: "Conta apagada junto com as carreiras dela.",
        }[dialog!.kind],
      );
      setDialog(null);
      qc.invalidateQueries({ queryKey: ["admin-accounts"] });
    },
  });
  function open(kind: NonNullable<Dialog>["kind"], account: AdminAccount) {
    act.reset();
    setNotice("");
    setValue(kind === "rename" ? account.name : "");
    setDialog({ kind, account });
  }
  const needsValue = dialog?.kind === "rename" || dialog?.kind === "password";
  return (
    <>
      <div className="catalog-source">
        <label className="search">
          <Search size={17} />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Buscar por nome ou e-mail…"
          />
        </label>
      </div>
      <div className="table-scroll">
        <table className="editor-table">
          <thead>
            <tr>
              <th>Nome</th>
              <th>E-mail</th>
              <th>Carreiras</th>
              <th>Sessões</th>
              <th>Último acesso</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>
            {accounts.data?.map((a) => (
              <tr key={a.id}>
                <td>
                  {a.name} {a.isAdmin && <b className="green">ADMIN</b>}
                </td>
                <td>{a.email}</td>
                <td>{a.careers}</td>
                <td>{a.sessions}</td>
                <td>{dateText(a.lastAccess)}</td>
                <td className="actions">
                  <Button variant="ghost" onClick={() => open("rename", a)}>
                    Renomear
                  </Button>
                  <Button variant="ghost" onClick={() => open("password", a)}>
                    Nova senha
                  </Button>
                  <Button variant="ghost" onClick={() => open("revoke", a)}>
                    Encerrar sessões
                  </Button>
                  <Button variant="ghost" onClick={() => open("delete", a)}>
                    <Trash2 size={14} /> Apagar
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {accounts.isPending && <p className="notice">Carregando contas…</p>}
      {accounts.data?.length === 0 && (
        <p className="notice">Nenhuma conta encontrada.</p>
      )}
      {accounts.error && (
        <p className="error notice" role="alert">
          {accounts.error.message}
        </p>
      )}
      {notice && (
        <p className="notice positive" role="status">
          {notice}
        </p>
      )}
      {dialog && (
        <div className="modal-backdrop">
          <section className="panel modal" role="dialog" aria-modal="true">
            <h2>
              {
                {
                  rename: "Renomear conta",
                  password: "Definir nova senha",
                  revoke: "Encerrar sessões",
                  delete: "Apagar conta",
                }[dialog.kind]
              }
            </h2>
            <p>
              {dialog.account.name} · {dialog.account.email}
            </p>
            {dialog.kind === "delete" && (
              <p>
                A conta e todas as {dialog.account.careers} carreiras dela serão
                apagadas. Isso não pode ser desfeito.
              </p>
            )}
            {dialog.kind === "revoke" && (
              <p>A pessoa precisará entrar de novo em todos os aparelhos.</p>
            )}
            {needsValue && (
              <label>
                {dialog.kind === "rename"
                  ? "Novo nome"
                  : "Nova senha (12 a 128 caracteres)"}
                <input
                  type={dialog.kind === "password" ? "text" : "text"}
                  autoComplete="off"
                  maxLength={dialog.kind === "rename" ? 60 : 128}
                  value={value}
                  onChange={(e) => setValue(e.target.value)}
                />
              </label>
            )}
            {act.error && (
              <p className="error" role="alert">
                {act.error.message}
              </p>
            )}
            <div className="actions">
              <Button
                disabled={act.isPending || (needsValue && !value.trim())}
                onClick={() => act.mutate()}
              >
                {dialog.kind === "delete" ? "Apagar definitivamente" : "Confirmar"}
              </Button>
              <Button variant="outline" onClick={() => setDialog(null)}>
                Cancelar
              </Button>
            </div>
          </section>
        </div>
      )}
    </>
  );
}

// ----------------------------------------------------------------- Times
async function toLogoDataUri(file: File): Promise<string> {
  if (!/^image\/(png|jpeg|webp)$/.test(file.type))
    throw new Error("Use uma imagem PNG, JPEG ou WebP.");
  const bmp = await createImageBitmap(file);
  const scale = Math.min(1, 256 / Math.max(bmp.width, bmp.height));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bmp.width * scale));
  canvas.height = Math.max(1, Math.round(bmp.height * scale));
  canvas.getContext("2d")!.drawImage(bmp, 0, 0, canvas.width, canvas.height);
  let uri = canvas.toDataURL("image/png");
  if (uri.length > 270_000) uri = canvas.toDataURL("image/webp", 0.85);
  if (uri.length > 270_000) throw new Error("A imagem ficou grande demais.");
  return uri;
}

function ClubRow({
  club,
  rid,
  onDone,
}: {
  club: Club;
  rid: string;
  onDone: (message: string) => void;
}) {
  const qc = useQueryClient();
  const [form, setForm] = useState(club);
  const dirty = JSON.stringify(form) !== JSON.stringify(club);
  const save = useMutation({
    mutationFn: () =>
      api(`/admin/releases/${rid}/clubs/${club.id}`, {
        name: form.name,
        short: form.short,
        color: form.color,
        strength: form.strength,
      }),
    onSuccess: () => {
      onDone(`${form.name} salvo. Vale para carreiras novas.`);
      qc.invalidateQueries({ queryKey: ["admin-catalog", rid] });
      qc.invalidateQueries({ queryKey: ["clubs"] });
    },
  });
  const logo = useMutation({
    mutationFn: async (file: File | null) => {
      if (file === null)
        await api(`/admin/clubs/${club.id}/logo`, undefined, true, "DELETE");
      else
        await api(`/admin/clubs/${club.id}/logo`, {
          dataUri: await toLogoDataUri(file),
        });
    },
    onSuccess: () => {
      onDone("Escudo atualizado.");
      qc.invalidateQueries({ queryKey: ["logos"] });
    },
  });
  const set = (patch: Partial<Club>) => setForm({ ...form, ...patch });
  return (
    <tr>
      <td>
        <Crest club={form} />
      </td>
      <td>{club.id}</td>
      <td>
        <input
          aria-label={"Nome " + club.id}
          maxLength={80}
          value={form.name}
          onChange={(e) => set({ name: e.target.value })}
        />
      </td>
      <td>
        <input
          aria-label={"Sigla " + club.id}
          maxLength={5}
          value={form.short}
          onChange={(e) => set({ short: e.target.value })}
        />
      </td>
      <td>
        <input
          type="color"
          aria-label={"Cor " + club.id}
          value={form.color}
          onChange={(e) => set({ color: e.target.value })}
        />
      </td>
      <td>
        <input
          type="number"
          min={1}
          max={99}
          aria-label={"Força " + club.id}
          value={form.strength}
          onChange={(e) => set({ strength: Number(e.target.value) })}
        />
      </td>
      <td className="actions">
        <label className="button outline file-picker">
          <Upload size={14} /> Escudo
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            onChange={(e) => {
              const f = e.target.files?.[0];
              e.target.value = "";
              if (f) logo.mutate(f);
            }}
          />
        </label>
        <Button variant="ghost" disabled={logo.isPending} onClick={() => logo.mutate(null)}>
          Tirar escudo
        </Button>
        <Button disabled={!dirty || save.isPending} onClick={() => save.mutate()}>
          <Save size={14} /> Salvar
        </Button>
        {(save.error || logo.error) && (
          <small className="error" role="alert">
            {(save.error ?? logo.error)!.message}
          </small>
        )}
      </td>
    </tr>
  );
}

function TeamsTab() {
  const [rid, setRid] = useState("");
  const [notice, setNotice] = useState("");
  const catalog = useCatalog(rid);
  return (
    <>
      <div className="catalog-fields">
        <ReleasePicker value={rid} onChange={setRid} />
      </div>
      <p className="notice">
        As edições valem para carreiras novas. Carreiras já criadas guardam uma
        cópia própria e não mudam. Para criar, remover ou mudar a série de um
        time, use a aba Séries.
      </p>
      <div className="table-scroll">
        <table className="editor-table">
          <thead>
            <tr>
              <th>Escudo</th>
              <th>ID</th>
              <th>Nome</th>
              <th>Sigla</th>
              <th>Cor</th>
              <th>Força</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>
            {catalog.data?.catalog.clubs.map((c) => (
              <ClubRow
                key={c.id + JSON.stringify(c)}
                club={c}
                rid={rid}
                onDone={setNotice}
              />
            ))}
          </tbody>
        </table>
      </div>
      {catalog.isPending && !!rid && <p className="notice">Carregando times…</p>}
      {catalog.error && (
        <p className="error notice" role="alert">
          {catalog.error.message}
        </p>
      )}
      {notice && (
        <p className="notice positive" role="status">
          {notice}
        </p>
      )}
    </>
  );
}

// ------------------------------------------------------------- Jogadores
type Row = { clubId: string; player: Player };

function PlayerRow({
  row,
  rid,
  clubs,
  onDone,
}: {
  row: Row;
  rid: string;
  clubs: Club[];
  onDone: (message: string) => void;
}) {
  const qc = useQueryClient();
  const [form, setForm] = useState({
    name: row.player.name,
    position: row.player.position,
    age: row.player.age,
    rating: row.player.rating,
    value: row.player.value,
    wage: row.player.wage,
    clubId: row.clubId,
  });
  const original = {
    name: row.player.name,
    position: row.player.position,
    age: row.player.age,
    rating: row.player.rating,
    value: row.player.value,
    wage: row.player.wage,
    clubId: row.clubId,
  };
  const dirty = JSON.stringify(form) !== JSON.stringify(original);
  const refresh = () => qc.invalidateQueries({ queryKey: ["admin-catalog", rid] });
  const save = useMutation({
    mutationFn: () => api(`/admin/releases/${rid}/players/${row.player.id}`, form),
    onSuccess: () => {
      onDone(`${form.name} salvo. Vale para carreiras novas.`);
      refresh();
    },
  });
  const remove = useMutation({
    mutationFn: () =>
      api(`/admin/releases/${rid}/players/${row.player.id}`, undefined, true, "DELETE"),
    onSuccess: () => {
      onDone(`${row.player.name} removido.`);
      refresh();
    },
  });
  const num = (key: "age" | "rating" | "value" | "wage") => (
    <td>
      <input
        type="number"
        aria-label={`${key} de ${row.player.name}`}
        value={form[key]}
        onChange={(e) => setForm({ ...form, [key]: Number(e.target.value) })}
      />
    </td>
  );
  return (
    <tr>
      <td>
        <select
          aria-label={"Clube de " + row.player.name}
          value={form.clubId}
          onChange={(e) => setForm({ ...form, clubId: e.target.value })}
        >
          <option value="">Livre</option>
          {clubs.map((c) => (
            <option key={c.id} value={c.id}>
              {c.short}
            </option>
          ))}
        </select>
      </td>
      <td>
        <input
          aria-label={"Nome do jogador " + row.player.id}
          maxLength={80}
          value={form.name}
          onChange={(e) => setForm({ ...form, name: e.target.value })}
        />
      </td>
      <td>
        <select
          aria-label={"Posição de " + row.player.name}
          value={form.position}
          onChange={(e) => setForm({ ...form, position: e.target.value })}
        >
          {POSITIONS.map((p) => (
            <option key={p}>{p}</option>
          ))}
        </select>
      </td>
      {num("age")}
      {num("rating")}
      {num("value")}
      {num("wage")}
      <td className="actions">
        <Button disabled={!dirty || save.isPending} onClick={() => save.mutate()}>
          <Save size={14} /> Salvar
        </Button>
        <Button variant="ghost" disabled={remove.isPending} onClick={() => remove.mutate()}>
          <Trash2 size={14} /> Remover
        </Button>
        {(save.error || remove.error) && (
          <small className="error" role="alert">
            {(save.error ?? remove.error)!.message}
          </small>
        )}
      </td>
    </tr>
  );
}

function AddPlayer({
  rid,
  clubs,
  clubId,
  onDone,
}: {
  rid: string;
  clubs: Club[];
  clubId: string;
  onDone: (message: string) => void;
}) {
  const qc = useQueryClient();
  const blank = { name: "", position: "ATA", age: 20, rating: 60, value: 1000000, wage: 20000 };
  const [form, setForm] = useState(blank);
  const [club, setClub] = useState(clubId || clubs[0]?.id || "");
  useEffect(() => {
    if (clubId) setClub(clubId);
    else if (!club && clubs[0]) setClub(clubs[0].id);
  }, [clubId, clubs, club]);
  const add = useMutation({
    mutationFn: () => api(`/admin/releases/${rid}/players`, { ...form, clubId: club }),
    onSuccess: () => {
      onDone(`${form.name} adicionado.`);
      setForm(blank);
      qc.invalidateQueries({ queryKey: ["admin-catalog", rid] });
    },
  });
  return (
    <div className="catalog-fields">
      <label>
        Clube
        <select value={club} onChange={(e) => setClub(e.target.value)}>
          {clubs.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Nome
        <input maxLength={80} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
      </label>
      <label>
        Posição
        <select value={form.position} onChange={(e) => setForm({ ...form, position: e.target.value })}>
          {POSITIONS.map((p) => (
            <option key={p}>{p}</option>
          ))}
        </select>
      </label>
      {(["age", "rating", "value", "wage"] as const).map((k) => (
        <label key={k}>
          {{ age: "Idade", rating: "Overall", value: "Valor (R$)", wage: "Salário (R$)" }[k]}
          <input type="number" value={form[k]} onChange={(e) => setForm({ ...form, [k]: Number(e.target.value) })} />
        </label>
      ))}
      <Button disabled={!form.name.trim() || !club || add.isPending} onClick={() => add.mutate()}>
        <Plus size={15} /> Adicionar jogador
      </Button>
      {add.error && (
        <p className="error" role="alert">
          {add.error.message}
        </p>
      )}
    </div>
  );
}

function PlayersTab() {
  const [rid, setRid] = useState("");
  const [clubId, setClubId] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(0);
  const [notice, setNotice] = useState("");
  const catalog = useCatalog(rid);
  const clubs = catalog.data?.catalog.clubs ?? [];
  const filtered = (catalog.data?.catalog.players ?? []).filter(
    (p) =>
      (!clubId || p.clubId === (clubId === "_free" ? "" : clubId)) &&
      p.player.name.toLowerCase().includes(search.toLowerCase()),
  );
  return (
    <>
      <div className="catalog-fields">
        <ReleasePicker
          value={rid}
          onChange={(id) => {
            setRid(id);
            setClubId("");
            setPage(0);
          }}
        />
        <label>
          Clube
          <select
            value={clubId}
            onChange={(e) => {
              setClubId(e.target.value);
              setPage(0);
            }}
          >
            <option value="">Todos</option>
            <option value="_free">Livres</option>
            {clubs.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      <p className="notice">
        As edições valem para carreiras novas. Cada clube precisa manter de 18
        a 40 jogadores, com pelo menos 2 goleiros.
      </p>
      {rid && clubs.length > 0 && (
        <AddPlayer rid={rid} clubs={clubs} clubId={clubId === "_free" ? "" : clubId} onDone={setNotice} />
      )}
      <div className="catalog-source">
        <label className="search">
          <Search size={17} />
          <input
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(0);
            }}
            placeholder="Buscar jogador…"
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
              <th>Overall</th>
              <th>Valor (R$)</th>
              <th>Salário (R$)</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>
            {filtered.slice(page * PAGE, page * PAGE + PAGE).map((p) => (
              <PlayerRow
                key={p.player.id + JSON.stringify(p)}
                row={p}
                rid={rid}
                clubs={clubs}
                onDone={setNotice}
              />
            ))}
          </tbody>
        </table>
      </div>
      <div className="pagination">
        <Button variant="ghost" disabled={page === 0} onClick={() => setPage(page - 1)}>
          Anterior
        </Button>
        <span>
          {filtered.length} jogadores · página {page + 1} de{" "}
          {Math.max(1, Math.ceil(filtered.length / PAGE))}
        </span>
        <Button variant="ghost" disabled={(page + 1) * PAGE >= filtered.length} onClick={() => setPage(page + 1)}>
          Próxima
        </Button>
      </div>
      {catalog.error && (
        <p className="error notice" role="alert">
          {catalog.error.message}
        </p>
      )}
      {notice && (
        <p className="notice positive" role="status">
          {notice}
        </p>
      )}
    </>
  );
}

// ---------------------------------------------------------------- Séries
const PLAYABLE: SeriesId[] = ["A", "B", "C"];

function SeriesTab() {
  const qc = useQueryClient();
  const directory = useQuery({
    queryKey: ["admin-directory"],
    queryFn: () => api<Directory>("/directory"),
  });
  const [notice, setNotice] = useState("");
  const clubs = directory.data?.clubs ?? [];
  const bySeries = (s: SeriesId) =>
    clubs.filter((c) => c.series === s).sort((a, b) => a.name.localeCompare(b.name, "pt-BR"));
  const label = (c: DirectoryClub) => `Série ${c.series} · ${c.name}`;
  const playable = clubs
    .filter((c) => c.series !== "D")
    .sort((a, b) => a.series.localeCompare(b.series) || a.name.localeCompare(b.name, "pt-BR"));
  const refresh = () => {
    for (const key of ["admin-directory", "directory", "admin-catalog", "admin-catalogs", "clubs", "catalogs"])
      qc.invalidateQueries({ queryKey: [key] });
  };

  const [clubA, setClubA] = useState("");
  const [clubB, setClubB] = useState("");
  const swap = useMutation({
    mutationFn: () => api("/admin/clubs/swap", { clubA, clubB }),
    onSuccess: () => {
      setNotice("Times trocados de série. Vale para carreiras novas.");
      setClubA("");
      setClubB("");
      refresh();
    },
  });

  const blank = { id: "", name: "", short: "", color: "#4faa79", strength: 70, city: "", uf: "" };
  const [series, setSeries] = useState<SeriesId>("C");
  const [outId, setOutId] = useState("");
  const [form, setForm] = useState(blank);
  const [preset, setPreset] = useState("");
  const replace = useMutation({
    mutationFn: () => api<{ id: string }>(`/admin/series/${series}/replace`, { outClubId: outId, ...form }),
    onSuccess: () => {
      setNotice(`${form.name} entrou na Série ${series}. O time substituído foi para a Série D.`);
      setForm(blank);
      setOutId("");
      setPreset("");
      refresh();
    },
  });
  function usePreset(id: string) {
    setPreset(id);
    const c = clubs.find((x) => x.id === id);
    if (c)
      setForm({ id: c.id, name: c.name, short: c.short, color: c.color, strength: c.strength, city: c.city, uf: c.uf });
  }
  const set = (patch: Partial<typeof blank>) => setForm({ ...form, ...patch });
  const clubSelect = (value: string, onChange: (v: string) => void, list: DirectoryClub[]) => (
    <select value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">Escolha um time…</option>
      {list.map((c) => (
        <option key={c.id} value={c.id}>
          {label(c)}
        </option>
      ))}
    </select>
  );
  return (
    <>
      <p className="notice">
        As Séries A, B e C têm sempre 20 times, por isso adicionar e remover acontecem juntos: um time entra e outro
        sai (vai para a Série D, que ainda não é jogável). As mudanças valem para carreiras novas.
      </p>
      <div className="catalog-fields">
        {PLAYABLE.map((s) => (
          <label key={s} className="wide">
            Série {s} ({bySeries(s).length} times)
            <small className="muted">{bySeries(s).map((c) => c.short).join(" · ")}</small>
          </label>
        ))}
      </div>

      <h3>Trocar dois times de série</h3>
      <div className="catalog-fields">
        <label>
          Time 1
          {clubSelect(clubA, setClubA, playable)}
        </label>
        <label>
          Time 2
          {clubSelect(clubB, setClubB, playable.filter((c) => c.series !== clubs.find((x) => x.id === clubA)?.series))}
        </label>
        <Button disabled={!clubA || !clubB || swap.isPending} onClick={() => swap.mutate()}>
          Trocar de série
        </Button>
      </div>
      {swap.error && (
        <p className="error notice" role="alert">
          {swap.error.message}
        </p>
      )}

      <h3>Substituir um time por outro (novo ou da Série D)</h3>
      <div className="catalog-fields">
        <label>
          Série
          <select
            value={series}
            onChange={(e) => {
              setSeries(e.target.value as SeriesId);
              setOutId("");
            }}
          >
            {PLAYABLE.map((s) => (
              <option key={s}>{s}</option>
            ))}
          </select>
        </label>
        <label>
          Time que sai
          {clubSelect(outId, setOutId, bySeries(series))}
        </label>
        <label>
          Trazer da Série D (opcional)
          {clubSelect(preset, usePreset, bySeries("D"))}
        </label>
        <label>
          Nome
          <input maxLength={80} value={form.name} onChange={(e) => set({ name: e.target.value, id: preset ? form.id : "" })} />
        </label>
        <label>
          Sigla
          <input maxLength={5} value={form.short} onChange={(e) => set({ short: e.target.value })} />
        </label>
        <label>
          Cor
          <input type="color" value={form.color} onChange={(e) => set({ color: e.target.value })} />
        </label>
        <label>
          Força (1 a 99)
          <input type="number" min={1} max={99} value={form.strength} onChange={(e) => set({ strength: Number(e.target.value) })} />
        </label>
        <label>
          Cidade
          <input maxLength={60} value={form.city} onChange={(e) => set({ city: e.target.value })} />
        </label>
        <label>
          Estado (UF)
          <input maxLength={2} value={form.uf} onChange={(e) => set({ uf: e.target.value.toUpperCase() })} />
        </label>
        <Button
          disabled={!outId || !form.name.trim() || !form.short.trim() || !form.city.trim() || form.uf.length !== 2 || replace.isPending}
          onClick={() => replace.mutate()}
        >
          <Plus size={15} /> Substituir time
        </Button>
      </div>
      {replace.error && (
        <p className="error notice" role="alert">
          {replace.error.message}
        </p>
      )}
      {directory.error && (
        <p className="error notice" role="alert">
          {directory.error.message}
        </p>
      )}
      {notice && (
        <p className="notice positive" role="status">
          {notice}
        </p>
      )}
    </>
  );
}

// ----------------------------------------------------------------- Painel
export default function AdminPanel() {
  const identity = useIdentity();
  const [tab, setTab] = useState("contas");
  if (identity.isPending)
    return <p className="empty">Verificando acesso administrativo…</p>;
  if (identity.data?.isAdmin !== true)
    // A tela de catálogo já explica como autorizar a conta.
    return (
      <Suspense fallback={<p className="empty">Carregando…</p>}>
        <AdminCatalog />
      </Suspense>
    );
  return (
    <div className="catalog-admin">
      <div className="page-heading">
        <div>
          <span className="eyebrow">PAINEL DO ADMINISTRADOR</span>
          <h1>Contas, times e jogadores.</h1>
          <p>Gerencie quem joga e edite os dados usados nas novas carreiras.</p>
        </div>
        <Users size={30} className="green" />
      </div>
      <section className="panel catalog-editor">
        <div className="catalog-tabs" role="tablist" aria-label="Painel do administrador">
          {[
            ["contas", "Contas"],
            ["times", "Times"],
            ["series", "Séries"],
            ["jogadores", "Jogadores"],
            ["dados", "Importar dados"],
          ].map(([key, label]) => (
            <button key={key} role="tab" aria-selected={tab === key} onClick={() => setTab(key)}>
              {label}
            </button>
          ))}
        </div>
        {tab === "contas" && <AccountsTab />}
        {tab === "times" && <TeamsTab />}
        {tab === "series" && <SeriesTab />}
        {tab === "jogadores" && <PlayersTab />}
        {tab === "dados" && (
          <Suspense fallback={<p className="empty">Carregando…</p>}>
            <AdminCatalog />
          </Suspense>
        )}
      </section>
    </div>
  );
}
