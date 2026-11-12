using MissedCall.Core.Callers;
using MissedCall.Core.Round;
using TMPro;
using UnityEngine;

namespace MissedCall.Feature.Ui {
    /// <summary>결과 화면. 성공이면 받은 시간, 실패면 폰까지의 거리와 함께 발신자의 말을 보여준다.</summary>
    public sealed class ResultView : MonoBehaviour {
        [SerializeField] private TMP_Text head;
        [SerializeField] private TMP_Text caller;
        [SerializeField] private TMP_Text quote;
        [SerializeField] private TMP_Text stat;

        public void Show(RoundContext context, bool won) {
            CallerProfile profile = context.Caller;
            caller.text = profile != null ? profile.displayName : "Unknown Number";
            if (won) {
                head.text = "Hello?";
                head.color = UiPalette.Answer;
                quote.text = profile != null ? profile.answeredLine : string.Empty;
                stat.SetText("Answered in {0:1}s", context.AnswerTime);
            } else {
                Vector3 offset = context.PhonePosition - context.PlayerPosition;
                offset.y = 0f;
                head.text = "1 Missed Call";
                head.color = UiPalette.Miss;
                quote.text = profile != null ? profile.missedLine : string.Empty;
                stat.SetText("Your phone was {0:1}m away", offset.magnitude);
            }

            quote.gameObject.SetActive(!string.IsNullOrEmpty(quote.text));
        }
    }
}
