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

            // 가중치로 고른다. 희귀 발신자는 가중치가 작다.
            System.Random rng = context.Seed.Stream(RoundStream.Caller);
            float total = 0f;
            foreach (CallerProfile caller in callers) {
                total += Mathf.Max(0f, caller.weight);
            }

            float pick = (float)rng.NextDouble() * total;
            context.Caller = callers[callers.Length - 1];
            foreach (CallerProfile caller in callers) {
                pick -= Mathf.Max(0f, caller.weight);
                if (pick < 0f) {
                    context.Caller = caller;
                    break;
                }
            }
        }
    }
}
