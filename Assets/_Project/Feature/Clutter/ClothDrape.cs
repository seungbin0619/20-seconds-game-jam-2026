using System.Collections.Generic;
using UnityEngine;

namespace MissedCall.Feature.Clutter {
    /// <summary>
    /// 잡거나 던진 천(셔츠, 담요)의 겉모습을 위한 작은 천 시뮬레이션. 점 N x N 격자를 Verlet 적분하고,
    /// 이웃 점 사이 거리(가로세로, 대각선, 한 칸과 두 칸 건너)를 지켜 천처럼 늘어지게 한다.
    /// - 고정(Hard): 잡혔거나 던져서 날아가는 동안 가운데 2 x 2 점을 물건 중심에 붙인다.
    /// - 약한 고정(Soft): 떨어진 뒤에는 가운데를 물건 쪽으로 약하게만 당겨, 천이 충돌체(조준 판정) 근처에 머물게 한다.
    /// - 감쇠는 물건 속도 기준이라 천이 손을 따라 함께 움직이고 흔들림만 조금 남는다.
    /// - 점마다 가구, 바닥, 다른 물건과 부딪혀 그 위에 걸치거나 늘어진다 (자기 충돌체 무시).
    /// - 가라앉으면 모양을 물건 기준으로 굳혀(Freeze) 물건을 따라다니게 하고, 다시 움직이면 그 모양에서 이어 간다.
    /// 그릴 때는 격자를 곡선 보간(Catmull-Rom)으로 R x R로 늘리고 법선을 직접 계산해, 많이 꺾여도 삼각형이 드러나지 않게 한다.
    /// 충돌체는 따로 있고(상자), 이 시뮬레이션은 겉모습만 바꾼다. 물건마다 하나를 만들어 재사용한다.
    /// </summary>
    public sealed class ClothDrape {
        public enum Pin {
            /// <summary>가운데 2 x 2 점을 물건 중심에 붙인다 (잡혔거나 날아가는 동안).</summary>
            Hard,
            /// <summary>가운데를 물건 쪽으로 약하게만 당긴다 (떨어진 뒤).</summary>
            Soft,
            /// <summary>가운데 두 줄을 봉 위에 붙인다 (봉에 반으로 넌 수건).</summary>
            Edge,
        }

        // 걸린 옷의 어깨선 길이 (천 폭 비율)
        // 봉에 넌 천: 봉 폭의 절반(m, 옷걸이 봉 상자 폭 0.1m). 가운데 두 줄이 봉 양쪽 위 모서리에 걸친다.
        private const float BarRadius = 0.055f;

        public const int N = 12;
        private const int R = (N - 1) * 2 + 1;
        private const float FixedStep = 1f / 60f;
        private const int MaxSteps = 3;
        // 한 스텝에 움직일 수 있는 최대 거리(m). 충돌과 제약이 부딪혀 튀는 것을 막는다.
        private const float MaxStepMove = 0.08f;
        // 약한 고정: 반복마다 가운데 점을 제자리 쪽으로 당기는 비율
        private const float SoftPull = 0.08f;
        // 한 스텝에 이만큼(m) 안 움직이는 상태가 SettleSteps 동안 이어지면 가라앉은 것으로 본다.
        private const float SettleMove = 0.0006f;
        private const int SettleSteps = 20;

        private static readonly RaycastHit[] Hits = new RaycastHit[6];
        // 천 크기별로 미리 계산한 걸린 모양 (물건 로컬)
        private static readonly Dictionary<Vector3, Vector3[]> HangingShapes = new();
        private bool skipCollision;
        private static int collisionMask;

        private readonly Vector3[] position = new Vector3[N * N];
        private readonly Vector3[] previous = new Vector3[N * N];
        private readonly Vector3[] start = new Vector3[N * N];
        private readonly Vector3[] restLocal = new Vector3[N * N];
        private readonly Vector3[] frozenLocal = new Vector3[N * N];
        private readonly int[] constraintA;
        private readonly int[] constraintB;
        private readonly float[] restLength;
        private readonly int[] pins = { Index(N / 2 - 1, N / 2 - 1), Index(N / 2, N / 2 - 1), Index(N / 2 - 1, N / 2), Index(N / 2, N / 2) };
        private readonly bool[] pinned = new bool[N * N];
        private readonly bool[] edge = new bool[N * N];
        private readonly int[] edgePins = new int[N * 2];

