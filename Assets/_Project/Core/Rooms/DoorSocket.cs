using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 벽의 정해진 문 자리. 집을 조립할 때 연결 여부에 따라 열거나(빈 문틀) 막는다(벽 조각).
    /// 이 오브젝트의 위치는 벽 두께 가운데, 바닥 높이의 문 중심이다. forward는 방 바깥을 향한다.
    /// </summary>
    public sealed class DoorSocket : MonoBehaviour {
        [SerializeField] private WallSide side;
        [Tooltip("벽 가운데에서 문 중심까지의 거리(m). 방 안에서 벽을 볼 때 오른쪽이 +")]
        [SerializeField] private float offset;
        [SerializeField] private float width = 0.9f;
        [Tooltip("열렸을 때 켜는 오브젝트 (문틀 등). 없어도 된다")]
        [SerializeField] private GameObject openPiece;
        [Tooltip("막혔을 때 켜는 벽 조각. 라이트맵 굽기 대상에서 제외한다")]
        [SerializeField] private GameObject closedPiece;

        public WallSide Side => side;
        public float Offset => offset;
        public float Width => width;
        public bool IsOpen { get; private set; }

        public void SetOpen(bool open) {
            IsOpen = open;
            if (openPiece != null) {
                openPiece.SetActive(open);
            }

            if (closedPiece != null) {
                closedPiece.SetActive(!open);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(WallSide wallSide, float wallOffset, float doorWidth, GameObject open, GameObject closed) {
            side = wallSide;
            offset = wallOffset;
            width = doorWidth;
            openPiece = open;
            closedPiece = closed;
        }

        private void OnDrawGizmos() {
            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            Vector3 center = transform.position + Vector3.up * 1f;
            Gizmos.DrawWireCube(center, transform.rotation * new Vector3(width, 2f, 0.05f));
            Gizmos.DrawRay(center, transform.forward * 0.5f);
        }
#endif
    }
}
