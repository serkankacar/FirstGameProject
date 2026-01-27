using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.StateMachine;

/// <summary>
/// Tur bilgilerini tutan immutable value object.
/// Sıra yönetimi için gerekli tüm bilgileri içerir.
/// 
/// Thread-safe ve snapshot olarak kullanılabilir.
/// </summary>
public sealed record TurnContext
{
    #region Sabitler

    /// <summary>
    /// Varsayılan tur süresi (saniye).
    /// </summary>
    public const int DefaultTurnDurationSeconds = 15;

    /// <summary>
    /// Minimum tur süresi (saniye).
    /// </summary>
    public const int MinTurnDurationSeconds = 5;

    /// <summary>
    /// Maksimum tur süresi (saniye).
    /// </summary>
    public const int MaxTurnDurationSeconds = 60;

    #endregion

    #region Özellikler

    /// <summary>
    /// Oda ID'si.
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// Sıradaki oyuncunun ID'si.
    /// </summary>
    public Guid CurrentPlayerId { get; init; }

    /// <summary>
    /// Sıradaki oyuncunun pozisyonu.
    /// </summary>
    public PlayerPosition CurrentPosition { get; init; }

    /// <summary>
    /// Tur numarası (1'den başlar).
    /// </summary>
    public int TurnNumber { get; init; }

    /// <summary>
    /// Tur fazı (çekme/atma bekliyor).
    /// </summary>
    public TurnPhase Phase { get; init; }

    /// <summary>
    /// Tur başlama zamanı (UTC).
    /// </summary>
    public DateTime TurnStartedAt { get; init; }

    /// <summary>
    /// Tur bitiş zamanı (zaman aşımı) (UTC).
    /// </summary>
    public DateTime TurnExpiresAt { get; init; }

    /// <summary>
    /// Bu turda taş çekildi mi?
    /// </summary>
    public bool HasDrawnTile { get; init; }

    /// <summary>
    /// Discard'dan mı çekildi?
    /// </summary>
    public bool DrewFromDiscard { get; init; }

    /// <summary>
    /// Oyuncu bot mu?
    /// </summary>
    public bool IsBot { get; init; }

    /// <summary>
    /// Oyuncu bağlı mı?
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Otomatik oynatma aktif mi?
    /// Oyuncu AFK olduğunda true olur.
    /// </summary>
    public bool IsAutoPlay { get; init; }

    #endregion

    #region Hesaplanmış Özellikler

    /// <summary>
    /// Kalan süre (saniye).
    /// Negatif olabilir (zaman aşımı).
    /// </summary>
    public double RemainingSeconds => (TurnExpiresAt - DateTime.UtcNow).TotalSeconds;

    /// <summary>
    /// Kalan süre (tam saniye, minimum 0).
    /// </summary>
    public int RemainingSecondsInt => Math.Max(0, (int)Math.Ceiling(RemainingSeconds));

    /// <summary>
    /// Tur süresi doldu mu?
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= TurnExpiresAt;

    /// <summary>
    /// Tur tamamlandı mı?
    /// </summary>
    public bool IsCompleted => Phase == TurnPhase.TurnCompleted;

    /// <summary>
    /// Oyuncu taş çekebilir mi?
    /// </summary>
    public bool CanDraw => Phase == TurnPhase.WaitingForDraw && !HasDrawnTile;

    /// <summary>
    /// Oyuncu taş atabilir mi?
    /// </summary>
    public bool CanDiscard => Phase == TurnPhase.WaitingForDiscard && HasDrawnTile;

    /// <summary>
    /// Toplam tur süresi (saniye).
    /// </summary>
    public int TotalTurnDurationSeconds => (int)(TurnExpiresAt - TurnStartedAt).TotalSeconds;

    #endregion

    #region Factory Metodlar

    /// <summary>
    /// Yeni bir tur başlatır.
    /// </summary>
    public static TurnContext StartNew(
        Guid roomId,
        Guid playerId,
        PlayerPosition position,
        int turnNumber,
        bool isBot = false,
        bool isConnected = true,
        int turnDurationSeconds = DefaultTurnDurationSeconds)
    {
        var now = DateTime.UtcNow;
        
        return new TurnContext
        {
            RoomId = roomId,
            CurrentPlayerId = playerId,
            CurrentPosition = position,
            TurnNumber = turnNumber,
            Phase = TurnPhase.WaitingForDraw,
            TurnStartedAt = now,
            TurnExpiresAt = now.AddSeconds(turnDurationSeconds),
            HasDrawnTile = false,
            DrewFromDiscard = false,
            IsBot = isBot,
            IsConnected = isConnected,
            IsAutoPlay = false
        };
    }

    /// <summary>
    /// İlk oyuncunun sırasını başlatır (15 taşlı oyuncu).
    /// İlk oyuncu zaten taş çekmiş sayılır (15 taş aldı).
    /// </summary>
    public static TurnContext StartFirst(
        Guid roomId,
        Guid playerId,
        PlayerPosition position,
        bool isBot = false,
        bool isConnected = true,
        int turnDurationSeconds = DefaultTurnDurationSeconds)
    {
        var now = DateTime.UtcNow;
        
        return new TurnContext
        {
            RoomId = roomId,
            CurrentPlayerId = playerId,
            CurrentPosition = position,
            TurnNumber = 1,
            Phase = TurnPhase.WaitingForDiscard, // İlk oyuncu zaten 15 taş aldı
            TurnStartedAt = now,
            TurnExpiresAt = now.AddSeconds(turnDurationSeconds),
            HasDrawnTile = true, // İlk oyuncu çekmiş sayılır
            DrewFromDiscard = false,
            IsBot = isBot,
            IsConnected = isConnected,
            IsAutoPlay = false
        };
    }

