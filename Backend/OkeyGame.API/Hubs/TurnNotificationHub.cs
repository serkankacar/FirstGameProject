using Microsoft.AspNetCore.SignalR;
using OkeyGame.Application.Services;

namespace OkeyGame.API.Hubs;

/// <summary>
/// Tur bildirimleri için SignalR Hub extension.
/// 
/// CLIENT METODLARI (Server -> Client):
/// - OnTurnChanged: Sıra değiştiğinde tüm masaya gönderilir
/// - OnTurnTimerTick: Kalan süre güncellemesi (kritik sürede)
/// - OnAutoPlayTriggered: Otomatik oynatma başladığında
/// - OnPlayerTimeout: Oyuncu zaman aşımına uğradığında
/// 
/// KULLANIM:
/// Unity client'ta bu eventleri dinleyerek UI güncellenir.
/// </summary>
public static class TurnNotificationMethods
{
    #region Client Method Names

    /// <summary>
    /// Sıra değiştiğinde gönderilir.
    /// Payload: { playerId, playerName, position, timeLeft, turnNumber }
    /// </summary>
    public const string OnTurnChanged = "OnTurnChanged";

    /// <summary>
    /// Kalan süre güncellemesi.
    /// Payload: { playerId, timeLeft, isCritical }
    /// </summary>
    public const string OnTurnTimerTick = "OnTurnTimerTick";

    /// <summary>
    /// Otomatik oynatma başladığında.
    /// Payload: { playerId, reason }
    /// </summary>
    public const string OnAutoPlayTriggered = "OnAutoPlayTriggered";

    /// <summary>
    /// Oyuncu zaman aşımına uğradığında.
    /// Payload: { playerId, turnNumber }
    /// </summary>
    public const string OnPlayerTimeout = "OnPlayerTimeout";

    /// <summary>
    /// Oyun fazı değiştiğinde.
    /// Payload: { oldPhase, newPhase, timestamp }
    /// </summary>
    public const string OnGamePhaseChanged = "OnGamePhaseChanged";

    #endregion
}

/// <summary>
/// Tur bildirim DTO'ları.
/// </summary>
public sealed class TurnChangedDto
{
    /// <summary>Sıradaki oyuncu ID'si.</summary>
    public required Guid PlayerId { get; init; }

    /// <summary>Oyuncu adı.</summary>
    public required string PlayerName { get; init; }

    /// <summary>Oyuncu pozisyonu (0-3).</summary>
    public required int Position { get; init; }

    /// <summary>Kalan süre (saniye).</summary>
    public required int TimeLeft { get; init; }

    /// <summary>Tur numarası.</summary>
    public required int TurnNumber { get; init; }

    /// <summary>Tur fazı.</summary>
    public required string TurnPhase { get; init; }

    /// <summary>Zaman damgası.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Timer tick DTO'su.
/// </summary>
public sealed class TimerTickDto
{
    /// <summary>Oyuncu ID'si.</summary>
    public required Guid PlayerId { get; init; }

    /// <summary>Kalan süre (saniye).</summary>
    public required int TimeLeft { get; init; }

    /// <summary>Kritik sürede mi? (10 saniye altı)</summary>
    public required bool IsCritical { get; init; }

    /// <summary>Zaman damgası.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Auto-play DTO'su.
/// </summary>
public sealed class AutoPlayTriggeredDto
{
    /// <summary>Oyuncu ID'si.</summary>
    public required Guid PlayerId { get; init; }

    /// <summary>Neden (Timeout, Disconnected, AFK).</summary>
    public required string Reason { get; init; }

    /// <summary>Zaman damgası.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Faz değişimi DTO'su.
/// </summary>
public sealed class GamePhaseChangedDto
{
    /// <summary>Önceki faz.</summary>
    public required string OldPhase { get; init; }

    /// <summary>Yeni faz.</summary>
    public required string NewPhase { get; init; }

    /// <summary>Zaman damgası.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// GameHub için tur bildirimi extension metodları.
/// </summary>
public static class GameHubTurnExtensions
{
    /// <summary>
    /// Sıra değişimini tüm masaya bildirir.
    /// </summary>
    public static async Task NotifyTurnChangedAsync(
        this IHubContext<GameHub> hubContext,
        Guid roomId,
        TurnChangedDto dto)
    {
        await hubContext.Clients
            .Group($"room:{roomId}")
            .SendAsync(TurnNotificationMethods.OnTurnChanged, dto);
    }

    /// <summary>
    /// Timer tick'i tüm masaya bildirir.
    /// </summary>
    public static async Task NotifyTimerTickAsync(
        this IHubContext<GameHub> hubContext,
        Guid roomId,
        TimerTickDto dto)
    {
        await hubContext.Clients
            .Group($"room:{roomId}")
            .SendAsync(TurnNotificationMethods.OnTurnTimerTick, dto);
    }

    /// <summary>
    /// Auto-play başladığını bildirir.
    /// </summary>
    public static async Task NotifyAutoPlayTriggeredAsync(
        this IHubContext<GameHub> hubContext,
        Guid roomId,
        AutoPlayTriggeredDto dto)
    {
        await hubContext.Clients
            .Group($"room:{roomId}")
            .SendAsync(TurnNotificationMethods.OnAutoPlayTriggered, dto);
    }

    /// <summary>
    /// Faz değişimini bildirir.
    /// </summary>
    public static async Task NotifyGamePhaseChangedAsync(
        this IHubContext<GameHub> hubContext,
        Guid roomId,
        GamePhaseChangedDto dto)
    {
        await hubContext.Clients
            .Group($"room:{roomId}")
            .SendAsync(TurnNotificationMethods.OnGamePhaseChanged, dto);
    }
}
