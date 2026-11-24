import {useMemo,useState} from "react";
import {useMutation,useQuery,useQueryClient} from "@tanstack/react-query";
import {api,type Career,type TacticalPlan} from "../../services/api";
import TacticalPositionEditor from "./TacticalPositionEditor";
import TacticalInstructions,{defaultTacticalPlan} from "../sports/TacticalInstructions";
import "./tacticalDesk.css";
import { Loader } from "../../components/Loader";

type Point={x:number;z:number};
type Formation={id:string;label:string;category:string;positions:Point[]};
type Preset={id:string;name:string;formationId:string;mentality:string;plan:TacticalPlan};
type Catalog={formations:Formation[];plans:Preset[]};
const order=(position:string)=>({GOL:0,ZAG:1,LAT:2,MEI:3,ATA:4})[position]??5;

/** Unreleased editor: mounted only after the tactical desk passes its release gate. */
export default function TacticalPresetEditor({career}:{career:Career}){
 const qc=useQueryClient();
 const query=useQuery({queryKey:["tactical-catalog",career.id,career.revision],queryFn:()=>api<Catalog>(`/careers/${career.id}/sports/tactical-catalog`)});
 const [id,setId]=useState(""),[name,setName]=useState("Meu plano"),[formationId,setFormationId]=useState("433"),[category,setCategory]=useState("Todas"),[mentality,setMentality]=useState("Equilibrada"),[plan,setPlan]=useState<TacticalPlan>(career.state.tacticalPlan??defaultTacticalPlan),[notice,setNotice]=useState("");
 const starters=useMemo(()=>career.state.squad.filter(p=>p.starter&&p.injuryDays===0&&p.suspensionMatches===0).sort((a,b)=>order(a.position)-order(b.position)||a.id-b.id),[career.state.squad]);
 const mutation=useMutation({mutationFn:({path,body}:{path:string;body:object})=>api<Preset|{revision:number}>(`/careers/${career.id}/sports/tactical-presets${path}`,{revision:career.revision,...body}),onSuccess:async()=>{await Promise.all([qc.invalidateQueries({queryKey:["career"]}),qc.invalidateQueries({queryKey:["tactical-catalog",career.id]})]);setNotice("Alteração registrada.")}});
 const catalogue=query.data;
 function selectFormation(f:Formation){setFormationId(f.id);const positions=Object.fromEntries(starters.map((p,i)=>[p.id,f.positions[i]]).filter(([,point])=>point));setPlan(p=>({...p,positions}));}
 function open(p:Preset){setId(p.id);setName(p.name);setFormationId(p.formationId);setMentality(p.mentality);setPlan(p.plan);setNotice("")}
 if(query.isPending)return <Loader variant="panel" label="Carregando formações…" />;
 if(!catalogue)return <p role="alert">{query.error?.message??"Não foi possível carregar os planos."}</p>;
 return <div className="preset-editor"><div className="preset-controls"><label>Categoria<select value={category} onChange={e=>setCategory(e.target.value)}>{["Todas",...new Set(catalogue.formations.map(f=>f.category))].map(c=><option key={c}>{c}</option>)}</select></label><label>Formação<select value={formationId} onChange={e=>{const f=catalogue.formations.find(x=>x.id===e.target.value);if(f)selectFormation(f)}}>{catalogue.formations.filter(f=>category==="Todas"||f.category===category||f.id===formationId).map(f=><option key={f.id} value={f.id}>{f.label}</option>)}</select></label><label>Mentalidade<select value={mentality} onChange={e=>setMentality(e.target.value)}><option>Defensiva</option><option>Equilibrada</option><option>Ofensiva</option></select></label><label>Nome do plano<input maxLength={60} value={name} onChange={e=>setName(e.target.value)}/></label><TacticalInstructions plan={plan} onChange={setPlan} players={career.state.squad} disabled={mutation.isPending}/></div>
 <div><p>Arraste um atleta ou use as setas do teclado para ajustar sua posição de referência.</p><TacticalPositionEditor players={starters} plan={plan} onChange={setPlan} disabled={mutation.isPending}/>{starters.length!==11&&<p role="alert">Escale onze atletas aptos antes de aplicar uma formação.</p>}</div>
 <div className="preset-controls"><h3>Planos salvos</h3><button disabled={mutation.isPending||!name.trim()} onClick={()=>mutation.mutate({path:"",body:{id,name,formationId,mentality,plan}})}>{id?"Salvar alterações":"Salvar novo plano"}</button><button disabled={mutation.isPending} onClick={()=>{setId("");setName(name+" cópia")}}>Duplicar como novo</button>{catalogue.plans.map(p=><article className="desk-panel" key={p.id}><strong>{p.name}</strong><p>{catalogue.formations.find(f=>f.id===p.formationId)?.label??p.formationId} · {p.mentality}</p><button disabled={mutation.isPending} onClick={()=>open(p)}>Editar</button><button disabled={mutation.isPending||starters.length!==11} onClick={()=>mutation.mutate({path:"/apply",body:{id:p.id}})}>Aplicar na próxima partida</button><button disabled={mutation.isPending} onClick={()=>mutation.mutate({path:"/delete",body:{id:p.id}})}>Excluir</button></article>)}{notice&&<p role="status">{notice}</p>}{mutation.error&&<p role="alert">{mutation.error.message}</p>}</div></div>
}
