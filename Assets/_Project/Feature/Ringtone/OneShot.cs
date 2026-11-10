namespace MissedCall.Feature.Ringtone {
    /// <summary>주파수가 절반으로 떨어지며 사라지는 짧은 효과음.</summary>
    public readonly struct OneShot {
        public readonly Waveform Wave;
        public readonly float Frequency;
        public readonly float Length;
        public readonly float Level;
        public readonly float Delay;

        public OneShot(Waveform wave, float frequency, float length, float level, float delay = 0f) {
            Wave = wave;
            Frequency = frequency;
            Length = length;
            Level = level;
            Delay = delay;
        }
    }

    /// <summary>프로토타입의 효과음 묶음. 임시 소리이며 폴리시 단계에서 확정한다.</summary>
    public static class Sfx {
        public static readonly OneShot[] Throw = { new OneShot(Waveform.Triangle, 170f, 0.1f, 0.28f) };
        public static readonly OneShot[] Grab = { new OneShot(Waveform.Triangle, 620f, 0.08f, 0.25f) };

        public static readonly OneShot[] Win = {
            new OneShot(Waveform.Sine, 880f, 0.14f, 0.3f),
            new OneShot(Waveform.Sine, 1320f, 0.2f, 0.3f, 0.12f)
        };

        public static readonly OneShot[] Miss = {
            new OneShot(Waveform.Square, 420f, 0.09f, 0.18f),
            new OneShot(Waveform.Square, 420f, 0.09f, 0.18f, 0.16f)
        };
    }
}
