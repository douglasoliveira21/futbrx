import * as T from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { createKeeperGloves } from "./features/match3d/keeperGloves";
const scene = new T.Scene();
scene.background = new T.Color("#14232d");
const camera = new T.PerspectiveCamera(38, 1, 0.005, 20),
  renderer = new T.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 1.6));
renderer.outputColorSpace = T.SRGBColorSpace;
document.body.appendChild(renderer.domElement);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
scene.add(new T.HemisphereLight("#ffffff", "#697787", 3));
const light = new T.DirectionalLight("#ffffff", 3);
light.position.set(1, 4, 3);
scene.add(light);
const fill = new T.DirectionalLight("#b4d2ff", 2);
fill.position.set(-2, -2, -3);
scene.add(fill);
const gltf = await new GLTFLoader().loadAsync(
  "/assets/players/user-footballer-mocap.glb",
);
scene.add(gltf.scene);
gltf.scene.updateMatrixWorld(true);
const old = ["l", "r"].map((s) =>
  gltf.scene.getObjectByName("KeeperGlove_" + s)!,
);
old.forEach((o, i) => {
  o.name = "OldGlove_" + i;
  o.visible = false;
});
const gloves = createKeeperGloves(gltf.scene);
gloves.forEach((g) => (g.visible = true));
const mixer = new T.AnimationMixer(gltf.scene),
  rest = new Map<T.Object3D, { p: T.Vector3; q: T.Quaternion }>();
gltf.scene.traverse((o) => {
  if (o instanceof T.Bone)
    rest.set(o, { p: o.position.clone(), q: o.quaternion.clone() });
});
let side = "L",
  palm = false,
  playing = true,
  follow = true;
function frame() {
  const wrist = gltf.scene
      .getObjectByName(side + "_Hand")!
      .getWorldPosition(new T.Vector3()),
    forearm = gltf.scene
      .getObjectByName(side + "_Forearm")!
      .getWorldPosition(new T.Vector3());
  const axis = wrist.clone().sub(forearm).normalize();
  const target = wrist.clone().addScaledVector(axis, 0.065);
  camera.position
    .copy(target)
    .add(new T.Vector3(0.08, palm ? -0.32 : 0.32, 0.23));
  controls.target.copy(target);
  controls.update();
}
(document.querySelector("#left") as HTMLButtonElement).onclick = () => {
  side = "L";
  follow = true;
  frame();
};
(document.querySelector("#right") as HTMLButtonElement).onclick = () => {
  side = "R";
  follow = true;
  frame();
};
(document.querySelector("#back") as HTMLButtonElement).onclick = () => {
  palm = false;
  follow = true;
  frame();
};
(document.querySelector("#palm") as HTMLButtonElement).onclick = () => {
  palm = true;
  follow = true;
  frame();
};
(document.querySelector("#old") as HTMLInputElement).onchange = (e) => {
  const previous = (e.target as HTMLInputElement).checked;
  old.forEach((g) => (g.visible = previous));
  gloves.forEach((g) => (g.visible = !previous));
};
(document.querySelector("#action") as HTMLSelectElement).onchange = (e) => {
  mixer.stopAllAction();
  for (const [b, r] of rest) {
    b.position.copy(r.p);
    b.quaternion.copy(r.q);
  }
  const clip = gltf.animations.find(
    (c) => c.name === (e.target as HTMLSelectElement).value,
  );
  if (clip) mixer.clipAction(clip).play();
  follow = true;
};
(document.querySelector("#pause") as HTMLButtonElement).onclick = (e) => {
  playing = !playing;
  (e.target as HTMLButtonElement).textContent = playing
    ? "Pausar"
    : "Reproduzir";
};
controls.addEventListener("start", () => {
  follow = false;
});
function resize() {
  const height = innerHeight - 135;
  renderer.setSize(innerWidth, height);
  camera.aspect = innerWidth / height;
  camera.updateProjectionMatrix();
}
addEventListener("resize", resize);
resize();
frame();
let last = performance.now();
renderer.setAnimationLoop((now) => {
  if (playing) mixer.update(Math.min(0.04, (now - last) / 1000));
  last = now;
  gltf.scene.updateMatrixWorld(true);
  if (follow) frame();
  else controls.update();
  renderer.render(scene, camera);
});
