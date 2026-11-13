using MissedCall.Core;
using MissedCall.Core.Aim;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Phone {
    /// <summary>
    /// 숨겨진 폰. 판 동안 덮임 수를 세고, 주변 물건을 진동으로 튀게 하고, 화면을 깜박인다.
    /// 물리 콜라이더가 없다. 조준은 Interaction이 <see cref="GrabPoint"/>와 화면 중심 레이의 거리로 한다.
    /// 원점은 폰 바닥이 놓이는 표면이다.
    /// </summary>
    public sealed class PhoneDevice : MonoBehaviour, IGrabbable {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // 원점(표면)에서 폰 중심까지 높이(m). 몸체 두께 0.014의 절반 + 바닥과의 틈 0.005.
        private const float CenterHeight = 0.012f;
        // 덮임 후보를 찾는 기둥의 여유(m). 기울어진 물건이 기둥에 닿지 않아 빠지지 않게 넉넉히 잡는다.
        private const float CoverQuerySlack = 0.3f;

        [SerializeField] private GameTuning tuning;
        [Tooltip("떨림을 주는 시각 요소의 부모. 원점은 그대로 두고 이것만 흔든다")]
        [SerializeField] private Transform visual;
        [SerializeField] private Renderer screen;
        [SerializeField] private Color ringColor = new(0.55f, 0.9f, 1f);
        [Tooltip("받지 못했을 때 화면 색 (프로토타입 0xff6b57)")]
        [SerializeField] private Color missedColor = new(1f, 0.42f, 0.34f);
        [SerializeField] private Color idleColor = new(0.08f, 0.1f, 0.12f);
        [Tooltip("받지 못했을 때만 켜는 빨간 빛. 더미에 덮인 폰도 틈으로 화면 빛이 보이게 한다. 없어도 된다")]
        [SerializeField] private Light missedGlow;
        [Tooltip("받지 못했을 때만 켜는 형상. 벽과 물건 위에 단색으로 그린다 (PhoneXRay 셰이더)")]
        [SerializeField] private Renderer[] xray = new Renderer[0];
        [Tooltip("형상의 부모. 원점이 폰 중심이다. 받지 못했을 때 플레이어 쪽으로 세우고 키운다")]
        [SerializeField] private Transform xrayRoot;
        [Tooltip("덮임을 세고 진동으로 튀게 할 물건 레이어")]
        [SerializeField] private LayerMask clutterMask;

        private readonly Collider[] hits = new Collider[64];
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private RoundContext context;
        private System.Random rng;
        private float nextJiggle;
        private bool visible = true;

        public AimKind Kind => AimKind.Phone;

        /// <summary>폰 중심 (월드). 덮임과 진동 판정의 기준이다.</summary>
        public Vector3 Center => transform.TransformPoint(0f, CenterHeight, 0f);
        public Vector3 GrabPoint => Center;

        public bool Aimed { get; private set; }

        /// <summary>판에 놓였는지.</summary>
        public bool Placed => context != null;

        /// <summary>진동 횟수와 마지막으로 튀긴 물건 수. 확인용.</summary>
        public int Jiggles { get; private set; }
        public int LastJiggled { get; private set; }

        private void Awake() {
            // 형상(xray)은 숨기기·보이기와 따로 켜고 끈다.
            var list = new System.Collections.Generic.List<Renderer>(GetComponentsInChildren<Renderer>(true));
            foreach (Renderer r in xray) {
                list.Remove(r);
                r.enabled = false;
            }

            renderers = list.ToArray();
            block = new MaterialPropertyBlock();
        }

        /// <summary>새 판에 폰을 놓는다. 이전 판에서 숨겼거나 화면이 빨개졌어도 여기서 되돌린다.</summary>
        public void Place(RoundContext round, Vector3 surface, Quaternion rotation, int room) {
            context = round;
            rng = round.Seed.Stream(RoundStream.Vibration);
            nextJiggle = tuning.jiggleDelay;
            Jiggles = 0;
            LastJiggled = 0;
            Aimed = false;
            transform.SetPositionAndRotation(surface, rotation);
            visual.localPosition = Vector3.zero;
            gameObject.SetActive(true);
            SetVisible(true);

            round.Phone = this;
            round.PhonePosition = Center;
            round.PhoneRoom = room;
            round.Covers = CountCovers();
            SetScreen(ringColor);
        }

        /// <summary>판에서 뺀다. 놓을 자리가 없는 판이나 준비 시작 때 쓴다.</summary>
        public void Hide() {
            context = null;
            gameObject.SetActive(false);
        }

        public void SetAimed(bool aimed) => Aimed = aimed;

        /// <summary>집었다. 받기 화면 동안 폰을 숨긴다 (손에 든 것으로 본다).</summary>
        public void Grab() {
            Aimed = false;
            SetVisible(false);
        }

        private void Update() {
            if (context == null) {
                return;
            }

            RoundState state = context.State;
            // 받기 화면에서만 숨긴다. 결과 화면에서는 폰이 다시 보인다.
            SetVisible(state != RoundState.Answer);
            bool missed = state == RoundState.Missed;
            if (missedGlow != null) {
                missedGlow.enabled = missed;
            }

            foreach (Renderer r in xray) {
                r.enabled = missed;
            }

            if (missed) {
                FaceXRayToPlayer();
            }

            if (!GameFlow.IsRunning(state)) {
                visual.localPosition = Vector3.zero;
                SetScreen(state == RoundState.Missed ? missedColor : idleColor);
                return;
            }

            int covers = CountCovers();
            context.Covers = covers;
            if (state != RoundState.Playing) {
                return;
            }

            if (context.Elapsed >= nextJiggle) {
                Jiggle();
                nextJiggle += Mathf.Max(0.05f, tuning.jiggleInterval);
            }

            float shake = covers == 0 ? Mathf.Sin(Time.time * tuning.phoneShakeRate) * tuning.phoneShake : 0f;
            visual.localPosition = new Vector3(shake, 0f, 0f);
            bool bright = Mathf.Sin(Time.unscaledTime * tuning.screenBlinkRate) > 0f;
            SetScreen(bright ? ringColor : ringColor * tuning.screenBlinkDim);
        }

        /// <summary>
        /// 폰을 덮은 물건 수. 중심이 폰 중심 기준 높이 -coverBelow ~ +coverAbove 안에 있고,
        /// 그 물건의 로컬 가로세로 범위를 coverMargin만큼 넓혔을 때 폰 중심이 그 안에 드는 물건을 센다 (프로토타입 countCovers).
        /// </summary>
        public int CountCovers() {
            Vector3 phone = Center;
            float below = tuning.coverBelow;
            float above = tuning.coverAbove;
            float side = tuning.coverMargin + CoverQuerySlack;
            Vector3 queryCenter = phone + Vector3.up * ((above - below) * 0.5f);
            var queryHalf = new Vector3(side, (above + below) * 0.5f + CoverQuerySlack, side);
            int count = Physics.OverlapBoxNonAlloc(queryCenter, queryHalf, hits, Quaternion.identity, clutterMask,
                QueryTriggerInteraction.Ignore);
            int covers = 0;
            for (int i = 0; i < count; i++) {
                if (hits[i] is not BoxCollider box) {
                    continue;
                }

                Transform t = box.transform;
                float dy = t.TransformPoint(box.center).y - phone.y;
                if (dy < -below || dy > above) {
                    continue;
                }

                Vector3 local = t.InverseTransformPoint(phone) - box.center;
                Vector3 half = box.size * 0.5f;
                if (Mathf.Abs(local.x) < half.x + tuning.coverMargin && Mathf.Abs(local.z) < half.z + tuning.coverMargin) {
                    covers++;
                }
            }

            return covers;
        }

        // 폰 주변 물건을 살짝 튀게 한다 (프로토타입 vibrate). 벨이 울리는 판에서는 약하게, 진동 모드에서는 원래 세기로.
        private void Jiggle() {
            float strength = context.VibrationMode ? 1f : tuning.ringJiggleScale;
            Jiggles++;
            LastJiggled = 0;
            if (strength <= 0f) {
                return;
            }

            Vector3 phone = Center;
            float radius = tuning.jiggleRadius;
            float height = tuning.jiggleHeight;
            float reach = Mathf.Sqrt(radius * radius + height * height);
            int count = Physics.OverlapSphereNonAlloc(phone, reach, hits, clutterMask, QueryTriggerInteraction.Ignore);
            Vector3 spin = tuning.jiggleSpin * strength;
            int jiggled = 0;
            for (int i = 0; i < count; i++) {
                Rigidbody body = hits[i].attachedRigidbody;
                if (body == null || body.isKinematic) {
                    continue;
                }

                Vector3 offset = body.position - phone;
                if (Mathf.Abs(offset.y) >= height || new Vector2(offset.x, offset.z).sqrMagnitude >= radius * radius) {
                    continue;
                }

                body.WakeUp();
                Vector3 velocity = body.linearVelocity;
                velocity.y += tuning.jiggle * strength;
                body.linearVelocity = velocity;
                body.angularVelocity = new Vector3(Range(spin.x), Range(spin.y), Range(spin.z));
                jiggled++;
            }

            LastJiggled = jiggled;
        }

        // 폰은 바닥에 눕혀 있어서 눈높이에서 보면 얇은 선으로만 보인다.
        // 형상은 화면이 플레이어를 향하게 세우고(긴 변이 위아래) 키워서 멀리서도 폰 모양으로 읽히게 한다.
        private void FaceXRayToPlayer() {
            if (xrayRoot == null) {
                return;
            }

            Vector3 center = Center;
            Vector3 toPlayer = context.PlayerPosition - center;
            if (toPlayer.sqrMagnitude < 0.0001f) {
                return;
            }

            toPlayer.Normalize();
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, toPlayer);
            if (up.sqrMagnitude < 0.0001f) {
                up = Vector3.ProjectOnPlane(Vector3.forward, toPlayer);
            }

            // 폰 로컬 축: y = 화면 법선, z = 긴 변.
            xrayRoot.SetPositionAndRotation(center, Quaternion.LookRotation(up.normalized, toPlayer));
            xrayRoot.localScale = Vector3.one * tuning.missedXRayScale;
        }

        private void SetVisible(bool value) {
            if (visible == value) {
                return;
            }

            visible = value;
            foreach (Renderer r in renderers) {
                r.enabled = value;
            }
        }

        private void SetScreen(Color color) {
            block.SetColor(BaseColorId, color);
            screen.SetPropertyBlock(block);
        }

        private float Range(float limit) => (float)(rng.NextDouble() * 2.0 - 1.0) * limit;
    }
}
