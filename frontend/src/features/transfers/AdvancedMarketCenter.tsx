import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, moneyExact as money, type Career } from "../../services/api";
import { Button } from "../../components/ui/button";
import { DialogShell } from "../../components/ui/dialog";
type Candidate = {
  id: number;
  name: string;
  clubId: string;
  wage: number;
  loanFee: number;
  releaseClause: number;
  releaseWage: number;
  releaseBonus: number;
  releaseCommission?: number;
  available: boolean;
};
type Loan = {
  id: string;
  playerId: number;
  playerName: string;
  ownerClub: string;
  borrowerClub: string;
  endSeason: number;
  fee: number;
  returned: boolean;
};
type Payment = {
  id: string;
  playerId: number;
  buyer: string;
  seller: string;
  dueSeason: number;
  dueRound: number;
  amount: number;
  paid: boolean;
};
type Data = {
  candidates: Candidate[];
  loans: Loan[];
  transferPayments: Payment[];
  window: { isOpen: boolean; description: string };
};
export default function AdvancedMarketCenter({ c }: { c: Career }) {
  const qc = useQueryClient(),
    [search, setSearch] = useState(""),
    [review, setReview] = useState<{
      player: Candidate;
      release: boolean;
    } | null>(null),
    [notice, setNotice] = useState("");
  const [lendPlayer, setLendPlayer] = useState(
    String(
      c.state.squad.find((p) => !p.starter && p.position !== "GOL")?.id ?? "",
    ),
  );
  const [borrower, setBorrower] = useState(
    c.state.clubs.find((x) => x.id !== c.state.clubId)?.id ?? "",
  );
  const [lendReview, setLendReview] = useState(false);
  const lendingPlayer = c.state.squad.find((p) => p.id === Number(lendPlayer));
  const lendingFee = Math.max(
    50000,
    Math.floor((lendingPlayer?.value ?? 0) / 20),
  );
  const lend = useMutation({
    mutationFn: () =>
      api(`/careers/${c.id}/loans/lend`, {
        revision: c.revision,
        playerId: Number(lendPlayer),
        borrowerClub: borrower,
        fee: lendingFee,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["career", c.id] });
      qc.invalidateQueries({ queryKey: ["advanced-market", c.id] });
      setLendReview(false);
      setNotice("Empréstimo de saída registrado; retorno garantido na virada.");
    },
  });
  const q = useQuery({
    queryKey: ["advanced-market", c.id, c.revision],
    queryFn: () => api<Data>(`/careers/${c.id}/advanced-market`),
  });
  const m = useMutation({
    mutationFn: ({
      player,
      release,
    }: {
      player: Candidate;
      release: boolean;
    }) =>
      api(`/careers/${c.id}${release ? "/market/release" : "/loans"}`, {
        revision: c.revision,
        playerId: player.id,
        fee: player.loanFee,
        clause: player.releaseClause,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["career", c.id] });
      qc.invalidateQueries({ queryKey: ["advanced-market", c.id] });
      setReview(null);
      setNotice("Operação concluída e registrada na carreira.");
    },
  });
  const name = (id: string) =>
    c.state.clubs.concat(c.state.otherClubs ?? []).find((x) => x.id === id)
      ?.name ?? id;
  if (q.isPending) return <p>Carregando gestão avançada…</p>;
  if (q.error) return <p role="alert">{q.error.message}</p>;
  const d = q.data!,
    candidates = d.candidates.filter(
      (p) =>
        p.available &&
        p.name.toLocaleLowerCase().includes(search.toLocaleLowerCase()),
    );
  return (
    <div className="form-stack">
      <section className="panel" style={{ padding: 24 }}>
        <h2>Empréstimos e multas rescisórias</h2>
        <p>{d.window.description}</p>
        <p>
          Empréstimo até o fim da temporada: taxa à vista de 5% do valor de
          jogo, salário integral por rodada e retorno automático antes da
          virada. A multa substitui a taxa pedida pelo clube; salário, janela e
          elenco mínimo continuam obrigatórios.
        </p>
        <p>
          Para parcelar uma compra ou definir a multa de um novo contrato, use a
          negociação em Transferências ou Contratos.
        </p>
        <label>
          Buscar atleta
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Nome do jogador"
          />
        </label>
        {notice && <p role="status">{notice}</p>}
        <div style={{ maxHeight: 500, overflow: "auto" }}>
          <table className="data-table">
            <thead>
              <tr>
                <th>Atleta</th>
                <th>Origem</th>
                <th>Taxa do empréstimo</th>
                <th>Salário / rodada</th>
                <th>Operação</th>
              </tr>
            </thead>
            <tbody>
              {candidates.slice(0, 60).map((p) => (
                <tr key={p.id}>
                  <td>{p.name}</td>
                  <td>{name(p.clubId)}</td>
                  <td>{money(p.loanFee)}</td>
                  <td>{money(p.wage)}</td>
                  <td>
                    <Button
                      disabled={!d.window.isOpen || m.isPending}
                      onClick={() => {
                        m.reset();
                        setReview({ player: p, release: false });
                      }}
                    >
                      Revisar empréstimo
                    </Button>
                    {p.releaseClause > 0 && (
                      <Button
                        variant="outline"
                        onClick={() => {
                          m.reset();
                          setReview({ player: p, release: true });
                        }}
                      >
                        Acionar multa · {money(p.releaseClause)}
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p>
          {candidates.length} atletas encontrados; exibindo até 60. Refine a
          busca para encontrar outros nomes.
        </p>
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Ceder atleta por empréstimo</h2>
        <p>
          O destino paga a taxa e assume o salário integral. A vaga do atleta
          fica reservada para seu retorno.
        </p>
        <div className="contract-inputs">
          <label>
            Seu atleta
            <select
              value={lendPlayer}
              onChange={(e) => setLendPlayer(e.target.value)}
            >
              {c.state.squad.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} · {p.position}
                </option>
              ))}
            </select>
          </label>
          <label>
            Clube de destino
            <select
              value={borrower}
              onChange={(e) => setBorrower(e.target.value)}
            >
              {c.state.clubs
                .concat(c.state.otherClubs ?? [])
                .filter((x) => x.id !== c.state.clubId)
                .map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
            </select>
          </label>
        </div>
        <Button
          disabled={!lendingPlayer || !d.window.isOpen || lend.isPending}
          onClick={() => {
            lend.reset();
            setLendReview(true);
          }}
        >
          Revisar cessão por empréstimo
        </Button>
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Empréstimos registrados</h2>
        {d.loans.length === 0 ? (
          <p>Nenhum empréstimo.</p>
        ) : (
          d.loans.map((l) => (
            <p key={l.id}>
              {l.playerName} · {name(l.ownerClub)} → {name(l.borrowerClub)} ·
              até {l.endSeason} · {l.returned ? "Devolvido" : "Ativo"}
            </p>
          ))
        )}
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Parcelas de transferências</h2>
        <p>
          As obrigações permanecem após a virada do ano e são liquidadas uma
          única vez na rodada de vencimento, mesmo que o caixa fique negativo.
        </p>
        {d.transferPayments
          .filter(
            (p) => p.buyer === c.state.clubId || p.seller === c.state.clubId,
          )
          .map((p) => (
            <p key={p.id}>
              {p.dueSeason} · rodada {p.dueRound} ·{" "}
              {p.buyer === c.state.clubId ? "Pagar" : "Receber"}{" "}
              {money(p.amount)} · {p.paid ? "Liquidada" : "Pendente"}
            </p>
          ))}
        {d.transferPayments.length === 0 && <p>Nenhuma parcela registrada.</p>}
      </section>
      {lendReview && lendingPlayer && (
        <DialogShell
          label="Revisar cessão"
          busy={lend.isPending}
          onClose={() => setLendReview(false)}
        >
          <section className="panel contract-dialog">
            <h2>
              {lendingPlayer.name} → {name(borrower)}
            </h2>
            <p>
              Recebimento: {money(lendingFee)}. O destino assume{" "}
              {money(lendingPlayer.wage)} por rodada até o fim de{" "}
              {c.state.season}. O atleta retorna automaticamente e sua vaga fica
              reservada.
            </p>
            <p>
              O servidor confere caixa do destino, janela, negociações pendentes
              e elenco mínimo antes de registrar a cessão.
            </p>
            {lend.error && (
              <p role="alert" className="error">
                {lend.error.message}
              </p>
            )}
            <div className="toolbar">
              <Button variant="outline" onClick={() => setLendReview(false)}>
                Cancelar
              </Button>
              <Button disabled={lend.isPending} onClick={() => lend.mutate()}>
                Confirmar cessão
              </Button>
            </div>
          </section>
        </DialogShell>
      )}
      {review && (
        <DialogShell
          label="Revisar operação"
          busy={m.isPending}
          onClose={() => setReview(null)}
        >
          <section className="panel contract-dialog">
            <h2>{review.player.name}</h2>
            <p>
              {review.release
                ? "Acionar multa rescisória"
                : "Empréstimo até o fim de " + c.state.season}
            </p>
            <p>
              Débito inicial:{" "}
              {money(
                review.release
                  ? review.player.releaseClause +
                      review.player.releaseBonus +
                      (review.player.releaseCommission ?? 0)
                  : review.player.loanFee,
              )}
              {review.release ? " (multa + luvas + comissão empresarial)" : ""}.
            </p>
            <p>
              Salário:{" "}
              {money(
                review.release ? review.player.releaseWage : review.player.wage,
              )}{" "}
              por rodada.{" "}
              {review.release
                ? "Novo contrato de três temporadas; a API confere os termos atuais antes da contratação."
                : "Você assume o salário integral e o jogador retorna à origem na virada do ano."}
            </p>
            {m.error && (
              <p role="alert" className="error">
                {m.error.message}
              </p>
            )}
            <div className="toolbar">
              <Button
                variant="outline"
                disabled={m.isPending}
                onClick={() => setReview(null)}
              >
                Cancelar
              </Button>
              <Button disabled={m.isPending} onClick={() => m.mutate(review)}>
                Confirmar operação
              </Button>
            </div>
          </section>
        </DialogShell>
      )}
    </div>
  );
}
