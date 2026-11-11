#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System.Collections.Generic;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>
    /// 에디터용 근사 구현. 벨 한 주기를 클립으로 미리 합성해 3D AudioSource + AudioLowPassFilter로 재생한다.
    /// HRTF가 아니라 Unity 기본 패닝이므로 방향감은 Web 빌드와 다르다. 최종 판정은 Web 빌드에서 한다.
    /// </summary>
    internal sealed class UnityAudioBackend : IRingtoneBackend {
        private const float RefDistance = 0.8f;
        private const float MaxDistance = 20f;
        private const float MasterGain = 0.85f;

        private readonly Transform host;
        private readonly List<Voice> voices = new();
        private readonly Dictionary<OneShot, AudioClip> oneShotClips = new();
        private AudioSource oneShotSource;
        private float rolloff;

        private sealed class Voice {
            public AudioSource Source;
            public AudioLowPassFilter Filter;
        }

        public UnityAudioBackend(Transform host) {
            this.host = host;
        }

        public bool IsReady => true;
        public string Status => "Unity audio (editor approximation)";

        public void Init(float rolloffFactor) {
            rolloff = rolloffFactor;
            var go = new GameObject("OneShots");
            go.transform.SetParent(host, false);
            oneShotSource = go.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.spatialBlend = 0f;
        }

        public int CreateVoice(RingtonePattern pattern) {
            var go = new GameObject("Voice " + pattern.name);
            go.transform.SetParent(host, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = RingtoneSynth.RenderCycle(pattern, AudioSettings.outputSampleRate);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.maxDistance = MaxDistance;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, InverseRolloff());

            var filter = go.AddComponent<AudioLowPassFilter>();
            filter.lowpassResonanceQ = 0.7f;

            voices.Add(new Voice { Source = source, Filter = filter });
            return voices.Count - 1;
        }

        public void StartVoice(int voice) => voices[voice].Source.Play();
        public void StopVoice(int voice) => voices[voice].Source.Stop();

        public void SetVoice(int voice, Vector3 position, float cutoff, float gain) {
            Voice v = voices[voice];
            v.Source.transform.position = position;
            v.Filter.cutoffFrequency = cutoff;
            v.Source.volume = Mathf.Clamp01(gain * MasterGain);
        }

        // 씬의 AudioListener를 그대로 쓴다.
        public void SetListener(Vector3 position, Vector3 forward) {
        }

        public void PlayOneShot(in OneShot shot) {
            if (!oneShotClips.TryGetValue(shot, out AudioClip clip)) {
                clip = RingtoneSynth.RenderOneShot(shot, AudioSettings.outputSampleRate);
                oneShotClips.Add(shot, clip);
            }

            oneShotSource.PlayOneShot(clip, MasterGain);
        }

        public void Dispose() {
            foreach (Voice v in voices) {
                if (v.Source != null) {
                    Object.Destroy(v.Source.clip);
                }
            }

            foreach (AudioClip clip in oneShotClips.Values) {
                Object.Destroy(clip);
            }
        }

        // Web Audio의 inverse 거리 모델: ref / (ref + rolloff * (d - ref))
        private AnimationCurve InverseRolloff() {
            const int samples = 40;
            var keys = new Keyframe[samples];
            for (int i = 0; i < samples; i++) {
                float t = i / (samples - 1f);
                float distance = Mathf.Max(RefDistance, t * MaxDistance);
                keys[i] = new Keyframe(t, RefDistance / (RefDistance + rolloff * (distance - RefDistance)));
            }

            var curve = new AnimationCurve(keys);
            for (int i = 0; i < samples; i++) {
                curve.SmoothTangents(i, 0f);
            }

            return curve;
        }
    }
}
#endif
