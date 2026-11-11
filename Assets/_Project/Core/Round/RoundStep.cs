using UnityEngine;

namespace MissedCall.Core.Round {
    /// <summary>
    /// 판 준비 단계. Feature가 상속해 집 생성, 물건 배치, 폰 배치 같은 준비를 맡는다.
    /// <see cref="GameFlow"/>가 인스펙터에 놓인 순서대로 호출한다.
    /// 이전 판의 오브젝트는 여기서 풀에 돌려놓고 다시 쓴다. 이전 판의 상태를 읽어 결과에 반영하지 않는다.
    /// </summary>
    public abstract class RoundStep : MonoBehaviour {
        /// <summary>새 판을 준비한다. 무작위는 <c>context.Seed.Stream(...)</c>에서만 뽑는다.</summary>
        public abstract void Prepare(RoundContext context);
    }
}
