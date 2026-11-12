using MissedCall.Core.Aim;
using MissedCall.Core.Round;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 임시 화면. Ui 단계(타이틀, HUD, 받기 슬라이드, 결과)가 생기면 지운다.
    /// 타이틀에서 클릭하면 판을 시작하고(같은 클릭이 포인터 잠금과 오디오 시작에도 쓰인다), 받기 화면에서는 클릭이나 Space로 받는다.
    /// 결과 화면에서 클릭하거나 R을 누르면 새 판이다. 문구는 game-design.md를 따른다.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour {
        [SerializeField] private GameFlow flow;
        [Tooltip("결과가 나온 뒤 이 시간(초) 동안은 클릭으로 새 판을 시작하지 않는다. 마지막 클릭이 바로 넘기지 않게 한다")]
        [SerializeField] private float resultLock = 0.6f;

        private float endedAt;
        private float missedDistance;

        private void OnEnable() {
            flow.StateChanged += OnStateChanged;
        }

        private void OnDisable() {
            flow.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(RoundState previous, RoundState next) {
            if (next is RoundState.Won or RoundState.Missed) {
                endedAt = Time.unscaledTime;
                Vector3 offset = flow.Context.PhonePosition - flow.Context.PlayerPosition;
                offset.y = 0f;
                missedDistance = offset.magnitude;
            }
        }

        private void Update() {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool space = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            switch (flow.State) {
                case RoundState.Title:
                    if (click || space) {
                        flow.StartRound();
                    }

                    break;
                case RoundState.Answer:
                    if (click || space) {
                        flow.Answer();
                    }

                    break;
                case RoundState.Won:
                case RoundState.Missed:
                    if ((click || space) && Time.unscaledTime - endedAt > resultLock) {
                        flow.StartRound();
                    }

                    break;
            }
        }

        private void OnGUI() {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            RoundContext context = flow.Context;
            var center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
            var big = new GUIStyle(center) { fontSize = 40 };

            switch (flow.State) {
                case RoundState.Title:
                    GUI.Box(new Rect(0f, 0f, width, height), GUIContent.none);
                    GUI.Label(new Rect(0f, height * 0.3f, width, 60f), "Missed Call", big);
                    GUI.Label(new Rect(0f, height * 0.42f, width, 40f), "Your phone is ringing somewhere. Find it and pick up before it stops.", center);
                    GUI.Label(new Rect(0f, height * 0.55f, width, 40f), "Click to start", center);
                    GUI.Label(new Rect(0f, height * 0.65f, width, 40f), "Headphones make the direction much easier to hear.", center);
                    return;
                case RoundState.Playing:
                    GUI.Label(new Rect(0f, 16f, width, 50f), context.TimeLeft.ToString("0.0"), big);
                    DrawCrosshair(width, height, center);
                    break;
                case RoundState.Answer:
                    GUI.Label(new Rect(0f, 16f, width, 50f), context.TimeLeft.ToString("0.0"), big);
                    GUI.Label(new Rect(0f, height * 0.4f, width, 60f), "Incoming call", big);
                    GUI.Label(new Rect(0f, height * 0.52f, width, 40f), "Pick up! (click)", center);
                    break;
                case RoundState.Won:
                    GUI.Label(new Rect(0f, height * 0.38f, width, 60f), "Hello?", big);
                    GUI.Label(new Rect(0f, height * 0.5f, width, 40f), $"Answered in {context.AnswerTime:0.0}s", center);
                    GUI.Label(new Rect(0f, height * 0.6f, width, 40f), "Click or R to play again", center);
                    break;
                case RoundState.Missed:
                    GUI.Label(new Rect(0f, height * 0.38f, width, 60f), "1 Missed Call", big);
                    GUI.Label(new Rect(0f, height * 0.5f, width, 40f), $"Your phone was {missedDistance:0.0}m away", center);
                    GUI.Label(new Rect(0f, height * 0.6f, width, 40f), "Click or R to play again", center);
                    break;
            }

            GUI.Label(new Rect(8f, height - 26f, 400f, 22f), $"Seed {context.Seed}  (debug HUD)");
        }

        private void DrawCrosshair(float width, float height, GUIStyle style) {
            IAimTarget target = flow.Context.AimTarget;
            string label = target == null ? "+"
                : target.Kind == AimKind.Phone ? "( + )\nGrab phone"
                : target.Kind == AimKind.Door ? "+\nOpen"
                : "+\nGrab";
            var crosshair = new GUIStyle(style) { fontSize = 18, alignment = TextAnchor.UpperCenter };
            GUI.Label(new Rect(0f, height * 0.5f - 12f, width, 60f), label, crosshair);
        }
    }
}
