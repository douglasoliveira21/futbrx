import { useEffect, useMemo, useRef } from "react";
import { useFrame } from "@react-three/fiber";
import * as THREE from "three";
import type { Quality } from "./presentation";
import {
  bowlPoint,
  bowlSeats,
  stadiumMoment,
  type StadiumMoment,
} from "./stadiumDynamics";
import { framePair } from "./playback";
import type { MatchReplay } from "../../services/api";

type Timeline = {
  minute: number;
  playing: boolean;
  visible: boolean;
  entrance?: number;
};
export type Atmosphere = React.RefObject<StadiumMoment>;

function ringGeometry(rows: number, roof = false) {
  const positions: number[] = [],
    colors: number[] = [];
  const segments = 160;
  const vertex = (x: number, y: number, z: number, c: THREE.Color) => {
    positions.push(x, y, z);
    colors.push(c.r, c.g, c.b);
  };
  const quad = (
    a: THREE.Vector3,
    b: THREE.Vector3,
    c: THREE.Vector3,
    d: THREE.Vector3,
    color: THREE.Color,
  ) => {
    for (const p of [a, b, c, a, c, d]) vertex(p.x, p.y, p.z, color);
  };
  for (let row = 0; row < rows; row++)
    for (let i = 0; i < segments; i++) {
      const angle = (i / segments) * Math.PI * 2,
        next = ((i + 1) / segments) * Math.PI * 2;
      const p = bowlPoint(angle, roof ? 8 : row),
        q = bowlPoint(next, roof ? 8 : row),
        r = bowlPoint(next, roof ? 19 : row + 1),
        s = bowlPoint(angle, roof ? 19 : row + 1);
      if (!roof && row < 7 && Math.abs(p.x) < 4 && p.z > 0) continue;
      const y = roof ? 17.8 : p.y,
        outerY = roof ? 19.6 : p.y;
      quad(
        new THREE.Vector3(p.x, y, p.z),
        new THREE.Vector3(q.x, y, q.z),
        new THREE.Vector3(r.x, outerY, r.z),
        new THREE.Vector3(s.x, outerY, s.z),
        new THREE.Color(roof ? "#a9b8c3" : row % 2 ? "#344252" : "#2a3747"),
      );
      if (!roof)
        quad(
          new THREE.Vector3(p.x, p.y - 0.68, p.z),
          new THREE.Vector3(q.x, p.y - 0.68, q.z),
          new THREE.Vector3(q.x, p.y, q.z),
          new THREE.Vector3(p.x, p.y, p.z),
          new THREE.Color("#1b2839"),
        );
    }
  const g = new THREE.BufferGeometry();
  g.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
  g.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
  g.computeVertexNormals();
  return g;
}