        // 그리기용 보간 격자
        private static readonly int[,] SampleIndex = BuildSampleIndex(out SampleWeight);
        private static readonly float[,] SampleWeight;
        private readonly Vector3[] rows = new Vector3[N * R];
        private readonly Vector3[] smooth = new Vector3[R * R];
        private readonly Vector3[] smoothNormal = new Vector3[R * R];
        private readonly Vector3[] vertices = new Vector3[R * R * 2];
        private readonly Vector3[] meshNormals = new Vector3[R * R * 2];

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

            // 봉에 넌 천: 가운데 두 줄(N / 2 - 1, N / 2)을 봉 위에 붙인다.
            for (int i = 0; i < N; i++) {
                edgePins[i] = Index(i, N / 2 - 1);
                edgePins[N + i] = Index(i, N / 2);
                edge[edgePins[i]] = true;
                edge[edgePins[N + i]] = true;
            }

            // 가로세로(구조), 대각선(전단), 한 칸 건너(굽힘) 제약. 두 칸 건너까지 넣으면 너무 빳빳해서 늘어지지 않았다.
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
            Mesh.normals = meshNormals;
            Mesh.triangles = BuildTriangles();
        }

        private static int Index(int i, int j) => j * N + i;

        private static int RenderIndex(int i, int j) => j * R + i;

        // 그리기 격자의 각 칸(0..R-1)이 시뮬레이션 격자에서 쓰는 4점과 Catmull-Rom 가중치
        private static int[,] BuildSampleIndex(out float[,] weights) {
            var index = new int[R, 4];
            weights = new float[R, 4];
            for (int r = 0; r < R; r++) {
                float x = r * (N - 1f) / (R - 1f);
                int i1 = Mathf.Min(Mathf.FloorToInt(x), N - 2);
                float t = x - i1;
                float t2 = t * t;
                float t3 = t2 * t;
                float[] w = {
                    -0.5f * t3 + t2 - 0.5f * t,
                    1.5f * t3 - 2.5f * t2 + 1f,
                    -1.5f * t3 + 2f * t2 + 0.5f * t,
                    0.5f * t3 - 0.5f * t2,
                };
                for (int k = 0; k < 4; k++) {
                    index[r, k] = Mathf.Clamp(i1 - 1 + k, 0, N - 1);
                    weights[r, k] = w[k];
                }
            }

            return index;
        }

        // 앞면(위), 뒷면(아래), 그리고 둘을 잇는 테두리 띠
        private static int[] BuildTriangles() {
            var triangles = new List<int>();
            int back = R * R;
            for (int j = 0; j < R - 1; j++) {
                for (int i = 0; i < R - 1; i++) {
                    int k = RenderIndex(i, j);
                    triangles.AddRange(new[] { k, k + R, k + 1, k + 1, k + R, k + R + 1 });
                    triangles.AddRange(new[] { back + k, back + k + 1, back + k + R, back + k + 1, back + k + R + 1, back + k + R });
                }
            }

            var ring = new List<int>();
            for (int i = 0; i < R - 1; i++) {
                ring.Add(RenderIndex(i, 0));
            }

            for (int j = 0; j < R - 1; j++) {
                ring.Add(RenderIndex(R - 1, j));
            }

            for (int i = R - 1; i > 0; i--) {
                ring.Add(RenderIndex(i, R - 1));
            }

            for (int j = R - 1; j > 0; j--) {
                ring.Add(RenderIndex(0, j));
            }

            for (int r = 0; r < ring.Count; r++) {
                int a = ring[r];
                int b = ring[(r + 1) % ring.Count];
                triangles.AddRange(new[] { a, b, back + a, b, back + b, back + a });
            }

            return triangles.ToArray();
        }

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

        /// <summary>굳혀 둔 모양에서 다시 시뮬레이션을 이어 간다 (다시 집었을 때).</summary>
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

