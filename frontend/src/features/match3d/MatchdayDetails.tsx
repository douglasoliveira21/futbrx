import { useEffect, useMemo } from "react";
import * as THREE from "three";

export function useGrassTexture() {
  const texture = useMemo(() => {
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = 512;
    const ctx = canvas.getContext("2d")!;
    ctx.fillStyle = "#859a72";
    ctx.fillRect(0, 0, 512, 512);
    let seed = 71;
    for (let i = 0; i < 18000; i++) {
      seed = (seed * 1664525 + 1013904223) >>> 0;
      const x = seed % 512;
      seed = (seed * 1664525 + 1013904223) >>> 0;
      const y = seed % 512;
      ctx.fillStyle = i % 2 ? "#294c2440" : "#d9e8bb38";
      ctx.fillRect(x, y, 1, 2 + (i % 4));
    }
    const map = new THREE.CanvasTexture(canvas);
    map.wrapS = map.wrapT = THREE.RepeatWrapping;
    map.repeat.set(2, 8);
    map.colorSpace = THREE.SRGBColorSpace;
    map.anisotropy = 4;
    return map;
  }, []);
  useEffect(() => () => texture.dispose(), [texture]);
  return texture;
}

export default function MatchdayDetails() {
  const markings = useMemo(() => {
    const points: number[] = [];
    for (const side of [-1, 1]) {
      const max = Math.acos(5.5 / 9.15);
      for (let i = 0; i < 48; i++) {
        for (const angle of [
          -max + (2 * max * i) / 48,
          -max + (2 * max * (i + 1)) / 48,
        ])
          points.push(
            side * (41.5 - 9.15 * Math.cos(angle)),
            0.08,
            9.15 * Math.sin(angle),
          );
      }
      for (const corner of [-1, 1])
        for (let i = 0; i < 18; i++)
          for (const angle of [
            ((i / 18) * Math.PI) / 2,
            (((i + 1) / 18) * Math.PI) / 2,
          ])
            points.push(
              side * (52.5 - Math.cos(angle)),
              0.08,
              corner * (34 - Math.sin(angle)),
            );
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute(
      "position",
      new THREE.Float32BufferAttribute(points, 3),
    );
    return geometry;
  }, []);
  useEffect(() => () => markings.dispose(), [markings]);

  return (
    <>
      <lineSegments geometry={markings}>
        <lineBasicMaterial color="#e0e9d6" />
      </lineSegments>
      <mesh position={[0, 0.08, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <circleGeometry args={[0.12, 12]} />
        <meshBasicMaterial color="#e0e9d6" />
      </mesh>
      {[-1, 1].map((side) => (
        <group key={side}>
          {[-1, 1].map((z) => (
            <mesh
              key={z}
              position={[side * 49.75, 0.075, z * 9.16]}
              rotation={[-Math.PI / 2, 0, 0]}
            >
              <planeGeometry args={[5.5, 0.13]} />
              <meshBasicMaterial color="#dce7d0" />
            </mesh>
          ))}
          <mesh
            position={[side * 47, 0.075, 0]}
            rotation={[-Math.PI / 2, 0, 0]}
          >
            <planeGeometry args={[0.13, 18.32]} />
            <meshBasicMaterial color="#dce7d0" />
          </mesh>
          <mesh
            position={[side * 41.5, 0.08, 0]}
            rotation={[-Math.PI / 2, 0, 0]}
          >
            <circleGeometry args={[0.15, 16]} />
            <meshBasicMaterial color="#e0ead9" />
          </mesh>
        </group>
      ))}
    </>
  );
}
