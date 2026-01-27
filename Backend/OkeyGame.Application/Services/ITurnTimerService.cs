namespace OkeyGame.Application.Services;

/// <summary>
/// Tur zamanlayıcısı event argümanları.
/// </summary>
public sealed class TurnTimerEventArgs : EventArgs
{
    /// <summary>Oda ID'si.</summary>
    public Guid RoomId { get; init; }

    /// <summary>Oyuncu ID'si.</summary>
    public Guid PlayerId { get; init; }

    /// <summary>Kalan süre (saniye).</summary>
    public int RemainingSeconds { get; init; }

    /// <summary>Tur numarası.</summary>
    public int TurnNumber { get; init; }

    /// <summary>Olay zamanı.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Zaman aşımı event argümanları.
/// </summary>
public sealed class TurnTimeoutEventArgs : EventArgs
{
    /// <summary>Oda ID'si.</summary>
    public Guid RoomId { get; init; }

    /// <summary>Oyuncu ID'si.</summary>
    public Guid PlayerId { get; init; }

    /// <summary>Tur numarası.</summary>
    public int TurnNumber { get; init; }

    /// <summary>Zaman aşımı zamanı.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Tur zamanlayıcısı arayüzü.
/// 
/// SORUMLULUKLAR:
/// - Aktif odaların tur sürelerini izleme
/// - Kalan süreyi periyodik olarak yayınlama
/// - Zaman aşımı olduğunda auto-play tetikleme
/// </summary>
public interface ITurnTimerService
{
    /// <summary>
    /// Bir oda için tur zamanlayıcısını başlatır.
    /// </summary>
    /// <param name="roomId">Oda ID</param>
    /// <param name="playerId">Oyuncu ID</param>
    /// <param name="turnNumber">Tur numarası</param>
    /// <param name="durationSeconds">Tur süresi (saniye)</param>
    void StartTimer(Guid roomId, Guid playerId, int turnNumber, int durationSeconds = 15);

    /// <summary>
    /// Bir oda için tur zamanlayıcısını durdurur.
    /// </summary>
    /// <param name="roomId">Oda ID</param>
    void StopTimer(Guid roomId);

    /// <summary>
    /// Bir oda için tur süresini uzatır (reconnection için).
    /// </summary>
    /// <param name="roomId">Oda ID</param>
    /// <param name="additionalSeconds">Ek süre</param>
    void ExtendTimer(Guid roomId, int additionalSeconds);

    /// <summary>
    /// Bir odanın zamanlayıcı durumunu sorgular.
    /// </summary>
    /// <param name="roomId">Oda ID</param>
    /// <returns>Zamanlayıcı bilgisi veya null</returns>
    TurnTimerInfo? GetTimerInfo(Guid roomId);

    /// <summary>
    /// Tüm aktif zamanlayıcıları durdurur.
    /// </summary>
    void StopAllTimers();

    /// <summary>
    /// Kalan süre güncellemesi eventi.
    /// Her saniye tetiklenir.
    /// </summary>
    event EventHandler<TurnTimerEventArgs>? OnTimerTick;

    /// <summary>
    /// Zaman aşımı eventi.
    /// Süre dolduğunda tetiklenir.
    /// </summary>
    event EventHandler<TurnTimeoutEventArgs>? OnTimeout;
}

/// <summary>
/// Tur zamanlayıcı bilgisi.
/// </summary>
public sealed class TurnTimerInfo
{
    /// <summary>Oda ID'si.</summary>
    public Guid RoomId { get; init; }

    /// <summary>Oyuncu ID'si.</summary>
    public Guid PlayerId { get; init; }

    /// <summary>Tur numarası.</summary>
    public int TurnNumber { get; init; }

    /// <summary>Başlangıç zamanı.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>Bitiş zamanı.</summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>Kalan süre (saniye).</summary>
    public int RemainingSeconds => Math.Max(0, (int)Math.Ceiling((ExpiresAt - DateTime.UtcNow).TotalSeconds));

    /// <summary>Süre doldu mu?</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
