using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OkeyGame.Application.Interfaces;
using OkeyGame.Infrastructure.Persistence;
using OkeyGame.Infrastructure.Repositories;

namespace OkeyGame.Infrastructure;

/// <summary>
/// Unit of Work implementasyonu.
/// Transaction yönetimi ve repository'lere erişim sağlar.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly OkeyGameDbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    private IUserRepository? _users;
    private IGameHistoryRepository? _gameHistories;
    private IChipTransactionRepository? _chipTransactions;

    public UnitOfWork(OkeyGameDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Repositories (Lazy Loading)

    public IUserRepository Users => _users ??= new UserRepository(_context);

    public IGameHistoryRepository GameHistories => _gameHistories ??= new GameHistoryRepository(_context);

    public IChipTransactionRepository ChipTransactions => _chipTransactions ??= new ChipTransactionRepository(_context);

    #endregion

    #region Transaction Yönetimi

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("Zaten aktif bir transaction var.");
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("Commit edilecek transaction yok.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("Rollback edilecek transaction yok.");
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
        _disposed = true;
    }

    #endregion
}
