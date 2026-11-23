import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  api,
  moneyExact as money,
  type Career,
  type Player,
  type YouthData,
  type YouthProspect,
} from "../../services/api";
import { Button } from "../../components/ui/button";
import { DialogShell } from "../../components/ui/dialog";
import { Shirt } from "../sports/SquadRoom";

type Choice =
  | { kind: "search"; region: string; position: string }
  | { kind: "recruit"; prospect: YouthProspect }
  | { kind: "promote"; player: Player };
export default function YouthCenter({ c }: { c: Career }) {
  const qc = useQueryClient();
  const [region, setRegion] = useState("Sudeste");
  const [position, setPosition] = useState("Todas");
  const [choice, setChoice] = useState<Choice | null>(null);
  const [message, setMessage] = useState("");
  const q = useQuery({
    queryKey: ["youth", c.id, c.revision],
    placeholderData: (previous) => previous,
    queryFn: () => api<YouthData>(`/careers/${c.id}/youth/`),
  });
  const change = useMutation({
    mutationFn: (selected: Choice) => {
      const path =
        selected.kind === "search"
          ? "scouting"
          : selected.kind === "recruit"
            ? `prospects/${selected.prospect.player.id}/recruit`
            : `players/${selected.player.id}/promote`;
      return api<{ result: { message: string } }>(
        `/careers/${c.id}/youth/${path}`,
        {
          revision: q.data!.revision,
          ...(selected.kind === "search"
            ? { region: selected.region, position: selected.position }
            : {}),
        },
      );
    },
    onSuccess: async (r) => {
      setMessage(r.result.message);
      setChoice(null);
      await Promise.all([
        qc.invalidateQueries({ queryKey: ["youth", c.id] }),
        qc.invalidateQueries({ queryKey: ["career", c.id] }),
        qc.invalidateQueries({ queryKey: ["training", c.id] }),
      ]);
    },
    onError: () => {
      void qc.invalidateQueries({ queryKey: ["youth", c.id] });
      void qc.invalidateQueries({ queryKey: ["career", c.id] });
    },
  });
  const select = (selected: Choice) => {
    change.reset();
    setChoice(selected);
  };
  if (q.isPending) return <p>Carregando a base…</p>;
  if (q.error)
    return (
      <p className="error" role="alert">
        {q.error.message}
      </p>
    );
  const data = q.data;
  const busy = change.isPending || q.isFetching;
  const mission = data.scouting;
  return (
    <div className="form-stack">
      <div className="notice">
        Atletas fictícios de 16 a 20 anos. A base treina com o planejamento do
        clube e aceita foco individual. Ao completar 21 anos na virada da
        temporada, o jovem que não foi promovido sai como agente livre.
      </div>
      {message && <p role="status">{message}</p>}
      {change.error && !choice && (
        <p className="error" role="alert">
          {change.error.message}
        </p>
      )}
      <section className="panel" style={{ padding: 24 }}>
        <h2>Centro de formação</h2>
        <div className="toolbar">
          <strong>
            {data.academy.length}/{data.capacity} vagas na base
          </strong>
          <span>
            Manutenção: {money(data.academy.length * data.weeklyCost)} por
            rodada
          </span>
          <span>Caixa: {money(data.balance)}</span>
          <Link to="/treinamentos">Treinar e acompanhar evolução</Link>
        </div>
        <p>
          Promover custa {money(data.promotionFee)} em luvas, além do salário
          profissional por rodada. O vínculo termina em {data.contractEndSeason}
          . Profissional: {data.professionalCount}/40 atletas.
        </p>
        <div className="academy-cards">{data.academy.map(p=><article className="contract-card" key={p.id}>
          <div className="contract-person"><Shirt color={c.state.clubs.find(x=>x.id===c.state.clubId)!.color} number={0} short={p.position}/><div><span className="eyebrow">{p.position} · {p.age} anos</span><h3>{p.name}</h3><span className={p.age===20 ? "gold-text" : "muted"}>{p.age===20 ? "Último ano na base" : "Centro de formação"}</span></div><strong className="academy-rating">{p.rating}</strong></div>
          <div className="contract-terms"><div><small>Condição física</small><strong>{p.fitness}%</strong></div><div><small>Salário ao promover</small><strong>{money(p.wage)}</strong></div></div>
          <Button disabled={busy || data.professionalCount>=40 || data.balance<data.promotionFee} variant="outline" onClick={()=>select({kind:"promote",player:p})}>Promover {p.name}</Button>
        </article>)}</div>
        <p className="muted">
          Seis jovens iniciais e até três novas entradas por ano, limitadas às
          vagas. Não há partidas de base nesta etapa; a evolução acontece nos
          treinos. Promoções são permitidas fora da janela de transferências.
        </p>
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Observação e captação</h2>
        <p>
          Uma observação custa {money(data.searchFee)} e encontra seis jovens
          após duas rodadas. Você escolhe quais incorporar, pagando a taxa
          indicada. Um novo pedido substitui o relatório anterior.
        </p>
        <div className="toolbar">
          <label>
            Região
            <select
              value={region}
              disabled={busy}
              onChange={(e) => setRegion(e.target.value)}
            >
              {data.regions.map((r) => (
                <option key={r}>{r}</option>
              ))}
            </select>
          </label>
          <label>
            Posição observada
            <select
              value={position}
              disabled={busy}
              onChange={(e) => setPosition(e.target.value)}
            >
              {["Todas", ...data.positions].map((p) => (
                <option key={p}>{p}</option>
              ))}
            </select>
          </label>
          <Button
            disabled={
              busy ||
              mission?.status === "searching" ||
              data.round > 36 ||
              data.balance < data.searchFee
            }
            onClick={() => select({ kind: "search", region, position })}
          >
            Solicitar observação
          </Button>
        </div>
        {data.round > 36 && (
          <p className="muted">
            Novas observações reabrem na próxima temporada.
          </p>
        )}
        {mission?.status === "searching" && (
          <p role="status">
            Observando {mission.position} · {mission.region}. Relatório
            disponível no início da rodada {mission.completionRound}, após
            concluir duas rodadas.
          </p>
        )}
        {mission?.status === "completed" && (
          <p>
            Relatório de {mission.region} · válido até a rodada{" "}
            {mission.expiresRound} desta temporada.
          </p>
        )}
        {mission?.status === "expired" && (
          <p>O último relatório expirou. Solicite uma nova observação.</p>
        )}
        {!!data.prospects.length && (
          <div className="academy-cards">{data.prospects.map(prospect=><article className="contract-card" key={prospect.player.id}><div className="contract-person"><Shirt color="#365d75" number={0} short={prospect.player.position}/><div><span className="eyebrow">{prospect.player.position} · {prospect.player.age} anos</span><h3>{prospect.player.name}</h3><p>Habilidade {prospect.player.rating} · Captação {money(prospect.signingFee)}</p></div></div><Button disabled={busy || data.academy.length>=data.capacity || data.balance<prospect.signingFee} variant="outline" onClick={()=>select({kind:"recruit",prospect})}>Incorporar {prospect.player.name}</Button></article>)}</div>
        )}
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Histórico da formação</h2>
        {data.history.length ? (
          <details>
            <summary>Ver {data.history.length} registros</summary>
            {data.history.map((h, i) => (
              <p key={i}>{h}</p>
            ))}
          </details>
        ) : (
          <p>Ainda não há captações ou promoções nesta carreira.</p>
        )}
        <details>
          <summary>Jovens já no profissional</summary>
          <div className="player-leaderboard">{c.state.squad.filter(p=>p.age<=21).map(p=><article key={p.id}><div><h3>{p.name}</h3><small>{p.position} · {p.age} anos · {p.starter ? "Titular" : "Reserva"}</small></div><strong>{p.rating}<small>habilidade</small></strong></article>)}</div>
        </details>
      </section>
      {choice && (
        <DialogShell
          label="Confirmar operação da base"
          busy={change.isPending}
          onClose={() => setChoice(null)}
        >
          <div className="form-stack">
            <h2>
              {choice.kind === "search"
                ? "Contratar observação"
                : choice.kind === "recruit"
                  ? "Incorporar jovem"
                  : "Promover ao profissional"}
            </h2>
            {choice.kind === "search" ? (
              <>
                <p>
                  {choice.position} · {choice.region} · duas rodadas de espera.
                </p>
                <p>
                  Débito imediato: <strong>{money(data.searchFee)}</strong>. A
                  taxa não é reembolsada. Os candidatos do relatório anterior
                  serão descartados.
                </p>
              </>
            ) : choice.kind === "recruit" ? (
              <>
                <p>
                  <strong>{choice.prospect.player.name}</strong> ·{" "}
                  {choice.prospect.player.age} anos ·{" "}
                  {choice.prospect.player.position}.
                </p>
                <p>
                  Captação: <strong>{money(choice.prospect.signingFee)}</strong>
                  . Manutenção adicional: {money(data.weeklyCost)} por rodada.
                  Ainda não há contrato profissional.
                </p>
              </>
            ) : (
              <>
                <p>
                  <strong>{choice.player.name}</strong> ·{" "}
                  {choice.player.position} · habilidade {choice.player.rating}.
                </p>
                <p>
                  Luvas imediatas: <strong>{money(data.promotionFee)}</strong>.
                  Salário:{" "}
                  <strong>{money(choice.player.wage)} por rodada</strong>.
                  Contrato até o fim de {data.contractEndSeason}.
                </p>
                <p>
                  O atleta sai da base, conserva sua evolução e passa a integrar
                  o elenco profissional.
                </p>
              </>
            )}
            {change.error && (
              <p className="error" role="alert">
                {change.error.message}
              </p>
            )}
            <div className="actions">
              <Button disabled={busy} onClick={() => change.mutate(choice)}>
                Confirmar{" "}
                {choice.kind === "search"
                  ? "observação"
                  : choice.kind === "recruit"
                    ? "captação"
                    : "promoção"}
              </Button>
              <Button
                disabled={change.isPending}
                variant="ghost"
                onClick={() => setChoice(null)}
              >
                Cancelar
              </Button>
            </div>
          </div>
        </DialogShell>
      )}
    </div>
  );
}
