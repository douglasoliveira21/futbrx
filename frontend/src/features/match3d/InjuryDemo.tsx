import { InjuryDecision } from "./InjuryDecision";
import type { BroadcastPacket } from "../../services/api";

// Development-only preview of the injury window (?injuryDemo=1) with fictitious data.
const bench = [
  { id: 21, name: "Rafael Almeida", position: "ZAG", rating: 70, fitness: 100 },
  { id: 22, name: "Lucas Costa", position: "MEI", rating: 74, fitness: 98 },
  { id: 23, name: "Gabriel Mendes", position: "MEI", rating: 78, fitness: 90 },
];
const home = { id: "pal", formation: "4-3-3", mentality: "Equilibrada", players: [{ id: 8, name: "Matheus Rocha", position: "MEI", rating: 80, fitness: 60 }], bench };
const packet = {
  session: { injuryAwaiting: true, injuryPlayerId: 8, managedTeam: "pal", fixture: { setup: { home, away: { ...home, id: "fla", bench: [] } } } },
  replay: {
    frames: [{ players: [{ id: 8, name: "Matheus Rocha", position: "MEI", team: 0, x: 0, z: 0, yaw: 0, action: "injured" }] }],
    events: [{ type: "injury", team: "pal", player: 8, minute: 54, text: "Matheus Rocha se lesiona: entorse de tornozelo. Previsão: 14 dias.", injuryDays: 14, injuryName: "Entorse de tornozelo" }],
  },
} as unknown as BroadcastPacket;

export default function InjuryDemo() {
  return (
    <div style={{ position: "relative", minHeight: "100vh", background: "#0b1a14" }}>
      <InjuryDecision packet={packet} busy={false} error="" control={async () => true} />
    </div>
  );
}
