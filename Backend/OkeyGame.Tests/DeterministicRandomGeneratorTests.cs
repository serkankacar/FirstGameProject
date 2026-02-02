using OkeyGame.Domain.Services;
using Xunit;

namespace OkeyGame.Tests;

public class DeterministicRandomGeneratorTests
{
    [Fact]
    public void Should_Produce_Same_Sequence_With_Same_Seed()
    {
        var seed = "test-seed-123";
        var rng1 = new DeterministicRandomGenerator(seed);
        var rng2 = new DeterministicRandomGenerator(seed);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(rng1.NextInt(1000), rng2.NextInt(1000));
        }
    }

    [Fact]
    public void Should_Produce_Different_Sequence_With_Different_Seeds()
    {
        var rng1 = new DeterministicRandomGenerator("seed1");
        var rng2 = new DeterministicRandomGenerator("seed2");

        bool allSame = true;
        for (int i = 0; i < 100; i++)
        {
            if (rng1.NextInt(1000) != rng2.NextInt(1000))
            {
                allSame = false;
                break;
            }
        }
        Assert.False(allSame);
    }
}
