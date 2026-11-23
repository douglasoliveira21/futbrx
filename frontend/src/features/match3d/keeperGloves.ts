import * as THREE from "three";

/** A thin, skinned shell of the supplied hands, in the body's bind space.
 * Keeping the original weights also preserves forearm twist at the cuff. */
export function createKeeperGloves(root: THREE.Object3D) {
  root.updateMatrixWorld(true);
  const body = root.getObjectByName("User_Footballer") as
    THREE.SkinnedMesh | undefined;
  if (!body?.isSkinnedMesh) return [];
  const source = body.geometry;
  const normalMatrix = new THREE.Matrix3().getNormalMatrix(body.matrixWorld);
  const inverse = body.matrixWorld.clone().invert();
  const gloves: THREE.SkinnedMesh[] = [];
  // FBX UV and hard-normal seams duplicate vertices. Inflate each shared point
  // along the same averaged normal so the shell has no cracks at those seams.
  const smoothNormals = new Map<string, THREE.Vector3>();
  const key = (i: number) =>
    [0, 1, 2]
      .map((c) => source.getAttribute("position").getComponent(i, c).toFixed(5))
      .join("/");
  for (let i = 0; i < source.getAttribute("position").count; i++) {
    const k = key(i),
      n = new THREE.Vector3().fromBufferAttribute(
        source.getAttribute("normal"),
        i,
      );
    if (smoothNormals.has(k)) smoothNormals.get(k)!.add(n);
    else smoothNormals.set(k, n);
  }
  for (const n of smoothNormals.values()) n.normalize();
  for (const side of ["L", "R"]) {
    const hand = root.getObjectByName(side + "_Hand");
    const forearm = root.getObjectByName(side + "_Forearm");
    if (!hand || !forearm) continue;
    root
      .getObjectByName("KeeperGlove_" + side.toLowerCase())
      ?.removeFromParent();
    const wrist = hand.getWorldPosition(new THREE.Vector3());
    const axis = wrist
      .clone()
      .sub(forearm.getWorldPosition(new THREE.Vector3()))
      .normalize();
    const panel: number[] = [];
    const positions: number[] = [],
      normals: number[] = [],
      indices: number[] = [],
      weights: number[] = [],
      colors: number[] = [];
    const position = source.getAttribute("position"),
      normal = source.getAttribute("normal");
    const skinIndex = source.getAttribute("skinIndex"),
      skinWeight = source.getAttribute("skinWeight");
    const count = source.index?.count ?? position.count;
    const latex = new THREE.Color("#eef0dd"),
      back = new THREE.Color("#18303c"),
      accent = new THREE.Color("#b7e64c"),
      cuff = new THREE.Color("#10242e");
    const world = new THREE.Vector3(),
      n = new THREE.Vector3(),
      centre = new THREE.Vector3();
    for (let triangle = 0; triangle < count; triangle += 3) {
      const vertices = [0, 1, 2].map(
        (j) => source.index?.getX(triangle + j) ?? triangle + j,
      );
      centre.set(0, 0, 0);
      for (const i of vertices)
        centre.add(
          world.fromBufferAttribute(position, i).applyMatrix4(body.matrixWorld),
        );
      centre.multiplyScalar(1 / 3).sub(wrist);
      const along = centre.dot(axis);
      if (along < -0.044 || along > 0.205 || centre.length() > 0.23) continue;
      for (const i of vertices) {
        world.fromBufferAttribute(position, i).applyMatrix4(body.matrixWorld);
        n.copy(smoothNormals.get(key(i))!).applyNormalMatrix(normalMatrix);
        const vertexAlong = world.clone().sub(wrist).dot(axis);
        const dorsal = THREE.MathUtils.smoothstep(n.y, -0.05, 0.35);
        const color = latex.clone().lerp(back, dorsal);
        if (vertexAlong < 0.008) color.copy(cuff);
        const stripe =
          1 -
          THREE.MathUtils.smoothstep(
            Math.abs(vertexAlong + 0.021),
            0.004,
            0.009,
          );
        const backStripe =
          (1 -
            THREE.MathUtils.smoothstep(
              Math.abs(vertexAlong - 0.04),
              0.004,
              0.011,
            )) *
          dorsal;
        color.lerp(accent, Math.max(stripe, backStripe));
        const inflation =
          0.0035 +
          0.0015 *
            Math.exp(-Math.pow((vertexAlong - 0.07) / 0.035, 2)) *
            Math.max(0, n.y);
        world.addScaledVector(n, inflation).applyMatrix4(inverse);
        positions.push(...world.toArray());
        normals.push(...smoothNormals.get(key(i))!.toArray());
        panel.push(vertexAlong, n.y);
        colors.push(color.r, color.g, color.b);
        for (let component = 0; component < 4; component++) {
          indices.push(skinIndex.getComponent(i, component));
          weights.push(skinWeight.getComponent(i, component));
        }
      }
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute(
      "position",
      new THREE.Float32BufferAttribute(positions, 3),
    );
    geometry.setAttribute(
      "normal",
      new THREE.Float32BufferAttribute(normals, 3),
    );
    geometry.setAttribute(
      "glovePanel",
      new THREE.Float32BufferAttribute(panel, 2),
    );
    geometry.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
    geometry.setAttribute(
      "skinIndex",
      new THREE.Uint16BufferAttribute(indices, 4),
    );
    geometry.setAttribute(
      "skinWeight",
      new THREE.Float32BufferAttribute(weights, 4),
    );
    const material = new THREE.MeshStandardMaterial({
      name: "KeeperGlove_Anatomical",
      vertexColors: false,
      roughness: 0.68,
    });
    material.onBeforeCompile = (shader) => {
      shader.vertexShader =
        "attribute vec2 glovePanel; varying vec2 vGlovePanel;\n" +
        shader.vertexShader.replace(
          "#include <begin_vertex>",
          "#include <begin_vertex>\nvGlovePanel=glovePanel;",
        );
      shader.fragmentShader =
        "varying vec2 vGlovePanel;\n" +
        shader.fragmentShader.replace(
          "#include <color_fragment>",
          `#include <color_fragment>
        float dorsal=smoothstep(-.05,.35,vGlovePanel.y);
        vec3 fabric=mix(vec3(.855,.871,.724),vec3(.009,.03,.045),dorsal);
        fabric=mix(fabric,vec3(.005,.017,.027),1.0-smoothstep(.005,.009,vGlovePanel.x));
        float wrist=1.0-smoothstep(.004,.0055,abs(vGlovePanel.x+.021));
        float back= (1.0-smoothstep(.004,.0055,abs(vGlovePanel.x-.04)))*dorsal;
        fabric=mix(fabric,vec3(.47,.79,.075),max(wrist,back));
        diffuseColor.rgb=fabric;
      `,
        );
    };
    material.customProgramCacheKey = () => "anatomical-keeper-glove-v1";
    const glove = new THREE.SkinnedMesh(geometry, material);
    glove.name = "KeeperGlove_" + side.toLowerCase();
    glove.position.copy(body.position);
    glove.quaternion.copy(body.quaternion);
    glove.scale.copy(body.scale);
    glove.bindMode = body.bindMode;
    glove.bind(body.skeleton, body.bindMatrix);
    glove.visible = false;
    glove.frustumCulled = false;
    glove.castShadow = true;
    glove.userData = {
      construction: "hand-shell",
      paddingMetres: [0.0035, 0.005],
      side,
    };
    body.parent!.add(glove);
    gloves.push(glove);
  }
  return gloves;
}