        /// <summary>
        /// 시뮬레이션을 진행한다. itemVelocity는 물건 속도로, 감쇠의 기준이 된다 (천이 물건과 함께 움직인다).
        /// hitDynamic이 false면 움직이는 물체(다른 물건)와는 부딪히지 않는다. 들고 있을 때 더미를 헤집으며 떨리지 않게 한다.
        /// </summary>
        public void Simulate(float dt, Transform item, Pin pin, Vector3 itemVelocity, float damping, int iterations,
            Collider self, bool hitDynamic) {
            if (collisionMask == 0) {
                collisionMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player");
            }

            accumulator = Mathf.Min(accumulator + dt, FixedStep * MaxSteps);
            Vector3 gravity = Physics.gravity * (FixedStep * FixedStep);
            Vector3 carried = itemVelocity * FixedStep;
            bool[] fixedSet = pin == Pin.Hard ? pinned : pin == Pin.Edge ? edge : null;
            int[] targets = pin == Pin.Edge ? edgePins : pins;
            while (accumulator >= FixedStep) {
                accumulator -= FixedStep;
                for (int k = 0; k < position.Length; k++) {
                    Vector3 current = position[k];
                    start[k] = current;
                    // 물건과 같이 움직이는 몫은 그대로 두고, 그 위에 얹힌 흔들림만 감쇠한다.
                    Vector3 velocity = carried + (current - previous[k] - carried) * damping + gravity;
                    position[k] += Vector3.ClampMagnitude(velocity, MaxStepMove);
                    previous[k] = current;
                }

                for (int iteration = 0; iteration < iterations; iteration++) {
                    SolveDistances(fixedSet);
                    foreach (int p in targets) {
                        Vector3 target = item.TransformPoint(PinLocal(p, pin));
                        position[p] = fixedSet != null ? target : Vector3.Lerp(position[p], target, SoftPull);
                    }
                }

                if (!skipCollision) {
                    Collide(self, hitDynamic);
                }

                float maxMove = 0f;
                for (int k = 0; k < position.Length; k++) {
                    maxMove = Mathf.Max(maxMove, (position[k] - start[k]).sqrMagnitude);
                }

                stillSteps = maxMove < SettleMove * SettleMove ? stillSteps + 1 : 0;
            }
        }

        // 봉에 넌 천: 가운데 두 줄을 봉의 양쪽 위 모서리에 붙인다. 로컬 x가 봉 방향이다.
        private Vector3 PinLocal(int p, Pin pin) {
            Vector3 rest = restLocal[p];
            if (pin != Pin.Edge) {
                return rest;
            }

            float side = p / N < N / 2 ? -1f : 1f;
            return new Vector3(rest.x, 0f, side * BarRadius);
        }

        /// <summary>
        /// 봉에 반으로 접어 넌 모양을 미리 만들어 굳힌다. item 원점이 봉 위 가운데이고, 로컬 x가 봉 방향이다.
        /// 가운데 두 줄을 봉 위에 붙이고 양쪽 절반을 steps 스텝 동안 늘어뜨린다. 주변과 부딪히지 않으므로 모양은 천 크기에만
        /// 달려 있어서, 크기마다 처음 한 번만 계산해 두고 다시 쓴다 (판마다 계산하면 판 시작이 0.5초 넘게 걸렸다).
        /// </summary>
        public void Hang(Transform item, Vector3 size, float sheetThickness, int steps, float damping, int iterations) {
            Begin(item, size, sheetThickness);
            if (!HangingShapes.TryGetValue(size, out Vector3[] shape)) {
                // 처음부터 봉 양쪽으로 늘어진 모양으로 놓는다 (수평에서 떨어뜨리면 흔들리며 꼬였다).
                float half = (N - 1) * 0.5f;
                for (int j = 0; j < N; j++) {
                    for (int i = 0; i < N; i++) {
                        int k = Index(i, j);
                        float along = (j - half) / (N - 1f) * size.z;
                        float side = Mathf.Sign(along);
                        float drop = Mathf.Max(0f, Mathf.Abs(along) - size.z / (N - 1f) * 0.5f);
                        var local = new Vector3(restLocal[k].x, -drop, side * BarRadius + Mathf.Sin(i * 1.3f + j * 0.7f) * 0.003f);
                        position[k] = item.TransformPoint(local);
                        previous[k] = position[k];
                    }
                }

                skipCollision = true;
                for (int s = 0; s < steps; s++) {
                    Simulate(FixedStep, item, Pin.Edge, Vector3.zero, damping, iterations, null, false);
                }

                skipCollision = false;
                shape = new Vector3[position.Length];
                for (int k = 0; k < position.Length; k++) {
                    shape[k] = item.InverseTransformPoint(position[k]);
                }

                HangingShapes[size] = shape;
            }

            for (int k = 0; k < position.Length; k++) {
                position[k] = item.TransformPoint(shape[k]);
                previous[k] = position[k];
            }

            Write(item);
            Freeze(item);
        }

