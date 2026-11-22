using MissedCall.Core;
using MissedCall.Core.Round;
using TMPro;
using UnityEngine;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 위쪽 가운데 타이머와 판 시작 힌트. 초가 넘어갈 때마다 숫자 잔상이 옆으로 밀려나며 사라지고 뒤의 판이 살짝 기울었다
    /// 돌아온다. 남은 시간이 적으면 타이머가 빨갛게 바뀌고, 조금 튀고, 미세하게 떨리며, 판이 박자에 맞춰 붉게 물든다.
    /// 크기 변화는 작게 둔다. 힌트는 서서히 나타나고 사라진다.
    /// </summary>
    public sealed class HudView : MonoBehaviour {
        [SerializeField] private TMP_Text clock;
        [SerializeField] private TMP_Text hint;
        [Tooltip("판 변수를 알리는 강조색 태그 (바탕과 글자)")]
        [SerializeField] private RectTransform modifierTag;
        [SerializeField] private TMP_Text modifierText;
        [Tooltip("숫자 잔상 (타이머와 같은 모양, 타이머 뒤)")]
        [SerializeField] private TMP_Text clockGhost;
        [Tooltip("타이머 뒤 기울인 판")]
        [SerializeField] private UnityEngine.UI.Image clockPlate;
        [Tooltip("남은 시간이 적을 때 초가 넘어가는 순간 타이머가 커지는 비율")]
        [SerializeField] private float tickPunch = 0.07f;
        [Tooltip("잔상이 밀려나는 거리(px)와 사라지는 시간(초)")]
        [SerializeField] private float ghostTravel = 22f;
        [SerializeField] private float ghostTime = 0.4f;
        [Tooltip("초가 넘어갈 때 판이 기우는 각도(도)")]
        [SerializeField] private float plateKick = 4f;
        [Tooltip("남은 시간이 적을 때 숫자가 떨리는 폭(px)")]
        [SerializeField] private float lowShake = 1.6f;
        [SerializeField] private float tickDecay = 9f;
        [Tooltip("힌트가 사라지는 시간(초)")]
        [SerializeField] private float hintFade = 0.5f;

        private GameFlow flow;
        private float hintUntil;
        private float hintShownAt;
        private int lastSecond = -1;
        private float punch;
        private float ghostAt = -10f;
        private float kick;
        private int ghostSide = 1;
        private Vector2 clockHome;
        private Vector2 ghostHome;
        private bool homes;

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
            kick = 0f;
            ghostAt = -10f;
        }

        private void Update() {
            if (flow == null) {
                return;
            }

            float left = flow.Context.TimeLeft;
            bool low = left <= flow.Tuning.lowTimeWarning;
            clock.SetText("{0:1}", left);
            clock.color = low ? UiPalette.Miss : UiPalette.Ring;

            if (!homes) {
                clockHome = clock.rectTransform.anchoredPosition;
                ghostHome = clockGhost != null ? clockGhost.rectTransform.anchoredPosition : clockHome;
                homes = true;
            }

            int second = Mathf.CeilToInt(left);
            bool running = !flow.IsPaused && flow.State == RoundState.Playing;
            if (second != lastSecond && lastSecond >= 0 && running) {
                Tick(low);
            }

            lastSecond = second;
            float dt = Time.unscaledDeltaTime;
            punch *= Mathf.Exp(-tickDecay * dt);
            kick *= Mathf.Exp(-10f * dt);
            float scale = 1f + punch;
            clock.rectTransform.localScale = new Vector3(scale, scale, 1f);

            // 마지막 구간: 미세하게 떨린다 (프레임마다 말고 1/30초마다 자리를 바꾼다).
            float lowAmount = low ? 1f - left / Mathf.Max(0.01f, flow.Tuning.lowTimeWarning) : 0f;
            Vector2 shake = Vector2.zero;
            if (low && running) {
                float step = Mathf.Floor(Time.unscaledTime * 30f);
                shake = new Vector2(Hash(step) * 2f - 1f, Hash(step + 7.1f) * 2f - 1f) * lowShake * (0.4f + 0.6f * lowAmount);
            }

            clock.rectTransform.anchoredPosition = clockHome + shake;
            UpdateGhost(low);
            UpdatePlate(low, lowAmount);

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

        private void Tick(bool low) {
            punch = low ? tickPunch : 0f;
            kick = plateKick * (low ? 1.5f : 1f);
            ghostAt = Time.unscaledTime;
            ghostSide = -ghostSide;
            if (clockGhost != null) {
                clockGhost.text = clock.text;
            }
        }

        // 초가 넘어갈 때 그 순간의 숫자가 옆으로 밀려나며 사라진다. 마지막 구간에는 더 진하고 좌우를 번갈아 간다.
        private void UpdateGhost(bool low) {
            if (clockGhost == null) {
                return;
            }

            float t = ghostTime > 0f ? (Time.unscaledTime - ghostAt) / ghostTime : 1f;
            if (t >= 1f) {
                clockGhost.alpha = 0f;
                return;
            }

            float ease = 1f - (1f - t) * (1f - t);
            int side = low ? ghostSide : 1;
            clockGhost.rectTransform.anchoredPosition = ghostHome + new Vector2(ghostTravel * ease * side, -3f * ease);
            Color color = clock.color;
            color.a = (low ? 0.6f : 0.35f) * (1f - t) * (1f - t);
            clockGhost.color = color;
        }

        // 뒤의 판: 초가 넘어갈 때 살짝 기울었다 돌아오고, 마지막 구간에는 박자에 맞춰 붉게 물든다.
        private void UpdatePlate(bool low, float lowAmount) {
            if (clockPlate == null) {
                return;
            }

            clockPlate.rectTransform.localEulerAngles = new Vector3(0f, 0f, kick);
            Color color = UiPalette.Night;
            if (low) {
                GameTuning tuning = flow.Tuning;
                float interval = Mathf.Max(0.05f, tuning.jiggleInterval);
                float phase = Mathf.Repeat((flow.Context.Elapsed - tuning.jiggleDelay) / interval, 1f);
                float beat = Mathf.Exp(-phase * 6f);
                color = Color.Lerp(UiPalette.Night, UiPalette.Miss * 0.55f, beat * (0.35f + 0.5f * lowAmount));
            }

            clockPlate.color = color;
        }

        private static float Hash(float x) {
            float s = Mathf.Sin(x * 12.9898f) * 43758.547f;
            return s - Mathf.Floor(s);
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
