import {useQuery} from "@tanstack/react-query";
import {api,type Fixture,type Club,type MatchReplay} from "../../services/api";
import Match3D from "./Match3D";
export default function SimulationValidation(){
 const requested=Number(new URLSearchParams(location.search).get("seed")??117);
 const seed=Number.isSafeInteger(requested)&&requested>=0&&requested<=2147483647?requested:117;
 const engine=new URLSearchParams(location.search).get("simulationTest")==="v11"?"v11":"v12";
 const query=useQuery({queryKey:["development-simulation",engine,seed],queryFn:()=>api<{fixture:Fixture;clubs:Club[];replay:MatchReplay}>(`/development/simulation-v11?seed=${seed}&engine=${engine}`)});
 if(query.error)return <p role="alert">{query.error.message}</p>;
 if(!query.data)return <p>Calculando partida de validação positional-{engine}…</p>;
 return <Match3D fixture={query.data.fixture} clubs={query.data.clubs} validationReplay={query.data.replay} careerId="development" season={2026}/>;
}
