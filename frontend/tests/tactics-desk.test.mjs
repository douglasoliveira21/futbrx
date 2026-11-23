import assert from 'node:assert/strict';
import {mentalities,defaultTemplates,roleOrder,startersOf,rosterOf,mapPositions,toPercent,templateFor,quickPlans,planKey} from '../src/features/tactics/tacticsModel.ts';

const player=(id,position,starter=true,extra={})=>({id,name:'J'+id,position,age:25,rating:70,fitness:90,value:1,wage:1,starter,injuryDays:0,injuryName:'',yellowCards:0,suspensionMatches:0,transferListed:false,...extra});
const squad=[player(30,'ATA'),player(2,'MEI'),player(11,'LAT'),player(0,'GOL'),player(7,'ZAG'),player(5,'ZAG'),player(40,'MEI',false),player(41,'GOL',false),player(42,'ATA',false)];

assert.equal(mentalities.length,9,'as nove mentalidades do vestiário da partida');
assert.ok(mentalities.includes('Ataque total')&&mentalities.includes('Ultradefensiva'),'extremos disponíveis');
assert.deepEqual(Object.keys(defaultTemplates),['4-3-3','4-4-2','3-5-2','4-2-3-1'],'formações guardadas na carreira');

// Same order as the match team: goalkeeper, centre-backs, full-backs, midfield, attack; ties by id.
assert.deepEqual(startersOf(squad).map(p=>p.id),[0,5,7,11,2,30],'titulares por função e id');
assert.deepEqual([roleOrder('GOL'),roleOrder('ZAG'),roleOrder('LAT'),roleOrder('MEI'),roleOrder('ATA')],[0,1,2,3,4],'ordem das funções');
assert.deepEqual(rosterOf(squad,[41,40]).map(p=>p.id),[0,5,7,11,2,30,41,40,42],'camisas: titulares, banco na ordem do banco, depois o resto');
assert.equal(squad.filter(p=>p.starter).length,6,'startersOf não altera o elenco');

// A catalog layout is assigned to the starters in role order.
const starters=startersOf(squad);
const points=starters.map((_,i)=>({x:1-i*.3,z:i/10}));
const mapped=mapPositions(points,starters);
assert.deepEqual(mapped[0],points[0],'goleiro recebe o primeiro ponto');
assert.deepEqual(mapped[30],points[5],'último titular recebe o último ponto');
assert.deepEqual(Object.keys(mapPositions(points.slice(0,3),starters)).map(Number).sort((a,b)=>a-b),[0,5,7],'sem ponto sobrando, ninguém fica sem lugar errado');

// Pitch drawing is the inverse of what dragging stores (adjust in TacticalPitch).
const stored={x:.5,z:-.4};
const drawn=toPercent(stored);
assert.deepEqual([drawn.x,drawn.y],[30,25],'ponto tático para percentual do campo');
assert.ok(Math.abs((1-drawn.y/50)-stored.x)<1e-9&&Math.abs((drawn.x/50-1)-stored.z)<1e-9,'conversão inversa ao arrastar');

// The saved positions tell which catalog layout they came from.
const formations=[{id:'a',positions:points},{id:'b',positions:points.map(p=>({x:p.x*.5,z:-p.z}))}];
assert.equal(templateFor(mapped,formations,starters,'433'),'a','reconhece o layout salvo');
assert.equal(templateFor(undefined,formations,starters,'433'),'433','sem posições vale a formação base');
assert.equal(templateFor({},formations,starters,'433'),'433','posições vazias valem a formação base');
assert.equal(templateFor({...mapped,30:{x:0,z:0}},formations,starters,'433'),'','posição mexida à mão vira personalizada');

// Quick tactics keep the rest of the plan and change only what they promise.
const plan={style:'Equilibrado',pressure:'Média',width:'Normal',buildShort:true,overlap:false,shootMore:false,avoidFouls:false,offsideTrap:false,attackSide:'Ambos',roles:{0:'Livre'}};
const quick=Object.fromEntries(quickPlans(plan,'Cautelosa').map(q=>[q.label,q]));
assert.equal(Object.keys(quick).length,6,'seis táticas rápidas');
assert.equal(quick['Mais ofensivo'].mentality,'Ofensiva');assert.equal(quick['Mais ofensivo'].plan.overlap,true);
assert.deepEqual([quick['Mais defensivo'].mentality,quick['Mais defensivo'].plan.width,quick['Mais defensivo'].plan.pressure],['Defensiva','Fechada','Baixa']);
assert.equal(quick['Pressão alta'].mentality,'Cautelosa','pressão alta mantém a mentalidade atual');
assert.equal(quick['Pressão alta'].plan.pressure,'Alta');
assert.equal(quick['Contra-ataque'].plan.style,'Contra-ataque');assert.equal(quick['Contra-ataque'].plan.buildShort,false);
assert.deepEqual(quick['Manter posse'].plan.roles,{0:'Livre'},'instruções individuais são preservadas');
assert.equal(plan.pressure,'Média','o plano original não é alterado');

assert.notEqual(planKey(plan,'4-3-3','Equilibrada'),planKey(plan,'4-3-3','Ataque total'),'mudar a mentalidade deixa o plano pendente');
assert.equal(planKey(plan,'4-3-3','Equilibrada'),planKey({...plan},'4-3-3','Equilibrada'),'mesmo conteúdo, mesma chave');
console.log('PASS: vestiário tático antes da partida (mentalidades, ordem do elenco, formações do catálogo, táticas rápidas).');
