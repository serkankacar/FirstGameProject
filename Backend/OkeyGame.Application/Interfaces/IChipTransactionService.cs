using OkeyGame.Domain.Entities;
using OkeyGame.Domain.ValueObjects;

namespace OkeyGame.Application.Interfaces;

/// <summary>
/// Chip işlemleri servisi interface'i.
/// Atomic transaction'lar ile veri tutarlılığı sağlar.
/// </summary>
public interface IChipTransactionService
{
    /// <summary>
    /// Oyun sonucu işler (atomic transaction).
    /// 1. Kazananın çipini artırır
    /// 2. Kaybedenlerin çipini düşürür (eğer stake varsa)
    /// 3. Platform komisyonunu (rake) hesaplar
    /// 4. Tüm işlemleri ChipTransaction tablosuna kaydeder
    /// </summary>
    /// <param name="gameResult">Oyun sonucu</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>İşlem başarılı mı?</returns>
    Task<GameSettlementResult> SettleGameAsync(
        GameResult gameResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Masa bahsini oyunculardan alır (oyun başlangıcında).
    /// </summary>
    /// <param name="gameHistoryId">Oyun geçmişi ID</param>
    /// <param name="playerIds">Oyuncu ID'leri</param>
    /// <param name="stakeAmount">Bahis miktarı</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task<bool> CollectStakesAsync(
        Guid gameHistoryId,
        IEnumerable<Guid> playerIds,
        long stakeAmount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// İptal edilen oyunda bahisleri iade eder.
    /// </summary>
    Task<bool> RefundStakesAsync(
        Guid gameHistoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bonus çip ekler (günlük bonus, seviye atlama vb.).
    /// </summary>
    Task<ChipTransaction?> AddBonusAsync(
        Guid userId,
        long amount,
        ChipTransactionType bonusType,
        string description,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının çip bakiyesini getirir.
    /// </summary>
    Task<long> GetBalanceAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının yeterli bakiyesi var mı kontrol eder.
    /// </summary>
    Task<bool> HasSufficientBalanceAsync(Guid userId, long amount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Oyun settlement sonucu.
/// </summary>
public record GameSettlementResult
{
    /// <summary>İşlem başarılı mı?</summary>
    public bool Success { get; init; }

    /// <summary>Hata mesajı (başarısızsa).</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Toplam pot.</summary>
    public long TotalPot { get; init; }

    /// <summary>Platform komisyonu.</summary>
    public long RakeAmount { get; init; }

    /// <summary>Kazanana ödenen miktar.</summary>
    public long WinnerPayout { get; init; }

    /// <summary>İşlem referans numaraları.</summary>
    public IReadOnlyList<string> TransactionReferences { get; init; } = Array.Empty<string>();

    public static GameSettlementResult Failure(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };

    public static GameSettlementResult Ok(long totalPot, long rakeAmount, long winnerPayout, IReadOnlyList<string> references)
        => new()
        {
            Success = true,
            TotalPot = totalPot,
            RakeAmount = rakeAmount,
            WinnerPayout = winnerPayout,
            TransactionReferences = references
        };
}
