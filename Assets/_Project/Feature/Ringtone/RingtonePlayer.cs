using System.Collections.Generic;
using MissedCall.Core;
using MissedCall.Core.Audio;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>
    /// 벨소리 음원(voice)들을 관리한다. 게임 쪽은 소리 경로(<see cref="SoundPath"/>), 덮임 수, 경과 비율만 넘기고
    /// cutoff와 볼륨 계산, 음원 위치 보간, 백엔드 선택은 여기서 한다.
    /// </summary>
    public sealed class RingtonePlayer : MonoBehaviour {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private Transform listener;

        private const float ImpactSpacing = 0.03f;

        private readonly List<VoiceState> voices = new();
        private readonly Dictionary<SoundCue, int[]> sfx = new();
        // 효과음 변형과 음높이 흔들림용. 판 무작위와 상관없는 연출이다.
        private readonly System.Random sfxRandom = new(1234);
        private float nextImpactTime;
        private IRingtoneBackend backend;

        public bool IsReady => backend != null && backend.IsReady;
        public string Status => backend == null ? "not initialized" : backend.Status;
        public int VoiceCount => voices.Count;

        /// <summary>런타임에 튜닝을 바꿀 때 쓴다. 에셋을 직접 고치지 않도록 복사본을 넘긴다.</summary>
        public GameTuning Tuning {
            get => tuning;
            set => tuning = value;
        }

        // 보간은 듣는 사람 기준 상대 위치로 한다. 걸을 때 음원이 뒤따라 끌려오지 않고, 경로가 바뀔 때만 부드럽게 옮겨간다.
        private sealed class VoiceState {
            public int BackendId;
            public Vector3 TargetOffset;
            public Vector3 Offset;
            public bool Placed;
            public float Cutoff;
            public float Gain;
        }

        private void Awake() {
#if UNITY_WEBGL && !UNITY_EDITOR
            backend = new WebAudioBackend();
#else
            backend = new UnityAudioBackend(transform);
#endif
            backend.Init(tuning.rolloff);
            LoadSfx();
        }

        private void OnEnable() => SoundCues.Played += OnSound;
        private void OnDisable() => SoundCues.Played -= OnSound;

        private void OnDestroy() => backend?.Dispose();

        private void LoadSfx() {
            foreach (SoundCue cue in (SoundCue[])System.Enum.GetValues(typeof(SoundCue))) {
                float[][] variants = SfxSynth.Render(cue);
                var ids = new int[variants.Length];
                for (int i = 0; i < variants.Length; i++) {
                    ids[i] = backend.CreateSample(variants[i], SfxSynth.SampleRate);
                }

                sfx[cue] = ids;
            }
        }

        // 효과음마다 (가장 작을 때, 가장 클 때) 볼륨과 음높이 흔들림. intensity로 볼륨 사이를 고른다.
        private static (float min, float max, float jitter) SfxLevel(SoundCue cue) {
            switch (cue) {
                case SoundCue.Grab: return (0.32f, 0.32f, 0.08f);
                case SoundCue.Throw: return (0.18f, 0.55f, 0.06f);
                case SoundCue.Tug: return (0.38f, 0.38f, 0.06f);
                case SoundCue.ImpactSoft: return (0.12f, 0.6f, 0.1f);
                case SoundCue.ImpactHard: return (0.1f, 0.55f, 0.1f);
                case SoundCue.ImpactHollow: return (0.12f, 0.6f, 0.08f);
                case SoundCue.Step: return (0.1f, 0.1f, 0.08f);
                case SoundCue.Heartbeat: return (0.6f, 1f, 0f);
                case SoundCue.UiClick: return (0.35f, 0.35f, 0.04f);
                case SoundCue.SlideGrab: return (0.45f, 0.45f, 0.03f);
                case SoundCue.SlideBack: return (0.35f, 0.35f, 0.03f);
                default: return (0.7f, 0.7f, 0f);
            }
        }

        private static bool IsImpact(SoundCue cue) =>
            cue == SoundCue.ImpactSoft || cue == SoundCue.ImpactHard || cue == SoundCue.ImpactHollow;

        private void OnSound(SoundEvent sound) {
            if (!sfx.TryGetValue(sound.Cue, out int[] ids) || ids.Length == 0) {
                return;
            }

            // 더미가 무너질 때 충돌음이 한꺼번에 쏟아지지 않게 간격을 둔다.
            if (IsImpact(sound.Cue)) {
                float now = Time.unscaledTime;
                if (now < nextImpactTime) {
                    return;
                }

                nextImpactTime = now + ImpactSpacing;
            }

            (float min, float max, float jitter) = SfxLevel(sound.Cue);
            float gain = Mathf.Lerp(min, max, sound.Intensity);
            float rate = 1f + ((float)sfxRandom.NextDouble() * 2f - 1f) * jitter;
            if (sound.Cue == SoundCue.Throw) {
                rate *= Mathf.Lerp(0.85f, 1.2f, sound.Intensity);
            }

            int id = ids[sfxRandom.Next(ids.Length)];
            backend.PlaySample(id, gain, rate, sound.Spatial, sound.Position);
        }

        private Vector3 ListenerPosition => listener != null ? listener.position : Vector3.zero;

        /// <returns>이 플레이어 안에서 쓰는 음원 번호</returns>
        public int CreateVoice(RingtonePattern pattern) {
            voices.Add(new VoiceState {
                BackendId = backend.CreateVoice(pattern),
                Cutoff = tuning.clearCutoff,
                Gain = RingtoneMath.Gain(tuning, 0f)
            });
            return voices.Count - 1;
        }

        public void StartVoice(int voice) => backend.StartVoice(voices[voice].BackendId);
        public void StopVoice(int voice) => backend.StopVoice(voices[voice].BackendId);

        /// <summary>일시정지 메뉴가 열리고 닫힐 때. 벨과 효과음을 모두 멈춘다.</summary>
        public void SetPaused(bool paused) => backend.SetPaused(paused);

        /// <param name="route">같은 방이면 <see cref="SoundPath.Direct"/>, 다른 방이면 <see cref="SoundPath.ThroughDoor"/></param>
        /// <param name="clear">받기 화면처럼 먹먹함 없이 들려야 할 때</param>
        public void SetVoice(int voice, SoundRoute route, int covers, float elapsed01, bool clear = false) {
            VoiceState state = voices[voice];
            state.TargetOffset = route.Position - ListenerPosition;
            if (!state.Placed) {
                state.Offset = state.TargetOffset;
                state.Placed = true;
            }

            state.Cutoff = clear ? tuning.clearCutoff : RingtoneMath.Cutoff(tuning, covers, route.Walls);
            state.Gain = RingtoneMath.Gain(tuning, elapsed01, clear ? 0f : route.Walls);
        }

        /// <summary>보간 없이 음원을 목표 위치로 옮긴다. 새 판을 시작할 때 쓴다.</summary>
        public void SnapVoice(int voice) => voices[voice].Offset = voices[voice].TargetOffset;

        public float GetCutoff(int voice) => voices[voice].Cutoff;
        public Vector3 GetEmitPosition(int voice) => ListenerPosition + voices[voice].Offset;

        public void Play(OneShot[] shots) {
            foreach (OneShot shot in shots) {
                backend.PlayOneShot(shot);
            }
        }

        private void LateUpdate() {
            Vector3 origin = ListenerPosition;
            if (listener != null) {
                backend.SetListener(origin, listener.forward);
            }

            float follow = 1f - Mathf.Exp(-tuning.emitterFollowSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < voices.Count; i++) {
                VoiceState state = voices[i];
                state.Offset = Vector3.Lerp(state.Offset, state.TargetOffset, follow);
                backend.SetVoice(state.BackendId, origin + state.Offset, state.Cutoff, state.Gain);
            }
        }
    }
}
