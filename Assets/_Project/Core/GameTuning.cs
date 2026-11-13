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

        [Header("Clutter")]
        [Tooltip("방마다 바닥에 흩뿌리는 물건 수 범위 (양 끝 포함)")]
        public Vector2Int scatterPerRoom = new(2, 3);
        [Tooltip("판 시작 전 물건 더미를 가라앉히는 물리 스텝 수 (1/60초 단위)")]
        public int settleSteps = 90;
        public float itemLinearDamping = 0.22f;
        public float itemAngularDamping = 0.45f;
        [Tooltip("이 운동 에너지(질량당) 아래로 내려가면 물건이 잠든다. 0.5 × 속도² 이므로 0.016 ≈ 0.18m/s")]
        public float itemSleepThreshold = 0.016f;

        [Header("Hold")]
        [Tooltip("잡은 물건을 두는 거리. 카메라 앞 이 거리(m)")]
        public float holdDistance = 0.9f;
        [Tooltip("잡는 점의 높이. 눈높이에서 위(+)/아래(-)로 이만큼(m)")]
        public float holdHeight = -0.25f;
        [Tooltip("잡는 점을 따라가는 빠르기(1/s). 떨어진 거리 × 이 값이 따라가는 속도다")]
        public float holdResponse = 16f;
        [Tooltip("끌려오고 따라가는 최대 속도(m/s)")]
        public float holdMaxSpeed = 10f;
        [Tooltip("잡힌 동안의 회전 감쇠. 클수록 손에서 빨리 멈춘다")]
        public float holdAngularDamping = 6f;
        [Tooltip("천 시뮬레이션: 스텝마다 남기는 속도 비율. 낮을수록 공기 저항이 커서 덜 펄럭인다")]
        [Range(0.8f, 1f)] public float clothDamping = 0.85f;
        [Tooltip("천 시뮬레이션: 거리 제약 반복 횟수. 많을수록 덜 늘어나고 빳빳하다")]
        public int clothIterations = 12;
        [Tooltip("천이 떨어진 뒤 아직 움직여도 이 시간(초)이 지나면 그 모양으로 굳힌다")]
        public float clothSettleTime = 2.5f;
        [Tooltip("쿠션을 잡았을 때 휘는 정도")]
        [Range(0f, 1f)] public float cushionHoldBend = 0.3f;
        [Tooltip("던져서 날아가는 동안 휘는 정도 (잡았을 때 값에 곱한다)")]
        [Range(0f, 1f)] public float airBendScale = 0.5f;
        [Tooltip("휘는 정도가 따라가는 스프링 세기. 낮을수록 늘어지고 출렁이는 게 느리다")]
        public float bendStiffness = 60f;
        public float bendDamping = 8f;
        [Tooltip("움직이는 반대쪽 가장자리가 더 늘어지는 정도 (펄럭임)")]
        public float bendFlutter = 0.5f;
        [Tooltip("잡는 점에 이만큼(m) 다가오면 손에 들어온 것으로 본다")]
        public float holdArriveDistance = 0.25f;
        [Tooltip("손에 들어온 뒤 잡는 점에서 이만큼(m) 멀어지면 (가구에 걸리면) 놓친다")]
        public float holdBreakDistance = 0.9f;
        [Tooltip("잡은 뒤 이 시간(초) 안에 손에 들어오지 않으면 놓친다")]
        public float holdPullTimeout = 0.6f;
        [Tooltip("물건 윗면 위로 이 높이(m) 안에 다른 물건이 있으면 깔린 것으로 본다")]
        public float buriedCheckHeight = 0.015f;
        [Tooltip("위 물건의 중심이 윗면 밖에 있을 때, 윗면(격자 3×3)의 이 비율 이상을 덮어야 깔린 것으로 본다")]
        [Range(0f, 1f)] public float buriedCoverage = 0.5f;
        [Tooltip("깔림 판정 상자를 윗면보다 이 비율만큼 좁힌다 (0.15면 가로세로 85%). 가장자리에 걸치기만 한 물건은 세지 않는다")]
        public float buriedCheckInset = 0.15f;
        [Tooltip("깔린 물건을 잡으면 손 쪽으로 이만큼(m) 끌려 나오다 만다")]
        public float tugDistance = 0.06f;
        [Tooltip("깔린 물건이 끌려 나오다 멈추기까지 시간(초)")]
        public float tugTime = 0.1f;
        [Tooltip("눈과 잡는 점 사이에 벽이나 가구가 있으면 잡는 점을 그 표면에서 이만큼(m) 앞으로 당긴다")]
        public float holdWallMargin = 0.2f;
        [Tooltip("바구니처럼 둘러싸인 자리의 물건은 먼저 이만큼(m) 수직으로 들어 올린 뒤 끌어온다")]
        public float enclosedLift = 0.6f;

        [Header("Throw")]
        [Tooltip("휘두르지 않고 놓아도 앞으로 툭 던지는 수평 속도. 휘두른 속도에 더해진다")]
        public float tapThrowForward = 2f;
        [Tooltip("놓을 때 기본으로 주는 위쪽 속도")]
        public float tapThrowUp = 1.8f;
        [Tooltip("휘두른 속도(잡는 점의 이동 속도)에 곱하는 배율")]
        public float flickScale = 0.6f;
        [Tooltip("휘두른 속도를 재는 구간(초). 놓기 직전 이 시간 동안의 평균이다")]
        public float flickWindow = 0.08f;
        [Tooltip("던지는 최대 속도(m/s)")]
        public float maxThrowSpeed = 9f;
        [Tooltip("놓은 뒤 다음 물건을 잡기까지 간격(초)")]
        public float throwCooldown = 0.1f;
        [Tooltip("던질 때 무작위 회전 속도 최대값 (rad/s, x y z 각각 ±)")]
        public Vector3 throwSpin = new(9f, 6f, 9f);

        [Header("Phone")]
        [Tooltip("화면 중심 레이와 폰 중심 사이 거리가 이 값(m) 이하이고 사이에 가림막이 없으면 폰을 조준한다")]
        public float phoneAimRadius = 0.17f;
        [Tooltip("숨는 자리 중심에서 폰을 옮기는 최대 거리(m). x = 자리의 좌우, y = 자리의 앞뒤")]
        public Vector2 phoneSpotJitter = new(0.06f, 0.05f);
        [Tooltip("폰을 숨는 자리 방향에서 돌리는 최대 각도(도)")]
        public float phoneYawJitter = 28.6f;
        [Tooltip("폰 중심보다 이만큼(m) 아래까지 중심이 있는 물건을 덮은 것으로 센다")]
        public float coverBelow = 0.03f;
        [Tooltip("폰 중심보다 이만큼(m) 위까지 중심이 있는 물건을 덮은 것으로 센다")]
        public float coverAbove = 1.1f;
        [Tooltip("물건 로컬 가로세로 범위를 이만큼(m) 넓혀 폰이 그 안에 들면 덮은 것으로 센다")]
        public float coverMargin = 0.05f;
        [Tooltip("덮인 물건이 없을 때 폰이 좌우로 떠는 폭(m)")]
        public float phoneShake = 0.004f;
        [Tooltip("폰이 떠는 빠르기(rad/s)")]
        public float phoneShakeRate = 90f;
        [Tooltip("울리는 동안 화면이 깜박이는 빠르기(rad/s)")]
        public float screenBlinkRate = 20f;
        [Tooltip("깜박일 때 어두운 쪽 밝기 비율")]
        [Range(0f, 1f)] public float screenBlinkDim = 0.35f;

        [Header("Vibration")]
        [Tooltip("진동 시 물건이 튀어 오르는 속도(m/s)")]
        public float jiggle = 0.55f;
        [Tooltip("벨이 울리는 판(진동 모드가 아닌 판)의 진동 세기 배율. 튀는 속도와 회전에 곱한다. 소리가 주 단서이므로 가까이서만 보이게 약하게 둔다")]
        [Range(0f, 1f)] public float ringJiggleScale = 0.4f;
        [Tooltip("판 시작 후 첫 진동까지 시간(초)")]
        public float jiggleDelay = 0.3f;
        [Tooltip("진동 간격(초). 기본 벨소리 한 주기(2초)의 절반")]
        public float jiggleInterval = 1f;
        [Tooltip("폰에서 이 수평 거리(m) 안의 물건을 튀게 한다")]
        public float jiggleRadius = 0.5f;
        [Tooltip("폰과 높이 차가 이 값(m) 안인 물건을 튀게 한다")]
        public float jiggleHeight = 0.6f;
        [Tooltip("진동 시 무작위 회전 속도 최대값 (rad/s, x y z 각각 ±)")]
        public Vector3 jiggleSpin = new(1f, 1.5f, 1f);

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

        [Header("Ui")]
        [Tooltip("판 시작 후 힌트 문구를 보여주는 시간(초)")]
        public float hintDuration = 4f;
        [Tooltip("남은 시간이 이 값(초) 이하면 타이머가 빨갛게 바뀐다")]
        public float lowTimeWarning = 5f;
        [Tooltip("결과가 나온 뒤 이 시간(초) 동안은 클릭으로 새 판을 시작하지 않는다. 마지막 클릭이 결과를 바로 넘기지 않게 한다")]
        public float resultInputDelay = 0.6f;

        [Header("Screen Fx")]
        [Tooltip("외곽선 두께 (1080p 기준 px). 화면 높이에 비례한다")]
        public float fxOutlineWidth = 2f;
        [Tooltip("이웃 깊이와의 차이가 가운데 깊이의 이 비율 이상이면 경계")]
        public float fxOutlineDepthThreshold = 0.1f;
        [Tooltip("이웃 노멀과의 차이(1 - 내적)가 이 값 이상이면 경계")]
        public float fxOutlineNormalThreshold = 0.4f;
        [Tooltip("이 거리(m)부터 외곽선을 흐리게 한다. 두 배 거리에서 사라진다")]
        public float fxOutlineFadeDistance = 12f;
        [Tooltip("밝기 단계 수. 0이면 끊지 않는다")]
        public int fxShadeBands = 3;
        [Tooltip("남은 시간이 이 값(초) 이하면 화면이 조여 오기 시작한다")]
        public float fxTenseStart = 10f;
        [Tooltip("0초일 때 비네트 세기")]
        [Range(0f, 1f)] public float fxVignetteMax = 0.45f;
        [Tooltip("0초일 때 채도를 빼는 비율")]
        [Range(0f, 1f)] public float fxDesaturateMax = 0.5f;
        [Tooltip("마지막 lowTimeWarning초 동안 붉은 맥박의 최대 세기. 박자는 진동(jiggleDelay, jiggleInterval)과 같다")]
        [Range(0f, 1f)] public float fxPulseStrength = 0.4f;
        [Tooltip("0초일 때 가장자리 색수차 (화면 높이 비율)")]
        public float fxAberrationMax = 0.004f;
        [Tooltip("마지막 lowTimeWarning초 동안 외곽선이 떨리는 폭 (1080p 기준 px)")]
        public float fxOutlineJitter = 1f;
        [Tooltip("0초일 때 외곽선 두께에 더하는 비율. 0.5면 1.5배")]
        public float fxOutlineThicken = 0.5f;
    }
}
