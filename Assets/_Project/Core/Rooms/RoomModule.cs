using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 방 모듈 프리팹의 루트. 원점은 바닥 중심이고 x가 너비, z가 깊이다.
    /// 벽 두께는 <see cref="Size"/> 안쪽에 포함되어 이웃 모듈과 겹치지 않는다.
    /// </summary>
    public sealed class RoomModule : MonoBehaviour {
        [SerializeField] private RoomKind kind;
        [SerializeField] private RoomSize sizeClass;
        [Tooltip("모듈 바깥 치수(m). x = 너비, y = 깊이")]
        [SerializeField] private Vector2 size;
        [SerializeField] private DoorSocket[] sockets = new DoorSocket[0];
        [SerializeField] private HideSpot[] hideSpots = new HideSpot[0];
        [Tooltip("이 방의 메인 조명. 플레이어가 이 방에 있을 때만 실시간 그림자를 켠다")]
        [SerializeField] private Light mainLight;

        public RoomKind Kind => kind;
        public RoomSize SizeClass => sizeClass;
        public Vector2 Size => size;
        public DoorSocket[] Sockets => sockets;
        public HideSpot[] HideSpots => hideSpots;
        public Light MainLight => mainLight;

        /// <summary>모든 문 소켓을 막는다. 풀에서 꺼낼 때 초기 상태로 되돌린다.</summary>
        public void CloseAllSockets() {
            foreach (DoorSocket socket in sockets) {
                socket.SetOpen(false);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(RoomKind roomKind, RoomSize roomSize, Vector2 outerSize,
            DoorSocket[] doorSockets, HideSpot[] spots, Light light) {
            kind = roomKind;
            sizeClass = roomSize;
            size = outerSize;
            sockets = doorSockets;
            hideSpots = spots;
            mainLight = light;
        }

        private void OnDrawGizmos() {
            Gizmos.color = new Color(0.6f, 1f, 0.6f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 1.3f, 0f), new Vector3(size.x, 2.6f, size.y));
        }
#endif
    }
}
