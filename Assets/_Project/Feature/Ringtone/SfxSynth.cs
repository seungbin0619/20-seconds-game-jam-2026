using System;
using MissedCall.Core.Audio;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>
    /// 효과음을 PCM으로 합성한다. 시작할 때 한 번 만들어 두고 Web Audio와 Unity 오디오가 같은 샘플을 재생한다.
    /// 벨 기본음(660~1480Hz)을 가리지 않게 판 중에 나는 소리(발소리, 충돌, 심장 박동)는 낮은 대역에 둔다.
    /// </summary>
    internal static class SfxSynth {
        public const int SampleRate = 44100;

        /// <summary>종류마다 변형 몇 개. 재생할 때 하나를 고르고 음높이를 조금 흔든다.</summary>
        public static float[][] Render(SoundCue cue) {
            var noise = new Noise(0x5EED + (int)cue * 7919);
            switch (cue) {
                case SoundCue.Grab: return Variants(3, i => Grab(noise, i));
                case SoundCue.Throw: return Variants(2, i => Whoosh(noise, i));
                case SoundCue.Tug: return Variants(2, i => Tug(noise, i));
                case SoundCue.ImpactSoft: return Variants(3, i => ImpactSoft(noise, i));
                case SoundCue.ImpactHard: return Variants(3, i => ImpactHard(noise, i));
                case SoundCue.ImpactHollow: return Variants(3, i => ImpactHollow(noise, i));
                case SoundCue.Step: return Variants(4, i => Step(noise, i));
                case SoundCue.PhonePickup: return new[] { PhonePickup(noise) };
                case SoundCue.PhoneReveal: return new[] { PhoneReveal() };
                case SoundCue.Heartbeat: return new[] { Heartbeat() };
                case SoundCue.Win: return new[] { Win(noise) };
                case SoundCue.Miss: return new[] { Miss() };
                case SoundCue.UiClick: return new[] { Click(noise, 2400f, 0.5f) };
                case SoundCue.SlideGrab: return new[] { Click(noise, 1500f, 0.55f) };
                case SoundCue.SlideBack: return new[] { SlideBack(noise) };
                default: return new[] { new float[1] };
            }
        }

        private static float[][] Variants(int count, Func<int, float[]> make) {
            var list = new float[count][];
            for (int i = 0; i < count; i++) {
                list[i] = make(i);
            }

            return list;
        }

        // 천을 움켜쥐는 짧은 마찰음
        private static float[] Grab(Noise noise, int variant) {
            float length = 0.09f + variant * 0.015f;
            var data = Buffer(length);
            var band = new Biquad(Biquad.Kind.BandPass, 1800f + variant * 500f, 0.9f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float env = Attack(t, 0.006f) * Mathf.Exp(-t * 38f);
                data[i] = band.Process(noise.Next()) * env * 0.9f;
            }

            return Finish(data, 0.5f);
        }

        // 휙. 바람 소리 대역을 위로 쓸었다가 내린다.
        private static float[] Whoosh(Noise noise, int variant) {
            float length = 0.26f + variant * 0.06f;
            var data = Buffer(length);
            var band = new Biquad(Biquad.Kind.BandPass, 500f, 1.4f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float k = t / length;
                float center = Mathf.Lerp(380f, 1400f, Mathf.Sin(k * Mathf.PI) * 0.9f + k * 0.1f);
                band.Set(center, 1.4f);
                float env = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI), 1.6f);
                data[i] = band.Process(noise.Next()) * env;
            }

            return Finish(data, 0.5f);
        }

        // 깔린 물건이 끌리다 만다. 낮은 마찰음 두 번.
        private static float[] Tug(Noise noise, int variant) {
            float length = 0.16f;
            var data = Buffer(length);
            var band = new Biquad(Biquad.Kind.BandPass, 420f + variant * 90f, 1.6f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float a = Attack(t, 0.004f) * Mathf.Exp(-t * 45f);
                float b = t > 0.07f ? Attack(t - 0.07f, 0.004f) * Mathf.Exp(-(t - 0.07f) * 40f) * 0.7f : 0f;
                data[i] = band.Process(noise.Next()) * (a + b);
            }

            return Finish(data, 0.45f);
        }

        // 천, 쿠션이 떨어지는 털썩
        private static float[] ImpactSoft(Noise noise, int variant) {
            float length = 0.16f;
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, 520f + variant * 120f, 0.7f);
            float body = 85f + variant * 12f;
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float env = Attack(t, 0.003f) * Mathf.Exp(-t * 32f);
                float thump = Mathf.Sin(2f * Mathf.PI * body * t) * Mathf.Exp(-t * 40f) * 0.6f;
                data[i] = (low.Process(noise.Next()) * 1.4f + thump) * env;
            }

            return Finish(data, 0.55f);
        }

        // 책이 바닥이나 가구에 닿는 탁
        private static float[] ImpactHard(Noise noise, int variant) {
            float length = 0.14f;
            var data = Buffer(length);
            var high = new Biquad(Biquad.Kind.BandPass, 2600f + variant * 400f, 0.8f);
            var low = new Biquad(Biquad.Kind.LowPass, 900f, 0.7f);
            float body = 170f + variant * 25f;
            double phase = 0;
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float f = body * (1f + 0.6f * Mathf.Exp(-t * 60f));
                phase += f / SampleRate;
                float tone = Mathf.Sin((float)(phase * 2.0 * Math.PI)) * Mathf.Exp(-t * 34f);
                float n = noise.Next();
                float click = high.Process(n) * Mathf.Exp(-t * 260f) * 0.8f;
                float thud = low.Process(n) * Mathf.Exp(-t * 55f) * 0.7f;
                data[i] = (tone * 0.8f + click + thud) * Attack(t, 0.0015f);
            }

            return Finish(data, 0.6f);
        }

        // 빈 상자가 울리는 통
        private static float[] ImpactHollow(Noise noise, int variant) {
            float length = 0.22f;
            var data = Buffer(length);
            var r1 = new Biquad(Biquad.Kind.BandPass, 240f + variant * 30f, 6f);
            var r2 = new Biquad(Biquad.Kind.BandPass, 410f + variant * 45f, 5f);
            var low = new Biquad(Biquad.Kind.LowPass, 1200f, 0.7f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float n = noise.Next();
                float excite = Attack(t, 0.002f) * Mathf.Exp(-t * 90f);
                float ring = (r1.Process(n * excite) * 5f + r2.Process(n * excite) * 3.5f) * Mathf.Exp(-t * 14f);
                data[i] = ring + low.Process(n) * excite * 0.6f;
            }

            return Finish(data, 0.6f);
        }

        // 발소리. 벨을 가리지 않게 낮고 작다.
        private static float[] Step(Noise noise, int variant) {
            float length = 0.1f;
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, 300f + variant * 40f, 0.8f);
            float body = 62f + variant * 6f;
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float env = Attack(t, 0.004f) * Mathf.Exp(-t * 45f);
                float thump = Mathf.Sin(2f * Mathf.PI * body * t) * 0.7f;
                data[i] = (low.Process(noise.Next()) * 1.2f + thump) * env;
            }

            return Finish(data, 0.5f);
        }

        // 폰을 손에 쥐는 딸깍과 짧은 알림음
        private static float[] PhonePickup(Noise noise) {
            float length = 0.3f;
            var data = Buffer(length);
            var high = new Biquad(Biquad.Kind.BandPass, 3200f, 1.2f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float click = high.Process(noise.Next()) * Mathf.Exp(-t * 300f) * 1.2f;
                float chime = Bell(t - 0.03f, 1568f, 18f) * 0.5f + Bell(t - 0.09f, 2093f, 16f) * 0.4f;
                data[i] = click + chime;
            }

            return Finish(data, 0.45f);
        }

        // 폰이 드러남. 작게 반짝인다.
        private static float[] PhoneReveal() {
            float length = 0.45f;
            var data = Buffer(length);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                data[i] = Bell(t, 1760f, 9f) * 0.4f + Bell(t - 0.06f, 2637f, 10f) * 0.3f;
            }

            return Finish(data, 0.32f);
        }

        // 쿵-쿵. 100Hz 이하에 두어 벨을 가리지 않는다. 작은 스피커에서도 들리게 살짝 찌그러뜨려 배음을 만든다.
        private static float[] Heartbeat() {
            float length = 0.5f;
            var data = Buffer(length);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float lub = Thump(t, 58f, 0.9f);
                float dub = Thump(t - 0.2f, 50f, 0.65f);
                float s = lub + dub;
                data[i] = (float)Math.Tanh(s * 1.8f) * 0.8f;
            }

            return Finish(data, 0.85f);
        }

        private static float Thump(float t, float frequency, float level) {
            if (t < 0f) {
                return 0f;
            }

            float f = frequency * (1f + 0.35f * Mathf.Exp(-t * 30f));
            return Mathf.Sin(2f * Mathf.PI * f * t) * Attack(t, 0.008f) * Mathf.Exp(-t * 16f) * level;
        }

        // 받았다. 밝게 올라가는 세 음.
        private static float[] Win(Noise noise) {
            float length = 0.9f;
            var data = Buffer(length);
            var high = new Biquad(Biquad.Kind.BandPass, 2800f, 1f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float click = high.Process(noise.Next()) * Mathf.Exp(-t * 250f);
                float tones = Bell(t, 1046.5f, 5f) * 0.45f + Bell(t - 0.08f, 1318.5f, 5f) * 0.4f +
                    Bell(t - 0.16f, 1568f, 4f) * 0.45f + Bell(t - 0.16f, 2093f, 6f) * 0.15f;
                data[i] = click * 0.8f + tones;
            }

            return Finish(data, 0.5f);
        }

        // 놓쳤다. 통화가 끊긴 뒤의 뚜-뚜-뚜.
        private static float[] Miss() {
            float length = 0.95f;
            var data = Buffer(length);
            var soft = new Biquad(Biquad.Kind.LowPass, 1800f, 0.7f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float sum = 0f;
                for (int b = 0; b < 3; b++) {
                    float start = b * 0.3f;
                    float u = t - start;
                    if (u < 0f || u > 0.2f) {
                        continue;
                    }

                    float env = Attack(u, 0.01f) * Mathf.Clamp01((0.2f - u) / 0.02f);
                    float f = 480f - b * 25f;
                    sum += (Square(f * u) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 1.29f * u) * 0.5f) * env;
                }

                data[i] = soft.Process(sum);
            }

            return Finish(data, 0.32f);
        }

        private static float[] Click(Noise noise, float frequency, float level) {
            float length = 0.04f;
            var data = Buffer(length);
            var band = new Biquad(Biquad.Kind.BandPass, frequency, 2f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * 0.5f * t) * Mathf.Exp(-t * 120f) * 0.5f;
                data[i] = (band.Process(noise.Next()) * Mathf.Exp(-t * 160f) + tone) * Attack(t, 0.001f);
            }

            return Finish(data, level);
        }

        // 손잡이가 처음 자리로 미끄러져 돌아간다.
        private static float[] SlideBack(Noise noise) {
            float length = 0.18f;
            var data = Buffer(length);
            var band = new Biquad(Biquad.Kind.BandPass, 1600f, 2f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float k = t / length;
                band.Set(Mathf.Lerp(1600f, 500f, k), 2f);
                float env = Attack(t, 0.01f) * (1f - k);
                data[i] = band.Process(noise.Next()) * env;
            }

            return Finish(data, 0.35f);
        }

        private static float Bell(float t, float frequency, float decay) {
            if (t < 0f) {
                return 0f;
            }

            float env = Attack(t, 0.004f) * Mathf.Exp(-t * decay);
            return (Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * frequency * 2.76f * t) *
                Mathf.Exp(-t * decay * 2f)) * env;
        }

        private static float Square(float cycles) => cycles - Mathf.Floor(cycles) < 0.5f ? 1f : -1f;

        private static float Attack(float t, float time) => t <= 0f ? 0f : Mathf.Clamp01(t / time);

        private static float Sec(int i) => i / (float)SampleRate;

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

        // 최대값을 peak로 맞추고 끝을 짧게 페이드해 딸깍임을 없앤다.
        private static float[] Finish(float[] data, float peak) {
            float max = 0f;
            foreach (float s in data) {
                max = Mathf.Max(max, Mathf.Abs(s));
            }

            float scale = max > 1e-5f ? peak / max : 0f;
            int fade = Mathf.Min(data.Length, SampleRate / 200);
            for (int i = 0; i < data.Length; i++) {
                float tail = data.Length - 1 - i < fade ? (data.Length - 1 - i) / (float)fade : 1f;
                data[i] = Mathf.Clamp(data[i] * scale * tail, -1f, 1f);
            }

            return data;
        }

        // 합성용 결정적 잡음. 판 무작위와 상관없다.
        private struct Noise {
            private uint state;

            public Noise(int seed) {
                state = (uint)seed | 1u;
            }

            public float Next() {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state / (float)uint.MaxValue * 2f - 1f;
            }
        }

        // RBJ 쿡북 2차 필터
        private sealed class Biquad {
            public enum Kind {
                LowPass,
                BandPass,
            }

            private readonly Kind kind;
            private float b0, b1, b2, a1, a2;
            private float x1, x2, y1, y2;

            public Biquad(Kind kind, float frequency, float q) {
                this.kind = kind;
                Set(frequency, q);
            }

            public void Set(float frequency, float q) {
                float w = 2f * Mathf.PI * Mathf.Clamp(frequency, 10f, SampleRate * 0.45f) / SampleRate;
                float cos = Mathf.Cos(w);
                float alpha = Mathf.Sin(w) / (2f * q);
                float a0 = 1f + alpha;
                if (kind == Kind.LowPass) {
                    b0 = (1f - cos) * 0.5f / a0;
                    b1 = (1f - cos) / a0;
                    b2 = b0;
                } else {
                    b0 = alpha / a0;
                    b1 = 0f;
                    b2 = -alpha / a0;
                }

                a1 = -2f * cos / a0;
                a2 = (1f - alpha) / a0;
            }

            public float Process(float x) {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return y;
            }
        }
    }
}
