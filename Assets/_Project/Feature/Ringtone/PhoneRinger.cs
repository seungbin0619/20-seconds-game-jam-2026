using MissedCall.Core;
using MissedCall.Core.Audio;
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
        [Tooltip("벨소리 목록. 발신자(CallerProfile.ringtone)가 번호로 고른다")]
        [SerializeField] private RingtonePattern[] patterns = new RingtonePattern[0];

        private int[] voices;
        private int voice = -1;
        private bool ringing;
        // 문턱처럼 어느 방에도 속하지 않는 곳에서는 마지막으로 있던 방을 쓴다. 벽 수가 튀지 않게 한다.
        private int lastPlayerRoom = -1;
        private bool wasCovered;
        private bool revealed;
        private int lastBeat = -1;

        public bool Ringing => ringing;

        /// <summary>마지막으로 넘긴 소리 경로. 확인용.</summary>
        public SoundRoute LastRoute { get; private set; }

        private void Start() {
            EnsureVoice();
        }

        // 다른 컴포넌트의 Start에서 판을 바로 시작해도 음원이 있게 한다.
        private void EnsureVoice() {
            if (voices != null) {
                return;
            }

            voices = new int[patterns.Length];
            for (int i = 0; i < patterns.Length; i++) {
                voices[i] = player.CreateVoice(patterns[i]);
            }

            voice = voices.Length > 0 ? voices[0] : -1;
        }

        // 이번 판 발신자의 벨소리
        private void PickVoice() {
            EnsureVoice();
            if (voices.Length == 0) {
                return;
            }

            int index = flow.Context.Caller != null ? flow.Context.Caller.ringtone : 0;
            voice = voices[Mathf.Clamp(index, 0, voices.Length - 1)];
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
                    revealed = false;
                    wasCovered = false;
                    lastBeat = -1;
                    Stop();
                    PickVoice();
                    if (!flow.Context.VibrationMode) {
                        Apply();
                        player.SnapVoice(voice);
                        player.StartVoice(voice);
                        ringing = true;
                    }

                    break;
                case RoundState.Answer:
                    SoundCues.Play(SoundCue.PhonePickup);
                    break;
                case RoundState.Won:
                    Stop();
                    SoundCues.Play(SoundCue.Win);
                    break;
                case RoundState.Missed:
                    Stop();
                    SoundCues.Play(SoundCue.Miss);
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

            if (flow.State == RoundState.Playing && !flow.IsPaused) {
                UpdateReveal();
                UpdateHeartbeat();
            }
        }

        // 덮고 있던 물건이 다 치워진 순간 한 번. 처음부터 드러나 있던 판에서는 울리지 않는다.
        private void UpdateReveal() {
            int covers = flow.Context.Covers;
            if (covers > 0) {
                wasCovered = true;
            } else if (wasCovered && !revealed) {
                revealed = true;
                SoundCues.PlayAt(SoundCue.PhoneReveal, flow.Context.PhonePosition);
            }
        }

        // 마지막 lowTimeWarning초에는 화면의 맥박(ScreenFx)과 같은 박자로 심장이 뛴다. 끝에 가까울수록 세다.
        private void UpdateHeartbeat() {
            RoundContext context = flow.Context;
            if (context.TimeLeft > tuning.lowTimeWarning) {
                return;
            }

            float interval = Mathf.Max(0.05f, tuning.jiggleInterval);
            int beat = Mathf.FloorToInt((context.Elapsed - tuning.jiggleDelay) / interval);
            if (beat == lastBeat) {
                return;
            }

            bool first = lastBeat < 0;
            lastBeat = beat;
            // 경계를 넘자마자 박자 중간에서 울리지 않는다.
            float phase = Mathf.Repeat((context.Elapsed - tuning.jiggleDelay) / interval, 1f);
            if (first && phase > 0.15f) {
                return;
            }

            float low = 1f - context.TimeLeft / Mathf.Max(0.01f, tuning.lowTimeWarning);
            SoundCues.Play(SoundCue.Heartbeat, low);
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
