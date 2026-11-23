import assert from 'node:assert/strict';
import {framePair,lerp,lerpYaw,broadcastPosition,broadcastClock,replayBallHeight} from '../src/features/match3d/playback.ts';
for(const height of [.11,.24,.5,1.4,2.44])assert.equal(replayBallHeight('positional-v11',height),height,'V11 preserves the recorded physical centre');
assert.equal(replayBallHeight('positional-v10',.24),.11,'legacy replay keeps its old visual origin');
assert.equal(replayBallHeight('positional-v11',-.1),.11,'renderer keeps the ball above the ground');
for(const height of [.11,.24,.5,1.4,2.44])assert.equal(replayBallHeight('positional-v12',height),height,'V12 preserves the recorded physical centre');
console.log('PASS: V11 reproduz altura física sem deslocamento legado; versões antigas preservam apresentação.');
const frame=(second,x)=>({second,players:[{id:1,team:0,x,z:0,yaw:0,action:'run'}],ball:{x,y:.24,z:0}});
const frames=[frame(0,0),frame(6,12),frame(12,18)];
assert.equal(framePair([],3),null);
assert.equal(framePair(frames,-1).alpha,0);
assert.equal(framePair(frames,99).a.second,12);
const middle=framePair(frames,3);assert.equal(lerp(middle.a.ball.x,middle.b.ball.x,middle.alpha),6);
assert.equal(framePair(frames,6).a.second,6);
assert(Math.abs(Math.abs(lerpYaw(Math.PI-.1,-Math.PI+.1,.5))-Math.PI)<1e-8);
console.log('PASS: busca binária dos frames, limites, interpolação de posição e rotação pelo arco mais curto.');

assert.equal(broadcastPosition(10, 24, .1, 12, true), 11.2);
assert.equal(broadcastPosition(10, 24, 20, 12, true), 12.4);
assert.equal(broadcastPosition(23, 24, .2, 12, true), 24);
assert.equal(broadcastPosition(10, 24, .1, 12, false), 10);
console.log('PASS: transmissão interpola sem ultrapassar frames recebidos, sem salto após suspensão e com pausa.');

assert.equal(broadcastClock(42), '00:42');
assert.equal(broadcastClock(2760), '45+01:00');
assert.equal(broadcastClock(2940,2880), '46:00');
assert.equal(broadcastClock(5640,2880), '90+01:00');
console.log('PASS: relógio de transmissão distingue tempos e acréscimos.');
