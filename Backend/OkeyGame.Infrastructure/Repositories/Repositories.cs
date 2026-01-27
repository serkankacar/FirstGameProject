using Microsoft.EntityFrameworkCore;
using OkeyGame.Application.Interfaces;
using OkeyGame.Domain.Entities;
using OkeyGame.Infrastructure.Persistence;

namespace OkeyGame.Infrastructure.Repositories;

/// <summary>
/// User repository implementasyonu.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly OkeyGameDbContext _context;

    public UserRepository(OkeyGameDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Sorgular

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.ToLowerInvariant();
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username == normalizedUsername, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _context.Users
            .Where(u => idList.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.ToLowerInvariant();
        return !await _context.Users
            .AnyAsync(u => u.Username == normalizedUsername, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AnyAsync(u => u.Id == id, cancellationToken);
    }

    #endregion

    #region Komutlar

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        return user;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        _context.Users.UpdateRange(users);
        return Task.CompletedTask;
    }

    #endregion

    #region Leaderboard (Fallback)

    public async Task<IReadOnlyList<User>> GetTopByEloAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Where(u => u.IsActive)
            .OrderByDescending(u => u.EloScore)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetEloRankAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user == null) return -1;

        var rank = await _context.Users
            .Where(u => u.IsActive && u.EloScore > user.EloScore)
            .CountAsync(cancellationToken);

        return rank + 1;
    }

    #endregion
}

/// <summary>
/// GameHistory repository implementasyonu.
/// </summary>
public class GameHistoryRepository : IGameHistoryRepository
{
    private readonly OkeyGameDbContext _context;

    public GameHistoryRepository(OkeyGameDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Sorgular

    public async Task<GameHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GameHistories
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<GameHistory>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await _context.GameHistories
            .Where(g => g.RoomId == roomId)
            .OrderByDescending(g => g.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameHistory>> GetByUserIdAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        // PlayerResultsJson içinde UserId ara
        var userIdString = userId.ToString();
        return await _context.GameHistories
            .Where(g => g.WinnerId == userId || g.PlayerResultsJson.Contains(userIdString))
            .OrderByDescending(g => g.StartedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameHistory>> GetRecentByUserIdAsync(
        Guid userId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        return await GetByUserIdAsync(userId, 0, count, cancellationToken);
    }

    #endregion

    #region Komutlar

    public async Task<GameHistory> AddAsync(GameHistory gameHistory, CancellationToken cancellationToken = default)
    {
        await _context.GameHistories.AddAsync(gameHistory, cancellationToken);
        return gameHistory;
    }

    public Task UpdateAsync(GameHistory gameHistory, CancellationToken cancellationToken = default)
    {
        _context.GameHistories.Update(gameHistory);
        return Task.CompletedTask;
    }

    #endregion

    #region İstatistikler

    public async Task<int> GetTotalGamesCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.GameHistories
            .CountAsync(g => g.Status == GameHistoryStatus.Completed, cancellationToken);
    }

    public async Task<int> GetTodayGamesCountAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.GameHistories
            .CountAsync(g => g.Status == GameHistoryStatus.Completed && g.StartedAt >= today, cancellationToken);
    }

    #endregion
}

/// <summary>
/// ChipTransaction repository implementasyonu.
/// </summary>
public class ChipTransactionRepository : IChipTransactionRepository
{
    private readonly OkeyGameDbContext _context;

    public ChipTransactionRepository(OkeyGameDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Sorgular

    public async Task<ChipTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ChipTransactions
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<ChipTransaction?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken cancellationToken = default)
    {
        return await _context.ChipTransactions
            .FirstOrDefaultAsync(t => t.ReferenceNumber == referenceNumber, cancellationToken);
    }

    public async Task<ChipTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.ChipTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<ChipTransaction>> GetByUserIdAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChipTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChipTransaction>> GetByGameHistoryIdAsync(
        Guid gameHistoryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChipTransactions
            .Where(t => t.GameHistoryId == gameHistoryId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Komutlar

    public async Task<ChipTransaction> AddAsync(ChipTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.ChipTransactions.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task AddRangeAsync(IEnumerable<ChipTransaction> transactions, CancellationToken cancellationToken = default)
    {
        await _context.ChipTransactions.AddRangeAsync(transactions, cancellationToken);
    }

    #endregion
}
