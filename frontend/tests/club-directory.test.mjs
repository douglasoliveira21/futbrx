import assert from 'node:assert/strict';
import {filterClubs,countBy,groupBySeries,isPlayable,releaseFor,leagueName,stateNames,versionLabel} from '../src/features/career/directory.ts';

const club=(id,series,name,city,uf)=>({id,series,name,short:name.slice(0,3).toUpperCase(),color:'#fff',strength:60,city,uf});
const clubs=[
  club('pal','A','Palmeiras','São Paulo','SP'),club('cha','A','Chapecoense','Chapecó','SC'),
  club('avai','B','Avaí','Florianópolis','SC'),club('sport','B','Sport','Recife','PE'),
  club('figueirense','C','Figueirense','Florianópolis','SC'),club('paysandu','C','Paysandu','Belém','PA'),
  club('joinville','D','Joinville','Joinville','SC'),club('ceilandia','D','Ceilândia','Ceilândia','DF'),
];
const directory={versions:{A:['a-misto','a-reais'],B:['b-reais'],C:['c-reais']}};

// Filters by division, state and name.
assert.equal(filterClubs(clubs,{series:'all',uf:'',search:''}).length,8,'sem filtro mostra todos');
assert.deepEqual(filterClubs(clubs,{series:'B',uf:'',search:''}).map(c=>c.id),['avai','sport'],'filtra por divisão');
assert.deepEqual(filterClubs(clubs,{series:'all',uf:'SC',search:''}).map(c=>c.id),['cha','avai','figueirense','joinville'],'filtra por estado, em ordem de divisão');
assert.deepEqual(filterClubs(clubs,{series:'C',uf:'SC',search:''}).map(c=>c.id),['figueirense'],'estado e divisão juntos');
assert.deepEqual(filterClubs(clubs,{series:'all',uf:'',search:'florianopolis'}).map(c=>c.id),['avai','figueirense'],'busca por cidade ignora acentos e maiúsculas');
assert.deepEqual(filterClubs(clubs,{series:'all',uf:'',search:'  AVAI '}).map(c=>c.id),['avai'],'busca por nome com espaços e acentos');
assert.deepEqual(filterClubs(clubs,{series:'all',uf:'',search:'pernambuco'}).map(c=>c.id),['sport'],'busca pelo nome do estado');
assert.deepEqual(filterClubs(clubs,{series:'all',uf:'',search:'sao paulo pal'}).map(c=>c.id),['pal'],'várias palavras precisam aparecer todas');
assert.deepEqual(filterClubs(clubs,{series:'all',uf:'',search:'zzz'}),[],'sem resultado devolve lista vazia');
assert.equal(clubs[0].id,'pal','filtrar não altera a lista original');

// Counts feed the buttons and the state list.
assert.deepEqual(countBy(clubs,'series'),{A:2,B:2,C:2,D:2});
assert.equal(countBy(clubs,'uf').SC,4,'quatro clubes em Santa Catarina');
assert.deepEqual(groupBySeries(filterClubs(clubs,{series:'all',uf:'SC',search:''})).map(g=>[g.series,g.clubs.length]),[['A',1],['B',1],['C',1],['D',1]],'agrupa por divisão');
assert.deepEqual(groupBySeries(filterClubs(clubs,{series:'D',uf:'',search:''})).map(g=>g.series),['D'],'divisões vazias não aparecem');

// Série D is listed but not playable yet.
assert.equal(isPlayable(clubs[0],directory),true);assert.equal(isPlayable(clubs[4],directory),true);assert.equal(isPlayable(clubs[6],directory),false,'Série D em breve');

// Which data version a new career starts from.
const releases=[{id:'r1',version:'a-reais',source:'x'},{id:'r2',version:'a-misto',source:'y'},{id:'r3',version:'b-reais',source:'z'},{id:'r4',version:'c-reais',source:'w'}];
assert.equal(releaseFor(clubs[0],directory,releases).id,'r2','Série A começa pela primeira versão disponível');
assert.equal(releaseFor(clubs[0],directory,releases,'a-reais').id,'r1','e respeita a escolha do jogador');
assert.equal(releaseFor(clubs[0],directory,releases,'inexistente').id,'r2','escolha desconhecida volta à primeira');
assert.equal(releaseFor(clubs[2],directory,releases,'a-reais').id,'r3','escolha de outra série é ignorada');
assert.equal(releaseFor(clubs[6],directory,releases),undefined,'Série D não tem versão de dados');
assert.equal(releaseFor(clubs[0],directory,[releases[3]]),undefined,'sem a versão publicada não há carreira');
assert.equal(releaseFor(clubs[0],directory,releases).source,'y','devolve o registro completo da versão');

assert.equal(leagueName({dataVersion:'serie-b-2026-clubes-reais-v1'}),'Brasileirão Série B');
assert.equal(leagueName({dataVersion:'serie-c-2026-clubes-reais-v1'}),'Brasileirão Série C');
assert.equal(leagueName({dataVersion:'serie-d-2026-grupo-07-v1'}),'Brasileirão Série D');
{const d={...clubs[6],group:7,version:'serie-d-2026-grupo-07-v1'};const dir={versions:{...directory.versions,D:['serie-d-2026-grupo-07-v1']}};const rel=[{id:'g7',version:'serie-d-2026-grupo-07-v1'}];assert.equal(isPlayable(d,dir),true,'Série D jogável quando há versões');assert.equal(releaseFor(d,dir,rel).id,'g7','clube da Série D começa pela versão do seu grupo');assert.equal(releaseFor(d,dir,[{id:'x',version:'serie-d-2026-grupo-08-v1'}]),undefined,'sem a versão do grupo não há carreira');}
assert.equal(leagueName({dataVersion:'serie-a-2026-misto-candidatos-v2'}),'Brasileirão Série A');
assert.equal(leagueName({dataVersion:'fictional-2026-v1',division:'B'}),'Brasileirão Série B','divisão demonstrativa continua valendo fora das bases reais');
assert.equal(Object.keys(stateNames).length,27,'27 unidades da federação');
assert.equal(versionLabel('serie-a-2026-clubes-reais-v1'),'Clubes reais, elencos simulados');
console.log('PASS: escolha de clube por divisão, estado e nome; versão de dados e nome da liga.');
