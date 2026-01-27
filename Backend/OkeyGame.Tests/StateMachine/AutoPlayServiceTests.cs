using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.StateMachine;
using Xunit;

namespace OkeyGame.Tests.StateMachine;

/// <summary>
/// AutoPlayService birim testleri.
/// </summary>
public class AutoPlayServiceTests
{
    private readonly AutoPlayService _autoPlayService = AutoPlayService.Instance;

    #region Singleton Testi

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Act
        var instance1 = AutoPlayService.Instance;
        var instance2 = AutoPlayService.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    #endregion

    #region PlayTurnAsync Testleri

    [Fact]
    public async Task PlayTurnAsync_WithValidHand_ReturnsSuccess()
    {
        // Arrange
        var hand = CreateSampleHand();
        var indicator = Tile.Create(1, TileColor.Yellow, 5);
        var context = TurnContext.StartNew(
            Guid.NewGuid(), Guid.NewGuid(), PlayerPosition.South, 1);
        
        Tile? drawnTile = null;
        Func<Tile> drawFromDeck = () =>
        {
            drawnTile = Tile.Create(100, TileColor.Blue, 7);
            return drawnTile;
        };

        // Act
        var result = await _autoPlayService.PlayTurnAsync(
            hand, indicator, null, drawFromDeck, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.DiscardedTile);
        Assert.Equal(AutoPlayReason.Timeout, result.Reason);
    }

    [Fact]
    public async Task PlayTurnAsync_WithDiscardOption_CanChooseDiscard()
    {
        // Arrange
        var hand = CreateSampleHand();
        var indicator = Tile.Create(1, TileColor.Yellow, 5);
        var lastDiscarded = Tile.Create(99, TileColor.Yellow, 6); // Okey'e yakın taş
        var context = TurnContext.StartNew(
            Guid.NewGuid(), Guid.NewGuid(), PlayerPosition.South, 1);
        
        Func<Tile> drawFromDeck = () => Tile.Create(100, TileColor.Blue, 7);

        // Act
        var result = await _autoPlayService.PlayTurnAsync(
            hand, indicator, lastDiscarded, drawFromDeck, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.DiscardedTile);
    }

    [Fact]
    public async Task PlayTurnAsync_EmptyHand_ReturnsError()
    {
        // Arrange
        var hand = new List<Tile>();
        var indicator = Tile.Create(1, TileColor.Yellow, 5);
        var context = TurnContext.StartNew(
            Guid.NewGuid(), Guid.NewGuid(), PlayerPosition.South, 1);
        
        Func<Tile> drawFromDeck = () => Tile.Create(100, TileColor.Blue, 7);

        // Act - Should handle error gracefully
        var result = await _autoPlayService.PlayTurnAsync(
            hand, indicator, null, drawFromDeck, context);

        // Assert - Should return error result (not throw)
        // Error durumunda en basit hamle yapılır
        Assert.Equal(AutoPlayReason.Error, result.Reason);
    }

