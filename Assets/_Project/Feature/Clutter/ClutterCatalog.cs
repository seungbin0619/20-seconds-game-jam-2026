using System;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Feature.Clutter {
    /// <summary>
    /// 물건 종류와 더미 구성. 프로토타입의 KINDS, buildPile, setupRound의 흩뿌림과 대응한다.
    /// 물리 감각 값(감쇠, 잠들기)은 <see cref="Core.GameTuning"/>에 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Missed Call/Clutter Catalog", fileName = "ClutterCatalog_Default")]
    public sealed class ClutterCatalog : ScriptableObject {
        [Serializable]
        public sealed class Kind {
            public ItemKind kind;
            [Tooltip("너비(x), 높이(y), 깊이(z) (m)")]
            public Vector3 size;
            public float mass = 0.5f;
            [Tooltip("천처럼 울퉁불퉁한 메시를 쓴다")]
            public bool soft;
            public Color[] colors = new Color[0];
        }

        public Kind[] kinds = new Kind[0];

        [Header("Piles")]
        [Tooltip("더미에서 맨 아래 물건 위에 쌓는 종류. 같은 종류를 여러 번 넣으면 더 자주 나온다")]
        public ItemKind[] pileMix = new ItemKind[0];
        [Tooltip("바구니처럼 둘러싸인 자리에서 맨 아래 물건 위에 쌓는 종류")]
        public ItemKind[] enclosedMix = new ItemKind[0];
        [Tooltip("위로 쌓는 물건의 수평 흔들림(m)")]
        public float pileJitter = 0.08f;
        [Tooltip("맨 아래 물건의 회전 흔들림(도)")]
        public float baseSpin = 7f;
        [Tooltip("위로 쌓는 물건의 회전 흔들림(도)")]
        public float pileSpin = 17f;
        [Tooltip("숨는 자리에 맨 아래 물건이 들어가지 않을 때(욕조가 담요보다 좁을 때 등) 대신 까는 종류")]
        public ItemKind fallbackBase = ItemKind.SmallShirt;

        [Header("Scatter")]
        [Tooltip("방 바닥에 흩뿌리는 종류")]
        public ItemKind[] scatterMix = new ItemKind[0];
        [Tooltip("방 바깥 치수에서 안쪽으로 이만큼 떨어진 곳에만 흩뿌린다(m). 벽 두께 포함")]
        public float scatterWallMargin = 0.45f;

        [Header("Look")]
        [Tooltip("물건 기준 머티리얼 (URP Simple Lit). 색마다 복제해 쓴다")]
        public Material baseMaterial;
        [Tooltip("부드러운 물건 메시 변형 수. 판마다 이 중에서 고른다")]
        public int softVariants = 4;

        public Kind Get(ItemKind kind) {
            foreach (Kind k in kinds) {
                if (k.kind == kind) {
                    return k;
                }
            }

            throw new ArgumentException($"ClutterCatalog has no entry for {kind}.");
        }
    }
}
