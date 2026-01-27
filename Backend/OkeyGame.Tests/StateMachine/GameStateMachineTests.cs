using OkeyGame.Domain.Enums;
using OkeyGame.Domain.StateMachine;
using Xunit;

namespace OkeyGame.Tests.StateMachine;

/// <summary>
/// GameStateMachine birim testleri.
/// </summary>
public class GameStateMachineTests
{
    private readonly GameStateMachine _stateMachine = GameStateMachine.Instance;

    #region Geçerli Geçişler Testleri

    [Theory]
    [InlineData(GamePhase.WaitingForPlayers, GamePhase.ReadyToStart)]
    [InlineData(GamePhase.ReadyToStart, GamePhase.Shuffling)]
    [InlineData(GamePhase.ReadyToStart, GamePhase.WaitingForPlayers)]
    [InlineData(GamePhase.Shuffling, GamePhase.Dealing)]
    [InlineData(GamePhase.Dealing, GamePhase.Playing)]
    [InlineData(GamePhase.Playing, GamePhase.Finished)]
    public void CanTransition_ValidTransitions_ReturnsTrue(GamePhase from, GamePhase to)
    {
        // Act
        var result = _stateMachine.CanTransition(from, to);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(GamePhase.WaitingForPlayers, GamePhase.Cancelled)]
    [InlineData(GamePhase.ReadyToStart, GamePhase.Cancelled)]
    [InlineData(GamePhase.Shuffling, GamePhase.Cancelled)]
    [InlineData(GamePhase.Dealing, GamePhase.Cancelled)]
    [InlineData(GamePhase.Playing, GamePhase.Cancelled)]
    public void CanTransition_CancellationFromAnyState_ReturnsTrue(GamePhase from, GamePhase to)
    {
        // Act
        var result = _stateMachine.CanTransition(from, to);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(GamePhase.WaitingForPlayers, GamePhase.WaitingForPlayers)]
    [InlineData(GamePhase.Playing, GamePhase.Playing)]
    [InlineData(GamePhase.Finished, GamePhase.Finished)]
    public void CanTransition_SameState_ReturnsTrue(GamePhase state, GamePhase sameState)
    {
        // Act
        var result = _stateMachine.CanTransition(state, sameState);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Geçersiz Geçişler Testleri

    [Theory]
    [InlineData(GamePhase.WaitingForPlayers, GamePhase.Playing)]
    [InlineData(GamePhase.WaitingForPlayers, GamePhase.Finished)]
    [InlineData(GamePhase.ReadyToStart, GamePhase.Playing)]
    [InlineData(GamePhase.Playing, GamePhase.ReadyToStart)]
    [InlineData(GamePhase.Finished, GamePhase.Playing)]
    [InlineData(GamePhase.Cancelled, GamePhase.Playing)]
    public void CanTransition_InvalidTransitions_ReturnsFalse(GamePhase from, GamePhase to)
    {
        // Act
        var result = _stateMachine.CanTransition(from, to);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanTransition_FinishedToCancelled_ReturnsFalse()
    {
        // Finished durumundan Cancelled'a geçilemez (zaten terminal)
        var result = _stateMachine.CanTransition(GamePhase.Finished, GamePhase.Cancelled);
        
        Assert.False(result);
    }

    #endregion

    #region Transition Metodu Testleri

    [Fact]
    public void Transition_ValidTransition_ReturnsNewState()
    {
        // Act
        var result = _stateMachine.Transition(GamePhase.WaitingForPlayers, GamePhase.ReadyToStart);

        // Assert
        Assert.Equal(GamePhase.ReadyToStart, result);
    }

    [Fact]
    public void Transition_InvalidTransition_ThrowsException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _stateMachine.Transition(GamePhase.WaitingForPlayers, GamePhase.Playing));

        Assert.Contains("Geçersiz oyun durumu geçişi", exception.Message);
    }

    #endregion

    #region TryTransition Testleri

