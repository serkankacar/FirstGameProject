using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using Xunit;

namespace OkeyGame.Tests.Persistence;

/// <summary>
/// GameHistory entity testleri.
/// </summary>
public class GameHistoryTests
{
    #region Create Testleri

    [Fact]
    public void Create_ValidParameters_CreatesGameHistory()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        long tableStake = 1000;

        // Act
        var history = GameHistory.Create(roomId, tableStake);

        // Assert
        Assert.NotEqual(Guid.Empty, history.Id);
        Assert.Equal(roomId, history.RoomId);
        Assert.Equal(tableStake, history.TableStake);
        Assert.Equal(GameHistoryStatus.InProgress, history.Status);
        Assert.Equal(0, history.TotalTurns);
        Assert.Null(history.WinnerId);
    }

    [Fact]
    public void Create_WithServerSeedHash_StoresHash()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var serverSeedHash = "abc123hash";

        // Act
        var history = GameHistory.Create(roomId, 1000, serverSeedHash);

        // Assert
        Assert.Equal(serverSeedHash, history.ServerSeedHash);
    }

    [Fact]
    public void TotalPot_CalculatesCorrectly()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);

        // Assert (4 oyuncu * 1000 = 4000)
        Assert.Equal(4000, history.TotalPot);
    }

    #endregion

    #region Durum Güncellemeleri Testleri

    [Fact]
    public void IncrementTurn_IncreasesTurnCount()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);

        // Act
        history.IncrementTurn();
        history.IncrementTurn();
        history.IncrementTurn();

        // Assert
        Assert.Equal(3, history.TotalTurns);
    }

    [Fact]
    public void Complete_ValidParameters_CompletesGame()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);
        var winnerId = Guid.NewGuid();

        // Act
        history.Complete(
            winnerId: winnerId,
            winnerUsername: "winner",
            winType: WinType.Normal,
            winScore: 100,
            rakeAmount: 200,
            playerResultsJson: "[]");

        // Assert
        Assert.Equal(GameHistoryStatus.Completed, history.Status);
        Assert.Equal(winnerId, history.WinnerId);
        Assert.Equal("winner", history.WinnerUsername);
        Assert.Equal(WinType.Normal, history.WinType);
        Assert.Equal(100, history.WinScore);
        Assert.Equal(200, history.RakeAmount);
        Assert.NotNull(history.EndedAt);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);
        history.Complete(Guid.NewGuid(), "winner", WinType.Normal, 100, 200, "[]");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            history.Complete(Guid.NewGuid(), "winner2", WinType.Pairs, 200, 100, "[]"));
    }

    [Fact]
    public void Cancel_InProgress_CancelsGame()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);

        // Act
        history.Cancel("Player disconnected");

        // Assert
        Assert.Equal(GameHistoryStatus.Cancelled, history.Status);
        Assert.NotNull(history.EndedAt);
        Assert.Contains("cancelReason", history.PlayerResultsJson);
    }

    [Fact]
    public void Cancel_AlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);
        history.Complete(Guid.NewGuid(), "winner", WinType.Normal, 100, 200, "[]");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => history.Cancel("Test reason"));
    }

    [Fact]
    public void Timeout_InProgress_SetsTimeout()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);

        // Act
        history.Timeout();

        // Assert
        Assert.Equal(GameHistoryStatus.Timeout, history.Status);
        Assert.NotNull(history.EndedAt);
    }

    #endregion

    #region Provably Fair Testleri

    [Fact]
    public void SetClientSeed_ValidSeed_SetsSeed()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);

        // Act
        history.SetClientSeed("client_seed_123");

        // Assert
        Assert.Equal("client_seed_123", history.ClientSeed);
    }

    [Fact]
    public void SetClientSeed_EmptySeed_ThrowsArgumentException()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => history.SetClientSeed(""));
    }

    [Fact]
    public void RevealGameSeed_AfterComplete_RevealsSeed()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);
        history.Complete(Guid.NewGuid(), "winner", WinType.Normal, 100, 200, "[]");

        // Act
        history.RevealGameSeed("revealed_seed");

        // Assert
        Assert.Equal("revealed_seed", history.GameSeed);
    }

    [Fact]
    public void RevealGameSeed_WhileInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => history.RevealGameSeed("seed"));
    }

    #endregion

    #region WinnerPayout Testleri

    [Fact]
    public void WinnerPayout_CalculatesCorrectly()
    {
        // Arrange
        var history = GameHistory.Create(Guid.NewGuid(), 1000);
        history.Complete(Guid.NewGuid(), "winner", WinType.Normal, 100, 200, "[]");

        // Assert
        // TotalPot = 4000, RakeAmount = 200, WinnerPayout = 3800
        Assert.Equal(3800, history.WinnerPayout);
    }

    #endregion
}
