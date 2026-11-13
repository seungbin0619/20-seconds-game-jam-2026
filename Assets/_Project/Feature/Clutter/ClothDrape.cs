using System.Collections.Generic;
using UnityEngine;

namespace MissedCall.Feature.Clutter {
    /// <summary>
    /// 잡거나 던진 천(셔츠, 담요)의 겉모습을 위한 작은 천 시뮬레이션. 점 N x N 격자를 Verlet 적분하고,
    /// 이웃 점 사이 거리(가로세로, 대각선, 한 칸 건너)를 지켜 천처럼 늘어지고 주름지게 한다.
    /// - 잡혔거나 날아가는 동안은 가운데 2 x 2 점을 물건 중심에 붙인다. 물건이 움직이면 뒤로 끌린다.
    /// - 점마다 가구, 바닥, 다른 물건과 부딪혀 그 위에 걸치거나 늘어진다 (자기 충돌체는 무시).
    /// - 가라앉으면 모양을 물건 기준으로 굳혀(Freeze) 물건을 따라다니게 하고, 다시 움직이면 그 모양에서 이어 간다.
    /// 충돌체는 따로 있고(상자), 이 시뮬레이션은 겉모습만 바꾼다. 물건마다 하나를 만들어 재사용한다.
    /// </summary>
    public sealed class ClothDrape {
        public const int N = 12;
        private const float FixedStep = 1f / 60f;
        private const int MaxSteps = 3;
        // 한 스텝에 이만큼(m) 안 움직이는 상태가 SettleSteps 동안 이어지면 가라앉은 것으로 본다.
        private const float SettleMove = 0.0006f;
        private const int SettleSteps = 20;

        private static readonly RaycastHit[] Hits = new RaycastHit[6];
        private static int collisionMask;

        private readonly Vector3[] position = new Vector3[N * N];
        private readonly Vector3[] previous = new Vector3[N * N];
        private readonly Vector3[] start = new Vector3[N * N];
        private readonly Vector3[] restLocal = new Vector3[N * N];
        private readonly Vector3[] frozenLocal = new Vector3[N * N];
        private readonly Vector3[] normals = new Vector3[N * N];
        private readonly int[] constraintA;
        private readonly int[] constraintB;
        private readonly float[] restLength;
        private readonly int[] pins = { Index(N / 2 - 1, N / 2 - 1), Index(N / 2, N / 2 - 1), Index(N / 2 - 1, N / 2), Index(N / 2, N / 2) };
        private readonly bool[] pinned = new bool[N * N];
        private readonly Vector3[] vertices = new Vector3[N * N * 2];
        private float accumulator;
        private float thickness = 0.02f;
        private int stillSteps;

        public Mesh Mesh { get; }

        /// <summary>시뮬레이션 중인지. 굳힌 뒤에는 false지만 메시는 그 모양을 유지한다.</summary>
        public bool Active { get; private set; }

        /// <summary>굳힌 모양이 있는지 (한 번이라도 펼쳐졌는지).</summary>
        public bool HasShape { get; private set; }

        /// <summary>마지막 SettleSteps 스텝 동안 거의 움직이지 않았는지.</summary>
        public bool Settled => stillSteps >= SettleSteps;

        public ClothDrape() {
            foreach (int p in pins) {
                pinned[p] = true;
            }

            // 가로세로(구조), 대각선(전단), 한 칸 건너(굽힘) 제약
            var a = new List<int>();
            var b = new List<int>();
            void Link(int i0, int j0, int i1, int j1) {
                if (i1 < N && j1 < N && i1 >= 0) {
                    a.Add(Index(i0, j0));
                    b.Add(Index(i1, j1));
                }
            }

            for (int j = 0; j < N; j++) {
                for (int i = 0; i < N; i++) {
                    Link(i, j, i + 1, j);
                    Link(i, j, i, j + 1);
                    Link(i, j, i + 1, j + 1);
                    Link(i, j, i - 1, j + 1);
                    Link(i, j, i + 2, j);
                    Link(i, j, i, j + 2);
                }
            }

            constraintA = a.ToArray();
            constraintB = b.ToArray();
            restLength = new float[constraintA.Length];

            Mesh = new Mesh { name = "ClothDrape" };
            Mesh.MarkDynamic();
            Mesh.vertices = vertices;
            Mesh.triangles = BuildTriangles();
        }

