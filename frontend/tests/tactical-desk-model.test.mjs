import assert from 'node:assert/strict';
import { tacticalDeskModel } from '../src/features/match3d/tacticalDeskModel.ts';

const player={id:7,name:'Atleta',position:'ATA',rating:70,fitness:85};
const setup={home:{id:'a',players:[player]},away:{id:'b',players:[]}};
const packet={session:{managedTeam:'a',fixture:{setup},halfTimeAwaiting:false},replay:{setup,frames:[{second:100,players:[{id:7,team:0,x:30,z:8,active:true}]}],events:[
 {type:'goal',team:'a',player:7,minute:1,second:90},
 {type:'yellow_card',team:'a',player:7,minute:1,second:95},
 {type:'goal',team:'b',player:8,minute:2,second:120},
 {type:'red_card',team:'a',player:7,minute:3,second:150},
 {type:'goal',team:'b',player:8,minute:4,second:-1},
 {type:'second_half',team:'a',player:0,minute:45,second:2700}
]},complete:false};
const view=tacticalDeskModel(packet);
assert.deepEqual(view.score,[1,0],'future goals never enter the live scoreboard');
assert.equal(view.half,'1º tempo');
assert.equal(view.players[0].yellow,1);
assert.equal(view.players[0].red,false,'future cards are not displayed');
assert(view.players[0].x>=0&&view.players[0].x<=100&&view.players[0].y>=0&&view.players[0].y<=100);
assert.equal(tacticalDeskModel({...packet,session:{...packet.session,halfTimeAwaiting:true}}).half,'Intervalo');
assert.deepEqual(tacticalDeskModel({...packet,replay:{...packet.replay,frames:[]}}).score,[0,0]);
console.log('PASS: placar e campo usam somente eventos recebidos até o quadro atual.');
