import {useRef,type PointerEvent} from "react";
import type {TacticalPlan,MatchPlayer} from "../../services/api";

export default function TacticalPositionEditor({players,plan,onChange,disabled=false}:{players:MatchPlayer[];plan:TacticalPlan;onChange:(p:TacticalPlan)=>void;disabled?:boolean}){
 const field=useRef<HTMLDivElement>(null),dragging=useRef<number|null>(null);
 function move(e:PointerEvent){if(disabled||dragging.current===null||!field.current)return;const box=field.current.getBoundingClientRect();const x=Math.max(-1,Math.min(1,1-2*(e.clientY-box.top)/box.height)),z=Math.max(-1,Math.min(1,2*(e.clientX-box.left)/box.width-1));onChange({...plan,positions:{...plan.positions,[dragging.current]:{x,z}}})}
 return <div ref={field} className="desk-pitch" aria-label="Editor de posições de referência" onPointerMove={move} onPointerUp={()=>{dragging.current=null}} onPointerCancel={()=>{dragging.current=null}}>
  <div className="desk-center-circle"/><div className="desk-area top"/><div className="desk-area bottom"/>
  {players.map((p,i)=>{const point=plan.positions?.[p.id]??{x:p.position==="GOL"?-.93:-.6+Math.floor((i-1)/4)*.55,z:((i-1)%4-1.5)*.4};return <button key={p.id} disabled={disabled||p.position==="GOL"} className={`desk-player ${p.position==='GOL'?'keeper':''}`} style={{left:`${(point.z+1)*50}%`,top:`${(1-point.x)*50}%`,touchAction:'none'}} onPointerDown={e=>{if(p.position==='GOL')return;dragging.current=p.id;e.currentTarget.setPointerCapture(e.pointerId)}} onKeyDown={e=>{let dx=0,dz=0;if(e.key==='ArrowUp')dx=.03;else if(e.key==='ArrowDown')dx=-.03;else if(e.key==='ArrowLeft')dz=-.03;else if(e.key==='ArrowRight')dz=.03;else return;e.preventDefault();onChange({...plan,positions:{...plan.positions,[p.id]:{x:Math.max(-1,Math.min(1,point.x+dx)),z:Math.max(-1,Math.min(1,point.z+dz))}}})}} aria-label={`Mover ${p.name}`}><b>{p.position}</b><span>{p.name}</span></button>})}
 </div>
}
