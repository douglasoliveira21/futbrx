import assert from 'node:assert/strict';
import {aggregate,legLabel,played} from '../src/features/career/ties.ts';

const fx=(home,away,homeGoals=null,awayGoals=null,extra={})=>({round:1,home,away,homeGoals,awayGoals,events:[],seed:1,...extra});
const match=(id,tie,leg,fixture,winner='')=>({id,stage:0,tie,leg,winner,fixture});
const two={legs:2,matches:[
  match('a','t1',1,fx('pal','fla',2,1)),
  match('a-v','t1',2,fx('fla','pal',1,0)),
  match('b','t2',1,fx('cor','sao',null,null)),
  match('b-v','t2',2,fx('sao','cor',null,null)),
]};

assert.equal(legLabel(two,two.matches[0]),'Ida','primeira perna');
assert.equal(legLabel(two,two.matches[1]),'Volta','segunda perna');
assert.equal(legLabel({legs:1,matches:[]},match('x','t',1,fx('a','b'))),'','jogo único não tem rótulo');
assert.equal(legLabel({matches:[]},match('x','t',1,fx('a','b'))),'','competição antiga não tem rótulo');

// Aggregate from the point of view of the second leg's home club: fla 1+1 and pal 0+2.
assert.deepEqual(aggregate(two,two.matches[1]),{home:2,away:2},'agregado soma as duas pernas');
assert.equal(aggregate(two,two.matches[0]),null,'a ida não tem agregado');
assert.equal(aggregate(two,two.matches[3]),null,'volta sem resultado não tem agregado');
const firstLegPending={legs:2,matches:[match('c','t3',1,fx('a','b')),match('c-v','t3',2,fx('b','a',1,0))]};
assert.equal(aggregate(firstLegPending,firstLegPending.matches[1]),null,'sem o placar da ida não há agregado');
assert.equal(aggregate({legs:1,matches:[match('d','t4',1,fx('a','b',1,0))]},{legs:1,matches:[]}.matches[0]??match('d','t4',1,fx('a','b',1,0))),null,'jogo único não tem agregado');

// A first leg is played even though the tie has no winner yet.
assert.equal(played(two.matches[0]),true,'ida disputada conta como jogada');
assert.equal(two.matches[0].winner,'','e ainda sem classificado');
assert.equal(played(two.matches[2]),false,'ida futura não foi jogada');
console.log('PASS: rótulos de ida e volta, agregado e partidas disputadas.');
