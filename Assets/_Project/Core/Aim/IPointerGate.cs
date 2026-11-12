namespace MissedCall.Core.Aim {
    /// <summary>
    /// 클릭을 게임 입력으로 써도 되는지 알려준다. 포인터 잠금을 얻는 데 쓴 클릭은 제외해야 하므로
    /// Interaction은 버튼 상태만 보지 않고 이 값을 함께 본다. Player의 PointerLook이 구현한다.
    /// </summary>
    public interface IPointerGate {
        bool CanAct { get; }
    }
}
