using MissedCall.Core.Aim;
using MissedCall.Core.Round;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 화면 가운데 조준점과 라벨. <see cref="RoundContext.AimTarget"/>의 종류에 따라
    /// 물건은 노랗게 1.7배, 폰은 초록색으로 2.3배 커진다 (프로토타입 #aim).
    /// </summary>
    public sealed class AimView : MonoBehaviour {
        [SerializeField] private RectTransform ring;
        [SerializeField] private Image ringImage;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float itemScale = 1.7f;
        [SerializeField] private float phoneScale = 2.3f;
        [Tooltip("조준점 크기와 색이 바뀌는 시간(초)")]
        [SerializeField] private float transition = 0.08f;

        private GameFlow flow;
        private float scale = 1f;
        // 라벨이 보이는 정도 0~1. 켜고 끌 때 짧게 나타나고 사라진다.
        private float labelShown;
        [Tooltip("라벨이 나타나고 사라지는 시간(초)")]
        [SerializeField] private float labelFade = 0.08f;

        public void Bind(GameFlow gameFlow) {
            flow = gameFlow;
        }

        private void OnEnable() {
            scale = 1f;
            labelShown = 0f;
            Apply(null, true);
        }

        private void Update() {
            if (flow != null) {
                Apply(flow.Context.AimTarget, false);
            }
        }

        private void Apply(IAimTarget target, bool instant) {
            float targetScale = 1f;
            Color color = UiPalette.Paper;
            string text = null;
            if (target != null) {
                switch (target.Kind) {
                    case AimKind.Phone:
                        targetScale = phoneScale;
                        color = UiPalette.Answer;
                        text = "Grab phone";
                        break;
                    case AimKind.Door:
                        targetScale = itemScale;
                        color = UiPalette.Ring;
                        text = "Open";
                        break;
                    default:
                        targetScale = itemScale;
                        color = UiPalette.Ring;
                        text = "Grab";
                        break;
                }
            }

            scale = instant || transition <= 0f
                ? targetScale
                : Mathf.MoveTowards(scale, targetScale, Time.unscaledDeltaTime / transition * (phoneScale - 1f));
            ring.localScale = new Vector3(scale, scale, 1f);
            ringImage.color = color;
            if (text != null) {
                label.text = text;
                label.color = target.Kind == AimKind.Phone ? UiPalette.Answer : UiPalette.Paper;
            }

            float step = instant || labelFade <= 0f ? 1f : Time.unscaledDeltaTime / labelFade;
            labelShown = Mathf.MoveTowards(labelShown, text != null ? 1f : 0f, step);
            label.gameObject.SetActive(labelShown > 0f);
            label.alpha = labelShown;
            // 위에서 살짝 내려오며 나타난다
            float lift = (1f - labelShown) * 6f;
            label.rectTransform.anchoredPosition = new Vector2(0f, -22f + lift);
        }
    }
}
