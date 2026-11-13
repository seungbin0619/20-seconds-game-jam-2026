using System.Collections.Generic;
using UnityEngine;

namespace MissedCall.Feature.Room {
    /// <summary>
    /// 소파 좌석, 매트리스, 빈백처럼 푹신한 가구. 충돌체(정적)에 붙인다.
    /// 겉모습 메시의 윗면만 지점별로 움푹 꺼지게 한다:
    /// - 물건이 부딪히면 닿은 지점 주변이 파였다가 감쇠 스프링으로 출렁이며 돌아온다.
    /// - 물건이 위에 놓여 있으면 그 무게만큼 아래가 계속 꺼져 있다 (축 처짐). 치우면 다시 부푼다.
    /// 충돌체 자체는 움직이지 않으므로 더미와 길 찾기에는 영향이 없다. 겉면은 충돌체보다 조금 높게 만들어 두어
    /// (방 빌더의 SoftSink) 꺼진 자리에 물건이 파묻혀 보인다.
    /// </summary>
    public sealed class SoftSurface : MonoBehaviour {
        [Tooltip("움푹 꺼질 겉모습. 상자 기본 메시는 런타임에 윗면을 잘게 나눈 메시로 바꾼다")]
        [SerializeField] private MeshFilter[] meshes = new MeshFilter[0];
        [Tooltip("가장 깊이 꺼지는 깊이(m)")]
        [SerializeField] private float maxDepth = 0.07f;
        [Tooltip("부딪힌 속도(m/s) 1당 꺼지는 속도(m/s)")]
        [SerializeField] private float impactPerSpeed = 0.12f;
        [Tooltip("부딪힌 자리 둘레에서 함께 꺼지는 반경(m)")]
        [SerializeField] private float impactRadius = 0.16f;
        [Tooltip("놓인 물건 1kg당 꺼지는 깊이(m)")]
        [SerializeField] private float restDepthPerKg = 0.045f;
        [SerializeField] private float stiffness = 140f;
        [SerializeField] private float damping = 11f;
        [Tooltip("부딪힌 물건에 남기는 속도 비율. 낮을수록 푹 꺼지듯 멈춘다")]
        [SerializeField, Range(0f, 1f)] private float absorb = 0.3f;
        [Tooltip("윗면을 나누는 칸 수 (가로세로 각각)")]
        [SerializeField] private int subdivisions = 14;

        private sealed class Surface {
            public MeshFilter Filter;
            public Mesh Mesh;
            public Vector3[] Base;
            public Vector3[] Current;
            // 정점마다 위쪽 정도 (윗면 1, 바닥 0). 이만큼만 아래로 민다.
            public float[] Weight;
            public float[] Offset;
            public float[] Velocity;
            public Vector3[] World;
        }

        private readonly List<Surface> surfaces = new();
        private readonly HashSet<Rigidbody> resting = new();
        private readonly List<Rigidbody> removeBuffer = new();
        private readonly List<ContactPoint> contacts = new();
        // 이번 프레임 처짐 계산에 쓰는 놓인 물건 (중심 XZ, 반경, 깊이). 정점마다 GetComponent하지 않게 모아 둔다.
        private readonly List<Vector4> loads = new();
        private BoxCollider box;
        private bool built;
        // 움직이는 중인지. 가라앉으면 정점 계산을 건너뛴다 (컴포넌트를 끄면 OnDisable이 처짐을 지우므로 끄지 않는다).
        private bool active;

        /// <summary>마지막으로 받은 충돌에서 꺼지기 시작한 속도(m/s). 값을 맞출 때 확인용.</summary>
        public float LastPush { get; private set; }

#if UNITY_EDITOR
        public void EditorSetup(MeshFilter[] softMeshes, float depth, float stiff, float damp) {
            meshes = softMeshes;
            maxDepth = depth;
            stiffness = stiff;
            damping = damp;
        }
#endif

        private void Awake() {
            box = GetComponent<BoxCollider>();
            Build();
        }

        private void OnDisable() {
            // 모듈이 풀로 돌아가면 이전 판의 처짐을 지운다.
            resting.Clear();
            foreach (Surface s in surfaces) {
                System.Array.Clear(s.Offset, 0, s.Offset.Length);
                System.Array.Clear(s.Velocity, 0, s.Velocity.Length);
                Apply(s);
            }
        }

