namespace MissedCall.Core.Round {
    /// <summary>
    /// 판 시드. 판의 모든 무작위는 이 값 하나에서 용도별 스트림으로 갈라 뽑는다.
    /// 용도마다 스트림을 나누므로 한 기능에서 뽑는 횟수가 바뀌어도 다른 기능의 결과는 그대로다.
    /// </summary>
    public readonly struct RoundSeed {
        public readonly int Value;

        public RoundSeed(int value) {
            Value = value;
        }

        /// <summary>용도별 난수 생성기. 같은 시드와 용도면 항상 같은 수열을 낸다.</summary>
        public System.Random Stream(RoundStream stream) => new System.Random(Mix(Value, (int)stream + 1));

        public override string ToString() => Value.ToString();

        // System.Random(int)는 음수를 절댓값으로 바꾸므로 그대로 넘겨도 된다.
        // string.GetHashCode는 실행마다 달라질 수 있어 쓰지 않는다.
        internal static int Mix(int seed, int salt) {
            unchecked {
                uint h = (uint)seed * 0x9E3779B9u ^ (uint)salt * 0x85EBCA6Bu;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (int)h;
            }
        }
    }
}
