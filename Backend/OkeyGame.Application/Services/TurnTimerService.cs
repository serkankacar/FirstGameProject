using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OkeyGame.Application.Services;

/// <summary>
/// Tur zamanlayıcısı implementasyonu.
/// 
/// MİMARİ:
/// - Her oda için ayrı bir CancellationTokenSource
/// - Timer yerine async/await loop (daha kontrollü)
/// - Thread-safe ConcurrentDictionary ile state yönetimi
/// - Event-based bildirimler (decoupling için)
/// 
/// PERFORMANS:
/// - Minimal memory footprint
/// - Lazy timer başlatma
/// - Efficient cancellation
/// </summary>
public sealed class TurnTimerService : ITurnTimerService, IDisposable
{
    #region Sabitler

    /// <summary>
    /// Timer tick aralığı (ms).
    /// Her saniye bir tick.
    /// </summary>
    private const int TickIntervalMs = 1000;

    /// <summary>
    /// Kalan süre bildirimi için eşik (saniye).
    /// Bu süreden sonra her tick'te bildirim gönderilir.
    /// </summary>
    private const int CriticalTimeThresholdSeconds = 10;

    #endregion

    #region Alanlar

    private readonly ILogger<TurnTimerService> _logger;
    private readonly ConcurrentDictionary<Guid, ActiveTimer> _activeTimers;
    private bool _disposed;

    #endregion

    #region Events

    /// <summary>
    /// Kalan süre güncellemesi eventi.
    /// </summary>
    public event EventHandler<TurnTimerEventArgs>? OnTimerTick;

    /// <summary>
    /// Zaman aşımı eventi.
    /// </summary>
    public event EventHandler<TurnTimeoutEventArgs>? OnTimeout;

    #endregion

    #region Constructor

