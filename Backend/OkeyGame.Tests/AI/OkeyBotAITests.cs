using OkeyGame.Domain.AI;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using Xunit;

namespace OkeyGame.Tests.AI;

/// <summary>
/// OkeyBotAI testleri.
/// </summary>
public class OkeyBotAITests
{
    #region Yardımcı Metodlar

    private List<Tile> CreateTestHand()
    {
        var tiles = new List<Tile>();
        int id = 1;

        // Run potansiyeli: Kırmızı 1-2-3
        tiles.Add(Tile.Create(id++, TileColor.Red, 1));
        tiles.Add(Tile.Create(id++, TileColor.Red, 2));
        tiles.Add(Tile.Create(id++, TileColor.Red, 3));

        // Group potansiyeli: 7'ler
        tiles.Add(Tile.Create(id++, TileColor.Blue, 7));
        tiles.Add(Tile.Create(id++, TileColor.Black, 7));

        // Yalnız taşlar
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 10));
        tiles.Add(Tile.Create(id++, TileColor.Blue, 2));
        tiles.Add(Tile.Create(id++, TileColor.Black, 11));
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 5));
        tiles.Add(Tile.Create(id++, TileColor.Red, 9));
        tiles.Add(Tile.Create(id++, TileColor.Blue, 12));
        tiles.Add(Tile.Create(id++, TileColor.Black, 4));
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 8));
        tiles.Add(Tile.Create(id++, TileColor.Red, 6));

        return tiles;
    }

    #endregion

    #region Initialization Testleri

    [Fact]
    public void Initialize_ShouldSetupBot()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());
        var hand = CreateTestHand();
        var indicator = Tile.Create(100, TileColor.Blue, 5);

        // Act
        bot.Initialize(hand, indicator);

        // Assert
        Assert.Equal(14, bot.Hand.Count);
    }

    [Fact]
    public void DecideDrawSource_WithoutInitialize_ShouldThrow()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => bot.DecideDrawSource(null));
    }

    #endregion

    #region Çekme Kararı Testleri

    [Fact]
    public void DecideDrawSource_WithNoDiscard_ShouldDrawFromDeck()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());
        bot.Initialize(CreateTestHand(), Tile.Create(100, TileColor.Blue, 5));

        // Act
        var decision = bot.DecideDrawSource(null);

        // Assert
        Assert.Equal(BotDecisionType.DrawFromDeck, decision.Type);
    }

    [Fact]
    public void DecideDrawSource_WithUsefulDiscard_ShouldPickup()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Expert, Guid.NewGuid(), seed: 42);
        var hand = CreateTestHand();
        bot.Initialize(hand, Tile.Create(100, TileColor.Blue, 5));

        // Elde Sarı 7 yoksa, Sarı 7 işe yarar mı?
        var usefulTile = Tile.Create(200, TileColor.Yellow, 7); // Group tamamlar

        // Act
        var decision = bot.DecideDrawSource(usefulTile);

        // Assert - Expert bot bunu almalı
        Assert.Equal(BotDecisionType.DrawFromDiscard, decision.Type);
    }

    [Fact]
    public void DecideDrawSource_WithUselessDiscard_ShouldDrawFromDeck()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());
        bot.Initialize(CreateTestHand(), Tile.Create(100, TileColor.Blue, 5));

        // El ile hiç alakası olmayan bir taş
        var uselessTile = Tile.Create(200, TileColor.Yellow, 13);

        // Act
        var decision = bot.DecideDrawSource(uselessTile);

        // Assert
        Assert.Equal(BotDecisionType.DrawFromDeck, decision.Type);
    }

    #endregion

    #region Atma Kararı Testleri

    [Fact]
    public void DecideDiscard_ShouldNotDiscardOkey()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());
        var hand = CreateTestHand();
        
        // Gösterge: Mavi 5, yani Okey: Mavi 6
        var indicator = Tile.Create(100, TileColor.Blue, 5);
        bot.Initialize(hand, indicator);

        // Mavi 6 (Okey) çektik
        var okeyTile = Tile.Create(200, TileColor.Blue, 6);

        // Act
        var decision = bot.DecideDiscard(okeyTile);

        // Assert - Okey atılmamalı
        Assert.NotEqual(okeyTile.Id, decision.Tile?.Id);
    }

    [Fact]
    public void DecideDiscard_ShouldPreferIsolatedTiles()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Hard, Guid.NewGuid(), seed: 42);
        var hand = new List<Tile>();
        int id = 1;

        // Tamamlanmış run: Kırmızı 1-2-3
        hand.Add(Tile.Create(id++, TileColor.Red, 1));
        hand.Add(Tile.Create(id++, TileColor.Red, 2));
        hand.Add(Tile.Create(id++, TileColor.Red, 3));

        // Tamamlanmış group: 7'ler
        hand.Add(Tile.Create(id++, TileColor.Blue, 7));
        hand.Add(Tile.Create(id++, TileColor.Black, 7));
        hand.Add(Tile.Create(id++, TileColor.Yellow, 7));

        // Yalnız taşlar
        hand.Add(Tile.Create(id++, TileColor.Black, 13)); // Yalnız
        hand.Add(Tile.Create(id++, TileColor.Yellow, 1));
        hand.Add(Tile.Create(id++, TileColor.Blue, 10));
        hand.Add(Tile.Create(id++, TileColor.Red, 8));
        hand.Add(Tile.Create(id++, TileColor.Black, 4));
        hand.Add(Tile.Create(id++, TileColor.Yellow, 12));
        hand.Add(Tile.Create(id++, TileColor.Blue, 3));
        hand.Add(Tile.Create(id++, TileColor.Red, 11));

        var indicator = Tile.Create(100, TileColor.Blue, 5);
        bot.Initialize(hand, indicator);

        var drawnTile = Tile.Create(200, TileColor.Black, 9);

        // Act
        var decision = bot.DecideDiscard(drawnTile);

        // Assert - Per içindeki taşlar atılmamalı
        var discardedTile = decision.Tile!;
        bool isInRun = discardedTile.Color == TileColor.Red && 
                       discardedTile.Value >= 1 && discardedTile.Value <= 3;
        bool isInGroup = discardedTile.Value == 7;

        Assert.False(isInRun && isInGroup, "Per içindeki taş atılmamalı");
    }

    #endregion

    #region Hafıza Testleri

    [Fact]
    public void OnOpponentDiscard_ShouldUpdateMemory()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());
        bot.Initialize(CreateTestHand(), Tile.Create(100, TileColor.Blue, 5));

        var opponentId = Guid.NewGuid();
        var discardedTile = Tile.Create(200, TileColor.Red, 10);

        // Act
        bot.OnOpponentDiscard(discardedTile, opponentId);

        // Assert
        Assert.Equal(1, bot.Memory.GetSeenCount(TileColor.Red, 10));
    }

    [Fact]
    public void OnOpponentPickup_ShouldTrackPlayer()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());
        bot.Initialize(CreateTestHand(), Tile.Create(100, TileColor.Blue, 5));

        var opponentId = Guid.NewGuid();
        var pickedTile = Tile.Create(200, TileColor.Yellow, 8);

        // Önce discard'a at
        bot.OnOpponentDiscard(pickedTile, Guid.NewGuid());

        // Act
        bot.OnOpponentPickup(pickedTile, opponentId);

        // Assert
        Assert.Single(bot.Memory.GetPlayerPickups(opponentId));
    }

    #endregion

    #region ThinkingTime Testleri

    [Fact]
    public void DecideDrawSource_ShouldHaveReasonableThinkingTime()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());
        bot.Initialize(CreateTestHand(), Tile.Create(100, TileColor.Blue, 5));

        // Act
        var decision = bot.DecideDrawSource(null);

        // Assert
        Assert.InRange(decision.ThinkingTimeMs, 1500, 5500);
    }

    [Fact]
    public void DecideDiscard_ShouldHaveReasonableThinkingTime()
    {
        // Arrange
        var bot = new OkeyBotAI(BotDifficulty.Normal, Guid.NewGuid());
        bot.Initialize(CreateTestHand(), Tile.Create(100, TileColor.Blue, 5));

        var drawnTile = Tile.Create(200, TileColor.Black, 9);

        // Act
        var decision = bot.DecideDiscard(drawnTile);

        // Assert
        Assert.InRange(decision.ThinkingTimeMs, 1500, 5500);
    }

    #endregion

    #region Zorluk Seviyesi Testleri

    [Theory]
    [InlineData(BotDifficulty.Easy)]
    [InlineData(BotDifficulty.Normal)]
    [InlineData(BotDifficulty.Hard)]
    [InlineData(BotDifficulty.Expert)]
    public void AllDifficulties_ShouldWork(BotDifficulty difficulty)
    {
        // Arrange
        var bot = new OkeyBotAI(difficulty, Guid.NewGuid());
        bot.Initialize(CreateTestHand(), Tile.Create(100, TileColor.Blue, 5));

        // Act
        var drawDecision = bot.DecideDrawSource(null);
        var discardDecision = bot.DecideDiscard(Tile.Create(200, TileColor.Red, 10));

        // Assert
        Assert.NotNull(drawDecision);
        Assert.NotNull(discardDecision);
        Assert.NotNull(discardDecision.Tile);
    }

    #endregion
}
