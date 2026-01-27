using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.StateMachine;

/// <summary>
/// Okey oyunu için State Machine (Durum Makinesi).
/// 
/// Geçerli durum geçişlerini tanımlar ve yönetir.
/// Thread-safe ve immutable tasarım.
/// 
/// DURUM GEÇİŞLERİ:
/// ┌─────────────────────────────────────────────────────────────────┐
/// │  WaitingForPlayers ──► ReadyToStart ──► Shuffling              │
/// │                                              │                  │
/// │                                              ▼                  │
/// │                                          Dealing                │
/// │                                              │                  │
/// │                                              ▼                  │
/// │                                          Playing ──► Finished   │
/// │                                              │                  │
/// │                                              ▼                  │
/// │  [Her durumdan] ─────────────────────► Cancelled               │
/// └─────────────────────────────────────────────────────────────────┘
/// </summary>
public sealed class GameStateMachine
{
    #region Singleton

    private static readonly Lazy<GameStateMachine> _instance = new(() => new GameStateMachine());
    
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static GameStateMachine Instance => _instance.Value;

    #endregion

    #region Geçerli Geçişler

    /// <summary>
    /// Geçerli durum geçişlerinin tanımları.
    /// (CurrentPhase, NextPhase) -> IsValid
    /// </summary>
    private static readonly HashSet<(GamePhase From, GamePhase To)> ValidTransitions = new()
    {
        // Normal akış
        (GamePhase.WaitingForPlayers, GamePhase.ReadyToStart),
        (GamePhase.ReadyToStart, GamePhase.Shuffling),
        (GamePhase.ReadyToStart, GamePhase.WaitingForPlayers), // Oyuncu ayrılırsa
        (GamePhase.Shuffling, GamePhase.Dealing),
        (GamePhase.Dealing, GamePhase.Playing),
        (GamePhase.Playing, GamePhase.Finished),

        // İptal geçişleri (her durumdan)
        (GamePhase.WaitingForPlayers, GamePhase.Cancelled),
        (GamePhase.ReadyToStart, GamePhase.Cancelled),
        (GamePhase.Shuffling, GamePhase.Cancelled),
        (GamePhase.Dealing, GamePhase.Cancelled),
        (GamePhase.Playing, GamePhase.Cancelled),
    };

    /// <summary>
    /// Tur fazı geçişleri.
    /// </summary>
    private static readonly HashSet<(TurnPhase From, TurnPhase To)> ValidTurnTransitions = new()
    {
        (TurnPhase.WaitingForDraw, TurnPhase.WaitingForDiscard),
        (TurnPhase.WaitingForDiscard, TurnPhase.TurnCompleted),
        (TurnPhase.TurnCompleted, TurnPhase.WaitingForDraw), // Yeni tur
    };

    #endregion

    #region Constructor

    private GameStateMachine()
    {
        // Private constructor for singleton
    }

    #endregion

    #region Geçiş Doğrulama

    /// <summary>
    /// Belirtilen durum geçişinin geçerli olup olmadığını kontrol eder.
    /// </summary>
    /// <param name="from">Mevcut durum</param>
    /// <param name="to">Hedef durum</param>
    /// <returns>Geçiş geçerli mi?</returns>
    public bool CanTransition(GamePhase from, GamePhase to)
    {
        // Aynı duruma geçiş her zaman geçerli (no-op)
        if (from == to) return true;

        return ValidTransitions.Contains((from, to));
    }

    /// <summary>
    /// Tur fazı geçişinin geçerli olup olmadığını kontrol eder.
    /// </summary>
    public bool CanTransitionTurn(TurnPhase from, TurnPhase to)
    {
        if (from == to) return true;
        return ValidTurnTransitions.Contains((from, to));
    }

