using OkeyGame.Domain.Enums;
using OkeyGame.Domain.StateMachine;
using Xunit;

namespace OkeyGame.Tests.StateMachine;

/// <summary>
/// TurnManager birim testleri.
/// </summary>
public class TurnManagerTests
{
    private readonly TurnManager _turnManager = TurnManager.Instance;
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();

    #region Singleton Testi

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Act
        var instance1 = TurnManager.Instance;
        var instance2 = TurnManager.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    #endregion

    #region StartFirstTurn Testleri

    [Fact]
    public void StartFirstTurn_CreatesValidContext()
    {
        // Act
        var context = _turnManager.StartFirstTurn(
            _roomId, _playerId, PlayerPosition.South);

        // Assert
        Assert.Equal(_roomId, context.RoomId);
        Assert.Equal(_playerId, context.CurrentPlayerId);
        Assert.Equal(PlayerPosition.South, context.CurrentPosition);
        Assert.Equal(1, context.TurnNumber);
        Assert.Equal(TurnPhase.WaitingForDiscard, context.Phase);
        Assert.True(context.HasDrawnTile);
    }

    [Fact]
    public void StartFirstTurn_WithBot_SetsIsBot()
    {
        // Act
        var context = _turnManager.StartFirstTurn(
            _roomId, _playerId, PlayerPosition.South, isBot: true);

        // Assert
        Assert.True(context.IsBot);
    }

    [Fact]
    public void StartFirstTurn_WithCustomDuration_SetsCorrectExpiration()
    {
        // Act
        var context = _turnManager.StartFirstTurn(
            _roomId, _playerId, PlayerPosition.South,
            turnDurationSeconds: 30);

        // Assert
        Assert.Equal(30, context.TotalTurnDurationSeconds);
    }

    #endregion

    #region StartNextTurn Testleri

    [Fact]
    public void StartNextTurn_IncrementsNumberAndResetsFase()
    {
        // Arrange
        var previousContext = _turnManager.StartFirstTurn(
            _roomId, _playerId, PlayerPosition.South);
        var nextPlayerId = Guid.NewGuid();

        // Act
        var context = _turnManager.StartNextTurn(
            previousContext, nextPlayerId, PlayerPosition.West);

        // Assert
        Assert.Equal(2, context.TurnNumber);
        Assert.Equal(TurnPhase.WaitingForDraw, context.Phase);
        Assert.False(context.HasDrawnTile);
        Assert.Equal(nextPlayerId, context.CurrentPlayerId);
        Assert.Equal(PlayerPosition.West, context.CurrentPosition);
    }

