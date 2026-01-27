using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;
using Xunit;

namespace OkeyGame.Tests.Domain;

/// <summary>
/// ScoringService testleri.
/// </summary>
public class ScoringServiceTests
{
    private readonly ScoringService _service = ScoringService.Instance;

    #region CalculateGameScore Testleri

    [Fact]
    public void CalculateGameScore_NormalWin_ShouldGiveCorrectPenalties()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId1 = Guid.NewGuid();
        var loserId2 = Guid.NewGuid();

        var playerHands = new Dictionary<Guid, List<Tile>>
        {
            [winnerId] = new List<Tile>(), // Kazanan el boş
            [loserId1] = CreateSimpleHand(14),
            [loserId2] = CreateSimpleHand(14)
        };

        // Act
        var result = _service.CalculateGameScore(winnerId, WinType.Normal, playerHands);

        // Assert
        Assert.Equal(winnerId, result.WinnerId);
        Assert.Equal(WinType.Normal, result.WinType);
        Assert.Equal(0, result.Scores[winnerId]);
        Assert.Equal(ScoringService.NormalWinPenalty, result.Scores[loserId1]);
        Assert.Equal(ScoringService.NormalWinPenalty, result.Scores[loserId2]);
    }

    [Fact]
    public void CalculateGameScore_PairsWin_ShouldGiveHigherPenalty()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var playerHands = new Dictionary<Guid, List<Tile>>
        {
            [winnerId] = new List<Tile>(),
            [loserId] = CreateSimpleHand(14)
        };

        // Act
        var result = _service.CalculateGameScore(winnerId, WinType.Pairs, playerHands);

        // Assert
        Assert.Equal(ScoringService.PairsWinPenalty, result.Scores[loserId]);
        Assert.True(result.Scores[loserId] > ScoringService.NormalWinPenalty);
    }

    [Fact]
    public void CalculateGameScore_OkeyDiscard_ShouldGiveHighestPenalty()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var playerHands = new Dictionary<Guid, List<Tile>>
        {
            [winnerId] = new List<Tile>(),
            [loserId] = CreateSimpleHand(14)
        };

        // Act
        var result = _service.CalculateGameScore(winnerId, WinType.OkeyDiscard, playerHands);

        // Assert
        Assert.Equal(ScoringService.OkeyDiscardPenalty, result.Scores[loserId]);
    }

    [Fact]
    public void CalculateGameScore_LoserWithOkey_ShouldGetExtraPenalty()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var loserHand = CreateSimpleHand(13);
        loserHand.Add(Tile.Create(100, TileColor.Yellow, 5).AsOkey()); // Elde Okey var

        var playerHands = new Dictionary<Guid, List<Tile>>
        {
            [winnerId] = new List<Tile>(),
            [loserId] = loserHand
        };

        // Act
        var result = _service.CalculateGameScore(winnerId, WinType.Normal, playerHands);

        // Assert
        int expectedPenalty = ScoringService.NormalWinPenalty + ScoringService.HandOkeyPenalty;
        Assert.Equal(expectedPenalty, result.Scores[loserId]);
    }

    [Fact]
    public void CalculateGameScore_LoserWithMultipleOkeys_ShouldStackPenalty()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var loserHand = CreateSimpleHand(12);
        loserHand.Add(Tile.Create(100, TileColor.Yellow, 5).AsOkey());
        loserHand.Add(Tile.Create(101, TileColor.Blue, 6).AsOkey());

        var playerHands = new Dictionary<Guid, List<Tile>>
        {
            [winnerId] = new List<Tile>(),
            [loserId] = loserHand
        };

        // Act
        var result = _service.CalculateGameScore(winnerId, WinType.Normal, playerHands);

        // Assert
        int expectedPenalty = ScoringService.NormalWinPenalty + (2 * ScoringService.HandOkeyPenalty);
        Assert.Equal(expectedPenalty, result.Scores[loserId]);
    }

    #endregion

    #region CalculateDeckEmptyScore Testleri

    [Fact]
    public void CalculateDeckEmptyScore_AllPlayersGetPenalty()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var player4 = Guid.NewGuid();

        var playerHands = new Dictionary<Guid, List<Tile>>
        {
            [player1] = CreateSimpleHand(14),
            [player2] = CreateSimpleHand(14),
            [player3] = CreateSimpleHand(14),
            [player4] = CreateSimpleHand(14)
        };

        // Act
        var result = _service.CalculateDeckEmptyScore(playerHands);

        // Assert
        Assert.Equal(Guid.Empty, result.WinnerId);
        Assert.Equal(WinType.DeckEmpty, result.WinType);
        Assert.All(result.Scores.Values, score => Assert.Equal(ScoringService.DeckEmptyPenalty, score));
    }

    [Fact]
    public void CalculateDeckEmptyScore_PlayerWithOkey_GetsExtraPenalty()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        var hand1 = CreateSimpleHand(14);
        var hand2 = CreateSimpleHand(13);
        hand2.Add(Tile.Create(100, TileColor.Yellow, 5).AsOkey());

        var playerHands = new Dictionary<Guid, List<Tile>>
        {
            [player1] = hand1,
            [player2] = hand2
        };

        // Act
        var result = _service.CalculateDeckEmptyScore(playerHands);

        // Assert
        Assert.Equal(ScoringService.DeckEmptyPenalty, result.Scores[player1]);
        Assert.Equal(ScoringService.DeckEmptyPenalty + ScoringService.HandOkeyPenalty, result.Scores[player2]);
    }

    #endregion

    #region CalculateHandValue Testleri

    [Fact]
    public void CalculateHandValue_SumsAllTileValues()
    {
        // Arrange
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 1),
            Tile.Create(2, TileColor.Blue, 5),
            Tile.Create(3, TileColor.Black, 10)
        };

        // Act
        var value = _service.CalculateHandValue(tiles);

        // Assert
        Assert.Equal(16, value); // 1 + 5 + 10
    }

    [Fact]
    public void CalculateHandValue_OkeyUsesIndicatorPlusOne()
    {
        // Arrange
        var indicatorTile = Tile.Create(50, TileColor.Red, 7);
        var okey = Tile.Create(100, TileColor.Yellow, 8).AsOkey();
        
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 1),
            okey
        };

        // Act
        var value = _service.CalculateHandValue(tiles, indicatorTile);

        // Assert
        Assert.Equal(9, value); // 1 + 8 (gösterge 7, okey 8)
    }

    [Fact]
    public void CalculateHandValue_FalseJokerIsWorthZero()
    {
        // Arrange
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 5),
            Tile.CreateFalseJoker(100)
        };

        // Act
        var value = _service.CalculateHandValue(tiles);

        // Assert
        Assert.Equal(5, value); // Sadece 5, false joker 0
    }

    #endregion

    #region Total Scores & Rankings Testleri

    [Fact]
    public void CalculateTotalScores_SumsAcrossGames()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        var game1 = new GameScoreResult { WinnerId = player1, WinType = WinType.Normal };
        game1.Scores[player1] = 0;
        game1.Scores[player2] = 2;

        var game2 = new GameScoreResult { WinnerId = player2, WinType = WinType.Normal };
        game2.Scores[player1] = 2;
        game2.Scores[player2] = 0;

        // Act
        var totals = _service.CalculateTotalScores(new List<GameScoreResult> { game1, game2 });

        // Assert
        Assert.Equal(2, totals[player1]);
        Assert.Equal(2, totals[player2]);
    }

    [Fact]
    public void GetRankings_OrdersByScore_LowToHigh()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();

        var scores = new Dictionary<Guid, int>
        {
            [player1] = 10,
            [player2] = 5,
            [player3] = 15
        };

        // Act
        var rankings = _service.GetRankings(scores);

        // Assert
        Assert.Equal(3, rankings.Count);
        Assert.Equal(player2, rankings[0].PlayerId); // 5 puan - 1. sıra
        Assert.Equal(player1, rankings[1].PlayerId); // 10 puan - 2. sıra
        Assert.Equal(player3, rankings[2].PlayerId); // 15 puan - 3. sıra
        
        Assert.Equal(1, rankings[0].Rank);
        Assert.Equal(2, rankings[1].Rank);
        Assert.Equal(3, rankings[2].Rank);
    }

    #endregion

    #region Yardımcı Metodlar

    private List<Tile> CreateSimpleHand(int count)
    {
        var tiles = new List<Tile>();
        for (int i = 0; i < count; i++)
        {
            var color = (TileColor)(i % 4);
            var value = (i % 13) + 1;
            tiles.Add(Tile.Create(i + 1, color, value));
        }
        return tiles;
    }

    #endregion
}
