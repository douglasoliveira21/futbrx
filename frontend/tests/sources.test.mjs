import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {createHash} from 'node:crypto';
const root=new URL('../public/',import.meta.url);
const calendar=JSON.parse(await fs.readFile(new URL('data/calendar-2026.json',root),'utf8'));
assert.equal(calendar.license,'CC0-1.0');assert.equal(calendar.fixtures.length,380);
assert.equal(new Set(calendar.fixtures.map(f=>f.home+'/'+f.away)).size,380);
for(let n=1;n<=38;n++){const round=calendar.fixtures.filter(f=>f.round===n);assert.equal(round.length,10);assert.equal(new Set(round.flatMap(f=>[f.home,f.away])).size,20);}
assert(calendar.fixtures.every(f=>!('homeGoals'in f)&&!('awayGoals'in f)),'real scores must not overwrite simulation');
const crests=JSON.parse(await fs.readFile(new URL('data/crest-credits.json',root),'utf8'));
const crestIds=new Set(crests.clubs.map(c=>c.clubId));
assert.equal(crestIds.size,crests.clubs.length,'every crest credit has a unique club');
for(const club of new Set(calendar.fixtures.flatMap(f=>[f.home,f.away])))assert(crestIds.has(club),`missing crest status for calendar club ${club}`);
for(const c of crests.clubs){if(c.status!=='included'){assert.equal(c.path,null);continue;}
 assert(c.source.startsWith('https://commons.wikimedia.org/wiki/File:'));assert(c.license);assert(c.author);assert(c.licenseUrl.startsWith('https://'));
 const b=await fs.readFile(new URL(c.path.slice(1),root));assert.equal(createHash('sha256').update(b).digest('hex'),c.sha256);assert.equal(b.length,c.bytes);
 if(c.path.endsWith('.svg'))assert(!/<(?:\w+:)?(?:script|foreignObject)\b|\bon(?:load|error|click)\s*=/i.test(b.toString()));
}
const approved=JSON.parse(await fs.readFile(new URL('../../data/real-2026/approved-players.json',import.meta.url),'utf8'));
if(approved.length===0)await assert.rejects(fs.access(new URL('../../data/real-2026/catalog-reviewed.json',import.meta.url)),'empty review must not produce a playable real catalog');
console.log('PASS: calendário completo sem placares importados, escudos com hash/créditos e bloqueio da base incompleta.');
