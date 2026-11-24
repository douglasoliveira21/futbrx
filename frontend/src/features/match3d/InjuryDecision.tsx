import { Button } from "../../components/ui/button";
import type { BroadcastPacket } from "../../services/api";
import { injuryDecision } from "./injuryModel";
import "./injuryDecision.css";

// The broadcast is paused by the server until the coach decides about the hurt player.
export function InjuryDecision({
  packet,
  busy,
  error,
  control,
}: {
  packet: BroadcastPacket;
  busy: boolean;
  error: string;
  control: (body: object) => Promise<boolean>;
}) {
  const model = injuryDecision(packet);
  if (!model) return null;
  const substitute = (inId: number) => void control({ type: "substitution", outPlayerId: model.playerId, inPlayerId: inId });
  return (
    <div className="injury-backdrop" role="alertdialog" aria-modal="true" aria-labelledby="injury-title">
      <div className="injury-card">
        <span className="injury-pulse" aria-hidden>
          +
        </span>
        <h2 id="injury-title">Lesão em campo</h2>
        <p className="injury-lead">
          <b>{model.name}</b> ({model.position}) precisa sair. {model.injuryName}
          {model.days > 0 && <> · previsão de {model.days} dias</>}
        </p>
        {model.reserves.length > 0 ? (
          <>
            <p className="injury-hint">Escolha quem entra. A substituição por lesão não gasta uma das três paradas, mas conta como uma das cinco trocas ({model.changesLeft} restantes).</p>
            <ul className="injury-reserves">
              {model.reserves.map((r) => (
                <li key={r.id}>
                  <button type="button" disabled={busy} onClick={() => substitute(r.id)}>
                    <b>{r.name}</b>
                    <span>
                      {r.position} · nota {r.rating} · condição {r.fitness}%
                    </span>
                  </button>
                </li>
              ))}
            </ul>
            {model.suggested && (
              <Button variant="outline" disabled={busy} onClick={() => substitute(model.suggested!.id)}>
                Deixar o auxiliar escolher ({model.suggested.name})
              </Button>
            )}
          </>
        ) : (
          <>
            <p className="injury-hint">
              {model.changesLeft === 0 ? "As cinco substituições já foram feitas." : "Não há reserva disponível para esta posição."} O time seguirá com um jogador a menos.
            </p>
            <Button disabled={busy} onClick={() => void control({ type: "injury_retire", outPlayerId: model.playerId })}>
              Continuar com dez
            </Button>
          </>
        )}
        {error && (
          <p className="injury-error" role="alert">
            {error}
          </p>
        )}
      </div>
    </div>
  );
}
