using MissedCall.Core;
using MissedCall.Core.Round;
using UnityEngine;

namespace MissedCall.Feature.Layout {
    /// <summary>판 준비 1단계: 집을 조립하고 <see cref="RoundContext.Graph"/>를 채운다.</summary>
    public sealed class LayoutStep : RoundStep {
        [SerializeField] private RoomCatalog catalog;
        [SerializeField] private GameTuning tuning;
        [Tooltip("방 모듈을 둘 부모. 비우면 이 오브젝트")]
        [SerializeField] private Transform root;

        private HouseBuilder builder;

        /// <summary>마지막으로 고른 템플릿 (디버그 표시용).</summary>
        public LayoutTemplate LastTemplate => builder?.LastTemplate;

        public override void Prepare(RoundContext context) {
            builder ??= new HouseBuilder(catalog, root != null ? root : transform);
            context.Graph = builder.Build(context.Seed.Stream(RoundStream.Layout), tuning);
            if (context.Graph == null) {
                Debug.LogError($"[LayoutStep] Could not build a house for seed {context.Seed}.");
                return;
            }

            // 방금 옮긴 모듈의 콜라이더를 이후 단계(시작 위치, 물건 배치)의 물리 질의에 반영한다.
            Physics.SyncTransforms();
        }
    }
}
