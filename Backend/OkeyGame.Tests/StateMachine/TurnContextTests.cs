using OkeyGame.Domain.Enums;
using OkeyGame.Domain.StateMachine;
using Xunit;

namespace OkeyGame.Tests.StateMachine;

/// <summary>
/// TurnContext birim testleri.
/// </summary>
public class TurnContextTests
{
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();

    #region Factory Metod Testleri

    [Fact]
    public void StartNew_CreatesValidContext()
    {
        // Act
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Assert
        Assert.Equal(_roomId, context.RoomId);
        Assert.Equal(_playerId, context.CurrentPlayerId);
        Assert.Equal(PlayerPosition.South, context.CurrentPosition);
        Assert.Equal(1, context.TurnNumber);
        Assert.Equal(TurnPhase.WaitingForDraw, context.Phase);
        Assert.False(context.HasDrawnTile);
        Assert.False(context.DrewFromDiscard);
        Assert.False(context.IsBot);
        Assert.True(context.IsConnected);
        Assert.False(context.IsAutoPlay);
    }

    [Fact]
    public void StartFirst_CreatesContextWithDiscard()
    {
        // İlk oyuncu 15 taş aldığı için doğrudan atma fazındadır

        // Act
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Assert
        Assert.Equal(TurnPhase.WaitingForDiscard, context.Phase);
        Assert.True(context.HasDrawnTile);
        Assert.Equal(1, context.TurnNumber);
    }

    [Fact]
    public void StartNew_WithCustomDuration_SetsCorrectExpiration()
    {
        // Arrange
        int duration = 30;

        // Act
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1,
            turnDurationSeconds: duration);

        // Assert
        Assert.Equal(duration, context.TotalTurnDurationSeconds);
    }

    [Fact]
    public void StartNew_WithBot_SetsIsBot()
    {
        // Act
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1, isBot: true);

        // Assert
        Assert.True(context.IsBot);
    }

    #endregion

    #region Hesaplanmış Özellikler Testleri

    [Fact]
    public void RemainingSeconds_FreshContext_ReturnsPositive()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1,
            turnDurationSeconds: 15);

        // Act & Assert
        Assert.True(context.RemainingSeconds > 0);
        Assert.True(context.RemainingSecondsInt > 0);
    }

    [Fact]
    public void IsExpired_FreshContext_ReturnsFalse()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Assert
        Assert.False(context.IsExpired);
    }

    [Fact]
    public void CanDraw_WaitingForDraw_ReturnsTrue()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Assert
        Assert.True(context.CanDraw);
        Assert.False(context.CanDiscard);
    }

    [Fact]
    public void CanDiscard_WaitingForDiscard_ReturnsTrue()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Assert
        Assert.False(context.CanDraw);
        Assert.True(context.CanDiscard);
    }

    [Fact]
    public void IsCompleted_TurnCompleted_ReturnsTrue()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South)
            .WithTileDiscarded();

        // Assert
        Assert.True(context.IsCompleted);
    }

    #endregion

    #region Immutable Güncelleme Testleri

    [Fact]
    public void WithTileDrawn_UpdatesPhaseAndFlags()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var updated = context.WithTileDrawn(fromDiscard: true);

        // Assert
        Assert.Equal(TurnPhase.WaitingForDiscard, updated.Phase);
        Assert.True(updated.HasDrawnTile);
        Assert.True(updated.DrewFromDiscard);
        
        // Orijinal değişmemeli
        Assert.Equal(TurnPhase.WaitingForDraw, context.Phase);
        Assert.False(context.HasDrawnTile);
    }

    [Fact]
    public void WithTileDiscarded_UpdatesPhase()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Act
        var updated = context.WithTileDiscarded();

        // Assert
        Assert.Equal(TurnPhase.TurnCompleted, updated.Phase);
    }

    [Fact]
    public void WithAutoPlay_SetsAutoPlayFlag()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var updated = context.WithAutoPlay();

        // Assert
        Assert.True(updated.IsAutoPlay);
        Assert.False(context.IsAutoPlay);
    }

    [Fact]
    public void WithExtendedTime_ExtendsExpiration()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1,
            turnDurationSeconds: 10);
        var originalExpiration = context.TurnExpiresAt;

        // Act
        var updated = context.WithExtendedTime(5);

        // Assert
        Assert.True(updated.TurnExpiresAt > originalExpiration);
        Assert.Equal(5, (updated.TurnExpiresAt - originalExpiration).TotalSeconds, precision: 1);
    }

    [Fact]
    public void WithConnectionStatus_UpdatesFlag()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var disconnected = context.WithConnectionStatus(false);
        var reconnected = disconnected.WithConnectionStatus(true);

        // Assert
        Assert.False(disconnected.IsConnected);
        Assert.True(reconnected.IsConnected);
    }

    #endregion

    #region IsPlayerTurn Testleri

    [Fact]
    public void IsPlayerTurn_CorrectPlayer_ReturnsTrue()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Assert
        Assert.True(context.IsPlayerTurn(_playerId));
    }

    [Fact]
    public void IsPlayerTurn_WrongPlayer_ReturnsFalse()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);
        var otherPlayerId = Guid.NewGuid();

        // Assert
        Assert.False(context.IsPlayerTurn(otherPlayerId));
    }

    #endregion

    #region ValidateAction Testleri

    [Fact]
    public void ValidateAction_DrawWhenCanDraw_ReturnsValid()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var result = context.ValidateAction(TurnAction.DrawFromDeck, _playerId);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAction_DrawWhenCannotDraw_ReturnsInvalid()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Act
        var result = context.ValidateAction(TurnAction.DrawFromDeck, _playerId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(TurnValidationError.InvalidAction, result.Error);
    }

    [Fact]
    public void ValidateAction_DiscardWhenCanDiscard_ReturnsValid()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Act
        var result = context.ValidateAction(TurnAction.Discard, _playerId);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAction_DiscardWithoutDraw_ReturnsInvalid()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);

        // Act
        var result = context.ValidateAction(TurnAction.Discard, _playerId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(TurnValidationError.InvalidAction, result.Error);
        Assert.Contains("taş çekme", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAction_WrongPlayer_ReturnsNotYourTurn()
    {
        // Arrange
        var context = TurnContext.StartNew(
            _roomId, _playerId, PlayerPosition.South, 1);
        var otherPlayerId = Guid.NewGuid();

        // Act
        var result = context.ValidateAction(TurnAction.DrawFromDeck, otherPlayerId);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(TurnValidationError.NotYourTurn, result.Error);
        Assert.Equal(_playerId, result.CorrectPlayerId);
    }

    [Fact]
    public void ValidateAction_DeclareWin_ValidWhenCanDiscard()
    {
        // Arrange
        var context = TurnContext.StartFirst(
            _roomId, _playerId, PlayerPosition.South);

        // Act
        var result = context.ValidateAction(TurnAction.DeclareWin, _playerId);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Sabitler Testleri

    [Fact]
    public void DefaultTurnDuration_Is15Seconds()
    {
        Assert.Equal(15, TurnContext.DefaultTurnDurationSeconds);
    }

    [Fact]
    public void MinTurnDuration_Is5Seconds()
    {
        Assert.Equal(5, TurnContext.MinTurnDurationSeconds);
    }

    [Fact]
    public void MaxTurnDuration_Is60Seconds()
    {
        Assert.Equal(60, TurnContext.MaxTurnDurationSeconds);
    }

    #endregion
}
