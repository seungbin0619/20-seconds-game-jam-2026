using System.Collections.Generic;
using UnityEngine;

namespace MissedCall.Core.Rooms {
    /// <summary>
    /// 한 판의 집 구조. 방을 노드, 열린 문을 간선으로 둔다. 소리 경로는 레이캐스트 대신 이 그래프로 계산한다.
    /// 집을 만들 때(Layout) 한 번 만들고 판 동안 바뀌지 않는다.
    /// </summary>
    public sealed class RoomGraph {
        private readonly RoomModule[] rooms;
        private readonly Rect[] footprints;
        private readonly RoomDoor[] doors;

        // Route 계산용 작업 버퍼. 매 프레임 호출되므로 할당하지 않는다.
        private float[] cost = new float[0];
        private int[] previous = new int[0];
        private bool[] done = new bool[0];
        private Vector3[] points = new Vector3[0];

        public RoomGraph(RoomModule[] rooms, Rect[] footprints, RoomDoor[] doors) {
            this.rooms = rooms;
            this.footprints = footprints;
            this.doors = doors;
        }

        public IReadOnlyList<RoomModule> Rooms => rooms;
        public IReadOnlyList<RoomDoor> Doors => doors;

        /// <summary>월드 XZ 범위 (벽 포함 바깥 치수).</summary>
        public Rect Footprint(int room) => footprints[room];

        /// <summary>위치가 속한 방. 집 밖이면 -1. 맞닿은 벽 안(문턱)도 어느 한 방에 속한다.</summary>
        public int RoomAt(Vector3 position) {
            var point = new Vector2(position.x, position.z);
            for (int i = 0; i < footprints.Length; i++) {
                if (footprints[i].Contains(point)) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>모든 방이 문으로 이어져 있는지.</summary>
        public bool IsConnected() {
            if (rooms.Length == 0) {
                return true;
            }

            var seen = new bool[rooms.Length];
            var stack = new Stack<int>();
            stack.Push(0);
            seen[0] = true;
            int count = 1;
            while (stack.Count > 0) {
                int room = stack.Pop();
                foreach (RoomDoor door in doors) {
                    if (!door.Touches(room)) {
                        continue;
                    }

                    int other = door.RoomA == room ? door.RoomB : door.RoomA;
                    if (!seen[other]) {
                        seen[other] = true;
                        count++;
                        stack.Push(other);
                    }
                }
            }

            return count == rooms.Length;
        }

        /// <summary>
        /// 듣는 사람에서 음원까지 문을 거치는 최단 경로. 노드는 듣는 사람, 문들, 음원이고
        /// 같은 방에 있는 두 점 사이를 직선으로 잇는다. 이어지지 않으면 같은 방으로 취급한다.
        /// </summary>
        public GraphRoute Route(Vector3 listener, int listenerRoom, Vector3 source, int sourceRoom) {
            if (listenerRoom < 0 || sourceRoom < 0 || listenerRoom == sourceRoom) {
                return GraphRoute.Same;
            }

            int n = doors.Length + 2;
            EnsureBuffers(n);
            int start = 0;
            int goal = n - 1;
            points[start] = listener;
            points[goal] = source;
            for (int i = 0; i < doors.Length; i++) {
                points[i + 1] = doors[i].Position;
            }

            for (int i = 0; i < n; i++) {
                cost[i] = float.PositiveInfinity;
                previous[i] = -1;
                done[i] = false;
            }

            cost[start] = 0f;
            for (int step = 0; step < n; step++) {
                int current = -1;
                for (int i = 0; i < n; i++) {
                    if (!done[i] && (current < 0 || cost[i] < cost[current])) {
                        current = i;
                    }
                }

                if (current < 0 || float.IsPositiveInfinity(cost[current]) || current == goal) {
                    break;
                }

                done[current] = true;
                for (int next = 0; next < n; next++) {
                    if (done[next] || !ShareRoom(current, next, listenerRoom, sourceRoom, n)) {
                        continue;
                    }

                    float candidate = cost[current] + Vector3.Distance(points[current], points[next]);
                    if (candidate < cost[next]) {
                        cost[next] = candidate;
                        previous[next] = current;
                    }
                }
            }

            if (previous[goal] < 0) {
                return GraphRoute.Same;
            }

            // 경로를 거꾸로 따라가 첫 문과 그 다음 지점을 찾는다. 거쳐 간 문 노드 수가 벽 수다.
            int walls = 0;
            int after = goal;
            int node = goal;
            while (previous[node] != start) {
                after = node;
                node = previous[node];
                walls++;
            }

            int first = node;
            float fromDoor = cost[goal] - cost[first];
            return new GraphRoute(false, points[first], points[after], fromDoor, walls);
        }

        private bool ShareRoom(int a, int b, int listenerRoom, int sourceRoom, int n) {
            if (a == b) {
                return false;
            }

            if (a > b) {
                (a, b) = (b, a);
            }

            int last = n - 1;
            if (a == 0 && b == last) {
                return false; // 같은 방이 아님은 호출 전에 확인했다.
            }

            if (a == 0) {
                return doors[b - 1].Touches(listenerRoom);
            }

            if (b == last) {
                return doors[a - 1].Touches(sourceRoom);
            }

            RoomDoor da = doors[a - 1];
            RoomDoor db = doors[b - 1];
            return db.Touches(da.RoomA) || db.Touches(da.RoomB);
        }

        private void EnsureBuffers(int n) {
            if (cost.Length >= n) {
                return;
            }

            cost = new float[n];
            previous = new int[n];
            done = new bool[n];
            points = new Vector3[n];
        }
    }
}
