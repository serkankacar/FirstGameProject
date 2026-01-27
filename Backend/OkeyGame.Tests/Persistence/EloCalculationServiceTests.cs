using OkeyGame.Application.Interfaces;
using OkeyGame.Domain.Enums;
using OkeyGame.Infrastructure.Services;
using Xunit;

namespace OkeyGame.Tests.Persistence;

/// <summary>
/// ELO hesaplama servisi testleri.
/// </summary>
public class EloCalculationServiceTests
{
    private readonly IEloCalculationService _service;

    public EloCalculationServiceTests()
    {
        _service = new EloCalculationService();
    }

    #region Calculate Testleri

    [Fact]
    public void Calculate_FourPlayers_ReturnsChangesForAll()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loser1Id = Guid.NewGuid();
        var loser2Id = Guid.NewGuid();
        var loser3Id = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { winnerId, 1200 },
            { loser1Id, 1200 },
            { loser2Id, 1200 },
            { loser3Id, 1200 }
        };

        // Act
        var result = _service.Calculate(winnerId, playerEloScores, WinType.Normal);

        // Assert
        Assert.Equal(4, result.EloChanges.Count);
        Assert.Equal(4, result.NewEloScores.Count);
        Assert.True(result.EloChanges[winnerId] > 0);
        Assert.True(result.EloChanges[loser1Id] < 0);
        Assert.True(result.EloChanges[loser2Id] < 0);
        Assert.True(result.EloChanges[loser3Id] < 0);
    }

    [Fact]
    public void Calculate_EqualElo_WinnerGainsLosersLose()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { winnerId, 1200 },
            { loserId, 1200 }
        };

        // Act
        var result = _service.Calculate(winnerId, playerEloScores, WinType.Normal);

        // Assert
        Assert.True(result.EloChanges[winnerId] > 0);
        Assert.True(result.EloChanges[loserId] < 0);
    }

    [Fact]
    public void Calculate_HigherEloWins_SmallerGain()
    {
        // Arrange
        var strongWinnerId = Guid.NewGuid();
        var weakLoserId = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { strongWinnerId, 1500 },
            { weakLoserId, 1000 }
        };

        // Act
        var result = _service.Calculate(strongWinnerId, playerEloScores, WinType.Normal);

        // Assert
        // Güçlü oyuncu zayıf rakibi yenince az kazanır
        Assert.True(result.EloChanges[strongWinnerId] < 10);
    }

    [Fact]
    public void Calculate_LowerEloWins_LargerGain()
    {
        // Arrange
        var weakWinnerId = Guid.NewGuid();
        var strongLoserId = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { weakWinnerId, 1000 },
            { strongLoserId, 1500 }
        };

        // Act
        var result = _service.Calculate(weakWinnerId, playerEloScores, WinType.Normal);

        // Assert
        // Zayıf oyuncu güçlü rakibi yenince çok kazanır
        Assert.True(result.EloChanges[weakWinnerId] > 10);
    }

    #endregion

    #region Win Type Çarpanı Testleri

    [Fact]
    public void Calculate_PairsWin_AppliesBonus()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { winnerId, 1200 },
            { loserId, 1200 }
        };

        // Act
        var normalResult = _service.Calculate(winnerId, playerEloScores, WinType.Normal);
        var pairsResult = _service.Calculate(winnerId, playerEloScores, WinType.Pairs);

        // Assert
        Assert.Equal(1.5, pairsResult.WinTypeMultiplier);
        Assert.True(pairsResult.EloChanges[winnerId] > normalResult.EloChanges[winnerId]);
    }

    [Fact]
    public void Calculate_OkeyDiscardWin_AppliesDoubleBonus()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { winnerId, 1200 },
            { loserId, 1200 }
        };

        // Act
        var result = _service.Calculate(winnerId, playerEloScores, WinType.OkeyDiscard);

        // Assert
        Assert.Equal(2.0, result.WinTypeMultiplier);
    }

    [Fact]
    public void Calculate_DeckEmptyWin_AppliesPenalty()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { winnerId, 1200 },
            { loserId, 1200 }
        };

        // Act
        var normalResult = _service.Calculate(winnerId, playerEloScores, WinType.Normal);
        var deckEmptyResult = _service.Calculate(winnerId, playerEloScores, WinType.DeckEmpty);

        // Assert
        Assert.Equal(0.5, deckEmptyResult.WinTypeMultiplier);
        Assert.True(deckEmptyResult.EloChanges[winnerId] < normalResult.EloChanges[winnerId]);
    }

    #endregion

    #region NewEloScores Testleri

    [Fact]
    public void Calculate_UpdatesNewEloScoresCorrectly()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { winnerId, 1200 },
            { loserId, 1200 }
        };

        // Act
        var result = _service.Calculate(winnerId, playerEloScores, WinType.Normal);

        // Assert
        Assert.Equal(1200 + result.EloChanges[winnerId], result.NewEloScores[winnerId]);
        Assert.Equal(1200 + result.EloChanges[loserId], result.NewEloScores[loserId]);
    }

    [Fact]
    public void Calculate_NewEloNeverBelowMinimum()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var playerEloScores = new Dictionary<Guid, int>
        {
            { winnerId, 1500 },
            { loserId, 100 } // Minimum ELO'da
        };

        // Act
        var result = _service.Calculate(winnerId, playerEloScores, WinType.Normal);

        // Assert
        Assert.True(result.NewEloScores[loserId] >= 100);
    }

    #endregion

    #region Validation Testleri

    [Fact]
    public void Calculate_LessThanTwoPlayers_ThrowsArgumentException()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var playerEloScores = new Dictionary<Guid, int>
        {
            { winnerId, 1200 }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.Calculate(winnerId, playerEloScores, WinType.Normal));
    }

    [Fact]
    public void Calculate_WinnerNotInList_ThrowsArgumentException()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var playerEloScores = new Dictionary<Guid, int>
        {
            { playerId, 1200 }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.Calculate(winnerId, playerEloScores, WinType.Normal));
    }

    [Fact]
    public void Calculate_NullPlayerScores_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.Calculate(Guid.NewGuid(), null!, WinType.Normal));
    }

    #endregion

    #region CalculateHeadToHead Testleri

    [Fact]
    public void CalculateHeadToHead_EqualElo_ReturnsExpectedChanges()
    {
        // Act
        var (winnerChange, loserChange) = _service.CalculateHeadToHead(1200, 1200);

        // Assert
        Assert.True(winnerChange > 0);
        Assert.True(loserChange < 0);
        Assert.Equal(winnerChange, -loserChange); // Sıfır toplamlı
    }

    [Fact]
    public void CalculateHeadToHead_Draw_ReturnsSmallChanges()
    {
        // Act
        var (winnerChange, loserChange) = _service.CalculateHeadToHead(1200, 1200, isDraw: true);

        // Assert
        Assert.Equal(0, winnerChange);
        Assert.Equal(0, loserChange);
    }

    #endregion

    #region KFactor Testleri

    [Fact]
    public void GetKFactor_NewPlayer_ReturnsHighK()
    {
        Assert.Equal(EloCalculationService.KFactorNewPlayer, EloCalculationService.GetKFactor(10));
    }

    [Fact]
    public void GetKFactor_NormalPlayer_ReturnsNormalK()
    {
        Assert.Equal(EloCalculationService.KFactorNormal, EloCalculationService.GetKFactor(50));
    }

    [Fact]
    public void GetKFactor_ExperiencedPlayer_ReturnsLowK()
    {
        Assert.Equal(EloCalculationService.KFactorExperienced, EloCalculationService.GetKFactor(150));
    }

    #endregion
}
