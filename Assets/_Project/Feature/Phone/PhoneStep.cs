using MissedCall.Core;
using MissedCall.Core.Rooms;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Phone {
    /// <summary>
    /// 판 준비 단계: 집의 숨는 자리 중 한 곳을 골라 맨 아래(자리 표면)에 폰을 둔다.
    /// Clutter 다음에 둔다. 더미가 가라앉은 뒤 놓는 논리 오브젝트라 물리 콜라이더가 없다.
    /// 무작위는 <see cref="RoundStream.Phone"/>에서만 뽑는다.
    /// </summary>
    public sealed class PhoneStep : RoundStep {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private PhoneDevice device;

        /// <summary>이번 판에 고른 숨는 자리. 확인용.</summary>
        public HideSpot Spot { get; private set; }

        public PhoneDevice Device => device;

        public override void Prepare(RoundContext context) {
            Spot = null;
            device.Hide();

            RoomGraph graph = context.Graph;
            if (graph == null) {
                return;
            }

            int total = 0;
            for (int room = 0; room < graph.Rooms.Count; room++) {
                total += graph.Rooms[room].HideSpots.Length;
            }

            if (total == 0) {
                Debug.LogError("[PhoneStep] The house has no hide spot", this);
                return;
            }

            System.Random rng = context.Seed.Stream(RoundStream.Phone);
            int pick = rng.Next(total);
            int phoneRoom = -1;
            for (int room = 0; room < graph.Rooms.Count; room++) {
                HideSpot[] spots = graph.Rooms[room].HideSpots;
                if (pick < spots.Length) {
                    Spot = spots[pick];
                    phoneRoom = room;
                    break;
                }

                pick -= spots.Length;
            }

            Transform t = Spot.transform;
            Vector2 jitter = tuning.phoneSpotJitter;
            Vector3 offset = t.rotation * new Vector3(Range(rng, jitter.x), 0f, Range(rng, jitter.y));
            Quaternion rotation = t.rotation * Quaternion.Euler(0f, Range(rng, tuning.phoneYawJitter), 0f);
            device.Place(context, t.position + offset, rotation, phoneRoom);
        }

        private static float Range(System.Random rng, float limit) => (float)(rng.NextDouble() * 2.0 - 1.0) * limit;
    }
}
