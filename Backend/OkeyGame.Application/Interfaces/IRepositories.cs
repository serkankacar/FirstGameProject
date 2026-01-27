using OkeyGame.Domain.Entities;

namespace OkeyGame.Application.Interfaces;

/// <summary>
/// User repository interface.
/// Clean Architecture: Application -> Domain dependency.
/// </summary>
public interface IUserRepository
{
    #region Sorgular

    /// <summary>
    /// ID ile kullanıcı getirir.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcı adı ile kullanıcı getirir.
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Birden fazla kullanıcıyı ID'leri ile getirir.
    /// </summary>
    Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcı adının benzersiz olup olmadığını kontrol eder.
    /// </summary>
    Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının var olup olmadığını kontrol eder.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion

    #region Komutlar

    /// <summary>
    /// Yeni kullanıcı ekler.
    /// </summary>
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcıyı günceller.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Birden fazla kullanıcıyı günceller.
    /// </summary>
    Task UpdateRangeAsync(IEnumerable<User> users, CancellationToken cancellationToken = default);

    #endregion

    #region Leaderboard (Fallback)

    /// <summary>
    /// ELO sıralamasına göre kullanıcıları getirir (Redis yoksa fallback).
    /// </summary>
    Task<IReadOnlyList<User>> GetTopByEloAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının ELO sıralamasını getirir (Redis yoksa fallback).
    /// </summary>
    Task<int> GetEloRankAsync(Guid userId, CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// GameHistory repository interface.
/// </summary>
public interface IGameHistoryRepository
{
    #region Sorgular

    /// <summary>
    /// ID ile oyun geçmişi getirir.
    /// </summary>
    Task<GameHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Oda ID'si ile oyun geçmişlerini getirir.
    /// </summary>
    Task<IReadOnlyList<GameHistory>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının oyun geçmişini getirir (sayfalı).
    /// </summary>
    Task<IReadOnlyList<GameHistory>> GetByUserIdAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının son N oyununu getirir.
    /// </summary>
    Task<IReadOnlyList<GameHistory>> GetRecentByUserIdAsync(
        Guid userId,
        int count = 10,
        CancellationToken cancellationToken = default);

    #endregion

    #region Komutlar

    /// <summary>
    /// Yeni oyun geçmişi ekler.
    /// </summary>
    Task<GameHistory> AddAsync(GameHistory gameHistory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Oyun geçmişini günceller.
    /// </summary>
    Task UpdateAsync(GameHistory gameHistory, CancellationToken cancellationToken = default);

    #endregion

    #region İstatistikler

    /// <summary>
    /// Toplam oyun sayısını getirir.
    /// </summary>
    Task<int> GetTotalGamesCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bugün oynanan oyun sayısını getirir.
    /// </summary>
    Task<int> GetTodayGamesCountAsync(CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// ChipTransaction repository interface.
/// </summary>
public interface IChipTransactionRepository
{
    #region Sorgular

    /// <summary>
    /// ID ile işlem getirir.
    /// </summary>
    Task<ChipTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Referans numarası ile işlem getirir.
    /// </summary>
    Task<ChipTransaction?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotency key ile işlem getirir (çift işlem kontrolü).
    /// </summary>
    Task<ChipTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının işlem geçmişini getirir (sayfalı).
    /// </summary>
    Task<IReadOnlyList<ChipTransaction>> GetByUserIdAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Oyun ID'si ile işlemleri getirir.
    /// </summary>
    Task<IReadOnlyList<ChipTransaction>> GetByGameHistoryIdAsync(
        Guid gameHistoryId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Komutlar

    /// <summary>
    /// Yeni işlem ekler.
    /// </summary>
    Task<ChipTransaction> AddAsync(ChipTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Birden fazla işlem ekler.
    /// </summary>
    Task AddRangeAsync(IEnumerable<ChipTransaction> transactions, CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Unit of Work pattern interface.
/// Transaction yönetimi için.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// User repository.
    /// </summary>
    IUserRepository Users { get; }

    /// <summary>
    /// GameHistory repository.
    /// </summary>
    IGameHistoryRepository GameHistories { get; }

    /// <summary>
    /// ChipTransaction repository.
    /// </summary>
    IChipTransactionRepository ChipTransactions { get; }

    /// <summary>
    /// Tüm değişiklikleri veritabanına kaydeder.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transaction başlatır.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transaction'ı commit eder.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transaction'ı rollback eder.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
