using MissedCall.Core;
using MissedCall.Core.Rooms;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Vacuum {
    /// <summary>
    /// 폰을 싣고 집 안을 돌아다니는 로봇청소기 (로봇청소기 변수). 키네마틱 강체라 지나가며 바닥의 물건을 밀어낸다.
    /// 방 안에서는 앞이 막힐 때마다 제자리에서 돌아 다른 방향으로 가고(vacuumRoamTime 동안), 그다음 지금 방의 문 하나를 골라
    /// 문 앞 → 문 너머 두 지점을 차례로 찾아가 옆방으로 넘어간다. 가는 길에 부딪히면 옆으로 비켜 조금 간 뒤 다시 찾아간다.
    /// 원점은 바닥이고, <see cref="Mount"/>가 폰을 얹는 윗면이다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RobotVacuum : MonoBehaviour {
        // 문 앞 지점은 벽에서 이만큼(m) 안쪽, 문 너머 지점은 옆방으로 이만큼 들어간 곳
        private const float DoorFront = 0.45f;
        private const float DoorBack = 0.75f;
        // 지점에 이만큼(m) 가까워지면 도착으로 본다.
        private const float ArriveDistance = 0.18f;
        // 문 찾기를 포기하기까지(초)
        private const float SeekTimeout = 9f;
        // 부딪혔을 때 옆으로 비켜 가는 거리(m)
        private const float DetourDistance = 0.35f;
        // 바닥 위 이 높이(m)보다 낮은 것(러그, 문턱)은 막는 것으로 보지 않는다.
        private const float FloorSkip = 0.03f;

        [SerializeField] private GameTuning tuning;
        [Tooltip("폰을 얹는 윗면. 폰 원점이 여기에 놓인다")]
        [SerializeField] private Transform mount;
        [Tooltip("몸체 반지름(m). 앞이 막혔는지 볼 때 쓴다")]
        [SerializeField] private float radius = 0.17f;
        [Tooltip("벽과 가구 레이어. 물건과 플레이어는 뺀다 (물건은 밀고 지나간다)")]
        [SerializeField] private LayerMask blockers = 1;
        [Tooltip("돌아가는 동안 도는 옆 솔")]
        [SerializeField] private Transform brush;

        private readonly Vector3[] waypoints = new Vector3[2];
        private Rigidbody body;
        private RoundContext context;
        private System.Random rng;
        private float yaw;
        private float targetYaw;
        private bool turning;
        // 찾아가는 지점 번호. waypoints.Length면 찾아가는 중이 아니다.
        private int waypoint;
        private float seekUntil;
        private float detourLeft;
        private float nextRoam;

        public Transform Mount => mount;

        private bool Seeking => waypoint < waypoints.Length;

        /// <summary>판 동안 움직인 거리(m)와 지나간 방 수, 부딪힌 횟수. 확인용.</summary>
        public float Traveled { get; private set; }
        public int RoomsVisited { get; private set; }
        public int Bumps { get; private set; }

        private void Awake() {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            waypoint = waypoints.Length;
        }

        /// <summary>판에 놓는다. 위치는 바닥, 회전은 처음 가는 방향이다.</summary>
        public void Place(RoundContext round, Vector3 floor, float startYaw, System.Random random) {
            context = round;
            rng = random;
            yaw = startYaw;
            turning = false;
            waypoint = waypoints.Length;
            detourLeft = 0f;
            nextRoam = tuning.vacuumRoamTime * 0.5f;
            Traveled = 0f;
            RoomsVisited = 1;
            Bumps = 0;
            gameObject.SetActive(false);
            transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
            gameObject.SetActive(true);
            Physics.SyncTransforms();
            Report();
        }

        public void Hide() {
            context = null;
            gameObject.SetActive(false);
        }

        private void FixedUpdate() {
            if (context == null || context.State != RoundState.Playing) {
                return;
            }

            float dt = Time.fixedDeltaTime;
            if (brush != null) {
                brush.Rotate(0f, 600f * dt, 0f, Space.Self);
            }

            if (!Seeking && context.Elapsed >= nextRoam) {
                PickDoor();
            } else if (Seeking && context.Elapsed >= seekUntil) {
                StopSeeking();
            }

            if (Seeking && detourLeft <= 0f && !turning) {
                Steer(dt);
            }

            if (turning) {
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, tuning.vacuumTurnSpeed * dt);
                if (Mathf.Approximately(Mathf.DeltaAngle(yaw, targetYaw), 0f)) {
                    turning = false;
                }

                body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
                Report();
                return;
            }

            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float step = tuning.vacuumSpeed * dt;
            if (Blocked(forward, step)) {
                Bumps++;
                if (Seeking) {
                    // 옆으로 비켜 조금 간 뒤 다시 지점을 찾아간다.
                    TurnTo(yaw + (rng.Next(2) == 0 ? -1f : 1f) * (60f + (float)rng.NextDouble() * 40f));
                    detourLeft = DetourDistance;
                } else {
                    // 반대쪽 반원 중 한 방향
                    TurnTo(yaw + 180f + Range(80f));
                }

                return;
            }

            body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
            body.MovePosition(body.position + forward * step);
            Traveled += step;
            detourLeft -= step;
            Report();
        }

        // 지금 지점 쪽으로 방향을 잡는다. 크게 틀어야 하면 제자리에서 돌고, 조금이면 가면서 튼다.
        private void Steer(float dt) {
            Vector3 toGoal = waypoints[waypoint] - body.position;
            toGoal.y = 0f;
            if (toGoal.magnitude < ArriveDistance) {
                waypoint++;
                if (!Seeking) {
                    StopSeeking();
                }

                return;
            }

            float wanted = Mathf.Atan2(toGoal.x, toGoal.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, wanted)) > 30f) {
                TurnTo(wanted);
            } else {
                yaw = Mathf.MoveTowardsAngle(yaw, wanted, tuning.vacuumTurnSpeed * 0.5f * dt);
            }
        }

        private void StopSeeking() {
            waypoint = waypoints.Length;
            detourLeft = 0f;
            nextRoam = context.Elapsed + tuning.vacuumRoamTime;
        }

        private bool Blocked(Vector3 forward, float step) {
            // 몸 폭의 얇은 상자를 앞으로 민다. 바닥 위 FloorSkip 아래(러그, 문턱)는 무시한다.
            // 상자는 몸 앞쪽 끝에서 출발하므로 자기 콜라이더와 겹쳐 시작하고, 겹쳐 시작한 콜라이더는 맞지 않는다.
            Quaternion rotation = Quaternion.LookRotation(forward);
            var half = new Vector3(radius * 0.95f, 0.025f, 0.02f);
            Vector3 origin = body.position + Vector3.up * (FloorSkip + half.y) + forward * (radius - half.z);
            if (Physics.BoxCast(origin, half, forward, rotation, step + 0.03f, blockers, QueryTriggerInteraction.Ignore)) {
                return true;
            }

            // 집 밖으로 나가지 않는다.
            Vector3 next = body.position + forward * (radius + step);
            return context.Graph != null && context.Graph.RoomAt(next) < 0;
        }

        // 지금 방에 닿은 문 중 하나를 골라 문 앞과 문 너머를 지점으로 잡는다. 방향은 문이 놓인 벽에 수직이다.
        private void PickDoor() {
            nextRoam = context.Elapsed + tuning.vacuumRoamTime;
            RoomGraph graph = context.Graph;
            int room = graph != null ? graph.RoomAt(body.position) : -1;
            if (room < 0) {
                return;
            }

            int count = 0;
            foreach (RoomDoor door in graph.Doors) {
                if (door.Touches(room)) {
                    count++;
                }
            }

            if (count == 0) {
                return;
            }

            int pick = rng.Next(count);
            foreach (RoomDoor door in graph.Doors) {
                if (!door.Touches(room) || pick-- > 0) {
                    continue;
                }

                Vector3 doorFloor = new(door.Position.x, 0f, door.Position.z);
                Vector3 outward = WallNormal(graph.Footprint(room), doorFloor);
                waypoints[0] = doorFloor - outward * DoorFront;
                waypoints[1] = doorFloor + outward * DoorBack;
                waypoint = 0;
                detourLeft = 0f;
                seekUntil = context.Elapsed + SeekTimeout;
                return;
            }
        }

        // 방 범위에서 문과 가장 가까운 변의 바깥 방향.
        private static Vector3 WallNormal(Rect footprint, Vector3 door) {
            float left = Mathf.Abs(door.x - footprint.xMin);
            float right = Mathf.Abs(door.x - footprint.xMax);
            float bottom = Mathf.Abs(door.z - footprint.yMin);
            float top = Mathf.Abs(door.z - footprint.yMax);
            float best = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
            if (Mathf.Approximately(best, left)) {
                return Vector3.left;
            }

            if (Mathf.Approximately(best, right)) {
                return Vector3.right;
            }

            return Mathf.Approximately(best, bottom) ? Vector3.back : Vector3.forward;
        }

        private void TurnTo(float yawDegrees) {
            targetYaw = yawDegrees;
            turning = true;
        }

        private void Report() {
            Vector3 p = body.position;
            context.VacuumPosition = p + Vector3.up * 0.08f;
            if (context.Graph != null) {
                int room = context.Graph.RoomAt(p);
                if (room >= 0) {
                    if (room != context.VacuumRoom && context.VacuumRoom >= 0) {
                        RoomsVisited++;
                    }

                    context.VacuumRoom = room;
                }
            }
        }

        private float Range(float limit) => (float)(rng.NextDouble() * 2.0 - 1.0) * limit;
    }
}