        private void SolveDistances(bool[] fixedSet) {
            bool hard = fixedSet != null;
            bool[] pinned = fixedSet;
            for (int c = 0; c < constraintA.Length; c++) {
                int a = constraintA[c];
                int b = constraintB[c];
                Vector3 delta = position[b] - position[a];
                float length = delta.magnitude;
                if (length < 1e-6f) {
                    continue;
                }

                Vector3 fix = delta * (0.5f * (length - restLength[c]) / length);
                bool fixedA = hard && pinned[a];
                bool fixedB = hard && pinned[b];
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

        // 점마다 이번 스텝에 지나간 길을 쏘아, 면 쪽으로 들어가는 중이면 그 면 위(두께 절반만큼 띄워)에 멈춘다.
        // 닿은 점은 미끄러지지 않게 한다.
        private void Collide(Collider self, bool hitDynamic) {
            float lift = thickness * 0.5f + 0.003f;
            for (int k = 0; k < position.Length; k++) {
                Vector3 from = start[k];
                Vector3 move = position[k] - from;
                float length = move.magnitude;
                if (length < 1e-6f) {
                    continue;
                }

                Vector3 direction = move / length;
                int count = Physics.RaycastNonAlloc(from, direction, Hits, length + lift, collisionMask, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                RaycastHit best = default;
                for (int h = 0; h < count; h++) {
                    RaycastHit hit = Hits[h];
                    if (hit.collider == self || hit.distance >= nearest || hit.distance <= 0f) {
                        continue;
                    }

                    if (!hitDynamic && hit.rigidbody != null) {
                        continue;
                    }

                    if (Vector3.Dot(direction, hit.normal) >= 0f) {
                        continue;
                    }

                    nearest = hit.distance;
                    best = hit;
                }

                if (nearest == float.MaxValue) {
                    continue;
                }

                position[k] = best.point + best.normal * lift;
                previous[k] = Vector3.Lerp(previous[k], position[k], 0.85f);
            }
        }

        /// <summary>시뮬레이션 결과를 부드럽게 늘린 격자로 옮겨 물건 로컬 메시를 만든다. 앞뒤 두 장을 법선 방향으로 벌려 두께를 준다.</summary>
        public void Write(Transform item) {
            // 가로 먼저 보간 (N줄 x R칸), 이어서 세로 보간 (R줄 x R칸)
            for (int j = 0; j < N; j++) {
                for (int r = 0; r < R; r++) {
                    Vector3 sum = Vector3.zero;
                    for (int k = 0; k < 4; k++) {
                        sum += position[Index(SampleIndex[r, k], j)] * SampleWeight[r, k];
                    }

                    rows[j * R + r] = sum;
                }
            }

            for (int rj = 0; rj < R; rj++) {
                for (int ri = 0; ri < R; ri++) {
                    Vector3 sum = Vector3.zero;
                    for (int k = 0; k < 4; k++) {
                        sum += rows[SampleIndex[rj, k] * R + ri] * SampleWeight[rj, k];
                    }

                    smooth[RenderIndex(ri, rj)] = sum;
                }
            }

            for (int j = 0; j < R; j++) {
                for (int i = 0; i < R; i++) {
                    Vector3 du = smooth[RenderIndex(Mathf.Min(i + 1, R - 1), j)] - smooth[RenderIndex(Mathf.Max(i - 1, 0), j)];
                    Vector3 dv = smooth[RenderIndex(i, Mathf.Min(j + 1, R - 1))] - smooth[RenderIndex(i, Mathf.Max(j - 1, 0))];
                    Vector3 n = Vector3.Cross(dv, du);
                    smoothNormal[RenderIndex(i, j)] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
                }
            }

            Quaternion toLocal = Quaternion.Inverse(item.rotation);
            for (int k = 0; k < smooth.Length; k++) {
                Vector3 n = smoothNormal[k];
                Vector3 offset = n * (thickness * 0.5f);
                vertices[k] = item.InverseTransformPoint(smooth[k] + offset);
                vertices[R * R + k] = item.InverseTransformPoint(smooth[k] - offset);
                Vector3 localNormal = toLocal * n;
                meshNormals[k] = localNormal;
                meshNormals[R * R + k] = -localNormal;
            }

            Mesh.vertices = vertices;
            Mesh.normals = meshNormals;
            Mesh.RecalculateBounds();
        }
    }
}
