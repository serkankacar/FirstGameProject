using OkeyGame.Domain.AI;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using Xunit;

namespace OkeyGame.Tests.AI;

/// <summary>
/// TileMemory testleri.
/// </summary>
public class TileMemoryTests
{
    [Fact]
    public void SetIndicator_ShouldCalculateOkeyCorrectly()
    {
        // Arrange
        var memory = new TileMemory();
        var indicator = Tile.Create(1, TileColor.Red, 5);

        // Act
        memory.SetIndicator(indicator);

        // Assert
        Assert.NotNull(memory.OkeyIdentity);
        Assert.Equal(TileColor.Red, memory.OkeyIdentity.Value.Color);
        Assert.Equal(6, memory.OkeyIdentity.Value.Value); // 5 + 1 = 6
    }

    [Fact]
    public void SetIndicator_With13_ShouldWrapTo1()
    {
        // Arrange
        var memory = new TileMemory();
        var indicator = Tile.Create(1, TileColor.Blue, 13);

        // Act
        memory.SetIndicator(indicator);

        // Assert
        Assert.NotNull(memory.OkeyIdentity);
        Assert.Equal(1, memory.OkeyIdentity.Value.Value); // 13 + 1 = 1 (wrap)
    }

    [Fact]
    public void RecordDiscard_ShouldTrackTile()
    {
        // Arrange
        var memory = new TileMemory();
        var tile = Tile.Create(1, TileColor.Yellow, 7);

        // Act
        memory.RecordDiscard(tile);

        // Assert
        Assert.Equal(1, memory.GetSeenCount(TileColor.Yellow, 7));
        Assert.Single(memory.GetDiscardedTiles());
    }

    [Fact]
    public void RecordSeenTile_ShouldNotExceed2()
    {
        // Arrange
        var memory = new TileMemory();
        var tile1 = Tile.Create(1, TileColor.Black, 10);
        var tile2 = Tile.Create(2, TileColor.Black, 10);
        var tile3 = Tile.Create(3, TileColor.Black, 10); // 3. kopya yok ama test için

        // Act
        memory.RecordSeenTile(tile1);
        memory.RecordSeenTile(tile2);
        memory.RecordSeenTile(tile3);

        // Assert
        Assert.Equal(2, memory.GetSeenCount(TileColor.Black, 10)); // Max 2
    }

    [Fact]
    public void GetAvailabilityProbability_WhenBothSeen_ShouldBeZero()
    {
        // Arrange
        var memory = new TileMemory();
        memory.RecordSeenTile(Tile.Create(1, TileColor.Red, 5));
        memory.RecordSeenTile(Tile.Create(2, TileColor.Red, 5));

        // Act
        var probability = memory.GetAvailabilityProbability(TileColor.Red, 5);

        // Assert
        Assert.Equal(0.0, probability);
    }

    [Fact]
    public void GetAvailabilityProbability_WhenNoneSeen_ShouldBeOne()
    {
        // Arrange
        var memory = new TileMemory();

        // Act
        var probability = memory.GetAvailabilityProbability(TileColor.Red, 5);

        // Assert
        Assert.Equal(1.0, probability);
    }

    [Fact]
    public void RecordPickupFromDiscard_ShouldRemoveFromDiscardList()
    {
        // Arrange
        var memory = new TileMemory();
        var tile = Tile.Create(1, TileColor.Yellow, 3);
        var playerId = Guid.NewGuid();

        memory.RecordDiscard(tile);

        // Act
        memory.RecordPickupFromDiscard(tile, playerId);

        // Assert
        Assert.Empty(memory.GetDiscardedTiles());
        Assert.Single(memory.GetPlayerPickups(playerId));
    }

    [Fact]
    public void IsOkeyTile_ShouldIdentifyOkeyCorrectly()
    {
        // Arrange
        var memory = new TileMemory();
        var indicator = Tile.Create(1, TileColor.Blue, 7);
        memory.SetIndicator(indicator);

        var okeyTile = Tile.Create(2, TileColor.Blue, 8); // Gösterge+1
        var normalTile = Tile.Create(3, TileColor.Blue, 7);

        // Act & Assert
        Assert.True(memory.IsOkeyTile(okeyTile));
        Assert.False(memory.IsOkeyTile(normalTile));
    }
}
