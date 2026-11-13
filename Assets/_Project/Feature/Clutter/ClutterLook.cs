using System.Collections.Generic;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Clutter {
    /// <summary>
    /// 물건 메시와 머티리얼 캐시. 판마다 새로 만들면 Web에서 GC 끊김이 생기므로 처음 쓸 때 한 번 만들고 재사용한다.
    /// 단단한 물건(책, 상자)은 각진 상자다. 부드러운 물건은 베개(가운데가 불룩한 쿠션)와
    /// 개어 놓은 천(모서리가 둥근 판 2~3겹)으로 만든다 (11/13, 프로토타입의 물결 상자 대신).
    /// </summary>
    public sealed class ClutterLook {
        private readonly ClutterCatalog catalog;
        private readonly Dictionary<ItemKind, Mesh[]> meshes = new();
        private readonly Dictionary<Color, Material> materials = new();

        public ClutterLook(ClutterCatalog catalog) {
            this.catalog = catalog;
        }

        /// <summary>종류의 메시 하나를 고른다. 단단한 물건도 rng를 한 번 소비해 뽑는 순서가 종류와 무관하게 같다.</summary>
        public Mesh PickMesh(ClutterCatalog.Kind kind, System.Random rng) {
            if (!meshes.TryGetValue(kind.kind, out Mesh[] variants)) {
                int count = kind.soft ? Mathf.Max(1, catalog.softVariants) : 1;
                variants = new Mesh[count];
                for (int i = 0; i < count; i++) {
                    // 변형마다 고정된 모양을 쓴다. 판 시드와 무관하게 같은 모양이 나온다.
                    variants[i] = kind.soft ? BuildSoft(kind, i) : BuildBox(kind.size, false, 0f);
                    variants[i].name = $"{kind.kind}_{i}";
                }

                meshes[kind.kind] = variants;
            }

            return variants[rng.Next(variants.Length)];
        }

        public Material PickMaterial(ClutterCatalog.Kind kind, System.Random rng) {
            Color color = kind.colors.Length > 0 ? kind.colors[rng.Next(kind.colors.Length)] : Color.white;
            if (!materials.TryGetValue(color, out Material material)) {
                material = new Material(catalog.baseMaterial) { color = color };
                material.name = $"{catalog.baseMaterial.name}_{ColorUtility.ToHtmlStringRGB(color)}";
                materials[color] = material;
            }

            return material;
        }

        public void Dispose() {
            foreach (Mesh[] variants in meshes.Values) {
                foreach (Mesh mesh in variants) {
                    Object.Destroy(mesh);
                }
            }

            foreach (Material material in materials.Values) {
                Object.Destroy(material);
            }

            meshes.Clear();
            materials.Clear();
        }

        /// <summary>
        /// 부드러운 물건 메시. 베개는 한 겹이 가운데가 불룩한 쿠션이고, 셔츠와 담요는 모서리가 둥근 납작한 천을
        /// 2~3겹 살짝 어긋나게 쌓은 개어 놓은 빨래다. 충돌체는 그대로 상자(size)이고 메시는 그 안에 들어간다.
        /// </summary>
        private static Mesh BuildSoft(ClutterCatalog.Kind kind, int variant) {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 size = kind.size;
            // 변형마다 조금씩 다르게 (판 시드와 무관한 고정값)
            var shape = new System.Random(1000 + (int)kind.kind * 31 + variant);
            float Jitter(float range) => ((float)shape.NextDouble() * 2f - 1f) * range;

            if (kind.kind == ItemKind.Cushion) {
                // 가운데가 둥글게 부푼 쿠션. 윤곽은 모서리가 둥근 사각(초타원 4.5. 2.6은 너무 동글었다), 테두리에 두께를 남긴다.
                Slab(vertices, triangles, Vector3.zero, size * 0.5f, 0f,
                    sharpness: 2.6f, puff: 0.5f + 0.05f * (variant % 3), seam: 0.35f, pinch: 0.02f, round: 4.5f);
            } else {
                int layers = kind.kind == ItemKind.Blanket ? 3 : 2;
                float layerHeight = size.y / layers;
                for (int layer = 0; layer < layers; layer++) {
                    // 위로 갈수록 조금 작고, 겹마다 살짝 어긋나고 돌아가 있다.
                    float shrink = 1f - 0.035f * layer - Mathf.Abs(Jitter(0.02f));
                    Vector3 half = new(size.x * 0.5f * shrink, layerHeight * 0.5f, size.z * 0.5f * shrink);
                    Vector3 center = new(Jitter(0.015f), -size.y * 0.5f + layerHeight * (layer + 0.5f), Jitter(0.015f));
                    Slab(vertices, triangles, center, half, Jitter(4f),
                        sharpness: 7f, puff: 0.3f, seam: 0.5f, pinch: 0f, round: 6f);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            // 부드러운 음영. 윗면, 옆 띠, 아랫면은 정점을 나눠 두어 테두리에서만 법선이 꺾이고, 그 자리에 선이 그어진다.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // 잡았을 때 처지게 하려고 ClutterItem이 정점을 읽으므로 CPU 쪽 데이터를 남겨 둔다.
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>
        /// 둥근 판 하나: 윗면과 아랫면은 테두리로 갈수록 얇아지고, 테두리는 옆 띠로 잇는다.
        /// sharpness가 클수록 가운데가 평평하고 모서리만 둥글다. puff가 작을수록 가운데가 넓게 부푼다.
        /// seam은 테두리에 남기는 두께 비율, pinch는 변 가운데를 안쪽으로 당기는 비율, yaw는 y축 회전(도).
        /// round가 0보다 크면 네모 격자를 초타원(|x|^round + |z|^round = 1)으로 펴서 윤곽 모서리를 둥글게 한다.
        /// 작을수록 둥글다 (2면 타원, 6이면 모서리만 살짝 둥근 사각).
        /// </summary>
        private static void Slab(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 half, float yaw,
            float sharpness, float puff, float seam, float pinch, float round) {
            const int n = 10;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

            Vector3 Point(float s, float t, float side) {
                float f;
                if (round > 0f) {
                    // 같은 '네모 반지름'(max)의 점을 같은 초타원 위로 옮긴다. 높이도 그 반지름으로 정해 둥글게 부푼다.
                    float r = Mathf.Max(Mathf.Abs(s), Mathf.Abs(t));
                    float norm = Mathf.Pow(Mathf.Pow(Mathf.Abs(s), round) + Mathf.Pow(Mathf.Abs(t), round), 1f / round);
                    if (norm > 1e-5f) {
                        s *= r / norm;
                        t *= r / norm;
                    }

                    f = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(r, sharpness)), puff);
                } else {
                    f = Mathf.Pow(Mathf.Max(0f, (1f - Mathf.Pow(Mathf.Abs(s), sharpness)) * (1f - Mathf.Pow(Mathf.Abs(t), sharpness))), puff);
                }

                float x = s * half.x * (1f - pinch * (1f - t * t));
                float z = t * half.z * (1f - pinch * (1f - s * s));
                float y = side * half.y * (seam + (1f - seam) * f);
                return center + turn * new Vector3(x, y, z);
            }

            void Tri(int a, int b, int c, Vector3 outward) {
                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (Vector3.Dot(normal, outward) < 0f) {
                    (b, c) = (c, b);
                }

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }

            foreach (float side in new[] { 1f, -1f }) {
                int start = vertices.Count;
                for (int j = 0; j <= n; j++) {
                    for (int i = 0; i <= n; i++) {
                        vertices.Add(Point(2f * i / n - 1f, 2f * j / n - 1f, side));
                    }
                }

                Vector3 outward = Vector3.up * side;
                for (int j = 0; j < n; j++) {
                    for (int i = 0; i < n; i++) {
                        int k = start + j * (n + 1) + i;
                        Tri(k, k + n + 1, k + 1, outward);
                        Tri(k + 1, k + n + 1, k + n + 2, outward);
                    }
                }
            }

            // 옆 띠: 테두리를 한 바퀴 돌며 위아래 테두리 점을 잇는다.
            var ring = new List<Vector2>();
            for (int i = 0; i < n; i++) {
                ring.Add(new Vector2(2f * i / n - 1f, -1f));
            }

            for (int j = 0; j < n; j++) {
                ring.Add(new Vector2(1f, 2f * j / n - 1f));
            }

            for (int i = n; i > 0; i--) {
                ring.Add(new Vector2(2f * i / n - 1f, 1f));
            }

            for (int j = n; j > 0; j--) {
                ring.Add(new Vector2(-1f, 2f * j / n - 1f));
            }
            int band = vertices.Count;
            foreach (Vector2 p in ring) {
                vertices.Add(Point(p.x, p.y, 1f));
                vertices.Add(Point(p.x, p.y, -1f));
            }

            for (int r = 0; r < ring.Count; r++) {
                int a = band + r * 2;
                int b = band + (r + 1) % ring.Count * 2;
                Vector3 mid = (vertices[a] + vertices[b]) * 0.5f - center;
                Vector3 outward = new(mid.x, 0f, mid.z);
                Tri(a, a + 1, b, outward);
                Tri(b, a + 1, b + 1, outward);
            }
        }

        // 프로토타입: BoxGeometry(w, h, d, 4, 1, 4)의 모든 정점을 y로 sin(x·9+s)·cos(z·8+1.7s)·min(0.65h, 0.035)만큼 옮긴다.
        private static Mesh BuildBox(Vector3 size, bool soft, float seed) {
            int sx = soft ? 4 : 1;
            int sz = soft ? 4 : 1;
            const int sy = 1;
            float amplitude = soft ? Mathf.Min(size.y * 0.65f, 0.035f) : 0f;
            Vector3 half = size * 0.5f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            Vector3 Displace(Vector3 p) {
                if (amplitude > 0f) {
                    p.y += Mathf.Sin(p.x * 9f + seed) * Mathf.Cos(p.z * 8f + seed * 1.7f) * amplitude;
                }

                return p;
            }

            void Face(Vector3 normal, Vector3 uAxis, Vector3 vAxis, int uSeg, int vSeg) {
                Vector3 center = Vector3.Scale(normal, half);
                Vector3 u = Vector3.Scale(uAxis, half);
                Vector3 v = Vector3.Scale(vAxis, half);
                for (int i = 0; i < uSeg; i++) {
                    for (int j = 0; j < vSeg; j++) {
                        Vector3 Corner(int a, int b) =>
                            Displace(center + u * (2f * (i + a) / uSeg - 1f) + v * (2f * (j + b) / vSeg - 1f));

                        Vector3 p00 = Corner(0, 0);
                        Vector3 p10 = Corner(1, 0);
                        Vector3 p01 = Corner(0, 1);
                        Vector3 p11 = Corner(1, 1);
                        Triangle(p00, p01, p11, normal);
                        Triangle(p00, p11, p10, normal);
                    }
                }
            }

            // 삼각형마다 정점을 따로 두어 각진(flat) 음영을 낸다. 법선이 면 바깥을 향하도록 감는 순서를 맞춘다.
            void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward) {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, outward) < 0f) {
                    (b, c) = (c, b);
                    n = -n;
                }

                n.Normalize();
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                normals.Add(n);
                normals.Add(n);
                normals.Add(n);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            Face(Vector3.up, Vector3.right, Vector3.forward, sx, sz);
            Face(Vector3.down, Vector3.right, Vector3.forward, sx, sz);
            Face(Vector3.right, Vector3.forward, Vector3.up, sz, sy);
            Face(Vector3.left, Vector3.forward, Vector3.up, sz, sy);
            Face(Vector3.forward, Vector3.right, Vector3.up, sx, sy);
            Face(Vector3.back, Vector3.right, Vector3.up, sx, sy);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