function Architecture() {
  const floor = useMemo(() => ringGeometry(16), []),
    roof = useMemo(() => ringGeometry(1, true), []);
  const steel = useRef<THREE.InstancedMesh>(null);
  useEffect(() => {
    const mesh = steel.current;
    if (!mesh) return;
    const obj = new THREE.Object3D(),
      up = new THREE.Vector3(0, 1, 0);
    let index = 0;
    const bar = (a: THREE.Vector3, b: THREE.Vector3, r: number) => {
      const d = b.clone().sub(a);
      obj.position.copy(a).add(b).multiplyScalar(0.5);
      obj.quaternion.setFromUnitVectors(up, d.clone().normalize());
      obj.scale.set(r, d.length(), r);
      obj.updateMatrix();
      mesh.setMatrixAt(index++, obj.matrix);
    };
    for (let i = 0; i < 32; i++) {
      const a = (i / 32) * Math.PI * 2,
        p = bowlPoint(a, 8),
        q = bowlPoint(a, 19);
      bar(
        new THREE.Vector3(p.x, 17.6, p.z),
        new THREE.Vector3(q.x, 19.4, q.z),
        0.09,
      );
      bar(
        new THREE.Vector3(q.x, 1, q.z),
        new THREE.Vector3(q.x, 19.4, q.z),
        0.14,
      );
      bar(
        new THREE.Vector3(p.x, 17.6, p.z),
        new THREE.Vector3(q.x, 16, q.z),
        0.065,
      );
    }
    mesh.instanceMatrix.needsUpdate = true;
  }, []);
  useEffect(
    () => () => {
      floor.dispose();
      roof.dispose();
    },
    [floor, roof],
  );
  return (
    <>
      <mesh geometry={floor} receiveShadow>
        <meshStandardMaterial
          vertexColors
          roughness={0.85}
          side={THREE.DoubleSide}
        />
      </mesh>
      <mesh geometry={roof} receiveShadow>
        <meshStandardMaterial
          vertexColors
          metalness={0.2}
          roughness={0.75}
          side={THREE.DoubleSide}
        />
      </mesh>
      <instancedMesh
        frustumCulled={false}
        ref={steel}
        args={[undefined, undefined, 96]}
      >
        <cylinderGeometry args={[1, 1, 1, 6]} />
        <meshStandardMaterial
          color="#647786"
          metalness={0.6}
          roughness={0.45}
        />
      </instancedMesh>
      {[-1, 1].map((side) => (
        <group key={side}>
          <mesh position={[0, 1, side * 37]}>
            <boxGeometry args={[111, 1.05, 0.18]} />
            <meshStandardMaterial
              color="#0d2939"
              emissive="#0d3f38"
              emissiveIntensity={0.6}
            />
          </mesh>
          <mesh position={[side * 56.5, 1, 0]}>
            <boxGeometry args={[0.18, 1.05, 74]} />
            <meshStandardMaterial
              color="#133245"
              emissive="#173e5c"
              emissiveIntensity={0.45}
            />
          </mesh>
          {[-45, -15, 15, 45].map((x) => (
            <mesh
              key={x}
              position={[x, 17.6, side * 48]}
              rotation={[side * 0.65, 0, 0]}
            >
              <boxGeometry args={[8, 0.15, 1.1]} />
              <meshBasicMaterial color="#f2f6ff" />
            </mesh>
          ))}
        </group>
      ))}
      <group position={[0, 0, 43]}>
        <mesh position={[-3.6, 1.8, 0]}>
          <boxGeometry args={[0.4, 3.6, 9]} />
          <meshStandardMaterial color="#1b293a" />
        </mesh>
        <mesh position={[3.6, 1.8, 0]}>
          <boxGeometry args={[0.4, 3.6, 9]} />
          <meshStandardMaterial color="#1b293a" />
        </mesh>
        <mesh position={[0, 3.6, 0]}>
          <boxGeometry args={[7.6, 0.4, 9]} />
          <meshStandardMaterial color="#263c50" />
        </mesh>
        <mesh position={[0, 1.8, 4.3]}>
          <planeGeometry args={[7, 3.6]} />
          <meshBasicMaterial color="#020509" />
        </mesh>
        <mesh position={[0, 3.35, -4.55]}>
          <boxGeometry args={[7, 0.09, 0.12]} />
          <meshBasicMaterial color="#d2fa69" />
        </mesh>
      </group>
    </>
  );
}

