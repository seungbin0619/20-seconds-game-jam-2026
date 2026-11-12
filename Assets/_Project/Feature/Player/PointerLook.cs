using System.Runtime.InteropServices;
using MissedCall.Core;
using MissedCall.Core.Aim;
using MissedCall.Core.Round;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Player {
    /// <summary>
    /// 포인터 잠금과 마우스 둘러보기. 브라우저는 사용자 클릭 안에서만 잠금을 허용하고 Esc로 강제로 푼다.
    /// 그래서 잠기지 않은 상태의 클릭은 잠금 요청에만 쓰고, 그 버튼을 뗄 때까지는 게임 입력으로 넘기지 않는다.
    /// Web에서는 Unity의 Cursor.lockState가 Esc 해제를 반영하지 않으므로 브라우저 상태(jslib)를 기준으로 삼는다.
    /// <see cref="flow"/>가 있으면 playing에서만 잠근다. 판이 시작되면(시작 버튼, R) 그 입력으로 잠금을 요청하고,
    /// 그 밖의 상태(타이틀, 받기 슬라이드, 결과)에서는 풀어서 커서로 UI를 누르게 한다.
    /// </summary>
    public sealed class PointerLook : MonoBehaviour, IPointerGate {
        [SerializeField] private GameTuning tuning;
        [Tooltip("있으면 playing 상태에서만 잠그고 둘러본다. 비우면 항상 클릭으로 잠그고 둘러본다 (샌드박스)")]
        [SerializeField] private GameFlow flow;
        [Tooltip("좌우 회전(yaw)을 받는 몸통")]
        [SerializeField] private Transform body;
        [Tooltip("상하 회전(pitch)을 받는 머리(카메라)")]
        [SerializeField] private Transform head;
        [SerializeField] private float pitchLimit = 80f;
        [Tooltip("잠금을 요청한 뒤 이 시간(초) 안에 브라우저가 잠그지 않으면 실패로 보고 다음 클릭에서 다시 요청한다")]
        [SerializeField] private float lockTimeout = 1f;

        private float yaw;
        private float pitch;
        private bool swallowClick;
        private int settleFrames;
        private bool requested;
        private float requestTime;
        private bool wasLocked;

        /// <summary>실제로 잠겨 있는지. Web에서는 브라우저의 잠금 상태다.</summary>
        public bool IsLocked => Cursor.lockState == CursorLockMode.Locked && BrowserLocked();

        /// <summary>던지기나 집기 같은 클릭 입력을 받아도 되는지. 잠금 요청에 쓴 클릭은 제외한다.</summary>
        public bool CanAct => IsLocked && !swallowClick;

        /// <summary>마지막 프레임에 반영한 마우스 이동량(px). 확인용.</summary>
        public Vector2 LastDelta { get; private set; }

        public float Yaw => yaw;
        public float Pitch => pitch;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int PointerLock_IsLocked();

        private static bool BrowserLocked() => PointerLock_IsLocked() != 0;
#else
        private static bool BrowserLocked() => true;
#endif

        private void OnEnable() {
            yaw = body.eulerAngles.y;
            pitch = NormalizeAngle(head.localEulerAngles.x);
            if (flow != null) {
                flow.StateChanged += OnStateChanged;
                flow.PausedChanged += OnPausedChanged;
            }
        }

        private void OnDisable() {
            if (flow != null) {
                flow.StateChanged -= OnStateChanged;
                flow.PausedChanged -= OnPausedChanged;
            }

            Unlock();
        }

        // 재개는 일시정지 메뉴의 Resume을 누른 프레임에 일어나므로 그 클릭으로 다시 잠근다.
        private void OnPausedChanged(bool paused) {
            if (paused) {
                Unlock();
            } else if (flow.State == RoundState.Playing) {
                RequestLock();
            }
        }

        // 판 시작은 시작 버튼을 누른 프레임이나 R을 누른 프레임에 일어난다.
        // 이때 요청하면 Unity가 같은 입력의 다음 브라우저 이벤트(mouseup, keyup)에서 잠근다.
        private void OnStateChanged(RoundState previous, RoundState next) {
            if (next == RoundState.Playing) {
                RequestLock();
            } else {
                Unlock();
            }
        }

        private bool LockAllowed => flow == null || (flow.State == RoundState.Playing && !flow.IsPaused);

        /// <summary>시선을 정한다(도). 판 시작 위치를 잡을 때 쓴다.</summary>
        public void SetView(float newYaw, float newPitch) {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, -pitchLimit, pitchLimit);
            body.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Unity는 이 요청을 다음 브라우저 입력 이벤트(같은 클릭의 mouseup, 같은 키의 keyup) 안에서 처리한다.
        // 요청에 쓴 클릭은 뗄 때까지 게임 입력으로 넘기지 않는다.
        private void RequestLock() {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            swallowClick = true;
            requested = true;
            requestTime = Time.unscaledTime;
            // 잠긴 직후 프레임의 이동량은 커서 위치가 튀는 값이 섞일 수 있어 버린다.
            settleFrames = 2;
        }

        // Web에서 Cursor.visible = true는 캔버스 커서를 기본 화살표로 되돌리므로(전용 커서가 지워짐) 바뀔 때만 건드린다.
        public void Unlock() {
            if (Cursor.lockState != CursorLockMode.None) {
                Cursor.lockState = CursorLockMode.None;
            }

            if (!Cursor.visible) {
                Cursor.visible = true;
            }

            requested = false;
        }

        private void Update() {
            Mouse mouse = Mouse.current;
            LastDelta = Vector2.zero;
            if (mouse == null) {
                return;
            }

            if (swallowClick && !mouse.leftButton.isPressed) {
                swallowClick = false;
            }

            SyncWithBrowser();

            // playing 중 잠금을 잃으면(Esc, 탭 전환, 창 전환) 판을 멈춘다. 잠금을 얻기 전(요청 중, 거부됨)은 해당하지 않는다.
            bool locked = IsLocked;
            if (wasLocked && !locked && flow != null && flow.State == RoundState.Playing) {
                flow.Pause();
            }

            wasLocked = locked;

            if (!LockAllowed) {
                return;
            }

            if (!IsLocked) {
                // Esc로 풀렸거나 브라우저가 거부했으면 다음 클릭에서 다시 잠근다.
                if (mouse.leftButton.wasPressedThisFrame) {
                    RequestLock();
                }

                return;
            }

            requested = false;
            Vector2 delta = mouse.delta.ReadValue();
            if (settleFrames > 0) {
                settleFrames--;
                return;
            }

            LastDelta = delta;

            // lookSpeed는 픽셀당 라디안이다 (프로토타입 기준).
            float degreesPerPixel = tuning.lookSpeed * Mathf.Rad2Deg;
            yaw += delta.x * degreesPerPixel;
            pitch = Mathf.Clamp(pitch - delta.y * degreesPerPixel, -pitchLimit, pitchLimit);
            body.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Unity는 잠겼다고 알지만 브라우저가 풀었거나(Esc) 끝내 잠그지 않았으면 Unity 쪽도 푼다.
        private void SyncWithBrowser() {
            if (Cursor.lockState != CursorLockMode.Locked || BrowserLocked()) {
                return;
            }

            bool pending = requested && Time.unscaledTime - requestTime < lockTimeout;
            if (!pending) {
                Unlock();
            }
        }

        private static float NormalizeAngle(float degrees) => degrees > 180f ? degrees - 360f : degrees;
    }
}
