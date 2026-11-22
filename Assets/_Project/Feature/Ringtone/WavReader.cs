using System;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>
    /// 녹음 효과음(.wav를 .bytes로 넣은 것)을 PCM으로 읽는다. Web 빌드에서는 AudioClip의 샘플을 읽을 수 없어서
    /// 원본 바이트를 직접 풀어 합성 효과음과 같은 경로(<see cref="IRingtoneBackend.CreateSample"/>)로 재생한다.
    /// 16비트 PCM만 읽고, 여러 채널이면 섞어 모노로 만든다.
    /// </summary>
    internal static class WavReader {
        /// <param name="trimThreshold">앞뒤에서 이 크기(최대값 비율)보다 작은 소리는 잘라 낸다</param>
        public static float[] Read(byte[] bytes, out int sampleRate, float trimThreshold = 0.04f) {
            sampleRate = 44100;
            if (bytes == null || bytes.Length < 44 || Tag(bytes, 0) != "RIFF" || Tag(bytes, 8) != "WAVE") {
                return null;
            }

            int channels = 1;
            int bits = 16;
            int offset = 12;
            while (offset + 8 <= bytes.Length) {
                string id = Tag(bytes, offset);
                int size = BitConverter.ToInt32(bytes, offset + 4);
                int body = offset + 8;
                if (id == "fmt ") {
                    channels = BitConverter.ToInt16(bytes, body + 2);
                    sampleRate = BitConverter.ToInt32(bytes, body + 4);
                    bits = BitConverter.ToInt16(bytes, body + 14);
                } else if (id == "data") {
                    if (bits != 16 || channels < 1) {
                        Debug.LogWarning($"[WavReader] Only 16-bit PCM is supported ({bits} bit)");
                        return null;
                    }

                    size = Mathf.Min(size, bytes.Length - body);
                    int frames = size / (2 * channels);
                    var data = new float[frames];
                    for (int i = 0; i < frames; i++) {
                        float sum = 0f;
                        for (int c = 0; c < channels; c++) {
                            sum += BitConverter.ToInt16(bytes, body + (i * channels + c) * 2) / 32768f;
                        }

                        data[i] = sum / channels;
                    }

                    return Clean(data, sampleRate, trimThreshold);
                }

                offset = body + size + (size & 1);
            }

            return null;
        }

        // 앞뒤의 조용한 부분(배경 잡음)을 잘라 내고, 끝을 짧게 줄여 딸깍임을 없애고, 최대값을 0.9로 맞춘다.
        private static float[] Clean(float[] data, int rate, float threshold) {
            float peak = 0f;
            foreach (float s in data) {
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }

            if (peak <= 1e-4f) {
                return data;
            }

            float limit = peak * threshold;
            int start = 0;
            while (start < data.Length && Mathf.Abs(data[start]) < limit) {
                start++;
            }

            int end = data.Length - 1;
            while (end > start && Mathf.Abs(data[end]) < limit) {
                end--;
            }

            // 소리 앞뒤로 조금 여유를 둔다.
            start = Mathf.Max(0, start - rate / 100);
            end = Mathf.Min(data.Length - 1, end + rate / 20);
            int length = end - start + 1;
            var result = new float[length];
            int fadeIn = Mathf.Min(length / 2, rate / 200);
            int fadeOut = Mathf.Min(length / 2, rate / 25);
            float gain = 0.9f / peak;
            for (int i = 0; i < length; i++) {
                float k = 1f;
                if (i < fadeIn) {
                    k = i / (float)fadeIn;
                } else if (length - 1 - i < fadeOut) {
                    k = (length - 1 - i) / (float)fadeOut;
                }

                result[i] = data[start + i] * gain * k;
            }

            return result;
        }

        private static string Tag(byte[] bytes, int offset) =>
            offset + 4 <= bytes.Length ? System.Text.Encoding.ASCII.GetString(bytes, offset, 4) : string.Empty;
    }
}