// Crossed planes retain the silhouette from both axes at grandstand distance.
// They are used only in the light profile; close-detail profiles keep volumes.
function crowdLimb(width: number, height: number) {
  const w=width/2, h=height/2;
  const geometry=new THREE.BufferGeometry();
  geometry.setAttribute("position",new THREE.Float32BufferAttribute([
    -w,-h,0, w,-h,0, w,h,0, -w,-h,0, w,h,0, -w,h,0,
    0,-h,-w, 0,h,w, 0,-h,w, 0,-h,-w, 0,h,-w, 0,h,w,
  ],3));
  geometry.computeVertexNormals();
  return geometry;
}
function Audience({
  quality,
  home,
  away,
  mood,
  timeline,
  rate,
}: {
  quality: Quality;
  home: string;
  away: string;
  mood: Atmosphere;
  timeline: React.RefObject<Timeline>;
  rate: number;
}) {
  const seats = useMemo(
    () =>
      bowlSeats(10400),
    [quality],
  );
  const people = useMemo(
    () => seats,
    [seats],
  );
  const chairs = useRef<THREE.InstancedMesh>(null),
    bodies = useRef<THREE.InstancedMesh>(null),
    heads = useRef<THREE.InstancedMesh>(null),
    arms = useRef<THREE.InstancedMesh>(null),
    legs = useRef<THREE.InstancedMesh>(null);
  const object = useMemo(() => new THREE.Object3D(), []);
  const lightGeometry=useMemo(()=>({arms:crowdLimb(.11,.45),legs:crowdLimb(.14,.46),seats:new THREE.PlaneGeometry(1,1).rotateX(-Math.PI/2)}),[]);
  useEffect(()=>()=>Object.values(lightGeometry).forEach(g=>g.dispose()),[lightGeometry]);
  useEffect(() => {
    const color = new THREE.Color();
    seats.forEach((p, i) => {
      object.position.set(p.x, p.y + 0.14, p.z);
      object.rotation.set(0, p.yaw, 0);
      object.scale.set(0.52, 0.28, 0.48);
      object.updateMatrix();
      chairs.current?.setMatrixAt(i, object.matrix);
      color.set(Math.floor(i / 40) % 3 === 0 ? home : "#30465e");
      chairs.current?.setColorAt(i, color);
    });
    if (chairs.current) chairs.current.instanceMatrix.needsUpdate = true;
    people.forEach((_, i) => {
      const p = people[i];
      const awaySector = p.x > 30 && p.z < -42;
      color.set(i % 17 === 0 ? ["#d5d8df", "#1c3045"][i % 2] : awaySector ? away : home);
      color.multiplyScalar(.6 + ((i * 13) % 9) * .055);
      bodies.current?.setColorAt(i, color);
      arms.current?.setColorAt(i * 2, color);
      arms.current?.setColorAt(i * 2 + 1, color);
      heads.current?.setColorAt(
        i,
        new THREE.Color(["#d3a27b", "#9b694b", "#62422e", "#e2b993"][i % 4]),
      );
    });
  }, [seats, people, home, away, object]);
  const cpuCrowdBaseline = import.meta.env.DEV && new URLSearchParams(location.search).get("crowdCpu") === "1";
  const crowdUniforms = useMemo(() => ({
    crowdTime: { value: 0 }, crowdEnergy: { value: 0 }, crowdGoal: { value: 0 },
  }), []);
  useEffect(() => {
    if (cpuCrowdBaseline) return;
    for (const mesh of [bodies.current, heads.current, arms.current, legs.current]) {
      if (!mesh) continue;
      const material = mesh.material as THREE.MeshStandardMaterial;
      material.onBeforeCompile = shader => {
        Object.assign(shader.uniforms, crowdUniforms);
        shader.vertexShader = "uniform float crowdTime; uniform float crowdEnergy; uniform float crowdGoal;\n" + shader.vertexShader;
        const armMotion = mesh === arms.current ? "transformed.x += sin(crowdTime * 3.0 + instanceMatrix[3].x) * max(0.0, position.y) * crowdEnergy * 0.8;" : "";
        shader.vertexShader = shader.vertexShader.replace("#include <begin_vertex>",
          "#include <begin_vertex>\nfloat phase = instanceMatrix[3].x * 1.7 + instanceMatrix[3].z;\ntransformed.y += max(0.0, sin(crowdTime * 8.0 + phase)) * crowdGoal * 0.23 + sin(crowdTime * 3.0 + phase) * crowdEnergy * 0.025;" + armMotion);
      };
      material.customProgramCacheKey = () => mesh === arms.current ? "futbrx-crowd-arms-v2" : "futbrx-crowd-motion-v1";
      material.needsUpdate = true;
    }
  }, [crowdUniforms, cpuCrowdBaseline]);
  const phase = useRef(0), lastUpdate = useRef(-1), lastMood = useRef("");
  useFrame((_, delta) => {
    if (timeline.current.playing && timeline.current.visible)
      phase.current += Math.min(delta, 0.1);
    const t = phase.current;
    crowdUniforms.crowdTime.value = t;
    crowdUniforms.crowdEnergy.value = mood.current.energy;
    crowdUniforms.crowdGoal.value = mood.current.reaction === "goal" ? 1 : 0;
    // Instance transforms are static. Crowd motion is evaluated on the GPU.
    if (!cpuCrowdBaseline && lastUpdate.current >= 0) return;
    const moodKey = `${mood.current.reaction}/${Math.round(mood.current.energy * 10)}`;
    if (lastMood.current === moodKey && t - lastUpdate.current < (quality === "low" ? 1 / 12 : 1 / 24)) return;
    lastUpdate.current = t; lastMood.current = moodKey;
    const excitement = mood.current.energy,
      goal = mood.current.reaction === "goal";
    people.forEach((p, i) => {
      const responsive = (i % 5) / 5 < excitement;
      const jump = goal
        ? Math.max(0, Math.sin(t * 8 + i * 1.7)) * 0.23
        : responsive
          ? Math.sin(t * 3 + i) * 0.025
          : 0;
      const stand = goal ? 0.35 : responsive && excitement > 0.55 ? 0.22 : 0;
      const y = p.y + 0.55 + stand + jump;
      object.rotation.set(0, p.yaw, 0);
      object.position.set(p.x, y, p.z);
      object.scale.set(.72 + (i % 5) * .05, .85 + (i % 7) * .035, .8);
      object.updateMatrix();
      bodies.current?.setMatrixAt(i, object.matrix);
      object.position.y = y + 0.48;
      object.scale.set(1, 1, 1);
      object.updateMatrix();
      heads.current?.setMatrixAt(i, object.matrix);
      for (let side = -1; side <= 1; side += 2) {
        const raised = goal ? 1 : responsive ? 0.35 + excitement * 0.45 : 0.05;
        const localX = side * (0.26 + raised * 0.12),
          localZ = 0;
        object.position.set(
          p.x + Math.cos(p.yaw) * localX + Math.sin(p.yaw) * localZ,
          y + 0.06 + raised * 0.37,
          p.z - Math.sin(p.yaw) * localX,
        );
        object.rotation.set(
          0,
          p.yaw,
          side * (raised * 2.5 + 0.1) * Math.sin(t * 2 + i * 0.1) * 0.2 +
            side * raised * 2.2,
        );
        object.scale.set(1, 1, 1);
        object.updateMatrix();
        arms.current?.setMatrixAt(i * 2 + (side === -1 ? 0 : 1), object.matrix);
        object.position.set(
          p.x + Math.cos(p.yaw) * side * 0.12,
          p.y + 0.19 + stand + jump,
          p.z - Math.sin(p.yaw) * side * 0.12,
        );
        object.rotation.set(0.12, p.yaw, 0);
        object.scale.set(1, 1, 1);
        object.updateMatrix();
        legs.current?.setMatrixAt(i * 2 + (side === -1 ? 0 : 1), object.matrix);
      }
    });
    for (const mesh of [
      bodies.current,
      heads.current,
      arms.current,
      legs.current,
    ])
      if (mesh) mesh.instanceMatrix.needsUpdate = true;
  });
  return (
    <>
      <instancedMesh
        frustumCulled={false}
        ref={chairs}
        args={[undefined, undefined, seats.length]}
      >
        {quality === "low" ? <primitive object={lightGeometry.seats} attach="geometry" /> : <boxGeometry args={[1, 1, 1]} />}
        <meshStandardMaterial roughness={0.65} />
      </instancedMesh>
      <instancedMesh
        frustumCulled={false}
        ref={bodies}
        args={[undefined, undefined, people.length]}
      >
        {quality === "high" ? <capsuleGeometry args={[0.19, 0.26, 2, 5]} /> : <cylinderGeometry args={[.17,.21,.64,quality === "low" ? 4 : 6,1,quality === "low"]} />}
        <meshLambertMaterial />
      </instancedMesh>
      <instancedMesh
        frustumCulled={false}
        ref={heads}
        args={[undefined, undefined, people.length]}
      >
        {quality === "high" ? <sphereGeometry args={[0.135, 6, 4]} /> : <octahedronGeometry args={[.15,0]} />}
        <meshLambertMaterial />
      </instancedMesh>
      <instancedMesh
        frustumCulled={false}
        ref={arms}
        args={[undefined, undefined, people.length * 2]}
      >
        {quality === "high" ? <capsuleGeometry args={[0.055, 0.34, 2, 4]} /> : quality === "low" ? <primitive object={lightGeometry.arms} attach="geometry" /> : <boxGeometry args={[.11,.45,.11]} />}
        <meshLambertMaterial side={quality === "low" ? THREE.DoubleSide : THREE.FrontSide} />
      </instancedMesh>
      <instancedMesh
        frustumCulled={false}
        ref={legs}
        args={[undefined, undefined, people.length * 2]}
      >
        {quality === "high" ? <capsuleGeometry args={[0.07, 0.32, 2, 4]} /> : quality === "low" ? <primitive object={lightGeometry.legs} attach="geometry" /> : <boxGeometry args={[.14,.46,.14]} />}
        <meshLambertMaterial color="#1b2638" side={quality === "low" ? THREE.DoubleSide : THREE.FrontSide} />
      </instancedMesh>
    </>
  );
}

