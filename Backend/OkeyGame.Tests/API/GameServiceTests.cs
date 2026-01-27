using OkeyGame.API.Models;
using OkeyGame.API.Services;
using OkeyGame.Application.Services;
using OkeyGame.Domain.Enums;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;

namespace OkeyGame.Tests.API;

/// <summary>
/// GameService birim testleri.
/// Mock'lanmış Redis ile test edilir.
/// </summary>
public class GameServiceTests
{
    private readonly Mock<IGameStateService> _mockStateService;
    private readonly ProvablyFairService _provablyFairService;
    private readonly Mock<ILogger<GameService>> _mockLogger;
    private readonly GameService _gameService;

    public GameServiceTests()
    {
        _mockStateService = new Mock<IGameStateService>();
        _provablyFairService = new ProvablyFairService();
        _mockLogger = new Mock<ILogger<GameService>>();
        
        _gameService = new GameService(
            _mockStateService.Object,
            _provablyFairService,
            _mockLogger.Object);
    }

    #region Oda Oluşturma Testleri

    [Fact]
    public async Task CreateRoom_ShouldReturnNewRoom_WithCreatorAsFirstPlayer()
    {
        // Arrange
        var roomName = "Test Odası";
        var creatorId = Guid.NewGuid();
        var creatorName = "Player1";

        _mockStateService
            .Setup(x => x.SaveRoomStateAsync(It.IsAny<GameRoomState>()))
            .Returns(Task.CompletedTask);
        
        _mockStateService
            .Setup(x => x.AddToActiveRoomsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Act
        var room = await _gameService.CreateRoomAsync(roomName, creatorId, creatorName);

        // Assert
        Assert.NotNull(room);
        Assert.Equal(roomName, room.RoomName);
        Assert.Equal(GameState.WaitingForPlayers, room.State);
        Assert.Single(room.Players);
        Assert.True(room.Players.ContainsKey(creatorId));
        Assert.Equal(PlayerPosition.South, room.Players[creatorId].Position);
        Assert.True(room.Players[creatorId].IsConnected);
    }

    [Fact]
    public async Task CreateRoom_ShouldSaveToStateService()
    {
        // Arrange
        var roomName = "Test Odası";
        var creatorId = Guid.NewGuid();
        var creatorName = "Player1";

        _mockStateService
            .Setup(x => x.SaveRoomStateAsync(It.IsAny<GameRoomState>()))
            .Returns(Task.CompletedTask);
        
        _mockStateService
            .Setup(x => x.AddToActiveRoomsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Act
        await _gameService.CreateRoomAsync(roomName, creatorId, creatorName);

        // Assert
        _mockStateService.Verify(
            x => x.SaveRoomStateAsync(It.IsAny<GameRoomState>()),
            Times.Once);
        
        _mockStateService.Verify(
            x => x.AddToActiveRoomsAsync(It.IsAny<Guid>()),
            Times.Once);
    }

    #endregion

    #region Odaya Katılma Testleri

    [Fact]
    public async Task JoinRoom_ShouldReturnSuccess_WhenRoomHasSpace()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var connectionId = "conn-123";
        
        var existingRoom = CreateTestRoom(roomId, 1);

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(existingRoom);
        
        _mockStateService
            .Setup(x => x.SaveRoomStateAsync(It.IsAny<GameRoomState>()))
            .Returns(Task.CompletedTask);
        
        _mockStateService
            .Setup(x => x.SaveConnectionMappingAsync(playerId, roomId, connectionId))
            .Returns(Task.CompletedTask);

        // Act
        var (success, error) = await _gameService.JoinRoomAsync(
            roomId, playerId, "NewPlayer", connectionId);

        // Assert
        Assert.True(success);
        Assert.Null(error);
    }

    [Fact]
    public async Task JoinRoom_ShouldReturnError_WhenRoomIsFull()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var connectionId = "conn-123";
        
        var fullRoom = CreateTestRoom(roomId, 4); // 4 oyuncu var

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(fullRoom);

        // Act
        var (success, error) = await _gameService.JoinRoomAsync(
            roomId, playerId, "NewPlayer", connectionId);

        // Assert
        Assert.False(success);
        Assert.Equal("Oda dolu.", error);
    }

