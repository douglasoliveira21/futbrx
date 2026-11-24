import MatchCeremony, { AssistantReferee } from "./MatchCeremony";
import { FutbrxLogo } from "../../components/FutbrxLogo";
import MatchManagement from "./MatchManagement";
import { matchKitColors } from "./kitColors";
import React, { useEffect, useMemo, useRef, useState, Suspense } from "react";
import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { Line, OrbitControls, PerspectiveCamera } from "@react-three/drei";
import * as THREE from "three";
import { Button } from "../../components/ui/button";
import {
  api,
  type Fixture,
  type Club,
  type MatchReplay,
  type BroadcastSession,
} from "../../services/api";
import { useQuery } from "@tanstack/react-query";
import {
  framePair,
  lerp,
  lerpYaw,
  broadcastPosition,
  broadcastClock,
  replayBallHeight,
} from "./playback";
import { Play, Pause, RotateCcw, Volume2, VolumeX } from "lucide-react";
import FootballerAvatar, {
  AvatarBoundary,
  type AvatarPose,
} from "./FootballerAvatar";
import Arena, { type Atmosphere } from "./Arena";
import ArenaGoals from "./ArenaGoals";
import { BALL_RADIUS, footballGeometry } from "./footballGeometry";
import StadiumDetails, { PerformanceMeter } from "./StadiumDetails";
import MatchdayDetails, { useGrassTexture } from "./MatchdayDetails";
import { useBroadcast } from "./useBroadcast";
import { StadiumAudio } from "./StadiumAudio";
import {
  resolvePresentationPose,
  qualityProfiles,
  initialQuality,
  type Quality,
} from "./presentation";
type Timeline = {
  minute: number;
  playing: boolean;
  visible: boolean;
  entrance?: number;
  animationScale?: number;
};
function WebGLContextMonitor({onLost}:{onLost:()=>void}) {
  const gl=useThree(state=>state.gl);
  const callback=useRef(onLost);
  callback.current=onLost;
  useEffect(()=>{
    const lost=()=>callback.current();
    gl.domElement.addEventListener("webglcontextlost",lost);
    return ()=>gl.domElement.removeEventListener("webglcontextlost",lost);
  },[gl]);
  return null;
}
function Pitch() {
  const grass = useGrassTexture();
  const lines = useMemo(() => {
    const pts: THREE.Vector3[] = [];
    for (let i = 0; i <= 80; i++) {
      let a = (i / 80) * Math.PI * 2;
      pts.push(new THREE.Vector3(Math.cos(a) * 9.15, 0.06, Math.sin(a) * 9.15));
    }
    return new THREE.BufferGeometry().setFromPoints(pts);
  }, []);
  useEffect(() => () => lines.dispose(), [lines]);
  return (
    <>
      <mesh rotation={[-Math.PI / 2, 0, 0]} receiveShadow>
        <planeGeometry args={[118, 80]} />
        <meshStandardMaterial color="#29633e" roughness={1} />
      </mesh>
      {Array.from({ length: 14 }, (_, i) => (
        <mesh
          key={i}
          rotation={[-Math.PI / 2, 0, 0]}
          position={[-48.75 + i * 7.5, 0.02, 0]}
        >
          <planeGeometry args={[7.5, 68]} />
          <meshStandardMaterial
            map={grass}
            bumpMap={grass}
            bumpScale={0.035}
            color={i % 2 ? "#51864a" : "#457a43"}
            roughness={1}
          />
        </mesh>
      ))}
      <lineLoop geometry={lines}>
        <lineBasicMaterial color="#d3e7ce" />
      </lineLoop>
      {[
        [0, 0, 0.14, 68],
        [0, -34, 105, 0.14],
        [0, 34, 105, 0.14],
        [-52.5, 0, 0.14, 68],
        [52.5, 0, 0.14, 68],
        [-36, 0, 0.14, 40.3],
        [36, 0, 0.14, 40.3],
        [-44.25, -20.15, 16.5, 0.14],
        [-44.25, 20.15, 16.5, 0.14],
        [44.25, -20.15, 16.5, 0.14],
        [44.25, 20.15, 16.5, 0.14],
      ].map(([x, z, w, h], i) => (
        <mesh key={i} position={[x, 0.065, z]} rotation={[-Math.PI / 2, 0, 0]}>
          <planeGeometry args={[w, h]} />
          <meshBasicMaterial color="#d3e7ce" />
        </mesh>
      ))}
    </>
  );
}
function Footballer({
  index,
  team,
  color,
  timeline,
  replay,
  rigged,
}: {
  rigged: boolean;
  index: number;
  team: number;
  color: string;
  timeline: React.RefObject<Timeline>;
  replay: React.RefObject<MatchReplay | undefined>;
}) {
  const ref = useRef<THREE.Group>(null);
  const pose = useRef<AvatarPose>({ id: team * 11 + index, action: "idle" });
  const motion = useRef({ second: -1, x: 0, z: 0, distance: 0 });
  const left = useRef<THREE.Group>(null);
  const right = useRef<THREE.Group>(null);
  const row = index === 0 ? 0 : index < 5 ? 1 : index < 8 ? 2 : 3;
  const n = row === 0 ? 1 : row === 1 ? 4 : 3;
  const j =
    row === 0 ? 0 : row === 1 ? index - 1 : row === 2 ? index - 5 : index - 8;
  const baseX = (team === 0 ? 1 : -1) * (-49 + row * 15);
  const baseZ = (j - (n - 1) / 2) * 13;
  useFrame(() => {
    if (!ref.current) return;
    const t = timeline.current.minute;
    const entry = timeline.current.entrance;
    if (entry !== undefined && entry < 12) {
      const target = replay.current?.frames[0]?.players[team * 11 + index];
      const walk = Math.min(1, Math.max(0, entry - index * 0.1) / 9),
        spread = entry >= 9 ? 1 : 0;
      const x = team === 0 ? -1.4 : 1.4,
        z = 41 + index * 0.72 - walk * 17;
      ref.current.visible = true;
      ref.current.position.set(
        lerp(x, target?.x ?? baseX, spread),
        0,
        lerp(z, target?.z ?? baseZ, spread),
      );
      ref.current.rotation.y =
        spread > 0
          ? Math.atan2((target?.x ?? baseX) - x, (target?.z ?? baseZ) - z)
          : Math.PI;
      pose.current = {
        id: team * 11 + index,
        action: spread ? "idle" : "enter",
        actionSecond: -12,
        locomotionDistance: walk * 17,
        shirtNumber: index + 1,
        goalkeeper: index === 0,
      };
      if (left.current)
        left.current.rotation.x = Math.sin(entry * 9 + index) * 0.6;
      if (right.current)
        right.current.rotation.x = -Math.sin(entry * 9 + index) * 0.6;
      return;
    }
    const pair = replay.current && framePair(replay.current.frames, t * 60);
    if (pair) {
      const a = pair.a.players[team * 11 + index],
        b = pair.b.players[team * 11 + index];
      const current = pair.alpha < 1 ? a : b;
      const dismissed =
        current.active === false &&
        replay.current?.events.some(
          (e) =>
            e.type === "red_card" &&
            e.player === current.id &&
            t * 60 - (e.second ?? e.minute * 60) >= 0 &&
            t * 60 - (e.second ?? e.minute * 60) < 36,
        );
      ref.current.visible = current.active !== false || !!dismissed;
      const action = current.action;
      const animationScale = timeline.current.animationScale ?? 1;
      const anticipation =
        animationScale > 1 && replay.current
          ? (framePair(replay.current.frames, t * 60 + animationScale * 1.1)?.b
              .players[team * 11 + index] ?? b)
          : b;
      pose.current = resolvePresentationPose(
        current,
        anticipation,
        t * 60,
        pair.a.second,
        replay.current?.events ?? [],
        index === 0,
        animationScale,
      );
      if (dismissed) pose.current.action = "idle";
      const nextX = lerp(a.x, b.x, pair.alpha),
        nextZ = lerp(a.z, b.z, pair.alpha);
      const elapsed = t * 60 - motion.current.second;
      if (elapsed >= 0 && elapsed < 12 && motion.current.second >= 0)
        motion.current.distance += Math.hypot(
          nextX - motion.current.x,
          nextZ - motion.current.z,
        );
      else motion.current.distance = 0;
      motion.current.second = t * 60;
      motion.current.x = nextX;
      motion.current.z = nextZ;
      pose.current.locomotionDistance = motion.current.distance;
      ref.current.position.set(
        lerp(a.x, b.x, pair.alpha),
        !rigged && action === "celebrate"
          ? Math.abs(Math.sin(t * 22)) * 0.18
          : 0,
        lerp(a.z, b.z, pair.alpha),
      );
      ref.current.rotation.y = lerpYaw(a.yaw, b.yaw, pair.alpha);
      ref.current.rotation.z =
        !rigged && action === "save" ? Math.sin(t * 8) * 0.45 : 0;
      const stride =
        timeline.current.playing && timeline.current.visible && action === "run"
          ? Math.sin(t * 30 + index) * 0.65
          : 0;
      if (left.current)
        left.current.rotation.x = action === "kick" ? -0.9 : stride;
      if (right.current) right.current.rotation.x = -stride;
      return;
    }
    ref.current.visible = true;
    pose.current.action = "run";
    const wave = Math.sin(t * 0.18);
    ref.current.position.set(
      baseX + (index ? wave * 10 : 0),
      0,
      baseZ + Math.cos(t * 0.12 + index) * 3,
    );
    ref.current.rotation.y = team === 0 ? Math.PI / 2 : -Math.PI / 2;
    const stride =
      timeline.current.playing && timeline.current.visible
        ? Math.sin(t * 5 + index) * 0.65
        : 0;
    if (left.current) left.current.rotation.x = stride;
    if (right.current) right.current.rotation.x = -stride;
  });
  const skin = ["#c9926a", "#865b41", "#e3b18d", "#613e2b"][index % 4];
  const body = (
    <>
      <mesh position={[0, 1.22, 0]} castShadow>
        <capsuleGeometry args={[0.27, 0.45, 3, 6]} />
        <meshStandardMaterial color={color} />
      </mesh>
      <mesh position={[0, 1.94, 0]} castShadow>
        <sphereGeometry args={[0.22, 8, 6]} />
        <meshStandardMaterial color={skin} />
      </mesh>
      {[-1, 1].map((s, i) => (
        <group
          key={s}
          ref={i === 0 ? left : right}
          position={[s * 0.16, 0.94, 0]}
        >
          <mesh position={[0, -0.34, 0]} castShadow>
            <capsuleGeometry args={[0.11, 0.5, 2, 5]} />
            <meshStandardMaterial color={color} />
          </mesh>
          <mesh position={[0, -0.69, 0.07]}>
            <boxGeometry args={[0.19, 0.15, 0.34]} />
            <meshStandardMaterial color="#12202a" />
          </mesh>
          <mesh position={[s * 0.28, 0.29, 0]} rotation={[0, 0, s * 0.22]}>
            <capsuleGeometry args={[0.09, 0.48, 2, 5]} />
            <meshStandardMaterial color={skin} />
          </mesh>
        </group>
      ))}
    </>
  );
  return (
    <group ref={ref}>
      <mesh position={[0, 0.025, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <circleGeometry args={[0.48, 16]} />
        <meshBasicMaterial
          color="#06130b"
          transparent
          opacity={0.2}
          depthWrite={false}
        />
      </mesh>
      {rigged ? (
        <Suspense fallback={body}>
          <AvatarBoundary fallback={body}>
            <FootballerAvatar color={color} timeline={timeline} pose={pose} />
          </AvatarBoundary>
        </Suspense>
      ) : (
        body
      )}
    </group>
  );
}
function Referee({
  timeline,
  replay,
  rigged,
  color,
}: {
  timeline: React.RefObject<Timeline>;
  replay: React.RefObject<MatchReplay | undefined>;
  rigged: boolean;
  color: string;
}) {
  const ref = useRef<THREE.Group>(null);
  const pose = useRef<AvatarPose>({
    id: 900000,
    action: "idle",
    shirtNumber: 0,
  });
  useFrame((_, delta) => {
    if (!ref.current) return;
    if (
      timeline.current.entrance !== undefined &&
      timeline.current.entrance < 9
    ) {
      ref.current.position.set(0, 0, 39 - timeline.current.entrance * 1.8);
      ref.current.rotation.y = Math.PI;
      pose.current.action = "enter";
      return;
    }
    const second = Math.max(0, timeline.current.minute) * 60;
    const booking = replay.current?.events
      .slice()
      .reverse()
      .find(
        (e) =>
          ["yellow_card", "red_card", "second_yellow"].includes(e.type) &&
          (e.second ?? e.minute * 60) <= second &&
          second - (e.second ?? e.minute * 60) < 42,
      );
    if (booking) {
      const offender =
        replay.current &&
        framePair(replay.current.frames, second)?.a.players.find(
          (p) => p.id === booking.player,
        );
      if (offender)
        ref.current.rotation.y = Math.atan2(
          offender.x - ref.current.position.x,
          offender.z - ref.current.position.z,
        );
      const secondYellow =
        booking.type === "second_yellow" ||
        replay.current?.events.some(
          (e) =>
            e.type === "second_yellow" &&
            e.player === booking.player &&
            e.second === booking.second,
        );
      pose.current.action =
        booking.type === "yellow_card"
          ? "yellow_card"
          : secondYellow
            ? "second_yellow_card"
            : "red_card";
      pose.current.actionSecond =
        (booking.second ?? booking.minute * 60) /
          (timeline.current.animationScale ?? 1) +
        0.55;
      pose.current.card =
        booking.type === "yellow_card" ||
        (secondYellow && second - (booking.second ?? booking.minute * 60) < 30)
          ? "yellow"
          : "red";
      return;
    }
    pose.current.card = undefined;
    const frame =
      replay.current &&
      framePair(
        replay.current.frames,
        Math.max(0, timeline.current.minute) * 60,
      )?.a;
    const x = (frame?.ball.x ?? 0) * 0.55 - 6,
      z = (frame?.ball.z ?? 0) * 0.45 + 7;
    const distance = Math.hypot(
      x - ref.current.position.x,
      z - ref.current.position.z,
    );
    ref.current.rotation.y = Math.atan2(
      x - ref.current.position.x,
      z - ref.current.position.z,
    );
    ref.current.position.x = lerp(
      ref.current.position.x,
      x,
      1 - Math.exp(-delta * 2),
    );
    ref.current.position.z = lerp(
      ref.current.position.z,
      z,
      1 - Math.exp(-delta * 2),
    );
    pose.current.action =
      distance > 0.4 && timeline.current.playing ? "run" : "idle";
  });
  return (
    <group ref={ref} position={[-6, 0, 7]}>
      {rigged ? (
        <Suspense fallback={null}>
          <FootballerAvatar color={color} pose={pose} timeline={timeline} />
        </Suspense>
      ) : (
        <>
          <mesh position={[0, 1.2, 0]}>
            <capsuleGeometry args={[0.22, 0.55, 3, 6]} />
            <meshStandardMaterial color={color} />
          </mesh>
          <mesh position={[0, 1.85, 0]}>
            <sphereGeometry args={[0.18, 8, 6]} />
            <meshStandardMaterial color="#ae815c" />
          </mesh>
          {[-1, 1].map((x) => (
            <mesh key={x} position={[x * 0.12, 0.45, 0]}>
              <capsuleGeometry args={[0.09, 0.65, 2, 5]} />
              <meshStandardMaterial color="#121c29" />
            </mesh>
          ))}
        </>
      )}
    </group>
  );
}

function Ball({
  fixture,
  timeline,
  replay,
}: {
  fixture: Fixture;
  timeline: React.RefObject<Timeline>;
  replay: React.RefObject<MatchReplay | undefined>;
}) {
  const ref = useRef<THREE.Mesh>(null);
  const geometry = useMemo(footballGeometry, []);
  const lastPosition = useRef(new THREE.Vector3());
  useEffect(() => () => geometry.dispose(), [geometry]);
  useFrame(() => {
    if (!ref.current) return;
    ref.current.visible =
      timeline.current.entrance === undefined ||
      timeline.current.entrance >= 12;
    const minute = timeline.current.minute;
    const pair =
      replay.current && framePair(replay.current.frames, minute * 60);
    if (pair) {
      ref.current.position.set(
        lerp(pair.a.ball.x, pair.b.ball.x, pair.alpha),
        replayBallHeight(replay.current!.engineVersion,lerp(pair.a.ball.y,pair.b.ball.y,pair.alpha)),
        lerp(pair.a.ball.z, pair.b.ball.z, pair.alpha),
      );
      const dx = ref.current.position.x - lastPosition.current.x,
        dz = ref.current.position.z - lastPosition.current.z;
      if (Math.hypot(dx, dz) < 3) {
        ref.current.rotation.z -= dx / BALL_RADIUS;
        ref.current.rotation.x += dz / BALL_RADIUS;
      }
      lastPosition.current.copy(ref.current.position);
      return;
    }
    const event = fixture.events.find(
      (e) => e.minute >= minute && e.minute - minute < 2,
    );
    if (event) {
      const progress = 1 - (event.minute - minute) / 2;
      const direction = event.team === fixture.home ? 1 : -1;
      ref.current.position.set(
        direction * (20 + 34 * progress),
        0.24 + Math.sin(progress * Math.PI) * 2,
        Math.sin(progress * Math.PI * 2) * 4,
      );
    } else
      ref.current.position.set(
        Math.sin(minute * 0.31) * 24,
        0.25,
        Math.cos(minute * 0.53) * 17,
      );
    if (ref.current.position.y <= 0.25) {
      const dx = ref.current.position.x - lastPosition.current.x,
        dz = ref.current.position.z - lastPosition.current.z;
      if (Math.hypot(dx, dz) < 3) {
        ref.current.rotation.z -= dx / BALL_RADIUS;
        ref.current.rotation.x += dz / BALL_RADIUS;
      }
    } else ref.current.rotation.z += 0.02;
    lastPosition.current.copy(ref.current.position);
  });
  return (
    <mesh ref={ref} castShadow>
      <primitive object={geometry} attach="geometry" />
      <meshStandardMaterial vertexColors roughness={0.48} metalness={0.02} />
    </mesh>
  );
}
function Clock({
  timeline,
  onTick,
  speed,
  duration,
}: {
  timeline: React.RefObject<Timeline>;
  onTick: (n: number) => void;
  speed: number;
  duration: number;
}) {
  const lastReport = useRef(0);
  useFrame(({ clock }, delta) => {
    if (timeline.current.playing && timeline.current.visible) {
      timeline.current.minute = Math.min(
        duration,
        timeline.current.minute + (Math.min(delta, 0.1) * speed) / 60,
      );
      if (
        clock.elapsedTime - lastReport.current > 0.1 ||
        timeline.current.minute === duration
      ) {
        onTick(timeline.current.minute);
        lastReport.current = clock.elapsedTime;
      }
    }
  });
  return null;
}
class WebGLBoundary extends React.Component<
  { children: React.ReactNode; fallback?: React.ReactNode; onFailure?: (message:string)=>void },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  componentDidCatch(error:Error) { this.props.onFailure?.(error.message); }
  render() {
    return this.state.failed
      ? (this.props.fallback ?? (
          <div className="match-fallback">Visualização 3D indisponível.</div>
        ))
      : this.props.children;
  }
}
function TacticalFallback({
  replay,
  minute,
  home,
  away,
}: {
  replay?: MatchReplay;
  minute: number;
  home: string;
  away: string;
}) {
  const frame = replay && framePair(replay.frames, Math.max(0, minute) * 60)?.a;
  return (
    <div className="tactical-fallback">
      <svg
        viewBox="-57 -39 114 78"
        role="img"
        aria-label="Transmissão tática da partida"
      >
        <rect x="-56" y="-38" width="112" height="76" fill="#1c5435" />
        <g fill="none" stroke="#ffffff80" strokeWidth=".2">
          <rect x="-52.5" y="-34" width="105" height="68" />
          <path d="M0 -34v68M-52.5 -20.15h16.5v40.3h-16.5M52.5 -20.15H36v40.3h16.5" />
          <circle r="9.15" />
        </g>
        {frame?.players
          .filter((p) => p.active !== false)
          .map((p) => (
            <circle
              key={p.id}
              cx={p.x}
              cy={p.z}
              r=".85"
              fill={p.team === 0 ? home : away}
              stroke="white"
              strokeWidth=".2"
            />
          ))}
        {frame && (
          <circle cx={frame.ball.x} cy={frame.ball.z} r=".4" fill="white" />
        )}
      </svg>
      <span>Modo tático · placar e lances ao vivo</span>
    </div>
  );
}

function BroadcastCamera({
  mode,
  timeline,
  replay,
}: {
  mode: string;
  timeline: React.RefObject<Timeline>;
  replay: React.RefObject<MatchReplay | undefined>;
}) {
  const desired = useMemo(() => new THREE.Vector3(), []);
  const target = useMemo(() => new THREE.Vector3(), []);
  useFrame(({ camera }, delta) => {
    if (mode === "orbital") {
      camera.up.set(0, 1, 0);
      return;
    }
    if (
      timeline.current.entrance !== undefined &&
      timeline.current.entrance < 9
    ) {
      desired.set(12, 4.5, 23);
      target.set(0, 1, 36 - timeline.current.entrance * 1.2);
      camera.position.lerp(desired, 1 - Math.exp(-delta * 2));
      camera.lookAt(target);
      return;
    }
    const pair =
      replay.current &&
      framePair(replay.current.frames, timeline.current.minute * 60);
    const x = pair ? lerp(pair.a.ball.x, pair.b.ball.x, pair.alpha) : 0;
    const z = pair ? lerp(pair.a.ball.z, pair.b.ball.z, pair.alpha) : 0;
    camera.up.set(0, mode === "tactical" ? 0 : 1, mode === "tactical" ? -1 : 0);
    if (mode === "tactical") {
      desired.set(0, 108, 0.01);
      target.set(0, 0, 0);
    } else if (mode === "goal") {
      desired.set(x >= 0 ? 66 : -66, 15, 10);
      target.set(x * 0.5, 0, z * 0.5);
    } else if (mode === "contact") {
      const involved = pair?.a.players.find(p => p.id === pair.a.lastContact?.playerId);
      desired.set(x - 7, 7, z + 10);
      target.set(involved ? (x + involved.x) / 2 : x, 1, involved ? (z + involved.z) / 2 : z);
    } else if (mode === "pitch") {
      desired.set(x - 9, 5, z + 11);
      target.set(x, 1, z);
    } else {
      desired.set(x * 0.55, 22, 36);
      target.set(x * 0.65, 0, z * 0.4);
    }
    camera.position.lerp(desired, 1 - Math.exp(-delta * 3));
    camera.lookAt(target);
  });
  return null;
}
export default function Match3D({
  fixture,
  clubs,
  careerId,
  season,
  live,
  onComplete,
  validationReplay,
}: {
  validationReplay?: MatchReplay;
  live?: BroadcastSession;
  onComplete?: () => Promise<void>;
  fixture: Fixture;
  clubs: Club[];
  careerId: string;
  season: number;
}) {
  const recordedReplay = useQuery({
    queryKey: [
      "replay",
      careerId,
      season,
      fixture.round,
      fixture.home,
      fixture.seed,
    ],
    queryFn: () =>
      api<MatchReplay>(
        fixture.competitionMatchId
          ? `/careers/${careerId}/life/replay/${fixture.competitionMatchId}`
          : `/careers/${careerId}/matches/${fixture.round}/${fixture.home}/replay?season=${season}`,
      ),
    enabled: !!fixture.setup && !live && !validationReplay,
    staleTime: Infinity,
    gcTime: 60_000,
    retry: 1,
  });
  const replayRef = useRef<MatchReplay | undefined>(undefined);
  const [minute, setMinute] = useState(0);
  const [playing, setPlaying] = useState(true);
  const [managerOpen, setManagerOpen] = useState(false);
  const [speed, setSpeed] = useState(30);
  const [cameraMode, setCameraMode] = useState("tv");
  const [qualityMode, setQualityMode] = useState<Quality | "auto">("auto");
  const [autoQuality, setAutoQuality] = useState<Quality>(() =>
    initialQuality(
      (navigator as Navigator & { deviceMemory?: number }).deviceMemory,
      navigator.hardwareConcurrency,
    ),
  );
  const quality = qualityMode === "auto" ? autoQuality : qualityMode;
  const profile = qualityProfiles[quality];
  const high = profile.shadows;
  const [performance, setPerformance] = useState({
    fps: 0,
    calls: 0,
    triangles: 0,
    cpuMs: 0,
    gpuMs: null as number | null,
  });
  const benchmark=useRef<Partial<Record<Quality,{samples:number;sum:number;min:number;max:number}>>>({});
  const [volume, setVolume] = useState(0.3);
  const [audioError, setAudioError] = useState("");
  const [audioBusy, setAudioBusy] = useState(false);
  const [sound, setSound] = useState(false);
  const [visible, setVisible] = useState(true);
  const [contextLost, setContextLost] = useState(false);
  const [renderError,setRenderError]=useState("");
  const [contextGeneration,setContextGeneration]=useState(0);
  const rendererFailed=(message:string)=>{
    setContextLost(true);setRenderError(message);setPlaying(false);
    setPerformance({fps:0,calls:0,triangles:0,cpuMs:0,gpuMs:null});
    benchmark.current={};
  };
  const broadcast = useBroadcast(
    live,
    careerId,
    playing && !managerOpen,
    visible,
  );
  const replay = live
    ? {
        data: broadcast.packet?.replay,
        isPending: !broadcast.packet,
        error: undefined,
        refetch: recordedReplay.refetch,
      }
    : validationReplay ? {...recordedReplay,data:validationReplay,isPending:false,isLoading:false,isError:false} : recordedReplay;
  // The inspector draws recorded backend positions, never a predicted path.
  const inspectedPath = useMemo(() => validationReplay?.frames
    .filter(f => Math.abs(f.second - minute * 60) <= 6)
    .map(f => [f.ball.x, f.ball.y, f.ball.z] as [number, number, number]) ?? [], [validationReplay, minute]);
  useEffect(() => {
    replayRef.current = replay.data;
    timeline.current.animationScale = [
      "positional-v5",
      "positional-v6",
      "positional-v7",
      "positional-v8",
      "positional-v9",
      "positional-v10",
      "positional-v11",
      "positional-v12",
      "positional-v13",
      "positional-v14",
    ].includes(replay.data?.engineVersion ?? "")
      ? 12
      : 1;
  }, [replay.data]);
  useEffect(() => {
    if (
      broadcast.packet?.session.halfTimeAwaiting ||
      broadcast.packet?.session.stoppagePaused
    ) {
      timeline.current.minute =
        (broadcast.packet.session.simulatedSecond ?? 0) / 60;
      setMinute(timeline.current.minute);
      setPlaying(false);
      setManagerOpen(true);
    }
  }, [
    broadcast.packet?.session.halfTimeAwaiting,
    broadcast.packet?.session.stoppagePaused,
  ]);
  const [entrance, setEntrance] = useState(live?.watchedSeconds ?? 12);
  const [finishError, setFinishError] = useState("");
  const [finishing, setFinishing] = useState(false);
  const container = useRef<HTMLDivElement>(null);
  const intersects = useRef(true);
  const timeline = useRef<Timeline>({
    minute: 0,
    playing: true,
    visible: true,
  });
  const playbackStarted = useRef(false);
  const atmosphere: Atmosphere = useRef({
    danger: 0,
    energy: 0.18,
    reaction: "normal",
    age: 100,
    goalSide: 1,
  });
  const audio = useRef<StadiumAudio | null>(null);
  useEffect(() => {
    if (!live || !broadcast.packet) return;
    let frame = 0,
      last = globalThis.performance.now(),
      lastReport = 0;
    const base = broadcast.packet.session.watchedSeconds,
      started = last;
    const paint = (now: number) => {
      const active =
        playing &&
        visible &&
        !managerOpen &&
        !broadcast.packet!.session.halfTimeAwaiting &&
        !broadcast.packet!.session.stoppagePaused &&
        !broadcast.error;
      const elapsed =
        base + (active ? Math.min(1.2, (now - started) / 1000) : 0);
      const entry = Math.min(live.entranceSeconds, elapsed);
      const available = broadcast.packet!.replay.frames.at(-1)?.second ?? 0;
      if (entry < live.entranceSeconds)
        timeline.current.minute = (entry - live.entranceSeconds) / 60;
      else {
        if (!playbackStarted.current) {
          timeline.current.minute =
            Math.max(
              0,
              (broadcast.packet!.session.matchSecond ??
                (base - live.entranceSeconds) * 12) -
                broadcast.packet!.session.speed,
            ) / 60;
          playbackStarted.current = true;
        }
        timeline.current.minute =
          broadcastPosition(
            timeline.current.minute * 60,
            available,
            (now - last) / 1000,
            broadcast.packet!.session.speed,
            active,
          ) / 60;
      }
      timeline.current.entrance = entry;
      if (now - lastReport > 100) {
        setEntrance(entry);
        setMinute(timeline.current.minute);

        lastReport = now;
      }
      last = now;
      frame = requestAnimationFrame(paint);
    };
    frame = requestAnimationFrame(paint);
    return () => cancelAnimationFrame(frame);
  }, [
    broadcast.packet,
    broadcast.error,
    live?.id,
    playing,
    visible,
    managerOpen,
  ]);
  useEffect(() => {
    if (!live) return;
    const warn = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [live?.id]);
  const [audioLoaded, setAudioLoaded] = useState(-1);
  const [audioLevel, setAudioLevel] = useState(0);
  useEffect(() => {
    const prepared = StadiumAudio.takePrepared();
    if (prepared) {
      audio.current = prepared;
      prepared.seek(Math.max(0, timeline.current.minute));
      prepared.setVolume(volume);
      setSound(true);
      void prepared.ready.then(() => setAudioLoaded(prepared.loaded));
    }
  }, []);
  useEffect(() => {
    let handle = 0,
      lastSample = 0;
    const sample = (now: number) => {
      const player = audio.current,
        data = replayRef.current;
      if (
        player &&
        data &&
        playing &&
        visible &&
        (timeline.current.entrance === undefined ||
          timeline.current.entrance >= 12)
      ) {
        const second = Math.max(0, timeline.current.minute * 60),
          pair = framePair(data.frames, second);
        if (pair) {
          if (now - lastSample > 50) {
            lastSample = now;
            setAudioLevel(Math.min(1, player.level() * 5));
          }
          player.sample(
            second,
            {
              x: lerp(pair.a.ball.x, pair.b.ball.x, pair.alpha),
              y: lerp(pair.a.ball.y, pair.b.ball.y, pair.alpha),
              z: lerp(pair.a.ball.z, pair.b.ball.z, pair.alpha),
            },
            data.events,
            pair.a.players,
            broadcast.packet?.session.speed ?? live?.speed ?? speed,
            data.setup.home.id,
          );
        }
      }
      handle = requestAnimationFrame(sample);
    };
    handle = requestAnimationFrame(sample);
    return () => cancelAnimationFrame(handle);
  }, [playing, visible, speed, live?.speed, broadcast.packet?.session.speed]);
  async function previewSound(type: "whistle" | "kick" | "goal" | "near-miss") {
    if (!audio.current) await toggleSound();
    const player = audio.current;
    if (!player) return;
    setAudioBusy(true);
    try {
      await player.ready;
      await player.setActive(true);
      player.preview(type);
      setTimeout(
        () =>
          void player.setActive(
            timeline.current.playing && timeline.current.visible,
          ),
        type === "goal" ? 9500 : 4000,
      );
    } finally {
      setAudioBusy(false);
    }
  }
  const h = clubs.find((c) => c.id === fixture.home)!;
  const a = clubs.find((c) => c.id === fixture.away)!;
  const kits = matchKitColors(h.color, a.color);
  useEffect(() => {
    timeline.current.playing =
      playing && !managerOpen && !broadcast.packet?.session.halfTimeAwaiting;
    timeline.current.visible = visible;
  }, [
    playing,
    visible,
    managerOpen,
    broadcast.packet?.session.halfTimeAwaiting,
  ]);
  useEffect(() => {
    const onVisibility = () =>
      setVisible(intersects.current && !document.hidden);
    document.addEventListener("visibilitychange", onVisibility);
    const observer = new IntersectionObserver(([e]) => {
      intersects.current = e.isIntersecting;
      setVisible(e.isIntersecting && !document.hidden);
    });
    if (container.current) observer.observe(container.current);
    return () => {
      document.removeEventListener("visibilitychange", onVisibility);
      observer.disconnect();
    };
  }, [replay.isPending]);
  useEffect(
    () => () => {
      void audio.current?.close();
    },
    [],
  );
  const duration = (replay.data?.duration ?? 5400) / 60;
  const matchEvents = replay.data?.events ?? fixture.events;
  const eventTime = (e: (typeof fixture.events)[number]) =>
    e.second !== undefined && e.second >= 0 ? e.second / 60 : e.minute;
  const secondHalfAt = matchEvents.find(
    (e) => e.type === "second_half",
  )?.second;
  const eventClock = (e: (typeof fixture.events)[number]) =>
    broadcastClock(eventTime(e) * 60, secondHalfAt);
  const event = [...matchEvents].reverse().find((e) => eventTime(e) <= minute);
  const review = [...matchEvents]
    .reverse()
    .find(
      (e) =>
        eventTime(e) <= minute &&
        ["var_check", "var_confirmed", "var_overturned"].includes(e.type),
    );
  const shootout = matchEvents.some(
    (e) => e.type === "shootout_start" && eventTime(e) <= minute,
  );
  const penalties = [fixture.home, fixture.away].map(
    (team) =>
      matchEvents.filter(
        (e) =>
          e.type === "shootout_goal" &&
          e.team === team &&
          eventTime(e) <= minute,
      ).length,
  );
  const goals = matchEvents.filter(
    (e) => e.type === "goal" && eventTime(e) <= minute,
  );
  async function toggleSound() {
    if (audioBusy) return;
    setAudioBusy(true);
    setAudioError("");
    try {
      if (audio.current) {
        await audio.current.close();
        audio.current = null;
        setSound(false);
        localStorage.setItem("bm-match-sound", "off");
        return;
      }
      const player = new StadiumAudio(minute);
      audio.current = player;
      player.setVolume(volume);
      await player.setActive(visible && playing);
      setSound(true);
      localStorage.setItem("bm-match-sound", "on");
      void player.ready.then(() => setAudioLoaded(player.loaded));
    } catch {
      void audio.current?.close();
      audio.current = null;
      setSound(false);
      setAudioError("Não foi possível iniciar o áudio neste navegador.");
    } finally {
      setAudioBusy(false);
    }
  }
  const ended = minute >= duration;
  useEffect(() => {
    if (audio.current) {
      void audio.current.setActive(visible && playing);
    }
  }, [visible, playing, ended]);
  if (fixture.setup && replay.isPending)
    return (
      <div className="loading">
        {broadcast.error || "Preparando a transmissão…"}
      </div>
    );
  if (fixture.setup && replay.error)
    return (
      <div className="form-stack">
        <p className="error" role="alert">
          {replay.error.message}
        </p>
        <Button onClick={() => void replay.refetch()}>Tentar novamente</Button>
      </div>
    );
  return (
    <div className={live ? "broadcast-screen" : "recorded-match"}>
      {live && (
        <header className="broadcast-header">
          <span className="broadcast-brand">
            <FutbrxLogo />
          </span>
          <span>
            {fixture.competitionName || live?.competitionName || "BRASILEIRÃO"}{" "}
            · RODADA {fixture.round}
          </span>
          <span className="on-air">AO VIVO</span>
        </header>
      )}
      <div className="match-viewport" ref={container}>
        <div className="match-score">
          <span className="score-team" style={{ borderColor: h.color }}>
            {h.short}
          </span>
          <b>
            {goals.filter((e) => e.team === h.id).length}
            <i>–</i>
            {goals.filter((e) => e.team === a.id).length}
          </b>
          <span className="score-team" style={{ borderColor: a.color }}>
            {a.short}
          </span>
          <small>
            {live && entrance < 12
              ? "PRÉ-JOGO"
              : ended
                ? "FIM"
                : broadcastClock(
                    minute * 60,
                    matchEvents.find((e) => e.type === "second_half")?.second,
                  )}
          </small>
        </div>
        {shootout && (
          <div className="shootout-banner">
            <strong>DISPUTA DE PÊNALTIS</strong>
            <span>
              {h.short} {penalties[0]} × {penalties[1]} {a.short}
            </span>
          </div>
        )}
        {live && entrance < 12 && (
          <div className="entrance-card">
            <span>O ESPETÁCULO VAI COMEÇAR</span>
            <h1>
              {h.name}
              <small>vs</small>
              {a.name}
            </h1>
            <p>
              {entrance < 9
                ? "As equipes entram em campo"
                : "Tudo pronto para a saída de bola"}
            </p>
          </div>
        )}
        {(event?.type === "goal" || event?.type === "shootout_goal") &&
          minute - eventTime(event) < 0.22 && (
            <div className="goal-flash">
              GOOOL!<small>{event.text}</small>
            </div>
          )}
        {review &&
          (review.type === "var_check" || minute - eventTime(review) < 0.6) && (
            <div className="referee-review" role="status">
              <strong>
                {review.type === "var_check"
                  ? "VAR · CHECANDO GOL"
                  : review.type === "var_overturned"
                    ? "VAR · GOL ANULADO"
                    : "VAR · GOL CONFIRMADO"}
              </strong>
              <small>{review.text}</small>
            </div>
          )}
        {contextLost ? (
          <TacticalFallback
            replay={replay.data}
            minute={minute}
            home={h.color}
            away={a.color}
          />
        ) : (
          <WebGLBoundary
            key={contextGeneration}
            onFailure={rendererFailed}
            fallback={
              <TacticalFallback
                replay={replay.data}
                minute={minute}
                home={h.color}
                away={a.color}
              />
            }
          >
            <Canvas
              shadows={high}
              dpr={profile.dpr}
              frameloop={visible ? "always" : "never"}
              gl={{ antialias: high, powerPreference: "high-performance" }}
            >
              <WebGLContextMonitor onLost={()=>rendererFailed("O navegador perdeu o contexto WebGL.")}/>
              <color attach="background" args={["#080f1c"]} />
              <fog attach="fog" args={["#080f1c", 130, 280]} />
              <PerspectiveCamera makeDefault position={[5, 34, 57]} fov={48} />
              {cameraMode === "orbital" && (
                <OrbitControls
                  target={[0, 0, 0]}
                  minDistance={8}
                  maxDistance={150}
                  maxPolarAngle={Math.PI * 0.46}
                />
              )}
              <BroadcastCamera
                mode={cameraMode}
                timeline={timeline}
                replay={replayRef}
              />
              {validationReplay && inspectedPath.length > 1 && <Line points={inspectedPath} color="#ffdf4d" lineWidth={2} />}
              <ambientLight intensity={0.45} />
              <directionalLight
                position={[-40, 38, -35]}
                intensity={1.1}
                color="#e4edff"
              />
              <directionalLight
                position={[45, 35, -25]}
                intensity={0.7}
                color="#fff6e6"
              />
              <hemisphereLight args={["#b9cee9", "#243523", 0.7]} />
              <directionalLight
                position={[20, 45, 25]}
                intensity={1.85}
                castShadow={high}
                shadow-mapSize={[2048, 2048]}
                shadow-camera-left={-65}
                shadow-camera-right={65}
                shadow-camera-top={45}
                shadow-camera-bottom={-45}
              />
              <Pitch />
              <MatchdayDetails />
              <Arena
                quality={quality}
                home={h.color}
                away={a.color}
                timeline={timeline}
                replay={replayRef}
                mood={atmosphere}
                rate={broadcast.packet?.session.speed ?? live?.speed ?? speed}
              />
              <ArenaGoals mood={atmosphere} />
              <StadiumDetails
                quality={quality}
                home={h.short}
                away={a.short}
                homeGoals={goals.filter((e) => e.team === h.id).length}
                awayGoals={goals.filter((e) => e.team === a.id).length}
              />
              <PerformanceMeter
                key={quality}
                onSample={(fps, calls, triangles, cpuMs, gpuMs) => {
                  setPerformance({ fps, calls, triangles, cpuMs, gpuMs });
                  if(validationReplay && speed===1 && playing) {
                    const previous=benchmark.current[quality];
                    benchmark.current[quality]={samples:(previous?.samples??0)+1,sum:(previous?.sum??0)+fps,min:Math.min(previous?.min??fps,fps),max:Math.max(previous?.max??fps,fps)};
                  }
                  if (qualityMode === "auto" && fps < 24) setAutoQuality("low");
                }}
              />
              <SpatialListener audio={audio} />
              {Array.from({ length: 22 }, (_, i) => (
                <Footballer
                  key={i}
                  rigged={profile.rigged}
                  index={i % 11}
                  team={Math.floor(i / 11)}
                  color={
                    i % 11 === 0
                      ? kits.keepers[i < 11 ? 0 : 1]
                      : kits.outfield[i < 11 ? 0 : 1]
                  }
                  timeline={timeline}
                  replay={replayRef}
                />
              ))}
              <Referee
                color={kits.referee}
                timeline={timeline}
                replay={replayRef}
                rigged={profile.rigged}
              />
              <AssistantReferee
                side={-1}
                color={kits.referee}
                timeline={timeline}
                replay={replayRef}
              />
              <AssistantReferee
                side={1}
                color={kits.referee}
                timeline={timeline}
                replay={replayRef}
              />
              <MatchCeremony color={kits.outfield[0]} timeline={timeline} />
              <Ball fixture={fixture} timeline={timeline} replay={replayRef} />
              {!live && (
                <Clock
                  duration={duration}
                  timeline={timeline}
                  onTick={(n) => {
                    setMinute(n);
                  }}
                  speed={speed}
                />
              )}
            </Canvas>
          </WebGLBoundary>
        )}
      </div>
      {import.meta.env.DEV && <div className="engine-debug">
        <p role={contextLost ? "alert" : undefined}>Renderização: {contextLost ? `modo tático — métricas 3D invalidadas. ${renderError}` : "cena WebGL"}</p>
        <p>Página: {document.visibilityState} · Cena no campo de visão da página: {visible ? "sim" : "não"}</p>
        {contextLost && <button onClick={()=>{setContextGeneration(n=>n+1);setContextLost(false);setRenderError("");}}>Tentar reabrir a cena 3D</button>}
        {validationReplay && <div><button onClick={()=>{benchmark.current={};}}>Zerar medições</button>{(["low","balanced","high"] as Quality[]).map(key=>{const b=benchmark.current[key];return b ? <p key={key}>{qualityProfiles[key].label} em 1×: {b.samples} amostras · média {(b.sum/b.samples).toFixed(1)} FPS · mínimo {b.min} · máximo {b.max}</p> : null;})}</div>}
        <p>CPU (atualização e submissão): {performance.cpuMs.toFixed(1)} ms · GPU: {performance.gpuMs?.toFixed(1) ?? "temporizador indisponível"} {performance.gpuMs !== null ? "ms" : ""}</p>
        <p>Motor: {fixture.setup?.engineVersion} · Semente: {fixture.seed} · Frame médio: {performance.fps ? (1000/performance.fps).toFixed(1) : "—"} ms · Heap: {((globalThis.performance as Performance & {memory?:{usedJSHeapSize:number}}).memory?.usedJSHeapSize ?? 0) ? Math.round((globalThis.performance as Performance & {memory?:{usedJSHeapSize:number}}).memory!.usedJSHeapSize/1048576)+" MB" : "indisponível"}</p>
        {validationReplay && <div>
          <button onClick={()=>{setPlaying(false);const frames=validationReplay.frames;const index=frames.findIndex(f=>f.second>=minute*60);const frame=frames[Math.max(0,index-1)];if(frame){timeline.current.minute=frame.second/60;setMinute(frame.second/60);}}}>Quadro anterior</button>
          <button onClick={()=>{setPlaying(false);const frame=validationReplay.frames.find(f=>f.second>minute*60+.001);if(frame){timeline.current.minute=frame.second/60;setMinute(frame.second/60);}}}>Próximo quadro</button>
          <button onClick={()=>setCameraMode("contact")}>Câmera: bola e último contato</button>
          <p>Trajetória amarela: posições gravadas nos 6 segundos anteriores e seguintes.</p>
          {(()=>{const d=framePair(validationReplay.frames,minute*60)?.a.diagnostics;return <p>Estado: {d?.state ?? "não registrado"} · Reposição: {d?.restart ?? "nenhuma"} · Último evento da timeline: {d?.eventIndex == null ? "nenhum" : `#${d.eventIndex}`}</p>;})()}
          {(()=>{const frame=framePair(validationReplay.frames,minute*60)?.a;const contact=frame?.lastContact;const velocity=frame?.ballVelocity;return <p>
            Contato: {contact ? `#${contact.id ?? "—"} · ${frame?.players.find(p=>p.id===contact.playerId)?.name ?? contact.playerId} · ${clubs.find(c=>c.id===(contact.team===0 ? fixture.home : fixture.away))?.name ?? contact.team} · ${contact.type} · ${contact.origin} · (${contact.x.toFixed(2)}, ${contact.y.toFixed(2)}, ${contact.z.toFixed(2)}) · segundo ${contact.second}` : "sem registro neste quadro"} · Velocidade: {velocity ? Math.hypot(velocity.x,velocity.y,velocity.z).toFixed(2)+" m/s" : "sem registro físico neste quadro"}
          </p>;})()}
        </div>}
        {validationReplay && <label>Inspecionar evento<select defaultValue="" onChange={e=>{
          const event=validationReplay.events[Number(e.target.value)];
          if(!event)return;
          const at=Math.max(0,(event.second??event.minute*60)-5)/60;
          timeline.current.minute=at;setMinute(at);setPlaying(true);
        }}><option value="" disabled>Escolha um lance (início 5 s antes)</option>{validationReplay.events.map((event,index)=>
          ["goal","corner","goal_kick","throw_in","rebound","offside","restart_taken","header","aerial_challenge","keeper_punch","keeper_catch"].includes(event.type) ?
          <option key={index} value={index}>#{index} · {event.minute}′ · {event.type} · jogador {event.player} · {event.text}</option> : null)}</select></label>}
      </div>}
      {!live && (
        <label className="replay-seek">
          Linha do tempo
          <input
            type="range"
            min={0}
            max={duration}
            step={1 / 60}
            value={minute}
            onChange={(e) => {
              audio.current?.seek(Number(e.target.value));
              timeline.current.minute = Number(e.target.value);
              setMinute(Number(e.target.value));
            }}
          />
          <span>
            {Math.floor(minute)}′ / {Math.ceil(duration)}′
          </span>
        </label>
      )}
      <div className="match-controls">
        <Button
          variant="outline"
          disabled={!!live && ended}
          onClick={() => {
            if (minute >= duration) {
              audio.current?.seek(0);
              timeline.current.minute = 0;
              setMinute(0);
              setPlaying(true);
            } else setPlaying(!playing);
          }}
        >
          {playing && minute < duration ? (
            <Pause size={15} />
          ) : (
            <Play size={15} />
          )}{" "}
          {playing && minute < duration ? "Pausar" : "Reproduzir"}
        </Button>
        {!live && (
          <Button
            variant="ghost"
            onClick={() => {
              audio.current?.seek(0);
              timeline.current.minute = 0;
              setMinute(0);
              setPlaying(true);
            }}
          >
            <RotateCcw size={15} />
            Reiniciar
          </Button>
        )}
        {!live && (
          <select
            aria-label="Velocidade da reprodução"
            value={speed}
            onChange={(e) => setSpeed(Number(e.target.value))}
          >
            <option value={1}>Tempo real · 1×</option>
            <option value={10}>10×</option>
            <option value={30}>30×</option>
            <option value={60}>60×</option>
          </select>
        )}
        <select
          aria-label="Câmera da partida"
          value={cameraMode}
          onChange={(e) => setCameraMode(e.target.value)}
        >
          <option value="orbital">Câmera livre</option>
          <option value="tv">Transmissão</option>
          <option value="tactical">Tática</option>
          <option value="goal">Atrás do gol</option>
          <option value="pitch">Perto do campo</option>{validationReplay && <option value="contact">Bola e último contato</option>}
        </select>
        <select
          aria-label="Qualidade gráfica"
          value={qualityMode}
          onChange={(e) => setQualityMode(e.target.value as Quality | "auto")}
        >
          <option value="auto">Automática</option>
          <option value="low">Leve</option>
          <option value="balanced">Equilibrada</option>
          <option value="high">Alta</option>
        </select>
        <Button
          disabled={audioBusy}
          aria-pressed={sound}
          aria-label={
            sound ? "Desligar áudio do estádio" : "Ligar áudio do estádio"
          }
          variant="ghost"
          onClick={() => void toggleSound()}
        >
          {sound ? <Volume2 size={17} /> : <VolumeX size={17} />}
          {sound ? "Som ligado" : "Ativar som"}
        </Button>
        {sound && (
          <meter
            className="stadium-audio-meter"
            aria-label="Nível de áudio do estádio"
            min={0}
            max={1}
            value={audioLevel}
          />
        )}
      </div>
      {live && broadcast.packet?.session.controlsEnabled && (
        <div className="live-coach-bar">
          <label>
            Velocidade da simulação{" "}
            <select
              aria-label="Multiplicador da partida"
              value={broadcast.packet.session.speed}
              disabled={broadcast.controlBusy}
              onChange={(e) =>
                void broadcast.control({
                  type: "speed",
                  speed: Number(e.target.value),
                })
              }
            >
              {[1, 2, 4, 6, 8, 12, 24, 48, 96].map((n) => (
                <option key={n} value={n}>
                  {n}×
                </option>
              ))}
            </select>
          </label>
          <Button
            variant="outline"
            disabled={ended}
            onClick={() => {
              setPlaying(false);
              setManagerOpen(true);
            }}
          >
            Banco e táticas
          </Button>
          <Button
            variant="outline"
            disabled={ended || broadcast.controlBusy}
            onClick={() =>
              void broadcast.control({
                type:
                  broadcast.packet?.session.pauseAfterSecond != null
                    ? "cancel_stoppage"
                    : "arm_stoppage",
              })
            }
          >
            {broadcast.packet.session.pauseAfterSecond != null
              ? "Cancelar pausa na bola parada"
              : "Pausar na próxima bola parada"}
          </Button>
          {broadcast.controlError && (
            <p role="alert">{broadcast.controlError}</p>
          )}
        </div>
      )}
      {live && managerOpen && broadcast.packet && (
        <MatchManagement
          packet={broadcast.packet}
          home={h}
          away={a}
          careerId={careerId}
          busy={broadcast.controlBusy}
          error={broadcast.controlError}
          control={broadcast.control}
          onClose={() => {
            setManagerOpen(false);
            setPlaying(true);
          }}
        />
      )}
      <label className="replay-seek">
        Volume
        <input
          aria-label="Volume do estádio"
          type="range"
          min={0}
          max={1}
          step={0.05}
          value={volume}
          onChange={(e) => {
            const v = Number(e.target.value);
            setVolume(v);
            audio.current?.setVolume(v);
          }}
        />
      </label>
      <details className="stadium-audio-options">
        <summary>
          Áudio do estádio ·{" "}
          {sound
            ? audioLoaded === 5
              ? "Ambiente completo"
              : audioLoaded >= 0
                ? "Ambiente simplificado"
                : "Carregando sons…"
            : "Desligado"}
        </summary>
        <p>
          Apitos, impactos e reações da torcida acompanham os lances. Ajuste o
          volume antes de testar.
        </p>
        <div>
          {[
            ["whistle", "Testar apito"],
            ["kick", "Testar chute"],
            ["near-miss", "Testar quase gol"],
            ["goal", "Testar torcida no gol"],
          ].map(([type, label]) => (
            <Button
              key={type}
              variant="outline"
              disabled={audioBusy}
              onClick={() =>
                void previewSound(
                  type as "whistle" | "kick" | "near-miss" | "goal",
                )
              }
            >
              {label}
            </Button>
          ))}
        </div>
      </details>
      {audioError && (
        <p role="alert" className="error">
          {audioError}
        </p>
      )}
      {!live && (
        <p className="replay-note">
          Qualidade {profile.label.toLowerCase()} ·{" "}
          {performance.fps
            ? `${performance.fps} FPS observados · ${performance.calls} chamadas de desenho · ${performance.triangles.toLocaleString()} triângulos`
            : "Medindo desempenho…"}
          .{" "}
          {qualityMode === "auto"
            ? "A qualidade pode ser reduzida durante a reprodução."
            : "Perfil manual."}
        </p>
      )}
      {broadcast.error && (
        <p role="alert" className="error">
          {broadcast.error} A partida será retomada deste ponto.
        </p>
      )}
      <div className="match-event" role="status">
        {event
          ? `${eventClock(event)} · ${event.text}`
          : "0′ · A bola vai rolar."}
        {minute >= duration &&
          ` Resultado final: ${h.short} ${replay.data?.homeGoals ?? fixture.homeGoals} × ${replay.data?.awayGoals ?? fixture.awayGoals} ${a.short}.${shootout ? ` Pênaltis: ${penalties[0]} × ${penalties[1]}.` : ""}`}
      </div>
      <details>
        <summary>
          Lances e substituições ·{" "}
          {broadcastClock(Math.max(0, minute) * 60, secondHalfAt)}
        </summary>
        <div style={{ maxHeight: 180, overflowY: "auto" }}>
          {matchEvents
            .filter(
              (e) =>
                eventTime(e) <= minute &&
                [
                  "goal",
                  "var_check",
                  "var_confirmed",
                  "var_overturned",
                  "advantage",
                  "advantage_recalled",
                  "handball",
                  "tackle",
                  "yellow_card",
                  "second_yellow",
                  "red_card",
                  "injury",
                  "substitution",
                  "emergency_keeper",
                  "second_half",
                ].includes(e.type),
            )
            .map((e, i) => (
              <p key={i}>
                {eventClock(e)} · {e.text}
              </p>
            ))}
        </div>
      </details>
      {!live && (
        <p className="replay-note">
          {replay.data
            ? `Motor ${replay.data.engineVersion} · ${replay.data.frames.length} frames do servidor · ${replay.data.setup.home.formation} × ${replay.data.setup.away.formation}. Posições interpoladas; modelo FBX fornecido com animações de futebol em todos os perfis. 1× acompanha segundos de jogo; 30× é a velocidade inicial.`
            : "Partida legada: reprodução ilustrativa dos eventos. Novas partidas usam posições calculadas no servidor."}
        </p>
      )}
      {live && (
        <div className="broadcast-bottom">
          <span>
            {ended
              ? "APITO FINAL"
              : playing
                ? `TRANSMISSÃO CONTÍNUA · ${broadcast.packet?.session.speed ?? live?.speed ?? 12}×`
                : "TRANSMISSÃO PAUSADA"}
          </span>
          <p>
            {ended
              ? `${h.name} ${fixture.homeGoals ?? replay.data?.homeGoals} × ${fixture.awayGoals ?? replay.data?.awayGoals} ${a.name}`
              : "Cada lance conta. Sua torcida está com você."}
            {ended && shootout && (
              <strong>
                {" "}
                · Pênaltis {penalties[0]} × {penalties[1]} ·{" "}
                {penalties[0] > penalties[1] ? h.name : a.name} classificado
              </strong>
            )}
          </p>
          {ended && broadcast.packet?.complete && (
            <Button
              disabled={finishing}
              onClick={async () => {
                setFinishing(true);
                setFinishError("");
                try {
                  await onComplete?.();
                } catch (e) {
                  setFinishError(
                    e instanceof Error ? e.message : "Tente novamente.",
                  );
                  setFinishing(false);
                }
              }}
            >
              {finishing ? "Salvando resultado…" : "Voltar ao vestiário"}
            </Button>
          )}
          {finishError && (
            <p className="error" role="alert">
              {finishError}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function SpatialListener({
  audio,
}: {
  audio: React.RefObject<StadiumAudio | null>;
}) {
  const direction = useMemo(() => new THREE.Vector3(), []);
  useFrame(({ camera }) => {
    if (!audio.current) return;
    camera.getWorldDirection(direction);
    audio.current.listener(
      camera.position.x,
      camera.position.y,
      camera.position.z,
      direction.x,
      direction.y,
      direction.z,
      camera.up.x,
      camera.up.y,
      camera.up.z,
    );
  });
  return null;
}



