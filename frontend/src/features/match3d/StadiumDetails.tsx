import { useEffect, useMemo, useRef } from "react";
import { addAfterEffect, useFrame, useThree } from "@react-three/fiber";
import * as THREE from "three";
import type { Quality } from "./presentation";

function Scoreboard({
  home,
  away,
  homeGoals,
  awayGoals,
}: {
  home: string;
  away: string;
  homeGoals: number;
  awayGoals: number;
}) {
  const texture = useMemo(() => {
    const canvas = document.createElement("canvas");
    canvas.width = 1024;
    canvas.height = 256;
    const ctx = canvas.getContext("2d")!;
    ctx.fillStyle = "#071d24";
    ctx.fillRect(0, 0, 1024, 256);
    ctx.fillStyle = "#9aebc6";
    ctx.font = "bold 78px Arial";
    ctx.textAlign = "center";
    ctx.fillText(`${home}   ${homeGoals} : ${awayGoals}   ${away}`, 512, 120);
    ctx.fillStyle = "#9aacb6";
    ctx.font = "32px Arial";
    ctx.fillText("futbrx", 512, 195);
    return new THREE.CanvasTexture(canvas);
  }, [home, away, homeGoals, awayGoals]);
  useEffect(() => () => texture.dispose(), [texture]);
  return (
    <group position={[-67, 13, 0]} rotation={[0, Math.PI / 2, 0]}>
      <mesh>
        <boxGeometry args={[20, 5.8, 0.6]} />
        <meshStandardMaterial color="#132735" />
      </mesh>
      <mesh position={[0, 0, 0.32]}>
        <planeGeometry args={[19, 4.8]} />
        <meshBasicMaterial map={texture} />
      </mesh>
      {[-8, 8].map((x) => (
        <mesh key={x} position={[x, -7, 0]}>
          <boxGeometry args={[0.5, 14, 0.5]} />
          <meshStandardMaterial color="#536370" />
        </mesh>
      ))}
    </group>
  );
}
export default function StadiumDetails({
  quality,
  home,
  away,
  homeGoals,
  awayGoals,
}: {
  quality: Quality;
  home: string;
  away: string;
  homeGoals: number;
  awayGoals: number;
}) {
  return (
    <>
      <Scoreboard
        home={home}
        away={away}
        homeGoals={homeGoals}
        awayGoals={awayGoals}
      />
      {[-1, 1].map((side) => (
        <group key={side} position={[side * 12, 0, 36]}>
          <mesh position={[0, 1.4, 0]}>
            <boxGeometry args={[9, 2.8, 2]} />
            <meshStandardMaterial
              color="#b5d2db"
              transparent
              opacity={0.18}
              depthWrite={false}
            />
          </mesh>
          <mesh position={[0, 2.8, 0]}>
            <boxGeometry args={[9.4, 0.25, 2.5]} />
            <meshStandardMaterial color="#183c42" />
          </mesh>
          {Array.from({ length: 7 }, (_, i) => (
            <mesh key={i} position={[-3.5 + i * 1.15, 0.6, 0]}>
              <boxGeometry args={[0.85, 0.4, 0.8]} />
              <meshStandardMaterial color="#234f63" />
            </mesh>
          ))}
        </group>
      ))}
      {Array.from({ length: 12 }, (_, i) => (
        <mesh key={i} position={[-49 + i * 9, 0.75, -35.5]}>
          <boxGeometry args={[8, 1.4, 0.25]} />
          <meshStandardMaterial
            color={i % 2 ? "#163e38" : "#212e4c"}
            emissive={i % 2 ? "#114d33" : "#1b3a69"}
            emissiveIntensity={0.4}
          />
        </mesh>
      ))}
    </>
  );
}
export function PerformanceMeter({
  onSample,
}: {
  onSample: (fps: number, calls: number, triangles: number, cpuMs: number, gpuMs: number | null) => void;
}) {
  const gl = useThree(state => state.gl);
  const timings = useRef({ start: 0, cpu: 0, count: 0, gpu: 0, gpuCount: 0 });
  const timer = useRef<{ context: WebGL2RenderingContext; extension: { TIME_ELAPSED_EXT: number; GPU_DISJOINT_EXT: number }; active: WebGLQuery | null; pending: WebGLQuery[] } | null>(null);
  useEffect(() => {
    const context = gl.getContext();
    if (typeof WebGL2RenderingContext !== "undefined" && context instanceof WebGL2RenderingContext) {
      const extension = context.getExtension("EXT_disjoint_timer_query_webgl2");
      if (extension) timer.current = { context, extension, active: null, pending: [] };
    }
    const unsubscribe = addAfterEffect(() => {
      const t = timings.current;
      if (!t.start) return;
      t.cpu += performance.now() - t.start; t.count++; t.start = 0;
      const query = timer.current;
      if (query?.active) {
        query.context.endQuery(query.extension.TIME_ELAPSED_EXT);
        query.pending.push(query.active); query.active = null;
      }
    });
    return () => {
      unsubscribe();
      const query = timer.current;
      if (query) {
        if (query.active) { query.context.endQuery(query.extension.TIME_ELAPSED_EXT); query.context.deleteQuery(query.active); }
        query.pending.forEach(q => query.context.deleteQuery(q));
      }
      timer.current = null;
    };
  }, [gl]);
  useFrame(() => {
    const query = timer.current;
    if (query) {
      const { context, extension } = query;
      const disjoint = context.getParameter(extension.GPU_DISJOINT_EXT);
      while (query.pending.length && context.getQueryParameter(query.pending[0], context.QUERY_RESULT_AVAILABLE)) {
        const done = query.pending.shift()!;
        if (!disjoint) { timings.current.gpu += context.getQueryParameter(done, context.QUERY_RESULT) / 1e6; timings.current.gpuCount++; }
        context.deleteQuery(done);
      }
      if (!query.active && query.pending.length < 4) {
        query.active = context.createQuery();
        if (query.active) context.beginQuery(extension.TIME_ELAPSED_EXT, query.active);
      }
    }
    timings.current.start = performance.now();
  }, -1000);
  const sample = useRef({ seconds: 0, frames: 0 });
  useFrame(({ gl }, delta) => {
    if (delta > 2) {
      sample.current = { seconds: 0, frames: 0 };
      return;
    }
    sample.current.seconds += delta;
    sample.current.frames++;
    if (sample.current.seconds >= 2) {
      onSample(
        Math.round(sample.current.frames / sample.current.seconds),
        gl.info.render.calls,
        gl.info.render.triangles,
        timings.current.count ? timings.current.cpu / timings.current.count : 0,
        timings.current.gpuCount ? timings.current.gpu / timings.current.gpuCount : null,
      );
      timings.current.cpu = timings.current.count = timings.current.gpu = timings.current.gpuCount = 0;
      sample.current = { seconds: 0, frames: 0 };
    }
  });
  return null;
}