        private void OnCollisionEnter(Collision collision) {
            Rigidbody body = collision.rigidbody;
            if (body == null) {
                return;
            }

            resting.Add(body);
            active = true;
            // 판 준비 중 안정화(스크립트 시뮬레이션)의 충돌은 처짐만 남기고 출렁임은 주지 않는다.
            if (Physics.simulationMode == SimulationMode.Script) {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.3f) {
                LastPush = 0f;
                return;
            }

            if (!body.isKinematic) {
                // 튀거나 미끄러지지 않고 쿠션에 받혀 멈춘다.
                body.linearVelocity *= absorb;
                body.angularVelocity *= absorb;
            }

            collision.GetContacts(contacts);
            Vector3 point = contacts.Count > 0 ? contacts[0].point : body.worldCenterOfMass;
            LastPush = speed * impactPerSpeed;
            Kick(point, LastPush);
        }

        private void OnCollisionStay(Collision collision) {
            // 위에서 밀리거나 굴러가는 물건이 있으면 처짐을 따라가게 깨운다.
            Rigidbody body = collision.rigidbody;
            if (body != null && !active && body.linearVelocity.sqrMagnitude > 0.0025f) {
                resting.Add(body);
                active = true;
            }
        }

        private void OnCollisionExit(Collision collision) {
            if (collision.rigidbody != null) {
                resting.Remove(collision.rigidbody);
                active = true;
            }
        }

        private void Update() {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (!active || dt <= 0f) {
                return;
            }

            DropStale();
            CollectLoads();
            bool moving = false;
            foreach (Surface s in surfaces) {
                RefreshWorld(s);
                for (int i = 0; i < s.Base.Length; i++) {
                    if (s.Weight[i] <= 0f) {
                        continue;
                    }

                    float target = RestDepth(s.World[i]);
                    float accel = stiffness * (target - s.Offset[i]) - damping * s.Velocity[i];
                    s.Velocity[i] += accel * dt;
                    s.Offset[i] = Mathf.Clamp(s.Offset[i] + s.Velocity[i] * dt, -maxDepth * 0.3f, maxDepth);
                    if (Mathf.Abs(s.Velocity[i]) > 0.002f || Mathf.Abs(target - s.Offset[i]) > 0.0005f) {
                        moving = true;
                    }
                }

                Apply(s);
            }

            // 다 가라앉았으면 다음 충돌까지 쉰다.
            if (!moving) {
                active = false;
            }
        }

        // 위에 놓인 물건들의 무게로 이 지점이 꺼질 깊이.
        private float RestDepth(Vector3 world) {
            float depth = 0f;
            foreach (Vector4 load in loads) {
                float dx = world.x - load.x;
                float dz = world.z - load.y;
                depth += load.w * Mathf.Exp(-(dx * dx + dz * dz) / (load.z * load.z));
            }

            return Mathf.Min(depth, maxDepth * 0.8f);
        }

        private void CollectLoads() {
            loads.Clear();
            foreach (Rigidbody body in resting) {
                Collider collider = body.GetComponent<Collider>();
                Bounds b = collider != null ? collider.bounds : new Bounds(body.position, Vector3.one * 0.2f);
                float radius = Mathf.Max(b.extents.x, b.extents.z) + 0.04f;
                float depth = Mathf.Min(body.mass * restDepthPerKg, maxDepth * 0.7f);
                loads.Add(new Vector4(b.center.x, b.center.z, radius, depth));
            }
        }

        private void Kick(Vector3 point, float speed) {
            float r2 = impactRadius * impactRadius;
            foreach (Surface s in surfaces) {
                RefreshWorld(s);
                for (int i = 0; i < s.Base.Length; i++) {
                    if (s.Weight[i] <= 0f) {
                        continue;
                    }

                    float dx = s.World[i].x - point.x;
                    float dz = s.World[i].z - point.z;
                    s.Velocity[i] += speed * Mathf.Exp(-(dx * dx + dz * dz) / r2);
                }
            }
        }

        // 손에 들렸거나 풀로 돌아갔거나 멀리 떨어진 물건은 처짐에서 뺀다.
        private void DropStale() {
            if (resting.Count == 0) {
                return;
            }

            Bounds area = box.bounds;
            area.Expand(new Vector3(0.3f, 0.6f, 0.3f));
            removeBuffer.Clear();
            foreach (Rigidbody body in resting) {
                if (body == null || !body.gameObject.activeInHierarchy || body.isKinematic || !area.Contains(body.position)) {
                    removeBuffer.Add(body);
                }
            }

            foreach (Rigidbody body in removeBuffer) {
                resting.Remove(body);
            }
        }