    /// <summary>
    /// Durum geçişini gerçekleştirir.
    /// Geçersiz geçişte exception fırlatır.
    /// </summary>
    /// <param name="from">Mevcut durum</param>
    /// <param name="to">Hedef durum</param>
    /// <returns>Yeni durum</returns>
    /// <exception cref="InvalidOperationException">Geçersiz geçiş</exception>
    public GamePhase Transition(GamePhase from, GamePhase to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Geçersiz oyun durumu geçişi: {from} -> {to}. " +
                $"Bu geçiş oyun kurallarına uygun değil.");
        }

        return to;
    }

    /// <summary>
    /// Tur fazı geçişini gerçekleştirir.
    /// </summary>
    public TurnPhase TransitionTurn(TurnPhase from, TurnPhase to)
    {
        if (!CanTransitionTurn(from, to))
        {
            throw new InvalidOperationException(
                $"Geçersiz tur fazı geçişi: {from} -> {to}.");
        }

        return to;
    }

    #endregion

    #region Durum Sorguları

    /// <summary>
    /// Belirtilen durumdan geçilebilecek tüm durumları döndürür.
    /// </summary>
    public IReadOnlyList<GamePhase> GetPossibleTransitions(GamePhase from)
    {
        return ValidTransitions
            .Where(t => t.From == from)
            .Select(t => t.To)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Oyunun aktif (oynanabilir) durumda olup olmadığını kontrol eder.
    /// </summary>
    public bool IsActivePhase(GamePhase phase)
    {
        return phase == GamePhase.Playing;
    }

    /// <summary>
    /// Oyunun başlangıç aşamasında olup olmadığını kontrol eder.
    /// </summary>
    public bool IsSetupPhase(GamePhase phase)
    {
        return phase is GamePhase.WaitingForPlayers 
            or GamePhase.ReadyToStart 
            or GamePhase.Shuffling 
            or GamePhase.Dealing;
    }

    /// <summary>
    /// Oyunun sona erip ermediğini kontrol eder.
    /// </summary>
    public bool IsTerminalPhase(GamePhase phase)
    {
        return phase is GamePhase.Finished or GamePhase.Cancelled;
    }

    /// <summary>
    /// Belirtilen durumda oyuncunun sıra bekleme hakkı olup olmadığını kontrol eder.
    /// </summary>
    public bool RequiresTurnManagement(GamePhase phase)
    {
        return phase == GamePhase.Playing;
    }

    #endregion

    #region Yardımcı Metodlar

    /// <summary>
    /// Geçiş sonucu oluşturur.
    /// </summary>
    public StateTransitionResult TryTransition(GamePhase from, GamePhase to, string? reason = null)
    {
        if (CanTransition(from, to))
        {
            return StateTransitionResult.Success(from, to, reason);
        }

        return StateTransitionResult.Failure(from, to, 
            $"Geçersiz geçiş: {from} -> {to}");
    }

    #endregion
}

/// <summary>
/// Durum geçişi sonucu.
/// </summary>
public sealed class StateTransitionResult
{
    /// <summary>Geçiş başarılı mı?</summary>
    public bool IsSuccess { get; }

    /// <summary>Önceki durum.</summary>
    public GamePhase FromPhase { get; }

    /// <summary>Yeni/Hedef durum.</summary>
    public GamePhase ToPhase { get; }

    /// <summary>Geçiş nedeni veya hata mesajı.</summary>
    public string? Message { get; }

    /// <summary>Geçiş zamanı.</summary>
    public DateTime TransitionTime { get; }

    private StateTransitionResult(bool isSuccess, GamePhase from, GamePhase to, string? message)
    {
        IsSuccess = isSuccess;
        FromPhase = from;
        ToPhase = to;
        Message = message;
        TransitionTime = DateTime.UtcNow;
    }

    public static StateTransitionResult Success(GamePhase from, GamePhase to, string? reason = null)
        => new(true, from, to, reason);

    public static StateTransitionResult Failure(GamePhase from, GamePhase to, string errorMessage)
        => new(false, from, to, errorMessage);
}
