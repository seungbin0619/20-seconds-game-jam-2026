using MissedCall.Core;
using MissedCall.Core.Aim;
using MissedCall.Core.Round;
using MissedCall.Feature.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Phone.Sandbox {
    /// <summary>
    /// 폰 배치, 덮임 수, 진동, 집기 확인용 테스트 씬. 시작하면 바로 판을 연다.
    /// 받기 슬라이드는 Ui 단계에서 만들므로 여기서는 클릭이나 Space로 받는다. F는 폰 위치 표시, V는 진동 모드.
    /// 진동 세기 슬라이더는 <see cref="GameTuning"/> 에셋을 직접 고친다. 게임 코드가 아니다.
    /// </summary>
    public sealed class PhoneSandbox : MonoBehaviour {
        private const float RestartDelay = 2f;

        [SerializeField] private GameFlow flow;
        [SerializeField] private PhoneStep phone;
        [SerializeField] private Interactor interactor;
        [SerializeField] private Camera view;

        [SerializeField] private GameTuning tuning;

        private bool reveal;
        // 진동 모드 변수 흉내. Modifiers가 생기기 전까지 V로 켜서 세기를 비교한다.
        private bool vibrationMode;
        private float originalRingJiggleScale;
        private float endedAt = -1f;
        private int wins;
        private int misses;

        private void Start() {
            originalRingJiggleScale = tuning.ringJiggleScale;
            flow.StateChanged += OnStateChanged;
            flow.StartRound();
        }

        private void OnDestroy() {
            flow.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(RoundState previous, RoundState next) {
            if (next == RoundState.Won) {
                wins++;
            } else if (next == RoundState.Missed) {
                misses++;
            }

            endedAt = next is RoundState.Won or RoundState.Missed ? Time.unscaledTime : -1f;
        }

        private void Update() {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame) {
                reveal = !reveal;
            }

            if (keyboard != null && keyboard.vKey.wasPressedThisFrame) {
                vibrationMode = !vibrationMode;
            }

            // 판이 바뀌면 RoundContext가 초기화되므로 매 프레임 다시 넣는다.
            flow.Context.VibrationMode = vibrationMode;

            if (flow.State == RoundState.Answer) {
                bool answer = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                    || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                if (answer) {
                    flow.Answer();
                }
            }

            if (endedAt >= 0f && Time.unscaledTime - endedAt > RestartDelay) {
                flow.StartRound();
            }
        }

        private void OnGUI() {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            DrawCrosshair(scale);
            if (reveal) {
                DrawPhoneMarker(scale);
            }

            RoundContext context = flow.Context;
            PhoneDevice device = phone.Device;
            float distance = Vector3.Distance(context.PlayerPosition, context.PhonePosition);
            GUILayout.BeginArea(new Rect(10f, 10f, 380f, 460f), GUI.skin.box);
            GUILayout.Label("Phone sandbox");
            GUILayout.Label($"Seed {context.Seed} / {flow.State} {context.TimeLeft:0.0}s / won {wins} missed {misses}");
            GUILayout.Label($"Spot {(phone.Spot != null ? phone.Spot.name : "-")} / phone room {context.PhoneRoom}, player room {context.PlayerRoom}");
            GUILayout.Label($"Covers {context.Covers} / distance {distance:0.0} m");
            GUILayout.Label($"Jiggles {device.Jiggles} (last moved {device.LastJiggled}) / thrown {context.Throws}");
            GUILayout.Label($"Aim {(context.AimTarget != null ? context.AimTarget.Kind.ToString() : "-")}");
            if (flow.State == RoundState.Answer) {
                GUILayout.Label("Incoming call - click or Space to answer");
            } else if (context.AnswerTime >= 0f) {
                GUILayout.Label($"Answered in {context.AnswerTime:0.0}s");
            }

            GUILayout.Label("Hold click: grab and throw / click phone: pick up / F: show phone / V: vibration mode / R: new house");
            GUILayout.Space(6f);
            GUILayout.Label($"Vibration mode {(vibrationMode ? "ON (full jiggle)" : "OFF (ring jiggle)")}");
            GUILayout.Label($"Ring jiggle scale: {tuning.ringJiggleScale:0.00} (= {tuning.jiggle * tuning.ringJiggleScale:0.00} m/s)");
            tuning.ringJiggleScale = GUILayout.HorizontalSlider(tuning.ringJiggleScale, 0f, 1f);
            if (GUILayout.Button("Reset to value at start")) {
                tuning.ringJiggleScale = originalRingJiggleScale;
            }

            GUILayout.EndArea();
        }

        private void DrawCrosshair(float scale) {
            float x = Screen.width / scale * 0.5f;
            float y = Screen.height / scale * 0.5f;
            string mark = interactor.Held != null ? "( )"
                : interactor.Aimed is IGrabbable ? "[O] Grab phone"
                : interactor.Aimed is IThrowable { Buried: true } ? "[+] Grab (buried)"
                : interactor.Aimed != null ? "[+] Grab" : "+";
            GUI.Label(new Rect(x - 8f, y - 10f, 160f, 20f), mark);
        }

        private void DrawPhoneMarker(float scale) {
            if (!phone.Device.Placed) {
                return;
            }

            Vector3 screen = view.WorldToScreenPoint(flow.Context.PhonePosition);
            if (screen.z <= 0f) {
                return;
            }

            float x = screen.x / scale;
            float y = (Screen.height - screen.y) / scale;
            GUI.Label(new Rect(x - 20f, y - 10f, 80f, 20f), "<PHONE>");
        }
    }
}
