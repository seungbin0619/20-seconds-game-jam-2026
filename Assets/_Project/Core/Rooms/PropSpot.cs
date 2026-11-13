using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 가구 위에 놓는 낱개 소품 자리 (침대 베개, 소파 쿠션, 책장의 책, 봉에 넌 수건). 판마다 물건 하나를 물리 물체로 놓는다.
    /// 숨는 자리가 아니므로 폰은 들어가지 않는다. 이 오브젝트의 위치는 소품이 놓이는 표면의 중심(널린 천은 봉 위의 점)이고,
    /// 회전은 소품이 놓이는 방향이다. 세운 책은 로컬 x가 책등 두께 방향, 널린 천은 로컬 x가 봉 방향이다.
    /// </summary>
    public sealed class PropSpot : MonoBehaviour {
        public enum Pose {
            /// <summary>표면 위에 눕힌다.</summary>
            Lying,
            /// <summary>책처럼 세운다 (가장 긴 변이 위로). 처음엔 잠들어 있다가 건드리면 움직인다.</summary>
            Standing,
            /// <summary>봉에 반으로 접어 넌다 (수건). 잡기 전까지 움직이지 않는다.</summary>
            Hanging,
        }

        [SerializeField] private ItemKind kind = ItemKind.Cushion;
        [SerializeField] private Pose pose = Pose.Lying;
        [Tooltip("놓는 방향을 무작위로 흔드는 각도(도)")]
        [SerializeField] private float spin = 12f;

        public ItemKind Kind => kind;
        public Pose Placement => pose;
        public float Spin => spin;

#if UNITY_EDITOR
        public void EditorSetup(ItemKind itemKind, float spinDegrees, Pose itemPose = Pose.Lying) {
            kind = itemKind;
            spin = spinDegrees;
            pose = itemPose;
        }

        private void OnDrawGizmos() {
            Gizmos.color = new Color(0.5f, 0.8f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.08f, new Vector3(0.3f, 0.16f, 0.3f));
        }
#endif
    }
}
