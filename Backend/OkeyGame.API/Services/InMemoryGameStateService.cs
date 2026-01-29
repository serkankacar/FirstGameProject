using System.Collections.Concurrent;
using System.Text.Json;
using OkeyGame.API.Models;

namespace OkeyGame.API.Services;

/// <summary>
/// In-Memory oyun durumu yönetim servisi.
/// Development ve test için kullanılır.
/// </summary>
public class InMemoryGameStateService : IGameStateService
{
    private readonly ConcurrentDictionary<Guid, GameRoomState> _rooms = new();
    private readonly ConcurrentDictionary<Guid, ConnectionMapping> _connections = new();
    private readonly ConcurrentDictionary<Guid, bool> _activeRooms = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    private readonly ILogger<InMemoryGameStateService> _logger;

    public InMemoryGameStateService(ILogger<InMemoryGameStateService> logger)
    {
        _logger = logger;
        _logger.LogInformation("InMemoryGameStateService initialized (Development Mode)");
    }

    #region Oda İşlemleri

    public Task<GameRoomState?> GetRoomStateAsync(Guid roomId)
    {
        _rooms.TryGetValue(roomId, out var state);
        return Task.FromResult(state);
    }

    public Task SaveRoomStateAsync(GameRoomState state)
    {
        _rooms[state.RoomId] = state;
        _logger.LogDebug("Room state saved: {RoomId}", state.RoomId);
        return Task.CompletedTask;
    }

    public Task DeleteRoomStateAsync(Guid roomId)
    {
        _rooms.TryRemove(roomId, out _);
        _activeRooms.TryRemove(roomId, out _);
        _logger.LogDebug("Room state deleted: {RoomId}", roomId);
        return Task.CompletedTask;
    }

    public Task<bool> RoomExistsAsync(Guid roomId)
    {
        return Task.FromResult(_rooms.ContainsKey(roomId));
    }

    #endregion

    #region Bağlantı Eşleştirme

    public Task SaveConnectionMappingAsync(Guid playerId, Guid roomId, string connectionId)
    {
        _connections[playerId] = new ConnectionMapping
        {
            PlayerId = playerId,
            RoomId = roomId,
            LastConnectionId = connectionId,
            LastConnectedAt = DateTime.UtcNow
        };
        _logger.LogDebug("Connection mapping saved: Player {PlayerId} -> Room {RoomId}", playerId, roomId);
        return Task.CompletedTask;
    }

    public Task<ConnectionMapping?> GetConnectionMappingAsync(Guid playerId)
    {
        _connections.TryGetValue(playerId, out var mapping);
        return Task.FromResult(mapping);
    }

    public Task RemoveConnectionMappingAsync(Guid playerId)
    {
        _connections.TryRemove(playerId, out _);
        _logger.LogDebug("Connection mapping removed: {PlayerId}", playerId);
        return Task.CompletedTask;
    }

    #endregion

    #region Aktif Odalar

    public Task<List<Guid>> GetActiveRoomIdsAsync()
    {
        return Task.FromResult(_activeRooms.Keys.ToList());
    }

    public Task AddToActiveRoomsAsync(Guid roomId)
    {
        _activeRooms[roomId] = true;
        _logger.LogDebug("Room added to active: {RoomId}", roomId);
        return Task.CompletedTask;
    }

    public Task RemoveFromActiveRoomsAsync(Guid roomId)
    {
        _activeRooms.TryRemove(roomId, out _);
        _logger.LogDebug("Room removed from active: {RoomId}", roomId);
        return Task.CompletedTask;
    }

    #endregion

    #region Lock Mekanizması

    public async Task<IDisposable?> AcquireLockAsync(Guid roomId, TimeSpan timeout)
    {
        var semaphore = _locks.GetOrAdd(roomId, _ => new SemaphoreSlim(1, 1));
        
        if (await semaphore.WaitAsync(timeout))
        {
            return new LockReleaser(semaphore);
        }
        
        return null;
    }

    private class LockReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public LockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore.Release();
                _disposed = true;
            }
        }
    }

    #endregion

    #region Debug/Test Methods

    public int GetRoomCount() => _rooms.Count;
    public int GetConnectionCount() => _connections.Count;
    public int GetActiveRoomCount() => _activeRooms.Count;

    public void ClearAll()
    {
        _rooms.Clear();
        _connections.Clear();
        _activeRooms.Clear();
        _logger.LogWarning("All in-memory state cleared!");
    }

    #endregion
}
