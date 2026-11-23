import { createKeeperGloves } from "./keeperGloves";
import React, { useEffect, useMemo } from "react";
import { useFrame } from "@react-three/fiber";
import { useGLTF } from "@react-three/drei";
import * as THREE from "three";
import { clone } from "three/addons/utils/SkeletonUtils.js";
import {
  animationForAction,
  contactOffsets,
  isOneShot,
  type PresentationPose,
} from "./presentation";
export type AvatarPose = PresentationPose;
export default function FootballerAvatar({
  color,
  timeline,
  pose,
  assetPath = "/assets/players/user-footballer-mocap.glb",
}: {
  color: string;
  assetPath?: string;
  timeline: React.RefObject<{
    minute: number;
    playing: boolean;
    visible: boolean;
    animationScale?: number;
    entrance?: number;
  }>;
  pose: React.RefObject<AvatarPose>;
}) {
  const gltf = useGLTF(assetPath);
  const model = useMemo(() => {
    const root = clone(gltf.scene),
      kitColor = new THREE.Color(color),
      skinColor = new THREE.Color("#bd8765");
    const skeletons = new Set<THREE.Skeleton>(),
      materials = new Map<string, THREE.MeshStandardMaterial>();
    root.traverse((o) => {
      if (!(o instanceof THREE.Mesh)) return;
      o.castShadow = true;
      if (o instanceof THREE.SkinnedMesh) {
        skeletons.add(o.skeleton);
        o.frustumCulled = false;
      }
      if (o.geometry.getAttribute("uv1"))
        o.geometry.setAttribute("kitWeights", o.geometry.getAttribute("uv1"));
      if (o.geometry.getAttribute("_kitzone"))
        o.geometry.setAttribute("kitZone", o.geometry.getAttribute("_kitzone"));
      const copy = (m: THREE.Material) => {
        if (!materials.has(m.uuid)) {
          const result = m.clone() as THREE.MeshStandardMaterial;
          if (result.name === "Imported_Football_Kit") {
            result.onBeforeCompile = (shader) => {
              shader.uniforms.teamColor = { value: kitColor };
              shader.vertexShader =
                "attribute float kitZone; varying float vKitZone;\n" +
                shader.vertexShader.replace(
                  "#include <begin_vertex>",
                  "#include <begin_vertex>\nvKitZone=kitZone;",
                );
              shader.fragmentShader =
                "uniform vec3 teamColor; varying float vKitZone;\n" +
                shader.fragmentShader.replace(
                  "#include <color_fragment>",
                  `#include <color_fragment>
                float strongest=max(diffuseColor.r,max(diffuseColor.g,diffuseColor.b));
                float weakest=min(diffuseColor.r,min(diffuseColor.g,diffuseColor.b));
                float blue=smoothstep(.08,.28,(diffuseColor.b-max(diffuseColor.r,diffuseColor.g)*1.12)/max(.002,diffuseColor.b));
                float neutral=1.0-smoothstep(.12,.28,(strongest-weakest)/max(.002,strongest));
                float shorts=clamp(vKitZone,0.0,1.0);
                float mask=max(blue,shorts*neutral);
                float lighting=clamp(dot(diffuseColor.rgb,vec3(.2126,.7152,.0722))*4.0,.06,1.4);
                vec3 kit=mix(teamColor,teamColor*.32+vec3(.015),shorts);
                diffuseColor.rgb=mix(diffuseColor.rgb,kit*lighting,mask);
              `,
                );
            };
            result.customProgramCacheKey = () => "user-football-kit-v1";
          }
          if (result.name === "Football_Kit") {
            result.onBeforeCompile = (shader) => {
              shader.uniforms.teamColor = { value: kitColor };
              shader.uniforms.skinColor = { value: skinColor };
              shader.vertexShader =
                "attribute vec2 kitWeights; varying vec2 vKitWeights; varying vec3 vFabricPoint;\n" +
                shader.vertexShader.replace(
                  "#include <color_vertex>",
                  "#include <color_vertex>\nvKitWeights=kitWeights;vFabricPoint=position;",
                );
              shader.fragmentShader =
                "uniform vec3 teamColor; uniform vec3 skinColor; varying vec2 vKitWeights; varying vec3 vFabricPoint;\n" +
                shader.fragmentShader.replace(
                  "#include <color_fragment>",
                  "#include <color_fragment>\nfloat weave=sin(vFabricPoint.x*700.0)*sin(vFabricPoint.y*700.0);diffuseColor.rgb*=mix(vec3(1.0),teamColor*(.98+weave*.018),vKitWeights.x);diffuseColor.rgb*=mix(vec3(1.0),skinColor,vKitWeights.y);",
                );
            };
            result.customProgramCacheKey = () => "football-human-v3";
          }
          materials.set(m.uuid, result);
        }
        return materials.get(m.uuid)!;
      };
      o.material = Array.isArray(o.material)
        ? o.material.map(copy)
        : copy(o.material);
    });
    root.updateMatrixWorld(true);
    const fittedGloves = createKeeperGloves(root);
    const spine = (root.getObjectByName("Spine02") ??
      root.getObjectByName("spine_03"))!;
    const imported = root.getObjectByName("Imported_Player")?.userData;
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = 512;
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    const numberMaterial = new THREE.MeshStandardMaterial({
      name: "JerseyNumber",
      map: texture,
      transparent: true,
      alphaTest: 0.2,
      roughness: 0.9,
      depthWrite: false,
    });
    const numberGeometry = new THREE.PlaneGeometry(1, 1);
    for (const [x, y, z, w, h, angle] of [
      [0, 1.31, imported?.jerseyBackZ ?? -0.113, 0.34, 0.4, Math.PI],
    ]) {
      const number = new THREE.Mesh(numberGeometry, numberMaterial);
      number.name = "JerseyNumber";
      const matrix = new THREE.Matrix4().compose(
        new THREE.Vector3(x, y, z),
        new THREE.Quaternion().setFromAxisAngle(
          new THREE.Vector3(0, 1, 0),
          angle,
        ),
        new THREE.Vector3(w, h, 1),
      );
      number.matrixAutoUpdate = false;
      number.matrix.copy(
        new THREE.Matrix4().copy(spine.matrixWorld).invert().multiply(matrix),
      );
      spine.add(number);
    }
    const hand =
      root.getObjectByName("L_Hand") ?? root.getObjectByName("hand_l");
    const card = new THREE.Mesh(
      new THREE.BoxGeometry(0.075, 0.12, 0.008),
      new THREE.MeshStandardMaterial({ color: "#ffdf00", roughness: 0.5 }),
    );
    card.name = "RefereeCard";
    card.visible = false;
    hand?.add(card);
    const flag = new THREE.Group();
    flag.visible = false;
    flag.name = "AssistantFlag";
    const pole = new THREE.Mesh(
      new THREE.CylinderGeometry(0.007, 0.007, 0.55, 6),
      new THREE.MeshStandardMaterial({ color: "#111827" }),
    );
    pole.position.y = -0.2;
    flag.add(pole);
    for (let row = 0; row < 2; row++)
      for (let col = 0; col < 2; col++) {
        const square = new THREE.Mesh(
          new THREE.PlaneGeometry(0.13, 0.13),
          new THREE.MeshStandardMaterial({
            color: (row + col) % 2 ? "#ff7400" : "#ffe11a",
            side: THREE.DoubleSide,
          }),
        );
        square.position.set(0.065 + col * 0.13, -0.27 + row * 0.13, 0);
        flag.add(square);
      }
    hand?.add(flag);
    const mixer = new THREE.AnimationMixer(root),
      actions = gltf.animations.map((c) => {
        const a = mixer.clipAction(c);
        a.play();
        a.setEffectiveWeight(c.name === "Football_Idle" ? 1 : 0);
        return a;
      });
    mixer.update(0);
    return {
      root,
      fittedGloves,
      card,
      flag,
      kitColor,
      skinColor,
      skeletons,
      materials: [...materials.values()],
      numberMaterial,
      numberGeometry,
      texture,
      canvas,
      mixer,
      actions,
      lastIdentity: "",
      hair: ["Hair_Crop", "Hair_Sweep", "Hair_Curls"].map((n) =>
        root.getObjectByName(n),
      ),
      gloves: ["KeeperGlove_l", "KeeperGlove_r"].map((n) =>
        root.getObjectByName(n),
      ),
    };
  }, [gltf]);
  useEffect(
    () => () => {
      model.mixer.stopAllAction();
      model.mixer.uncacheRoot(model.root);
      model.materials.forEach((m) => m.dispose());
      model.skeletons.forEach((s) => s.dispose());
      model.card.geometry.dispose();
      (model.card.material as THREE.Material).dispose();
      model.flag.traverse((o) => {
        if (o instanceof THREE.Mesh) {
          o.geometry.dispose();
          (o.material as THREE.Material).dispose();
        }
      });
      model.fittedGloves.forEach((g) => {
        g.geometry.dispose();
        (g.material as THREE.Material).dispose();
      });
      model.texture.dispose();
      model.numberMaterial.dispose();
      model.numberGeometry.dispose();
    },
    [model],
  );
  useEffect(() => {
    model.kitColor.set(color);
  }, [model, color]);
  const visibility = useMemo(()=>({frustum:new THREE.Frustum(),matrix:new THREE.Matrix4(),bounds:new THREE.Sphere(new THREE.Vector3(),2.5)}),[]);
  useFrame(({camera}) => {
    // Animation time is absolute, so returning to view samples the correct pose
    // immediately without accumulating invisible mixer updates.
    if (model.root.parent?.visible === false || !timeline.current.visible) return;
    visibility.matrix.multiplyMatrices(camera.projectionMatrix,camera.matrixWorldInverse);
    visibility.frustum.setFromProjectionMatrix(visibility.matrix);
    model.root.getWorldPosition(visibility.bounds.center);
    if (!visibility.frustum.intersectsSphere(visibility.bounds)) return;
    const p = pose.current,
      seconds =
        timeline.current.entrance !== undefined &&
        timeline.current.entrance < 12
          ? Math.max(0, timeline.current.entrance)
          : (timeline.current.minute * 60) /
            (timeline.current.animationScale ?? 1);
    const identity = `${p.id}/${p.skinIndex}/${p.hairIndex}/${p.shirtNumber}/${p.goalkeeper}`;
    if (identity !== model.lastIdentity) {
      model.lastIdentity = identity;
      const i = Math.abs(p.id);
      model.skinColor.set(
        ["#d8a17c", "#a36e4b", "#70472f", "#e5b999"][p.skinIndex ?? i % 4],
      );
      model.hair.forEach((h, index) => {
        if (h) h.visible = index === (p.hairIndex ?? i % 3);
      });
      model.gloves.forEach((g) => {
        if (g) g.visible = !!p.goalkeeper;
      });
      model.materials
        .filter((m) => m.name === "Hair")
        .forEach((m) =>
          m.color.set(["#211a17", "#37241b", "#161513", "#68462d"][i % 4]),
        );
      const ctx = model.canvas.getContext("2d")!;
      ctx.clearRect(0, 0, 512, 512);
      ctx.fillStyle = "#f5f4e7";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.font = "bold 360px Arial";
      ctx.strokeStyle = "#10202b";
      ctx.lineWidth = 14;
      ctx.lineJoin = "round";
      if (p.shirtNumber !== 0) {
        const shirt = String(p.shirtNumber ?? (i % 99) + 1);
        ctx.strokeText(shirt, 256, 272, 450);
        ctx.fillText(shirt, 256, 272, 450);
      }
      model.texture.needsUpdate = true;
    }
    model.card.visible = !!p.card;
    (model.card.material as THREE.MeshStandardMaterial).color.set(
      p.card === "red" ? "#ed172e" : "#ffdf00",
    );
    model.flag.visible = !!p.flag;
    const clip = animationForAction(p.action, p.goalkeeper),
      base = p.goalkeeper ? "Football_KeeperReady" : "Football_Idle";
    const start =
        (p.actionSecond ?? 0) -
        (p.contactOffset ?? contactOffsets[p.action] ?? 0),
      elapsed = Math.max(0, seconds - start);
    const blend = isOneShot(p.action) ? Math.min(1, elapsed / 0.09) : 1;
    model.actions.forEach((a) => {
      const target =
        a.getClip().name === clip
          ? blend
          : a.getClip().name === base && base !== clip
            ? 1 - blend
            : 0;
      a.enabled = target > 0;
      if (!a.enabled) return;
      a.setEffectiveWeight(target);
      a.time = isOneShot(p.action)
        ? Math.min(elapsed, a.getClip().duration - 0.00001)
        : ((p.locomotionDistance !== undefined &&
          ["run", "sprint", "dribble", "enter"].includes(p.action)
            ? (p.locomotionDistance /
                (p.action === "sprint"
                  ? 4
                  : p.action === "dribble"
                    ? 2.4
                    : p.action === "enter"
                      ? 1.5
                      : 3)) *
              a.getClip().duration
            : seconds) +
            (p.exactTime ? 0 : (Math.abs(p.id) % 13) * 0.07)) %
          a.getClip().duration;
    });
    model.mixer.update(0);
  });
  return <primitive object={model.root} dispose={null} />;
}
export class AvatarBoundary extends React.Component<
  { children: React.ReactNode; fallback: React.ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? this.props.fallback : this.props.children;
  }
}
