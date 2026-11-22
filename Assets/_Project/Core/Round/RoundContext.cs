using MissedCall.Core.Aim;
using MissedCall.Core.Callers;
using MissedCall.Core.Rooms;
using UnityEngine;

namespace MissedCall.Core.Round {
    /// <summary>
    /// 한 판의 공용 상태. Feature끼리는 서로를 참조하지 않고 이 객체를 통해 정보를 주고받는다.
    /// 판이 시작될 때 <see cref="Reset"/>으로 통째로 초기화하며, 다음 판으로 넘기는 값은 없다.
    /// </summary>
    public sealed class RoundContext {
        public RoundSeed Seed { get; private set; }
        public RoundState State { get; internal set; }

        /// <summary>판 시작 후 흐른 실제 시간(초). 받기 화면에서도 흐르고, 판이 끝나면 멈춘다.</summary>
        public float Elapsed { get; internal set; }
        public float Duration { get; internal set; }
        public float TimeLeft => Mathf.Max(0f, Duration - Elapsed);

        /// <summary>이번 판의 발신자. CallerStep이 판 준비 중에 고른다.</summary>
        public CallerProfile Caller { get; set; }

        /// <summary>집 구조. Layout이 판 준비 중에 채운다.</summary>
        public RoomGraph Graph { get; set; }

        /// <summary>폰. Phone이 판 준비 중에 채우고 판 동안 갱신한다.</summary>
        public Vector3 PhonePosition { get; set; }
        public int PhoneRoom { get; set; } = -1;
        public int Covers { get; set; }
        /// <summary>폰 조준 대상. Phone이 판 준비 중에 채우고, Interaction이 물건보다 먼저 조준한다.</summary>
        public IGrabbable Phone { get; set; }
        /// <summary>판 변수. Modifiers가 판 준비 중에 정하고 Phone, Ringtone, ScreenFx, Ui가 읽는다.</summary>
        public RoundModifier Modifier { get; set; }

        /// <summary>진동 모드 변수. 벨 없이 진동만 난다.</summary>
        public bool VibrationMode {
            get => Modifier == RoundModifier.Vibrate;
            set => Modifier = value ? RoundModifier.Vibrate : Modifier == RoundModifier.Vibrate ? RoundModifier.None : Modifier;
        }

        /// <summary>배터리 부족 변수에서 벨이 끊기는 경과 시간(초). 변수가 없으면 무한대.</summary>
        public float RingStopTime { get; set; } = float.PositiveInfinity;

        /// <summary>배터리가 다 되어 벨과 화면이 꺼졌는가.</summary>
        public bool BatteryDead => Elapsed >= RingStopTime;

        /// <summary>시간대 연출. 0 새벽(기본), 1 한밤, 2 흐린 아침. ScreenFx가 색만 바꾼다.</summary>
        public int Mood { get; set; }

        /// <summary>미끼 소리 위치와 방. 미끼 변수가 없으면 방이 -1.</summary>
        public Vector3 DecoyPosition { get; set; }
        public int DecoyRoom { get; set; } = -1;

        /// <summary>소음원 위치와 방. 소음 변수가 없으면 방이 -1.</summary>
        public Vector3 NoisePosition { get; set; }
        public int NoiseRoom { get; set; } = -1;

        /// <summary>플레이어(듣는 사람). Player가 갱신한다. 판 준비 중에는 시작 위치를 담는다.</summary>
        public Vector3 PlayerPosition { get; set; }
        public int PlayerRoom { get; set; } = -1;

        /// <summary>화면 중심이 가리키는 대상. Interaction이 매 프레임 갱신하고 Ui가 라벨을 고른다.</summary>
        public IAimTarget AimTarget { get; set; }

        /// <summary>판 통계. 결과 칭호 판정에 쓴다.</summary>
        public int Throws { get; set; }
        /// <summary>받은 시점의 <see cref="Elapsed"/>. 받지 못했으면 음수.</summary>
        public float AnswerTime { get; internal set; } = -1f;

        internal void Reset(RoundSeed seed, float duration) {
            Seed = seed;
            State = RoundState.Playing;
            Elapsed = 0f;
            Duration = duration;
            Caller = null;
            Graph = null;
            PhonePosition = Vector3.zero;
            PhoneRoom = -1;
            Covers = 0;
            Phone = null;
            Modifier = RoundModifier.None;
            Mood = 0;
            RingStopTime = float.PositiveInfinity;
            DecoyPosition = Vector3.zero;
            DecoyRoom = -1;
            NoisePosition = Vector3.zero;
            NoiseRoom = -1;
            PlayerPosition = Vector3.zero;
            PlayerRoom = -1;
            AimTarget = null;
            Throws = 0;
            AnswerTime = -1f;
        }
    }
}
