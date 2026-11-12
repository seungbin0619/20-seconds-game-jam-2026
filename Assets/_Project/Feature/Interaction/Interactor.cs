using System;
using MissedCall.Core;
using MissedCall.Core.Aim;
using MissedCall.Core.Round;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Interaction {
    /// <summary>
    /// 화면 중심 조준과 잡아 던지기. 누르면 조준한 물건을 카메라 앞 잡는 점으로 끌어오고,
    /// 떼면 잡는 점이 직전에 움직이던 속도(휘두른 속도)에 기본 토스를 더해 던진다.
    /// 시선은 잡은 동안에도 돌아가므로 시선을 휙 돌리며 떼면 그 방향으로 날아간다.
    /// 물건은 순간이동하지 않고 속도로 끌려오므로 벽을 뚫지 않고, 가구에 걸려 멀어지면 놓친다.
    /// </summary>
    public sealed class Interactor : MonoBehaviour {
        private const int SampleCount = 32;
        private const float LiftTolerance = 0.05f;

        [SerializeField] private GameFlow flow;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private Camera view;
        [Tooltip("IPointerGate를 구현한 컴포넌트 (PointerLook). 잠금을 얻는 클릭을 잡기로 쓰지 않는다")]
        [SerializeField] private MonoBehaviour pointerGate;
        [Tooltip("조준 레이가 맞힐 레이어. 플레이어 자신은 뺀다")]
        [SerializeField] private LayerMask aimMask = ~0;
        [Tooltip("잡는 점을 가로막는 레이어 (벽, 가구). 플레이어와 물건은 뺀다")]
        [SerializeField] private LayerMask holdBlockers = ~0;

        // 잡는 점의 최근 위치. 휘두른 속도를 구한다.
        private readonly Vector3[] samplePoints = new Vector3[SampleCount];
        private readonly float[] sampleTimes = new float[SampleCount];
        private int sampleHead;
        private int sampleFilled;

        private IPointerGate gate;
        private System.Random rng;
        private IAimTarget aimed;
        private IThrowable held;
        private float pullStartTime;
        private bool arrived;
        private bool lifting;
        // 둘러싸인 자리에서 들어 올리기를 마쳤다. 손에 들어올 때까지 그 높이를 지킨다.
        private bool raised;
        private Vector3 liftPoint;
        private bool wasDown;
        private float nextGrabTime;
        // 깔린 물건을 잡으려 해서 조금 끌려 나오는 중.
        private IThrowable tugged;
        private Vector3 tugVelocity;
        private float tugEndTime;

        /// <summary>확인용 입력 대체. 값이 있으면 마우스 대신 버튼 상태로 쓰고 잠금 확인을 건너뛴다.</summary>
        [NonSerialized] internal bool? debugButton;

        /// <summary>지금 조준한 대상. 잡고 있는 동안에는 null이다.</summary>
        public IAimTarget Aimed => aimed;
        public IThrowable Held => held;
        public bool Arrived => arrived;

        /// <summary>깔린 물건을 잡으려다 만 횟수. 확인용.</summary>
        public int Tugs { get; private set; }

        /// <summary>
        /// 잡은 물건이 따라가는 점. 카메라 앞 holdDistance, 눈높이에서 holdHeight만큼.
        /// 눈과 이 점 사이에 벽이나 가구가 있으면 그 앞으로 당긴다. 그러지 않으면 물건이 벽 끝(문틀)을 돌아
        /// 옆방으로 끌려가거나 가구 뒤로 들어간다.
        /// </summary>
        public Vector3 HoldPoint {
            get {
                Vector3 eye = view.transform.position;
                Vector3 offset = RawHoldPoint - eye;
                float length = offset.magnitude;
                if (length < 1e-4f) {
                    return eye;
                }

                Vector3 direction = offset / length;
                if (Physics.Raycast(eye, direction, out RaycastHit hit, length + tuning.holdWallMargin, holdBlockers,
                        QueryTriggerInteraction.Ignore)) {
                    return eye + direction * Mathf.Max(0f, hit.distance - tuning.holdWallMargin);
                }

                return RawHoldPoint;
            }
        }

        // 가로막힘을 따지지 않은 잡는 점. 휘두른 속도는 이 점으로 잰다 (벽 앞에서도 손목 움직임은 그대로다).
        private Vector3 RawHoldPoint {
            get {
                Transform eye = view.transform;
                return eye.position + eye.forward * tuning.holdDistance + Vector3.up * tuning.holdHeight;
            }
        }

        /// <summary>마지막으로 던진 속도와 그중 휘두른 속도. 확인용.</summary>
        public Vector3 LastThrowVelocity { get; private set; }
        public Vector3 LastFlick { get; private set; }

        /// <summary>걸려서 놓친 횟수. 확인용.</summary>
        public int Drops { get; private set; }

        private bool CanAct => debugButton.HasValue || gate == null || gate.CanAct;

        private void Awake() {
            gate = pointerGate as IPointerGate;
            if (pointerGate != null && gate == null) {
                Debug.LogError($"[Interactor] {pointerGate.name} does not implement IPointerGate", this);
            }
        }

        private void OnEnable() {
            flow.StateChanged += OnStateChanged;
        }

        private void OnDisable() {
            flow.StateChanged -= OnStateChanged;
            LetGo();
            SetAim(null);
        }

        private void OnStateChanged(RoundState previous, RoundState next) {
            if (next == RoundState.Playing) {
                // 새 판. 이전 판의 물건은 이미 풀로 돌아가 다시 놓였으므로 손을 비우기만 한다.
                rng = flow.Context.Seed.Stream(RoundStream.Throw);
                held = null;
                tugged = null;
                aimed = null;
                sampleFilled = 0;
                // 이전 판부터 누르고 있던 버튼으로는 잡지 않는다.
                wasDown = IsDown();
                return;
            }

            LetGo();
            SetAim(null);
        }

        private void LateUpdate() {
            if (flow.State != RoundState.Playing) {
                return;
            }

            rng ??= flow.Context.Seed.Stream(RoundStream.Throw);
            Sample();

            bool down = IsDown();
            bool pressed = down && !wasDown;
            wasDown = down;

            if (held != null && !held.Alive) {
                held = null;
            }

            if (held != null) {
                if (!down) {
                    Throw();
                }

                return;
            }

            SetAim(FindTarget());
            if (pressed && CanAct && Time.unscaledTime >= nextGrabTime && aimed is IThrowable item) {
                if (item.Buried) {
                    Tug(item);
                } else {
                    Grab(item);
                }
            }
        }

        private void FixedUpdate() {
            UpdateTug();
            if (held == null) {
                return;
            }

            if (!held.Alive) {
                held = null;
                return;
            }

            Vector3 center = held.Center;
            Vector3 hand = HoldPoint;
            Vector3 target = hand;
            if (lifting) {
                target = liftPoint;
                if (center.y >= liftPoint.y - LiftTolerance || Time.time - pullStartTime > tuning.holdPullTimeout) {
                    // 들어 올리기가 끝나면 그때부터 끌어오는 시간을 잰다.
                    lifting = false;
                    raised = true;
                    pullStartTime = Time.time;
                }
            } else if (raised && !arrived) {
                // 손이 테두리보다 낮아도 손에 들어올 때까지는 들어 올린 높이 아래로 내리지 않는다.
                target.y = Mathf.Max(target.y, liftPoint.y);
            }

            float distance = Vector3.Distance(center, hand);
            if (!arrived) {
                if (distance <= tuning.holdArriveDistance || (raised && Vector3.Distance(center, target) <= tuning.holdArriveDistance)) {
                    arrived = true;
                } else if (!lifting && Time.time - pullStartTime > tuning.holdPullTimeout) {
                    Drop();
                    return;
                }
            } else if (distance > tuning.holdBreakDistance) {
                Drop();
                return;
            }

            Vector3 velocity = (target - center) * tuning.holdResponse;
            if (arrived) {
                // 손을 휘두르는 만큼 미리 더해 줘야 뒤처지지 않는다.
                velocity += HandVelocity();
            }

            held.Drive(Vector3.ClampMagnitude(velocity, tuning.holdMaxSpeed));
        }

        private IAimTarget FindTarget() {
            var ray = new Ray(view.transform.position, view.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, tuning.reach, aimMask, QueryTriggerInteraction.Collide)) {
                return null;
            }

            // 처음 맞은 것이 대상일 때만 조준한다. 가구 너머의 물건은 잡지 못한다.
            // 시야만 막는 가구(트리거)도 맞히도록 트리거를 포함한다 (porting-notes 5장).
            return hit.collider.GetComponentInParent<IAimTarget>();
        }

        private void SetAim(IAimTarget next) {
            if (ReferenceEquals(next, aimed)) {
                return;
            }

            aimed?.SetAimed(false);
            aimed = next;
            aimed?.SetAimed(true);
        }

        // 위에 물건이 얹혀 있어 잡히지 않는다. 손 쪽으로 tugDistance만큼 옆으로 끌려 나오다 멈춘다.
        // 위로는 당기지 않는다. 위로 밀면 얹힌 더미를 들어 올리다 튀게 한다.
        private void Tug(IThrowable item) {
            Vector3 toHand = HoldPoint - item.Center;
            toHand.y = 0f;
            float time = Mathf.Max(0.01f, tuning.tugTime);
            tugVelocity = toHand.sqrMagnitude > 1e-6f ? toHand.normalized * (tuning.tugDistance / time) : Vector3.zero;
            tugged = item;
            tugEndTime = Time.time + time;
            Tugs++;
            // 끌려 나오는 동안과 그 직후에는 다시 잡지 않는다.
            nextGrabTime = Time.unscaledTime + time + tuning.throwCooldown;
        }

        private void UpdateTug() {
            if (tugged == null) {
                return;
            }

            if (!tugged.Alive) {
                tugged = null;
                return;
            }

            if (Time.time < tugEndTime) {
                tugged.Drive(tugVelocity);
                return;
            }

            tugged.Drive(Vector3.zero);
            tugged = null;
        }

        private void Grab(IThrowable item) {
            SetAim(null);
            // BeginHold가 Enclosed를 지우므로 먼저 읽는다.
            lifting = item.Enclosed;
            raised = false;
            liftPoint = item.Center + Vector3.up * tuning.enclosedLift;
            item.BeginHold();
            held = item;
            arrived = false;
            pullStartTime = Time.time;
        }

        private void Throw() {
            Vector3 forward = view.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) {
                forward = view.transform.up;
                forward.y = 0f;
            }

            forward.Normalize();
            Vector3 flick = HandVelocity();
            Vector3 velocity = forward * tuning.tapThrowForward + Vector3.up * tuning.tapThrowUp + flick * tuning.flickScale;
            velocity = Vector3.ClampMagnitude(velocity, tuning.maxThrowSpeed);
            Vector3 spin = new(Range(tuning.throwSpin.x), Range(tuning.throwSpin.y), Range(tuning.throwSpin.z));

            held.Release(velocity, spin);
            held = null;
            LastFlick = flick;
            LastThrowVelocity = velocity;
            flow.Context.Throws++;
            nextGrabTime = Time.unscaledTime + tuning.throwCooldown;
        }

        // 가구에 걸려 손에서 빠졌다. 그 자리에 떨어뜨린다.
        private void Drop() {
            Drops++;
            LetGo();
        }

        private void LetGo() {
            if (held != null && held.Alive) {
                held.Release(Vector3.zero, Vector3.zero);
            }

            held = null;
            nextGrabTime = Time.unscaledTime + tuning.throwCooldown;
        }

        private void Sample() {
            samplePoints[sampleHead] = RawHoldPoint;
            sampleTimes[sampleHead] = Time.time;
            sampleHead = (sampleHead + 1) % SampleCount;
            sampleFilled = Mathf.Min(sampleFilled + 1, SampleCount);
        }

        /// <summary>잡는 점이 최근 flickWindow 동안 움직인 평균 속도. 걷는 속도도 포함된다.</summary>
        private Vector3 HandVelocity() {
            if (sampleFilled < 2) {
                return Vector3.zero;
            }

            int newest = (sampleHead - 1 + SampleCount) % SampleCount;
            float now = sampleTimes[newest];
            int oldest = newest;
            for (int i = 1; i < sampleFilled; i++) {
                int index = (newest - i + SampleCount) % SampleCount;
                if (now - sampleTimes[index] > tuning.flickWindow) {
                    break;
                }

                oldest = index;
            }

            float span = now - sampleTimes[oldest];
            return span > 1e-4f ? (samplePoints[newest] - samplePoints[oldest]) / span : Vector3.zero;
        }

        private bool IsDown() {
            if (debugButton.HasValue) {
                return debugButton.Value;
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        private float Range(float limit) => (float)(rng.NextDouble() * 2.0 - 1.0) * limit;
    }
}
