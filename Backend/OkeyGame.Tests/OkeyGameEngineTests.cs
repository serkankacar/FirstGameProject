using OkeyGame.Application.Services;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using Xunit;

namespace OkeyGame.Tests;

/// <summary>
/// OkeyGameEngine sınıfı için birim testleri.
/// </summary>
public class OkeyGameEngineTests
{
    private Room CreateRoomWithPlayers()
    {
        var room = new Room("Test Odası");
        
        room.AddPlayer(new Player(Guid.NewGuid(), "Oyuncu1", PlayerPosition.South));
        room.AddPlayer(new Player(Guid.NewGuid(), "Oyuncu2", PlayerPosition.East));
        room.AddPlayer(new Player(Guid.NewGuid(), "Oyuncu3", PlayerPosition.North));
        room.AddPlayer(new Player(Guid.NewGuid(), "Oyuncu4", PlayerPosition.West));
        
        return room;
    }

    [Fact]
    public void Constructor_ValidRoom_ShouldCreateEngine()
    {
        // Arrange
        var room = CreateRoomWithPlayers();

        // Act
        var engine = new OkeyGameEngine(room);

        // Assert
        Assert.NotNull(engine);
    }

    [Fact]
    public void Constructor_RoomWithLessThan4Players_ShouldThrow()
    {
        // Arrange
        var room = new Room("Test Odası");
        room.AddPlayer(new Player(Guid.NewGuid(), "Oyuncu1", PlayerPosition.South));
        room.AddPlayer(new Player(Guid.NewGuid(), "Oyuncu2", PlayerPosition.East));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new OkeyGameEngine(room));
    }

    [Fact]
    public void Constructor_NullRoom_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OkeyGameEngine(null!));
    }

    [Fact]
    public void StartGame_ShouldDistributeTilesCorrectly()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);

        // Act
        var result = engine.StartGame();

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);

        // 1. oyuncu 15 taş almalı
        var firstPlayer = room.GetPlayerByPosition(PlayerPosition.South);
        Assert.Equal(15, firstPlayer!.TileCount);

        // Diğer oyuncular 14'er taş almalı
        Assert.Equal(14, room.GetPlayerByPosition(PlayerPosition.East)!.TileCount);
        Assert.Equal(14, room.GetPlayerByPosition(PlayerPosition.North)!.TileCount);
        Assert.Equal(14, room.GetPlayerByPosition(PlayerPosition.West)!.TileCount);
    }

    [Fact]
    public void StartGame_ShouldSetGameStateToInProgress()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);

        // Act
        engine.StartGame();

        // Assert
        Assert.Equal(GameState.InProgress, room.State);
        Assert.True(engine.IsGameInProgress);
    }

    [Fact]
    public void StartGame_ShouldSetIndicatorTile()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);

        // Act
        engine.StartGame();

        // Assert
        Assert.NotNull(engine.IndicatorTile);
        Assert.False(engine.IndicatorTile.IsFalseJoker);
    }

    [Fact]
    public void StartGame_ShouldHaveRemainingTilesInDeck()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);

        // Act
        engine.StartGame();

        // Assert
        // Toplam: 106 taş
        // Dağıtılan: 15 + 14 + 14 + 14 = 57 taş
        // Kalan: 106 - 57 = 49 taş
        Assert.True(engine.RemainingTileCount > 0);
        Assert.True(engine.RemainingTileCount < 106);
    }

    [Fact]
    public void StartGame_CalledTwice_ShouldFail()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);

        // Act
        var firstResult = engine.StartGame();
        var secondResult = engine.StartGame();

        // Assert
        Assert.True(firstResult.Success);
        Assert.False(secondResult.Success);
    }

    [Fact]
    public void GetGameStateForPlayer_ShouldNotExposeOpponentHands()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);
        engine.StartGame();
        
        var player = room.Players.First();

        // Act
        var state = engine.GetGameStateForPlayer(player.Id);

        // Assert
        Assert.NotEmpty(state.Self.Hand); // Kendi eli var
        
        foreach (var opponent in state.Opponents)
        {
            // Rakiplerin taş sayısı var
            Assert.True(opponent.TileCount > 0);
            // Ama elimiz dışında opponent'ta Hand property yok (DTO tasarımı)
        }
    }

    [Fact]
    public void GetGameStateForPlayer_ShouldIncludeIndicatorTile()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);
        engine.StartGame();
        
        var player = room.Players.First();

        // Act
        var state = engine.GetGameStateForPlayer(player.Id);

        // Assert
        Assert.NotNull(state.IndicatorTile);
        Assert.False(state.IndicatorTile.IsFalseJoker);
    }

    [Fact]
    public void DrawTile_WhenPlayersTurn_ShouldSucceed()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);
        engine.StartGame();
        
        var currentPlayer = room.GetCurrentPlayer()!;
        int initialTileCount = currentPlayer.TileCount;

        // Act
        var result = engine.DrawTile(currentPlayer.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.DrawnTile);
        Assert.Equal(initialTileCount + 1, currentPlayer.TileCount);
    }

    [Fact]
    public void DrawTile_WhenNotPlayersTurn_ShouldFail()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);
        engine.StartGame();
        
        // Sırası olmayan bir oyuncu
        var otherPlayer = room.Players.First(p => !p.IsCurrentTurn);

        // Act
        var result = engine.DrawTile(otherPlayer.Id);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void DiscardTile_WhenPlayersTurn_ShouldAdvanceTurn()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);
        engine.StartGame();
        
        var currentPlayer = room.GetCurrentPlayer()!;
        var currentPosition = room.CurrentTurnPosition;
        var tileToDiscard = currentPlayer.Hand.First();

        // Act
        var result = engine.DiscardTile(currentPlayer.Id, tileToDiscard.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotEqual(currentPosition, room.CurrentTurnPosition);
    }

    [Fact]
    public void GetGameStartDto_ShouldIncludeServerSeedHash()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);
        engine.StartGame();
        
        var player = room.Players.First();

        // Act
        var dto = engine.GetGameStartDto(player.Id);

        // Assert
        Assert.NotNull(dto.ServerSeedHash);
        Assert.Equal(64, dto.ServerSeedHash.Length); // SHA256 = 64 hex char
    }

    [Fact]
    public void RevealServerSeed_BeforeGameEnds_ShouldThrow()
    {
        // Arrange
        var room = CreateRoomWithPlayers();
        var engine = new OkeyGameEngine(room);
        engine.StartGame();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => engine.RevealServerSeed());
    }
}
