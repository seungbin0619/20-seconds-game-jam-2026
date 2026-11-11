using System;
using MissedCall.Core;
using MissedCall.Core.Round;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Player {
    /// <summary>
    /// CharacterController 걷기. 바로 최고 속도로 튀지 않도록 가속과 감속을 주고,
    /// 뒤나 옆으로 걸을 때는 느리게 걷는다. playing 상태에서만 입력을 받고, 그 외에는 미끄러지듯 멈춘다.
    /// 이 오브젝트의 위치는 발(바닥)이고, 좌우 회전(yaw)은 <see cref="PointerLook"/>이 이 오브젝트에 준다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private GameFlow flow;
        [SerializeField] private PointerLook look;

        // 바닥에 붙여두는 아래 방향 속도. 계단과 경사가 없으므로 이 정도면 된다.
        private const float GroundStick = -2f;

        private CharacterController controller;
        private Vector3 velocity;
        private float verticalSpeed;
        private int lastRoom = -1;

        /// <summary>현재 수평 속도(월드).</summary>
        public Vector3 Velocity => velocity;

        /// <summary>순간이동 직후 호출된다. 머리 흔들림 같은 연출을 초기화한다.</summary>
        public event Action Teleported;

        private void Awake() {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>발 위치와 시선으로 순간이동한다. 판 시작 위치를 잡을 때 쓴다.</summary>
        public void Teleport(Vector3 feet, float yaw, float pitch) {
            controller.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            look.SetView(yaw, pitch);
            velocity = Vector3.zero;
            verticalSpeed = 0f;
            lastRoom = -1;
            Teleported?.Invoke();
            WriteContext();
        }

        private void Update() {
            Vector2 input = flow.State == RoundState.Playing ? ReadInput() : Vector2.zero;
            Simulate(input, Time.deltaTime);
        }

        /// <summary>입력(x 옆, y 앞)으로 한 프레임 움직인다. 입력 장치와 분리해 두어 테스트에서 직접 부를 수 있다.</summary>
        private void Simulate(Vector2 input, float dt) {
            // 앞, 뒤, 옆 속도가 다르다. 대각선은 각 축 속도로 만든 타원 위의 값이 된다.
            float forwardSpeed = tuning.walkSpeed * (input.y < 0f ? tuning.backwardSpeedScale : 1f);
            float strafeSpeed = tuning.walkSpeed * tuning.strafeSpeedScale;
            Vector3 local = new(input.x * strafeSpeed, 0f, input.y * forwardSpeed);
            Vector3 target = transform.rotation * local;

            // 원하는 방향으로 속도를 키울 때는 가속, 줄이거나 방향을 틀 때는 감속 값을 쓴다.
            bool speedingUp = input != Vector2.zero && Vector3.Dot(target, velocity) >= velocity.sqrMagnitude;
            float rate = speedingUp ? tuning.walkAcceleration : tuning.walkDeceleration;
            velocity = Vector3.MoveTowards(velocity, target, rate * dt);

            verticalSpeed = controller.isGrounded ? GroundStick : verticalSpeed + Physics.gravity.y * dt;
            controller.Move((velocity + Vector3.up * verticalSpeed) * dt);

            // 벽에 막힌 만큼 속도를 버린다. 벽을 따라 미끄러지는 성분은 남는다.
            if (dt > 0f) {
                Vector3 actual = controller.velocity;
                actual.y = 0f;
                if (actual.sqrMagnitude < velocity.sqrMagnitude) {
                    velocity = actual;
                }
            }

            WriteContext();
        }

        private void WriteContext() {
            RoundContext context = flow.Context;
            Vector3 ears = transform.position + Vector3.up * tuning.eyeHeight;
            context.PlayerPosition = ears;
            if (context.Graph != null) {
                int room = context.Graph.RoomAt(ears);
                // 집 밖(-1)이면 마지막 방을 유지한다.
                if (room >= 0) {
                    lastRoom = room;
                }
            }

            context.PlayerRoom = lastRoom;
        }

        private static Vector2 ReadInput() {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) {
                return Vector2.zero;
            }

            float x = Axis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            float y = Axis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            Vector2 input = new(x, y);
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        private static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
