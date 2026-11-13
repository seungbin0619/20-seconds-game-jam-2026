using System;
using MissedCall.Core.Round;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 게임 화면 전체. 상태에 맞는 화면을 켜고 버튼을 받는다 (타이틀의 Ring It, 결과의 Try Again, 일시정지 메뉴).
    /// 버튼은 커서로 버튼 위를 <b>누르는 순간</b> 동작한다. 같은 클릭의 mouseup에서 브라우저가 포인터를 잠글 수 있게 하기 위해서다
    /// (uGUI 버튼은 뗄 때 눌려서 잠금 요청이 다음 입력으로 밀린다). 그래서 EventSystem 없이 직접 판정한다.
    /// 일시정지는 판 중에 포인터 잠금이 풀리면 PointerLook이 걸고, 잠금이 없을 때(받기 화면 등) Esc는 여기서 건다.
    /// 프리팹은 <c>Missed Call/Ui/Build Game Ui</c> 메뉴로 다시 만든다.
    /// </summary>
    public sealed class GameUi : MonoBehaviour {
        [SerializeField] private GameFlow flow;

        [SerializeField] private GameObject titleScreen;
        [SerializeField] private HudView hud;
        [SerializeField] private AimView aim;
        [SerializeField] private AnswerSlide answer;
        [SerializeField] private ResultView result;
        [SerializeField] private GameObject pauseScreen;
        [Tooltip("일시정지 중 Esc를 누르면 메뉴 대신 보이는 'Click to resume' 안내")]
        [SerializeField] private GameObject resumeHint;
        [Tooltip("멈춘 직후 이 시간(초) 동안은 Esc로 재개하지 않는다. 잠금을 푼 Esc가 같은 순간 키 입력으로도 들어와 메뉴가 바로 닫히는 것을 막는다")]
        [SerializeField] private float escResumeDelay = 0.25f;
        [SerializeField] private Image startButton;
        [SerializeField] private Image retryButton;
        [SerializeField] private Image resumeButton;
        [SerializeField] private Image restartButton;
        [SerializeField] private Image titleButton;
        [Tooltip("일시정지 메뉴의 남은 시간")]
        [SerializeField] private TMP_Text pauseTime;
        [Tooltip("휴대폰 잠금 화면처럼 지금 시각(HH:mm)을 보여주는 글자")]
        [SerializeField] private TMP_Text[] lockClocks = new TMP_Text[0];

        [Tooltip("포인터가 풀린 동안(타이틀, 받기, 결과, 일시정지) 쓰는 커서. 가운데 조준점과 같은 링 모양")]
        [SerializeField] private Texture2D cursor;
        [SerializeField] private Vector2 cursorHotspot = new(16f, 16f);
        [Tooltip("커서가 버튼 위에 있을 때 버튼 색을 밝히는 비율")]
        [SerializeField] private float hoverBrighten = 0.12f;

        private float endedAt;
        private int shownMinute = -1;
        private bool cursorApplied;
        private float pausedAt;
        // 일시정지 중 Esc를 눌러 메뉴를 닫고 클릭을 기다리는 중
        private bool awaitingClick;

        private void Awake() {
            hud.Bind(flow);
            aim.Bind(flow);
            answer.Bind(flow);
        }

        private void OnEnable() {
            cursorApplied = false;
            flow.StateChanged += OnStateChanged;
            flow.PausedChanged += OnPausedChanged;
            Show();
        }

        private void OnDisable() {
            flow.StateChanged -= OnStateChanged;
            flow.PausedChanged -= OnPausedChanged;
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

            Show();
        }

        private void OnPausedChanged(bool paused) {
            if (paused) {
                pauseTime.SetText("{0:1}s left", flow.Context.TimeLeft);
                pausedAt = Time.unscaledTime;
            }

            awaitingClick = false;
            Show();
        }

        private void Show() {
            RoundState state = flow.State;
            bool paused = flow.IsPaused;
            titleScreen.SetActive(state == RoundState.Title);
            hud.gameObject.SetActive(GameFlow.IsRunning(state));
            aim.gameObject.SetActive(state == RoundState.Playing && !paused);
            answer.gameObject.SetActive(state == RoundState.Answer && !paused);
            result.gameObject.SetActive(state is RoundState.Won or RoundState.Missed);
            pauseScreen.SetActive(paused && !awaitingClick);
            resumeHint.SetActive(paused && awaitingClick);
            shownMinute = -1;
        }

        private void Update() {
            RoundState state = flow.State;
            if (flow.IsPaused) {
                UpdatePaused(state);
                return;
            }

            switch (state) {
                case RoundState.Title:
                    UpdateLockClocks();
                    if (Clicked(startButton, true)) {
                        flow.StartRound();
                    }

                    break;
                case RoundState.Won:
                case RoundState.Missed:
                    UpdateLockClocks();
                    // 결과 직후에는 슬라이드나 던지기의 마지막 클릭이 버튼을 누르지 않게 잠시 막는다.
                    if (Time.unscaledTime - endedAt >= flow.Tuning.resultInputDelay && Clicked(retryButton, true)) {
                        flow.StartRound();
                    }

                    break;
                default:
                    // 잠긴 동안의 Esc는 브라우저가 가져가므로 PointerLook이 잠금 해제로 알아챈다.
                    // 잠금이 없을 때(받기 화면, 잠금 실패)의 Esc는 여기 들어온다.
                    Keyboard keyboard = Keyboard.current;
                    if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) {
                        flow.Pause();
                    }

                    break;
            }
        }

        // 일시정지 중. Esc는 재개다. 다만 브라우저는 Esc 키 안에서 포인터 잠금을 허용하지 않으므로,
        // playing에서는 메뉴를 닫고 'Click to resume'을 띄운 뒤 다음 클릭에서 재개한다 (그 클릭으로 잠긴다).
        // 잠금이 필요 없는 받기 화면에서는 Esc로 바로 재개한다.
        private void UpdatePaused(RoundState state) {
            Keyboard keyboard = Keyboard.current;
            bool esc = keyboard != null && keyboard.escapeKey.wasPressedThisFrame
                && Time.unscaledTime - pausedAt >= escResumeDelay;

            if (awaitingClick) {
                Mouse mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) {
                    flow.Resume();
                } else if (esc) {
                    awaitingClick = false;
                    Show();
                }

                return;
            }

            if (esc) {
                if (state == RoundState.Playing) {
                    awaitingClick = true;
                    Show();
                } else {
                    flow.Resume();
                }

                return;
            }

            if (Clicked(resumeButton, true)) {
                flow.Resume();
            } else if (Clicked(restartButton, false)) {
                flow.StartRound();
            } else if (Clicked(titleButton, false)) {
                flow.ReturnToTitle();
            }
        }

        // 커서가 버튼 위에 있으면 밝히고, 그 위에서 누른 프레임에 true.
        private bool Clicked(Image button, bool primary) {
            Mouse mouse = Mouse.current;
            bool hovered = mouse != null && Cursor.lockState != CursorLockMode.Locked
                && RectTransformUtility.RectangleContainsScreenPoint(button.rectTransform, mouse.position.ReadValue(), null);
            Color normal = primary ? UiPalette.Ring : UiPalette.Secondary;
            Color hover = primary ? Color.Lerp(UiPalette.Ring, Color.white, hoverBrighten) : UiPalette.SecondaryHover;
            button.color = hovered ? hover : normal;
            return hovered && mouse.leftButton.wasPressedThisFrame;
        }

        // Web에서는 커서를 다시 보이게 하면(Cursor.visible = true) 캔버스 커서가 기본 화살표로 돌아간다.
        // 포인터 잠금이 풀려 커서가 다시 보인 프레임마다 전용 커서를 다시 입힌다. LateUpdate라서 같은 프레임의 해제 뒤에 온다.
        private void LateUpdate() {
            bool shown = Cursor.visible && Cursor.lockState != CursorLockMode.Locked;
            if (!shown) {
                cursorApplied = false;
            } else if (!cursorApplied && cursor != null) {
                Cursor.SetCursor(cursor, cursorHotspot, CursorMode.Auto);
                cursorApplied = true;
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
