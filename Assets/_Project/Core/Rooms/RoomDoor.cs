using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>두 방을 잇는 열린 문. 방 그래프의 간선이다.</summary>
    public readonly struct RoomDoor {
        public readonly int RoomA;
        public readonly int RoomB;
        /// <summary>문 중심 (벽 두께 가운데, 소리 경로 높이).</summary>
        public readonly Vector3 Position;
        public readonly DoorSocket SocketA;
        public readonly DoorSocket SocketB;

        public RoomDoor(int roomA, int roomB, Vector3 position, DoorSocket socketA, DoorSocket socketB) {
            RoomA = roomA;
            RoomB = roomB;
            Position = position;
            SocketA = socketA;
            SocketB = socketB;
        }

        public bool Touches(int room) => RoomA == room || RoomB == room;
    }
}
