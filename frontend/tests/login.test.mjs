import assert from 'node:assert/strict';
import {STAGE,stageScale,COMPACT_WIDTH} from '../src/features/auth/loginLayout.ts';

assert.deepEqual(STAGE,{width:1648,height:928},'composição de referência do mockup');
assert.equal(stageScale(1648,928),1,'na resolução de referência a escala é 1');
assert.equal(stageScale(1920,1080).toFixed(3),(1080/928).toFixed(3),'1920x1080 é limitada pela altura');
assert.equal(stageScale(2560,1440).toFixed(3),(1440/928).toFixed(3),'2560x1440 escala proporcionalmente');
assert.equal(stageScale(3840,2160).toFixed(3),(2160/928).toFixed(3),'4K escala proporcionalmente');
assert.equal(stageScale(3440,1440).toFixed(3),(1440/928).toFixed(3),'ultrawide é limitado pela altura, sem deformar');
assert.equal(stageScale(1280,720).toFixed(3),(720/928).toFixed(3),'1280x720 encolhe a composição inteira');
assert.equal(stageScale(1366,768),768/928,'1366x768 é limitado pela altura');assert.equal(stageScale(1280,1024),1280/1648,'tela quase quadrada é limitada pela largura');
assert.ok(390<COMPACT_WIDTH&&768<COMPACT_WIDTH&&1280>COMPACT_WIDTH,'celular e tablet usam a coluna; desktop usa a composição');
console.log('PASS: escala da tela de login para as resoluções de referência e limite do layout compacto.');
