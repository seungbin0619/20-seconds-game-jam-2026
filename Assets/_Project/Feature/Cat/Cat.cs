using MissedCall.Core.Aim;
using MissedCall.Core.Audio;
using UnityEngine;

namespace MissedCall.Feature.Cat {
    /// <summary>
    /// 폰 더미 위에 웅크려 앉은 고양이. 물건처럼 조준되지만 잡히지 않는다: 잡으려 하면 야옹 하고 방 안 다른 곳으로
    /// 폴짝 뛰어 달아난다(뛰는 동안 <see cref="Alive"/>가 false라 Interaction이 손에서 놓는다).
    /// 앉아 있는 동안은 Clutter 레이어 콜라이더라서 폰 덮임으로 세어지고 폰 조준을 가린다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Cat : MonoBehaviour, IThrowable {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float hopTime = 0.45f;
        [SerializeField] private float hopHeight = 0.55f;
        [Tooltip("숨 쉬는 몸의 오르내림 폭")]
        [SerializeField] private float breath = 0.03f;
        [SerializeField] private Transform body;
        [SerializeField] private Transform tail;
        [Tooltip("뛰어 내려앉을 바닥을 고를 때 피할 가구 레이어")]
        [SerializeField] private LayerMask blockers;

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private System.Random rng;
        private Rect floor;
        private Vector3 hopFrom;
        private Vector3 hopTo;
        private Quaternion turnFrom;
        private Quaternion turnTo;
        private float hopStart = -1f;
        private float phase;

        public AimKind Kind => AimKind.Item;
        public bool Enclosed => false;
        public Vector3 Center => transform.position + Vector3.up * 0.12f;
        public bool Buried => false;
        public bool Alive => gameObject.activeInHierarchy && hopStart < 0f;

        private void Awake() {
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        /// <summary>판 준비 중 앉힌다. floor는 달아날 때 내려앉을 방 바닥(XZ).</summary>
        public void Sit(Vector3 position, float yaw, Rect room, System.Random random) {
            rng = random;
            floor = room;
            hopStart = -1f;
            phase = (float)random.NextDouble() * 10f;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            gameObject.SetActive(true);
            SetAimed(false);
        }

        public void Hide() {
            hopStart = -1f;
            gameObject.SetActive(false);
        }

        public void SetAimed(bool aimed) {
            if (renderers == null) {
                return;
            }

            foreach (Renderer r in renderers) {
                if (aimed) {
                    block.SetColor(BaseColorId, r.sharedMaterial.color + new Color(0.23f, 0.2f, 0.125f, 0f));
                    r.SetPropertyBlock(block);
                } else {
                    r.SetPropertyBlock(null);
                }
            }
        }

        // 잡으려 하면 달아난다.
        public void BeginHold() {
            SetAimed(false);
            SoundCues.PlayAt(SoundCue.Meow, Center);
            hopFrom = transform.position;
            hopTo = PickLanding();
            Vector3 away = hopTo - hopFrom;
            away.y = 0f;
            turnFrom = transform.rotation;
            turnTo = away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(away.normalized, Vector3.up) : turnFrom;
            hopStart = Time.time;
        }

        public void Drive(Vector3 velocity) {
        }

        public void Release(Vector3 velocity, Vector3 angularVelocity) {
        }

        private Vector3 PickLanding() {
            const float margin = 0.6f;
            var half = new Vector3(0.15f, 0.12f, 0.25f);
            for (int attempt = 0; attempt < 12; attempt++) {
                float x = Mathf.Lerp(floor.xMin + margin, floor.xMax - margin, (float)rng.NextDouble());
                float z = Mathf.Lerp(floor.yMin + margin, floor.yMax - margin, (float)rng.NextDouble());
                var p = new Vector3(x, 0f, z);
                if (Vector3.Distance(p, transform.position) > 0.8f
                    && !Physics.CheckBox(p + Vector3.up * (half.y + 0.02f), half, Quaternion.identity, blockers, QueryTriggerInteraction.Ignore)) {
                    return p;
                }
            }

            return new Vector3(floor.center.x, 0f, floor.center.y);
        }

        private void Update() {
            phase += Time.deltaTime;
            if (hopStart >= 0f) {
                float t = hopTime > 0f ? Mathf.Clamp01((Time.time - hopStart) / hopTime) : 1f;
                Vector3 p = Vector3.Lerp(hopFrom, hopTo, t);
                p.y += 4f * hopHeight * t * (1f - t);
                transform.SetPositionAndRotation(p, Quaternion.Slerp(turnFrom, turnTo, Mathf.Clamp01(t * 2f)));
                if (t >= 1f) {
                    hopStart = -1f;
                }
            }

            if (body != null) {
                float s = 1f + breath * Mathf.Sin(phase * 2.4f);
                body.localScale = new Vector3(body.localScale.x, baseBodyY * s, body.localScale.z);
            }

            if (tail != null) {
                tail.localRotation = tailRest * Quaternion.Euler(0f, 25f * Mathf.Sin(phase * 1.7f), 0f);
            }
        }

        private float baseBodyY = 1f;
        private Quaternion tailRest = Quaternion.identity;

        private void Start() {
            if (body != null) {
                baseBodyY = body.localScale.y;
            }

            if (tail != null) {
                tailRest = tail.localRotation;
            }
        }
    }
}
