using MissedCall.Core;
using MissedCall.Core.Aim;
using MissedCall.Core.Audio;
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
        [Tooltip("플립폰일 때 시각 요소 크기 배율 (짧고 두껍다)")]
        [SerializeField] private Vector3 flipScale = new(0.9f, 2.2f, 0.62f);
        [Tooltip("덮임을 세고 진동으로 튀게 할 물건 레이어")]
        [SerializeField] private LayerMask clutterMask;
        [Tooltip("진동으로 기어갈 때 딛는 표면과 막는 벽, 가구 레이어. 물건과 플레이어는 뺀다")]
        [SerializeField] private LayerMask surfaceMask = 1;

        private readonly Collider[] hits = new Collider[64];
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private RoundContext context;
        private System.Random rng;
        private float nextJiggle;
        private bool visible = true;
        // 진동으로 기어가는 방향(월드 yaw, 도)과 이번 진동이 끝나는 판 시각.
        private float crawlYaw;
        private float buzzEnd;
        // 표면 끝에서 떨어지는 중. 떨어지는 속도와 닿을 높이.
        private bool falling;
        private float fallSpeed;
        private float fallTo;

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

        /// <summary>이번 판에 기어간 거리(m)와 떨어진 횟수. 확인용.</summary>
        public float Crawled { get; private set; }
        public int Falls { get; private set; }

        private void Awake() {
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        /// <summary>새 판에 폰을 놓는다. 이전 판에서 숨겼거나 화면이 빨개졌어도 여기서 되돌린다.</summary>
        public void Place(RoundContext round, Vector3 surface, Quaternion rotation, int room) {
            context = round;
            rng = round.Seed.Stream(RoundStream.Vibration);
            nextJiggle = tuning.jiggleDelay;
            Jiggles = 0;
            LastJiggled = 0;
            Crawled = 0f;
            Falls = 0;
            falling = false;
            buzzEnd = 0f;
            Aimed = false;
            transform.SetPositionAndRotation(surface, rotation);
            visual.localPosition = Vector3.zero;
            gameObject.SetActive(true);
            SetVisible(true);

            round.Phone = this;
            round.PhonePosition = Center;
            round.PhoneRoom = room;
            round.Covers = CountCovers();
            crawlYaw = rotation.eulerAngles.y + (float)rng.NextDouble() * 360f;
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
            visual.localScale = context.FlipPhone ? flipScale : Vector3.one;
            // 받기 화면에서만 숨긴다. 결과 화면에서는 폰이 다시 보인다.
            SetVisible(state != RoundState.Answer);
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

            // 배터리 부족 변수: 꺼진 폰은 떨지도 빛나지도 않는다.
            if (context.BatteryDead) {
                visual.localPosition = Vector3.zero;
                SetScreen(idleColor);
                return;
            }

            if (context.Elapsed >= nextJiggle) {
                Jiggle();
                buzzEnd = context.Elapsed + tuning.jiggleInterval * tuning.crawlDuty;
                // 진동마다 방향이 조금씩 틀어진다.
                crawlYaw += Range(tuning.crawlTurn);
                nextJiggle += Mathf.Max(0.05f, tuning.jiggleInterval);
            }

            Crawl(covers, Time.deltaTime);

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

        /// <summary>
        /// 진동하는 동안 폰이 표면 위를 조금씩 미끄러진다. 위에 물건이 crawlMaxCovers개 넘게 얹혀 있으면 눌려서 못 움직인다.
        /// 벽이나 가구에 막히면 방향을 바꾸고, 표면 끝을 넘으면 아래 표면(대개 바닥)으로 떨어진다.
        /// 폰은 콜라이더가 없으므로 레이로 표면을 따라간다. 물건(Clutter)은 표면으로 보지 않는다.
        /// </summary>
        private void Crawl(int covers, float dt) {
            if (falling) {
                Fall(dt);
                return;
            }

            float scale = context.VibrationMode ? 1f : tuning.ringCrawlScale;
            if (scale <= 0f || covers > tuning.crawlMaxCovers || context.Elapsed >= buzzEnd || dt <= 0f) {
                return;
            }

            Vector3 direction = Quaternion.Euler(0f, crawlYaw, 0f) * Vector3.forward;
            float step = tuning.crawlSpeed * scale * dt;
            Vector3 center = Center;
            // 앞이 막혔으면 반대쪽 반원 중 한 방향으로 돌린다.
            if (Physics.Raycast(center, direction, step + tuning.crawlWallMargin, surfaceMask, QueryTriggerInteraction.Ignore)) {
                crawlYaw += 180f + Range(70f);
                return;
            }

            Vector3 next = transform.position + direction * step;
            if (context.Graph != null && context.Graph.RoomAt(next) < 0) {
                crawlYaw += 180f + Range(70f);
                return;
            }

            float surface = transform.position.y;
            Vector3 origin = new(next.x, surface + tuning.crawlStepUp, next.z);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, surfaceMask, QueryTriggerInteraction.Ignore)) {
                crawlYaw += 180f + Range(70f);
                return;
            }

            transform.position = new Vector3(next.x, transform.position.y, next.z);
            transform.rotation = Quaternion.Euler(0f, tuning.crawlSpin * scale * dt, 0f) * transform.rotation;
            Crawled += step;
            if (hit.point.y < surface - tuning.crawlStepUp) {
                // 끝을 넘었다. 떨어진다.
                falling = true;
                fallSpeed = 0f;
                fallTo = hit.point.y;
            } else {
                transform.position = new Vector3(next.x, hit.point.y, next.z);
            }

            UpdatePlace();
        }

        private void Fall(float dt) {
            fallSpeed += -Physics.gravity.y * dt;
            Vector3 p = transform.position;
            p.y = Mathf.Max(fallTo, p.y - fallSpeed * dt);
            transform.position = p;
            if (p.y <= fallTo) {
                falling = false;
                Falls++;
                // 떨어진 높이만큼 크게. 진동으로 튀는 소리와 달리 한 번뿐이라 단서로 둔다.
                SoundCues.PlayAt(SoundCue.ImpactHard, Center, Mathf.Clamp01(fallSpeed / tuning.impactFullSpeed + 0.2f));
            }

            UpdatePlace();
        }

        // 다른 기능이 읽는 폰 위치와 방을 갱신한다.
        private void UpdatePlace() {
            context.PhonePosition = Center;
            if (context.Graph != null) {
                int room = context.Graph.RoomAt(Center);
                if (room >= 0) {
                    context.PhoneRoom = room;
                }
            }
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
