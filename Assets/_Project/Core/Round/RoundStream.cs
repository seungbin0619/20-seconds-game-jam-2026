namespace MissedCall.Core.Round {
    /// <summary>
    /// 판 시드에서 가르는 난수 스트림의 용도. 값이 곧 스트림 구분자이므로
    /// 기존 항목의 값을 바꾸면 같은 시드의 판이 달라진다. 새 항목은 끝에 붙인다.
    /// </summary>
    public enum RoundStream {
        Layout = 0,
        Phone = 1,
        Clutter = 2,
        Start = 3,
        Caller = 4,
        Modifier = 5,
        /// <summary>던질 때 무작위 회전. 판 중 플레이어 입력에 따라 뽑는 횟수가 달라진다.</summary>
        Throw = 6,
    }
}
