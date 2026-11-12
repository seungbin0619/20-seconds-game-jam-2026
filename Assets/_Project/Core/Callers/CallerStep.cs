using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Core.Callers {
    /// <summary>판 준비: 판 시드의 Caller 스트림으로 발신자를 고른다. 다른 단계와 무관하므로 순서는 상관없다.</summary>
    public sealed class CallerStep : RoundStep {
        [SerializeField] private CallerCatalog catalog;

        public override void Prepare(RoundContext context) {
            CallerProfile[] callers = catalog.callers;
            if (callers.Length == 0) {
                Debug.LogError("[CallerStep] Caller catalog is empty", this);
                return;
            }

            System.Random rng = context.Seed.Stream(RoundStream.Caller);
            context.Caller = callers[rng.Next(callers.Length)];
        }
    }
}
