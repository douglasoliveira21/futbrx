import { Suspense, useEffect, useMemo, useRef } from "react";
import { useFrame } from "@react-three/fiber";
import * as THREE from "three";
import FootballerAvatar, { type AvatarPose } from "./FootballerAvatar";
import { framePair } from "./playback";
import type { MatchReplay } from "../../services/api";
type Timeline = React.RefObject<{
  minute: number;
  playing: boolean;
  visible: boolean;
  entrance?: number;
  animationScale?: number;
}>;

export function AssistantReferee({
  side,
  color,
  timeline,
  replay,
}: {
  side: number;
  color: string;
  timeline: Timeline;
  replay: React.RefObject<MatchReplay | undefined>;
}) {
  const group = useRef<THREE.Group>(null);
  const pose = useRef<AvatarPose>({
    id: 900001 + (side + 1),
    action: "idle",
    shirtNumber: 0,
    flag: true,
  });
  useFrame((_, delta) => {
    if (!group.current) return;
    const second = Math.max(0, timeline.current.minute) * 60;
    const frame = replay.current && framePair(replay.current.frames, second)?.a;
    // Each assistant covers one half and follows the second-last defender or the ball.
    const swapped =
      replay.current?.events.some(
        (e) =>
          e.type === "second_half" && (e.second ?? e.minute * 60) <= second,
      ) ?? false;
    const defendingTeam = (side > 0 ? 1 : 0) ^ (swapped ? 1 : 0);
    const defenders =
      frame?.players
        .filter((p) => p.active !== false && p.team === defendingTeam)
        .map((p) => p.x)
        .sort((a, b) => side * (b - a)) ?? [];
    const line =
      side > 0
        ? Math.max(frame?.ball.x ?? 0, defenders[1] ?? 0)
        : Math.min(frame?.ball.x ?? 0, defenders[1] ?? 0);
    const target = THREE.MathUtils.clamp(
      line,
      side > 0 ? 0 : -52,
      side > 0 ? 52 : 0,
    );
    const dx = target - group.current.position.x;
    const flag = replay.current?.events.some(
      (e) =>
        e.type === "offside" &&
        (frame?.ball.x ?? 0) * side >= 0 &&
        second - (e.second ?? e.minute * 60) >= 0 &&
        second - (e.second ?? e.minute * 60) < 30,
    );
    if (timeline.current.playing)
      group.current.position.x += THREE.MathUtils.clamp(
        dx,
        -delta * 12,
        delta * 12,
      );
    group.current.rotation.y =
      Math.abs(dx) > 0.3
        ? (Math.sign(dx) * Math.PI) / 2
        : side > 0
          ? 0
          : Math.PI;
    pose.current = {
      ...pose.current,
      action: flag
        ? "yellow_card"
        : Math.abs(dx) > 0.3 && timeline.current.playing
          ? "run"
          : "idle",
      actionSecond: flag ? second / (timeline.current.animationScale ?? 1) : 0,
      flag: true,
    };
  });
  return (
    <group ref={group} position={[side * 20, 0, side * 34.8]}>
      <Suspense fallback={null}>
        <FootballerAvatar color={color} timeline={timeline} pose={pose} />
      </Suspense>
    </group>
  );
}

export default function MatchCeremony({
  color,
  timeline,
}: {
  color: string;
  timeline: Timeline;
}) {
  const smoke = useRef<THREE.InstancedMesh>(null),
    sparks = useRef<THREE.Points>(null);
  const dummy = useMemo(() => new THREE.Object3D(), []);
  const particles = useMemo(() => {
    const positions = new Float32Array(256 * 3),
      colors = new Float32Array(256 * 3),
      home = new THREE.Color(color),
      white = new THREE.Color("#fff1b9");
    for (let i = 0; i < 256; i++) {
      const c = i % 5 ? home : white;
      colors.set(c.toArray(), i * 3);
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute("color", new THREE.BufferAttribute(colors, 3));
    return geometry;
  }, [color]);
  useEffect(() => () => particles.dispose(), [particles]);
  useFrame(() => {
    const t = timeline.current.entrance ?? 12,
      visible = t >= 0 && t < 12;
    if (smoke.current) {
      smoke.current.visible = visible;
      for (let i = 0; i < 64; i++) {
        const age = (t + i * 0.073) % 4,
          side = i % 2 ? 1 : -1;
        dummy.position.set(
          side * (7 + Math.sin(i * 2.4) * age * 0.6),
          0.4 + age * 1.8,
          38 + Math.cos(i * 1.7) * age * 0.5,
        );
        dummy.scale.setScalar(0.25 + age * 0.45);
        dummy.updateMatrix();
        smoke.current.setMatrixAt(i, dummy.matrix);
      }
      smoke.current.instanceMatrix.needsUpdate = true;
    }
    if (sparks.current) {
      sparks.current.visible = visible;
      const a = particles.attributes.position as THREE.BufferAttribute;
      for (let i = 0; i < 256; i++) {
        const burst = Math.floor(i / 64),
          age = (t + burst * 0.7) % 2.8,
          angle = i * 2.39996,
          v = 5 + (i % 11) * 0.2;
        a.setXYZ(
          i,
          (burst % 2 ? 1 : -1) * 19 + Math.cos(angle) * v * age,
          10 + Math.sin(i * 1.83) * v * age - age * age * 2,
          39 + Math.sin(angle) * v * age * 0.6,
        );
      }
      a.needsUpdate = true;
    }
  });
  return (
    <>
      <instancedMesh
        ref={smoke}
        args={[undefined, undefined, 64]}
        frustumCulled={false}
      >
        <sphereGeometry args={[1, 7, 5]} />
        <meshStandardMaterial
          color={color}
          transparent
          opacity={0.16}
          depthWrite={false}
        />
      </instancedMesh>
      <points ref={sparks} geometry={particles} frustumCulled={false}>
        <pointsMaterial
          size={0.22}
          vertexColors
          transparent
          opacity={0.9}
          depthWrite={false}
          blending={THREE.AdditiveBlending}
        />
      </points>
    </>
  );
}
