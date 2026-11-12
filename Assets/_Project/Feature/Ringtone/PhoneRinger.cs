using MissedCall.Core;
using MissedCall.Core.Rooms;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>
    /// 판 흐름에 맞춰 폰 벨을 울린다. 매 프레임 <see cref="RoundContext"/>의 플레이어와 폰 위치, 덮임 수를 읽어
    /// <see cref="RoomGraph.Route"/>로 소리 경로를 구하고 <see cref="RingtonePlayer"/>에 넘긴다.
    /// 받기 화면에서는 먹먹함 없이 울리고, 판이 끝나면 멈춘다. 진동 모드 판에서는 벨을 울리지 않는다.
    /// </summary>
    // RingtonePlayer.LateUpdate가 값을 백엔드로 보내기 전에 갱신한다.
    [DefaultExecutionOrder(-50)]
    public sealed class PhoneRinger : MonoBehaviour {
        [SerializeField] private GameFlow flow;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private RingtonePlayer player;
        [Tooltip("벨소리. 발신자별 벨소리는 4단계에서 판마다 고른다")]
        [SerializeField] private RingtonePattern pattern;

        private int voice = -1;
        private bool ringing;
        // 문턱처럼 어느 방에도 속하지 않는 곳에서는 마지막으로 있던 방을 쓴다. 벽 수가 튀지 않게 한다.
        private int lastPlayerRoom = -1;

        public bool Ringing => ringing;

        /// <summary>마지막으로 넘긴 소리 경로. 확인용.</summary>
        public SoundRoute LastRoute { get; private set; }

        private void Start() {
            EnsureVoice();
        }

        // 다른 컴포넌트의 Start에서 판을 바로 시작해도 음원이 있게 한다.
        private void EnsureVoice() {
            if (voice < 0) {
                voice = player.CreateVoice(pattern);
            }
        }

        private void OnEnable() {
            flow.StateChanged += OnStateChanged;
            flow.PausedChanged += OnPausedChanged;
        }

        private void OnDisable() {
            flow.StateChanged -= OnStateChanged;
            flow.PausedChanged -= OnPausedChanged;
            Stop();
        }

        private void OnPausedChanged(bool paused) {
            player.SetPaused(paused);
        }

        private void OnStateChanged(RoundState previous, RoundState next) {
            switch (next) {
                case RoundState.Playing:
                    lastPlayerRoom = -1;
                    EnsureVoice();
                    Stop();
                    if (!flow.Context.VibrationMode) {
                        Apply();
                        player.SnapVoice(voice);
                        player.StartVoice(voice);
                        ringing = true;
                    }

                    break;
                case RoundState.Answer:
                    player.Play(Sfx.Grab);
                    break;
                case RoundState.Won:
                    Stop();
                    player.Play(Sfx.Win);
                    break;
                case RoundState.Missed:
                    Stop();
                    player.Play(Sfx.Miss);
                    break;
                default:
                    Stop();
                    break;
            }
        }

        private void LateUpdate() {
            if (ringing) {
                Apply();
            }
        }

        private void Apply() {
            RoundContext context = flow.Context;
            if (context.PlayerRoom >= 0) {
                lastPlayerRoom = context.PlayerRoom;
            }

            SoundRoute route = Route(context);
            LastRoute = route;
            float elapsed01 = context.Duration > 0f ? context.Elapsed / context.Duration : 0f;
            player.SetVoice(voice, route, context.Covers, elapsed01, context.State == RoundState.Answer);
        }

        private SoundRoute Route(RoundContext context) {
            RoomGraph graph = context.Graph;
            if (graph == null) {
                return SoundPath.Direct(context.PhonePosition);
            }

            Vector3 listener = context.PlayerPosition;
            GraphRoute path = graph.Route(listener, lastPlayerRoom, context.PhonePosition, context.PhoneRoom);
            if (path.SameRoom) {
                return SoundPath.Direct(context.PhonePosition);
            }

            return SoundPath.ThroughDoor(listener, path.FirstDoor, path.AfterDoor, path.PathFromDoor, path.Walls,
                tuning.doorBlendRadius);
        }

        private void Stop() {
            if (ringing && voice >= 0) {
                player.StopVoice(voice);
            }

            ringing = false;
        }
    }
}
