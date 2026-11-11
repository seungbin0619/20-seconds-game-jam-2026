using UnityEngine;

namespace MissedCall.Core.Aim {
    /// <summary>던질 수 있는 물건. 속도 계산은 Interaction이 하고, 물건은 받은 속도를 적용만 한다.</summary>
    public interface IThrowable : IAimTarget {
        /// <summary>바구니나 욕조처럼 둘레에 벽이 있는 자리에 놓여 있는지. 그렇다면 더 높이 띄워 던진다.</summary>
        bool Enclosed { get; }

        void Launch(Vector3 velocity);
    }
}
