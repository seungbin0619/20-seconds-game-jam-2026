using System;
using UnityEngine;

namespace MissedCall.Core.Callers {
    /// <summary>
    /// 발신자 한 명. 문구는 game-design.md의 발신자 표를 따른다.
    /// 발신자별 벨소리는 Ringtone 단계에서 필드를 추가한다.
    /// </summary>
    [Serializable]
    public sealed class CallerProfile {
        public string displayName;
        [Tooltip("받았을 때 결과 화면에 나오는 말")]
        public string answeredLine;
        [Tooltip("놓쳤을 때 결과 화면에 나오는 말")]
        public string missedLine;
    }
}
