using UnityEngine;

namespace MissedCall.Core {
    /// <summary>
    /// 플레이 감각을 맞출 때 만지는 값. 필드 이름과 기본값은 docs/design/game-design.md의 튜닝 표와 일치시킨다.
    /// </summary>
    [CreateAssetMenu(menuName = "Missed Call/Game Tuning", fileName = "GameTuning_Default")]
    public sealed class GameTuning : ScriptableObject {
        [Header("Round")]
        public float duration = 20f;
        public Vector2Int roomCount = new(2, 4);
        [Range(0f, 1f)] public float modifierChance = 0.5f;

        [Header("Player")]
        public float walkSpeed = 3.2f;
        public float lookSpeed = 0.0023f;
        public float reach = 3.1f;

        [Header("Walk Feel")]
        [Tooltip("뒤로 걸을 때 속도 배율")]
        [Range(0.3f, 1f)] public float backwardSpeedScale = 0.7f;
        [Tooltip("옆으로 걸을 때 속도 배율")]
        [Range(0.3f, 1f)] public float strafeSpeedScale = 0.85f;
        [Tooltip("걷기 시작할 때 가속도(m/s²). 클수록 바로 최고 속도에 닿는다")]
        public float walkAcceleration = 14f;
        [Tooltip("키를 뗐을 때 감속도(m/s²)")]
        public float walkDeceleration = 18f;
        [Tooltip("바닥에서 눈까지 높이(m)")]
        public float eyeHeight = 1.52f;
        [Tooltip("한 걸음 길이(m). 머리 흔들림과 발소리 간격을 정한다")]
        public float stepLength = 1f;
        [Tooltip("발이 닿을 때 머리가 내려가는 깊이(m)")]
        public float bobHeight = 0.035f;
        [Tooltip("걸음마다 머리가 좌우로 옮겨가는 폭(m)")]
        public float swayWidth = 0.02f;
        [Tooltip("걸음마다 머리가 좌우로 기우는 각도(도)")]
        public float swayRoll = 0.5f;
        [Tooltip("흔들림이 속도를 따라가는 빠르기(1/s). 멈출 때 흔들림이 가라앉는 속도")]
        public float bobResponse = 8f;

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
