using System;
using UnityEngine;

namespace MissedCall.Core.Callers {
    /// <summary>
    /// 발신자 한 명. 문구는 game-design.md의 발신자 표를 따른다.
    /// 벨소리는 Ringtone의 벨소리 목록 번호로 고른다 (Core는 Feature 에셋을 모르므로 번호로 둔다).
    /// </summary>
    [Serializable]
    public sealed class CallerProfile {
        public string displayName;
        [Tooltip("받았을 때 결과 화면에 나오는 말")]
        public string answeredLine;
        [Tooltip("놓쳤을 때 결과 화면에 나오는 말")]
        public string missedLine;
        [Tooltip("PhoneRinger의 벨소리 목록에서 이 발신자가 쓰는 번호")]
        public int ringtone;
        [Tooltip("판마다 뽑힐 상대 확률. 희귀 발신자는 작게 둔다")]
        public float weight = 1f;
    }
}