export function WavingFlag({
  position,
  color,
  accent = "#e3eced",
  width = 2,
  height = 1.3,
  pole = 4,
  phase = 0,
  timeline,
}: {
  position: [number, number, number];
  color: string;
  accent?: string;
  width?: number;
  height?: number;
  pole?: number;
  phase?: number;
  timeline: React.RefObject<Timeline>;
}) {
  const geometry = useMemo(() => {
    const g = new THREE.PlaneGeometry(width, height, 12, 6);
    g.translate(width / 2, 0, 0);
    const p = g.getAttribute("position"),
      rgb = [];
    for (let i = 0; i < p.count; i++) {
      const x = p.getX(i) / width,
        c = new THREE.Color(x > 0.38 && x < 0.6 ? accent : color);
      rgb.push(c.r, c.g, c.b);
    }
    g.setAttribute("color", new THREE.Float32BufferAttribute(rgb, 3));
    return g;
  }, [width, height, color, accent]);
  const time = useRef(phase);
  useEffect(() => () => geometry.dispose(), [geometry]);
  useFrame((_, delta) => {
    if (timeline.current.playing && timeline.current.visible)
      time.current += Math.min(delta, 0.1);
    const p = geometry.getAttribute("position");
    for (let i = 0; i < p.count; i++) {
      const x = p.getX(i);
      p.setZ(
        i,
        (Math.sin(x * 4 - time.current * 3 + phase) * 0.14 * x) / width +
          (Math.sin(x * 7 - time.current * 5) * 0.045 * x) / width,
      );
    }
    p.needsUpdate = true;
    geometry.computeVertexNormals();
  });
  return (
    <group position={position}>
      <mesh position={[0, pole / 2, 0]}>
        <cylinderGeometry args={[0.025, 0.03, pole, 6]} />
        <meshStandardMaterial color="#c8d3dc" metalness={0.6} />
      </mesh>
      <group position={[0, pole - height * 0.55, 0]}>
        <mesh geometry={geometry}>
          <meshStandardMaterial
            vertexColors
            side={THREE.DoubleSide}
            roughness={0.9}
          />
        </mesh>
      </group>
    </group>
  );
}

