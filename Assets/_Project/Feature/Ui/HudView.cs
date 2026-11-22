using MissedCall.Core.Round;
using TMPro;
using UnityEngine;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 위쪽 가운데 타이머와 판 시작 힌트. 남은 시간이 적으면 타이머가 빨갛게 바뀌고 초가 넘어갈 때마다 튄다.
    /// 힌트는 끝날 때 서서히 사라진다.
    /// </summary>
    public sealed class HudView : MonoBehaviour {
        [SerializeField] private TMP_Text clock;
        [SerializeField] private TMP_Text hint;
        [Tooltip("남은 시간이 적을 때 초가 넘어가는 순간 타이머가 커지는 비율")]
        [SerializeField] private float tickPunch = 0.18f;
        [SerializeField] private float tickDecay = 9f;
        [Tooltip("힌트가 사라지는 시간(초)")]
        [SerializeField] private float hintFade = 0.5f;

        private GameFlow flow;
        private float hintUntil;
        private int lastSecond = -1;
        private float punch;

        public void Bind(GameFlow gameFlow) {
            flow = gameFlow;
        }

        /// <summary>새 판이 시작될 때 부른다. 힌트는 실제 시간으로 hintDuration 동안 보인다.</summary>
        public void ShowHint() {
            hint.gameObject.SetActive(true);
            hint.alpha = 1f;
            hintUntil = Time.unscaledTime + flow.Tuning.hintDuration;
            lastSecond = -1;
            punch = 0f;
        }

        private void Update() {
            if (flow == null) {
                return;
            }

            float left = flow.Context.TimeLeft;
            bool low = left <= flow.Tuning.lowTimeWarning;
            clock.SetText("{0:1}", left);
            clock.color = low ? UiPalette.Miss : UiPalette.Ring;

            int second = Mathf.CeilToInt(left);
            if (low && second != lastSecond && lastSecond >= 0 && !flow.IsPaused && flow.State == RoundState.Playing) {
                punch = tickPunch;
            }

            lastSecond = second;
            punch *= Mathf.Exp(-tickDecay * Time.unscaledDeltaTime);
            float scale = 1f + punch;
            clock.rectTransform.localScale = new Vector3(scale, scale, 1f);

            if (hint.gameObject.activeSelf) {
                float fade = hintFade > 0f ? (hintUntil - Time.unscaledTime) / hintFade : 0f;
                hint.alpha = Mathf.Clamp01(fade);
                if (fade <= 0f) {
                    hint.gameObject.SetActive(false);
                }
            }
        }
    }
}
