using System.Collections.Generic;
using MissedCall.Core;
using MissedCall.Core.Aim;
using MissedCall.Core.Audio;
using MissedCall.Core.Round;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Player {
    /// <summary>
    /// 몸으로 물건을 치운다. 물건(Clutter 레이어)은 플레이어와 충돌하지 않으므로 직접 밀어낸다.
    /// 박치기: 빠르게 걷다가 몸에 닿은 물건을 걷는 방향으로 튕긴다. 걷는 속도가 slamMinSpeed 이상일 때만.
    /// 발차기: 우클릭. 발 앞 낮은 구 안의 물건을 앞으로 차 올린다. 깔린 물건도 함께 날아간다.
    /// 폰은 콜라이더가 없어 밀리지 않는다.
    /// </summary>
    public sealed class PlayerShove : MonoBehaviour {
        // 밀어낼 물건 후보를 찾는 버퍼
        private static readonly Collider[] Hits = new Collider[48];

        [SerializeField] private GameFlow flow;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CharacterController controller;
        [Tooltip("IPointerGate를 구현한 컴포넌트 (PointerLook). 잠금을 얻는 클릭을 발차기로 쓰지 않는다")]
        [SerializeField] private MonoBehaviour pointerGate;
        [Tooltip("밀어낼 물건 레이어")]
        [SerializeField] private LayerMask clutterMask;

        // 박치기로 튕긴 물건이 다시 튕기기까지 시각. 몸에 붙은 채 매 스텝 가속되지 않게 한다.
        private readonly Dictionary<Rigidbody, float> slamReady = new();
        private readonly HashSet<Rigidbody> touched = new();
        private IPointerGate gate;
        private System.Random rng;
        private bool wasDown;
        private bool kickQueued;
        private float nextKickTime;
        private float nextSlamSound;

        /// <summary>이번 판에 찬 횟수와 박치기로 튕긴 물건 수. 확인용.</summary>
        public int Kicks { get; private set; }
        public int Slammed { get; private set; }

        /// <summary>마지막 발차기에 날아간 물건 수. 확인용.</summary>
        public int LastKicked { get; private set; }

        private bool Active => flow.State == RoundState.Playing && !flow.IsPaused;

        private void Awake() {
            gate = pointerGate as IPointerGate;
            if (pointerGate != null && gate == null) {
                Debug.LogError($"[PlayerShove] {pointerGate.name} does not implement IPointerGate", this);
            }
        }

        private void OnEnable() {
            flow.StateChanged += OnStateChanged;
        }

        private void OnDisable() {
            flow.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(RoundState previous, RoundState next) {
            if (next != RoundState.Playing) {
                return;
            }

            rng = flow.Context.Seed.Stream(RoundStream.Shove);
            slamReady.Clear();
            Kicks = 0;
            Slammed = 0;
            LastKicked = 0;
            kickQueued = false;
            nextKickTime = 0f;
            // 이전 판부터 누르고 있던 버튼으로는 차지 않는다.
            wasDown = IsDown();
        }

        private void Update() {
            bool down = IsDown();
            bool pressed = down && !wasDown;
            wasDown = down;
            if (!Active || !pressed || (gate != null && !gate.CanAct) || Time.unscaledTime < nextKickTime) {
                return;
            }

            // 물리 스텝에서 찬다. 물건 속도를 바꾸는 일은 FixedUpdate에 모은다.
            kickQueued = true;
            nextKickTime = Time.unscaledTime + tuning.kickCooldown;
        }

        private void FixedUpdate() {
            if (!Active) {
                kickQueued = false;
                return;
            }

            rng ??= flow.Context.Seed.Stream(RoundStream.Shove);
            if (kickQueued) {
                kickQueued = false;
                Kick();
            }

            Slam();
        }

        /// <summary>다음 물리 스텝에서 찬다. 확인용 (입력 없이 차 본다).</summary>
        public void QueueKick() => kickQueued = true;

        /// <summary>발 앞 구 안의 물건을 앞쪽 위로 찬다. 가운데에 가까울수록 세다.</summary>
        private void Kick() {
            Vector3 forward = Flat(transform.forward);
            Vector3 center = transform.position + forward * tuning.kickReach + Vector3.up * tuning.kickHeight;
            float radius = tuning.kickRadius;
            int count = Physics.OverlapSphereNonAlloc(center, radius, Hits, clutterMask, QueryTriggerInteraction.Ignore);
            Collect(count);

            Kicks++;
            LastKicked = 0;
            foreach (Rigidbody body in touched) {
                Vector3 offset = body.worldCenterOfMass - center;
                float falloff = Mathf.Lerp(1f, tuning.kickEdgeScale, Mathf.Clamp01(offset.magnitude / radius));
                // 앞으로 차되, 옆에 있던 물건은 그 옆으로 조금 벌어진다.
                Vector3 side = Vector3.ProjectOnPlane(Flat(offset), forward);
                Vector3 velocity = (forward + side * tuning.kickSpread) * tuning.kickSpeed + Vector3.up * tuning.kickLift;
                if (!body.TryGetComponent(out IThrowable _)) {
                    // 물건이 아닌 물리 가구(빨래 바구니)는 대개 구석에 있어 앞으로 차면 벽에 막힌다.
                    // 윗부분이 플레이어 쪽으로 넘어오게 엎어 안의 물건을 쏟는다.
                    body.WakeUp();
                    body.linearVelocity = Vector3.up * tuning.kickTipLift - forward * tuning.kickTipBack;
                    body.angularVelocity = Vector3.Cross(Vector3.up, -forward) * tuning.kickTipSpin;
                    LastKicked++;
                    continue;
                }

                if (body.TryGetComponent(out IThrowable item) && item.Enclosed) {
                    // 바구니, 상자 안의 물건은 앞으로 차면 테두리에 걸려 멈춘다. 위로 들썩이게만 한다 (비우려면 들어 올려야 한다).
                    velocity = forward * (tuning.kickSpeed * tuning.kickEnclosedForward) + Vector3.up * tuning.kickEnclosedLift;
                }

                Push(body, velocity * falloff + motor.Velocity, tuning.kickSpin);
                LastKicked++;
            }

            SoundCues.PlayAt(SoundCue.Kick, center, LastKicked > 0 ? 1f : 0.4f);
        }

        // 걷는 방향 앞쪽 반원 안에 몸이 닿은 물건을 걷는 속도에 비례해 튕긴다.
        private void Slam() {
            Vector3 velocity = motor.Velocity;
            float speed = velocity.magnitude;
            if (speed < tuning.slamMinSpeed) {
                return;
            }

            Vector3 direction = velocity / speed;
            float radius = controller.radius + tuning.slamReach;
            Vector3 feet = transform.position;
            // 아래 구의 중심을 발 높이에 둔다. 바닥에 납작하게 깔린 물건도 몸 앞에서 닿는다.
            Vector3 bottom = feet + Vector3.up * 0.05f;
            Vector3 top = feet + Vector3.up * Mathf.Max(0.05f, controller.height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Hits, clutterMask, QueryTriggerInteraction.Ignore);
            Collect(count);

            float strength = Mathf.InverseLerp(tuning.slamMinSpeed, tuning.walkSpeed, speed);
            int slammed = 0;
            Vector3 hitPoint = Vector3.zero;
            foreach (Rigidbody body in touched) {
                if (slamReady.TryGetValue(body, out float ready) && Time.time < ready) {
                    continue;
                }

                Vector3 away = Flat(body.worldCenterOfMass - feet);
                if (Vector3.Dot(away, direction) <= 0f) {
                    continue;
                }

                // 걷는 방향과 몸에서 바깥쪽 방향을 섞어 길을 비키듯 옆으로 튄다.
                Vector3 push = (direction + away).normalized * (speed * tuning.slamPush)
                    + Vector3.up * (tuning.slamLift * strength);
                Push(body, push, tuning.kickSpin * 0.5f);
                slamReady[body] = Time.time + tuning.slamCooldown;
                hitPoint += body.worldCenterOfMass;
                slammed++;
            }

            if (slammed == 0) {
                return;
            }

            Slammed += slammed;
            if (Time.time >= nextSlamSound) {
                nextSlamSound = Time.time + tuning.slamCooldown;
                SoundCues.PlayAt(SoundCue.Slam, hitPoint / slammed, Mathf.Clamp01(0.4f + slammed * 0.15f) * strength);
            }
        }

        // 겹친 콜라이더를 움직일 수 있는 물체 하나씩으로 모은다.
        private void Collect(int count) {
            touched.Clear();
            for (int i = 0; i < count; i++) {
                Rigidbody body = Hits[i].attachedRigidbody;
                if (body != null && !body.isKinematic) {
                    touched.Add(body);
                }
            }
        }

        // 지금 속도보다 빠르게만 바꾼다. 이미 날아가는 물건을 늦추지 않는다.
        private void Push(Rigidbody body, Vector3 velocity, Vector3 spin) {
            Vector3 current = body.linearVelocity;
            Vector3 next = current.sqrMagnitude >= velocity.sqrMagnitude ? current + velocity * 0.3f : velocity;
            Vector3 angular = body.angularVelocity + new Vector3(Range(spin.x), Range(spin.y), Range(spin.z));
            if (body.TryGetComponent(out IThrowable item)) {
                item.Knock(next, angular);
                return;
            }

            body.WakeUp();
            body.linearVelocity = next;
            body.angularVelocity = angular;
        }

        private static Vector3 Flat(Vector3 v) {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }

        private float Range(float limit) => (float)(rng.NextDouble() * 2.0 - 1.0) * limit;

        private static bool IsDown() {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }
    }
}
