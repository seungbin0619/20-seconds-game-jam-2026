using System.Diagnostics;
using MissedCall.Core;
using MissedCall.Core.Rooms;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Clutter {
    /// <summary>
    /// 판 준비 단계: 모든 숨는 자리에 물건 더미를 쌓고, 방마다 바닥에 물건을 흩뿌린 뒤,
    /// 물리를 정해진 스텝만큼 돌려 가라앉힌다. Layout 다음, Phone 앞에 둔다.
    /// 폰이 들어갈 자리는 고르지 않는다 (Phone 단계). 무작위는 <see cref="RoundStream.Clutter"/>에서만 뽑는다.
    /// </summary>
    public sealed class ClutterStep : RoundStep {
        [SerializeField] private ClutterCatalog catalog;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private ClutterItem prefab;
        [Tooltip("물건을 둘 부모. 비우면 이 오브젝트")]
        [SerializeField] private Transform root;
        [SerializeField] private int prewarm = 64;
        [Tooltip("물건이 겹치면 안 되는 레이어 (벽, 가구). Player와 Clutter는 빼둔다")]
        [SerializeField] private LayerMask blockers = ~0;
        [SerializeField] private int scatterAttempts = 12;

        // 물건 사이와 바닥 위에 두는 틈(m). 시작부터 겹쳐서 튀어 오르지 않게 한다.
        private const float Gap = 0.01f;
        // 겹침을 밀어낼 때 더 띄우는 거리(m).
        private const float FitMargin = 0.005f;
        private const int FitIterations = 8;
        // 옆으로 밀 거리를 잴 때 상자를 위아래로 늘이는 길이(m).
        private const float TallProbeExtra = 1f;
        private const float PenetrationTolerance = 0.001f;

        private readonly Collider[] overlaps = new Collider[16];
        private ClutterPool pool;
        private ClutterLook look;
        private BoxCollider probe;

        /// <summary>이번 판에 놓은 물건 수.</summary>
        public int Count => pool?.Used ?? 0;

        /// <summary>자리가 맞지 않아 대체 물건을 깔거나 빠뜨린 수. 방 모듈 데이터 확인용.</summary>
        public int Misfits { get; private set; }

        /// <summary>마지막 안정화에 걸린 시간(ms). 성능 확인용.</summary>
        public double LastSettleMs { get; private set; }

        /// <summary>이번 판의 물건 (앞에서부터 <see cref="Count"/>개).</summary>
        public ClutterPool Pool => pool;

        private void Awake() {
            // 겹침 계산(ComputePenetration)에만 쓰는 상자. 꺼진 콜라이더는 계산되지 않으므로 켜 두되,
            // 트리거로 만들어 멀리 두고 Clutter 레이어에 넣어 다른 질의(blockers)에 잡히지 않게 한다.
            var probeObject = new GameObject("FitProbe") { layer = prefab.gameObject.layer };
            probeObject.transform.SetParent(transform, false);
            probeObject.transform.position = new Vector3(0f, -1000f, 0f);
            probe = probeObject.AddComponent<BoxCollider>();
            probe.isTrigger = true;
        }

        private void OnDestroy() {
            look?.Dispose();
        }

        public override void Prepare(RoundContext context) {
            pool ??= new ClutterPool(prefab, root != null ? root : transform, prewarm);
            look ??= new ClutterLook(catalog);
            pool.ReleaseAll();
            Misfits = 0;

            RoomGraph graph = context.Graph;
            if (graph == null) {
                return;
            }

            // 이전 판 물건이 꺼진 상태를 물리 질의에 반영한다.
            Physics.SyncTransforms();
            System.Random rng = context.Seed.Stream(RoundStream.Clutter);
            for (int room = 0; room < graph.Rooms.Count; room++) {
                foreach (HideSpot spot in graph.Rooms[room].HideSpots) {
                    BuildPile(spot, rng);
                }
            }

            for (int room = 0; room < graph.Rooms.Count; room++) {
                Scatter(graph.Footprint(room), rng);
            }

            Settle();
        }

        private void BuildPile(HideSpot spot, System.Random rng) {
            Transform t = spot.transform;
            Quaternion baseRotation = t.rotation * Quaternion.Euler(0f, Range(rng, -catalog.baseSpin, catalog.baseSpin), 0f);
            ClutterCatalog.Kind baseKind = catalog.Get(spot.BaseKind);
            Vector3 center = CenterOn(t.position, t.position.y, baseKind);
            if (!TryFitOrStraighten(baseKind.size, ref center, ref baseRotation, t.rotation)) {
                // 자리보다 큰 물건이면 작은 물건으로 바꿔 깐다. 맨 아래 물건은 폰을 덮으므로 빠뜨리지 않는다.
                baseKind = catalog.Get(catalog.fallbackBase);
                center = CenterOn(t.position, t.position.y, baseKind);
                TryFitOrStraighten(baseKind.size, ref center, ref baseRotation, t.rotation);
                Misfits++;
            }

            Spawn(baseKind, center, baseRotation, spot.Enclosed, rng);
            // 위로 쌓는 물건은 밀려난 맨 아래 물건을 기준으로 쌓는다.
            Vector3 pileCenter = center;
            float y = center.y + baseKind.size.y * 0.5f + Gap;

            Vector2Int range = spot.PileCount;
            int total = rng.Next(range.x, range.y + 1);
            ItemKind[] mix = spot.Enclosed ? catalog.enclosedMix : catalog.pileMix;
            float jitter = spot.Enclosed ? 0f : catalog.pileJitter;
            for (int i = 1; i < total; i++) {
                ClutterCatalog.Kind kind = catalog.Get(mix[rng.Next(mix.Length)]);
                Quaternion rotation = t.rotation * Quaternion.Euler(0f, Range(rng, -catalog.pileSpin, catalog.pileSpin), 0f);
                Vector3 offset = t.rotation * new Vector3(Range(rng, -jitter, jitter), 0f, Range(rng, -jitter, jitter));
                Vector3 itemCenter = CenterOn(pileCenter + offset, y, kind);
                if (!TryFitOrStraighten(kind.size, ref itemCenter, ref rotation, t.rotation)) {
                    Misfits++;
                    continue;
                }

                Spawn(kind, itemCenter, rotation, spot.Enclosed, rng);
                y = itemCenter.y + kind.size.y * 0.5f + Gap;
            }
        }

        private void Scatter(Rect footprint, System.Random rng) {
            Vector2Int range = tuning.scatterPerRoom;
            int count = rng.Next(range.x, range.y + 1);
            float margin = catalog.scatterWallMargin;
            Rect inner = Rect.MinMaxRect(footprint.xMin + margin, footprint.yMin + margin,
                footprint.xMax - margin, footprint.yMax - margin);
            int mask = blockers | (1 << prefab.gameObject.layer);
            for (int i = 0; i < count; i++) {
                ClutterCatalog.Kind kind = catalog.Get(catalog.scatterMix[rng.Next(catalog.scatterMix.Length)]);
                for (int attempt = 0; attempt < scatterAttempts; attempt++) {
                    float x = Range(rng, inner.xMin, inner.xMax);
                    float z = Range(rng, inner.yMin, inner.yMax);
                    Quaternion rotation = Quaternion.Euler(0f, Range(rng, -180f, 180f), 0f);
                    Vector3 center = new(x, kind.size.y * 0.5f + Gap, z);
                    // 주변에 틈을 둔 상자로 가구, 벽, 다른 물건과의 겹침을 본다.
                    Vector3 halfExtents = kind.size * 0.5f + new Vector3(0.03f, 0f, 0.03f);
                    if (Physics.CheckBox(center, halfExtents, rotation, mask, QueryTriggerInteraction.Ignore)) {
                        continue;
                    }

                    Spawn(kind, center, rotation, false, rng);
                    break;
                }
            }
        }

        private static Vector3 CenterOn(Vector3 position, float floorY, ClutterCatalog.Kind kind) =>
            new(position.x, floorY + kind.size.y * 0.5f + Gap * 0.5f, position.z);

        /// <summary>
        /// 무작위로 돌린 채 맞춰 보고, 안 들어가면 자리 방향 그대로(돌리지 않고) 다시 맞춘다.
        /// 바구니처럼 물건과 폭이 거의 같은 자리는 조금만 돌려도 모서리가 벽에 걸린다.
        /// </summary>
        private bool TryFitOrStraighten(Vector3 size, ref Vector3 center, ref Quaternion rotation, Quaternion straight) {
            Vector3 start = center;
            if (TryFit(size, ref center, rotation)) {
                return true;
            }

            center = start;
            rotation = straight;
            return TryFit(size, ref center, rotation);
        }

        /// <summary>
        /// 벽이나 가구와 겹치지 않게 물건 중심을 밀어낸다. 얇은 바닥(러그)과 겹치면 위로, 벽이나 등받이와 겹치면 옆으로 민다.
        /// 끝내 겹치면 false.
        /// </summary>
        private bool TryFit(Vector3 size, ref Vector3 center, Quaternion rotation) {
            Vector3 half = size * 0.5f;
            // 옆으로 밀 거리를 잴 때 쓰는 키 큰 상자. 위아래로 길게 늘이면 가장 짧은 탈출 방향이 수평이 된다.
            Vector3 tall = new(size.x, size.y + TallProbeExtra * 2f, size.z);
            for (int iteration = 0; iteration < FitIterations; iteration++) {
                int count = Physics.OverlapBoxNonAlloc(center, half, overlaps, rotation, blockers, QueryTriggerInteraction.Ignore);
                if (count == 0) {
                    return true;
                }

                Vector3 push = Vector3.zero;
                probe.size = tall;
                for (int i = 0; i < count; i++) {
                    Collider other = overlaps[i];
                    float top = other.bounds.max.y;
                    if (top <= center.y) {
                        // 윗면이 물건 가운데보다 낮은 것(러그, 욕조 바닥, 의자 좌석)은 그 위로 올린다.
                        push.y = Mathf.Max(push.y, top - (center.y - half.y));
                        continue;
                    }

                    // 등받이, 벽, 욕조 테두리는 옆으로 민다.
                    if (!Physics.ComputePenetration(probe, center, rotation, other, other.transform.position,
                            other.transform.rotation, out Vector3 direction, out float distance)) {
                        continue;
                    }

                    Vector3 move = new Vector3(direction.x, 0f, direction.z) * distance;
                    // 여러 면에 걸치면 축마다 가장 큰 밀어냄을 쓴다 (같은 방향을 두 번 더하지 않게).
                    push.x = MaxAbs(push.x, move.x);
                    push.z = MaxAbs(push.z, move.z);
                }

                probe.size = size;
                // 겹침 검사는 접촉 거리 안에 닿기만 해도 잡는다. 밀 거리가 없으면 실제로 박혔는지만 확인한다.
                if (push.sqrMagnitude < 1e-8f) {
                    return !Penetrates(center, rotation);
                }

                center += push + push.normalized * FitMargin;
            }

            probe.size = size;
            return !Penetrates(center, rotation);
        }

        private bool Penetrates(Vector3 center, Quaternion rotation) {
            int count = Physics.OverlapBoxNonAlloc(center, probe.size * 0.5f, overlaps, rotation, blockers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) {
                Collider other = overlaps[i];
                // 맞닿기만 한 경우(거리 0)도 true가 나오므로 1mm 넘게 박힌 것만 센다.
                if (Physics.ComputePenetration(probe, center, rotation, other, other.transform.position,
                        other.transform.rotation, out _, out float distance) && distance > PenetrationTolerance) {
                    return true;
                }
            }

            return false;
        }

        private void Spawn(ClutterCatalog.Kind kind, Vector3 center, Quaternion rotation, bool enclosed, System.Random rng) {
            Mesh mesh = look.PickMesh(kind, rng);
            Material material = look.PickMaterial(kind, rng);
            pool.Take().Spawn(kind, mesh, material, center, rotation, enclosed, tuning);
            // 다음 물건의 겹침 검사가 방금 놓은 물건을 보게 한다.
            Physics.SyncTransforms();
        }

        // 프로토타입처럼 시작 전에 물리를 돌려 더미를 가라앉힌다. 그동안 다른 물체(플레이어)는 움직이지 않는다.
        private void Settle() {
            var watch = Stopwatch.StartNew();
            SimulationMode previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            float step = Time.fixedDeltaTime;
            for (int i = 0; i < tuning.settleSteps; i++) {
                Physics.Simulate(step);
            }

            Physics.simulationMode = previous;
            LastSettleMs = watch.Elapsed.TotalMilliseconds;
        }

        private static float MaxAbs(float a, float b) => Mathf.Abs(b) > Mathf.Abs(a) ? b : a;

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
