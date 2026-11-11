namespace MissedCall.Core.Aim {
    /// <summary>
    /// 화면 중심으로 조준해 클릭할 수 있는 대상. Interaction은 콜라이더에서
    /// <c>GetComponentInParent&lt;IAimTarget&gt;()</c>로 찾고, 구체 타입(Clutter, Phone 등)은 모른다.
    /// </summary>
    public interface IAimTarget {
        AimKind Kind { get; }
    }
}
