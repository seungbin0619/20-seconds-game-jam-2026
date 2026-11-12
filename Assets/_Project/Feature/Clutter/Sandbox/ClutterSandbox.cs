using MissedCall.Core;
using MissedCall.Core.Aim;
using MissedCall.Core.Round;
using MissedCall.Feature.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Clutter.Sandbox {
    /// <summary>
    /// 물건 더미와 잡아 던지기 확인용 테스트 씬. 시작하면 바로 판을 연다. 물건 수, 안정화 시간, 깨어 있는 물체 수,
    /// 마지막으로 던진 속도를 보여주고, 잡기와 던지기 튜닝 값을 슬라이더로 바꾼다.
    /// 슬라이더는 <see cref="GameTuning"/> 에셋을 직접 고친다. 마음에 들지 않으면 Reset을 누른다. 게임 코드가 아니다.
    /// </summary>
    public sealed class ClutterSandbox : MonoBehaviour {
        [SerializeField] private GameFlow flow;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private ClutterStep clutter;
        [SerializeField] private Interactor interactor;

        private GameTuning original;
        private float worstSettle;
        private bool panel = true;

        private void Awake() {
            original = Instantiate(tuning);
        }

        private void Start() {
            flow.StartRound();
            flow.StateChanged += (_, next) => {
                if (next == RoundState.Playing) {
                    worstSettle = Mathf.Max(worstSettle, (float)clutter.LastSettleMs);
                }
            };
            worstSettle = (float)clutter.LastSettleMs;
        }

        private void Update() {
            if (flow.State == RoundState.Missed) {
                flow.StartRound();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) {
                panel = !panel;
            }
        }

        private int CountAwake() {
            int awake = 0;
            for (int i = 0; i < clutter.Count; i++) {
                if (!clutter.Pool.Items[i].Body.IsSleeping()) {
                    awake++;
                }
            }

            return awake;
        }

        private void OnGUI() {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            DrawCrosshair(scale);

            RoundContext context = flow.Context;
            GUILayout.BeginArea(new Rect(10f, 10f, 380f, 700f), GUI.skin.box);
            GUILayout.Label("Clutter sandbox");
            GUILayout.Label($"Seed {context.Seed} / {flow.State} {context.TimeLeft:0.0}s");
            GUILayout.Label($"Items {clutter.Count} (pool {clutter.Pool?.Capacity}) / awake {CountAwake()}");
            GUILayout.Label($"Settle {clutter.LastSettleMs:0.0} ms (worst {worstSettle:0.0})");
            Vector3 v = interactor.LastThrowVelocity;
            Vector3 flick = interactor.LastFlick;
            GUILayout.Label($"Thrown {context.Throws} / dropped {interactor.Drops} / tugged {interactor.Tugs}");
            GUILayout.Label($"Last throw {v.magnitude:0.0} m/s (flick {flick.magnitude:0.0}, up {v.y:0.0})");
            GUILayout.Label("Hold click: grab / swing and release: throw / R: new house / Tab: panel");

            if (panel) {
                GUILayout.Space(6f);
                tuning.holdDistance = Slider("Hold distance (m)", tuning.holdDistance, 0.4f, 1.6f);
                tuning.holdHeight = Slider("Hold height (m)", tuning.holdHeight, -0.6f, 0.2f);
                tuning.holdResponse = Slider("Hold response (1/s)", tuning.holdResponse, 4f, 40f);
                tuning.holdMaxSpeed = Slider("Hold max speed (m/s)", tuning.holdMaxSpeed, 2f, 20f);
                tuning.holdBreakDistance = Slider("Break distance (m)", tuning.holdBreakDistance, 0.3f, 2f);
                tuning.tapThrowForward = Slider("Tap forward (m/s)", tuning.tapThrowForward, 0f, 6f);
                tuning.tapThrowUp = Slider("Tap up (m/s)", tuning.tapThrowUp, 0f, 5f);
                tuning.flickScale = Slider("Flick scale", tuning.flickScale, 0f, 2f);
                tuning.flickWindow = Slider("Flick window (s)", tuning.flickWindow, 0.02f, 0.25f, "0.000");
                tuning.maxThrowSpeed = Slider("Max throw speed (m/s)", tuning.maxThrowSpeed, 2f, 20f);
                if (GUILayout.Button("Reset to values at start")) {
                    Restore();
                }
            }

            GUILayout.EndArea();
        }

        private void DrawCrosshair(float scale) {
            float x = Screen.width / scale * 0.5f;
            float y = Screen.height / scale * 0.5f;
            string mark = interactor.Held != null ? "( )"
                : interactor.Aimed is IThrowable { Buried: true } ? "[+] Grab (buried)"
                : interactor.Aimed != null ? "[+] Grab" : "+";
            GUI.Label(new Rect(x - 8f, y - 10f, 120f, 20f), mark);
        }

        private void Restore() {
            tuning.holdDistance = original.holdDistance;
            tuning.holdHeight = original.holdHeight;
            tuning.holdResponse = original.holdResponse;
            tuning.holdMaxSpeed = original.holdMaxSpeed;
            tuning.holdBreakDistance = original.holdBreakDistance;
            tuning.tapThrowForward = original.tapThrowForward;
            tuning.tapThrowUp = original.tapThrowUp;
            tuning.flickScale = original.flickScale;
            tuning.flickWindow = original.flickWindow;
            tuning.maxThrowSpeed = original.maxThrowSpeed;
        }

        private static float Slider(string label, float value, float min, float max, string format = "0.00") {
            GUILayout.Label($"{label}: {value.ToString(format)}");
            return GUILayout.HorizontalSlider(value, min, max);
        }
    }
}
