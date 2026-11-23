import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {clone} from 'three/addons/utils/SkeletonUtils.js';
import * as THREE from 'three';
import {qualityProfiles,initialQuality,animationForAction,footballAnimations,resolvePresentationPose,contactOffsets} from '../src/features/match3d/presentation.ts';
const bytes=await fs.readFile(new URL('../public/assets/players/footballer.glb',import.meta.url));
assert(bytes.length<2_200_000);assert.equal(bytes.toString('ascii',0,4),'glTF');
const model=await new GLTFLoader().parseAsync(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),'');
const names=new Set(model.animations.map(a=>a.name));for(const {action} of footballAnimations.slice(0,20))assert(names.has(animationForAction(action)));assert.equal(names.size,20);
const first=clone(model.scene),second=clone(model.scene);let a,b;first.traverse(o=>{if(o.isSkinnedMesh)a=o});second.traverse(o=>{if(o.isSkinnedMesh)b=o});assert(a&&b);assert.notEqual(a.skeleton.bones[0],b.skeleton.bones[0]);
assert.equal(a.name,'Footballer_HumanBody');for(const n of ['Hair_Crop','Hair_Sweep','Hair_Curls','KeeperGlove_l','KeeperGlove_r'])assert(first.getObjectByName(n),n);
const mixer=new THREE.AnimationMixer(first),vertex=new THREE.Vector3();
for(const clip of model.animations){for(const track of clip.tracks){assert([...track.values].every(Number.isFinite));assert(first.getObjectByName(track.name.split('.')[0]),track.name);}
 const action=mixer.clipAction(clip);action.play();for(const phase of [0,.25,.5,.75,.99]){mixer.setTime(phase*clip.duration);first.updateMatrixWorld(true);a.skeleton.update();for(let i=0;i<a.geometry.attributes.position.count;i+=13){a.getVertexPosition(i,vertex);assert([vertex.x,vertex.y,vertex.z].every(Number.isFinite));assert(vertex.length()<4,clip.name+' excessive deformation');assert(vertex.y>-.22,clip.name+' penetrates floor: '+vertex.y);}}mixer.stopAllAction();}
const bonePoint=(clipName,boneName,time)=>{mixer.stopAllAction();mixer.clipAction(model.animations.find(c=>c.name===clipName)).play();mixer.setTime(time);first.updateMatrixWorld(true);return first.getObjectByName(boneName).getWorldPosition(new THREE.Vector3());};
assert(bonePoint('Football_Header','Head',.6).y>bonePoint('Football_Idle','Head',0).y+.12,'header jumps');
assert(bonePoint('Football_Shot','foot_r',.572).distanceTo(bonePoint('Football_Idle','foot_r',0))>.2,'shot moves foot');
assert(bonePoint('Football_SaveLeft','pelvis',.752).x>bonePoint('Football_SaveRight','pelvis',.752).x+.8,'opposite dives');
const standingHip=bonePoint('Football_Idle','pelvis',0).clone();
assert(bonePoint('Football_Fall','pelvis',1.2).y<standingHip.y-.25,'fall lowers the body onto the ground');
assert(bonePoint('Football_Fall','pelvis',2.999).distanceTo(standingHip)<.03,'recovery returns to the standing stance');
assert(bonePoint('Football_AirDuel','Head',.7).y>bonePoint('Football_Idle','Head',0).y+.2,'aerial contest has an actual jump');
assert(bonePoint('Football_Tackle','foot_r',.8).distanceTo(bonePoint('Football_Idle','foot_r',0))>.25,'sliding tackle extends the tackling leg');
const before={id:1,action:'run',actionSecond:8},after={id:1,action:'shot',actionSecond:10};assert.equal(resolvePresentationPose(before,after,9.5,8,[],false).action,'shot');assert.equal(resolvePresentationPose(before,after,9,8,[],false).action,'run');assert.equal(contactOffsets.header,.696);
const credits=JSON.parse(await fs.readFile(new URL('../public/data/asset-credits.json',import.meta.url),'utf8'));assert.equal(credits.assets[0].sha256,createHash('sha256').update(bytes).digest('hex'));assert.equal(credits.assets[0].components[0].license,'CC0-1.0');assert(credits.assets[0].components[1].license.includes('projeto'));
assert.equal(initialQuality(4,8),'low');assert.equal(initialQuality(8,8),'balanced');assert.equal(qualityProfiles.low.rigged,true);
const data=JSON.parse(await fs.readFile(new URL('../public/data/real-2026.json',import.meta.url),'utf8'));assert.equal(data.clubs.length,20);assert.equal(new Set(data.clubs.map(c=>c.id)).size,20);assert(data.clubs.some(c=>c.id==='rem'));assert(!data.clubs.some(c=>c.id==='spo'));
console.log('PASS: GLB local, tamanho, animações, esqueletos independentes, skinning, hash/licença, qualidade e participantes de 2026.');

const scaled = resolvePresentationPose({id:1,action:'run',actionSecond:96},{id:1,action:'shot',actionSecond:120},114,96,[],false,12);
assert.equal(scaled.action,'shot'); assert.equal(scaled.actionSecond,10);
assert.equal(resolvePresentationPose({id:1,action:'run',actionSecond:96},{id:1,action:'shot',actionSecond:120},108,96,[],false,12).action,'run');
console.log('PASS: animação do motor v5 usa segundos físicos e antecipa contato sem acelerar o corpo a 12×.');
