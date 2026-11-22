namespace MissedCall.Core.Aim {
    /// <summary>
    /// 클릭하면 열리고 닫히는 대상 (서랍). Interaction이 누를 때 <see cref="Click"/>을 부르고, Ui는 <see cref="IsOpen"/>으로 라벨을 고른다.
    /// </summary>
    public interface IClickable : IAimTarget {
        bool IsOpen { get; }

        void Click();
    }
}
