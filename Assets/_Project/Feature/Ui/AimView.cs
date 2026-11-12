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

        public void Bind(GameFlow gameFlow) {
            flow = gameFlow;
        }

        private void OnEnable() {
            scale = 1f;
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
            label.gameObject.SetActive(text != null);
            if (text != null) {
                label.text = text;
                label.color = target.Kind == AimKind.Phone ? UiPalette.Answer : UiPalette.Paper;
            }
        }
    }
}
