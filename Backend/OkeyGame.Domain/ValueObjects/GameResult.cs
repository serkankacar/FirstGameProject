using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.ValueObjects;

/// <summary>
/// Oyuncu sonuç bilgisi (value object).
/// GameHistory içinde JSON olarak saklanır.
/// </summary>
public sealed record PlayerResult
{
    /// <summary>Oyuncu ID'si.</summary>
    public Guid UserId { get; init; }

    /// <summary>Kullanıcı adı.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Masa pozisyonu.</summary>
    public PlayerPosition Position { get; init; }

    /// <summary>Kazandı mı?</summary>
    public bool IsWinner { get; init; }

    /// <summary>Final el puanı.</summary>
    public int FinalScore { get; init; }

    /// <summary>Çip değişimi.</summary>
    public long ChipChange { get; init; }

    /// <summary>ELO değişimi.</summary>
    public int EloChange { get; init; }

    /// <summary>Yeni ELO puanı.</summary>
    public int NewEloScore { get; init; }

    /// <summary>Oynanan tur sayısı.</summary>
    public int TurnsPlayed { get; init; }
}

/// <summary>
/// Oyun sonuç özeti (value object).
/// Oyun bittiğinde oluşturulur.
/// </summary>
public sealed record GameResult
{
    /// <summary>Oyun ID'si.</summary>
    public Guid GameHistoryId { get; init; }

    /// <summary>Oda ID'si.</summary>
    public Guid RoomId { get; init; }

    /// <summary>Kazanan ID.</summary>
    public Guid WinnerId { get; init; }

    /// <summary>Kazanma tipi.</summary>
    public WinType WinType { get; init; }

    /// <summary>Kazanma puanı.</summary>
    public int WinScore { get; init; }

    /// <summary>Masa bahsi.</summary>
    public long TableStake { get; init; }

    /// <summary>Toplam pot.</summary>
    public long TotalPot { get; init; }

    /// <summary>Rake miktarı.</summary>
    public long RakeAmount { get; init; }

    /// <summary>Kazanan payout.</summary>
    public long WinnerPayout { get; init; }

    /// <summary>Tüm oyuncu sonuçları.</summary>
    public IReadOnlyList<PlayerResult> PlayerResults { get; init; } = Array.Empty<PlayerResult>();

    /// <summary>Oyun süresi (saniye).</summary>
    public int DurationSeconds { get; init; }

    /// <summary>Toplam tur sayısı.</summary>
    public int TotalTurns { get; init; }
}
