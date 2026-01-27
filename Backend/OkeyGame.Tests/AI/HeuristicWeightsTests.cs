using OkeyGame.Domain.AI;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using Xunit;

namespace OkeyGame.Tests.AI;

/// <summary>
/// HeuristicWeights testleri.
/// </summary>
public class HeuristicWeightsTests
{
    [Theory]
    [InlineData(BotDifficulty.Easy)]
    [InlineData(BotDifficulty.Normal)]
    [InlineData(BotDifficulty.Hard)]
    [InlineData(BotDifficulty.Expert)]
    public void ForDifficulty_ShouldReturnValidWeights(BotDifficulty difficulty)
    {
        // Act
        var weights = HeuristicWeights.ForDifficulty(difficulty);

        // Assert
        Assert.NotNull(weights);
        Assert.True(weights.OkeyTile > 0);
        Assert.True(weights.CompletedMeld > 0);
        Assert.True(weights.AdjacentPair > 0);
    }

    [Fact]
    public void Expert_ShouldHaveHigherWeights()
    {
        // Arrange
        var easy = HeuristicWeights.Easy;
        var expert = HeuristicWeights.Expert;

        // Assert
        Assert.True(expert.OkeyTile > easy.OkeyTile);
        Assert.True(expert.CompletedMeld > easy.CompletedMeld);
        Assert.True(Math.Abs(expert.BothCopiesSeen) > Math.Abs(easy.BothCopiesSeen));
    }

    [Fact]
    public void Normal_ShouldBeBalanced()
    {
        // Arrange
        var normal = HeuristicWeights.Normal;

        // Assert
        Assert.True(normal.OkeyTile >= 10);
        Assert.True(normal.IsolatedTile < normal.AdjacentPair);
        Assert.True(normal.AdjacentPair < normal.CompletedMeld);
    }
}
