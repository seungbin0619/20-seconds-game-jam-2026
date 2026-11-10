using MissedCall.Core;
using UnityEngine;

namespace MissedCall.Feature.Ringtone {
    /// <summary>docs/design/game-design.md의 먹먹함, 볼륨 공식.</summary>
    public static class RingtoneMath {
        /// <param name="walls">벽 수. 문 근처에서는 소수가 된다.</param>
        public static float Cutoff(GameTuning tuning, int covers, float walls) {
            float level = Mathf.Min(1f, covers / (float)Mathf.Max(1, tuning.maxCovers));
            float covered = tuning.clearCutoff
                * Mathf.Pow(tuning.coveredCutoff / tuning.clearCutoff, Mathf.Pow(level, 0.6f));
            float cutoff = covered * Mathf.Pow(tuning.wallCutoffScale, Mathf.Max(0f, walls));
            return Mathf.Max(tuning.minCutoff, cutoff);
        }

        /// <param name="elapsed01">경과 시간 / 전체 시간</param>
        /// <param name="walls">벽 수. 문 근처에서는 소수가 된다.</param>
        public static float Gain(GameTuning tuning, float elapsed01, float walls = 0f) {
            float ring = Mathf.Lerp(tuning.ringGainStart, tuning.ringGainEnd, Mathf.Clamp01(elapsed01));
            return ring * Mathf.Pow(tuning.wallGainScale, Mathf.Max(0f, walls));
        }
    }
}
