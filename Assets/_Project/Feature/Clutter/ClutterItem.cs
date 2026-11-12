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
            body.useGravity = true;
            body.angularDamping = tuning.itemAngularDamping;
            body.WakeUp();
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
        }
    }
}
