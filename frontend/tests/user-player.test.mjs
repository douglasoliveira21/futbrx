import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {createHash} from 'node:crypto';
import * as T from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {clone} from 'three/addons/utils/SkeletonUtils.js';
import {matchKitColors,colorDistance} from '../src/features/match3d/kitColors.ts';
import {footballAnimations,qualityProfiles} from '../src/features/match3d/presentation.ts';
globalThis.self=globalThis;
const manager=new T.LoadingManager();manager.addHandler(/user-player-/, {load(url,onLoad){const t=new T.Texture();queueMicrotask(()=>onLoad(t));return t;}});
const dir=new URL('../public/assets/players/',import.meta.url),bytes=await fs.readFile(new URL('user-footballer.glb',dir));
const report=JSON.parse(await fs.readFile(new URL('user-player.json',dir),'utf8'));
assert.equal(createHash('sha256').update(bytes).digest('hex'),report.sha256);assert(bytes.length<3_000_000);assert.equal(report.triangles,8901);
for(const asset of report.textures){const data=await fs.readFile(new URL('../public'+asset.path,import.meta.url));assert.equal(createHash('sha256').update(data).digest('hex'),asset.sha256);assert(data.length<2_000_000);}
const model=await new GLTFLoader(manager).parseAsync(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),'');
assert.equal(model.animations.length,20);for(const a of footballAnimations.slice(0,20))assert(model.animations.some(c=>c.name===a.clip));
const first=clone(model.scene),second=clone(model.scene);let mesh,other;first.traverse(o=>{if(o.isSkinnedMesh)mesh=o;});second.traverse(o=>{if(o.isSkinnedMesh)other=o;});
assert.equal(mesh.name,'User_Footballer');assert.notEqual(mesh.skeleton.bones[0],other.skeleton.bones[0]);assert.equal(mesh.skeleton.bones.length,41);assert(mesh.geometry.getAttribute('_kitzone'));
const mixer=new T.AnimationMixer(first),v=new T.Vector3();let lowest=100,highest=-100;
for(const clip of model.animations){mixer.stopAllAction();mixer.clipAction(clip).play();for(const phase of [0,.15,.35,.55,.75,.99]){mixer.setTime(phase*clip.duration);first.updateMatrixWorld(true);mesh.skeleton.update();for(let i=0;i<mesh.geometry.attributes.position.count;i+=19){mesh.getVertexPosition(i,v);mesh.localToWorld(v);assert([v.x,v.y,v.z].every(Number.isFinite),clip.name+' finite skinning');assert(v.length()<4,clip.name+' exploded mesh');lowest=Math.min(lowest,v.y);highest=Math.max(highest,v.y);}}}
assert(lowest>-.04,'Animation penetrates the ground excessively: '+lowest);assert(highest<3.5);
for(const home of ['#176b3a','#e34b56','#ffffff','#161e2c','#f1bc20'])for(const away of ['#176b3a','#e34b56','#ffffff','#161e2c','#f1bc20']){const kit=matchKitColors(home,away);assert.equal(kit.outfield[0],home);assert(colorDistance(...kit.outfield)>=105,'Visiting alternative separates similar team kits');assert.notEqual(kit.keepers[0],kit.keepers[1]);for(const k of kit.keepers)for(const f of kit.outfield)assert(colorDistance(k,f)>70,'Keepers remain distinct from outfield');}
assert(qualityProfiles.low.rigged,'Supplied mesh is used in all graphic profiles');
console.log(`PASS: supplied mesh, original texture hashes, 41 independent bones, 20 retargeted clips, bounded skinning (minimum Y ${lowest.toFixed(3)}), team/keeper colour separation and all quality profiles.`);
