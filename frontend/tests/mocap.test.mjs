import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {createHash} from 'node:crypto';
import * as T from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {footballAnimations,contactOffsets} from '../src/features/match3d/presentation.ts';
import {mocapTimings} from '../src/features/match3d/mocapTimings.ts';
globalThis.self=globalThis;
const manager=new T.LoadingManager();manager.addHandler(/user-player-/, {load(url,onLoad){const texture=new T.Texture();queueMicrotask(()=>onLoad(texture));return texture;}});
const dir=new URL('../public/assets/players/',import.meta.url),bytes=await fs.readFile(new URL('user-footballer-mocap.glb',dir));
const report=JSON.parse(await fs.readFile(new URL('user-player-mocap.json',dir),'utf8'));
assert.equal(createHash('sha256').update(bytes).digest('hex'),report.sha256);
const model=await new GLTFLoader(manager).parseAsync(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),'');
assert.equal(model.animations.length,29);for(const a of footballAnimations)assert(model.animations.some(c=>c.name===a.clip),a.clip);
let mesh;model.scene.traverse(o=>{if(o.isSkinnedMesh)mesh=o;});assert.equal(mesh.skeleton.bones.length,41);
const mixer=new T.AnimationMixer(model.scene),v=new T.Vector3();let lowest=100;
for(const clip of model.animations){mixer.stopAllAction();mixer.clipAction(clip).play();for(const phase of [0,.2,.4,.6,.8,.99]){mixer.setTime(phase*clip.duration);model.scene.updateMatrixWorld(true);mesh.skeleton.update();for(let i=0;i<mesh.geometry.attributes.position.count;i+=29){mesh.getVertexPosition(i,v);mesh.localToWorld(v);assert([v.x,v.y,v.z].every(Number.isFinite),clip.name);assert(v.length()<5,clip.name+' deformation');lowest=Math.min(lowest,v.y);}}}
assert(lowest>-.05,'Floor penetration: '+lowest);
for(const [action,t] of Object.entries(mocapTimings)){assert(t.duration>0&&t.duration<t.rawDuration);assert(t.contact>=0&&t.contact<=t.duration);if(action in contactOffsets)assert.equal(contactOffsets[action],t.contact);}
assert(mocapTimings.dive_left.start>4);assert(mocapTimings.dive_low.start>6);
const left=model.animations.find(c=>c.name==='Football_SaveLeft').tracks.find(t=>t.name==='Hip.position'),right=model.animations.find(c=>c.name==='Football_SaveRight').tracks.find(t=>t.name==='Hip.position');
assert(left.values.some((v,i)=>Math.abs(v-right.values[i])>.1),'Side saves must be mirrored');
console.log('PASS: 29 clips, source hashes, finite skinning, floor, trimmed keeper idle, mirrored saves and contact times.');
