using System;
using UnityEngine;

namespace MissedCall.Core.Round {
    /// <summary>
    /// 게임 상태 머신. 판 시작(준비 단계 실행), 타이머, 상태 전환을 맡는다.
    /// 입력은 읽지 않는다. Feature가 <see cref="StartRound"/>, <see cref="PickUp"/>, <see cref="Answer"/>를 호출한다.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour {
        [SerializeField] private GameTuning tuning;
        [Tooltip("0이 아니면 매 판 이 시드를 쓴다. 버그 재현용")]
        [SerializeField] private int fixedSeed;
        [Tooltip("판 준비 단계. 이 순서대로 실행한다 (Layout → Clutter → Phone → Player ...)")]
        [SerializeField] private RoundStep[] steps = new RoundStep[0];

        private readonly RoundContext context = new();
        // 판 시드를 고르는 데만 쓴다. 판 안의 무작위는 RoundSeed에서 뽑는다.
        private System.Random seedSource;
        private double startTime;

        private double pausedAt;

        /// <summary>상태가 바뀐 뒤 (이전 상태, 새 상태)로 호출된다. 새 판 시작은 Playing으로의 전환이다.</summary>
        public event Action<RoundState, RoundState> StateChanged;

        /// <summary>
        /// 일시정지가 바뀐 뒤 (멈췄는가)로 호출된다. 일시정지는 상태가 아니라 playing, answer 위에 얹히는 플래그다.
        /// 상태를 바꾸지 않으므로 Playing 전환을 새 판으로 보는 구독자가 재개를 새 판으로 착각하지 않는다.
        /// </summary>
        public event Action<bool> PausedChanged;

        public RoundContext Context => context;
        public RoundState State => context.State;
        public GameTuning Tuning => tuning;

        /// <summary>일시정지 메뉴가 열려 있는가. 이 동안 타이머가 멈추고 Time.timeScale은 0이다.</summary>
        public bool IsPaused { get; private set; }

        private void Awake() {
            context.State = RoundState.Title;
            seedSource = new System.Random(unchecked((int)DateTime.UtcNow.Ticks));
        }

        private void OnDestroy() {
            Time.timeScale = 1f;
        }

        /// <summary>새 시드로 판을 준비하고 시작한다. 타이틀의 시작 버튼, 결과 화면, R에서 호출한다.</summary>
        public void StartRound() => StartRound(NextSeed());

        /// <summary>정해진 시드로 판을 시작한다. 같은 시드는 같은 판을 만든다.</summary>
        public void StartRound(RoundSeed seed) {
            ClearPause();
            RoundState previous = context.State;
            context.Reset(seed, tuning.duration);
            foreach (RoundStep step in steps) {
                step.Prepare(context);
            }

            Debug.Log($"[GameFlow] Round start, seed {seed}");
            // 준비에 걸린 시간은 판 시간에서 빼지 않는다.
            startTime = Now;
            StateChanged?.Invoke(previous, RoundState.Playing);
        }

        /// <summary>R. 타이틀이 아니면 즉시 새 판을 시작한다.</summary>
        public void RequestRestart() {
            if (context.State != RoundState.Title) {
                StartRound();
            }
        }

        /// <summary>폰을 집었다. playing → answer.</summary>
        public void PickUp() {
            if (context.State == RoundState.Playing) {
                SetState(RoundState.Answer);
            }
        }

        /// <summary>받기 슬라이드를 끝까지 밀었다. answer → won.</summary>
        public void Answer() {
            if (context.State != RoundState.Answer) {
                return;
            }

            Tick();
            context.AnswerTime = context.Elapsed;
            SetState(RoundState.Won);
        }

        /// <summary>
        /// 판을 멈춘다 (playing, answer에서만). 타이머, 물리(Time.timeScale), 벨소리가 멈춘다.
        /// playing 중 포인터 잠금이 풀렸을 때(Esc, 탭 전환)와 받기 화면의 Esc에서 호출한다.
        /// </summary>
        public void Pause() {
            if (IsPaused || !IsRunning(context.State)) {
                return;
            }

            Tick();
            IsPaused = true;
            pausedAt = Now;
            Time.timeScale = 0f;
            PausedChanged?.Invoke(true);
        }

        /// <summary>일시정지를 푼다. 멈춰 있던 시간은 판 시간에서 뺀다.</summary>
        public void Resume() {
            if (!IsPaused) {
                return;
            }

            startTime += Now - pausedAt;
            ClearPause();
        }

        /// <summary>판을 버리고 타이틀로 돌아간다. 일시정지 메뉴의 Title에서 호출한다.</summary>
        public void ReturnToTitle() {
            ClearPause();
            SetState(RoundState.Title);
        }

        private void ClearPause() {
            if (!IsPaused) {
                return;
            }

            IsPaused = false;
            Time.timeScale = 1f;
            PausedChanged?.Invoke(false);
        }

        private void Update() {
            if (!IsRunning(context.State) || IsPaused) {
                return;
            }

            Tick();
            if (context.Elapsed >= context.Duration) {
                context.Elapsed = context.Duration;
                SetState(RoundState.Missed);
            }
        }

        private void Tick() {
            context.Elapsed = (float)(Now - startTime);
        }

        private void SetState(RoundState next) {
            RoundState previous = context.State;
            if (previous == next) {
                return;
            }

            context.State = next;
            StateChanged?.Invoke(previous, next);
        }

        private RoundSeed NextSeed() {
            int value = fixedSeed != 0 ? fixedSeed : seedSource.Next(1, int.MaxValue);
            return new RoundSeed(value);
        }

        /// <summary>타이머는 실제 시간으로 흐른다. timeScale과 프레임 드랍의 영향을 받지 않는다.</summary>
        private static double Now => Time.realtimeSinceStartupAsDouble;

        public static bool IsRunning(RoundState state) => state == RoundState.Playing || state == RoundState.Answer;
    }
}
