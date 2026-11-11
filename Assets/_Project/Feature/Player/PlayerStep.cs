using System.Collections.Generic;
using MissedCall.Core.Rooms;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Player {
    /// <summary>
    /// 판 준비 단계: 폰이 없는 방 중 한 곳에서 가구에 끼지 않는 시작 위치를 고르고,
    /// 그 방의 문 하나를 바라보게 세운다. 폰 배치(Phone) 다음에 둔다. 폰 방을 모르면 아무 방에서나 시작한다.
    /// </summary>
    public sealed class PlayerStep : RoundStep {
        [SerializeField] private PlayerMotor motor;
        [Tooltip("시작 위치를 막는 콜라이더 레이어")]
        [SerializeField] private LayerMask blockers = ~0;
        [Tooltip("시작 시 내려다보는 각도(도). +가 아래")]
        [SerializeField] private float startPitch = 8f;
        [Tooltip("방 바깥 치수에서 안쪽으로 이만큼 떨어진 곳에서만 시작 위치를 찾는다(m). 벽 두께 포함")]
        [SerializeField] private float wallMargin = 0.5f;
        [SerializeField] private int attempts = 32;

        private readonly List<int> rooms = new();

        public override void Prepare(RoundContext context) {
            System.Random rng = context.Seed.Stream(RoundStream.Start);
            RoomGraph graph = context.Graph;
            if (graph == null) {
                motor.Teleport(Vector3.zero, 0f, startPitch);
                return;
            }

            rooms.Clear();
            for (int i = 0; i < graph.Rooms.Count; i++) {
                if (i != context.PhoneRoom) {
                    rooms.Add(i);
                }
            }

            int room = rooms[rng.Next(rooms.Count)];
            Vector3 feet = FindFreeSpot(graph.Footprint(room), rng);
            float yaw = FaceDoor(graph, room, feet, rng);
            motor.Teleport(feet, yaw, startPitch);
        }

        // 방 가운데를 먼저 보고, 막혀 있으면 방 안의 무작위 지점을 차례로 본다.
        private Vector3 FindFreeSpot(Rect footprint, System.Random rng) {
            var controller = motor.GetComponent<CharacterController>();
            Rect inner = Rect.MinMaxRect(footprint.xMin + wallMargin, footprint.yMin + wallMargin,
                footprint.xMax - wallMargin, footprint.yMax - wallMargin);
            Vector3 center = new(inner.center.x, 0f, inner.center.y);

            // 플레이어 자신의 콜라이더가 검사에 걸리지 않게 잠시 끈다.
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try {
                if (IsFree(center, controller)) {
                    return center;
                }

                for (int i = 0; i < attempts; i++) {
                    float x = Mathf.Lerp(inner.xMin, inner.xMax, (float)rng.NextDouble());
                    float z = Mathf.Lerp(inner.yMin, inner.yMax, (float)rng.NextDouble());
                    Vector3 candidate = new(x, 0f, z);
                    if (IsFree(candidate, controller)) {
                        return candidate;
                    }
                }
            } finally {
                controller.enabled = wasEnabled;
            }

            Debug.LogWarning("[PlayerStep] No free start spot found. Starting at room center.");
            return center;
        }

        private bool IsFree(Vector3 feet, CharacterController controller) {
            float radius = controller.radius;
            // 바닥과 닿지 않게 살짝 띄워서 검사한다.
            Vector3 bottom = feet + Vector3.up * (radius + 0.05f);
            Vector3 top = feet + Vector3.up * (controller.height - radius);
            return !Physics.CheckCapsule(bottom, top, radius, blockers, QueryTriggerInteraction.Ignore);
        }

        private static float FaceDoor(RoomGraph graph, int room, Vector3 feet, System.Random rng) {
            int count = 0;
            foreach (RoomDoor door in graph.Doors) {
                if (door.Touches(room)) {
                    count++;
                }
            }

            if (count == 0) {
                return (float)rng.NextDouble() * 360f;
            }

            int pick = rng.Next(count);
            foreach (RoomDoor door in graph.Doors) {
                if (!door.Touches(room)) {
                    continue;
                }

                if (pick-- == 0) {
                    Vector3 to = door.Position - feet;
                    return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                }
            }

            return 0f;
        }
    }
}
