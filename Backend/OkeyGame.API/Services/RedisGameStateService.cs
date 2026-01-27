using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OkeyGame.API.Models;
using StackExchange.Redis;

namespace OkeyGame.API.Services;

/// <summary>
/// Redis tabanlı oyun durumu yönetim servisi.
/// 
/// NEDEN REDIS?
/// - Sunucu yeniden başlasa bile veriler korunur
/// - Birden fazla sunucu instance'ı aynı veriyi paylaşabilir (scale-out)
/// - Yüksek performanslı okuma/yazma
/// - TTL (Time-To-Live) ile otomatik temizlik
/// </summary>
public interface IGameStateService
{
    // Oda İşlemleri
    Task<GameRoomState?> GetRoomStateAsync(Guid roomId);
    Task SaveRoomStateAsync(GameRoomState state);
    Task DeleteRoomStateAsync(Guid roomId);
    Task<bool> RoomExistsAsync(Guid roomId);

    // Bağlantı Eşleştirme (Reconnection için)
    Task SaveConnectionMappingAsync(Guid playerId, Guid roomId, string connectionId);
    Task<ConnectionMapping?> GetConnectionMappingAsync(Guid playerId);
    Task RemoveConnectionMappingAsync(Guid playerId);

    // Aktif Odalar
    Task<List<Guid>> GetActiveRoomIdsAsync();
    Task AddToActiveRoomsAsync(Guid roomId);
    Task RemoveFromActiveRoomsAsync(Guid roomId);

    // Lock Mekanizması (Concurrency için)
    Task<IDisposable?> AcquireLockAsync(Guid roomId, TimeSpan timeout);
}

/// <summary>
/// Redis implementasyonu.
/// </summary>
public class RedisGameStateService : IGameStateService
{
    #region Sabitler

    private const string RoomKeyPrefix = "okey:room:";
    private const string ConnectionKeyPrefix = "okey:connection:";
    private const string ActiveRoomsKey = "okey:activerooms";
    private const string LockKeyPrefix = "okey:lock:";

    // TTL Süreleri
    private static readonly TimeSpan RoomTTL = TimeSpan.FromHours(24);
    private static readonly TimeSpan ConnectionTTL = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LockTTL = TimeSpan.FromSeconds(10);

    #endregion

    #region Alanlar

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisGameStateService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    #endregion

    #region Constructor

    public RedisGameStateService(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        ILogger<RedisGameStateService> logger)
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    #endregion

    #region Oda İşlemleri

    /// <summary>
    /// Oda durumunu Redis'ten okur.
    /// </summary>
    public async Task<GameRoomState?> GetRoomStateAsync(Guid roomId)
    {
        try
        {
            var key = GetRoomKey(roomId);
            var json = await _cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<GameRoomState>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oda durumu okunamadı: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// Oda durumunu Redis'e kaydeder.
    /// </summary>
    public async Task SaveRoomStateAsync(GameRoomState state)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(state);

            state.UpdatedAt = DateTime.UtcNow;

            var key = GetRoomKey(state.RoomId);
            var json = JsonSerializer.Serialize(state, _jsonOptions);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = RoomTTL
            };

            await _cache.SetStringAsync(key, json, options);

            _logger.LogDebug("Oda durumu kaydedildi: {RoomId}", state.RoomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oda durumu kaydedilemedi: {RoomId}", state.RoomId);
            throw;
        }
    }