    [Fact]
    public async Task PlayTurnAsync_Cancellation_ReturnsCancelledResult()
    {
        // Arrange
        var hand = CreateSampleHand();
        var indicator = Tile.Create(1, TileColor.Yellow, 5);
        var context = TurnContext.StartNew(
            Guid.NewGuid(), Guid.NewGuid(), PlayerPosition.South, 1);
        
        Func<Tile> drawFromDeck = () => Tile.Create(100, TileColor.Blue, 7);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _autoPlayService.PlayTurnAsync(
            hand, indicator, null, drawFromDeck, context, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(AutoPlayReason.Cancelled, result.Reason);
    }

    #endregion

    #region DiscardOnlyAsync Testleri

    [Fact]
    public async Task DiscardOnlyAsync_WithValidHand_ReturnsSuccess()
    {
        // Arrange
        var hand = CreateSampleHandWithDraw();
        var indicator = Tile.Create(1, TileColor.Yellow, 5);
        var context = TurnContext.StartFirst(
            Guid.NewGuid(), Guid.NewGuid(), PlayerPosition.South);

        // Act
        var result = await _autoPlayService.DiscardOnlyAsync(
            hand, indicator, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.DiscardedTile);
        Assert.Equal(AutoPlayReason.Timeout, result.Reason);
    }

    [Fact]
    public async Task DiscardOnlyAsync_SelectsLeastValuableTile()
    {
        // Arrange - 2 izole taş + 1 grup
        var hand = new List<Tile>
        {
            // İzole taş (en düşük değerli)
            Tile.Create(1, TileColor.Red, 1),
            
            // Potansiyel grup
            Tile.Create(2, TileColor.Yellow, 7),
            Tile.Create(3, TileColor.Blue, 7),
            Tile.Create(4, TileColor.Black, 7),
            
            // Başka izole taş
            Tile.Create(5, TileColor.Blue, 3)
        };
        var indicator = Tile.Create(99, TileColor.Yellow, 10);
        var context = TurnContext.StartFirst(
            Guid.NewGuid(), Guid.NewGuid(), PlayerPosition.South);

        // Act
        var result = await _autoPlayService.DiscardOnlyAsync(
            hand, indicator, context);

        // Assert
        Assert.True(result.Success);
        // İzole ve düşük numaralı taş seçilmeli
        Assert.True(result.DiscardedTile!.Value <= 3);
    }

    #endregion

    #region AutoPlayResult Testleri

    [Fact]
    public void AutoPlayResult_Properties_SetCorrectly()
    {
        // Arrange
        var tile = Tile.Create(1, TileColor.Yellow, 5);
        
        // Act
        var result = new AutoPlayResult
        {
            Success = true,
            DrewFromDiscard = true,
            DrawnTile = tile,
            DiscardedTile = tile,
            IsWinning = false,
            Reason = AutoPlayReason.Timeout,
            Message = "Test mesajı"
        };

        // Assert
        Assert.True(result.Success);
        Assert.True(result.DrewFromDiscard);
        Assert.Same(tile, result.DrawnTile);
        Assert.Same(tile, result.DiscardedTile);
        Assert.False(result.IsWinning);
        Assert.Equal(AutoPlayReason.Timeout, result.Reason);
        Assert.Equal("Test mesajı", result.Message);
    }

    [Fact]
    public void AutoPlayResult_Timestamp_IsSet()
    {
        // Arrange & Act
        var before = DateTime.UtcNow;
        var result = new AutoPlayResult { Success = true };
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(result.Timestamp >= before);
        Assert.True(result.Timestamp <= after);
    }

    #endregion

    #region AutoPlayReason Testleri

    [Theory]
    [InlineData(AutoPlayReason.Timeout, "Timeout")]
    [InlineData(AutoPlayReason.Disconnected, "Disconnected")]
    [InlineData(AutoPlayReason.AFK, "AFK")]
    [InlineData(AutoPlayReason.Cancelled, "Cancelled")]
    [InlineData(AutoPlayReason.Error, "Error")]
    public void AutoPlayReason_AllValuesExist(AutoPlayReason reason, string expectedName)
    {
        Assert.Equal(expectedName, reason.ToString());
    }

    #endregion

    #region Yardımcı Metodlar

    private List<Tile> CreateSampleHand()
    {
        // 14 taşlık örnek el
        return new List<Tile>
        {
            Tile.Create(1, TileColor.Yellow, 1),
            Tile.Create(2, TileColor.Yellow, 2),
            Tile.Create(3, TileColor.Yellow, 3),
            Tile.Create(4, TileColor.Blue, 5),
            Tile.Create(5, TileColor.Blue, 6),
            Tile.Create(6, TileColor.Blue, 7),
            Tile.Create(7, TileColor.Red, 10),
            Tile.Create(8, TileColor.Red, 11),
            Tile.Create(9, TileColor.Red, 12),
            Tile.Create(10, TileColor.Black, 4),
            Tile.Create(11, TileColor.Black, 8),
            Tile.Create(12, TileColor.Black, 9),
            Tile.Create(13, TileColor.Yellow, 13),
            Tile.Create(14, TileColor.Blue, 1)
        };
    }

    private List<Tile> CreateSampleHandWithDraw()
    {
        // 15 taşlık örnek el (taş çekilmiş)
        var hand = CreateSampleHand();
        hand.Add(Tile.Create(15, TileColor.Red, 5));
        return hand;
    }

    #endregion
}
