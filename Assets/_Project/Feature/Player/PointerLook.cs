using System.Runtime.InteropServices;
using MissedCall.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Player {
    /// <summary>
    /// 포인터 잠금과 마우스 둘러보기. 브라우저는 사용자 클릭 안에서만 잠금을 허용하고 Esc로 강제로 푼다.
    /// 그래서 잠기지 않은 상태의 클릭은 잠금 요청에만 쓰고, 그 버튼을 뗄 때까지는 게임 입력으로 넘기지 않는다.
    /// Web에서는 Unity의 Cursor.lockState가 Esc 해제를 반영하지 않으므로 브라우저 상태(jslib)를 기준으로 삼는다.
    /// </summary>
    public sealed class PointerLook : MonoBehaviour {
        [SerializeField] private GameTuning tuning;
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
        }

        private void OnDisable() {
            Unlock();
        }

        public void Unlock() {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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

            if (!IsLocked) {
                if (mouse.leftButton.wasPressedThisFrame) {
                    // Unity는 이 요청을 다음 브라우저 입력 이벤트(같은 클릭의 mouseup) 안에서 처리한다.
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    swallowClick = true;
                    requested = true;
                    requestTime = Time.unscaledTime;
                    // 잠긴 직후 프레임의 이동량은 커서 위치가 튀는 값이 섞일 수 있어 버린다.
                    settleFrames = 2;
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
