using OkeyGame.Domain.Enums;

namespace OkeyGame.API.Models;

/// <summary>
/// Redis'te saklanan oyun durumu.
/// Sunucu yeniden başlasa bile oyun durumu korunur.
/// </summary>
public class GameRoomState
{
    #region Oda Bilgileri

    /// <summary>
    /// Oda benzersiz kimliği.
    /// </summary>
    public required Guid RoomId { get; set; }

    /// <summary>
    /// Oda adı.
    /// </summary>
    public required string RoomName { get; set; }

    /// <summary>
    /// Oyunun mevcut durumu.
    /// </summary>
    public GameState State { get; set; } = GameState.WaitingForPlayers;

    /// <summary>
    /// Oda oluşturulma zamanı.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Son güncelleme zamanı.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    #endregion

    #region Oyuncu Bilgileri

    /// <summary>
    /// Odadaki oyuncular.
    /// Key: PlayerId, Value: Oyuncu durumu
    /// </summary>
    public Dictionary<Guid, PlayerState> Players { get; set; } = new();

    /// <summary>
    /// Sıradaki oyuncunun pozisyonu.
    /// </summary>
    public PlayerPosition CurrentTurnPosition { get; set; } = PlayerPosition.South;

    /// <summary>
    /// Sıradaki oyuncunun ID'si.
    /// </summary>
    public Guid? CurrentTurnPlayerId { get; set; }

    #endregion

    #region Taş Durumu

    /// <summary>
    /// Destede kalan taşlar (ID listesi).
    /// </summary>
    public List<int> DeckTileIds { get; set; } = new();

    /// <summary>
    /// Atık yığınındaki taşlar (en üstte son eleman).
    /// </summary>
    public List<int> DiscardPileTileIds { get; set; } = new();

    /// <summary>
    /// Gösterge taşı ID'si.
    /// </summary>
    public int? IndicatorTileId { get; set; }

    /// <summary>
    /// Tüm taşların JSON serileştirilmiş hali.
    /// Oyun başında oluşturulur ve değişmez.
    /// </summary>
    public string? AllTilesJson { get; set; }

    #endregion

    #region Provably Fair

    /// <summary>
    /// Commitment hash (oyun başında oyunculara gösterilir).
    /// </summary>
    public string? CommitmentHash { get; set; }

    /// <summary>
    /// Server seed (oyun sonunda açıklanır).
    /// </summary>
    public string? ServerSeed { get; set; }

    /// <summary>
    /// Initial state (oyun sonunda açıklanır).
    /// </summary>
    public string? InitialState { get; set; }

    /// <summary>
    /// Oyun sayacı (nonce).
    /// </summary>
    public long Nonce { get; set; }

    #endregion

    #region Zaman Damgaları

    /// <summary>
    /// Oyunun başlama zamanı.
    /// </summary>
    public DateTime? GameStartedAt { get; set; }

    /// <summary>
    /// Mevcut turun başlama zamanı.
    /// Zaman aşımı kontrolü için kullanılır.
    /// </summary>
    public DateTime? TurnStartedAt { get; set; }

    /// <summary>
    /// Mevcut turun bitiş zamanı (zaman aşımı).
    /// </summary>
    public DateTime? TurnExpiresAt { get; set; }

    #endregion

    #region Tur Yönetimi

    /// <summary>
    /// Mevcut tur numarası (1'den başlar).
    /// </summary>
    public int TurnNumber { get; set; } = 0;

    /// <summary>
    /// Mevcut tur fazı.
    /// 0 = WaitingForDraw, 1 = WaitingForDiscard, 2 = TurnCompleted
    /// </summary>
    public int TurnPhase { get; set; } = 0;

    /// <summary>
    /// Tur süresi (saniye).
    /// </summary>
    public int TurnDurationSeconds { get; set; } = 15;

    /// <summary>
    /// Bu turda taş çekildi mi?
    /// </summary>
    public bool HasDrawnThisTurn { get; set; } = false;

    /// <summary>
    /// Otomatik oynatma aktif mi?
    /// </summary>
    public bool IsAutoPlay { get; set; } = false;

    /// <summary>
    /// Oyun fazı.
    /// 0 = WaitingForPlayers, 1 = ReadyToStart, 2 = Shuffling, 3 = Dealing, 4 = Playing, 5 = Finished, 6 = Cancelled
    /// </summary>
    public int GamePhase { get; set; } = 0;

    #endregion
}

/// <summary>
/// Redis'te saklanan oyuncu durumu.
/// </summary>
public class PlayerState
{
    /// <summary>
    /// Oyuncu benzersiz kimliği.
    /// </summary>
    public required Guid PlayerId { get; set; }

    /// <summary>
    /// Oyuncu görünen adı.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Masa pozisyonu.
    /// </summary>
    public PlayerPosition Position { get; set; }

    /// <summary>
    /// Oyuncunun elindeki taş ID'leri.
    /// </summary>
    public List<int> HandTileIds { get; set; } = new();

    /// <summary>
    /// Mevcut SignalR bağlantı ID'si.
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Oyuncu bağlı mı?
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Son bağlantı zamanı.
    /// </summary>
    public DateTime? LastConnectedAt { get; set; }

    /// <summary>
    /// Bağlantı kopma zamanı.
    /// </summary>
    public DateTime? DisconnectedAt { get; set; }

    /// <summary>
    /// Son aktivite zamanı.
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Oyuncunun sırası mı?
    /// </summary>
    public bool IsCurrentTurn { get; set; }

    /// <summary>
    /// Bu turda taş çekti mi?
    /// </summary>
    public bool HasDrawnThisTurn { get; set; }
}

/// <summary>
/// Bağlantı-Oda eşleştirmesi.
/// Reconnection için kullanılır.
/// </summary>
public class ConnectionMapping
{
    /// <summary>
    /// Oyuncu ID'si.
    /// </summary>
    public required Guid PlayerId { get; set; }

    /// <summary>
    /// Oda ID'si.
    /// </summary>
    public required Guid RoomId { get; set; }

    /// <summary>
    /// Son bağlantı ID'si.
    /// </summary>
    public string? LastConnectionId { get; set; }

    /// <summary>
    /// Son bağlantı zamanı.
    /// </summary>
    public DateTime LastConnectedAt { get; set; }
}
