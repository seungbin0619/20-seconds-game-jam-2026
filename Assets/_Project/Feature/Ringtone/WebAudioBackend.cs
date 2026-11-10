#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>RingtoneWebAudio.jslib를 호출한다. 좌표계 변환(z 반전)은 jslib에서 한다.</summary>
    internal sealed class WebAudioBackend : IRingtoneBackend {
        [DllImport("__Internal")] private static extern int MC_Audio_Init(float rolloff);
        [DllImport("__Internal")] private static extern int MC_Audio_State();
        [DllImport("__Internal")] private static extern int MC_Audio_CreateVoice(float[] notes, int count, float noteGap, float pause, int wave, int shimmer, int buzz, float level);
        [DllImport("__Internal")] private static extern void MC_Audio_StartVoice(int id);
        [DllImport("__Internal")] private static extern void MC_Audio_StopVoice(int id);
        [DllImport("__Internal")] private static extern void MC_Audio_SetVoice(int id, float x, float y, float z, float cutoff, float gain);
        [DllImport("__Internal")] private static extern void MC_Audio_SetListener(float px, float py, float pz, float fx, float fy, float fz);
        [DllImport("__Internal")] private static extern void MC_Audio_OneShot(int wave, float frequency, float length, float level, float delay);
        [DllImport("__Internal")] private static extern void MC_Audio_Shutdown();

        public bool IsReady => MC_Audio_State() == 2;

        public string Status {
            get {
                switch (MC_Audio_State()) {
                    case 0: return "Web Audio unavailable";
                    case 1: return "Web Audio suspended (click to enable)";
                    default: return "Web Audio running";
                }
            }
        }

        public void Init(float rolloff) {
            if (MC_Audio_Init(rolloff) == 0) {
                Debug.LogWarning("[Ringtone] Web Audio is not available in this browser.");
            }
        }

        public int CreateVoice(RingtonePattern pattern) =>
            MC_Audio_CreateVoice(pattern.notes, pattern.notes.Length, pattern.noteGap, pattern.pause,
                (int)pattern.waveform, pattern.shimmer ? 1 : 0, pattern.buzz ? 1 : 0, pattern.level);

        public void StartVoice(int voice) => MC_Audio_StartVoice(voice);
        public void StopVoice(int voice) => MC_Audio_StopVoice(voice);

        public void SetVoice(int voice, Vector3 position, float cutoff, float gain) =>
            MC_Audio_SetVoice(voice, position.x, position.y, position.z, cutoff, gain);

        public void SetListener(Vector3 position, Vector3 forward) =>
            MC_Audio_SetListener(position.x, position.y, position.z, forward.x, forward.y, forward.z);

        public void PlayOneShot(in OneShot shot) =>
            MC_Audio_OneShot((int)shot.Wave, shot.Frequency, shot.Length, shot.Level, shot.Delay);

        public void Dispose() => MC_Audio_Shutdown();
    }
}
#endif
