using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.StateMachine;

/// <summary>
/// Sıra yönetimi servisi.
/// 
/// SORUMLULUKLAR:
/// - Sıranın hangi oyuncuda olduğunu takip etme
/// - Tur geçişlerini yönetme
/// - Zaman aşımı hesaplama
/// - TurnContext oluşturma ve güncelleme
/// 
/// Thread-safe tasarım: Tüm state TurnContext'te tutulur.
/// Bu sınıf stateless helper metodlar sağlar.
/// </summary>
public sealed class TurnManager
{
    #region Sabitler

    /// <summary>
    /// Varsayılan tur süresi (saniye).
    /// </summary>
    public const int DefaultTurnDurationSeconds = 15;

    /// <summary>
    /// Bağlantı kopma sonrası ek süre (saniye).
    /// </summary>
    public const int ReconnectionGraceSeconds = 5;

    /// <summary>
    /// Bot'lar için minimum bekleme süresi (ms).
    /// </summary>
    public const int BotMinDelayMs = 1000;

    /// <summary>
    /// Bot'lar için maksimum bekleme süresi (ms).
    /// </summary>
    public const int BotMaxDelayMs = 3000;

    #endregion

    #region Singleton

    private static readonly Lazy<TurnManager> _instance = new(() => new TurnManager());

    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static TurnManager Instance => _instance.Value;

    private TurnManager() { }

    #endregion

    #region Tur Başlatma

    /// <summary>
    /// Oyunun ilk turunu başlatır.
    /// İlk oyuncu 15 taş aldığı için doğrudan atma fazındadır.
    /// </summary>
    /// <param name="roomId">Oda ID</param>
    /// <param name="firstPlayerId">İlk oyuncu ID</param>
    /// <param name="position">İlk oyuncu pozisyonu</param>
    /// <param name="isBot">Bot mu?</param>
    /// <param name="isConnected">Bağlı mı?</param>
    /// <param name="turnDurationSeconds">Tur süresi</param>
    /// <returns>İlk tur context'i</returns>
    public TurnContext StartFirstTurn(
        Guid roomId,
        Guid firstPlayerId,
        PlayerPosition position,
        bool isBot = false,
        bool isConnected = true,
        int turnDurationSeconds = DefaultTurnDurationSeconds)
    {
        return TurnContext.StartFirst(
            roomId,
            firstPlayerId,
            position,
            isBot,
            isConnected,
            turnDurationSeconds);
    }

    /// <summary>
    /// Sonraki oyuncunun turunu başlatır.
    /// </summary>
    /// <param name="previousContext">Önceki tur context'i</param>
    /// <param name="nextPlayerId">Sonraki oyuncu ID</param>
    /// <param name="nextPosition">Sonraki oyuncu pozisyonu</param>
    /// <param name="isBot">Bot mu?</param>
    /// <param name="isConnected">Bağlı mı?</param>
    /// <param name="turnDurationSeconds">Tur süresi</param>
    /// <returns>Yeni tur context'i</returns>
    public TurnContext StartNextTurn(
        TurnContext previousContext,
        Guid nextPlayerId,
        PlayerPosition nextPosition,
        bool isBot = false,
        bool isConnected = true,
        int turnDurationSeconds = DefaultTurnDurationSeconds)
    {
        ArgumentNullException.ThrowIfNull(previousContext);

        return TurnContext.StartNew(
            previousContext.RoomId,
            nextPlayerId,
            nextPosition,
            previousContext.TurnNumber + 1,
            isBot,
            isConnected,
            turnDurationSeconds);
    }

    #endregion

    #region Tur Aksiyonları

    /// <summary>
    /// Taş çekme aksiyonunu işler.
    /// </summary>
    /// <param name="context">Mevcut context</param>
    /// <param name="playerId">Oyuncu ID</param>
    /// <param name="fromDiscard">Discard'dan mı?</param>
    /// <returns>İşlem sonucu</returns>
    public TurnActionResult ProcessDraw(TurnContext context, Guid playerId, bool fromDiscard)
    {
        ArgumentNullException.ThrowIfNull(context);

        var action = fromDiscard ? TurnAction.DrawFromDiscard : TurnAction.DrawFromDeck;
        var validation = context.ValidateAction(action, playerId);

        if (!validation.IsValid)
        {
            return TurnActionResult.Failure(validation.Message ?? "Geçersiz aksiyon.");
        }

        var newContext = context.WithTileDrawn(fromDiscard);
        return TurnActionResult.Success(newContext, "Taş çekildi.");
    }

