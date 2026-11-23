import fs from 'node:fs/promises';
import {createHash} from 'node:crypto';
import * as T from '../../frontend/node_modules/three/build/three.module.js';
import {FBXLoader} from '../../frontend/node_modules/three/examples/jsm/loaders/FBXLoader.js';
import {GLTFLoader} from '../../frontend/node_modules/three/examples/jsm/loaders/GLTFLoader.js';
import {GLTFExporter} from '../../frontend/node_modules/three/examples/jsm/exporters/GLTFExporter.js';
const sourceUrl=new URL('../../.runtime/assets/user/soccer_player_rigged.fbx',import.meta.url);
const dest=new URL('../../frontend/public/assets/players/',import.meta.url);
const blobs=[];globalThis.window={URL:{createObjectURL(blob){const url=URL.createObjectURL(blob);blobs.push({url,blob});return url;}}};
const originalLoad=T.TextureLoader.prototype.load;
T.TextureLoader.prototype.load=function(url){const texture=new T.Texture();texture.userData.source=url;return texture;};
const bytes=await fs.readFile(sourceUrl);
const fbx=new FBXLoader().parse(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength),'');
T.TextureLoader.prototype.load=originalLoad;
const root=new T.Group();root.name='Imported_Player';root.rotation.y=-Math.PI/2;root.scale.setScalar(1.83);root.add(fbx);root.updateMatrixWorld(true);
let mesh;root.traverse(o=>{if(o.isSkinnedMesh)mesh=o;});if(!mesh)throw Error('The supplied FBX has no skinned mesh');
const fileTextures=[];
for(const [slot,name] of [['map','user-player-basecolor.jpg'],['normalMap','user-player-normal.png']]){
 const blob=blobs.find(b=>b.url===mesh.material[slot]?.userData.source)?.blob;
 if(!blob)throw Error('Missing embedded '+slot);
 const raw=Buffer.from(await blob.arrayBuffer());await fs.writeFile(new URL(name,dest),raw);
 fileTextures.push({slot,path:'/assets/players/'+name,sha256:createHash('sha256').update(raw).digest('hex'),bytes:raw.length});
}
mesh.material=new T.MeshStandardMaterial({name:'Imported_Football_Kit',color:'#ffffff',roughness:.82,metalness:0});
mesh.name='User_Footballer';mesh.normalizeSkinWeights();
const zone=[],chest=[];const bindPoint=new T.Vector3();
for(let i=0;i<mesh.geometry.attributes.position.count;i++){
 bindPoint.fromBufferAttribute(mesh.geometry.attributes.position,i).applyMatrix4(mesh.matrixWorld);
 zone.push(bindPoint.y>.70&&bindPoint.y<1.0?1:0);
 if(Math.abs(bindPoint.x)<.15&&bindPoint.y>1.23&&bindPoint.y<1.38)chest.push(bindPoint.z);
}
mesh.geometry.setAttribute('kitZone',new T.Float32BufferAttribute(zone,1));
const jerseyFrontZ=Math.max(...chest)+.005,jerseyBackZ=Math.min(...chest)-.005;

