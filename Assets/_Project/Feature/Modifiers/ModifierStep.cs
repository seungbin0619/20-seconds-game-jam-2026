using System.Collections.Generic;
using MissedCall.Core;
using MissedCall.Core.Rooms;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Modifiers {
    /// <summary>
    /// 판 준비: 판 시드의 Modifier 스트림으로 판 변수(0~1개)를 고르고 값을 정한다. 폰 위치가 필요하므로 Phone 다음에 둔다.
    /// 소리와 화면은 각 기능(Ringtone, Phone, ScreenFx, Ui)이 <see cref="RoundContext.Modifier"/>를 읽어 처리한다.
    /// </summary>
    public sealed class ModifierStep : RoundStep {
        // 소음원(천장 환풍기) 높이(m)
        private const float NoiseHeight = 2.2f;

        [SerializeField] private GameTuning tuning;
        [Tooltip("0이 아니면 매 판 이 변수를 쓴다. 확인용")]
        [SerializeField] private RoundModifier force;

        private readonly List<(Vector3 position, int room)> candidates = new();

        public override void Prepare(RoundContext context) {
            // 시간대는 따로 뽑아 변수 고르기에 영향을 주지 않는다.
            context.Mood = context.Seed.Stream(RoundStream.Mood).Next(3);
            context.FlipPhone = context.Seed.Stream(RoundStream.Extras).NextDouble() < tuning.flipPhoneChance;

            System.Random rng = context.Seed.Stream(RoundStream.Modifier);
            bool roll = rng.NextDouble() < tuning.modifierChance;
            RoundModifier pick = Pick(rng);
            RoundModifier modifier = force != RoundModifier.None ? force : roll ? pick : RoundModifier.None;
            context.Modifier = RoundModifier.None;

            switch (modifier) {
                case RoundModifier.LowBattery:
                    context.RingStopTime = Mathf.Lerp(tuning.batteryRingTime.x, tuning.batteryRingTime.y, (float)rng.NextDouble());
                    context.Modifier = modifier;
                    break;
                case RoundModifier.Decoy:
                    if (PlaceDecoy(context, rng)) {
                        context.Modifier = modifier;
                    }

                    break;
                case RoundModifier.Noise:
                    if (PlaceNoise(context, rng)) {
                        context.Modifier = modifier;
                    }

                    break;
                default:
                    context.Modifier = modifier;
                    break;
            }

            if (context.Modifier != RoundModifier.None) {
                Debug.Log($"[ModifierStep] {context.Modifier}");
            }
        }

        private RoundModifier Pick(System.Random rng) {
            float[] weights = {
                tuning.vibrateWeight, tuning.lowBatteryWeight, tuning.decoyWeight, tuning.noiseWeight, tuning.blackoutWeight
            };
            RoundModifier[] kinds = {
                RoundModifier.Vibrate, RoundModifier.LowBattery, RoundModifier.Decoy, RoundModifier.Noise, RoundModifier.Blackout
            };
            float total = 0f;
            foreach (float w in weights) {
                total += Mathf.Max(0f, w);
            }

            float value = (float)rng.NextDouble() * total;
            for (int i = 0; i < kinds.Length; i++) {
                value -= Mathf.Max(0f, weights[i]);
                if (value < 0f) {
                    return kinds[i];
                }
            }

            return RoundModifier.None;
        }

        // 폰에서 멀리 떨어진 숨는 자리 하나 (그 자리에도 물건 더미가 있어 그럴듯하다).
        private bool PlaceDecoy(RoundContext context, System.Random rng) {
            RoomGraph graph = context.Graph;
            if (graph == null) {
                return false;
            }

            candidates.Clear();
            for (int room = 0; room < graph.Rooms.Count; room++) {
                foreach (HideSpot spot in graph.Rooms[room].HideSpots) {
                    Vector3 p = spot.transform.position;
                    if (Vector3.Distance(p, context.PhonePosition) >= tuning.decoyMinDistance) {
                        candidates.Add((p + Vector3.up * 0.05f, room));
                    }
                }
            }

            if (candidates.Count == 0) {
                return false;
            }

            (Vector3 position, int room) chosen = candidates[rng.Next(candidates.Count)];
            context.DecoyPosition = chosen.position;
            context.DecoyRoom = chosen.room;
            return true;
        }

        // 소음원은 폰이 없는 방 천장 (방이 하나뿐이면 그 방).
        private bool PlaceNoise(RoundContext context, System.Random rng) {
            RoomGraph graph = context.Graph;
            if (graph == null || graph.Rooms.Count == 0) {
                return false;
            }

            var rooms = new List<int>();
            for (int room = 0; room < graph.Rooms.Count; room++) {
                if (room != context.PhoneRoom) {
                    rooms.Add(room);
                }
            }

            int chosen = rooms.Count > 0 ? rooms[rng.Next(rooms.Count)] : 0;
            Rect footprint = graph.Footprint(chosen);
            context.NoisePosition = new Vector3(footprint.center.x, NoiseHeight, footprint.center.y);
            context.NoiseRoom = chosen;
            return true;
        }
    }
}
