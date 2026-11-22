using MissedCall.Core.Rooms;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Vacuum {
    /// <summary>
    /// 판 준비: 로봇청소기 변수면 폰이 있던 방 바닥의 빈 자리에 청소기를 두고 폰을 그 위에 얹는다
    /// (<see cref="RoundContext.PhoneMount"/>). 폰과 변수가 정해진 뒤에 둔다 (Modifiers 다음).
    /// 빈 자리를 못 찾으면 변수를 없던 것으로 한다. 무작위는 <see cref="RoundStream.Vacuum"/>에서만 뽑는다.
    /// </summary>
    public sealed class VacuumStep : RoundStep {
        [SerializeField] private RobotVacuum vacuum;
        [Tooltip("자리를 찾을 때 피할 벽과 가구 레이어")]
        [SerializeField] private LayerMask blockers = 1;
        [Tooltip("청소기 둘레에 비워 둘 반지름(m)")]
        [SerializeField] private float clearance = 0.22f;
        [SerializeField] private int attempts = 60;

        public RobotVacuum Vacuum => vacuum;

        public override void Prepare(RoundContext context) {
            vacuum.Hide();
            if (context.Modifier != RoundModifier.Vacuum) {
                return;
            }

            System.Random rng = context.Seed.Stream(RoundStream.Vacuum);
            RoomGraph graph = context.Graph;
            int room = context.PhoneRoom;
            if (graph == null || room < 0 || !FindFloor(graph, room, rng, out Vector3 floor)) {
                Debug.LogWarning("[VacuumStep] No free floor for the vacuum, dropping the modifier", this);
                context.Modifier = RoundModifier.None;
                return;
            }

            vacuum.Place(context, floor, (float)rng.NextDouble() * 360f, rng);
            context.PhoneMount = vacuum.Mount;
            context.PhoneSpot = null;
            context.PhonePosition = vacuum.Mount.position;
            context.PhoneRoom = room;
        }

        // 방 안쪽에서 무작위 점을 골라 둘레에 벽이나 가구가 없는 바닥을 찾는다.
        private bool FindFloor(RoomGraph graph, int room, System.Random rng, out Vector3 floor) {
            Rect footprint = graph.Footprint(room);
            float inset = 0.35f + clearance;
            for (int i = 0; i < attempts; i++) {
                float x = Mathf.Lerp(footprint.xMin + inset, footprint.xMax - inset, (float)rng.NextDouble());
                float z = Mathf.Lerp(footprint.yMin + inset, footprint.yMax - inset, (float)rng.NextDouble());
                var point = new Vector3(x, 0f, z);
                if (graph.RoomAt(point) != room) {
                    continue;
                }

                // 바닥 위 3cm(러그, 문턱 위)부터 청소기와 폰 높이까지 비어 있어야 한다.
                var half = new Vector3(clearance, 0.1f, clearance);
                if (Physics.CheckBox(point + Vector3.up * 0.13f, half, Quaternion.identity, blockers, QueryTriggerInteraction.Ignore)) {
                    continue;
                }

                floor = point;
                return true;
            }

            floor = Vector3.zero;
            return false;
        }
    }
}
