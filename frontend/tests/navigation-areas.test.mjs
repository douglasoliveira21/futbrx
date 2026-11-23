import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {areas,areaOf} from '../src/features/navigation/areas.ts';

// Every route of the career shell (the `nav` array in main.tsx) belongs to exactly one area.
const main=await fs.readFile(new URL('../src/app/main.tsx',import.meta.url),'utf8');
const navBlock=main.slice(main.indexOf('const nav = ['),main.indexOf('] as const;'));
const routes=[...navBlock.matchAll(/\["([^"]*)",\s*"/g)].map(m=>m[1]);
assert.ok(routes.length>=20,'rotas lidas do main.tsx ('+routes.length+')');

assert.equal(areas.length,6,'o menu tem seis áreas');
const placed=areas.flatMap(a=>a.pages.map(p=>p.path));
assert.equal(new Set(placed).size,placed.length,'nenhuma tela aparece em duas áreas');
assert.deepEqual([...routes].sort(),[...placed].sort(),'toda rota está em uma área e toda página de área tem rota');
assert.ok(areas.every(a=>a.pages.length>=1&&a.pages.length<=6),'cada área tem de 1 a 6 abas');

assert.equal(areaOf('/').id,'inicio','raiz é o Início');
assert.equal(areaOf('/medico').id,'elenco','Médico fica em Elenco');
assert.equal(areaOf('/contratos/').id,'mercado','aceita barra final');
assert.equal(areaOf('taticas').id,'jogo','aceita caminho de rota sem barra');
assert.equal(areaOf('/conta'),undefined,'telas de conta ficam fora das áreas');
console.log('PASS: seis áreas cobrem todas as telas da carreira, sem repetição.');
