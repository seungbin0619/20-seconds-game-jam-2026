using MissedCall.Core;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Cat {
    /// <summary>
    /// 판 준비: 낮은 확률로 고양이를 폰 더미 맨 위에 앉힌다. 물건 안정화와 폰 놓기 뒤에 둔다.
    /// 판 시드의 Extras 스트림을 쓴다 (첫 번째 값은 플립폰 몫이라 건너뛴다).
    /// </summary>
    public sealed class CatStep : RoundStep {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private Cat prefab;
        [Tooltip("고양이가 앉을 윗면을 찾을 레이어 (물건, 가구)")]
        [SerializeField] private LayerMask surfaceMask;
        [Tooltip("확인용: 매 판 고양이를 앉힌다")]
        [SerializeField] private bool force;

        private Cat cat;

        public Cat Instance => cat;

        public override void Prepare(RoundContext context) {
            if (cat == null) {
                cat = Instantiate(prefab, transform);
            }

            cat.Hide();
            if (context.Graph == null || context.PhoneRoom < 0) {
                return;
            }

            System.Random rng = context.Seed.Stream(RoundStream.Extras);
            rng.NextDouble();
            if (!force && rng.NextDouble() >= tuning.catChance) {
                return;
            }

            // 폰 바로 위에서 내려 쏴 더미 맨 윗면을 찾는다.
            Vector3 phone = context.PhonePosition;
            Vector3 top = phone;
            if (Physics.Raycast(phone + Vector3.up * 1.6f, Vector3.down, out RaycastHit hit, 1.7f, surfaceMask,
                    QueryTriggerInteraction.Ignore)) {
                top = hit.point;
            }

            float yaw = (float)rng.NextDouble() * 360f;
            cat.Sit(top, yaw, context.Graph.Footprint(context.PhoneRoom), rng);
        }
    }
}
