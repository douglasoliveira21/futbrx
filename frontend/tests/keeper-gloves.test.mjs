import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import * as T from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {createKeeperGloves} from '../src/features/match3d/keeperGloves.ts';
globalThis.self=globalThis;
const manager=new T.LoadingManager();manager.addHandler(/user-player-/, {load(u,cb){const texture=new T.Texture();queueMicrotask(()=>cb(texture));return texture;}});
const bytes=await fs.readFile(new URL('../public/assets/players/user-footballer-mocap.glb',import.meta.url));
const model=await new GLTFLoader(manager).parseAsync(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),'');
const body=model.scene.getObjectByName('User_Footballer'),gloves=createKeeperGloves(model.scene);
assert.equal(gloves.length,2);
for(const glove of gloves){
  assert(glove.isSkinnedMesh);assert.equal(glove.skeleton,body.skeleton);
  assert.equal(glove.bindMatrix.equals(body.bindMatrix),true);
  const geometry=glove.geometry;assert(geometry.attributes.position.count>1000,'Must contain actual hand topology');
  assert(geometry.attributes.color.count===geometry.attributes.position.count);
  for(let i=0;i<geometry.attributes.skinWeight.count;i++)assert(Math.abs([0,1,2,3].reduce((sum,c)=>sum+geometry.attributes.skinWeight.getComponent(i,c),0)-1)<.00001);
}
const mixer=new T.AnimationMixer(model.scene),point=new T.Vector3();
for(const name of ['Football_KeeperReady','Football_Catch','Football_SaveLow','Football_SaveLeft','Football_SaveRight']){
  mixer.stopAllAction();mixer.clipAction(model.animations.find(a=>a.name===name)).play();
  const duration=model.animations.find(a=>a.name===name).duration;
  for(const phase of [0,.25,.5,.75,.99]){
    mixer.setTime(phase*duration);model.scene.updateMatrixWorld(true);body.skeleton.update();
    for(const glove of gloves){const wrist=model.scene.getObjectByName(glove.userData.side+'_Hand').getWorldPosition(new T.Vector3());
      for(let i=0;i<glove.geometry.attributes.position.count;i+=7){glove.getVertexPosition(i,point);glove.localToWorld(point);assert(point.toArray().every(Number.isFinite));assert(point.distanceTo(wrist)<.25,`${name}: glove detached from hand`);}
    }
  }
}
console.log('PASS: two anatomical skinned gloves, complete hand topology, normalized weights and wrist attachment through five keeper animations.');
