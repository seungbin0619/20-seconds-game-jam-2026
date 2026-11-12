using MissedCall.Core.Round;
using TMPro;
using UnityEngine;

namespace MissedCall.Feature.Ui {
    /// <summary>위쪽 가운데 타이머와 판 시작 힌트. 남은 시간이 적으면 타이머가 빨갛게 바뀐다.</summary>
    public sealed class HudView : MonoBehaviour {
        [SerializeField] private TMP_Text clock;
        [SerializeField] private TMP_Text hint;

        private GameFlow flow;
        private float hintUntil;

        public void Bind(GameFlow gameFlow) {
            flow = gameFlow;
        }

        /// <summary>새 판이 시작될 때 부른다. 힌트는 실제 시간으로 hintDuration 동안 보인다.</summary>
        public void ShowHint() {
            hint.gameObject.SetActive(true);
            hintUntil = Time.unscaledTime + flow.Tuning.hintDuration;
        }

        private void Update() {
            if (flow == null) {
                return;
            }

            float left = flow.Context.TimeLeft;
            clock.SetText("{0:1}", left);
            clock.color = left <= flow.Tuning.lowTimeWarning ? UiPalette.Miss : UiPalette.Ring;
            if (hint.gameObject.activeSelf && Time.unscaledTime >= hintUntil) {
                hint.gameObject.SetActive(false);
            }
        }
    }
}
