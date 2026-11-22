namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 방 모듈 안에서 판 동안 움직이는 것 (서랍, 굴러다니는 바구니). 모듈은 풀로 재사용하므로
    /// 판마다 모듈을 놓을 때 <see cref="RoomModule.ResetProps"/>가 처음 상태로 되돌린다.
    /// </summary>
    public interface IRoundReset {
        void ResetForRound();
    }
}
