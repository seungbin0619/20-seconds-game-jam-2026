using UnityEngine;

namespace MissedCall.Core.Aim {
    /// <summary>집을 수 있는 대상 (폰). 집기 조건(거리, 가림막) 판정은 Interaction이 하고, 대상은 집혔을 때 반응만 한다.</summary>
    public interface IGrabbable : IAimTarget {
        /// <summary>조준 판정에 쓰는 점 (월드). 콜라이더 없이 화면 중심 레이와 이 점의 거리로 조준한다.</summary>
        Vector3 GrabPoint { get; }

        void Grab();
    }
}
