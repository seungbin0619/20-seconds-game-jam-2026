using UnityEngine;

namespace MissedCall.Core {
    /// <summary>
    /// 플레이 감각을 맞출 때 만지는 값. 필드 이름과 기본값은 docs/design/game-design.md의 튜닝 표와 일치시킨다.
    /// </summary>
    [CreateAssetMenu(menuName = "Missed Call/Game Tuning", fileName = "GameTuning_Default")]
    public sealed class GameTuning : ScriptableObject {
        [Header("Round")]
        public float duration = 20f;
        public Vector2Int roomCount = new Vector2Int(2, 4);
        [Range(0f, 1f)] public float modifierChance = 0.5f;

        [Header("Player")]
        public float walkSpeed = 3.2f;
        public float lookSpeed = 0.0023f;
        public float reach = 3.1f;

        [Header("Throw")]
        public float throwForward = 4.6f;
        public float throwUp = 3.6f;
        public float throwCooldown = 0.1f;
        public float jiggle = 0.55f;

        [Header("Sound")]
        public float clearCutoff = 7000f;
        public float coveredCutoff = 420f;
        public int maxCovers = 4;
        [Tooltip("벽 한 겹당 고음 한계 배율")]
        [Range(0.05f, 1f)] public float wallCutoffScale = 0.5f;
        [Tooltip("벽 한 겹당 볼륨 배율")]
        [Range(0.1f, 1f)] public float wallGainScale = 0.8f;
        [Tooltip("덮임과 벽이 겹쳐도 이 아래로 고음 한계를 내리지 않는다(Hz)")]
        public float minCutoff = 200f;
        public float rolloff = 2.2f;
        [Tooltip("문에서 이 거리(m) 안으로 들어오면 소리 방향과 벽 가림이 서서히 바뀐다")]
        public float doorBlendRadius = 1.5f;
        [Tooltip("음원 경로가 바뀔 때 따라가는 속도(1/s)")]
        public float emitterFollowSpeed = 6f;
        [Tooltip("판 시작 시 벨 볼륨")]
        public float ringGainStart = 0.5f;
        [Tooltip("시간이 다 됐을 때 벨 볼륨")]
        public float ringGainEnd = 1.2f;

        [Header("Answer")]
        public float slideNeeded = 210f;
    }
}
