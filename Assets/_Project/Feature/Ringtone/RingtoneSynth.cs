#if !(UNITY_WEBGL && !UNITY_EDITOR)
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>jslib의 playNote, playBuzz, OneShot을 PCM으로 흉내 낸다. 에디터 근사용.</summary>
    internal static class RingtoneSynth {
        private const float NoteLength = 0.18f;
        private const float OutputScale = 0.8f;

        public static AudioClip RenderCycle(RingtonePattern pattern, int sampleRate) {
            int length = Mathf.Max(1, Mathf.CeilToInt(pattern.CycleLength * sampleRate));
            var data = new float[length];
            float at = 0f;
            for (int i = 0; i < pattern.notes.Length; i++) {
                if (i == 0 && pattern.buzz) {
                    AddBuzz(data, sampleRate, at);
                }

                AddNote(data, sampleRate, at, pattern.notes[i], pattern.waveform, pattern.shimmer, pattern.level);
                at += pattern.noteGap;
            }

            Finish(data);
            AudioClip clip = AudioClip.Create(pattern.name, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip RenderOneShot(in OneShot shot, int sampleRate) {
            int delay = Mathf.CeilToInt(shot.Delay * sampleRate);
            int body = Mathf.CeilToInt((shot.Length + 0.02f) * sampleRate);
            var data = new float[delay + body];
            float end = Mathf.Max(40f, shot.Frequency * 0.5f);
            double phase = 0;
            for (int i = 0; i < body; i++) {
                float t = i / (float)sampleRate;
                float k = Mathf.Clamp01(t / shot.Length);
                float frequency = shot.Frequency * Mathf.Pow(end / shot.Frequency, k);
                phase += frequency / sampleRate;
                float amp = ExpRamp(Mathf.Max(shot.Level, 0.0002f), 0.0001f, k);
                data[delay + i] = Wave(shot.Wave, phase) * amp;
            }

            Finish(data);
            AudioClip clip = AudioClip.Create("OneShot", data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 주기 끝을 넘는 소리는 처음으로 감아 넣어 루프가 끊기지 않게 한다.
        private static void AddNote(float[] data, int rate, float at, float frequency, Waveform wave, bool shimmer, float level) {
            int start = Mathf.RoundToInt(at * rate);
            int count = Mathf.CeilToInt(NoteLength * rate);
            float peak = Mathf.Max(level, 0.0002f);
            for (int i = 0; i < count; i++) {
                float t = i / (float)rate;
                float amp = t < 0.008f
                    ? ExpRamp(0.0001f, peak, t / 0.008f)
                    : t < 0.16f ? ExpRamp(peak, 0.0001f, (t - 0.008f) / 0.152f) : 0f;
                float sample = Wave(wave, frequency * t);
                if (shimmer) {
                    sample += Wave(Waveform.Sine, frequency * 2.01f * t);
                }

                data[(start + i) % data.Length] += sample * amp;
            }
        }

        private static void AddBuzz(float[] data, int rate, float at) {
            int start = Mathf.RoundToInt(at * rate);
            int count = Mathf.CeilToInt(0.36f * rate);
            for (int i = 0; i < count; i++) {
                float t = i / (float)rate;
                float amp = t < 0.02f ? Mathf.Lerp(0.0001f, 0.45f, t / 0.02f)
                    : t < 0.3f ? 0.45f
                    : t < 0.34f ? Mathf.Lerp(0.45f, 0.0001f, (t - 0.3f) / 0.04f) : 0f;
                data[(start + i) % data.Length] += Wave(Waveform.Sawtooth, 158f * t) * amp;
            }
        }

        private static float Wave(Waveform wave, double cycles) {
            float frac = (float)(cycles - System.Math.Floor(cycles));
            switch (wave) {
                case Waveform.Square: return frac < 0.5f ? 1f : -1f;
                case Waveform.Triangle: return frac < 0.5f ? 4f * frac - 1f : 3f - 4f * frac;
                case Waveform.Sawtooth: return 2f * frac - 1f;
                default: return Mathf.Sin(frac * 2f * Mathf.PI);
            }
        }

        private static float ExpRamp(float from, float to, float k) => from * Mathf.Pow(to / from, Mathf.Clamp01(k));

        private static void Finish(float[] data) {
            for (int i = 0; i < data.Length; i++) {
                data[i] = Mathf.Clamp(data[i] * OutputScale, -1f, 1f);
            }
        }
    }
}
#endif
