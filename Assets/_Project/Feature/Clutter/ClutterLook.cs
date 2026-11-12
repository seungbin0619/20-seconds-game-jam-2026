using System.Collections.Generic;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Clutter {
    /// <summary>
    /// 물건 메시와 머티리얼 캐시. 판마다 새로 만들면 Web에서 GC 끊김이 생기므로 처음 쓸 때 한 번 만들고 재사용한다.
    /// 부드러운 물건은 프로토타입 softGeometry처럼 물결 모양으로 휘고, 삼각형마다 정점을 나눠 각진 음영을 낸다.
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
                    // 변형마다 고정된 물결 위상을 쓴다. 판 시드와 무관하게 같은 모양이 나온다.
                    variants[i] = BuildBox(kind.size, kind.soft, 17.3f * (i + 1));
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
