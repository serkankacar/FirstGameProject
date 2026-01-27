using OkeyGame.API.Models;
using OkeyGame.Domain.Enums;
using System.Text.Json;
using Xunit;

namespace OkeyGame.Tests.API;

/// <summary>
/// GameRoomState modeli testleri.
/// </summary>
public class GameRoomStateTests
{
    [Fact]
    public void GameRoomState_ShouldSerializeAndDeserialize_Correctly()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        
        var state = new GameRoomState
        {
            RoomId = roomId,
            RoomName = "Test Room",
            State = GameState.InProgress,
            DeckTileIds = new List<int> { 1, 2, 3, 4, 5 },
            DiscardPileTileIds = new List<int> { 6, 7 },
            IndicatorTileId = 10,
            CommitmentHash = "abc123",
            CurrentTurnPosition = PlayerPosition.East
        };

        state.Players[playerId] = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = "Player1",
            Position = PlayerPosition.South,
            HandTileIds = new List<int> { 11, 12, 13 },
            IsConnected = true,
            IsCurrentTurn = true
        };

        // Act
        var json = JsonSerializer.Serialize(state);
        var deserialized = JsonSerializer.Deserialize<GameRoomState>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(roomId, deserialized.RoomId);
        Assert.Equal("Test Room", deserialized.RoomName);
        Assert.Equal(GameState.InProgress, deserialized.State);
        Assert.Equal(5, deserialized.DeckTileIds.Count);
        Assert.Equal(2, deserialized.DiscardPileTileIds.Count);
        Assert.Equal(10, deserialized.IndicatorTileId);
        Assert.Equal("abc123", deserialized.CommitmentHash);
        Assert.Equal(PlayerPosition.East, deserialized.CurrentTurnPosition);
        Assert.Single(deserialized.Players);
        Assert.True(deserialized.Players.ContainsKey(playerId));
        Assert.Equal(3, deserialized.Players[playerId].HandTileIds.Count);
    }

    [Fact]
    public void PlayerState_ShouldTrackConnectionTimes()
    {
        // Arrange
        var now = DateTime.UtcNow;
        
        var playerState = new PlayerState
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "TestPlayer",
            Position = PlayerPosition.South,
            IsConnected = true,
            LastConnectedAt = now,
            DisconnectedAt = null
        };

        // Act - Disconnect
        playerState.IsConnected = false;
        playerState.DisconnectedAt = now.AddSeconds(5);

        // Assert
        Assert.False(playerState.IsConnected);
        Assert.NotNull(playerState.DisconnectedAt);
        Assert.Equal(5, (playerState.DisconnectedAt.Value - playerState.LastConnectedAt!.Value).TotalSeconds);
    }

    [Fact]
    public void ConnectionMapping_ShouldStorePlayerRoomRelation()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var connectionId = "conn-123";

        // Act
        var mapping = new ConnectionMapping
        {
            PlayerId = playerId,
            RoomId = roomId,
            LastConnectionId = connectionId,
            LastConnectedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(playerId, mapping.PlayerId);
        Assert.Equal(roomId, mapping.RoomId);
        Assert.Equal(connectionId, mapping.LastConnectionId);
    }

    [Fact]
    public void GameRoomState_ShouldCalculateCorrectPositions()
    {
        // Arrange
        var state = new GameRoomState
        {
            RoomId = Guid.NewGuid(),
            RoomName = "Test"
        };

        // Act - Add players in order
        var positions = new[] { PlayerPosition.South, PlayerPosition.East, PlayerPosition.North, PlayerPosition.West };
        
        foreach (var pos in positions)
        {
            var playerId = Guid.NewGuid();
            state.Players[playerId] = new PlayerState
            {
                PlayerId = playerId,
                DisplayName = $"Player_{pos}",
                Position = pos
            };
        }

        // Assert
        Assert.Equal(4, state.Players.Count);
        
        var usedPositions = state.Players.Values.Select(p => p.Position).ToHashSet();
        Assert.Equal(4, usedPositions.Count);
        Assert.Contains(PlayerPosition.South, usedPositions);
        Assert.Contains(PlayerPosition.East, usedPositions);
        Assert.Contains(PlayerPosition.North, usedPositions);
        Assert.Contains(PlayerPosition.West, usedPositions);
    }
}
