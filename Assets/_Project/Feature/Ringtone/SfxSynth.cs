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

        /// <summary>
        /// 종류마다 변형 여러 개. 변형마다 잡음과 필터, 길이를 조금씩 다르게 만들고, 재생할 때 직전과 다른 하나를 골라
        /// 음높이와 볼륨을 조금 흔든다. 같은 소리가 반복되어 귀에 걸리지 않게 한다.
        /// </summary>
        public static float[][] Render(SoundCue cue) {
            var noise = new Noise(0x5EED + (int)cue * 7919);
            switch (cue) {
                case SoundCue.Grab: return Variants(6, i => Grab(noise, i));
                case SoundCue.Throw: return Variants(5, i => Whoosh(noise, i));
                case SoundCue.Tug: return Variants(4, i => Tug(noise, i));
                case SoundCue.ImpactSoft: return Variants(5, i => ImpactSoft(noise, i));
                case SoundCue.ImpactHard: return Variants(5, i => ImpactHard(noise, i));
                case SoundCue.ImpactHollow: return Variants(5, i => ImpactHollow(noise, i));
                case SoundCue.Step: return Variants(6, i => Step(noise, i));
                case SoundCue.PhonePickup: return new[] { PhonePickup(noise) };
                case SoundCue.PhoneReveal: return new[] { PhoneReveal() };
                case SoundCue.Heartbeat: return new[] { Heartbeat() };
                case SoundCue.Win: return new[] { Win(noise) };
                case SoundCue.Miss: return new[] { Miss() };
                case SoundCue.UiClick: return Variants(3, i => Tock(noise, 760f + i * 70f, 0.5f));
                case SoundCue.SlideGrab: return Variants(2, i => Tock(noise, 560f + i * 50f, 0.55f));
                case SoundCue.SlideBack: return new[] { SlideBack(noise) };
                case SoundCue.BatteryDead: return new[] { BatteryDead() };
                case SoundCue.Meow: return Variants(3, i => Meow(noise, i));
                case SoundCue.Kick: return Variants(4, i => Kick(noise, i));
                case SoundCue.Slam: return Variants(4, i => Slam(noise, i));
                case SoundCue.Drawer: return Variants(3, i => Drawer(noise, i));
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

        // -1~1 잡음으로 [from, to] 사이 값을 고른다. 변형마다 다른 값이 나온다.
        private static float Pick(Noise noise, float from, float to) => Mathf.Lerp(from, to, noise.Next() * 0.5f + 0.5f);

        // 천을 쥐는 바스락. 낮게 거른 작은 알갱이 몇 개를 겹친다. 날카로운 시작음이 없게 천천히 올라온다.
        private static float[] Grab(Noise noise, int variant) {
            float length = Pick(noise, 0.1f, 0.15f);
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, Pick(noise, 650f, 1000f), 0.6f);
            var rumble = new Biquad(Biquad.Kind.LowPass, 120f, 0.7f);
            int grains = 3 + variant % 3;
            var starts = new float[grains];
            var levels = new float[grains];
            for (int g = 0; g < grains; g++) {
                starts[g] = Pick(noise, 0f, length * 0.55f);
                levels[g] = Pick(noise, 0.4f, 1f);
            }

            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float env = 0f;
                for (int g = 0; g < grains; g++) {
                    float u = t - starts[g];
                    if (u > 0f) {
                        env += levels[g] * Attack(u, 0.012f) * Mathf.Exp(-u * 55f);
                    }
                }

                float n = low.Process(noise.Next());
                // 아주 낮은 대역을 빼서 웅웅거리지 않게
                data[i] = (n - rumble.Process(n)) * env * Fade(t, length, 0.03f);
            }

            return Finish(data, 0.45f);
        }

        // 휙. 바람 소리를 낮게 거르고 천천히 부풀었다 가라앉는다. 대역 공진 없이 부드럽게.
        private static float[] Whoosh(Noise noise, int variant) {
            float length = Pick(noise, 0.22f, 0.34f);
            float peak = Pick(noise, 0.4f, 0.6f);
            float top = Pick(noise, 550f, 800f);
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, 300f, 0.6f);
            var low2 = new Biquad(Biquad.Kind.LowPass, 300f, 0.6f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float k = t / length;
                // 부푸는 곳(peak)에서 가장 밝다
                float shape = k < peak ? k / peak : 1f - (k - peak) / (1f - peak);
                shape = shape * shape * (3f - 2f * shape);
                float cutoff = Mathf.Lerp(220f, top, shape);
                low.Set(cutoff, 0.6f);
                low2.Set(cutoff, 0.6f);
                data[i] = low2.Process(low.Process(noise.Next())) * shape;
            }

            return Finish(data, 0.5f);
        }

        // 깔린 물건이 끌리다 만다. 먹먹한 한 번의 툭.
        private static float[] Tug(Noise noise, int variant) {
            float length = Pick(noise, 0.12f, 0.16f);
            float body = Pick(noise, 70f, 95f);
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, Pick(noise, 260f, 380f), 0.6f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float env = Attack(t, 0.01f) * Mathf.Exp(-t * 30f);
                float thump = Mathf.Sin(2f * Mathf.PI * body * t) * 0.35f;
                data[i] = (low.Process(noise.Next()) * 1.3f + thump) * env * Fade(t, length, 0.03f);
            }

            return Finish(data, 0.4f);
        }

        // 발차기. 발등이 닿는 낮은 퍽과 짧은 바람.
        private static float[] Kick(Noise noise, int variant) {
            float length = Pick(noise, 0.2f, 0.26f);
            float body = Pick(noise, 62f, 85f);
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, Pick(noise, 380f, 520f), 0.7f);
            var air = new Biquad(Biquad.Kind.LowPass, 600f, 0.6f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float n = noise.Next();
                float env = Attack(t, 0.003f) * Mathf.Exp(-t * 38f);
                float thump = Mathf.Sin(2f * Mathf.PI * body * (1f + 0.5f * Mathf.Exp(-t * 50f)) * t) * Mathf.Exp(-t * 30f);
                float swish = air.Process(n) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length)) * 0.35f;
                data[i] = (low.Process(n) * 1.2f * env + thump * 0.9f + swish) * Fade(t, length, 0.04f);
            }

            return Finish(data, 0.6f);
        }

        // 서랍: 나무가 미끄러지는 낮은 드르륵과 끝에서 닿는 툭.
        private static float[] Drawer(Noise noise, int variant) {
            float slide = Pick(noise, 0.16f, 0.22f);
            float length = slide + 0.09f;
            float body = Pick(noise, 110f, 150f);
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, Pick(noise, 500f, 700f), 0.7f);
            var grain = new Biquad(Biquad.Kind.BandPass, Pick(noise, 900f, 1200f), 1.5f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float n = noise.Next();
                // 미끄러지는 동안 잘게 떨리는 마찰
                float rub = t < slide ? Mathf.Sin(Mathf.PI * t / slide) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 38f * t)) : 0f;
                float u = t - slide;
                float knock = u > 0f ? Attack(u, 0.002f) * Mathf.Exp(-u * 45f) : 0f;
                float thump = u > 0f ? Mathf.Sin(2f * Mathf.PI * body * u) * knock : 0f;
                data[i] = low.Process(n) * rub * 0.8f + grain.Process(n) * rub * 0.25f + (low.Process(n) * 0.6f + thump) * knock;
            }

            return Finish(data, 0.5f);
        }

        // 몸으로 더미를 들이받는 둔한 털썩. 충돌음보다 낮고 길다.
        private static float[] Slam(Noise noise, int variant) {
            float length = Pick(noise, 0.22f, 0.3f);
            float body = Pick(noise, 55f, 75f);
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, Pick(noise, 300f, 440f), 0.7f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float env = Attack(t, 0.008f) * Mathf.Exp(-t * 20f);
                float thump = Mathf.Sin(2f * Mathf.PI * body * t) * Mathf.Exp(-t * 26f) * 0.8f;
                data[i] = (low.Process(noise.Next()) * 1.4f + thump) * env * Fade(t, length, 0.05f);
            }

            return Finish(data, 0.55f);
        }

        // 끝을 부드럽게 줄인다.
        private static float Fade(float t, float length, float time) => Mathf.Clamp01((length - t) / time);

        // 천, 쿠션이 떨어지는 털썩
        private static float[] ImpactSoft(Noise noise, int variant) {
            float length = 0.16f;
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, Pick(noise, 480f, 900f), 0.7f);
            float body = Pick(noise, 80f, 130f);
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
            var high = new Biquad(Biquad.Kind.BandPass, Pick(noise, 1300f, 2000f), 0.8f);
            var low = new Biquad(Biquad.Kind.LowPass, 900f, 0.7f);
            float body = Pick(noise, 160f, 280f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float f = body * (1f + 0.6f * Mathf.Exp(-t * 60f));
                phase += f / SampleRate;
                float tone = Mathf.Sin((float)(phase * 2.0 * Math.PI)) * Mathf.Exp(-t * 34f);
                float n = noise.Next();
                float click = high.Process(n) * Mathf.Exp(-t * 300f) * 0.3f;
                float thud = low.Process(n) * Mathf.Exp(-t * 55f) * 0.7f;
                data[i] = (tone * 0.8f + click + thud) * Attack(t, 0.0015f);
            }

            return Finish(data, 0.6f);
        }

        // 빈 상자가 울리는 통
        private static float[] ImpactHollow(Noise noise, int variant) {
            float length = 0.22f;
            var data = Buffer(length);
            var r1 = new Biquad(Biquad.Kind.BandPass, Pick(noise, 220f, 360f), 6f);
            var r2 = new Biquad(Biquad.Kind.BandPass, Pick(noise, 380f, 560f), 5f);
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
            var low = new Biquad(Biquad.Kind.LowPass, Pick(noise, 260f, 460f), 0.8f);
            float body = Pick(noise, 58f, 90f);
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
                float click = high.Process(noise.Next()) * Mathf.Exp(-t * 350f) * 0.4f;
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

        // 받았다. 멜로디 대신 통화가 연결되는 소리: 부드러운 딸깍, 전화선 잡음이 잠깐 열렸다 닫히고,
        // 아래에 낮고 따뜻한 화음(A3, E4)이 천천히 부풀었다 가라앉는다. 긴장이 풀리는 느낌만 준다.
        private static float[] Win(Noise noise) {
            float length = 0.75f;
            var data = Buffer(length);
            var clickBand = new Biquad(Biquad.Kind.BandPass, 1400f, 1.2f);
            var lineBand = new Biquad(Biquad.Kind.BandPass, 1100f, 0.5f);
            var warm = new Biquad(Biquad.Kind.LowPass, 900f, 0.7f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float n = noise.Next();
                float click = clickBand.Process(n) * Attack(t, 0.001f) * Mathf.Exp(-t * 180f) * 0.6f;
                float line = lineBand.Process(n) * Attack(t - 0.02f, 0.03f) * Mathf.Exp(-t * 9f) * 0.12f;
                float u = t - 0.03f;
                float swell = u > 0f ? Attack(u, 0.06f) * Mathf.Exp(-u * 4.2f) : 0f;
                float chord = (Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 329.63f * t) * 0.35f
                    + Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.12f) * swell;
                data[i] = click + line + warm.Process(chord) * Fade(t, length, 0.08f);
            }

            return Finish(data, 0.45f);
        }

        // 놓쳤다. 통화가 끊긴 뒤의 뚜-뚜-뚜.
        private static float[] Miss() {
            float length = 0.95f;
            var data = Buffer(length);
            var soft = new Biquad(Biquad.Kind.LowPass, 1200f, 0.7f);
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
                    sum += (Square(f * u) * 0.2f + Mathf.Sin(2f * Mathf.PI * f * u) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 1.29f * u) * 0.4f) * env;
                }

                data[i] = soft.Process(sum);
            }

            return Finish(data, 0.32f);
        }

        // 부드러운 똑. 짧은 사인에 아주 약한 잡음.
        private static float[] Tock(Noise noise, float frequency, float level) {
            float length = 0.07f;
            var data = Buffer(length);
            var low = new Biquad(Biquad.Kind.LowPass, frequency * 2f, 0.7f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float f = frequency * (1f + 0.15f * Mathf.Exp(-t * 80f));
                float tone = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 65f);
                float n = low.Process(noise.Next()) * Mathf.Exp(-t * 200f) * 0.25f;
                data[i] = (tone + n) * Attack(t, 0.002f) * Fade(t, length, 0.01f);
            }

            return Finish(data, level);
        }

        // 배터리가 다 되어 꺼지는 소리. 낮아지는 짧은 두 음.
        private static float[] BatteryDead() {
            float length = 0.42f;
            var data = Buffer(length);
            var soft = new Biquad(Biquad.Kind.LowPass, 1500f, 0.7f);
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float a = t < 0.16f ? Mathf.Sin(2f * Mathf.PI * 880f * t) * Attack(t, 0.008f) * Mathf.Clamp01((0.16f - t) / 0.02f) : 0f;
                float u = t - 0.2f;
                float b = u > 0f && u < 0.2f ? Mathf.Sin(2f * Mathf.PI * 587.33f * u) * Attack(u, 0.008f) * Mathf.Clamp01((0.2f - u) / 0.05f) : 0f;
                data[i] = soft.Process(a + b);
            }

            return Finish(data, 0.4f);
        }

        // 야옹. 음높이가 올라갔다 내려가는 배음 많은 소리를 모음처럼 걸러 낸다.
        private static float[] Meow(Noise noise, int variant) {
            float length = Pick(noise, 0.45f, 0.65f);
            float peak = Pick(noise, 680f, 820f);
            var data = Buffer(length);
            var f1 = new Biquad(Biquad.Kind.BandPass, 900f, 2.5f);
            var f2 = new Biquad(Biquad.Kind.BandPass, 2200f, 3f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++) {
                float t = Sec(i);
                float k = t / length;
                // 미- 야- 옹: 올라갔다 내려간다
                float pitch = k < 0.35f ? Mathf.Lerp(peak * 0.7f, peak, k / 0.35f) : Mathf.Lerp(peak, peak * 0.62f, (k - 0.35f) / 0.65f);
                pitch *= 1f + 0.015f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                phase += pitch / SampleRate;
                float saw = (float)(2.0 * (phase - System.Math.Floor(phase)) - 1.0);
                // 모음: '이'에서 '아'를 거쳐 '오'로
                f1.Set(Mathf.Lerp(500f, 1000f, Mathf.Sin(k * Mathf.PI)), 2.5f);
                f2.Set(Mathf.Lerp(2400f, 1300f, k), 3f);
                float env = Attack(t, 0.04f) * Mathf.Clamp01((length - t) / 0.12f);
                data[i] = (f1.Process(saw) + f2.Process(saw) * 0.5f) * env;
            }

            return Finish(data, 0.5f);
        }

        /// <summary>
        /// 소음 변수의 환풍기 소리. 끊김 없이 반복되도록 끝을 처음에 겹쳐 만든다. 날개가 도는 느린 떨림과 낮은 모터 웅웅거림을 섞는다.
        /// </summary>
        public static float[] FanLoop() {
            const float length = 3f;
            const float overlap = 0.3f;
            var noise = new Noise(0xFA4);
            int total = Mathf.CeilToInt((length + overlap) * SampleRate);
            var raw = new float[total];
            var low = new Biquad(Biquad.Kind.LowPass, 700f, 0.6f);
            var band = new Biquad(Biquad.Kind.BandPass, 1600f, 0.8f);
            for (int i = 0; i < total; i++) {
                float t = Sec(i);
                float n = noise.Next();
                // 날개 한 바퀴마다 바람이 조금 세졌다 약해진다 (3초에 정수 번)
                float blade = 1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                float hum = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.18f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.06f;
                raw[i] = (low.Process(n) * 1.6f + band.Process(n) * 0.35f) * blade + hum;
            }

            int count = Mathf.CeilToInt(length * SampleRate);
            int fade = total - count;
            var data = new float[count];
            for (int i = 0; i < count; i++) {
                data[i] = raw[i];
                if (i < fade) {
                    float k = i / (float)fade;
                    data[i] = raw[i] * k + raw[count + i] * (1f - k);
                }
            }

            return Finish(data, 0.6f);
        }

        /// <summary>
        /// 로봇청소기 변수의 모터 소리. 높은 모터 윙(벨 대역 아래 520Hz 언저리)과 흡입 바람, 솔이 바닥을 긁는 사각거림.
        /// 끊김 없이 반복되도록 끝을 처음에 겹쳐 만든다.
        /// </summary>
        public static float[] VacuumLoop() {
            const float length = 2f;
            const float overlap = 0.25f;
            var noise = new Noise(0x7AC);
            int total = Mathf.CeilToInt((length + overlap) * SampleRate);
            var raw = new float[total];
            var air = new Biquad(Biquad.Kind.LowPass, 1100f, 0.6f);
            var brush = new Biquad(Biquad.Kind.BandPass, 2400f, 1.2f);
            for (int i = 0; i < total; i++) {
                float t = Sec(i);
                float n = noise.Next();
                // 모터가 조금씩 떨리며 돈다 (2초에 정수 번)
                float wobble = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 3f * t);
                float motor = Mathf.Sin(2f * Mathf.PI * 520f * wobble * t) * 0.07f + Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.1f;
                // 솔이 한 바퀴 돌 때마다 사각거림이 커졌다 작아진다
                float sweep = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 8f * t);
                raw[i] = air.Process(n) * 1.1f + brush.Process(n) * 0.25f * sweep + motor;
            }

            int count = Mathf.CeilToInt(length * SampleRate);
            int fade = total - count;
            var data = new float[count];
            for (int i = 0; i < count; i++) {
                data[i] = raw[i];
                if (i < fade) {
                    float k = i / (float)fade;
                    data[i] = raw[i] * k + raw[count + i] * (1f - k);
                }
            }

            return Finish(data, 0.6f);
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
        private sealed class Noise {
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
