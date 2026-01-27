using Microsoft.Extensions.Logging;
using OkeyGame.Application.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace OkeyGame.Infrastructure.Services;

/// <summary>
/// Redis Sorted Set tabanlı Leaderboard servisi.
/// 
/// TASARIM:
/// - Sorted Set: ZADD, ZRANK, ZREVRANK, ZRANGE ile O(log n) işlemler
/// - Hash: Kullanıcı detayları için (username, displayName, stats)
/// - Yüksek performans: SQL sorgusu yerine Redis
/// 
/// REDIS KEY YAPISI:
/// - leaderboard:elo -> Sorted Set (score: elo, member: userId)
/// - leaderboard:user:{userId} -> Hash (username, displayName, gamesPlayed, winRate)
/// </summary>
public class RedisLeaderboardService : ILeaderboardService
{
    #region Sabitler

    private const string LeaderboardKey = "leaderboard:elo";
    private const string UserKeyPrefix = "leaderboard:user:";
    private const int DefaultTopCount = 100;
    private const int MaxTopCount = 1000;

    #endregion

    private readonly IConnectionMultiplexer _redis;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<RedisLeaderboardService> _logger;

    public RedisLeaderboardService(
        IConnectionMultiplexer redis,
        IUserRepository userRepository,
        ILogger<RedisLeaderboardService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private IDatabase Database => _redis.GetDatabase();

    #region Sıralama Sorguları

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopPlayersAsync(
        int count = DefaultTopCount,
        CancellationToken cancellationToken = default)
    {
        count = Math.Min(count, MaxTopCount);

        try
        {
            // ZREVRANGE ile en yüksek skordan düşüğe sırala
            var results = await Database.SortedSetRangeByRankWithScoresAsync(
                LeaderboardKey,
                start: 0,
                stop: count - 1,
                order: Order.Descending);

            var entries = new List<LeaderboardEntry>();
            long rank = 1;

            foreach (var result in results)
            {
                var userId = Guid.Parse(result.Element.ToString());
                var userDetails = await GetUserDetailsAsync(userId);

                entries.Add(new LeaderboardEntry
                {
                    UserId = userId,
                    Username = userDetails.Username,
                    DisplayName = userDetails.DisplayName,
                    EloScore = (int)result.Score,
                    Rank = rank++,
                    TotalGamesPlayed = userDetails.TotalGamesPlayed,
                    WinRate = userDetails.WinRate
                });
            }

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get top players from Redis");
            
            // Fallback: Veritabanından getir
            return await GetTopPlayersFromDatabaseAsync(count, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<long> GetPlayerRankAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ZREVRANK: 0-based, null if not found
            var rank = await Database.SortedSetRankAsync(
                LeaderboardKey,
                userId.ToString(),
                Order.Descending);

            return rank.HasValue ? rank.Value + 1 : -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get player rank from Redis for {UserId}", userId);
            
            // Fallback
            return await _userRepository.GetEloRankAsync(userId, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<LeaderboardNeighbors> GetPlayerWithNeighborsAsync(
        Guid userId,
        int range = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rank = await Database.SortedSetRankAsync(
                LeaderboardKey,
                userId.ToString(),
                Order.Descending);

            if (!rank.HasValue)
            {
                return new LeaderboardNeighbors();
            }

            long playerRank = rank.Value;
            long startAbove = Math.Max(0, playerRank - range);
            long endBelow = playerRank + range;

            // Tüm aralığı getir
            var results = await Database.SortedSetRangeByRankWithScoresAsync(
                LeaderboardKey,
                start: startAbove,
                stop: endBelow,
                order: Order.Descending);

            var entries = new List<LeaderboardEntry>();
            long currentRank = startAbove + 1;

            foreach (var result in results)
            {
                var entryUserId = Guid.Parse(result.Element.ToString());
                var userDetails = await GetUserDetailsAsync(entryUserId);

                entries.Add(new LeaderboardEntry
                {
                    UserId = entryUserId,
                    Username = userDetails.Username,
                    DisplayName = userDetails.DisplayName,
                    EloScore = (int)result.Score,
                    Rank = currentRank++,
                    TotalGamesPlayed = userDetails.TotalGamesPlayed,
                    WinRate = userDetails.WinRate
                });
            }

            var player = entries.FirstOrDefault(e => e.UserId == userId);
            var above = entries.Where(e => e.Rank < player?.Rank).ToList();
            var below = entries.Where(e => e.Rank > player?.Rank).ToList();

            return new LeaderboardNeighbors
            {
                Player = player,
                Above = above,
                Below = below
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get player neighbors from Redis");
            return new LeaderboardNeighbors();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeaderboardEntry>> GetPlayersInRangeAsync(
        long start,
        long stop,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await Database.SortedSetRangeByRankWithScoresAsync(
                LeaderboardKey,
                start: start,
                stop: stop,
                order: Order.Descending);

            var entries = new List<LeaderboardEntry>();
            long rank = start + 1;

            foreach (var result in results)
            {
                var userId = Guid.Parse(result.Element.ToString());
                var userDetails = await GetUserDetailsAsync(userId);

                entries.Add(new LeaderboardEntry
                {
                    UserId = userId,
                    Username = userDetails.Username,
                    DisplayName = userDetails.DisplayName,
                    EloScore = (int)result.Score,
                    Rank = rank++,
                    TotalGamesPlayed = userDetails.TotalGamesPlayed,
                    WinRate = userDetails.WinRate
                });
            }

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get players in range from Redis");
            return Array.Empty<LeaderboardEntry>();
        }
    }

    #endregion

    #region Skor Güncelleme

    /// <inheritdoc />
    public async Task UpdatePlayerScoreAsync(
        Guid userId,
        int eloScore,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Sorted Set'e ekle/güncelle
            await Database.SortedSetAddAsync(
                LeaderboardKey,
                userId.ToString(),
                eloScore);

            _logger.LogDebug(
                "Updated leaderboard score for {UserId}: {EloScore}",
                userId, eloScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update player score in Redis for {UserId}", userId);
            // Redis hatası kritik değil, oyun devam edebilir
        }
    }

    /// <inheritdoc />
    public async Task UpdatePlayerScoresAsync(
        IEnumerable<(Guid UserId, int EloScore)> scores,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = scores
                .Select(s => new SortedSetEntry(s.UserId.ToString(), s.EloScore))
                .ToArray();

            if (entries.Length > 0)
            {
                await Database.SortedSetAddAsync(LeaderboardKey, entries);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch update player scores in Redis");
        }
    }

    /// <inheritdoc />
    public async Task RemovePlayerAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Database.SortedSetRemoveAsync(LeaderboardKey, userId.ToString());
            await Database.KeyDeleteAsync(GetUserKey(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove player from Redis for {UserId}", userId);
        }
    }

    #endregion

    #region Senkronizasyon

    /// <inheritdoc />
    public async Task SyncFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting leaderboard sync from database...");

            // Veritabanından tüm aktif kullanıcıları getir
            var topPlayers = await _userRepository.GetTopByEloAsync(10000, cancellationToken);

            if (topPlayers.Count == 0)
            {
                _logger.LogWarning("No players found for leaderboard sync");
                return;
            }

            var batch = Database.CreateBatch();
            var tasks = new List<Task>();

            foreach (var user in topPlayers)
            {
                // Sorted Set'e ekle
                tasks.Add(batch.SortedSetAddAsync(
                    LeaderboardKey,
                    user.Id.ToString(),
                    user.EloScore));

                // Kullanıcı detaylarını Hash'e kaydet
                tasks.Add(batch.HashSetAsync(
                    GetUserKey(user.Id),
                    new HashEntry[]
                    {
                        new("username", user.Username),
                        new("displayName", user.DisplayName),
                        new("gamesPlayed", user.TotalGamesPlayed),
                        new("winRate", user.WinRate.ToString("F2"))
                    }));
            }

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "Leaderboard sync completed. Synced {Count} players",
                topPlayers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leaderboard sync from database failed");
        }
    }

    /// <inheritdoc />
    public async Task<long> GetTotalPlayersCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Database.SortedSetLengthAsync(LeaderboardKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get total players count from Redis");
            return 0;
        }
    }

    #endregion

    #region Yardımcı Metodlar

    private static string GetUserKey(Guid userId) => $"{UserKeyPrefix}{userId}";

    private async Task<UserDetails> GetUserDetailsAsync(Guid userId)
    {
        try
        {
            var hash = await Database.HashGetAllAsync(GetUserKey(userId));

            if (hash.Length > 0)
            {
                return new UserDetails
                {
                    Username = hash.FirstOrDefault(h => h.Name == "username").Value.ToString() ?? "",
                    DisplayName = hash.FirstOrDefault(h => h.Name == "displayName").Value.ToString() ?? "",
                    TotalGamesPlayed = (int)hash.FirstOrDefault(h => h.Name == "gamesPlayed").Value,
                    WinRate = double.TryParse(
                        hash.FirstOrDefault(h => h.Name == "winRate").Value.ToString(),
                        out var wr) ? wr : 0
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user details from Redis for {UserId}", userId);
        }

        // Fallback: Veritabanından getir
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            // Cache'e kaydet
            await CacheUserDetailsAsync(user);

            return new UserDetails
            {
                Username = user.Username,
                DisplayName = user.DisplayName,
                TotalGamesPlayed = user.TotalGamesPlayed,
                WinRate = user.WinRate
            };
        }

        return new UserDetails();
    }

    private async Task CacheUserDetailsAsync(Domain.Entities.User user)
    {
        try
        {
            await Database.HashSetAsync(
                GetUserKey(user.Id),
                new HashEntry[]
                {
                    new("username", user.Username),
                    new("displayName", user.DisplayName),
                    new("gamesPlayed", user.TotalGamesPlayed),
                    new("winRate", user.WinRate.ToString("F2"))
                });
        }
        catch { }
    }

    private async Task<IReadOnlyList<LeaderboardEntry>> GetTopPlayersFromDatabaseAsync(
        int count,
        CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetTopByEloAsync(count, cancellationToken);

        return users.Select((u, i) => new LeaderboardEntry
        {
            UserId = u.Id,
            Username = u.Username,
            DisplayName = u.DisplayName,
            EloScore = u.EloScore,
            Rank = i + 1,
            TotalGamesPlayed = u.TotalGamesPlayed,
            WinRate = u.WinRate
        }).ToList();
    }

    private record UserDetails
    {
        public string Username { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public int TotalGamesPlayed { get; init; }
        public double WinRate { get; init; }
    }

    #endregion
}