    #endregion

    #region Immutable Güncellemeler

    /// <summary>
    /// Taş çekildiğinde yeni context döndürür.
    /// </summary>
    public TurnContext WithTileDrawn(bool fromDiscard)
    {
        return this with
        {
            Phase = TurnPhase.WaitingForDiscard,
            HasDrawnTile = true,
            DrewFromDiscard = fromDiscard
        };
    }

    /// <summary>
    /// Taş atıldığında yeni context döndürür.
    /// </summary>
    public TurnContext WithTileDiscarded()
    {
        return this with
        {
            Phase = TurnPhase.TurnCompleted
        };
    }

    /// <summary>
    /// Otomatik oynatma moduna geçiş.
    /// </summary>
    public TurnContext WithAutoPlay()
    {
        return this with { IsAutoPlay = true };
    }

    /// <summary>
    /// Süreyi uzatır (reconnection durumunda).
    /// </summary>
    public TurnContext WithExtendedTime(int additionalSeconds)
    {
        return this with
        {
            TurnExpiresAt = TurnExpiresAt.AddSeconds(additionalSeconds)
        };
    }

    /// <summary>
    /// Bağlantı durumunu günceller.
    /// </summary>
    public TurnContext WithConnectionStatus(bool isConnected)
    {
        return this with { IsConnected = isConnected };
    }

    #endregion

    #region Doğrulama

    /// <summary>
    /// Belirtilen oyuncunun sırası olup olmadığını kontrol eder.
    /// </summary>
    public bool IsPlayerTurn(Guid playerId)
    {
        return CurrentPlayerId == playerId;
    }

    /// <summary>
    /// Belirtilen aksiyonun geçerli olup olmadığını kontrol eder.
    /// </summary>
    public TurnValidationResult ValidateAction(TurnAction action, Guid playerId)
    {
        // Oyuncu kontrolü
        if (!IsPlayerTurn(playerId))
        {
            return TurnValidationResult.NotYourTurn(CurrentPlayerId);
        }

        // Zaman aşımı kontrolü
        if (IsExpired && !IsAutoPlay)
        {
            return TurnValidationResult.TimeExpired(RemainingSeconds);
        }

        // Aksiyon kontrolü
        return action switch
        {
            TurnAction.DrawFromDeck or TurnAction.DrawFromDiscard => 
                CanDraw 
                    ? TurnValidationResult.Valid() 
                    : TurnValidationResult.InvalidAction("Taş çekme aşaması değil."),
            
            TurnAction.Discard => 
                CanDiscard 
                    ? TurnValidationResult.Valid() 
                    : TurnValidationResult.InvalidAction("Önce taş çekmelisiniz."),
            
            TurnAction.DeclareWin => 
                CanDiscard 
                    ? TurnValidationResult.Valid() 
                    : TurnValidationResult.InvalidAction("Kazanmak için önce taş çekmelisiniz."),
            
            _ => TurnValidationResult.InvalidAction("Bilinmeyen aksiyon.")
        };
    }

    #endregion
}

/// <summary>
/// Tur aksiyonları.
/// </summary>
public enum TurnAction
{
    /// <summary>Desteden taş çek.</summary>
    DrawFromDeck,
    
    /// <summary>Discard'dan taş çek.</summary>
    DrawFromDiscard,
    
    /// <summary>Taş at.</summary>
    Discard,
    
    /// <summary>Kazanma ilan et.</summary>
    DeclareWin
}

/// <summary>
/// Tur doğrulama sonucu.
/// </summary>
public sealed record TurnValidationResult
{
    /// <summary>Aksiyon geçerli mi?</summary>
    public bool IsValid { get; init; }

    /// <summary>Hata türü.</summary>
    public TurnValidationError? Error { get; init; }

    /// <summary>Hata mesajı.</summary>
    public string? Message { get; init; }

    /// <summary>Doğru oyuncu ID'si (sıra başkasındaysa).</summary>
    public Guid? CorrectPlayerId { get; init; }

    /// <summary>Kalan süre (zaman aşımı durumunda).</summary>
    public double? RemainingSeconds { get; init; }

    public static TurnValidationResult Valid() => new() { IsValid = true };

    public static TurnValidationResult NotYourTurn(Guid correctPlayerId) => new()
    {
        IsValid = false,
        Error = TurnValidationError.NotYourTurn,
        Message = "Sıra sizde değil.",
        CorrectPlayerId = correctPlayerId
    };

    public static TurnValidationResult TimeExpired(double remainingSeconds) => new()
    {
        IsValid = false,
        Error = TurnValidationError.TimeExpired,
        Message = "Süreniz doldu.",
        RemainingSeconds = remainingSeconds
    };

    public static TurnValidationResult InvalidAction(string message) => new()
    {
        IsValid = false,
        Error = TurnValidationError.InvalidAction,
        Message = message
    };
}

/// <summary>
/// Tur doğrulama hata türleri.
/// </summary>
public enum TurnValidationError
{
    /// <summary>Sıra oyuncuda değil.</summary>
    NotYourTurn,
    
    /// <summary>Süre doldu.</summary>
    TimeExpired,
    
    /// <summary>Geçersiz aksiyon.</summary>
    InvalidAction
}
