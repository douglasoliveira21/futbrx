import assert from 'node:assert/strict';
import {buildInbox,recentForm,tableAround,nextFixture,seasonGoal,goalText,isMatchWeek} from '../src/features/home/inbox.ts';
import {seasonRounds,seasonOver} from '../src/features/career/season.ts';

const money=n=>'R$ '+n;
const club=(id,name)=>({id,name,short:id.toUpperCase(),color:'#fff',strength:70});
const clubs=[club('pal','Palmeiras'),club('fla','Flamengo'),club('cha','Chapecoense'),club('sao','São Paulo'),club('cor','Corinthians'),club('vas','Vasco'),club('bot','Botafogo'),club('gre','Grêmio')];
const player=(id,extra={})=>({id,name:'Jogador '+id,position:'MEI',age:25,rating:70,fitness:95,value:1,wage:1,starter:true,injuryDays:0,injuryName:'',yellowCards:0,suspensionMatches:0,transferListed:false,...extra});
const fixture=(round,home,away,homeGoals=null,awayGoals=null)=>({round,home,away,homeGoals,awayGoals,events:[],seed:1,date:'2026-02-08'});
const row=(c,points)=>({club:c,played:0,wins:0,draws:0,losses:0,goals:0,against:0,difference:0,points});

function career(state={},management={}){
  return {id:'c',name:'Teste',revision:1,
    table:clubs.map((c,i)=>row(c,30-i)),
    management:{window:{isOpen:false,name:'',closingRound:null,nextOpeningRound:null,description:''},wageBill:0,expiringContracts:0,...management},
    state:{season:2026,round:1,clubId:'pal',formation:'4-3-3',mentality:'Equilibrada',training:'Técnico',balance:45e6,
      clubs,squad:[player(1),player(2)],market:[],offers:[],transfers:[],pastSeasons:[],contracts:{},ledger:[],history:[],
      news:[],fixtures:[fixture(1,'cha','pal')],board:{dismissed:false,confidence:65},autoReplaceUnavailable:false,...state}};
}

// First round: welcome, ready team; no transfer or contract messages when nothing is pending.
let inbox=buildInbox(career(),money);
assert.deepEqual(inbox.map(m=>m.id),['welcome','match-ready'],'estreia mostra boas-vindas e time pronto');
assert.equal(nextFixture(career()).away,'pal','próximo jogo é o da rodada atual');

// Urgent items come first, in a stable order.
inbox=buildInbox(career({
  squad:[player(1,{injuryDays:5,injuryName:'Estiramento'}),player(2,{suspensionMatches:1}),player(3,{starter:false,injuryDays:9,injuryName:'Entorse'})],
  offers:[{id:'o1',playerId:2,playerName:'Jogador 2',kind:'outbound',fromClub:'pal',toClub:'fla',fee:9e6,wage:1,years:3,signingBonus:0,season:2026,createdRound:1,responseRound:1,expiresRound:3,contractRevision:1,status:'agreed',message:'',counterFee:null,counterWage:null},
          {id:'o2',playerId:9,playerName:'Alvo',kind:'inbound',fromClub:'sao',toClub:'pal',fee:5e6,wage:1,years:3,signingBonus:0,season:2026,createdRound:1,responseRound:2,expiresRound:3,contractRevision:1,status:'pending',message:'',counterFee:null,counterWage:null}],
  board:{dismissed:false,confidence:30},
  news:['Contrato inicial preservado na migração.','Flamengo vence clássico.'],
},{window:{isOpen:true,name:'Primeira janela',closingRound:1,nextOpeningRound:null,description:''},expiringContracts:2}),money);
const urgent=inbox.filter(m=>m.tone==='urgent').map(m=>m.id);
assert.deepEqual(urgent,['offer-o1','unavailable-starters','transfer-window','board-pressure'],'pendências urgentes vêm primeiro');
assert.ok(inbox.findIndex(m=>m.tone!=='urgent')>=urgent.length,'nada urgente fica abaixo de uma mensagem comum');
assert.equal(inbox.find(m=>m.id==='offer-o1').body,'Flamengo oferece R$ 9000000 e aguarda sua decisão.','proposta recebida cita comprador e valor');
assert.ok(!inbox.some(m=>m.id==='offer-o2'),'proposta ainda sem resposta não pede decisão');
assert.match(inbox.find(m=>m.id==='unavailable-starters').title,/2 titulares/,'conta titulares indisponíveis');
assert.equal(inbox.find(m=>m.id==='injuries').title,'Jogador 3 segue em tratamento','lesão de reserva vai ao departamento médico');
assert.deepEqual(inbox.filter(m=>m.sender==='press').map(m=>m.title),['Flamengo vence clássico.'],'nota técnica não vira notícia');
assert.ok(!inbox.some(m=>m.id==='match-ready'),'auxiliar não diz que o time está pronto quando há desfalques');

// Automatic replacement turns missing starters into a reminder instead of an urgent block.
inbox=buildInbox(career({squad:[player(1,{injuryDays:3})],autoReplaceUnavailable:true}),money);
assert.equal(inbox.find(m=>m.id==='unavailable-starters').tone,'normal','substituição automática reduz a urgência');

// Dismissal replaces the board messages.
inbox=buildInbox(career({coach:{employed:false,name:'T',reputation:50,date:'',unreadDismissal:true},board:{dismissed:true,confidence:10}}),money);
assert.equal(inbox[0].id,'dismissal','demissão aparece no topo');
assert.ok(!inbox.some(m=>m.id==='board-pressure'),'sem cobrança da diretoria depois da demissão');