    /// <summary>
    /// Oda durumunu Redis'ten siler.
    /// </summary>
    public async Task DeleteRoomStateAsync(Guid roomId)
    {
        try
        {
            var key = GetRoomKey(roomId);
            await _cache.RemoveAsync(key);
            await RemoveFromActiveRoomsAsync(roomId);

            _logger.LogInformation("Oda silindi: {RoomId}", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oda silinemedi: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// Odanın var olup olmadığını kontrol eder.
    /// </summary>
    public async Task<bool> RoomExistsAsync(Guid roomId)
    {
        var key = GetRoomKey(roomId);
        var value = await _cache.GetStringAsync(key);
        return !string.IsNullOrEmpty(value);
    }

    #endregion

    #region Bağlantı Eşleştirme

    /// <summary>
    /// Oyuncu-Oda-Bağlantı eşleştirmesini kaydeder.
    /// Reconnection için kritik öneme sahiptir.
    /// </summary>
    public async Task SaveConnectionMappingAsync(Guid playerId, Guid roomId, string connectionId)
    {
        try
        {
            var mapping = new ConnectionMapping
            {
                PlayerId = playerId,
                RoomId = roomId,
                LastConnectionId = connectionId,
                LastConnectedAt = DateTime.UtcNow
            };

            var key = GetConnectionKey(playerId);
            var json = JsonSerializer.Serialize(mapping, _jsonOptions);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ConnectionTTL
            };

            await _cache.SetStringAsync(key, json, options);

            _logger.LogDebug("Bağlantı eşleştirmesi kaydedildi: {PlayerId} -> {RoomId}", playerId, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı eşleştirmesi kaydedilemedi: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// Oyuncunun bağlantı eşleştirmesini getirir.
    /// </summary>
    public async Task<ConnectionMapping?> GetConnectionMappingAsync(Guid playerId)
    {
        try
        {
            var key = GetConnectionKey(playerId);
            var json = await _cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<ConnectionMapping>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı eşleştirmesi okunamadı: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// Oyuncunun bağlantı eşleştirmesini siler.
    /// </summary>
    public async Task RemoveConnectionMappingAsync(Guid playerId)
    {
        var key = GetConnectionKey(playerId);
        await _cache.RemoveAsync(key);
    }

    #endregion

    #region Aktif Odalar

    /// <summary>
    /// Aktif oda ID'lerini getirir.
    /// </summary>
    public async Task<List<Guid>> GetActiveRoomIdsAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var members = await db.SetMembersAsync(ActiveRoomsKey);

            return members
                .Where(m => m.HasValue)
                .Select(m => Guid.Parse(m.ToString()))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aktif odalar alınamadı");
            return new List<Guid>();
        }
    }

    /// <summary>
    /// Aktif odalara ekler.
    /// </summary>
    public async Task AddToActiveRoomsAsync(Guid roomId)
    {
        var db = _redis.GetDatabase();
        await db.SetAddAsync(ActiveRoomsKey, roomId.ToString());
    }

    /// <summary>
    /// Aktif odalardan çıkarır.
    /// </summary>
    public async Task RemoveFromActiveRoomsAsync(Guid roomId)
    {
        var db = _redis.GetDatabase();
        await db.SetRemoveAsync(ActiveRoomsKey, roomId.ToString());
    }

    #endregion

    #region Lock Mekanizması

    /// <summary>
    /// Oda için distributed lock alır.
    /// Race condition'ları önlemek için kullanılır.
    /// </summary>
    public async Task<IDisposable?> AcquireLockAsync(Guid roomId, TimeSpan timeout)
    {
        var lockKey = GetLockKey(roomId);
        var lockValue = Guid.NewGuid().ToString();
        var db = _redis.GetDatabase();

        var acquired = await db.StringSetAsync(
            lockKey,
            lockValue,
            LockTTL,
            When.NotExists);

        if (!acquired)
        {
            // Lock alınamadı, timeout süresince bekle ve tekrar dene
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(50);
                acquired = await db.StringSetAsync(lockKey, lockValue, LockTTL, When.NotExists);
                if (acquired) break;
            }
        }

        if (!acquired)
        {
            _logger.LogWarning("Lock alınamadı: {RoomId}", roomId);
            return null;
        }

        return new RedisLock(db, lockKey, lockValue);
    }

    #endregion

    #region Yardımcı Metotlar

    private static string GetRoomKey(Guid roomId) => $"{RoomKeyPrefix}{roomId}";
    private static string GetConnectionKey(Guid playerId) => $"{ConnectionKeyPrefix}{playerId}";
    private static string GetLockKey(Guid roomId) => $"{LockKeyPrefix}{roomId}";

    #endregion

    #region Nested Lock Class

    private class RedisLock : IDisposable
    {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _value;
        private bool _disposed;

        public RedisLock(IDatabase db, string key, string value)
        {
            _db = db;
            _key = key;
            _value = value;
        }

        public void Dispose()
        {
            if (_disposed) return;

            // Lock'u sadece biz aldıysak serbest bırak
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            _db.ScriptEvaluate(script, new RedisKey[] { _key }, new RedisValue[] { _value });
            _disposed = true;
        }
    }

    #endregion
}
