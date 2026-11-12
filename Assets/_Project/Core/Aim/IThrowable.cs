using UnityEngine;

namespace MissedCall.Core.Aim {
    /// <summary>
    /// 잡았다가 던질 수 있는 물건. 잡는 점을 따라가는 속도와 던지는 속도는 Interaction이 계산하고,
    /// 물건은 받은 속도를 적용만 한다.
    /// </summary>
    public interface IThrowable : IAimTarget {
        /// <summary>바구니나 욕조처럼 둘레에 벽이 있는 자리에 놓여 있는지. 그렇다면 먼저 위로 들어 올린 뒤 끌어온다.</summary>
        bool Enclosed { get; }

        /// <summary>물건의 중심 (월드).</summary>
        Vector3 Center { get; }

        /// <summary>위에 다른 물건이 얹혀 있는지. 그렇다면 잡히지 않고 조금 끌려 나오다 만다.</summary>
        bool Buried { get; }

        /// <summary>장면에 살아 있는지. 판이 바뀌어 풀로 돌아가면 false다.</summary>
        bool Alive { get; }

        /// <summary>손에 잡힌다. 중력을 끄고 회전을 잦아들게 한다.</summary>
        void BeginHold();

        /// <summary>잡힌 동안 물리 스텝마다 따라갈 속도를 받는다.</summary>
        void Drive(Vector3 velocity);

        /// <summary>손에서 놓는다. 중력을 되돌리고 받은 속도로 날아간다. 그냥 놓을 때는 0을 넘긴다.</summary>
        void Release(Vector3 velocity, Vector3 angularVelocity);
    }
}
