using System.Collections.Generic;
using MissedCall.Core;
using MissedCall.Core.Aim;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Clutter {
    /// <summary>
    /// 더미와 바닥에 놓이는 물건 하나. 풀에서 꺼내 종류에 맞게 다시 꾸며 쓴다.
    /// 프리팹은 Clutter 레이어에 있으며 플레이어와 충돌하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ClutterItem : MonoBehaviour, IThrowable {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // 프로토타입의 조준 강조(emissive 0x3a3220)를 기본색에 더한다.
        private static readonly Color AimTint = new(0.23f, 0.2f, 0.125f, 0f);

        private Rigidbody body;
        private BoxCollider box;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private static readonly Collider[] Overlaps = new Collider[8];

        private GameTuning tuning;

        // 부드러운 물건(천, 쿠션)을 잡거나 던졌을 때 휘게 하는 상태. 원본 메시는 공유하므로 휘는 동안만 자기 메시를 쓴다.
        private static readonly Dictionary<Mesh, Vector3[]> BaseVertices = new();
        private static readonly Dictionary<Mesh, int[]> BaseTriangles = new();
        private bool soft;
        private Mesh sourceMesh;
        private Mesh bentMesh;
        private Mesh bentSource;
        private Vector3[] bentVertices;
        private float bend;
        private float bendVelocity;
        private ClothDrape drape;
        private float clothSettle;
        // 던져서 아직 아무것에도 닿지 않았는지. 이 동안만 천 가운데를 물건에 붙인다.
        private bool airborne;

        public ItemKind Item { get; private set; }
        public Vector3 Size { get; private set; }
        public Rigidbody Body => body;

        public AimKind Kind => AimKind.Item;

        /// <summary>둘러싸인 자리(바구니, 욕조)에 놓였는지. 한 번 잡으면 false가 된다.</summary>
        public bool Enclosed { get; private set; }

        public bool Held { get; private set; }
        public Vector3 Center => body.worldCenterOfMass;
        public bool Alive => gameObject.activeInHierarchy;

        private void Awake() {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        /// <summary>종류와 모양을 입혀 멈춘 상태로 놓는다. 위치는 물건의 중심이다.</summary>
        public void Spawn(ClutterCatalog.Kind kind, Mesh mesh, Material material, Vector3 center, Quaternion rotation,
            bool enclosed, GameTuning gameTuning) {
            tuning = gameTuning;
            Item = kind.kind;
            Size = kind.size;
            Enclosed = enclosed;
            Held = false;
            soft = kind.soft;
            sourceMesh = mesh;
            bend = 0f;
            bendVelocity = 0f;
            drape?.Clear();
            airborne = false;
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            SetAimed(false);
            box.size = kind.size;
            box.center = Vector3.zero;

            // 꺼진 상태에서 자리를 잡고 켜야 이전 판 위치에서 끌려오는 보간이 생기지 않는다.
            gameObject.SetActive(false);
            transform.SetPositionAndRotation(center, rotation);
            gameObject.SetActive(true);
            body.mass = kind.mass;
            body.useGravity = true;
            body.linearDamping = tuning.itemLinearDamping;
            body.angularDamping = tuning.itemAngularDamping;
            body.sleepThreshold = tuning.itemSleepThreshold;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        public void Despawn() {
            Held = false;
            gameObject.SetActive(false);
        }

        public void SetAimed(bool aimed) {
            if (!aimed) {
                meshRenderer.SetPropertyBlock(null);
                return;
            }

            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColorId, meshRenderer.sharedMaterial.color + AimTint);
            meshRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// 위에 다른 물건이 얹혀 있는지. 물건의 회전을 따르는 윗면 바로 위에 얇은 상자를 두고(가장자리는 비율만큼 안쪽),
        /// 거기 닿으면서 중심이 이 물건보다 높은 물건이 있으면 후보로 본다. 후보 중 하나라도 중심이 윗면 안쪽 위에 있거나
        /// (이 물건에 기대 있음), 후보들이 윗면 격자 3×3 중 buriedCoverage 비율 이상을 덮으면 깔린 것이다.
        /// 가장자리에 걸치기만 한 물건은 세지 않는다.
        /// </summary>
        public bool Buried {
            get {
                Transform self = transform;
                Vector3 half = Size * 0.5f;
                // 위를 가장 많이 향하는 로컬 축이 윗면이다.
                int up = 0;
                float best = -1f;
                float sign = 1f;
                for (int axis = 0; axis < 3; axis++) {
                    Vector3 direction = axis == 0 ? self.right : axis == 1 ? self.up : self.forward;
                    float dot = Vector3.Dot(direction, Vector3.up);
                    if (Mathf.Abs(dot) > best) {
                        best = Mathf.Abs(dot);
                        up = axis;
                        sign = Mathf.Sign(dot);
                    }
                }

                float height = tuning.buriedCheckHeight;
                float keep = 1f - Mathf.Clamp01(tuning.buriedCheckInset);
                Vector3 probeHalf = half * keep;
                probeHalf[up] = height * 0.5f;
                Vector3 offset = Vector3.zero;
                offset[up] = sign * (half[up] + height * 0.5f);
                Vector3 center = self.TransformPoint(offset);
                int count = Physics.OverlapBoxNonAlloc(center, probeHalf, Overlaps, self.rotation, 1 << gameObject.layer,
                    QueryTriggerInteraction.Ignore);
                float myHeight = Center.y;
                int a = (up + 1) % 3;
                int b = (up + 2) % 3;
                int candidates = 0;
                for (int i = 0; i < count; i++) {
                    Collider other = Overlaps[i];
                    if (other == box || other.bounds.center.y <= myHeight) {
                        continue;
                    }

                    Overlaps[candidates++] = other;
                    Vector3 local = self.InverseTransformPoint(other.bounds.center);
                    if (Mathf.Abs(local[a]) < probeHalf[a] && Mathf.Abs(local[b]) < probeHalf[b]) {
                        return true;
                    }
                }

                if (candidates == 0) {
                    return false;
                }

                int covered = 0;
                for (int i = -1; i <= 1; i++) {
                    for (int j = -1; j <= 1; j++) {
                        Vector3 point = offset;
                        point[up] = sign * half[up];
                        point[a] = i * probeHalf[a];
                        point[b] = j * probeHalf[b];
                        Vector3 world = self.TransformPoint(point);
                        for (int k = 0; k < candidates; k++) {
                            if ((Overlaps[k].ClosestPoint(world) - world).sqrMagnitude <= height * height) {
                                covered++;
                                break;
                            }
                        }
                    }
                }

                return covered >= Mathf.CeilToInt(9f * tuning.buriedCoverage);
            }
        }

        public void BeginHold() {
            Held = true;
            Enclosed = false;
            body.useGravity = false;
            body.angularDamping = tuning.holdAngularDamping;
            body.WakeUp();
        }

        public void Drive(Vector3 velocity) {
            body.WakeUp();
            body.linearVelocity = velocity;
        }

        public void Release(Vector3 velocity, Vector3 angularVelocity) {
            Held = false;
            airborne = true;
            body.useGravity = true;
            body.angularDamping = tuning.itemAngularDamping;
            body.WakeUp();
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
        }

        private void OnDestroy() {
            if (bentMesh != null) {
                Destroy(bentMesh);
            }

            if (drape != null) {
                Destroy(drape.Mesh);
            }
        }

        private void LateUpdate() {
            if (!soft || tuning == null) {
                return;
            }

            if (Item == ItemKind.Cushion) {
                UpdateCushionBend();
            } else {
                UpdateCloth();
            }
        }

        /// <summary>
        /// 천(셔츠, 담요): 잡거나 날아가는 동안 펼친 천 한 장으로 바꿔 시뮬레이션한다(<see cref="ClothDrape"/>).
        /// 잡힌 동안과 날아가는 동안은 가운데를 물건에 붙여 늘어진다. 떨어지면 가운데를 풀어 가구, 바닥, 다른 물건 위에
        /// 닿은 모양대로 걸치게 두고, 가라앉으면(또는 clothSettleTime이 지나면) 그 모양을 굳힌다. 다시 집으면 그 모양에서 이어 간다.
        /// 새 판에서 다시 쓰이기 전까지 갠 모양으로 돌아가지 않는다.
        /// </summary>
        private void UpdateCloth() {
            // 던진 뒤 처음 무언가에 닿을 때까지만 '날아가는 중'이다. 그 뒤에 물건이 튀거나 미끄러져도 다시 붙이지 않는다
            // (붙이면 천 가운데가 물건 중심으로 홱 끌려가 발작하듯 보였다).
            bool moving = Held || airborne;
            if (!moving && (drape == null || !drape.Active)) {
                // 안 건드렸으면 갠 모양, 굳힌 뒤면 그 모양 그대로 (메시가 물건 로컬이라 물건을 따라다닌다).
                return;
            }

            drape ??= new ClothDrape();
            if (!drape.Active) {
                if (drape.HasShape) {
                    drape.Resume(transform);
                } else {
                    // 갠 천을 펼친 한 장. 두께는 갠 겹 하나 정도.
                    drape.Begin(transform, Size, Mathf.Clamp(Size.y * 0.4f, 0.018f, 0.03f));
                }
            }

            clothSettle = moving ? 0f : clothSettle + Time.deltaTime;
            // 들고 있거나 날아가는 동안은 가운데를 물건에 붙이고, 떨어진 뒤에는 약하게만 당겨 닿은 곳 모양대로 늘어지게 둔다.
            // 들고 있는 동안은 다른 물건과 부딪히지 않는다 (더미를 헤집으며 떨리지 않게).
            ClothDrape.Pin pin = moving ? ClothDrape.Pin.Hard : ClothDrape.Pin.Soft;
            drape.Simulate(Time.deltaTime, transform, pin, body.linearVelocity, tuning.clothDamping, tuning.clothIterations,
                box, !Held);
            drape.Write(transform);
            meshFilter.sharedMesh = drape.Mesh;
            if (!moving && (drape.Settled || clothSettle >= tuning.clothSettleTime)) {
                drape.Freeze(transform);
            }
        }

        private void OnCollisionEnter(Collision collision) {
            airborne = false;
        }

        // 무언가에 닿은 채로 놓았으면 Enter가 오지 않으므로 Stay에서도 끝낸다.
        private void OnCollisionStay(Collision collision) {
            if (!Held) {
                airborne = false;
            }
        }

        /// <summary>
        /// 쿠션: 잡으면 잡힌 곳(중심)에서 멀수록 중력 쪽으로 조금 휘고, 던지면 덜 휘며 움직이는 반대쪽이 더 늘어진다.
        /// 놓이면 스프링으로 출렁이며 펴진다. 길이를 지키도록 가장자리를 원호로 굽힌다.
        /// </summary>
        private void UpdateCushionBend() {
            float hold = tuning.cushionHoldBend;
            Vector3 velocity = body.linearVelocity;
            bool flying = !Held && !body.IsSleeping() && velocity.sqrMagnitude > 0.8f * 0.8f;
            float target = Held ? hold : flying ? hold * tuning.airBendScale : 0f;

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            bendVelocity += (tuning.bendStiffness * (target - bend) - tuning.bendDamping * bendVelocity) * dt;
            bend = Mathf.Clamp(bend + bendVelocity * dt, -0.25f, 1f);

            bool resting = !Held && Mathf.Abs(bend) < 0.002f && Mathf.Abs(bendVelocity) < 0.01f && target == 0f;
            if (resting) {
                bend = 0f;
                bendVelocity = 0f;
                if (meshFilter.sharedMesh != sourceMesh) {
                    meshFilter.sharedMesh = sourceMesh;
                }

                return;
            }

            Bend(velocity);
        }

        private void Bend(Vector3 velocity) {
            Vector3[] source = SourceVertices(sourceMesh);
            if (bentMesh == null) {
                bentMesh = new Mesh { name = "BentClutter" };
                bentMesh.MarkDynamic();
            }

            if (bentSource != sourceMesh) {
                bentSource = sourceMesh;
                bentMesh.Clear();
                bentMesh.vertices = source;
                bentMesh.triangles = BaseTriangles[sourceMesh];
                if (bentVertices == null || bentVertices.Length != source.Length) {
                    bentVertices = new Vector3[source.Length];
                }
            }

            meshFilter.sharedMesh = bentMesh;

            Quaternion rotation = transform.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);
            float reach = Mathf.Max(Size.x, Size.z) * 0.5f;
            // bend 1이면 가장자리(reach)가 90도 꺾여 똑바로 아래를 향한다.
            float curvature = bend * (Mathf.PI * 0.5f) / Mathf.Max(0.05f, reach);
            Vector3 trail = -new Vector3(velocity.x, 0f, velocity.z);
            float flutter = tuning.bendFlutter * Mathf.Clamp01(trail.magnitude / 4f);
            if (trail.sqrMagnitude > 1e-6f) {
                trail.Normalize();
            }

            for (int i = 0; i < source.Length; i++) {
                Vector3 w = rotation * source[i];
                float h = w.y;
                Vector3 flat = new(w.x, 0f, w.z);
                float d = flat.magnitude;
                float k = d > 1e-4f ? curvature * (1f + flutter * Vector3.Dot(flat / d, trail)) : 0f;
                if (Mathf.Abs(k) < 1e-4f) {
                    bentVertices[i] = source[i];
                    continue;
                }

                Vector3 u = flat / d;
                float theta = k * d;
                float sin = Mathf.Sin(theta);
                float cos = Mathf.Cos(theta);
                // 원호 위의 점 + 두께(h)는 꺾인 면의 법선 방향으로
                Vector3 p = u * (sin / k) - Vector3.up * ((1f - cos) / k) + (Vector3.up * cos + u * sin) * h;
                bentVertices[i] = inverse * p;
            }

            bentMesh.vertices = bentVertices;
            bentMesh.RecalculateNormals();
            bentMesh.RecalculateBounds();
        }

        private static Vector3[] SourceVertices(Mesh mesh) {
            if (!BaseVertices.TryGetValue(mesh, out Vector3[] vertices)) {
                vertices = mesh.vertices;
                BaseVertices[mesh] = vertices;
                BaseTriangles[mesh] = mesh.triangles;
            }

            return vertices;
        }
    }
}
