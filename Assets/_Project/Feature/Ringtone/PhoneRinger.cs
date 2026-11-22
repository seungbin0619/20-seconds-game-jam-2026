using MissedCall.Core;
using MissedCall.Core.Audio;
using MissedCall.Core.Rooms;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>
    /// 판 흐름에 맞춰 폰 벨을 울린다. 매 프레임 <see cref="RoundContext"/>의 플레이어와 폰 위치, 덮임 수를 읽어
    /// <see cref="RoomGraph.Route"/>로 소리 경로를 구하고 <see cref="RingtonePlayer"/>에 넘긴다.
    /// 받기 화면에서는 먹먹함 없이 울리고, 판이 끝나면 멈춘다.
    /// 판 변수: 진동 모드는 벨 대신 진동음, 배터리 부족은 정한 시각에 벨이 끊기고, 미끼 소리는 다른 자리에서 알람이,
    /// 소음은 한 방 천장에서 환풍기 소리가 난다. 미끼와 소음도 벨처럼 문 쪽 경로와 벽 먹먹함을 따른다.
    /// </summary>
    // RingtonePlayer.LateUpdate가 값을 백엔드로 보내기 전에 갱신한다.
    [DefaultExecutionOrder(-50)]
    public sealed class PhoneRinger : MonoBehaviour {
        [SerializeField] private GameFlow flow;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private RingtonePlayer player;
        [Tooltip("벨소리 목록. 발신자(CallerProfile.ringtone)가 번호로 고른다")]
        [SerializeField] private RingtonePattern[] patterns = new RingtonePattern[0];
        [Tooltip("진동 모드 판의 진동음 (벨 없이 진동만)")]
        [SerializeField] private RingtonePattern vibration;
        [Tooltip("미끼 소리 판의 가짜 소리")]
        [SerializeField] private RingtonePattern decoy;

        private int vibrationVoice = -1;
        private int decoyVoice = -1;
        private int noiseVoice = -1;
        private int vacuumVoice = -1;
        private bool decoyOn;
        private bool noiseOn;
        private bool vacuumOn;

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
            vibrationVoice = vibration != null ? player.CreateVoice(vibration) : -1;
            decoyVoice = decoy != null ? player.CreateVoice(decoy) : -1;
            noiseVoice = player.CreateLoopVoice(SfxSynth.FanLoop(), SfxSynth.SampleRate);
            vacuumVoice = player.CreateLoopVoice(SfxSynth.VacuumLoop(), SfxSynth.SampleRate);
        }

        // 이번 판 발신자의 벨소리
        private void PickVoice() {
            EnsureVoice();
            if (voices.Length == 0) {
                return;
            }

            int index = flow.Context.Caller != null ? flow.Context.Caller.ringtone : 0;
            voice = voices[Mathf.Clamp(index, 0, voices.Length - 1)];
            if (flow.Context.Modifier == RoundModifier.Vibrate && vibrationVoice >= 0) {
                voice = vibrationVoice;
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
                    revealed = false;
                    wasCovered = false;
                    lastBeat = -1;
                    Stop();
                    StopExtras();
                    PickVoice();
                    if (voice >= 0) {
                        Apply();
                        player.SnapVoice(voice);
                        player.StartVoice(voice);
                        ringing = true;
                    }

                    StartExtras();

                    break;
                case RoundState.Answer:
                    StopExtras();
                    SoundCues.Play(SoundCue.PhonePickup);
                    break;
                case RoundState.Won:
                    Stop();
                    StopExtras();
                    SoundCues.Play(SoundCue.Win);
                    break;
                case RoundState.Missed:
                    Stop();
                    StopExtras();
                    SoundCues.Play(SoundCue.Miss);
                    break;
                default:
                    Stop();
                    StopExtras();
                    break;
            }
        }

        private void LateUpdate() {
            RoundContext context = flow.Context;
            // 배터리 부족: 정한 시각에 벨이 끊기고 폰이 꺼지는 소리가 난다. 받기 화면에서는 끊지 않는다.
            if (ringing && context.State == RoundState.Playing && context.BatteryDead) {
                Stop();
                SoundCues.PlayAt(SoundCue.BatteryDead, context.PhonePosition);
            }

            if (ringing) {
                Apply();
            }

            ApplyExtras();

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

            SoundRoute route = Route(context, context.PhonePosition, context.PhoneRoom);
            LastRoute = route;
            float elapsed01 = context.Duration > 0f ? context.Elapsed / context.Duration : 0f;
            player.SetVoice(voice, route, context.Covers, elapsed01, context.State == RoundState.Answer);
        }

        private void StartExtras() {
            RoundContext context = flow.Context;
            if (context.Modifier == RoundModifier.Decoy && decoyVoice >= 0) {
                decoyOn = true;
                ApplyExtras();
                player.SnapVoice(decoyVoice);
                player.StartVoice(decoyVoice);
            }

            if (context.Modifier == RoundModifier.Noise && noiseVoice >= 0) {
                noiseOn = true;
                ApplyExtras();
                player.SnapVoice(noiseVoice);
                player.StartVoice(noiseVoice);
            }

            if (context.Modifier == RoundModifier.Vacuum && vacuumVoice >= 0) {
                vacuumOn = true;
                ApplyExtras();
                player.SnapVoice(vacuumVoice);
                player.StartVoice(vacuumVoice);
            }
        }

        private void ApplyExtras() {
            RoundContext context = flow.Context;
            float elapsed01 = context.Duration > 0f ? context.Elapsed / context.Duration : 0f;
            if (decoyOn) {
                SoundRoute route = Route(context, context.DecoyPosition, context.DecoyRoom);
                player.SetVoice(decoyVoice, route, tuning.decoyCovers, elapsed01);
            }

            if (noiseOn) {
                SoundRoute route = Route(context, context.NoisePosition, context.NoiseRoom);
                player.SetVoice(noiseVoice, route, 0, elapsed01);
                player.SetVoiceGain(noiseVoice, tuning.noiseGain * Mathf.Pow(tuning.wallGainScale, route.Walls));
            }

            if (vacuumOn) {
                SoundRoute route = Route(context, context.VacuumPosition, context.VacuumRoom);
                player.SetVoice(vacuumVoice, route, 0, elapsed01);
                player.SetVoiceGain(vacuumVoice, tuning.vacuumGain * Mathf.Pow(tuning.wallGainScale, route.Walls));
            }
        }

        private void StopExtras() {
            if (decoyOn) {
                player.StopVoice(decoyVoice);
            }

            if (noiseOn) {
                player.StopVoice(noiseVoice);
            }

            if (vacuumOn) {
                player.StopVoice(vacuumVoice);
            }

            decoyOn = false;
            noiseOn = false;
            vacuumOn = false;
        }

        private SoundRoute Route(RoundContext context, Vector3 target, int targetRoom) {
            RoomGraph graph = context.Graph;
            if (graph == null || targetRoom < 0) {
                return SoundPath.Direct(target);
            }

            Vector3 listener = context.PlayerPosition;
            GraphRoute path = graph.Route(listener, lastPlayerRoom, target, targetRoom);
            if (path.SameRoom) {
                return SoundPath.Direct(target);
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
