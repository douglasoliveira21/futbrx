import { mocapTimings } from "./mocapTimings";
import { matchKitColors } from "./kitColors";
import type { Club } from "../../services/api";
import React, { Suspense, useEffect, useRef, useState } from "react";
import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { OrbitControls, PerspectiveCamera, Html } from "@react-three/drei";
import FootballerAvatar, { type AvatarPose } from "./FootballerAvatar";
import { footballAnimations, contactOffsets } from "./presentation";
import { Button } from "../../components/ui/button";

function CameraView({ portrait, back }: { portrait: boolean; back: boolean }) {
  const { camera } = useThree();
  useEffect(() => {
    camera.position.set(
      ...((portrait
        ? [0, 1.65, 0.78]
        : back
          ? [-2, 1.25, -3.4]
          : [2, 1.25, 3.4]) as [number, number, number]),
    );
    camera.lookAt(0, portrait ? 1.65 : 0.95, 0);
  }, [camera, portrait, back]);
  return null;
}
function Actor({
  action,
  color,
  skin,
  hair,
  number,
  keeper,
  playing,
  reset,
  timeline,
  raw,
  onTick,
}: {
  raw: boolean;
  action: string;
  color: string;
  skin: number;
  hair: number;
  number: number;
  keeper: boolean;
  playing: boolean;
  reset: number;
  timeline: React.RefObject<{
    minute: number;
    playing: boolean;
    visible: boolean;
  }>;
  onTick: (second: number, duration: number) => void;
}) {
  const pose = useRef<AvatarPose>({ id: 7, action });
  const last = useRef(0);
  const clipDurations: Record<string, number> = {
    tackle: 2.1,
    fall: 3,
    stumble: 1.5,
    shield: 1.5,
    challenge: 1.2,
    air_duel: 1.5,
    pass: 0.9,
    shot: 1.1,
    cross: 1.15,
    header: 1.2,
    catch: 1.25,
    dive_left: 1.6,
    dive_right: 1.6,
    celebrate: 1.8,
    dribble: 0.9,
    keeper_ready: 1.6,
  };
  const timing = mocapTimings[action];
  const duration = timing
    ? raw
      ? timing.rawDuration
      : timing.duration
    : (clipDurations[action] ?? 2);
  useEffect(() => {
    timeline.current.minute = 0;
    last.current = 0;
  }, [action, reset, raw, timeline]);
  useFrame((_, delta) => {
    timeline.current.playing = playing;
    if (playing) timeline.current.minute += Math.min(delta, 0.1) / 60;
    if (timeline.current.minute * 60 > duration + 0.75)
      timeline.current.minute = 0;
    pose.current = {
      id: 7,
      action,
      actionSecond:
        raw && timing ? timing.rawContact : (contactOffsets[action] ?? 0),
      contactOffset: raw && timing ? timing.rawContact : undefined,
      exactTime: true,
      card: action.includes("card")
        ? action === "yellow_card" ||
          (action === "second_yellow_card" &&
            timeline.current.minute * 60 < (raw ? 2.73 : 2.55))
          ? "yellow"
          : "red"
        : undefined,
      skinIndex: skin,
      hairIndex: hair,
      shirtNumber: number,
      goalkeeper:
        keeper ||
        [
          "keeper_ready",
          "catch",
          "dive_left",
          "dive_right",
          "dive_low",
        ].includes(action),
    };
    if (Math.abs(timeline.current.minute * 60 - last.current) > 0.08) {
      last.current = timeline.current.minute * 60;
      onTick(last.current, duration + 0.75);
    }
  });
  const keeperRole =
    keeper ||
    ["keeper_ready", "catch", "dive_left", "dive_right", "dive_low"].includes(
      action,
    );
  return (
    <FootballerAvatar
      assetPath={raw ? "/assets/players/user-mocap-preview.glb" : undefined}
      color={keeperRole ? matchKitColors(color, "#e7ebf0").keepers[0] : color}
      timeline={timeline}
      pose={pose}
    />
  );
}
class PreviewBoundary extends React.Component<
  { children: React.ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <p role="alert" className="error">
        Não foi possível iniciar o estúdio 3D neste dispositivo.
      </p>
    ) : (
      this.props.children
    );
  }
}
export default function PlayerStudio({ clubs = [] }: { clubs?: Club[] }) {
  const [raw, setRaw] = useState(false);
  const [action, setAction] = useState("idle"),
    [color, setColor] = useState("#176b3a"),
    [skin, setSkin] = useState(1),
    [hair, setHair] = useState(0),
    [number, setNumber] = useState(9),
    [keeper, setKeeper] = useState(false),
    [portrait, setPortrait] = useState(false),
    [back, setBack] = useState(false),
    [playing, setPlaying] = useState(true),
    [reset, setReset] = useState(0),
    [second, setSecond] = useState(0),
    [duration, setDuration] = useState(2.75);
  const timeline = useRef({ minute: 0, playing: true, visible: true });
  return (
    <div className="form-stack">
      <section className="panel" style={{ padding: 24 }}>
        <h2>Jogador fornecido · uniformes e movimentos</h2>
        <p>
          Modelo FBX fornecido por você, com textura original, cores por clube e
          uniforme de goleiro. Selecione um movimento para examiná-lo em tempo
          real, pausar ou avançar quadro a quadro.
        </p>
        <div
          style={{
            height: "min(62vh,620px)",
            minHeight: 380,
            borderRadius: 14,
            overflow: "hidden",
            background: "#14232d",
          }}
        >
          <PreviewBoundary>
            <Canvas shadows dpr={[1, 1.6]}>
              <color attach="background" args={["#14232d"]} />
              <PerspectiveCamera
                makeDefault
                fov={36}
                position={[2, 1.25, 3.4]}
              />
              <CameraView portrait={portrait} back={back} />
              <OrbitControls
                target={[0, portrait ? 1.7 : 0.95, 0]}
                minDistance={0.25}
                maxDistance={6}
                maxPolarAngle={Math.PI * 0.55}
              />
              <ambientLight intensity={1.6} />
              <directionalLight
                position={[3, 5, 4]}
                intensity={3.2}
                castShadow
                shadow-mapSize={[1024, 1024]}
              />
              <directionalLight
                position={[-3, 2, -2]}
                intensity={1.4}
                color="#a1c7e4"
              />
              <mesh rotation={[-Math.PI / 2, 0, 0]} receiveShadow>
                <circleGeometry args={[3, 64]} />
                <meshStandardMaterial color="#21393d" roughness={0.9} />
              </mesh>
              <Suspense fallback={<Html center>Carregando jogador…</Html>}>
                <Actor
                  raw={raw}
                  action={action}
                  color={color}
                  skin={skin}
                  hair={hair}
                  number={number}
                  keeper={keeper}
                  playing={playing}
                  reset={reset}
                  timeline={timeline}
                  onTick={(s, d) => {
                    setSecond(s);
                    setDuration(d);
                  }}
                />
              </Suspense>
            </Canvas>
          </PreviewBoundary>
        </div>
        <div className="toolbar" style={{ marginTop: 16 }}>
          <Button onClick={() => setPlaying((p) => !p)}>
            {playing ? "Pausar movimento" : "Reproduzir movimento"}
          </Button>
          <Button variant="outline" onClick={() => setReset((r) => r + 1)}>
            Reiniciar movimento
          </Button>
          <Button
            variant="outline"
            onClick={() => {
              setPortrait(false);
              setBack((b) => !b);
            }}
          >
            {back ? "Ver frente" : "Ver costas"}
          </Button>
          <Button
            variant="outline"
            onClick={() => {
              setBack(false);
              setPortrait((p) => !p);
            }}
          >
            {portrait ? "Ver corpo inteiro" : "Examinar rosto"}
          </Button>
        </div>
        <label>
          <input
            type="checkbox"
            checked={raw}
            onChange={(e) => {
              setRaw(e.target.checked);
              setSecond(0);
            }}
          />{" "}
          Examinar captura completa, incluindo espera inicial
        </label>
        {mocapTimings[action] && (
          <p>
            {mocapTimings[action].file} · contato{" "}
            {(raw
              ? mocapTimings[action].rawContact
              : mocapTimings[action].contact
            ).toFixed(2)}{" "}
            s
          </p>
        )}
        <label className="replay-seek">
          Quadro da animação
          <input
            aria-label="Quadro da animação"
            type="range"
            min={0}
            max={duration}
            step={1 / 30}
            value={second}
            onChange={(e) => {
              setPlaying(false);
              timeline.current.playing = false;
              timeline.current.minute = Number(e.target.value) / 60;
              setSecond(Number(e.target.value));
            }}
          />
          <span>{second.toFixed(2)} s</span>
        </label>
        <div className="toolbar">
          <label>
            Uniforme
            <input
              aria-label="Cor do uniforme"
              type="color"
              value={color}
              onChange={(e) => setColor(e.target.value)}
            />
          </label>
          <label>
            Clube
            <select
              aria-label="Clube do uniforme"
              value={clubs.find((c) => c.color === color)?.id ?? "custom"}
              onChange={(e) => {
                const team = clubs.find((c) => c.id === e.target.value);
                if (team) setColor(team.color);
              }}
            >
              <option value="custom">Cor personalizada</option>
              {clubs.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Número
            <input
              aria-label="Número da camisa"
              type="number"
              min={1}
              max={99}
              value={number}
              onChange={(e) =>
                setNumber(Math.max(1, Math.min(99, Number(e.target.value))))
              }
            />
          </label>
          <label>
            <input
              type="checkbox"
              checked={keeper}
              onChange={(e) => setKeeper(e.target.checked)}
            />{" "}
            Uniforme de goleiro e luvas
          </label>
        </div>
      </section>
      <section className="panel" style={{ padding: 24 }}>
        <h2>Biblioteca de futebol</h2>
        <div className="training-week">
          {footballAnimations.map((a) => (
            <Button
              key={a.action}
              variant={a.action === action ? "default" : "outline"}
              onClick={() => {
                setAction(a.action);
                setPortrait(false);
                setPlaying(true);
                setSecond(0);
              }}
            >
              {a.label}
            </Button>
          ))}
        </div>
        <p>
          Os mesmos modelos e clipes são usados nas partidas. Novos replays
          distinguem condução, passe, cruzamento, cabeceio, chute, encaixe e
          defesa lateral, carrinho, queda, proteção de bola e disputa aérea.
          Partidas antigas mantêm o motor original e recebem adaptação visual
          compatível.
        </p>
      </section>
    </div>
  );
}
