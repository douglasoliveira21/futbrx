import fs from 'node:fs/promises';import {createHash} from 'node:crypto';
import * as T from '../../frontend/node_modules/three/build/three.module.js';
import {FBXLoader} from '../../frontend/node_modules/three/examples/jsm/loaders/FBXLoader.js';
export const plan=[
 {action:'throw_in',clip:'Football_ThrowIn',file:'07_Throw_In',start:5.75,end:7.75,contact:6.67},
 {action:'pass',clip:'Football_Pass',file:'08_Side_Foot_Kick',start:1.1,end:1.98,contact:1.42},
 {action:'shot',clip:'Football_Shot',file:'09_Power_Kick',start:2.08,end:3.2,contact:2.58},
 {action:'penalty_kick',clip:'Football_Penalty',file:'10_Penalty_Kick_01',start:5.45,end:7.2,contact:6.55},
 {action:'celebrate',clip:'Football_Celebrate',file:'12_Goal_Celebration_01',start:.2,end:3.45},
 {action:'celebrate_2',clip:'Football_Celebrate2',file:'13_Goal_Celebration_02',start:.2,end:3.5},
 {action:'celebrate_3',clip:'Football_Celebrate3',file:'14_Goal_Celebration_03',start:.2,end:3.15},
 {action:'dive_left',clip:'Football_SaveLeft',file:'15_Goalkeeper_Save_01',start:4.65,end:6.15,contact:5.25,translate:true},
 {action:'dive_right',clip:'Football_SaveRight',file:'15_Goalkeeper_Save_01',start:4.65,end:6.15,contact:5.25,translate:true,mirror:true},
 {action:'dive_low',clip:'Football_SaveLow',file:'17_Goalkeeper_Save_03',start:6.15,end:8.7,contact:7.15},
 {action:'defend',clip:'Football_Defend',file:'18_Defending_01',start:.25,end:6.2},
 {action:'challenge',clip:'Football_Challenge',file:'18_Defending_01',start:3.5,end:4.8,contact:4.0},
 {action:'yellow_card',clip:'Football_YellowCard',file:'20_Yellow_Card',start:.18,end:2.85,contact:.72},
 {action:'red_card',clip:'Football_RedCard',file:'21_Yellow_And_Red_Card',start:2.35,end:5.25,contact:3.0},
 {action:'second_yellow_card',clip:'Football_SecondYellow',file:'21_Yellow_And_Red_Card',start:.18,end:5.3,contact:.8,redAt:2.55}
];
const map={Hip:'Hips',Waist:'Spine1',Spine01:'Spine2',Spine02:'Spine4',NeckTwist01:'Neck',Head:'Head',L_Clavicle:'LeftShoulder',R_Clavicle:'RightShoulder',L_Upperarm:'LeftArm',R_Upperarm:'RightArm',L_Forearm:'LeftForeArm',R_Forearm:'RightForeArm',L_Hand:'LeftHand',R_Hand:'RightHand',L_Thigh:'LeftThigh',R_Thigh:'RightThigh',L_Calf:'LeftShin',R_Calf:'RightShin',L_Foot:'LeftFoot',R_Foot:'RightFoot',L_ToeBase:'LeftToe',R_ToeBase:'RightToe'};
export async function mocapLibrary(root,mesh,raw=false){
 root.updateMatrixWorld(true);const bones=[];const rest=new Map();root.traverse(b=>{if(b.isBone){bones.push(b);rest.set(b.name,{q:b.quaternion.clone(),p:b.position.clone(),wq:b.getWorldQuaternion(new T.Quaternion()),wp:b.getWorldPosition(new T.Vector3())});}});
 const clips=[],sources=[];const cache=new Map();const oldLoad=T.TextureLoader.prototype.load;T.TextureLoader.prototype.load=function(){return new T.Texture();};
 try{for(const item of plan){
  let data=cache.get(item.file);if(!data){const bytes=await fs.readFile(new URL('../../.runtime/assets/mocap/'+item.file+'.fbx',import.meta.url));const fbx=new FBXLoader().parse(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),'');const group=new T.Group();group.scale.setScalar(.01);group.add(fbx);group.updateMatrixWorld(true);const sourceBones=new Map();group.traverse(b=>{if(b.isBone&&!sourceBones.has(b.name))sourceBones.set(b.name,b);});const sourceRest=new Map([...sourceBones].map(([name,b])=>[name,{q:b.getWorldQuaternion(new T.Quaternion()),p:b.getWorldPosition(new T.Vector3())}]));data={root:group,bones:sourceBones,rest:sourceRest,clip:fbx.animations[0],hash:createHash('sha256').update(bytes).digest('hex')};cache.set(item.file,data);}
  const mixer=new T.AnimationMixer(data.root);mixer.clipAction(data.clip).play();const start=raw?0:item.start,end=raw?data.clip.duration:item.end,duration=end-start;
  mixer.setTime(Math.max(.001,start));data.root.updateMatrixWorld(true);const anchor=data.bones.get('Hips').getWorldPosition(new T.Vector3());
  const steps=Math.ceil(duration*30),times=Array.from({length:steps+1},(_,i)=>i/steps*duration),values=new Map(Object.keys(map).map(n=>[n,[]])),positions=[];
  for(const t of times){
   mixer.setTime(Math.min(start+t,data.clip.duration-.00001));data.root.updateMatrixWorld(true);
   for(const b of bones){b.quaternion.copy(rest.get(b.name).q);b.position.copy(rest.get(b.name).p);}root.updateMatrixWorld(true);
   for(const b of bones){let from=map[b.name];if(!from)continue;if(item.mirror)from=from.replace(/Left|Right/g,v=>v==='Left'?'Right':'Left');const sourceBone=data.bones.get(from);if(!sourceBone)throw Error('Missing mocap bone '+from);
    const delta=sourceBone.getWorldQuaternion(new T.Quaternion()).multiply(data.rest.get(from).q.clone().invert());if(item.mirror){delta.y*=-1;delta.z*=-1;}
    b.quaternion.copy(b.parent.getWorldQuaternion(new T.Quaternion()).invert().multiply(delta.multiply(rest.get(b.name).wq)));
    if(b.name==='Hip'){const now=sourceBone.getWorldPosition(new T.Vector3()),dy=now.y-data.rest.get(from).p.y;const move=new T.Vector3(item.translate?T.MathUtils.clamp((now.x-anchor.x)*(item.mirror?-1:1),-1.8,1.8):0,dy,0);b.position.copy(b.parent.worldToLocal(rest.get(b.name).wp.clone().add(move)));}
    b.updateMatrixWorld(true);values.get(b.name).push(...b.quaternion.toArray());
   }
   root.updateMatrixWorld(true);mesh.skeleton.update();let lowest=0;const p=new T.Vector3();for(let i=0;i<mesh.geometry.attributes.position.count;i+=3){mesh.getVertexPosition(i,p);mesh.localToWorld(p);lowest=Math.min(lowest,p.y);}
   const hip=root.getObjectByName('Hip');if(lowest<-.006){const world=hip.getWorldPosition(new T.Vector3());world.y-=lowest+.006;hip.position.copy(hip.parent.worldToLocal(world));}positions.push(...hip.position.toArray());
  }
  const tracks=Object.keys(map).map(n=>new T.QuaternionKeyframeTrack(n+'.quaternion',times,values.get(n)));tracks.push(new T.VectorKeyframeTrack('Hip.position',times,positions));clips.push(new T.AnimationClip(item.clip,duration,tracks));
  sources.push({...item,sourceDuration:data.clip.duration,sha256:data.hash,duration,contactOffset:item.contact===undefined?undefined:item.contact-start});mixer.stopAllAction();
 }
 }finally{T.TextureLoader.prototype.load=oldLoad;for(const b of bones){b.position.copy(rest.get(b.name).p);b.quaternion.copy(rest.get(b.name).q);}root.updateMatrixWorld(true);}
 return {clips,sources};
}
