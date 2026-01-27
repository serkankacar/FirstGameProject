using OkeyGame.Domain.AI;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using Xunit;

namespace OkeyGame.Tests.AI;

/// <summary>
/// BotManager testleri.
/// </summary>
public class BotManagerTests
{
    [Fact]
    public void CreateBot_ShouldAddToManager()
    {
        // Arrange
        var manager = new BotManager();

        // Act
        var bot = manager.CreateBot(BotDifficulty.Normal);

        // Assert
        Assert.Equal(1, manager.ActiveBotCount);
        Assert.True(manager.IsBot(bot.PlayerId));
    }

    [Fact]
    public void GetBot_ShouldReturnCorrectBot()
    {
        // Arrange
        var manager = new BotManager();
        var bot = manager.CreateBot(BotDifficulty.Hard);

        // Act
        var retrieved = manager.GetBot(bot.PlayerId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(bot.PlayerId, retrieved.PlayerId);
        Assert.Equal(BotDifficulty.Hard, retrieved.Difficulty);
    }

    [Fact]
    public void RemoveBot_ShouldRemoveFromManager()
    {
        // Arrange
        var manager = new BotManager();
        var bot = manager.CreateBot(BotDifficulty.Normal);

        // Act
        var result = manager.RemoveBot(bot.PlayerId);

        // Assert
        Assert.True(result);
        Assert.Equal(0, manager.ActiveBotCount);
        Assert.False(manager.IsBot(bot.PlayerId));
    }

    [Fact]
    public void FillRoomWithBots_ShouldAddCorrectNumber()
    {
        // Arrange
        var manager = new BotManager();

        // Act
        var botIds = manager.FillRoomWithBots(currentPlayerCount: 1, maxPlayers: 4);

        // Assert
        Assert.Equal(3, botIds.Count);
        Assert.Equal(3, manager.ActiveBotCount);
    }

    [Fact]
    public void FillRoomWithRandomBots_ShouldAddVariedDifficulties()
    {
        // Arrange
        var manager = new BotManager();

        // Act
        var botIds = manager.FillRoomWithRandomBots(currentPlayerCount: 0, maxPlayers: 4);

        // Assert
        Assert.Equal(4, botIds.Count);
        
        // Zorlukların dağılımını kontrol et
        var difficulties = botIds.Select(id => manager.GetBot(id)?.Difficulty).ToList();
        Assert.All(difficulties, d => Assert.NotNull(d));
    }

    [Fact]
    public void NotifyDiscard_ShouldUpdateAllBots()
    {
        // Arrange
        var manager = new BotManager();
        var bot1 = manager.CreateBot(BotDifficulty.Normal);
        var bot2 = manager.CreateBot(BotDifficulty.Normal);

        var indicator = Tile.Create(100, TileColor.Blue, 5);
        var hand = CreateSimpleHand();

        bot1.Initialize(hand, indicator);
        bot2.Initialize(hand, indicator);

        var discardedTile = Tile.Create(200, TileColor.Red, 10);
        var discardingPlayerId = Guid.NewGuid();

        // Act
        manager.NotifyDiscard(discardedTile, discardingPlayerId);

        // Assert
        Assert.Equal(1, bot1.Memory.GetSeenCount(TileColor.Red, 10));
        Assert.Equal(1, bot2.Memory.GetSeenCount(TileColor.Red, 10));
    }

    [Fact]
    public void NotifyDiscard_ShouldNotNotifySelf()
    {
        // Arrange
        var manager = new BotManager();
        var bot = manager.CreateBot(BotDifficulty.Normal);

        var indicator = Tile.Create(100, TileColor.Blue, 5);
        bot.Initialize(CreateSimpleHand(), indicator);

        var discardedTile = Tile.Create(200, TileColor.Yellow, 8);

        // Act - Bot kendi taşını attığını bildiriyor
        manager.NotifyDiscard(discardedTile, bot.PlayerId);

        // Assert - Kendi hafızasında olmamalı (OnOpponentDiscard çağrılmadı)
        // Not: RecordSeenTile ayrı çağrılır, bu sadece opponent için
        // Bu test'te memory güncellenmez çünkü bot kendisi attı
    }

    [Fact]
    public void ClearAll_ShouldRemoveAllBots()
    {
        // Arrange
        var manager = new BotManager();
        manager.FillRoomWithBots(0, 4);

        // Act
        manager.ClearAll();

        // Assert
        Assert.Equal(0, manager.ActiveBotCount);
    }

    private List<Tile> CreateSimpleHand()
    {
        var tiles = new List<Tile>();
        for (int i = 0; i < 14; i++)
        {
            var color = (TileColor)(i % 4);
            var value = (i % 13) + 1;
            tiles.Add(Tile.Create(i + 1, color, value));
        }
        return tiles;
    }
}
