using Microsoft.EntityFrameworkCore;
using OkeyGame.Domain.Entities;

namespace OkeyGame.Infrastructure.Persistence;

/// <summary>
/// Okey Oyunu veritabanı context'i.
/// Entity Framework Core ile veritabanı işlemlerini yönetir.
/// </summary>
public class OkeyGameDbContext : DbContext
{
    #region DbSets

    /// <summary>
    /// Kullanıcılar tablosu.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Oyun geçmişi tablosu.
    /// </summary>
    public DbSet<GameHistory> GameHistories => Set<GameHistory>();

    /// <summary>
    /// Çip işlemleri tablosu.
    /// </summary>
    public DbSet<ChipTransaction> ChipTransactions => Set<ChipTransaction>();

    #endregion

    #region Constructor

    public OkeyGameDbContext(DbContextOptions<OkeyGameDbContext> options)
        : base(options)
    {
    }

    #endregion

    #region Configuration

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity konfigürasyonlarını uygula
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new GameHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new ChipTransactionConfiguration());
    }

    #endregion
}
