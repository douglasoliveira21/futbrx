import assert from 'node:assert/strict';
import {injuryDecision} from '../src/features/match3d/injuryModel.ts';

const bench=[{id:21,name:'Reserva Zagueiro',position:'ZAG',rating:70,fitness:100},{id:22,name:'Reserva Meia',position:'MEI',rating:74,fitness:98},{id:23,name:'Reserva Meia B',position:'MEI',rating:78,fitness:90},{id:24,name:'Goleiro Reserva',position:'GOL',rating:65,fitness:100}];
const team={id:'pal',players:[{id:8,name:'Camisa 8',position:'MEI',rating:80,fitness:60},{id:1,name:'Goleiro',position:'GOL',rating:80,fitness:90}],bench};
const packet=(over={},events=[])=>({session:{injuryAwaiting:true,injuryPlayerId:8,managedTeam:'pal',fixture:{setup:{home:team,away:{id:'fla',players:[],bench:[]}}},...over},replay:{frames:[{players:[{id:8,name:'Camisa 8',position:'MEI',team:0,x:0,z:0,yaw:0,action:'injured'}]}],events:[{type:'injury',team:'pal',player:8,text:'Camisa 8 se lesiona: entorse de tornozelo. Previsão: 14 dias.',injuryDays:14,injuryName:'Entorse de tornozelo'},...events]}});

assert.equal(injuryDecision(packet({injuryAwaiting:false})),null,'sem lesão pendente não há janela');
assert.equal(injuryDecision(packet({managedTeam:undefined})),null,'sem time do treinador não há janela');
const m=injuryDecision(packet());
assert.equal(m.playerId,8);assert.equal(m.injuryName,'Entorse de tornozelo');assert.equal(m.days,14);assert.equal(m.changesLeft,5);
assert.deepEqual(m.reserves.map(r=>r.id),[23,22,21],'mesma posição primeiro, depois pela nota; o goleiro reserva não é oferecido a um meia');
assert.equal(m.suggested.id,23,'o auxiliar sugere o melhor reserva da posição');
const used=injuryDecision(packet({},[{type:'substitution',team:'pal',player:3,relatedPlayer:23}]));
assert.deepEqual(used.reserves.map(r=>r.id),[22,21],'reserva já utilizado não é oferecido');assert.equal(used.changesLeft,4);
const none=injuryDecision(packet({},[1,2,3,4,5].map(i=>({type:'substitution',team:'pal',player:i,relatedPlayer:90+i}))));
assert.equal(none.changesLeft,0);assert.deepEqual(none.reserves,[],'sem trocas restantes só resta continuar com dez');assert.equal(none.suggested,undefined);
const keeperPacket=packet();keeperPacket.session.injuryPlayerId=1;keeperPacket.replay.frames=[{players:[{id:1,name:'Goleiro',position:'GOL',team:0,x:0,z:0,yaw:0,action:'injured'}]}];keeperPacket.replay.events=[{type:'injury',team:'pal',player:1,text:'x',injuryDays:30,injuryName:'Fratura'}];
assert.deepEqual(injuryDecision(keeperPacket).reserves.map(r=>r.id),[24],'goleiro lesionado só pode ser trocado por goleiro');
console.log('PASS: janela de lesão — reservas por posição, trocas restantes, goleiro e retirada com dez.');