    public TurnTimerService(ILogger<TurnTimerService> logger)
    {
        _logger = logger;
        _activeTimers = new ConcurrentDictionary<Guid, ActiveTimer>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Bir oda için tur zamanlayıcısını başlatır.
    /// </summary>
    public void StartTimer(Guid roomId, Guid playerId, int turnNumber, int durationSeconds = 15)
    {
        // Önceki timer varsa durdur
        StopTimer(roomId);

        var cts = new CancellationTokenSource();
        var timer = new ActiveTimer
        {
            RoomId = roomId,
            PlayerId = playerId,
            TurnNumber = turnNumber,
            StartedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(durationSeconds),
            CancellationTokenSource = cts
        };

        if (_activeTimers.TryAdd(roomId, timer))
        {
            _logger.LogDebug(
                "Tur zamanlayıcısı başlatıldı: Oda {RoomId}, Oyuncu {PlayerId}, Tur {TurnNumber}, Süre {Duration}s",
                roomId, playerId, turnNumber, durationSeconds);

            // Async timer loop başlat
            _ = RunTimerLoopAsync(timer, cts.Token);
        }
    }

    /// <summary>
    /// Bir oda için tur zamanlayıcısını durdurur.
    /// </summary>
    public void StopTimer(Guid roomId)
    {
        if (_activeTimers.TryRemove(roomId, out var timer))
        {
            try
            {
                timer.CancellationTokenSource.Cancel();
                timer.CancellationTokenSource.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Zaten dispose edilmiş, sorun yok
            }

            _logger.LogDebug("Tur zamanlayıcısı durduruldu: Oda {RoomId}", roomId);
        }
    }

    /// <summary>
    /// Bir oda için tur süresini uzatır.
    /// </summary>
    public void ExtendTimer(Guid roomId, int additionalSeconds)
    {
        if (_activeTimers.TryGetValue(roomId, out var timer))
        {
            var newExpiresAt = timer.ExpiresAt.AddSeconds(additionalSeconds);
            
            // Atomic update için yeni timer oluştur
            var updatedTimer = timer with { ExpiresAt = newExpiresAt };
            _activeTimers.TryUpdate(roomId, updatedTimer, timer);

            _logger.LogDebug(
                "Tur süresi uzatıldı: Oda {RoomId}, Ek süre {Additional}s, Yeni bitiş {ExpiresAt}",
                roomId, additionalSeconds, newExpiresAt);
        }
    }

    /// <summary>
    /// Bir odanın zamanlayıcı durumunu sorgular.
    /// </summary>
    public TurnTimerInfo? GetTimerInfo(Guid roomId)
    {
        if (_activeTimers.TryGetValue(roomId, out var timer))
        {
            return new TurnTimerInfo
            {
                RoomId = timer.RoomId,
                PlayerId = timer.PlayerId,
                TurnNumber = timer.TurnNumber,
                StartedAt = timer.StartedAt,
                ExpiresAt = timer.ExpiresAt
            };
        }

        return null;
    }

    /// <summary>
    /// Tüm aktif zamanlayıcıları durdurur.
    /// </summary>
    public void StopAllTimers()
    {
        foreach (var roomId in _activeTimers.Keys.ToList())
        {
            StopTimer(roomId);
        }

        _logger.LogInformation("Tüm tur zamanlayıcıları durduruldu.");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Timer loop - her saniye çalışır.
    /// </summary>
    private async Task RunTimerLoopAsync(ActiveTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Güncel timer bilgisini al (extend edilmiş olabilir)
                if (!_activeTimers.TryGetValue(timer.RoomId, out var currentTimer))
                {
                    // Timer kaldırılmış, çık
                    break;
                }

                var remaining = currentTimer.ExpiresAt - DateTime.UtcNow;
                int remainingSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));

                // Süre doldu mu?
                if (remaining <= TimeSpan.Zero)
                {
                    // Zaman aşımı eventi
                    RaiseTimeoutEvent(currentTimer);

                    // Timer'ı kaldır
                    StopTimer(timer.RoomId);
                    break;
                }

                // Tick eventi (kritik sürede veya her 5 saniyede bir)
                if (remainingSeconds <= CriticalTimeThresholdSeconds || remainingSeconds % 5 == 0)
                {
                    RaiseTickEvent(currentTimer, remainingSeconds);
                }

                // Bir saniye bekle
                await Task.Delay(TickIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal iptal, sorun yok
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Timer loop hatası: Oda {RoomId}", timer.RoomId);
        }
    }

    /// <summary>
    /// Tick eventi fırlatır.
    /// </summary>
    private void RaiseTickEvent(ActiveTimer timer, int remainingSeconds)
    {
        try
        {
            OnTimerTick?.Invoke(this, new TurnTimerEventArgs
            {
                RoomId = timer.RoomId,
                PlayerId = timer.PlayerId,
                RemainingSeconds = remainingSeconds,
                TurnNumber = timer.TurnNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Timer tick event hatası");
        }
    }

    /// <summary>
    /// Timeout eventi fırlatır.
    /// </summary>
    private void RaiseTimeoutEvent(ActiveTimer timer)
    {
        try
        {
            _logger.LogInformation(
                "Tur zaman aşımı: Oda {RoomId}, Oyuncu {PlayerId}, Tur {TurnNumber}",
                timer.RoomId, timer.PlayerId, timer.TurnNumber);

            OnTimeout?.Invoke(this, new TurnTimeoutEventArgs
            {
                RoomId = timer.RoomId,
                PlayerId = timer.PlayerId,
                TurnNumber = timer.TurnNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Timer timeout event hatası");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        StopAllTimers();
        _disposed = true;
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Aktif timer bilgisi.
    /// </summary>
    private sealed record ActiveTimer
    {
        public Guid RoomId { get; init; }
        public Guid PlayerId { get; init; }
        public int TurnNumber { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
        public CancellationTokenSource CancellationTokenSource { get; init; } = null!;
    }

    #endregion
}