    [Fact]
    public void TryTransition_ValidTransition_ReturnsSuccessResult()
    {
        // Act
        var result = _stateMachine.TryTransition(
            GamePhase.Dealing, GamePhase.Playing, "Oyun başlıyor");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GamePhase.Dealing, result.FromPhase);
        Assert.Equal(GamePhase.Playing, result.ToPhase);
        Assert.Equal("Oyun başlıyor", result.Message);
    }

    [Fact]
    public void TryTransition_InvalidTransition_ReturnsFailureResult()
    {
        // Act
        var result = _stateMachine.TryTransition(
            GamePhase.WaitingForPlayers, GamePhase.Finished);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Geçersiz geçiş", result.Message);
    }

    #endregion

    #region Durum Sorguları Testleri

    [Theory]
    [InlineData(GamePhase.Playing, true)]
    [InlineData(GamePhase.WaitingForPlayers, false)]
    [InlineData(GamePhase.Finished, false)]
    public void IsActivePhase_ReturnsCorrectValue(GamePhase phase, bool expected)
    {
        // Act
        var result = _stateMachine.IsActivePhase(phase);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(GamePhase.WaitingForPlayers, true)]
    [InlineData(GamePhase.ReadyToStart, true)]
    [InlineData(GamePhase.Shuffling, true)]
    [InlineData(GamePhase.Dealing, true)]
    [InlineData(GamePhase.Playing, false)]
    [InlineData(GamePhase.Finished, false)]
    public void IsSetupPhase_ReturnsCorrectValue(GamePhase phase, bool expected)
    {
        // Act
        var result = _stateMachine.IsSetupPhase(phase);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(GamePhase.Finished, true)]
    [InlineData(GamePhase.Cancelled, true)]
    [InlineData(GamePhase.Playing, false)]
    [InlineData(GamePhase.WaitingForPlayers, false)]
    public void IsTerminalPhase_ReturnsCorrectValue(GamePhase phase, bool expected)
    {
        // Act
        var result = _stateMachine.IsTerminalPhase(phase);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RequiresTurnManagement_OnlyPlayingPhase_ReturnsTrue()
    {
        // Assert
        Assert.True(_stateMachine.RequiresTurnManagement(GamePhase.Playing));
        Assert.False(_stateMachine.RequiresTurnManagement(GamePhase.WaitingForPlayers));
        Assert.False(_stateMachine.RequiresTurnManagement(GamePhase.Finished));
    }

    #endregion

    #region GetPossibleTransitions Testleri

    [Fact]
    public void GetPossibleTransitions_WaitingForPlayers_ReturnsValidOptions()
    {
        // Act
        var transitions = _stateMachine.GetPossibleTransitions(GamePhase.WaitingForPlayers);

        // Assert
        Assert.Contains(GamePhase.ReadyToStart, transitions);
        Assert.Contains(GamePhase.Cancelled, transitions);
        Assert.Equal(2, transitions.Count);
    }

    [Fact]
    public void GetPossibleTransitions_Playing_ReturnsValidOptions()
    {
        // Act
        var transitions = _stateMachine.GetPossibleTransitions(GamePhase.Playing);

        // Assert
        Assert.Contains(GamePhase.Finished, transitions);
        Assert.Contains(GamePhase.Cancelled, transitions);
    }

    [Fact]
    public void GetPossibleTransitions_Finished_ReturnsEmpty()
    {
        // Act
        var transitions = _stateMachine.GetPossibleTransitions(GamePhase.Finished);

        // Assert
        Assert.Empty(transitions);
    }

    #endregion

    #region Tur Fazı Geçişleri Testleri

    [Theory]
    [InlineData(TurnPhase.WaitingForDraw, TurnPhase.WaitingForDiscard)]
    [InlineData(TurnPhase.WaitingForDiscard, TurnPhase.TurnCompleted)]
    [InlineData(TurnPhase.TurnCompleted, TurnPhase.WaitingForDraw)]
    public void CanTransitionTurn_ValidTransitions_ReturnsTrue(TurnPhase from, TurnPhase to)
    {
        // Act
        var result = _stateMachine.CanTransitionTurn(from, to);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(TurnPhase.WaitingForDraw, TurnPhase.TurnCompleted)]
    [InlineData(TurnPhase.WaitingForDiscard, TurnPhase.WaitingForDraw)]
    public void CanTransitionTurn_InvalidTransitions_ReturnsFalse(TurnPhase from, TurnPhase to)
    {
        // Act
        var result = _stateMachine.CanTransitionTurn(from, to);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TransitionTurn_InvalidTransition_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _stateMachine.TransitionTurn(TurnPhase.WaitingForDraw, TurnPhase.TurnCompleted));
    }

    #endregion

    #region Singleton Testi

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Act
        var instance1 = GameStateMachine.Instance;
        var instance2 = GameStateMachine.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    #endregion
}
