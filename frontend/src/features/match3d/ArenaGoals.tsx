import { useEffect, useMemo, useRef } from "react";
import { useFrame } from "@react-three/fiber";
import * as THREE from "three";
import type { Atmosphere } from "./Arena";

function Goal({ side, mood }: { side: number; mood: Atmosphere }) {
  const wasActive=useRef(false);
  const net = useMemo(() => {
    const p: number[] = [];
    const front = side * 52.5,
      back = side * 54.7,
      topBack = side * 54.05;
    const line = (
      ax: number,
      ay: number,
      az: number,
      bx: number,
      by: number,
      bz: number,
    ) => p.push(ax, ay, az, bx, by, bz);
    for (let y = 0.04; y <= 2.44; y += 0.14) {
      const x = back + ((topBack - back) * y) / 2.44;
      line(x, y, -3.66, x, y, 3.66);
      for (const z of [-3.66, 3.66]) line(front, y, z, x, y, z);
    }
    for (let z = -3.66; z <= 3.66; z += 0.14) {
      line(back, 0.04, z, topBack, 2.44, z);
      line(front, 2.44, z, topBack, 2.44, z);
    }
    for (let d = 0.14; d <= 1.5; d += 0.14)
      line(front + side * d, 2.44, -3.66, front + side * d, 2.44, 3.66);
    for (let d = 0.14; d <= 2.2; d += 0.14)
      for (const z of [-3.66, 3.66])
        line(
          front + side * d,
          0.04,
          z,
          front + side * Math.min(d, 1.55),
          2.44,
          z,
        );
    const g = new THREE.BufferGeometry();
    g.setAttribute("position", new THREE.Float32BufferAttribute(p, 3));
    return { geometry: g, original: new Float32Array(p) };
  }, [side]);
  useEffect(() => () => net.geometry.dispose(), [net]);
  useFrame(() => {
    const moment = mood.current,
      active =
        moment.reaction === "goal" &&
        moment.goalSide === side &&
        moment.age < 3;
    const pos = net.geometry.getAttribute("position");
    // Static nets do not need a buffer upload every render frame. Restore once
    // after the wave ends, then leave the geometry untouched until the next goal.
    if(!active && !wasActive.current) return;
    wasActive.current=active;
    for (let i = 0; i < pos.count; i++) {
      const x = net.original[i * 3],
        y = net.original[i * 3 + 1],
        z = net.original[i * 3 + 2];
      const distance = Math.abs(x) - 52.5;
      const wave = active
        ? Math.sin(moment.age * 24 - z * 1.8 - y * 2) *
          Math.exp(-moment.age * 2) *
          0.19 *
          Math.min(1, distance / 0.8)
        : 0;
      pos.setXYZ(i, x + side * wave, y, z);
    }
    pos.needsUpdate = true;
  });
  return (
    <group>
      {[-3.72, 3.72].map((z) => (
        <mesh key={z} position={[side * 52.5, 1.25, z]} castShadow>
          <cylinderGeometry args={[0.06, 0.06, 2.5, 12]} />
          <meshStandardMaterial
            color="#f3f4ed"
            metalness={0.15}
            roughness={0.32}
          />
        </mesh>
      ))}
      <mesh
        position={[side * 52.5, 2.5, 0]}
        rotation={[Math.PI / 2, 0, 0]}
        castShadow
      >
        <cylinderGeometry args={[0.06, 0.06, 7.56, 12]} />
        <meshStandardMaterial
          color="#f3f4ed"
          metalness={0.15}
          roughness={0.32}
        />
      </mesh>
      {[-3.66, 3.66].map((z) => (
        <group key={z}>
          <mesh
            position={[side * 54.7, 0.06, z]}
            rotation={[0, 0, Math.PI / 2]}
          >
            <cylinderGeometry args={[0.025, 0.025, 0.7, 6]} />
            <meshStandardMaterial color="#b5c5c8" />
          </mesh>
          <mesh
            position={[side * 54.4, 1.22, z]}
            rotation={[0, 0, side * 0.26]}
          >
            <cylinderGeometry args={[0.02, 0.02, 2.55, 6]} />
            <meshStandardMaterial color="#91a5aa" />
          </mesh>
        </group>
      ))}
      <lineSegments geometry={net.geometry}>
        <lineBasicMaterial
          color="#e0e9e7"
          transparent
          opacity={0.58}
          depthWrite={false}
        />
      </lineSegments>
      <mesh position={[side * 53.6, 0.035, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <planeGeometry args={[2.2, 7.32]} />
        <meshBasicMaterial
          color="#071b0b"
          transparent
          opacity={0.1}
          depthWrite={false}
        />
      </mesh>
    </group>
  );
}
export default function ArenaGoals({ mood }: { mood: Atmosphere }) {
  return (
    <>
      <Goal side={-1} mood={mood} />
      <Goal side={1} mood={mood} />
    </>
  );
}
