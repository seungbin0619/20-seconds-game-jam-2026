using MissedCall.Core.Round;
using NUnit.Framework;

namespace MissedCall.Core.Tests {
    public sealed class RoundSeedTests {
        [Test]
        public void SameSeedAndStreamGiveSameSequence() {
            var a = new RoundSeed(1234).Stream(RoundStream.Layout);
            var b = new RoundSeed(1234).Stream(RoundStream.Layout);
            for (int i = 0; i < 100; i++) {
                Assert.AreEqual(a.Next(), b.Next());
            }
        }

        [Test]
        public void StreamsAreIndependent() {
            var seed = new RoundSeed(1234);
            var layout = seed.Stream(RoundStream.Layout);
            var phone = seed.Stream(RoundStream.Phone);
            int same = 0;
            for (int i = 0; i < 100; i++) {
                if (layout.Next(1000) == phone.Next(1000)) {
                    same++;
                }
            }

            Assert.Less(same, 5);
        }

        [Test]
        public void NeighbourSeedsDiffer() {
            int first = new RoundSeed(1).Stream(RoundStream.Layout).Next();
            int second = new RoundSeed(2).Stream(RoundStream.Layout).Next();
            Assert.AreNotEqual(first, second);
        }
    }
}
