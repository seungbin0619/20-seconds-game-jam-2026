using MissedCall.Core.Callers;
using MissedCall.Core.Round;
using TMPro;
using UnityEngine;

namespace MissedCall.Feature.Ui {
    /// <summary>
    /// 결과 화면. 성공이면 받은 시간, 실패면 폰까지의 거리와 함께 발신자의 말을 보여준다.
    /// 숫자는 0에서 올라가고, 발신자의 말은 패널이 뜬 뒤 한 박자 늦게 나타난다.
    /// </summary>
    public sealed class ResultView : MonoBehaviour {
        [SerializeField] private TMP_Text head;
        [SerializeField] private TMP_Text caller;
        [SerializeField] private TMP_Text quote;
        [SerializeField] private TMP_Text stat;
        [Tooltip("숫자가 올라가는 시간(초)")]
        [SerializeField] private float countTime = 0.3f;
        [Tooltip("발신자의 말이 나타나기 시작하는 때와 걸리는 시간(초)")]
        [SerializeField] private float quoteDelay = 0.12f;
        [SerializeField] private float quoteFade = 0.2f;

        // 이 가중치보다 작으면 희귀 발신자로 본다.
        private const float RareWeight = 0.5f;

        private bool won;
        private float value;
        private float shownAt;

        public void Show(RoundContext context, bool answered) {
            won = answered;
            shownAt = Time.unscaledTime;
            CallerProfile profile = context.Caller;
            caller.text = profile != null ? profile.displayName : "Unknown Number";
            // 희귀 발신자는 이름 옆에 노란 표시
            if (profile != null && profile.weight < RareWeight) {
                caller.text += "  <color=#FFD23F><size=70%>RARE CALLER</size></color>";
            }
            if (won) {
                head.text = "Hello?";
                head.color = UiPalette.Answer;
                quote.text = profile != null ? profile.answeredLine : string.Empty;
                value = context.AnswerTime;
            } else {
                Vector3 offset = context.PhonePosition - context.PlayerPosition;
                offset.y = 0f;
                head.text = "1 Missed Call";
                head.color = UiPalette.Miss;
                quote.text = profile != null ? profile.missedLine : string.Empty;
                value = offset.magnitude;
            }

            quote.gameObject.SetActive(!string.IsNullOrEmpty(quote.text));
            quote.alpha = 0f;
            SetStat(0f);
        }

        private void Update() {
            float t = Time.unscaledTime - shownAt;
            float k = countTime > 0f ? Mathf.Clamp01(t / countTime) : 1f;
            // 끝에서 느려지게
            SetStat(value * (1f - (1f - k) * (1f - k)));
            quote.alpha = quoteFade > 0f ? Mathf.Clamp01((t - quoteDelay) / quoteFade) : 1f;
        }

        private void SetStat(float shown) {
            if (won) {
                stat.SetText("Answered in {0:1}s", shown);
            } else {
                stat.SetText("Your phone was {0:1}m away", shown);
            }
        }
    }
}
