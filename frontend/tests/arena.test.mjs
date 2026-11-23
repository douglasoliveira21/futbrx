import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import * as THREE from 'three';
import {footballGeometry,BALL_RADIUS} from '../src/features/match3d/footballGeometry.ts';
import {bowlSeats,stadiumMoment} from '../src/features/match3d/stadiumDynamics.ts';
import {matchAudioCues} from '../src/features/match3d/matchAudioCues.ts';
const geometry=footballGeometry(),p=geometry.getAttribute('position');
assert.equal(BALL_RADIUS,.11);assert.equal(geometry.userData.pentagons,12);assert.equal(geometry.userData.hexagons,20);
const a=new THREE.Vector3(),b=new THREE.Vector3(),c=new THREE.Vector3();
for(let i=0;i<p.count;i++){a.fromBufferAttribute(p,i);assert(Math.abs(a.length()-.11)<1e-6);}
for(let i=0;i<p.count;i+=3){a.fromBufferAttribute(p,i);b.fromBufferAttribute(p,i+1);c.fromBufferAttribute(p,i+2);assert(b.sub(a).cross(c.sub(a)).dot(a)>0,'ball panels face outwards');}
geometry.dispose();
const seats=bowlSeats(6200);assert(seats.length>5000);assert(seats.every(p=>(Math.abs(p.x)>52.5||Math.abs(p.z)>34)&&[p.x,p.y,p.z,p.yaw].every(Number.isFinite)),'stands stay outside the pitch, including corners');
const center=stadiumMoment(100,{x:0,y:.24,z:0},[]),area=stadiumMoment(100,{x:46,y:.24,z:0},[]);assert(area.danger>center.danger&&area.energy>center.energy);
const goal={type:'goal',minute:2,second:120,team:'home'};
assert.equal(stadiumMoment(100,{x:0,y:0,z:0},[goal],12,'home').reaction,'normal');
assert.equal(stadiumMoment(124,{x:0,y:0,z:0},[goal],12,'home').reaction,'goal');
assert.equal(stadiumMoment(124,{x:0,y:0,z:0},[goal],12,'home').goalSide,1);
assert.equal(stadiumMoment(140,{x:0,y:0,z:0},[goal,{type:'second_half',minute:2,second:130}],12,'home').goalSide,1,'halftime cannot move the earlier goal reaction to the opposite net');
assert.equal(stadiumMoment(2020,{x:0,y:0,z:0},[{type:'second_half',minute:30,second:1800},{...goal,second:2010}],12,'home').goalSide,-1);
assert.equal(stadiumMoment(220,{x:0,y:0,z:0},[goal],12,'home').reaction,'normal');
assert.equal(stadiumMoment(124,{x:0,y:0,z:0},[{type:'shot',minute:2,second:120,text:'Finalização para fora.'}]).reaction,'near-miss');
assert.deepEqual(matchAudioCues(119,120,[goal],[]),[{type:'goal'}]);assert.deepEqual(matchAudioCues(120,121,[goal],[]),[]);
assert.deepEqual(matchAudioCues(10,120,[goal],[]),[],'seeking does not play a burst of old sounds');
assert.deepEqual(matchAudioCues(119,120,[],[{id:1,action:'shot',actionSecond:120,x:40,z:2}]),[{type:'kick',x:40,z:2}]);
assert.deepEqual(matchAudioCues(119,120,[{type:'pass',minute:2,second:120}],[]),[],'pass arrival does not duplicate foot impact');
assert.equal(matchAudioCues(119,120,[{type:'foul',minute:2,second:120},{type:'penalty',minute:2,second:120}],[]).length,1);
const credits=JSON.parse(await readFile(new URL('../public/assets/audio/credits.json',import.meta.url),'utf8'));
assert.equal(credits.assets.length,5);
for(const asset of credits.assets){assert.equal(asset.licence,'CC0-1.0');assert(asset.author&&asset.source.startsWith('https://freesound.org/'));const bytes=await readFile(new URL('../public'+asset.path,import.meta.url));assert.equal(createHash('sha256').update(bytes).digest('hex'),asset.sha256);assert(bytes.length<2_000_000);}
console.log('PASS: bola de 22 cm, painéis fechados, arquibancada fora do campo, tensão/reação sem antecipar gols, áudio sem duplicação e cinco gravações CC0 com hashes.');

const top=seats.filter(p=>p.y>11.8);assert(top.length>200);assert(Math.max(...top.slice(1).map((p,i)=>Math.hypot(p.x-top[i].x,p.z-top[i].z)))<5,'central stands have uniform spacing, without artificial empty wedges');
