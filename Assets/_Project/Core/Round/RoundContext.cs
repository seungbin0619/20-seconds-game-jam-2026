using MissedCall.Core.Aim;
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

        /// <summary>집 구조. Layout이 판 준비 중에 채운다.</summary>
        public RoomGraph Graph { get; set; }

        /// <summary>폰. Phone이 판 준비 중에 채우고 판 동안 갱신한다.</summary>
        public Vector3 PhonePosition { get; set; }
        public int PhoneRoom { get; set; } = -1;
        public int Covers { get; set; }
        /// <summary>폰 조준 대상. Phone이 판 준비 중에 채우고, Interaction이 물건보다 먼저 조준한다.</summary>
        public IGrabbable Phone { get; set; }
        /// <summary>진동 모드 변수. 벨 없이 진동만 난다. Modifiers가 판 준비 중에 정하고 Phone, Ringtone이 읽는다.</summary>
        public bool VibrationMode { get; set; }

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
            Graph = null;
            PhonePosition = Vector3.zero;
            PhoneRoom = -1;
            Covers = 0;
            Phone = null;
            VibrationMode = false;
            PlayerPosition = Vector3.zero;
            PlayerRoom = -1;
            AimTarget = null;
            Throws = 0;
            AnswerTime = -1f;
        }
    }
}
