using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 물건 더미를 쌓는 자리. 판마다 이 중 한 곳의 맨 아래에 폰을 둔다.
    /// 이 오브젝트의 위치는 더미가 놓이는 표면의 중심이다.
    /// </summary>
    public sealed class HideSpot : MonoBehaviour {
        [SerializeField] private ItemKind baseKind = ItemKind.Shirt;
        [Tooltip("더미에 쌓는 물건 수 범위 (맨 아래 물건 포함, 양 끝 포함)")]
        [SerializeField] private Vector2Int pileCount = new Vector2Int(2, 4);
        [Tooltip("바구니나 욕조처럼 둘레에 벽이 있는 자리인지")]
        [SerializeField] private bool enclosed;

        public ItemKind BaseKind => baseKind;
        public Vector2Int PileCount => pileCount;
        public bool Enclosed => enclosed;

#if UNITY_EDITOR
        public void EditorSetup(ItemKind kind, Vector2Int count, bool isEnclosed) {
            baseKind = kind;
            pileCount = count;
            enclosed = isEnclosed;
        }

        private void OnDrawGizmos() {
            Gizmos.color = enclosed ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.12f);
        }
#endif
    }
}
