import * as T from '../../frontend/node_modules/three/build/three.module.js';
import {mergeGeometries} from '../../frontend/node_modules/three/examples/jsm/utils/BufferGeometryUtils.js';
// Original human football artwork. Coordinates match the licensed humanoid rig's bind pose.
export function createHumanFootballer(mesh){
 const bones=mesh.skeleton.bones;const ids=Object.fromEntries(bones.map((b,i)=>[b.name,i]));
 const out={p:[],i:[],c:[],m:[],j:[],w:[]};
 const rgb={skin:'#ffffff',kit:'#ffffff',white:'#eef2ed',boots:'#15202e',hair:'#211a17',eye:'#f8f6ec',iris:'#3d3021',pupil:'#0b1014',lip:'#925d56',line:'#dce4dd',sole:'#567a67'};
 const weight=(a,b=null,t=0)=>({j:[ids[a],ids[b??a],0,0],w:[1-t,t,0,0]});
 function add(geo,region,weights){const offset=out.p.length/3,p=geo.attributes.position,index=geo.index;for(let k=0;k<p.count;k++){const x=p.getX(k),y=p.getY(k),z=p.getZ(k);out.p.push(x,y,z);const r=typeof region==='function'?region(x,y,z):region;const color=new T.Color(rgb[r]??r);out.c.push(color.r,color.g,color.b);out.m.push(r==='kit'?1:0,r==='skin'?1:0);const skin=typeof weights==='function'?weights(x,y,z):weights;out.j.push(...skin.j);out.w.push(...skin.w);}if(index)for(let k=0;k<index.count;k++)out.i.push(offset+index.getX(k));else for(let k=0;k<p.count;k++)out.i.push(offset+k);}
 function loft(sections,axis='y',segments=18){const pos=[],idx=[];sections.forEach(([a,b,c,r1,r2],row)=>{for(let i=0;i<=segments;i++){const angle=i/segments*Math.PI*2;pos.push(...(axis==='y'?[a+Math.cos(angle)*r1,b,c+Math.sin(angle)*r2]:[a,b+Math.cos(angle)*r1,c+Math.sin(angle)*r2]));if(row&&i<segments){const n=row*(segments+1)+i,prev=n-segments-1;idx.push(prev,n,prev+1,n,n+1,prev+1);}}});const flip=axis==='y'?sections.at(-1)[1]<sections[0][1]:sections.at(-1)[0]>sections[0][0];if(flip)for(let i=0;i<idx.length;i+=3)[idx[i+1],idx[i+2]]=[idx[i+2],idx[i+1]];const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(pos,3));g.setIndex(idx);return g;}
 function ellipsoid(center,scale,region,weights,segments=14,rings=10){const g=new T.SphereGeometry(1,segments,rings);g.scale(...scale);g.translate(...center);add(g,region,weights);}
 const spine=(x,y)=>{const levels=[['pelvis',.98],['spine_01',1.10],['spine_02',1.24],['spine_03',1.43],['neck_01',1.53],['Head',1.58]];if(y<levels[0][1])return weight('pelvis');for(let i=1;i<levels.length;i++)if(y<=levels[i][1])return weight(levels[i-1][0],levels[i][0],(y-levels[i-1][1])/(levels[i][1]-levels[i-1][1]));return weight('Head');};
 add(loft([[0,.90,-.015,.105,.075],[0,.99,-.01,.16,.095],[0,1.10,0,.145,.09],[0,1.25,0,.18,.105],[0,1.38,-.005,.205,.105],[0,1.445,-.015,.205,.085],[0,1.475,-.005,.064,.048]],'y',24),(x,y)=>y<1.015?'boots':'kit',spine);
 // Shoulder seams, fabric folds and neckline are skinned with the torso.
 for(const side of [-1,1]){
  for(let f=0;f<4;f++)ellipsoid([side*(.10+f*.018),1.075+f*.055,.091],[.026,.003,.003],'line',spine(side*.12,1.1));
  ellipsoid([side*.115,1.452,.046],[.052,.004,.006],'line',weight('spine_03'));
 }
 // Collar, neck, jaw, cranium and individually modelled facial features.
 add(loft([[0,1.46,-.003,.063,.049],[0,1.49,-.003,.060,.046]],'y',24),'white',weight('neck_01'));
 add(loft([[0,1.47,0,.046,.043],[0,1.58,.008,.049,.048]],'y',18),'skin',weight('neck_01','Head',.45));
 add(loft([[0,1.575,.025,.035,.033],[0,1.605,.015,.069,.060],[0,1.65,.004,.085,.079],[0,1.71,0,.091,.087],[0,1.765,-.004,.086,.085],[0,1.808,-.006,.059,.063],[0,1.83,-.005,.002,.002]],'y',28),'skin',weight('Head'));
 for(const side of [-1,1]){
  ellipsoid([side*.09,1.695,.003],[.014,.025,.013],'skin',weight('Head'));
  ellipsoid([side*.094,1.694,.012],[.006,.014,.004],'lip',weight('Head'),10,8);
  ellipsoid([side*.035,1.709,.080],[.018,.009,.008],'eye',weight('Head'));
  ellipsoid([side*.035,1.709,.087],[.007,.0075,.003],'iris',weight('Head'),12,8);
  ellipsoid([side*.035,1.709,.090],[.0033,.0045,.0015],'pupil',weight('Head'),10,8);
  ellipsoid([side*.035,1.727,.079],[.022,.0038,.004],'hair',weight('Head'),12,6);
 }
 ellipsoid([0,1.679,.088],[.012,.025,.020],'skin',weight('Head'));
 ellipsoid([0,1.665,.101],[.014,.009,.012],'skin',weight('Head'));
 ellipsoid([0,1.635,.089],[.026,.0036,.0045],'lip',weight('Head'),18,6);
 ellipsoid([0,1.630,.088],[.021,.0022,.004],'lip',weight('Head'),16,6);
 for(const side of [-1,1]){
  const name=side===1?'l':'r',x=side*.089;
  const leg=(xx,y)=>y>.59?weight('thigh_'+name):y>.48?weight('thigh_'+name,'calf_'+name,(.59-y)/.11):y>.15?weight('calf_'+name):weight('calf_'+name,'foot_'+name,Math.max(0,Math.min(1,(.15-y)/.08)));
  add(loft([[x,.93,-.005,.084,.078],[x,.80,.0,.081,.079],[x,.64,0,.060,.061],[x,.54,0,.052,.053],[x,.45,0,.046,.046],[x,.35,-.003,.051,.048],[x,.18,-.016,.033,.033],[x,.095,-.029,.030,.031]],'y',18),(xx,y)=>y>.76?'boots':y>.435?'skin':'white',leg);
  const arm=(xx)=>{const t=Math.abs(xx);if(t<.22)return weight('spine_03','upperarm_'+name,Math.max(0,(t-.16)/.06));if(t<.43)return weight('upperarm_'+name);if(t<.49)return weight('upperarm_'+name,'lowerarm_'+name,(t-.43)/.06);if(t<.70)return weight('lowerarm_'+name);return weight('lowerarm_'+name,'hand_'+name,Math.min(1,(t-.70)/.05));};
  add(loft([[side*.17,1.44,-.065,.066,.064],[side*.27,1.441,-.065,.062,.059],[side*.35,1.441,-.067,.054,.050],[side*.46,1.441,-.070,.044,.043],[side*.59,1.441,-.067,.042,.040],[side*.735,1.441,-.065,.027,.025]],'x',18),(xx)=>Math.abs(xx)<.35?'kit':'skin',arm);
  ellipsoid([side*.775,1.44,-.065],[.043,.030,.018],'skin',weight('hand_'+name));
  for(let finger=0;finger<4;finger++)ellipsoid([side*(.824+(finger===1?.006:0)),1.462-finger*.013,-.065],[.031,.0057,.008],'skin',weight('hand_'+name),10,8);
  ellipsoid([side*.25,1.445,-.11],[.075,.044,.026],'kit',weight('upperarm_'+name));
  ellipsoid([side*.77,1.417,-.042],[.028,.017,.014],'skin',weight('hand_'+name),10,8);
  ellipsoid([x,.064,.044],[.054,.055,.133],'boots',weight('foot_'+name),18,12);
  for(let i=0;i<4;i++){const lace=new T.BoxGeometry(.058,.0025,.004);lace.translate(x,.106-i*.003,.025+i*.019);add(lace,'white',weight('foot_'+name));}
  for(const sx of [-1,1])for(const z of [-.035,.035,.115]){const stud=new T.CylinderGeometry(.008,.007,.012,7);stud.translate(x+sx*.033,.009,z);add(stud,'sole',weight('foot_'+name));}
 }
 const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(out.p,3));geometry.setAttribute('color',new T.Float32BufferAttribute(out.c,3));geometry.setAttribute('uv1',new T.Float32BufferAttribute(out.m,2));geometry.setAttribute('skinIndex',new T.Uint16BufferAttribute(out.j,4));geometry.setAttribute('skinWeight',new T.Float32BufferAttribute(out.w,4));geometry.setIndex(out.i);geometry.computeVertexNormals();geometry.computeBoundingBox();
 // Attach rigid hair and goalkeeper gloves to the same real bones, without extra skeletons.
 const head=bones[ids.Head];const inverseHead=new T.Matrix4().copy(head.matrixWorld).invert();
 const hairMaterial=new T.MeshStandardMaterial({name:'Hair',color:'#211a17',roughness:.95});
 const makeHair=(name,parts)=>{const group=new T.Group();group.name=name;const merged=mergeGeometries(parts.map(g=>{g.deleteAttribute("uv");return g.applyMatrix4(inverseHead);}));merged.normalizeNormals();group.add(new T.Mesh(merged,hairMaterial));head.add(group);return group;};
 const cap=loft([[0,1.752,-.006,.089,.087],[0,1.79,-.007,.078,.08],[0,1.829,-.008,.042,.048],[0,1.843,-.008,.002,.002]],'y',22);cap.computeVertexNormals();makeHair('Hair_Crop',[cap.clone()]);
 const sweep=new T.SphereGeometry(1,18,12);sweep.scale(.085,.034,.08);sweep.translate(.008,1.823,.006);makeHair('Hair_Sweep',[cap.clone(),sweep]);
 const curls=[];for(let i=0;i<19;i++){const a=i/18*Math.PI*2,r=i===18?0:.068;const curl=new T.SphereGeometry(.021,9,7);curl.translate(Math.cos(a)*r,1.813+(i%3)*.008,Math.sin(a)*r-.006);curls.push(curl);}makeHair('Hair_Curls',[cap.clone(),...curls]);
 for(const side of ['l','r']){const hand=bones[ids['hand_'+side]],sign=side==='l'?1:-1;const g=new T.SphereGeometry(1,14,10);g.scale(.066,.036,.024);g.translate(sign*.79,1.441,-.065);g.applyMatrix4(new T.Matrix4().copy(hand.matrixWorld).invert());const glove=new T.Mesh(g,new T.MeshStandardMaterial({name:'Gloves',color:'#e9eddb',roughness:.72}));glove.name='KeeperGlove_'+side;hand.add(glove);}
 return {geometry,triangles:out.i.length/3};
}
