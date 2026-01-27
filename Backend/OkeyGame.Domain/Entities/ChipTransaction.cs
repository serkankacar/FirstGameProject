namespace OkeyGame.Domain.Entities;

/// <summary>
/// Çip işlem geçmişi entity'si.
/// Her çip hareketini takip eder (audit trail).
/// </summary>
public class ChipTransaction
{
    #region Primary Key

    /// <summary>
    /// İşlem benzersiz kimliği.
    /// </summary>
    public Guid Id { get; private set; }

    #endregion

    #region Referanslar

    /// <summary>
    /// Kullanıcı ID'si.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// İlgili oyun ID'si (oyun işlemleri için).
    /// </summary>
    public Guid? GameHistoryId { get; private set; }

    #endregion

    #region İşlem Detayları

    /// <summary>
    /// İşlem tipi.
    /// </summary>
    public ChipTransactionType Type { get; private set; }

    /// <summary>
    /// İşlem miktarı (pozitif veya negatif).
    /// </summary>
    public long Amount { get; private set; }

    /// <summary>
    /// İşlem öncesi bakiye.
    /// </summary>
    public long BalanceBefore { get; private set; }

    /// <summary>
    /// İşlem sonrası bakiye.
    /// </summary>
    public long BalanceAfter { get; private set; }

    /// <summary>
    /// İşlem açıklaması.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// İşlem zamanı.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    #endregion

    #region Bütünlük

    /// <summary>
    /// İşlem referans numarası (unique).
    /// </summary>
    public string ReferenceNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Idempotency key (çift işlemi önlemek için).
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// EF Core için private constructor.
    /// </summary>
    private ChipTransaction() { }

    /// <summary>
    /// Yeni çip işlemi oluşturur.
    /// </summary>
    public static ChipTransaction Create(
        Guid userId,
        ChipTransactionType type,
        long amount,
        long balanceBefore,
        string description,
        Guid? gameHistoryId = null,
        string? idempotencyKey = null)
    {
        var transaction = new ChipTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceBefore + amount,
            Description = description,
            GameHistoryId = gameHistoryId,
            CreatedAt = DateTime.UtcNow,
            ReferenceNumber = GenerateReferenceNumber(),
            IdempotencyKey = idempotencyKey
        };

        return transaction;
    }

    /// <summary>
    /// Referans numarası oluşturur.
    /// Format: TXN-YYYYMMDD-XXXXXXXX
    /// </summary>
    private static string GenerateReferenceNumber()
    {
        return $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    #endregion
}

/// <summary>
/// Çip işlem tipleri.
/// </summary>
public enum ChipTransactionType
{
    /// <summary>Oyun bahsi (stake).</summary>
    GameStake = 1,

    /// <summary>Oyun kazancı.</summary>
    GameWin = 2,

    /// <summary>Oyun kaybı (bahis iadesi olmadan).</summary>
    GameLoss = 3,

    /// <summary>Günlük bonus.</summary>
    DailyBonus = 10,

    /// <summary>Seviye atlama bonusu.</summary>
    LevelUpBonus = 11,

    /// <summary>Davet bonusu.</summary>
    ReferralBonus = 12,

    /// <summary>Satın alma (IAP).</summary>
    Purchase = 20,

    /// <summary>Hediye gönderme.</summary>
    GiftSent = 30,

    /// <summary>Hediye alma.</summary>
    GiftReceived = 31,

    /// <summary>Admin düzeltmesi.</summary>
    AdminAdjustment = 99
}
