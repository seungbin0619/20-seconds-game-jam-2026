namespace MissedCall.Core.Aim {
    /// <summary>조준 대상의 종류. Ui가 조준점 라벨을 고르는 데 쓴다 (Item: Grab, Phone: Grab phone, Door: Open, Drawer: Open / Close).</summary>
    public enum AimKind {
        Item,
        Phone,
        Door,
        /// <summary>열고 닫는 서랍 (<see cref="IClickable"/>)</summary>
        Drawer,
    }
}
