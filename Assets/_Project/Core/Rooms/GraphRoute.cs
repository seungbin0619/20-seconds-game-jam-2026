using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 듣는 사람에서 음원까지 문을 거치는 최단 경로. Ringtone의 SoundPath.ThroughDoor 입력과 대응한다.
    /// </summary>
    public readonly struct GraphRoute {
        /// <summary>같은 방이면 true. 이때 나머지 값은 의미가 없다.</summary>
        public readonly bool SameRoom;
        /// <summary>듣는 사람의 방에서 음원 쪽으로 나가는 첫 문.</summary>
        public readonly Vector3 FirstDoor;
        /// <summary>첫 문 다음 지점. 벽이 1겹이면 음원, 더 많으면 다음 문.</summary>
        public readonly Vector3 AfterDoor;
        /// <summary>첫 문에서 음원까지 경로 길이(m).</summary>
        public readonly float PathFromDoor;
        /// <summary>지나는 문 수 = 사이의 벽 수.</summary>
        public readonly int Walls;

        public GraphRoute(bool sameRoom, Vector3 firstDoor, Vector3 afterDoor, float pathFromDoor, int walls) {
            SameRoom = sameRoom;
            FirstDoor = firstDoor;
            AfterDoor = afterDoor;
            PathFromDoor = pathFromDoor;
            Walls = walls;
        }

        public static GraphRoute Same => new GraphRoute(true, Vector3.zero, Vector3.zero, 0f, 0);
    }
}
