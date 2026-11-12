using System;
using MissedCall.Core.Round;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 게임 화면 전체. 상태에 맞는 화면을 켜고, 타이틀의 Ring It과 결과의 Try Again 버튼을 받는다.
    /// 버튼은 커서로 버튼 위를 <b>누르는 순간</b> 판을 시작한다. 같은 클릭의 mouseup에서 브라우저가 포인터를 잠글 수 있게 하기 위해서다
    /// (uGUI 버튼은 뗄 때 눌려서 잠금 요청이 다음 입력으로 밀린다). 그래서 EventSystem 없이 직접 판정한다.
    /// 프리팹은 <c>Missed Call/Ui/Build Game Ui</c> 메뉴로 다시 만든다.
    /// </summary>
    public sealed class GameUi : MonoBehaviour {
        [SerializeField] private GameFlow flow;

        [SerializeField] private GameObject titleScreen;
        [SerializeField] private HudView hud;
        [SerializeField] private AimView aim;
        [SerializeField] private AnswerSlide answer;
        [SerializeField] private ResultView result;
        [SerializeField] private Image startButton;
        [SerializeField] private Image retryButton;
        [Tooltip("휴대폰 잠금 화면처럼 지금 시각(HH:mm)을 보여주는 글자")]
        [SerializeField] private TMP_Text[] lockClocks = new TMP_Text[0];

        [Tooltip("포인터가 풀린 동안(타이틀, 받기, 결과) 쓰는 커서. 가운데 조준점과 같은 링 모양")]
        [SerializeField] private Texture2D cursor;
        [SerializeField] private Vector2 cursorHotspot = new(16f, 16f);
        [Tooltip("커서가 버튼 위에 있을 때 버튼 색을 밝히는 비율")]
        [SerializeField] private float hoverBrighten = 0.12f;

        private float endedAt;
        private int shownMinute = -1;

        private void Awake() {
            hud.Bind(flow);
            aim.Bind(flow);
            answer.Bind(flow);
        }

        private void OnEnable() {
            if (cursor != null) {
                Cursor.SetCursor(cursor, cursorHotspot, CursorMode.Auto);
            }

            flow.StateChanged += OnStateChanged;
            Show(flow.State);
        }

        private void OnDisable() {
            flow.StateChanged -= OnStateChanged;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void OnStateChanged(RoundState previous, RoundState next) {
            switch (next) {
                case RoundState.Playing:
                    hud.ShowHint();
                    break;
                case RoundState.Answer:
                    answer.Begin();
                    break;
                case RoundState.Won:
                case RoundState.Missed:
                    endedAt = Time.unscaledTime;
                    result.Show(flow.Context, next == RoundState.Won);
                    break;
            }

            Show(next);
        }

        private void Show(RoundState state) {
            titleScreen.SetActive(state == RoundState.Title);
            hud.gameObject.SetActive(GameFlow.IsRunning(state));
            aim.gameObject.SetActive(state == RoundState.Playing);
            answer.gameObject.SetActive(state == RoundState.Answer);
            result.gameObject.SetActive(state is RoundState.Won or RoundState.Missed);
            shownMinute = -1;
        }

        private void Update() {
            RoundState state = flow.State;
            Image button = state switch {
                RoundState.Title => startButton,
                RoundState.Won or RoundState.Missed => retryButton,
                _ => null,
            };
            if (button == null) {
                return;
            }

            UpdateLockClocks();
            // 결과 직후에는 슬라이드나 던지기의 마지막 클릭이 버튼을 누르지 않게 잠시 막는다.
            bool ready = state == RoundState.Title || Time.unscaledTime - endedAt >= flow.Tuning.resultInputDelay;
            Mouse mouse = Mouse.current;
            bool hovered = ready && mouse != null && Cursor.lockState != CursorLockMode.Locked
                && RectTransformUtility.RectangleContainsScreenPoint(button.rectTransform, mouse.position.ReadValue(), null);
            button.color = hovered ? Color.Lerp(UiPalette.Ring, Color.white, hoverBrighten) : UiPalette.Ring;
            if (hovered && mouse.leftButton.wasPressedThisFrame) {
                flow.StartRound();
            }
        }

        private void UpdateLockClocks() {
            DateTime now = DateTime.Now;
            int minute = now.Hour * 60 + now.Minute;
            if (minute == shownMinute) {
                return;
            }

            shownMinute = minute;
            foreach (TMP_Text clock in lockClocks) {
                clock.SetText("{0:00}:{1:00}", now.Hour, now.Minute);
            }
        }
    }
}