        // 앞면(위), 뒷면(아래), 그리고 둘을 잇는 테두리 띠. 띠는 앞뒤 정점을 같이 써서 가장자리가 둥글게 음영진다.
        private static int[] BuildTriangles() {
            var triangles = new List<int>();
            int back = N * N;
            for (int j = 0; j < N - 1; j++) {
                for (int i = 0; i < N - 1; i++) {
                    int k = Index(i, j);
                    triangles.AddRange(new[] { k, k + N, k + 1, k + 1, k + N, k + N + 1 });
                    triangles.AddRange(new[] { back + k, back + k + 1, back + k + N, back + k + 1, back + k + N + 1, back + k + N });
                }
            }

            var ring = new List<int>();
            for (int i = 0; i < N - 1; i++) {
                ring.Add(Index(i, 0));
            }

            for (int j = 0; j < N - 1; j++) {
                ring.Add(Index(N - 1, j));
            }

            for (int i = N - 1; i > 0; i--) {
                ring.Add(Index(i, N - 1));
            }

            for (int j = N - 1; j > 0; j--) {
                ring.Add(Index(0, j));
            }

            // 고리는 위에서 볼 때 반시계(+x 다음 +z)로 돈다. 바깥을 보게 감는다.
            for (int r = 0; r < ring.Count; r++) {
                int a = ring[r];
                int b = ring[(r + 1) % ring.Count];
                triangles.AddRange(new[] { a, b, back + a, b, back + b, back + a });
            }

            return triangles.ToArray();
        }

        private static int Index(int i, int j) => j * N + i;

        /// <summary>물건의 현재 자세로 펼친 천 한 장을 놓고 시작한다. size는 물건 상자 크기(가로, 세로 = x, z).</summary>
        public void Begin(Transform item, Vector3 size, float sheetThickness) {
            thickness = sheetThickness;
            for (int j = 0; j < N; j++) {
                for (int i = 0; i < N; i++) {
                    int k = Index(i, j);
                    var local = new Vector3((i / (N - 1f) - 0.5f) * size.x, 0f, (j / (N - 1f) - 0.5f) * size.z);
                    restLocal[k] = local;
                    // 완전히 대칭이면 주름이 생기지 않으므로 조금 흔든다 (판 무작위와 무관한 고정값).
                    local.y += Mathf.Sin(i * 1.7f + j * 2.3f) * 0.004f;
                    position[k] = item.TransformPoint(local);
                    previous[k] = position[k];
                }
            }

            for (int c = 0; c < constraintA.Length; c++) {
                restLength[c] = Vector3.Distance(restLocal[constraintA[c]], restLocal[constraintB[c]]);
            }

            StartRunning();
        }

        /// <summary>굳혀 둔 모양에서 다시 시뮬레이션을 이어 간다 (다시 집거나 밀렸을 때).</summary>
        public void Resume(Transform item) {
            for (int k = 0; k < position.Length; k++) {
                position[k] = item.TransformPoint(frozenLocal[k]);
                previous[k] = position[k];
            }

            StartRunning();
        }

        private void StartRunning() {
            accumulator = 0f;
            stillSteps = 0;
            Active = true;
            HasShape = true;
        }

        /// <summary>지금 모양을 물건 기준으로 굳힌다. 메시는 물건 로컬이라 그대로 물건을 따라다닌다.</summary>
        public void Freeze(Transform item) {
            for (int k = 0; k < position.Length; k++) {
                frozenLocal[k] = item.InverseTransformPoint(position[k]);
            }

            Active = false;
        }

        /// <summary>새 판에서 물건을 다시 쓸 때 굳힌 모양을 버린다.</summary>
        public void Clear() {
            Active = false;
            HasShape = false;
        }

