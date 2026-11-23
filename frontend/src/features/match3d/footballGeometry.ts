import * as THREE from "three";
export const BALL_RADIUS = 0.11;
// Truncated icosahedron: 12 pentagons and 20 hexagons, projected onto a round ball.
export function footballGeometry() {
  const ico = new THREE.IcosahedronGeometry(1, 0),
    attribute = ico.getAttribute("position"),
    vertices: THREE.Vector3[] = [],
    triangles: number[][] = [];
  for (let i = 0; i < attribute.count; i += 3) {
    const face: number[] = [];
    for (let j = 0; j < 3; j++) {
      const v = new THREE.Vector3().fromBufferAttribute(attribute, i + j);
      let index = vertices.findIndex((p) => p.distanceToSquared(v) < 1e-10);
      if (index < 0) {
        index = vertices.length;
        vertices.push(v);
      }
      face.push(index);
    }
    triangles.push(face);
  }
  ico.dispose();
  const neighbours = vertices.map(() => new Set<number>());
  for (const f of triangles)
    for (let i = 0; i < 3; i++) {
      neighbours[f[i]].add(f[(i + 1) % 3]);
      neighbours[f[i]].add(f[(i + 2) % 3]);
    }
  const edge = (a: number, b: number) =>
    vertices[a]
      .clone()
      .multiplyScalar(2 / 3)
      .addScaledVector(vertices[b], 1 / 3)
      .normalize();
  const panels: { points: THREE.Vector3[]; black: boolean }[] = [];
  for (let i = 0; i < vertices.length; i++) {
    const normal = vertices[i].clone().normalize(),
      u = new THREE.Vector3().crossVectors(normal, new THREE.Vector3(0, 1, 0));
    if (u.lengthSq() < 0.01) u.crossVectors(normal, new THREE.Vector3(1, 0, 0));
    u.normalize();
    const v = new THREE.Vector3().crossVectors(normal, u);
    const points = [...neighbours[i]]
      .map((j) => edge(i, j))
      .sort(
        (a, b) =>
          Math.atan2(a.dot(v), a.dot(u)) - Math.atan2(b.dot(v), b.dot(u)),
      );
    panels.push({ points, black: true });
  }
  for (const [a, b, c] of triangles)
    panels.push({
      points: [
        edge(a, b),
        edge(b, a),
        edge(b, c),
        edge(c, b),
        edge(c, a),
        edge(a, c),
      ],
      black: false,
    });
  const positions: number[] = [],
    normals: number[] = [],
    colors: number[] = [];
  const triangle = (
    a: THREE.Vector3,
    b: THREE.Vector3,
    c: THREE.Vector3,
    color: THREE.Color,
    depth: number,
  ) => {
    if (depth) {
      const ab = a.clone().add(b).normalize(),
        bc = b.clone().add(c).normalize(),
        ca = c.clone().add(a).normalize();
      triangle(a, ab, ca, color, depth - 1);
      triangle(ab, b, bc, color, depth - 1);
      triangle(ca, bc, c, color, depth - 1);
      triangle(ab, bc, ca, color, depth - 1);
      return;
    }
    for (const p of [a, b, c]) {
      positions.push(p.x * BALL_RADIUS, p.y * BALL_RADIUS, p.z * BALL_RADIUS);
      normals.push(p.x, p.y, p.z);
      colors.push(color.r, color.g, color.b);
    }
  };
  for (const panel of panels) {
    const center = panel.points
        .reduce((s, p) => s.add(p), new THREE.Vector3())
        .normalize(),
      color = new THREE.Color(panel.black ? "#14202a" : "#eff3e5");
    for (let i = 0; i < panel.points.length; i++)
      triangle(
        center,
        panel.points[i],
        panel.points[(i + 1) % panel.points.length],
        color,
        2,
      );
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute(
    "position",
    new THREE.Float32BufferAttribute(positions, 3),
  );
  geometry.setAttribute("normal", new THREE.Float32BufferAttribute(normals, 3));
  geometry.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
  geometry.userData = { pentagons: 12, hexagons: 20, radius: BALL_RADIUS };
  return geometry;
}