    [Fact]
    public void StartNextTurn_NullPreviousContext_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _turnManager.StartNextTurn(null!, Guid.NewGuid(), PlayerPosition.West));
    }

    #endregion

    #region ProcessDraw Testleri

    [Fact]
    public void ProcessDraw_ValidAction_ReturnsSuccess()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var result = _turnManager.ProcessDraw(context, _playerId, fromDiscard: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Context);
        Assert.Equal(TurnPhase.WaitingForDiscard, result.Context.Phase);
        Assert.True(result.Context.HasDrawnTile);
        Assert.False(result.Context.DrewFromDiscard);
    }

    [Fact]
    public void ProcessDraw_FromDiscard_SetsFlag()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var result = _turnManager.ProcessDraw(context, _playerId, fromDiscard: true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Context!.DrewFromDiscard);
    }

    [Fact]
    public void ProcessDraw_WrongPlayer_ReturnsFailure()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);
        var wrongPlayerId = Guid.NewGuid();

        // Act
        var result = _turnManager.ProcessDraw(context, wrongPlayerId, fromDiscard: false);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("sıra", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessDraw_AlreadyDrawn_ReturnsFailure()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Act
        var result = _turnManager.ProcessDraw(context, _playerId, fromDiscard: false);

        // Assert
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region ProcessDiscard Testleri

    [Fact]
    public void ProcessDiscard_ValidAction_ReturnsSuccess()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Act
        var result = _turnManager.ProcessDiscard(context, _playerId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Context);
        Assert.Equal(TurnPhase.TurnCompleted, result.Context.Phase);
    }

    [Fact]
    public void ProcessDiscard_WithoutDraw_ReturnsFailure()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var result = _turnManager.ProcessDiscard(context, _playerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("çek", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessDiscard_WrongPlayer_ReturnsFailure()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);
        var wrongPlayerId = Guid.NewGuid();

        // Act
        var result = _turnManager.ProcessDiscard(context, wrongPlayerId);

        // Assert
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region ProcessWinDeclaration Testleri

    [Fact]
    public void ProcessWinDeclaration_ValidAction_ReturnsSuccessWithWinning()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Act
        var result = _turnManager.ProcessWinDeclaration(context, _playerId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.IsWinning);
        Assert.Equal(TurnPhase.TurnCompleted, result.Context!.Phase);
    }

    [Fact]
    public void ProcessWinDeclaration_WithoutDraw_ReturnsFailure()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var result = _turnManager.ProcessWinDeclaration(context, _playerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(result.IsWinning);
    }

    #endregion

    #region ProcessTimeout Testleri

    [Fact]
    public void ProcessTimeout_SetsAutoPlayFlag()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var updated = _turnManager.ProcessTimeout(context);

        // Assert
        Assert.True(updated.IsAutoPlay);
        Assert.False(context.IsAutoPlay); // Orijinal değişmemeli
    }

    [Fact]
    public void IsTimeout_NotExpired_ReturnsFalse()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1,
            turnDurationSeconds: 60);

        // Act
        var result = _turnManager.IsTimeout(context);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetRemainingTime Testleri

    [Fact]
    public void GetRemainingTime_FreshContext_ReturnsPositive()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1,
            turnDurationSeconds: 15);

        // Act
        var remaining = _turnManager.GetRemainingTime(context);

        // Assert
        Assert.True(remaining > TimeSpan.Zero);
        Assert.True(remaining <= TimeSpan.FromSeconds(15));
    }

    #endregion

    #region Pozisyon Yönetimi Testleri

    [Theory]
    [InlineData(PlayerPosition.South, PlayerPosition.West)]
    [InlineData(PlayerPosition.West, PlayerPosition.North)]
    [InlineData(PlayerPosition.North, PlayerPosition.East)]
    [InlineData(PlayerPosition.East, PlayerPosition.South)]
    public void GetNextPosition_ReturnsCorrectPosition(PlayerPosition current, PlayerPosition expected)
    {
        // Act
        var next = _turnManager.GetNextPosition(current);

        // Assert
        Assert.Equal(expected, next);
    }

    [Theory]
    [InlineData(PlayerPosition.South, 0, PlayerPosition.South)]
    [InlineData(PlayerPosition.South, 1, PlayerPosition.West)]
    [InlineData(PlayerPosition.South, 2, PlayerPosition.North)]
    [InlineData(PlayerPosition.South, 3, PlayerPosition.East)]
    [InlineData(PlayerPosition.South, 4, PlayerPosition.South)]
    public void GetPositionAfter_ReturnsCorrectPosition(
        PlayerPosition current, int steps, PlayerPosition expected)
    {
        // Act
        var result = _turnManager.GetPositionAfter(current, steps);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(PlayerPosition.South, PlayerPosition.South, 0)]
    [InlineData(PlayerPosition.South, PlayerPosition.West, 1)]
    [InlineData(PlayerPosition.South, PlayerPosition.North, 2)]
    [InlineData(PlayerPosition.South, PlayerPosition.East, 3)]
    [InlineData(PlayerPosition.East, PlayerPosition.South, 1)]
    public void GetPositionDistance_ReturnsCorrectDistance(
        PlayerPosition from, PlayerPosition to, int expected)
    {
        // Act
        var distance = _turnManager.GetPositionDistance(from, to);

        // Assert
        Assert.Equal(expected, distance);
    }

    #endregion

    #region Reconnection Yönetimi Testleri

    [Fact]
    public void HandleReconnection_LowTime_ExtendsTimer()
    {
        // Arrange - 2 saniye kalan context
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1,
            turnDurationSeconds: 2);

        // Biraz bekle
        Thread.Sleep(100);

        // Act
        var updated = _turnManager.HandleReconnection(context, additionalSeconds: 5);

        // Assert
        Assert.True(updated.IsConnected);
        Assert.True(updated.TurnExpiresAt > context.TurnExpiresAt);
    }

    [Fact]
    public void HandleReconnection_EnoughTime_OnlyUpdatesConnection()
    {
        // Arrange - Yeterli süreli context
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1,
            turnDurationSeconds: 60)
            .WithConnectionStatus(false);

        // Act
        var updated = _turnManager.HandleReconnection(context);

        // Assert
        Assert.True(updated.IsConnected);
        Assert.Equal(context.TurnExpiresAt, updated.TurnExpiresAt);
    }

    [Fact]
    public void HandleDisconnection_UpdatesConnectionStatus()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var updated = _turnManager.HandleDisconnection(context);

        // Assert
        Assert.False(updated.IsConnected);
    }

    #endregion

    #region Sabitler Testleri

    [Fact]
    public void Constants_HaveCorrectValues()
    {
        Assert.Equal(15, TurnManager.DefaultTurnDurationSeconds);
        Assert.Equal(5, TurnManager.ReconnectionGraceSeconds);
        Assert.Equal(1000, TurnManager.BotMinDelayMs);
        Assert.Equal(3000, TurnManager.BotMaxDelayMs);
    }

    #endregion
}
