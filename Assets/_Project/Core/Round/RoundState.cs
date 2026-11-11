namespace MissedCall.Core.Round {
    /// <summary>게임 흐름 상태. docs/design/game-design.md의 상태 흐름과 같다.</summary>
    public enum RoundState {
        Title,
        Playing,
        Answer,
        Won,
        Missed,
    }
}
