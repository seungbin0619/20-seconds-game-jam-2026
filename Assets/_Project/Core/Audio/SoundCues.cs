using System;
using UnityEngine;

namespace MissedCall.Core.Audio {
    /// <summary>게임에서 나는 효과음 종류. 소리를 만드는 쪽(Ringtone)이 종류마다 소리를 고른다.</summary>
    public enum SoundCue {
        Grab,
        Throw,
        /// <summary>깔린 물건을 잡으려다 끌려 나오다 만 소리</summary>
        Tug,
        ImpactSoft,
        ImpactHard,
        ImpactHollow,
        Step,
        PhonePickup,
        /// <summary>폰 위의 물건이 다 치워져 폰이 드러났다</summary>
        PhoneReveal,
        Heartbeat,
        Win,
        Miss,
        UiClick,
        SlideGrab,
        SlideBack,
        /// <summary>배터리가 다 되어 폰이 꺼진다 (배터리 부족 변수)</summary>
        BatteryDead,
    }

    /// <summary>효과음 한 번. <see cref="Spatial"/>이면 <see cref="Position"/>에서 들린다.</summary>
    public readonly struct SoundEvent {
        public readonly SoundCue Cue;
        public readonly bool Spatial;
        public readonly Vector3 Position;
        /// <summary>0~1. 볼륨과 음높이에 쓴다.</summary>
        public readonly float Intensity;

        public SoundEvent(SoundCue cue, bool spatial, Vector3 position, float intensity) {
            Cue = cue;
            Spatial = spatial;
            Position = position;
            Intensity = intensity;
        }
    }

    /// <summary>
    /// 효과음 계약. Feature는 서로를 참조하지 않으므로 소리가 필요한 쪽은 여기로 알리고, Ringtone이 듣고 소리를 낸다.
    /// 게임플레이 상태는 담지 않는다.
    /// </summary>
    public static class SoundCues {
        private static int muted;

        public static event Action<SoundEvent> Played;

        /// <summary>판 준비(물리 안정화) 중처럼 소리를 내지 않아야 하는 동안 true.</summary>
        public static bool Muted => muted > 0;

        public static void Play(SoundCue cue, float intensity = 1f) {
            if (!Muted) {
                Played?.Invoke(new SoundEvent(cue, false, Vector3.zero, Mathf.Clamp01(intensity)));
            }
        }

        public static void PlayAt(SoundCue cue, Vector3 position, float intensity = 1f) {
            if (!Muted) {
                Played?.Invoke(new SoundEvent(cue, true, position, Mathf.Clamp01(intensity)));
            }
        }

        public static void BeginMute() => muted++;
        public static void EndMute() => muted = Mathf.Max(0, muted - 1);
    }
}
