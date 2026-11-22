namespace MissedCall.Core.Round {
    /// <summary>판 변수. 판마다 0~1개 붙는다 (game-design.md 변수 표). 값은 바꾸지 않고 끝에만 추가한다.</summary>
    public enum RoundModifier {
        None = 0,
        /// <summary>벨 없이 진동 소리와 물건 떨림만 난다.</summary>
        Vibrate = 1,
        /// <summary>벨이 20초보다 일찍 끊긴다.</summary>
        LowBattery = 2,
        /// <summary>다른 숨는 자리에서 가짜 소리(알람시계)가 난다.</summary>
        Decoy = 3,
        /// <summary>한 방에서 환풍기 소음이 벨을 가린다.</summary>
        Noise = 4,
        /// <summary>집이 어둡다. 폰 화면 불빛이 단서다.</summary>
        Blackout = 5,
    }
}