    [Fact]
    public async Task JoinRoom_ShouldReturnError_WhenGameAlreadyStarted()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        
        var room = CreateTestRoom(roomId, 2);
        room.State = GameState.InProgress;

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);

        // Act
        var (success, error) = await _gameService.JoinRoomAsync(
            roomId, playerId, "NewPlayer", "conn-123");

        // Assert
        Assert.False(success);
        Assert.Contains("başlamış", error!);
    }

    [Fact]
    public async Task JoinRoom_ShouldReturnError_WhenRoomNotFound()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync((GameRoomState?)null);

        // Act
        var (success, error) = await _gameService.JoinRoomAsync(
            roomId, playerId, "NewPlayer", "conn-123");

        // Assert
        Assert.False(success);
        Assert.Equal("Oda bulunamadı.", error);
    }

    #endregion

    #region Oyun Başlatma Testleri

    [Fact]
    public async Task StartGame_ShouldReturnSuccess_WhenRoomIsFull()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = CreateTestRoom(roomId, 4);

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);
        
        _mockStateService
            .Setup(x => x.SaveRoomStateAsync(It.IsAny<GameRoomState>()))
            .Returns(Task.CompletedTask);

        // Act
        var (success, error) = await _gameService.StartGameAsync(roomId);

        // Assert
        Assert.True(success);
        Assert.Null(error);
    }

    [Fact]
    public async Task StartGame_ShouldReturnError_WhenNotEnoughPlayers()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = CreateTestRoom(roomId, 2); // Sadece 2 oyuncu

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);

        // Act
        var (success, error) = await _gameService.StartGameAsync(roomId);

        // Assert
        Assert.False(success);
        Assert.Contains("4", error!);
    }

    [Fact]
    public async Task StartGame_ShouldDistributeTilesCorrectly()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var room = CreateTestRoom(roomId, 4);
        GameRoomState? savedState = null;

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);
        
        _mockStateService
            .Setup(x => x.SaveRoomStateAsync(It.IsAny<GameRoomState>()))
            .Callback<GameRoomState>(s => savedState = s)
            .Returns(Task.CompletedTask);

        // Act
        await _gameService.StartGameAsync(roomId);

        // Assert
        Assert.NotNull(savedState);
        Assert.Equal(GameState.InProgress, savedState.State);
        Assert.NotNull(savedState.AllTilesJson);
        Assert.NotNull(savedState.CommitmentHash);
        Assert.NotNull(savedState.IndicatorTileId);
        
        // İlk oyuncu 15, diğerleri 14 taş almalı
        var orderedPlayers = savedState.Players.Values.OrderBy(p => p.Position).ToList();
        Assert.Equal(15, orderedPlayers[0].HandTileIds.Count);
        Assert.Equal(14, orderedPlayers[1].HandTileIds.Count);
        Assert.Equal(14, orderedPlayers[2].HandTileIds.Count);
        Assert.Equal(14, orderedPlayers[3].HandTileIds.Count);
        
        // Toplam dağıtılan: 15 + 14 + 14 + 14 = 57
        // Kalan deste: 106 - 57 = 49
        Assert.Equal(49, savedState.DeckTileIds.Count);
    }

    #endregion

    #region Reconnection Testleri

    [Fact]
    public async Task TryReconnect_ShouldSucceed_WithinTimeout()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var newConnectionId = "new-conn-123";

        var mapping = new ConnectionMapping
        {
            PlayerId = playerId,
            RoomId = roomId,
            LastConnectionId = "old-conn",
            LastConnectedAt = DateTime.UtcNow
        };

        var room = CreateTestRoom(roomId, 4);
        room.Players[playerId] = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = "Player",
            Position = PlayerPosition.South,
            IsConnected = false,
            DisconnectedAt = DateTime.UtcNow.AddSeconds(-10) // 10 saniye önce koptu
        };

        _mockStateService
            .Setup(x => x.GetConnectionMappingAsync(playerId))
            .ReturnsAsync(mapping);
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);
        
        _mockStateService
            .Setup(x => x.SaveRoomStateAsync(It.IsAny<GameRoomState>()))
            .Returns(Task.CompletedTask);
        
        _mockStateService
            .Setup(x => x.SaveConnectionMappingAsync(playerId, roomId, newConnectionId))
            .Returns(Task.CompletedTask);

        // Act
        var (success, resultRoomId) = await _gameService.TryReconnectAsync(playerId, newConnectionId);

        // Assert
        Assert.True(success);
        Assert.Equal(roomId, resultRoomId);
    }

    [Fact]
    public async Task TryReconnect_ShouldFail_AfterTimeout()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        var mapping = new ConnectionMapping
        {
            PlayerId = playerId,
            RoomId = roomId,
            LastConnectionId = "old-conn",
            LastConnectedAt = DateTime.UtcNow
        };

        var room = CreateTestRoom(roomId, 4);
        room.Players[playerId] = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = "Player",
            Position = PlayerPosition.South,
            IsConnected = false,
            DisconnectedAt = DateTime.UtcNow.AddSeconds(-60) // 60 saniye önce koptu (timeout aşıldı)
        };

        _mockStateService
            .Setup(x => x.GetConnectionMappingAsync(playerId))
            .ReturnsAsync(mapping);
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);

        // Act
        var (success, resultRoomId) = await _gameService.TryReconnectAsync(playerId, "new-conn");

        // Assert
        Assert.False(success);
        Assert.Null(resultRoomId);
    }

    [Fact]
    public async Task TryReconnect_ShouldFail_WhenNoMapping()
    {
        // Arrange
        var playerId = Guid.NewGuid();

        _mockStateService
            .Setup(x => x.GetConnectionMappingAsync(playerId))
            .ReturnsAsync((ConnectionMapping?)null);

        // Act
        var (success, resultRoomId) = await _gameService.TryReconnectAsync(playerId, "new-conn");

        // Assert
        Assert.False(success);
        Assert.Null(resultRoomId);
    }

    #endregion

    #region Taş Çekme Testleri

    [Fact]
    public async Task DrawTile_ShouldFail_WhenNotPlayersTurn()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var room = CreateTestRoom(roomId, 4);
        room.State = GameState.InProgress;
        room.DeckTileIds = new List<int> { 1, 2, 3, 4, 5 };
        
        // Oyuncuyu ekle ama sırası değil
        room.Players[playerId] = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = "Player",
            Position = PlayerPosition.North,
            IsCurrentTurn = false,
            HandTileIds = new List<int>()
        };

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);

        // Act
        var result = await _gameService.DrawTileAsync(roomId, playerId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("sıra", result.ErrorMessage!.ToLower());
    }

    [Fact]
    public async Task DrawTile_ShouldFail_WhenAlreadyDrewThisTurn()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var room = CreateTestRoom(roomId, 4);
        room.State = GameState.InProgress;
        room.DeckTileIds = new List<int> { 1, 2, 3, 4, 5 };
        
        room.Players[playerId] = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = "Player",
            Position = PlayerPosition.South,
            IsCurrentTurn = true,
            HasDrawnThisTurn = true, // Zaten çekmiş
            HandTileIds = new List<int>()
        };

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);

        // Act
        var result = await _gameService.DrawTileAsync(roomId, playerId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("zaten", result.ErrorMessage!.ToLower());
    }

    #endregion

    #region Taş Atma Testleri

    [Fact]
    public async Task DiscardTile_ShouldFail_WhenTileNotInHand()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var room = CreateTestRoom(roomId, 4);
        room.State = GameState.InProgress;
        
        room.Players[playerId] = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = "Player",
            Position = PlayerPosition.South,
            IsCurrentTurn = true,
            HandTileIds = new List<int> { 1, 2, 3 } // 4 yok
        };

        _mockStateService
            .Setup(x => x.AcquireLockAsync(roomId, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new FakeLock());
        
        _mockStateService
            .Setup(x => x.GetRoomStateAsync(roomId))
            .ReturnsAsync(room);

        // Act
        var result = await _gameService.DiscardTileAsync(roomId, playerId, 4);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("yok", result.ErrorMessage!.ToLower());
    }

    #endregion

    #region Yardımcı Metodlar

    private static GameRoomState CreateTestRoom(Guid roomId, int playerCount)
    {
        var room = new GameRoomState
        {
            RoomId = roomId,
            RoomName = "Test Room",
            State = GameState.WaitingForPlayers
        };

        for (int i = 0; i < playerCount; i++)
        {
            var playerId = Guid.NewGuid();
            room.Players[playerId] = new PlayerState
            {
                PlayerId = playerId,
                DisplayName = $"Player{i + 1}",
                Position = (PlayerPosition)i,
                IsConnected = true,
                ConnectionId = $"conn-{i}"
            };
        }

        return room;
    }

    private class FakeLock : IDisposable
    {
        public void Dispose() { }
    }

    #endregion
}
