using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>Web Audio OscillatorType 순서와 같다. jslib에 정수로 넘긴다.</summary>
    public enum Waveform {
        Sine = 0,
        Square = 1,
        Triangle = 2,
        Sawtooth = 3
    }

    /// <summary>반복 재생되는 벨소리 한 주기. 음 n개를 noteGap 간격으로 울리고 pause만큼 쉰다.</summary>
    [CreateAssetMenu(menuName = "Missed Call/Ringtone Pattern", fileName = "Ringtone_")]
    public sealed class RingtonePattern : ScriptableObject {
        public float[] notes = {
            659.25f, 987.77f, 830.61f, 987.77f, 1318.51f, 987.77f,
            830.61f, 659.25f, 739.99f, 987.77f, 1108.73f, 1479.98f
        };

        public float noteGap = 0.125f;
        public float pause = 0.5f;
        public Waveform waveform = Waveform.Square;
        [Tooltip("한 옥타브 위 사인파를 겹쳐 반짝이는 소리를 낸다")]
        public bool shimmer = true;
        [Tooltip("주기 시작마다 진동음을 낸다")]
        public bool buzz = true;
        [Range(0.01f, 1f)] public float level = 0.34f;

        public float CycleLength => notes.Length * noteGap + pause;
    }
}
