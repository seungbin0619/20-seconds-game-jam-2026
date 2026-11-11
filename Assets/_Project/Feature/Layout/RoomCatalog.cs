using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Layout {
    /// <summary>집 생성에 쓰는 템플릿과 방 모듈 프리팹 목록.</summary>
    [CreateAssetMenu(menuName = "Missed Call/Room Catalog", fileName = "RoomCatalog_Default")]
    public sealed class RoomCatalog : ScriptableObject {
        public LayoutTemplate[] templates = new LayoutTemplate[0];
        public RoomModule[] modules = new RoomModule[0];
    }
}