    /// <summary>
    /// Taş atma aksiyonunu işler.
    /// </summary>
    /// <param name="context">Mevcut context</param>
    /// <param name="playerId">Oyuncu ID</param>
    /// <returns>İşlem sonucu</returns>
    public TurnActionResult ProcessDiscard(TurnContext context, Guid playerId)
    {
        ArgumentNullException.ThrowIfNull(context);

        var validation = context.ValidateAction(TurnAction.Discard, playerId);

        if (!validation.IsValid)
        {
            return TurnActionResult.Failure(validation.Message ?? "Geçersiz aksiyon.");
        }

        var newContext = context.WithTileDiscarded();
        return TurnActionResult.Success(newContext, "Taş atıldı, sıra sonraki oyuncuya geçiyor.");
    }

    /// <summary>
    /// Kazanma bildirimi işler.
    /// </summary>
    public TurnActionResult ProcessWinDeclaration(TurnContext context, Guid playerId)
    {
        ArgumentNullException.ThrowIfNull(context);

        var validation = context.ValidateAction(TurnAction.DeclareWin, playerId);

        if (!validation.IsValid)
        {
            return TurnActionResult.Failure(validation.Message ?? "Geçersiz aksiyon.");
        }

        var newContext = context.WithTileDiscarded(); // Kazanmak da turu bitirir
        return TurnActionResult.Success(newContext, "Kazanma bildirimi alındı.", isWinning: true);
    }

    #endregion

    #region Zaman Aşımı İşleme

    /// <summary>
    /// Zaman aşımını işler ve auto-play moduna geçirir.
    /// </summary>
    /// <param name="context">Mevcut context</param>
    /// <returns>Auto-play context</returns>
    public TurnContext ProcessTimeout(TurnContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.WithAutoPlay();
    }

    /// <summary>
    /// Zaman aşımı olup olmadığını kontrol eder.
    /// </summary>
    public bool IsTimeout(TurnContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.IsExpired && !context.IsAutoPlay;
    }

    /// <summary>
    /// Kalan süreyi hesaplar.
    /// </summary>
    public TimeSpan GetRemainingTime(TurnContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var remaining = context.TurnExpiresAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    #endregion

    #region Pozisyon Yönetimi

    /// <summary>
    /// Sonraki oyuncu pozisyonunu hesaplar (saat yönünün tersine - Okey kuralı).
    /// Okey'de sıra saat yönünün tersine ilerler: South → West → North → East → South
    /// </summary>
    /// <param name="currentPosition">Mevcut pozisyon</param>
    /// <returns>Sonraki pozisyon</returns>
    public PlayerPosition GetNextPosition(PlayerPosition currentPosition)
    {
        // Saat yönünün tersine: South(0) → West(3) → North(2) → East(1) → South(0)
        return (PlayerPosition)(((int)currentPosition + 3) % 4);
    }

    /// <summary>
    /// N adım sonraki pozisyonu hesaplar (saat yönünün tersine).
    /// </summary>
    public PlayerPosition GetPositionAfter(PlayerPosition currentPosition, int steps)
    {
        // Her adım saat yönünün tersine: +3 mod 4
        return (PlayerPosition)(((int)currentPosition + (3 * steps)) % 4);
    }

    /// <summary>
    /// İki pozisyon arasındaki mesafeyi hesaplar (saat yönünün tersine).
    /// </summary>
    public int GetPositionDistance(PlayerPosition from, PlayerPosition to)
    {
        // Saat yönünün tersine mesafe: from'dan to'ya kaç adım
        // South(0) → West(3): 1 adım, South(0) → North(2): 2 adım, South(0) → East(1): 3 adım
        int distance = ((int)from - (int)to + 4) % 4;
        return distance;
    }

    #endregion

    #region Reconnection Yönetimi

    /// <summary>
    /// Oyuncu yeniden bağlandığında tur süresini uzatır.
    /// </summary>
    public TurnContext HandleReconnection(TurnContext context, int additionalSeconds = ReconnectionGraceSeconds)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Zaten yeterli süre varsa uzatma
        if (context.RemainingSeconds >= additionalSeconds)
        {
            return context.WithConnectionStatus(true);
        }

        return context
            .WithConnectionStatus(true)
            .WithExtendedTime(additionalSeconds);
    }

    /// <summary>
    /// Oyuncu bağlantısı koptuğunda context'i günceller.
    /// </summary>
    public TurnContext HandleDisconnection(TurnContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.WithConnectionStatus(false);
    }

    #endregion
}

/// <summary>
/// Tur aksiyonu sonucu.
/// </summary>
public sealed record TurnActionResult
{
    /// <summary>İşlem başarılı mı?</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Güncel context (başarılı ise).</summary>
    public TurnContext? Context { get; init; }

    /// <summary>Mesaj.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Oyuncu kazandı mı?</summary>
    public bool IsWinning { get; init; }

    /// <summary>Zaman damgası.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static TurnActionResult Success(TurnContext context, string message, bool isWinning = false)
        => new()
        {
            IsSuccess = true,
            Context = context,
            Message = message,
            IsWinning = isWinning
        };

    public static TurnActionResult Failure(string message)
        => new()
        {
            IsSuccess = false,
            Message = message
        };
}