// Form and table helpers.
const played=career({round:4,fixtures:[fixture(1,'pal','fla',2,0),fixture(2,'cha','pal',1,1),fixture(3,'pal','sao',0,1),fixture(4,'pal','gre')]});
assert.deepEqual(recentForm(played,'pal').map(f=>f.result),['V','E','D'],'forma em ordem cronológica');
const middle=career({clubId:'bot'});
assert.deepEqual(tableAround(middle).map(r=>r.position),[1,4,5,6,7,8],'líder e vizinhança da posição, encostada no fim da tabela');
assert.deepEqual(tableAround(career({clubId:'cor'})).map(r=>r.position),[1,3,4,5,6,7],'líder e duas posições acima e abaixo');
assert.deepEqual(tableAround(career()).map(r=>r.position),[1,2,3,4,5],'líder não se repete no topo');
// Season goal: the board's "Top N" measured against the table.
const teams=Array.from({length:6},(_,i)=>club('t'+i,'Time '+i));teams[2]=club('pal','Palmeiras');
const league=(points,played)=>({...career({clubs:teams,round:played+1}),table:teams.map((c,i)=>({...row(c,points[i]),played}))});
let goal=seasonGoal(league([0,0,0,0,0,0],0),3);
assert.equal(goal.status,'not-started','sem jogos, a temporada ainda não começou');
assert.equal(goal.total,10,'seis clubes jogam dez rodadas');
assert.equal(goalText(goal).tone,'neutral');
goal=seasonGoal(league([9,7,6,3,3,1],4),3);
assert.deepEqual([goal.status,goal.position,goal.gap,goal.rival.name,goal.rival.position,goal.remaining],['on-track',3,3,'Time 3',4,6],'dentro da meta: folga sobre o primeiro fora');
assert.match(goalText(goal).detail,/3 pontos de folga sobre o Time 3 \(4º\) · restam 6 jogos/,'texto da folga');
goal=seasonGoal(league([9,7,5,5,3,1],4),3);
assert.match(goalText(goal).detail,/Sem folga: empatado em pontos com o Time 3/,'folga zero');
// Palmeiras is third in the fixture table; move the goal to the top place to be behind.
goal=seasonGoal(league([9,7,5,3,3,1],4),1);
assert.deepEqual([goal.status,goal.gap,goal.rival.name,goal.rival.position],['behind',4,'Time 0',1],'abaixo da meta: pontos que faltam');
assert.equal(goalText(goal).badge,'Abaixo da meta');assert.equal(goalText(goal).tone,'bad');
assert.match(goalText(goal).detail,/Faltam 4 pontos para alcançar o Time 0 \(1º\)/,'texto do que falta');
goal=seasonGoal(league([9,7,5,3,3,1],4),1);goal=seasonGoal(league([9,9,8,3,3,1],10),1);
assert.deepEqual([goal.status,goal.remaining],['missed',0],'liga encerrada fora da meta');
assert.match(goalText(goal).detail,/Terminou em 3º; a diretoria esperava o 1º/);
goal=seasonGoal(league([9,9,8,3,3,1],10),4);
assert.equal(goal.status,'achieved','liga encerrada dentro da meta');assert.equal(goalText(goal).badge,'Meta cumprida');
assert.equal(seasonGoal(league([9,9,8,3,3,1],10),99).target,6,'meta limitada ao número de clubes');
assert.equal(seasonGoal(league([0,0,0,0,0,0],0),NaN),null,'meta inválida não gera cartão');
assert.match(buildInbox(career(),money,6).find(m=>m.id==='welcome').body,/entre os 6 primeiros/,'boas-vindas citam a meta da diretoria');
assert.doesNotMatch(buildInbox(career(),money).find(m=>m.id==='welcome').body,/primeiros/,'sem meta conhecida, o texto não inventa uma');
// Weeks without a match for the club: the next match may be weeks away.
let bye=career({round:1,fixtures:[fixture(3,'pal','fla')]});
assert.equal(nextFixture(bye).round,3,'próximo jogo é o da terceira semana');
assert.equal(isMatchWeek(bye),false,'semana 1 não tem partida do clube');
assert.equal(isMatchWeek(career({round:3,fixtures:[fixture(3,'pal','fla')]})),true,'na semana do jogo há partida');
assert.equal(isMatchWeek(career({round:3,fixtures:[fixture(3,'pal','fla',1,0)]})),true,'partida já jogada na rodada atual ainda conta como a da semana');
const cupDue=fixture(2,'pal','cor');
assert.equal(nextFixture(career({round:2,nextCup:cupDue,fixtures:[fixture(3,'pal','fla')]})),cupDue,'copa devida vem antes da liga');
assert.equal(isMatchWeek(career({round:2,nextCup:cupDue,fixtures:[fixture(3,'pal','fla')]})),true,'copa devida é semana de jogo');
bye=career({round:2,fixtures:[fixture(1,'pal','fla',2,0),fixture(4,'cha','pal'),fixture(5,'pal','sao')]});
assert.deepEqual([nextFixture(bye).round,isMatchWeek(bye)],[4,false],'jogos já disputados não contam como próximos');
assert.deepEqual([nextFixture(career({round:39,fixtures:[fixture(38,'pal','fla',1,1)]})),isMatchWeek(career({round:39,fixtures:[fixture(38,'pal','fla',1,1)]}))],[undefined,false],'sem jogos pendentes a temporada acabou');
assert.equal(seasonRounds({}),38,'save antigo tem 38 rodadas');
assert.equal(seasonRounds({seasonRounds:42}),42,'tamanho próprio da temporada');
assert.deepEqual([seasonOver({round:38}),seasonOver({round:39}),seasonOver({round:42,seasonRounds:42}),seasonOver({round:43,seasonRounds:42})],[false,true,false,true],'fim da temporada segue o tamanho dela');
console.log('PASS: caixa de entrada, forma recente e tabela compacta da tela inicial.');
