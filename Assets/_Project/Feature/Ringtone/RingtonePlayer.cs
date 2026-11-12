using System.Collections.Generic;
using MissedCall.Core;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>
    /// 벨소리 음원(voice)들을 관리한다. 게임 쪽은 소리 경로(<see cref="SoundPath"/>), 덮임 수, 경과 비율만 넘기고
    /// cutoff와 볼륨 계산, 음원 위치 보간, 백엔드 선택은 여기서 한다.
    /// </summary>
    public sealed class RingtonePlayer : MonoBehaviour {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private Transform listener;

        private readonly List<VoiceState> voices = new();
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
        }

        private void OnDestroy() => backend?.Dispose();

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
