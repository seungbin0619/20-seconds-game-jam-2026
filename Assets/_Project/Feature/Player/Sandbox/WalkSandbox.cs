using MissedCall.Core;
using MissedCall.Core.Round;
using MissedCall.Feature.Layout;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Player.Sandbox {
    /// <summary>
    /// 걷기 느낌을 맞추는 테스트 씬. 시작하면 바로 판을 시작하고, 걷기 튜닝 값을 슬라이더로 바꾼다.
    /// 슬라이더는 <see cref="GameTuning"/> 에셋을 직접 고친다. 에디터에서는 플레이를 멈춰도 값이 남으므로
    /// 마음에 들면 그대로 두고, 아니면 Reset을 누른다. 게임 코드가 아니다.
    /// </summary>
    public sealed class WalkSandbox : MonoBehaviour {
        [SerializeField] private GameFlow flow;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private LayoutStep layout;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private HeadBob bob;

        private GameTuning original;
        private int steps;
        private float stepFlash;
        private int lastFoot;
        private bool panel = true;

        private void Awake() {
            original = Instantiate(tuning);
        }

        private void OnEnable() {
            bob.Stepped += OnStepped;
        }

        private void OnDisable() {
            bob.Stepped -= OnStepped;
        }

        private void Start() {
            flow.StartRound();
        }

        private void OnStepped(int foot) {
            steps++;
            lastFoot = foot;
            stepFlash = 0.12f;
        }

        private void Update() {
            stepFlash -= Time.deltaTime;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) {
                return;
            }

            // 판이 끝나도 계속 걸어볼 수 있게 시간이 다 되면 새 판을 연다.
            if (flow.State == RoundState.Missed) {
                flow.StartRound();
            }

            if (keyboard.tabKey.wasPressedThisFrame) {
                panel = !panel;
            }
        }

        private void OnGUI() {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            RoundContext context = flow.Context;
            GUILayout.BeginArea(new Rect(10f, 10f, 360f, 640f), GUI.skin.box);
            GUILayout.Label("Walk sandbox");
            string template = layout.LastTemplate != null ? layout.LastTemplate.name : "-";
            string room = context.Graph != null && context.PlayerRoom >= 0 ? context.Graph.Rooms[context.PlayerRoom].Kind.ToString() : "-";
            GUILayout.Label($"Seed {context.Seed} / {template} / {flow.State} {context.TimeLeft:0.0}s");
            GUILayout.Label($"Room {room} / speed {motor.Velocity.magnitude:0.00} m/s");
            GUILayout.Label($"Steps {steps} {(stepFlash > 0f ? (lastFoot == 0 ? "  [L]" : "        [R]") : "")}");
            GUILayout.Label("WASD: walk / Click: lock mouse / R: new house / Tab: panel");

            if (panel) {
                GUILayout.Space(6f);
                tuning.walkSpeed = Slider("Walk speed (m/s)", tuning.walkSpeed, 1f, 6f);
                tuning.backwardSpeedScale = Slider("Backward scale", tuning.backwardSpeedScale, 0.3f, 1f);
                tuning.strafeSpeedScale = Slider("Strafe scale", tuning.strafeSpeedScale, 0.3f, 1f);
                tuning.walkAcceleration = Slider("Acceleration (m/s²)", tuning.walkAcceleration, 2f, 60f);
                tuning.walkDeceleration = Slider("Deceleration (m/s²)", tuning.walkDeceleration, 2f, 60f);
                tuning.eyeHeight = Slider("Eye height (m)", tuning.eyeHeight, 1.2f, 1.8f);
                tuning.stepLength = Slider("Step length (m)", tuning.stepLength, 0.3f, 1.2f);
                tuning.bobHeight = Slider("Bob height (m)", tuning.bobHeight, 0f, 0.12f, "0.000");
                tuning.swayWidth = Slider("Sway width (m)", tuning.swayWidth, 0f, 0.08f, "0.000");
                tuning.swayRoll = Slider("Sway roll (deg)", tuning.swayRoll, 0f, 3f);
                tuning.bobResponse = Slider("Bob response (1/s)", tuning.bobResponse, 1f, 30f);
                if (GUILayout.Button("Reset to values at start")) {
                    Restore();
                }
            }

            GUILayout.EndArea();
        }

        private void Restore() {
            tuning.walkSpeed = original.walkSpeed;
            tuning.backwardSpeedScale = original.backwardSpeedScale;
            tuning.strafeSpeedScale = original.strafeSpeedScale;
            tuning.walkAcceleration = original.walkAcceleration;
            tuning.walkDeceleration = original.walkDeceleration;
            tuning.eyeHeight = original.eyeHeight;
            tuning.stepLength = original.stepLength;
            tuning.bobHeight = original.bobHeight;
            tuning.swayWidth = original.swayWidth;
            tuning.swayRoll = original.swayRoll;
            tuning.bobResponse = original.bobResponse;
        }

        private static float Slider(string label, float value, float min, float max, string format = "0.00") {
            GUILayout.Label($"{label}: {value.ToString(format)}");
            return GUILayout.HorizontalSlider(value, min, max);
        }
    }
}