        /// <summary>시뮬레이션을 진행한다. pin이면 가운데를 물건 중심(자세 포함)에 붙인다. self 충돌체는 무시한다.</summary>
        public void Simulate(float dt, Transform item, bool pin, float damping, int iterations, Collider self) {
            if (collisionMask == 0) {
                collisionMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player");
            }

            accumulator = Mathf.Min(accumulator + dt, FixedStep * MaxSteps);
            Vector3 gravity = Physics.gravity * (FixedStep * FixedStep);
            while (accumulator >= FixedStep) {
                accumulator -= FixedStep;
                for (int k = 0; k < position.Length; k++) {
                    Vector3 current = position[k];
                    start[k] = current;
                    position[k] += (current - previous[k]) * damping + gravity;
                    previous[k] = current;
                }

                for (int iteration = 0; iteration < iterations; iteration++) {
                    SolveDistances(pin);
                    if (pin) {
                        foreach (int p in pins) {
                            position[p] = item.TransformPoint(restLocal[p]);
                        }
                    }
                }

                Collide(self);

                float maxMove = 0f;
                for (int k = 0; k < position.Length; k++) {
                    maxMove = Mathf.Max(maxMove, (position[k] - start[k]).sqrMagnitude);
                }

                stillSteps = maxMove < SettleMove * SettleMove ? stillSteps + 1 : 0;
            }
        }

        private void SolveDistances(bool pin) {
            for (int c = 0; c < constraintA.Length; c++) {
                int a = constraintA[c];
                int b = constraintB[c];
                Vector3 delta = position[b] - position[a];
                float length = delta.magnitude;
                if (length < 1e-6f) {
                    continue;
                }

                Vector3 fix = delta * (0.5f * (length - restLength[c]) / length);
                bool fixedA = pin && pinned[a];
                bool fixedB = pin && pinned[b];
                if (fixedA && fixedB) {
                    continue;
                }

                if (fixedA) {
                    position[b] -= fix * 2f;
                } else if (fixedB) {
                    position[a] += fix * 2f;
                } else {
                    position[a] += fix;
                    position[b] -= fix;
                }
            }
        }

        // 점마다 이번 스텝에 지나간 길을 쏘아, 부딪히면 그 면 위(두께 절반만큼 띄워)에 멈춘다. 닿은 점은 미끄러지지 않게 한다.
        private void Collide(Collider self) {
            float lift = thickness * 0.5f + 0.003f;
            for (int k = 0; k < position.Length; k++) {
                Vector3 from = start[k];
                Vector3 move = position[k] - from;
                float distance = move.magnitude + lift;
                if (distance < 1e-5f) {
                    continue;
                }

                Vector3 direction = move.sqrMagnitude > 1e-10f ? move.normalized : Vector3.down;
                int count = Physics.RaycastNonAlloc(from, direction, Hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                RaycastHit best = default;
                for (int h = 0; h < count; h++) {
                    if (Hits[h].collider == self || Hits[h].distance >= nearest) {
                        continue;
                    }

                    nearest = Hits[h].distance;
                    best = Hits[h];
                }

                if (nearest == float.MaxValue) {
                    continue;
                }

                position[k] = best.point + best.normal * lift;
                // 마찰: 닿은 점은 거의 멈춘다.
                previous[k] = Vector3.Lerp(previous[k], position[k], 0.85f);
            }
        }

        /// <summary>시뮬레이션 결과를 물건 로컬 메시로 옮긴다. 앞뒤 두 장을 법선 방향으로 벌려 두께를 준다.</summary>
        public void Write(Transform item) {
            for (int j = 0; j < N; j++) {
                for (int i = 0; i < N; i++) {
                    int k = Index(i, j);
                    Vector3 du = position[Index(Mathf.Min(i + 1, N - 1), j)] - position[Index(Mathf.Max(i - 1, 0), j)];
                    Vector3 dv = position[Index(i, Mathf.Min(j + 1, N - 1))] - position[Index(i, Mathf.Max(j - 1, 0))];
                    Vector3 n = Vector3.Cross(dv, du);
                    normals[k] = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
                }
            }

            for (int k = 0; k < position.Length; k++) {
                Vector3 offset = normals[k] * (thickness * 0.5f);
                vertices[k] = item.InverseTransformPoint(position[k] + offset);
                vertices[N * N + k] = item.InverseTransformPoint(position[k] - offset);
            }

            Mesh.vertices = vertices;
            Mesh.RecalculateNormals();
            Mesh.RecalculateBounds();
        }
    }
}
