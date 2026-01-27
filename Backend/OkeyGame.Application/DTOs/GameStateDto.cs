using OkeyGame.Domain.Enums;

namespace OkeyGame.Application.DTOs;

/// <summary>
/// İstemciye gönderilecek oyun durumu DTO'su.
/// 
/// GÜVENLİK NOTU:
/// - Rakiplerin elleri ASLA gönderilmez
/// - Kapalı taşlar (deste) ASLA gönderilmez
/// - Sadece oyuncunun kendi eli ve açık bilgiler gönderilir
/// - Bu, hile yapılmasını önler
/// </summary>
public class GameStateDto
{
    /// <summary>
    /// Oda benzersiz kimliği.
    /// </summary>
    public required Guid RoomId { get; init; }

    /// <summary>
    /// Oyunun mevcut durumu.
    /// </summary>
    public required GameState State { get; init; }

    /// <summary>
    /// Sıradaki oyuncunun pozisyonu.
    /// </summary>
    public required PlayerPosition CurrentTurnPosition { get; init; }

    /// <summary>
    /// İstemcinin kendi oyuncu bilgileri ve eli.
    /// </summary>
    public required PlayerDto Self { get; init; }

    /// <summary>
    /// Rakiplerin bilgileri (elleri olmadan).
    /// </summary>
    public required List<OpponentDto> Opponents { get; init; }

    /// <summary>
    /// Gösterge taşı bilgisi.
    /// Okey'i belirlemek için kullanılır.
    /// </summary>
    public required TileDto IndicatorTile { get; init; }

    /// <summary>
    /// Destede kalan taş sayısı.
    /// İstemci sadece sayıyı bilir, taşları değil.
    /// </summary>
    public required int RemainingTileCount { get; init; }

    /// <summary>
    /// Atık yığınının en üstündeki taş (varsa).
    /// Bu taş açık olduğu için gösterilebilir.
    /// </summary>
    public TileDto? DiscardPileTopTile { get; init; }

    /// <summary>
    /// Oyunun başlama zamanı (UTC).
    /// </summary>
    public required DateTime GameStartedAt { get; init; }

    /// <summary>
    /// Sunucu zaman damgası (UTC).
    /// İstemci-sunucu senkronizasyonu için.
    /// </summary>
    public required DateTime ServerTimestamp { get; init; }
}

/// <summary>
/// İstemcinin kendi oyuncu bilgilerini içeren DTO.
/// El bilgisi dahildir.
/// </summary>
public class PlayerDto
{
    /// <summary>
    /// Oyuncu benzersiz kimliği.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Oyuncu görünen adı.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Oyuncunun masa pozisyonu.
    /// </summary>
    public required PlayerPosition Position { get; init; }

    /// <summary>
    /// Oyuncunun elindeki taşlar.
    /// Sadece kendi istemcisine gönderilir.
    /// </summary>
    public required List<TileDto> Hand { get; init; }

    /// <summary>
    /// Sıra bu oyuncuda mı?
    /// </summary>
    public required bool IsCurrentTurn { get; init; }

    /// <summary>
    /// Bağlantı durumu.
    /// </summary>
    public required bool IsConnected { get; init; }
}

/// <summary>
/// Rakip oyuncu bilgilerini içeren DTO.
/// El bilgisi İÇERMEZ (güvenlik için).
/// </summary>
public class OpponentDto
{
    /// <summary>
    /// Rakip oyuncu benzersiz kimliği.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Rakip oyuncu görünen adı.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Rakibin masa pozisyonu.
    /// </summary>
    public required PlayerPosition Position { get; init; }

    /// <summary>
    /// Rakibin elindeki taş sayısı.
    /// Sadece sayı gönderilir, taşlar DEĞİL.
    /// </summary>
    public required int TileCount { get; init; }

    /// <summary>
    /// Sıra bu rakipte mi?
    /// </summary>
    public required bool IsCurrentTurn { get; init; }

    /// <summary>
    /// Bağlantı durumu.
    /// </summary>
    public required bool IsConnected { get; init; }
}

/// <summary>
/// Taş bilgilerini içeren DTO.
/// </summary>
public class TileDto
{
    /// <summary>
    /// Taş benzersiz kimliği.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Taş rengi.
    /// </summary>
    public required TileColor Color { get; init; }

    /// <summary>
    /// Taş değeri (1-13).
    /// </summary>
    public required int Value { get; init; }

    /// <summary>
    /// Bu taş Okey (Joker) mi?
    /// </summary>
    public required bool IsOkey { get; init; }

    /// <summary>
    /// Bu taş Sahte Okey mi?
    /// </summary>
    public required bool IsFalseJoker { get; init; }
}

/// <summary>
/// Oyun başlangıç bilgilerini içeren DTO.
/// Oyun başladığında bir kez gönderilir.
/// </summary>
public class GameStartDto
{
    /// <summary>
    /// Oda benzersiz kimliği.
    /// </summary>
    public required Guid RoomId { get; init; }

    /// <summary>
    /// İstemcinin başlangıç oyun durumu.
    /// </summary>
    public required GameStateDto InitialState { get; init; }

    /// <summary>
    /// Provably Fair için sunucu seed hash'i.
    /// Oyun sonunda gerçek seed açıklanarak doğrulama yapılabilir.
    /// </summary>
    public required string ServerSeedHash { get; init; }
}

/// <summary>
/// Taş çekme işlemi sonucu DTO.
/// </summary>
public class DrawTileResultDto
{
    /// <summary>
    /// İşlem başarılı mı?
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Çekilen taş (başarılıysa).
    /// </summary>
    public TileDto? DrawnTile { get; init; }

    /// <summary>
    /// Hata mesajı (başarısızsa).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Güncellenmiş oyun durumu.
    /// </summary>
    public GameStateDto? UpdatedState { get; init; }
}

/// <summary>
/// Taş atma işlemi sonucu DTO.
/// </summary>
public class DiscardTileResultDto
{
    /// <summary>
    /// İşlem başarılı mı?
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Hata mesajı (başarısızsa).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Güncellenmiş oyun durumu.
    /// </summary>
    public GameStateDto? UpdatedState { get; init; }
}