export default function Arena({
  quality,
  home,
  away,
  timeline,
  replay,
  mood,
  rate,
}: {
  quality: Quality;
  home: string;
  away: string;
  timeline: React.RefObject<Timeline>;
  replay: React.RefObject<MatchReplay | undefined>;
  mood: Atmosphere;
  rate: number;
}) {
  useFrame(() => {
    const second = Math.max(0, timeline.current.minute * 60),
      pair = replay.current && framePair(replay.current.frames, second);
    if (pair)
      mood.current = stadiumMoment(
        second,
        pair.a.ball,
        replay.current!.events,
        rate,
        replay.current!.setup.home.id,
      );
  });
  return (
    <>
      <mesh
        rotation={[-Math.PI / 2, 0, 0]}
        position={[0, -0.04, 0]}
        receiveShadow
      >
        <planeGeometry args={[184, 146]} />
        <meshStandardMaterial color="#17212b" roughness={1} />
      </mesh>
      <Architecture />
      <Audience
        quality={quality}
        home={home}
        away={away}
        mood={mood}
        timeline={timeline}
        rate={rate}
      />
      {[-1, 1].flatMap((side) =>
        [-1, 1].map((end) => (
          <WavingFlag
            key={`${side}-${end}`}
            position={[side * 52.5, 0, end * 34]}
            color="#f4d141"
            width={0.4}
            height={0.32}
            pole={1.5}
            phase={side + end}
            timeline={timeline}
          />
        )),
      )}
      {Array.from({ length: 12 }, (_, i) => {
        const a = ((i + 0.35) / 12) * Math.PI * 2,
          p = bowlPoint(a, 7);
        return (
          <WavingFlag
            key={i}
            position={[p.x, p.y + 0.4, p.z]}
            color={i % 4 === 0 ? away : home}
            width={2.4}
            height={1.45}
            pole={3.4}
            phase={i}
            timeline={timeline}
          />
        );
      })}
    </>
  );
}