        private void RefreshWorld(Surface s) {
            Transform t = s.Filter.transform;
            for (int i = 0; i < s.Base.Length; i++) {
                s.World[i] = t.TransformPoint(s.Base[i]);
            }
        }

        private void Apply(Surface s) {
            // 부품은 크기를 스케일로 갖는 단위 메시라서, 월드 깊이를 메시 공간 높이로 바꾼다.
            float toLocal = 1f / Mathf.Max(0.001f, s.Filter.transform.lossyScale.y);
            for (int i = 0; i < s.Base.Length; i++) {
                Vector3 v = s.Base[i];
                v.y -= s.Offset[i] * s.Weight[i] * toLocal;
                s.Current[i] = v;
            }

            s.Mesh.vertices = s.Current;
            s.Mesh.RecalculateNormals();
        }

        private void Build() {
            if (built) {
                return;
            }

            built = true;
            foreach (MeshFilter filter in meshes) {
                if (filter == null) {
                    continue;
                }

                Mesh source = filter.sharedMesh;
                Mesh mesh = source != null && source.vertexCount > 24 ? Instantiate(source) : SubdividedBox(subdivisions);
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;

                Vector3[] vertices = mesh.vertices;
                var s = new Surface {
                    Filter = filter,
                    Mesh = mesh,
                    Base = vertices,
                    Current = (Vector3[])vertices.Clone(),
                    Weight = new float[vertices.Length],
                    Offset = new float[vertices.Length],
                    Velocity = new float[vertices.Length],
                    World = new Vector3[vertices.Length],
                };

                float minY = float.MaxValue;
                float maxY = float.MinValue;
                foreach (Vector3 v in vertices) {
                    minY = Mathf.Min(minY, v.y);
                    maxY = Mathf.Max(maxY, v.y);
                }

                for (int i = 0; i < vertices.Length; i++) {
                    // 윗면은 그대로 내려가고, 옆면은 위로 갈수록 많이 내려간다. 바닥은 고정.
                    float h = Mathf.InverseLerp(minY, maxY, vertices[i].y);
                    s.Weight[i] = h * h;
                }

                surfaces.Add(s);
            }
        }

        /// <summary>
        /// -0.5~0.5 단위 상자. 윗면은 n x n 칸, 옆면은 가로 n칸 x 세로 4칸으로 나눠 가장자리도 함께 처지게 한다.
        /// </summary>
        private static Mesh SubdividedBox(int n) {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // 면 하나: origin에서 u, v 방향으로 cu x cv 칸. 바깥을 보는 쪽이 앞면이 되게 감는다.
            void Face(Vector3 origin, Vector3 u, Vector3 v, int cu, int cv) {
                int start = vertices.Count;
                for (int j = 0; j <= cv; j++) {
                    for (int i = 0; i <= cu; i++) {
                        float a = (float)i / cu;
                        float b = (float)j / cv;
                        vertices.Add(origin + u * a + v * b);
                        uvs.Add(new Vector2(a, b));
                    }
                }

                for (int j = 0; j < cv; j++) {
                    for (int i = 0; i < cu; i++) {
                        int k = start + j * (cu + 1) + i;
                        triangles.Add(k);
                        triangles.Add(k + cu + 1);
                        triangles.Add(k + 1);
                        triangles.Add(k + 1);
                        triangles.Add(k + cu + 1);
                        triangles.Add(k + cu + 2);
                    }
                }
            }

            const int sideRows = 4;
            Face(new Vector3(-0.5f, 0.5f, -0.5f), Vector3.right, Vector3.forward, n, n);           // 위
            Face(new Vector3(-0.5f, -0.5f, 0.5f), Vector3.right, Vector3.back, 1, 1);             // 아래
            Face(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.right, Vector3.up, n, sideRows);       // 남
            Face(new Vector3(0.5f, -0.5f, 0.5f), Vector3.left, Vector3.up, n, sideRows);          // 북
            Face(new Vector3(0.5f, -0.5f, -0.5f), Vector3.forward, Vector3.up, n, sideRows);      // 동
            Face(new Vector3(-0.5f, -0.5f, 0.5f), Vector3.back, Vector3.up, n, sideRows);         // 서

            var mesh = new Mesh { name = "SoftBox" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