// FBX images use flipped texture Y; express the same convention in glTF UVs.
const uv=mesh.geometry.attributes.uv;for(let i=0;i<uv.count;i++)uv.setY(i,1-uv.getY(i));
const raw=await fs.readFile(new URL('footballer.glb',dest));
const source=await new GLTFLoader().parseAsync(raw.buffer.slice(raw.byteOffset,raw.byteOffset+raw.byteLength),'');
source.scene.updateMatrixWorld(true);
const sourceRest=new Map();source.scene.traverse(b=>{if(b.isBone)sourceRest.set(b.name,{q:b.getWorldQuaternion(new T.Quaternion()),p:b.getWorldPosition(new T.Vector3())});});
const rest=new Map();const bones=[];root.traverse(b=>{if(b.isBone){bones.push(b);rest.set(b.name,{q:b.quaternion.clone(),p:b.position.clone(),worldQ:b.getWorldQuaternion(new T.Quaternion()),worldP:b.getWorldPosition(new T.Vector3())});}});
const mapping={Hip:'pelvis',Waist:'spine_01',Spine01:'spine_02',Spine02:'spine_03',NeckTwist01:'neck_01',Head:'Head',L_Clavicle:'clavicle_l',R_Clavicle:'clavicle_r',L_Upperarm:'upperarm_l',R_Upperarm:'upperarm_r',L_Forearm:'lowerarm_l',R_Forearm:'lowerarm_r',L_Hand:'hand_l',R_Hand:'hand_r',L_Thigh:'thigh_l',R_Thigh:'thigh_r',L_Calf:'calf_l',R_Calf:'calf_r',L_Foot:'foot_l',R_Foot:'foot_r',L_ToeBase:'ball_l',R_ToeBase:'ball_r'};
const valid=Object.entries(mapping).filter(([to,from])=>rest.has(to)&&sourceRest.has(from));
if(valid.length<19)throw Error('Insufficient matching bones: '+valid.length);
const sourceNodes=new Map(valid.map(([,from])=>[from,source.scene.getObjectByName(from)]));
const mixer=new T.AnimationMixer(source.scene),clips=[];
for(const clip of source.animations){
 mixer.stopAllAction();mixer.clipAction(clip).play();
 const count=Math.ceil(clip.duration*30),times=Array.from({length:count+1},(_,i)=>i/count*clip.duration);
 const samples=new Map(valid.map(([name])=>[name,[]])),hipPositions=[];
 for(const time of times){
  mixer.setTime(Math.min(time,clip.duration-1e-6));source.scene.updateMatrixWorld(true);
  for(const b of bones){b.position.copy(rest.get(b.name).p);b.quaternion.copy(rest.get(b.name).q);}root.updateMatrixWorld(true);
  for(const b of bones){const from=mapping[b.name];if(!from||!sourceNodes.has(from))continue;
   const delta=sourceNodes.get(from).getWorldQuaternion(new T.Quaternion()).multiply(sourceRest.get(from).q.clone().invert());
   const desired=delta.multiply(rest.get(b.name).worldQ);
   b.quaternion.copy(b.parent.getWorldQuaternion(new T.Quaternion()).invert().multiply(desired));
   if(b.name==='Hip'){
    const offset=sourceNodes.get(from).getWorldPosition(new T.Vector3()).sub(sourceRest.get(from).p);
    b.position.copy(b.parent.worldToLocal(rest.get(b.name).worldP.clone().add(offset)));hipPositions.push(...b.position.toArray());
   }
   b.updateMatrixWorld(true);samples.get(b.name).push(...b.quaternion.toArray());
  }
  // Proportions differ from the source rig: keep the deformed surface on the pitch.
  root.updateMatrixWorld(true);mesh.skeleton.update();let ground=0;const point=new T.Vector3();
  for(let i=0;i<mesh.geometry.attributes.position.count;i+=3){mesh.getVertexPosition(i,point);mesh.localToWorld(point);ground=Math.min(ground,point.y);}
  if(ground<-.006){const hip=root.getObjectByName('Hip');const world=hip.getWorldPosition(new T.Vector3());world.y-=ground+.006;hip.position.copy(hip.parent.worldToLocal(world));hipPositions.splice(hipPositions.length-3,3,...hip.position.toArray());}
 }
 const tracks=valid.map(([name])=>new T.QuaternionKeyframeTrack(name+'.quaternion',times,samples.get(name)));
 tracks.push(new T.VectorKeyframeTrack('Hip.position',times,hipPositions));
 clips.push(new T.AnimationClip(clip.name,clip.duration,tracks));
}
mixer.stopAllAction();
for(const b of bones){b.position.copy(rest.get(b.name).p);b.quaternion.copy(rest.get(b.name).q);}root.updateMatrixWorld(true);
// Keep the original mesh/skin. Optional keeper gloves are attached to hand bones.
for(const side of ['L','R']){const bone=root.getObjectByName(side+'_Hand');const center=bone.getWorldPosition(new T.Vector3());const geometry=new T.SphereGeometry(1,12,8);geometry.scale(.065,.038,.035);geometry.translate(...center.toArray());geometry.applyMatrix4(bone.matrixWorld.clone().invert());const glove=new T.Mesh(geometry,new T.MeshStandardMaterial({name:'Imported_KeeperGloves',color:'#dce7da',roughness:.75}));glove.name='KeeperGlove_'+(side==='L'?'l':'r');glove.visible=false;bone.add(glove);}
const scene=new T.Scene();scene.add(root);
root.userData={source:'User supplied soccer_player_rigged.fbx',sourceSha256:createHash('sha256').update(bytes).digest('hex'),retargetedClips:clips.length,mappedBones:valid.length,heightMetres:1.83,jerseyFrontZ,jerseyBackZ};
globalThis.FileReader=class{readAsArrayBuffer(blob){blob.arrayBuffer().then(v=>{this.result=v;this.onloadend?.();});}readAsDataURL(blob){blob.arrayBuffer().then(v=>{this.result='data:application/octet-stream;base64,'+Buffer.from(v).toString('base64');this.onloadend?.();});}};
const binary=Buffer.from(await new GLTFExporter().parseAsync(scene,{binary:true,animations:clips,onlyVisible:false}));
const jsonLength=binary.readUInt32LE(12),json=JSON.parse(binary.subarray(20,20+jsonLength).toString('utf8'));
json.images=[{uri:'user-player-basecolor.jpg'},{uri:'user-player-normal.png'}];json.samplers=[{magFilter:9729,minFilter:9987,wrapS:10497,wrapT:10497}];json.textures=[{source:0,sampler:0},{source:1,sampler:0}];
const material=json.materials.find(m=>m.name==='Imported_Football_Kit');material.pbrMetallicRoughness.baseColorTexture={index:0};material.normalTexture={index:1,scale:.65};
let encoded=Buffer.from(JSON.stringify(json));encoded=Buffer.concat([encoded,Buffer.alloc((4-encoded.length%4)%4,32)]);
const restBinary=binary.subarray(20+jsonLength),header=Buffer.alloc(20);header.write('glTF',0);header.writeUInt32LE(2,4);header.writeUInt32LE(20+encoded.length+restBinary.length,8);header.writeUInt32LE(encoded.length,12);header.write('JSON',16);
const output=Buffer.concat([header,encoded,restBinary]);await fs.writeFile(new URL('user-footballer.glb',dest),output);
const report={source:root.userData,bytes:output.length,sha256:createHash('sha256').update(output).digest('hex'),triangles:(mesh.geometry.index?.count??mesh.geometry.attributes.position.count)/3,clips:clips.map(c=>c.name),textures:fileTextures};
await fs.writeFile(new URL('user-player.json',dest),JSON.stringify(report,null,2)+'\n');
const manifestUrl=new URL('../../frontend/public/data/asset-credits.json',import.meta.url);
const manifest=JSON.parse(await fs.readFile(manifestUrl,'utf8'));
const asset=manifest.assets.find(a=>a.path==='/assets/players/user-footballer.glb');
if(asset){asset.sha256=report.sha256;asset.bytes=report.bytes;await fs.writeFile(manifestUrl,JSON.stringify(manifest,null,2)+'\n');}
console.log(JSON.stringify(report,null,2));
