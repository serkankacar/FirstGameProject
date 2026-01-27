using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OkeyGame.Domain.Entities;

namespace OkeyGame.Infrastructure.Persistence;

/// <summary>
/// User entity konfigürasyonu.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Tablo adı
        builder.ToTable("Users");

        // Primary Key
        builder.HasKey(u => u.Id);

        // Username - unique index
        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(User.MaxUsernameLength);

        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasDatabaseName("IX_Users_Username");

        // DisplayName
        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(User.MaxUsernameLength * 2);

        // AvatarUrl
        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        // Ekonomi alanları
        builder.Property(u => u.Chips)
            .IsRequired()
            .HasDefaultValue(User.DefaultChips);

        builder.Property(u => u.TotalChipsWon)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(u => u.TotalChipsLost)
            .IsRequired()
            .HasDefaultValue(0L);

        // ELO alanları
        builder.Property(u => u.EloScore)
            .IsRequired()
            .HasDefaultValue(User.DefaultEloScore);

        builder.HasIndex(u => u.EloScore)
            .HasDatabaseName("IX_Users_EloScore");

        builder.Property(u => u.HighestEloScore)
            .IsRequired()
            .HasDefaultValue(User.DefaultEloScore);

        // İstatistikler
        builder.Property(u => u.TotalGamesPlayed)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.TotalGamesWon)
            .IsRequired()
            .HasDefaultValue(0);

        // Tarihler
        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.LastLoginAt)
            .IsRequired();

        // Durum
        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Concurrency token (optimistic locking)
        builder.Property(u => u.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Composite index - aktif kullanıcılar için ELO sıralaması
        builder.HasIndex(u => new { u.IsActive, u.EloScore })
            .HasDatabaseName("IX_Users_Active_EloScore")
            .IsDescending(false, true);
    }
}

/// <summary>
/// GameHistory entity konfigürasyonu.
/// </summary>
public class GameHistoryConfiguration : IEntityTypeConfiguration<GameHistory>
{
    public void Configure(EntityTypeBuilder<GameHistory> builder)
    {
        // Tablo adı
        builder.ToTable("GameHistories");

        // Primary Key
        builder.HasKey(g => g.Id);

        // RoomId index
        builder.Property(g => g.RoomId)
            .IsRequired();

        builder.HasIndex(g => g.RoomId)
            .HasDatabaseName("IX_GameHistories_RoomId");

        // Tarihler
        builder.Property(g => g.StartedAt)
            .IsRequired();

        builder.Property(g => g.EndedAt);

        // Index - tarih bazlı sorgular için
        builder.HasIndex(g => g.StartedAt)
            .HasDatabaseName("IX_GameHistories_StartedAt");

        // Status
        builder.Property(g => g.Status)
            .IsRequired()
            .HasConversion<int>();

        // Kazanan bilgileri
        builder.Property(g => g.WinnerId);

        builder.HasIndex(g => g.WinnerId)
            .HasDatabaseName("IX_GameHistories_WinnerId");

        builder.Property(g => g.WinnerUsername)
            .HasMaxLength(50);

        builder.Property(g => g.WinType)
            .HasConversion<int?>();

        builder.Property(g => g.WinScore);

        // Ekonomi
        builder.Property(g => g.TableStake)
            .IsRequired();

        builder.Property(g => g.RakeAmount)
            .IsRequired()
            .HasDefaultValue(0L);

        // Tur bilgisi
        builder.Property(g => g.TotalTurns)
            .IsRequired()
            .HasDefaultValue(0);

        // JSON alanları
        builder.Property(g => g.PlayerResultsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        // Provably Fair
        builder.Property(g => g.GameSeed)
            .HasMaxLength(128);

        builder.Property(g => g.ServerSeedHash)
            .HasMaxLength(128);

        builder.Property(g => g.ClientSeed)
            .HasMaxLength(128);

        // Computed column - DurationSeconds
        builder.Ignore(g => g.DurationSeconds);
        builder.Ignore(g => g.TotalPot);
        builder.Ignore(g => g.WinnerPayout);
    }
}

/// <summary>
/// ChipTransaction entity konfigürasyonu.
/// </summary>
public class ChipTransactionConfiguration : IEntityTypeConfiguration<ChipTransaction>
{
    public void Configure(EntityTypeBuilder<ChipTransaction> builder)
    {
        // Tablo adı
        builder.ToTable("ChipTransactions");

        // Primary Key
        builder.HasKey(t => t.Id);

        // UserId index
        builder.Property(t => t.UserId)
            .IsRequired();

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_ChipTransactions_UserId");

        // GameHistoryId (optional)
        builder.Property(t => t.GameHistoryId);

        builder.HasIndex(t => t.GameHistoryId)
            .HasDatabaseName("IX_ChipTransactions_GameHistoryId");

        // Type
        builder.Property(t => t.Type)
            .IsRequired()
            .HasConversion<int>();

        // Amount
        builder.Property(t => t.Amount)
            .IsRequired();

        // Bakiyeler
        builder.Property(t => t.BalanceBefore)
            .IsRequired();

        builder.Property(t => t.BalanceAfter)
            .IsRequired();

        // Description
        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(500);

        // ReferenceNumber - unique
        builder.Property(t => t.ReferenceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(t => t.ReferenceNumber)
            .IsUnique()
            .HasDatabaseName("IX_ChipTransactions_ReferenceNumber");

        // IdempotencyKey - unique (nulls allowed)
        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(100);

        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("IX_ChipTransactions_IdempotencyKey");

        // CreatedAt
        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.HasIndex(t => t.CreatedAt)
            .HasDatabaseName("IX_ChipTransactions_CreatedAt");

        // Composite index - kullanıcı işlem geçmişi
        builder.HasIndex(t => new { t.UserId, t.CreatedAt })
            .HasDatabaseName("IX_ChipTransactions_UserId_CreatedAt")
            .IsDescending(false, true);
    }
}
