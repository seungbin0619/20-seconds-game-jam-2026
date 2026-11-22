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
        [Tooltip("판 변수를 알리는 강조색 태그 (바탕과 글자)")]
        [SerializeField] private RectTransform modifierTag;
        [SerializeField] private TMP_Text modifierText;
        [Tooltip("남은 시간이 적을 때 초가 넘어가는 순간 타이머가 커지는 비율")]
        [SerializeField] private float tickPunch = 0.18f;
        [SerializeField] private float tickDecay = 9f;
        [Tooltip("힌트가 사라지는 시간(초)")]
        [SerializeField] private float hintFade = 0.5f;

        private GameFlow flow;
        private float hintUntil;
        private float hintShownAt;
        private int lastSecond = -1;
        private float punch;

        public void Bind(GameFlow gameFlow) {
            flow = gameFlow;
        }

        /// <summary>새 판이 시작될 때 부른다. 힌트는 실제 시간으로 hintDuration 동안 보인다.</summary>
        public void ShowHint() {
            hint.gameObject.SetActive(true);
            hint.alpha = 0f;
            hintShownAt = Time.unscaledTime;
            hintUntil = Time.unscaledTime + flow.Tuning.hintDuration;
            string label = ModifierLabel(flow.Context.Modifier);
            if (modifierTag != null) {
                modifierTag.gameObject.SetActive(label != null);
                if (label != null) {
                    modifierText.text = label;
                }
            }
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

            UpdateModifierTag();
            if (hint.gameObject.activeSelf) {
                float fade = hintFade > 0f ? (hintUntil - Time.unscaledTime) / hintFade : 0f;
                float appear = hintFade > 0f ? (Time.unscaledTime - hintShownAt) / (hintFade * 0.6f) : 1f;
                hint.alpha = Mathf.Clamp01(Mathf.Min(fade, appear));
                if (fade <= 0f) {
                    hint.gameObject.SetActive(false);
                }
            }
        }

        // 판 변수 태그: 힌트보다 조금 오래 보이고, 옆에서 튀어 들어왔다가 옆으로 빠진다.
        private void UpdateModifierTag() {
            if (modifierTag == null || !modifierTag.gameObject.activeSelf) {
                return;
            }

            float shown = Time.unscaledTime - hintShownAt;
            float until = hintUntil + 1.5f - Time.unscaledTime;
            float k = Mathf.Clamp01(Mathf.Min(shown / 0.18f, until / 0.15f));
            float ease = 1f - (1f - k) * (1f - k);
            modifierTag.anchoredPosition = new Vector2((1f - ease) * -60f, modifierTag.anchoredPosition.y);
            modifierTag.localScale = new Vector3(Mathf.Lerp(1.15f, 1f, ease), Mathf.Lerp(1.15f, 1f, ease), 1f);
            var group = modifierTag.GetComponent<CanvasGroup>();
            if (group != null) {
                group.alpha = ease;
            }

            if (until <= 0f) {
                modifierTag.gameObject.SetActive(false);
            }
        }

        /// <summary>판 변수 이름 (게임 안 문구, game-design.md).</summary>
        public static string ModifierLabel(RoundModifier modifier) {
            switch (modifier) {
                case RoundModifier.Vibrate: return "Vibrate only";
                case RoundModifier.LowBattery: return "Low battery";
                case RoundModifier.Decoy: return "Something else is ringing";
                case RoundModifier.Noise: return "Noisy house";
                case RoundModifier.Blackout: return "Blackout";
                default: return null;
            }
        }
    }
}
