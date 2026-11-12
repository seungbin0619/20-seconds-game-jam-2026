using UnityEngine;

namespace MissedCall.Core.Callers {
    /// <summary>판마다 고를 수 있는 발신자 목록. Ui(결과 문구)와 Ringtone(벨소리)이 함께 읽으므로 Core에 둔다.</summary>
    [CreateAssetMenu(menuName = "Missed Call/Caller Catalog", fileName = "CallerCatalog_Default")]
    public sealed class CallerCatalog : ScriptableObject {
        public CallerProfile[] callers = new CallerProfile[0];
    }
}
