using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 가구 위에 놓는 낱개 소품 자리 (침대 베개, 소파 쿠션). 판마다 물건 하나를 물리 물체로 놓는다.
    /// 숨는 자리가 아니므로 폰은 들어가지 않는다. 이 오브젝트의 위치는 소품이 놓이는 표면의 중심이고,
    /// 회전은 소품이 놓이는 방향이다.
    /// </summary>
    public sealed class PropSpot : MonoBehaviour {
        [SerializeField] private ItemKind kind = ItemKind.Cushion;
        [Tooltip("놓는 방향을 무작위로 흔드는 각도(도)")]
        [SerializeField] private float spin = 12f;

        public ItemKind Kind => kind;
        public float Spin => spin;

#if UNITY_EDITOR
        public void EditorSetup(ItemKind itemKind, float spinDegrees) {
            kind = itemKind;
            spin = spinDegrees;
        }

        private void OnDrawGizmos() {
            Gizmos.color = new Color(0.5f, 0.8f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.08f, new Vector3(0.3f, 0.16f, 0.3f));
        }
#endif
    }
}
