namespace OkeyGame.Application.Interfaces;

/// <summary>
/// Leaderboard servisi interface'i.
/// Redis Sorted Set ile yüksek performanslı sıralama sağlar.
/// </summary>
public interface ILeaderboardService
{
    #region Sıralama Sorguları

    /// <summary>
    /// ELO sıralamasında ilk N oyuncuyu getirir.
    /// </summary>
    /// <param name="count">Getirilecek oyuncu sayısı</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Sıralı oyuncu listesi</returns>
    Task<IReadOnlyList<LeaderboardEntry>> GetTopPlayersAsync(
        int count = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Oyuncunun sıralamasını getirir.
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Sıralama (1-based, bulunamazsa -1)</returns>
    Task<long> GetPlayerRankAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Oyuncunun sıralamasını ve çevresindeki oyuncuları getirir.
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="range">Çevre genişliği (± range)</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Oyuncu ve çevresindekiler</returns>
    Task<LeaderboardNeighbors> GetPlayerWithNeighborsAsync(
        Guid userId,
        int range = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirli sıralama aralığındaki oyuncuları getirir.
    /// </summary>
    /// <param name="start">Başlangıç sırası (0-based)</param>
    /// <param name="stop">Bitiş sırası (inclusive)</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task<IReadOnlyList<LeaderboardEntry>> GetPlayersInRangeAsync(
        long start,
        long stop,
        CancellationToken cancellationToken = default);

    #endregion

    #region Skor Güncelleme

    /// <summary>
    /// Oyuncunun ELO puanını günceller.
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="eloScore">Yeni ELO puanı</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task UpdatePlayerScoreAsync(
        Guid userId,
        int eloScore,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Birden fazla oyuncunun skorunu günceller (batch).
    /// </summary>
    Task UpdatePlayerScoresAsync(
        IEnumerable<(Guid UserId, int EloScore)> scores,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Oyuncuyu leaderboard'dan kaldırır.
    /// </summary>
    Task RemovePlayerAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Senkronizasyon

    /// <summary>
    /// Veritabanından tüm aktif oyuncuları Redis'e senkronize eder.
    /// Genellikle uygulama başlangıcında veya periyodik olarak çalıştırılır.
    /// </summary>
    Task SyncFromDatabaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaderboard'daki toplam oyuncu sayısını getirir.
    /// </summary>
    Task<long> GetTotalPlayersCountAsync(CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Leaderboard girişi.
/// </summary>
public record LeaderboardEntry
{
    /// <summary>Kullanıcı ID.</summary>
    public Guid UserId { get; init; }

    /// <summary>Kullanıcı adı.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Görünen ad.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>ELO puanı.</summary>
    public int EloScore { get; init; }

    /// <summary>Sıralama (1-based).</summary>
    public long Rank { get; init; }

    /// <summary>Toplam oyun sayısı.</summary>
    public int TotalGamesPlayed { get; init; }

    /// <summary>Kazanma oranı (%).</summary>
    public double WinRate { get; init; }
}

/// <summary>
/// Oyuncu ve çevresindekiler.
/// </summary>
public record LeaderboardNeighbors
{
    /// <summary>Hedef oyuncu.</summary>
    public LeaderboardEntry? Player { get; init; }

    /// <summary>Üstteki oyuncular (daha yüksek sıra).</summary>
    public IReadOnlyList<LeaderboardEntry> Above { get; init; } = Array.Empty<LeaderboardEntry>();

    /// <summary>Alttaki oyuncular (daha düşük sıra).</summary>
    public IReadOnlyList<LeaderboardEntry> Below { get; init; } = Array.Empty<LeaderboardEntry>();
}
