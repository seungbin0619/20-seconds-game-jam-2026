using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 물건 더미를 쌓는 자리. 판마다 이 중 한 곳의 맨 아래에 폰을 둔다.
    /// 이 오브젝트의 위치는 더미가 놓이는 표면의 중심이다.
    /// </summary>
    public sealed class HideSpot : MonoBehaviour {
        [SerializeField] private ItemKind baseKind = ItemKind.Shirt;
        [Tooltip("더미에 쌓는 물건 수 범위 (맨 아래 물건 포함, 양 끝 포함)")]
        [SerializeField] private Vector2Int pileCount = new(2, 4);
        [Tooltip("바구니나 욕조처럼 둘레에 벽이 있는 자리인지")]
        [SerializeField] private bool enclosed;
        [Tooltip("물건 더미 없이 폰만 두는 자리 (서랍 안)")]
        [SerializeField] private bool bare;
        [Tooltip("움직이는 가구(서랍)에 붙은 자리. 폰이 이 자리를 따라간다")]
        [SerializeField] private bool moves;

        public ItemKind BaseKind => baseKind;
        public Vector2Int PileCount => pileCount;
        public bool Enclosed => enclosed;
        public bool Bare => bare;
        public bool Moves => moves;

        /// <summary>
        /// 물건 말고도 폰을 덮어 소리를 먹먹하게 하는 것의 수 (닫힌 서랍). 서랍이 판 동안 바꾸고 Phone이 덮임 수에 더한다.
        /// </summary>
        public int ExtraCovers { get; set; }

#if UNITY_EDITOR
        public void EditorSetup(ItemKind kind, Vector2Int count, bool isEnclosed) {
            baseKind = kind;
            pileCount = count;
            enclosed = isEnclosed;
        }

        public void EditorSetupBare(bool isMoving) {
            bare = true;
            moves = isMoving;
            pileCount = Vector2Int.zero;
        }

        private void OnDrawGizmos() {
            Gizmos.color = enclosed ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.12f);
        }
#endif
    }
}
