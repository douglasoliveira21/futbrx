import { useState } from "react";
import {useMutation,useQueryClient} from "@tanstack/react-query";
import {api} from "../../services/api";
import TacticalInstructions,{defaultTacticalPlan} from "./TacticalInstructions";
import { Button } from "../../components/ui/button";
import type { Career } from "../../services/api";
import { available, roleOrder, Shirt, type TeamAction } from "./SquadRoom";

export default function TacticsRoom({
  c,
  action,
  busy,
}: {
  c: Career;
  action: TeamAction;
  busy: boolean;
}) {
  const [formation, setFormation] = useState(c.state.formation),
    [mentality, setMentality] = useState(c.state.mentality);
  const [selected, setSelected] = useState<number>();
  const [plan,setPlan]=useState(c.state.tacticalPlan??defaultTacticalPlan);
  const qc=useQueryClient();
  const savePlan=useMutation({mutationFn:()=>api(`/careers/${c.id}/tactical-plan`,{revision:c.revision,plan,formation,mentality}),onSuccess:()=>qc.invalidateQueries({queryKey:["career"]})});
  const club = c.state.clubs.find((x) => x.id === c.state.clubId)!;
  const starters = c.state.squad
    .filter((p) => p.starter)
    .sort((a, b) => roleOrder(a) - roleOrder(b) || a.id - b.id);
  const bench = c.state.squad
    .filter((p) => !p.starter)
    .sort((a, b) => b.rating - a.rating);
  const rows =
    formation === "4-3-3"
      ? [1, 4, 3, 3]
      : formation === "4-4-2"
        ? [1, 4, 4, 2]
        : formation==="4-2-3-1"?[1,4,2,3,1]:[1, 3, 5, 2];
  const outgoing = starters.find((p) => p.id === selected);
  return (
    <div
      className="tactics-room"
      style={{ "--team-color": club.color } as React.CSSProperties}
    >
      <section className="matchboard">
        <header>
          <div>
            <span className="eyebrow">ONZE PARA FAZER HISTÓRIA</span>
            <h2>{club.name}</h2>
          </div>
          <b>{formation}</b>
        </header>
        <div className="formation-field">
          <div className="formation-center" />
          <div className="formation-box upper" />
          <div className="formation-box lower" />
          {[...rows].reverse().map((n, r) => {
            const start = rows
              .slice(0, rows.length - r - 1)
              .reduce((a, b) => a + b, 0);
            return (
              <div className="formation-line" key={r}>
                {Array.from({ length: n }, (_, i) => {
                  const p = starters[start + i];
                  return (
                    <button
                      key={i}
                      className={
                        "formation-player " +
                        (p?.id === selected ? "chosen" : "")
                      }
                      disabled={!p || busy}
                      aria-label={
                        p ? `Selecionar ${p.name} para troca` : "Posição vaga"
                      }
                      aria-pressed={p?.id === selected}
                      onClick={() => setSelected(p?.id)}
                    >
                      {p ? (
                        <>
                          <Shirt
                            color={club.color}
                            number={
                              c.state.squad.findIndex((x) => x.id === p.id) + 1
                            }
                            short={club.short}
                          />
                          <span>{p.name.split(" ").at(-1)}</span>
                          <small>
                            {p.position} <b>{p.rating}</b>
                          </small>
                        </>
                      ) : (
                        <span>VAGA</span>
                      )}
                    </button>
                  );
                })}
              </div>
            );
          })}
        </div>
        <footer>
          {outgoing
            ? `${outgoing.name} selecionado. Escolha quem entra no banco abaixo.`
            : "Selecione um titular para fazer uma troca antes da partida."}
        </footer>
      </section>
      <aside className="game-plan">
        <span className="eyebrow">A SUA LEITURA DO JOGO</span>
        <h2>Plano de jogo</h2>
        <label>Desenho tático</label>
        <div className="formation-options">
          {["4-3-3", "4-4-2", "3-5-2","4-2-3-1"].map((f) => (
            <button
              key={f}
              aria-pressed={formation === f}
              onClick={() => setFormation(f)}
            >
              {f}
            </button>
          ))}
        </div>
        <label>Como vamos jogar?</label>
        <div className="mentality-options">
          {[
            [
              "Defensiva",
              "Bloco protegido",
              "Menos avanço e mais proteção da área.",
            ],
            [
              "Equilibrada",
              "Jogo equilibrado",
              "Aproximação com cobertura dos espaços.",
            ],
            [
              "Ofensiva",
              "Pressão por gols",
              "Linhas adiantadas e mais presença na frente.",
            ],
          ].map(([key, title, text]) => (
            <button
              key={key}
              aria-pressed={mentality === key}
              onClick={() => setMentality(key)}
            >
              <b>{title}</b>
              <span>{text}</span>
            </button>
          ))}
        </div>
        <TacticalInstructions plan={plan} onChange={setPlan} players={c.state.squad} disabled={busy||savePlan.isPending}/>
        {savePlan.error&&<p role="alert" className="error">{savePlan.error.message}</p>}
        {savePlan.isSuccess&&<p role="status">Plano de jogo salvo para a próxima partida.</p>}
        <Button
          disabled={
            busy ||savePlan.isPending||
            (formation === c.state.formation && mentality === c.state.mentality&&JSON.stringify(plan)===JSON.stringify(c.state.tacticalPlan??defaultTacticalPlan))
          }
          onClick={() => savePlan.mutate()}
        >
          Confirmar estratégia
        </Button>
        <p className="muted">
          A prancheta mostra a prévia. Confirme para usar o plano na próxima
          partida.
        </p>
        <div className="plan-status">
          <strong>{starters.length}/11</strong>
          <span>jogadores no onze inicial</span>
        </div>
      </aside>
      <section className="bench-room">
        <header>
          <span className="eyebrow">PRONTOS PARA ENTRAR</span>
          <h2>Banco de reservas</h2>
          <p>
            {outgoing
              ? `Quem entra no lugar de ${outgoing.name}?`
              : "Escolha primeiro um jogador no campo."}
          </p>
        </header>
        <div className="bench-cards">
          {bench.map((p) => (
            <button
              className="bench-athlete"
              key={p.id}
              disabled={
                busy ||
                !outgoing ||
                !available(p) ||
                (outgoing.position === "GOL" && p.position !== "GOL") ||
                (outgoing.position !== "GOL" && p.position === "GOL")
              }
              onClick={() => {
                action("swap", String(p.id), "", outgoing!.id);
                setSelected(undefined);
              }}
            >
              <span className="bench-rating">
                {p.rating}
                <small>{p.position}</small>
              </span>
              <div>
                <b>{p.name}</b>
                <small>
                  {p.injuryDays
                    ? "Lesionado"
                    : p.suspensionMatches
                      ? "Suspenso"
                      : `${p.fitness}% de condição`}
                </small>
              </div>
              <span>{outgoing ? "Trocar" : "Reserva"}</span>
            </button>
          ))}
        </div>
      </section>
    </div>
  );
}
