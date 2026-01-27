using Microsoft.Extensions.Logging;
using OkeyGame.Application.Interfaces;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.ValueObjects;

namespace OkeyGame.Infrastructure.Services;

/// <summary>
/// Chip işlemleri servisi.
/// Atomic transaction'lar ile veri tutarlılığı sağlar.
/// 
/// TASARIM PRENSİPLERİ:
/// 1. Tüm chip işlemleri single transaction içinde yapılır
/// 2. Optimistic concurrency ile race condition önlenir
/// 3. Idempotency key ile çift işlem engellenir
/// 4. Her işlem audit trail olarak kaydedilir
/// </summary>
public class ChipTransactionService : IChipTransactionService
{
    #region Sabitler

    /// <summary>Platform komisyon oranı (%).</summary>
    public const decimal RakePercentage = 5m;

    /// <summary>Maksimum rake miktarı.</summary>
    public const long MaxRakeAmount = 10_000;

    #endregion

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChipTransactionService> _logger;

    public ChipTransactionService(
        IUnitOfWork unitOfWork,
        ILogger<ChipTransactionService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Oyun Settlement

    /// <inheritdoc />
    public async Task<GameSettlementResult> SettleGameAsync(
        GameResult gameResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameResult);

        var idempotencyKey = $"game-settle-{gameResult.GameHistoryId}";

        // Çift işlem kontrolü
        var existingTransaction = await _unitOfWork.ChipTransactions
            .GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (existingTransaction != null)
        {
            _logger.LogWarning(
                "Duplicate settlement attempt for game {GameId}",
                gameResult.GameHistoryId);

            return GameSettlementResult.Failure("Bu oyun zaten settle edilmiş.");
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var transactionReferences = new List<string>();

            // Tüm oyuncuları getir
            var playerIds = gameResult.PlayerResults.Select(p => p.UserId).ToList();
            var users = await _unitOfWork.Users.GetByIdsAsync(playerIds, cancellationToken);

            if (users.Count != playerIds.Count)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return GameSettlementResult.Failure("Bazı oyuncular bulunamadı.");
            }

            var userDict = users.ToDictionary(u => u.Id);

            // Rake hesapla
            long rakeAmount = CalculateRake(gameResult.TotalPot);
            long winnerPayout = gameResult.TotalPot - rakeAmount;

            // Her oyuncu için işlem yap
            foreach (var playerResult in gameResult.PlayerResults)
            {
                var user = userDict[playerResult.UserId];

                if (playerResult.IsWinner)
                {
                    // Kazanan: net kazanç = payout - kendi stake'i
                    long netWin = winnerPayout - gameResult.TableStake;
                    
                    var transaction = ChipTransaction.Create(
                        userId: user.Id,
                        type: ChipTransactionType.GameWin,
                        amount: netWin,
                        balanceBefore: user.Chips,
                        description: $"Oyun kazancı - {gameResult.WinType}",
                        gameHistoryId: gameResult.GameHistoryId,
                        idempotencyKey: $"{idempotencyKey}-{user.Id}");

                    user.AddChips(netWin);
                    user.RecordGameResult(isWin: true);
                    user.ApplyEloChange(playerResult.EloChange);

                    await _unitOfWork.ChipTransactions.AddAsync(transaction, cancellationToken);
                    transactionReferences.Add(transaction.ReferenceNumber);
                }
                else
                {
                    // Kaybeden: stake zaten alınmış, sadece istatistik güncelle
                    user.RecordGameResult(isWin: false);
                    user.ApplyEloChange(playerResult.EloChange);

                    // Kayıp işlem logu (bilgi amaçlı, miktar 0)
                    var transaction = ChipTransaction.Create(
                        userId: user.Id,
                        type: ChipTransactionType.GameLoss,
                        amount: 0, // Stake zaten alınmıştı
                        balanceBefore: user.Chips,
                        description: "Oyun kaybı",
                        gameHistoryId: gameResult.GameHistoryId,
                        idempotencyKey: $"{idempotencyKey}-{user.Id}");

                    await _unitOfWork.ChipTransactions.AddAsync(transaction, cancellationToken);
                    transactionReferences.Add(transaction.ReferenceNumber);
                }

                await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Game {GameId} settled successfully. Winner: {WinnerId}, Payout: {Payout}, Rake: {Rake}",
                gameResult.GameHistoryId, gameResult.WinnerId, winnerPayout, rakeAmount);

            return GameSettlementResult.Ok(
                gameResult.TotalPot,
                rakeAmount,
                winnerPayout,
                transactionReferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Game settlement failed for {GameId}", gameResult.GameHistoryId);

            try
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback failed for {GameId}", gameResult.GameHistoryId);
            }

            return GameSettlementResult.Failure($"Settlement başarısız: {ex.Message}");
        }
    }

    #endregion

    #region Stake İşlemleri

    /// <inheritdoc />
    public async Task<bool> CollectStakesAsync(
        Guid gameHistoryId,
        IEnumerable<Guid> playerIds,
        long stakeAmount,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"game-stake-{gameHistoryId}";

        // Çift işlem kontrolü
        var existingTransaction = await _unitOfWork.ChipTransactions
            .GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (existingTransaction != null)
        {
            _logger.LogWarning("Duplicate stake collection for game {GameId}", gameHistoryId);
            return true; // Zaten alınmış
        }

        var playerIdList = playerIds.ToList();

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var users = await _unitOfWork.Users.GetByIdsAsync(playerIdList, cancellationToken);

            if (users.Count != playerIdList.Count)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return false;
            }

            // Tüm oyuncuların bakiyesini kontrol et
            foreach (var user in users)
            {
                if (!user.HasSufficientChips(stakeAmount))
                {
                    _logger.LogWarning(
                        "Insufficient balance for user {UserId}. Required: {Required}, Available: {Available}",
                        user.Id, stakeAmount, user.Chips);

                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return false;
                }
            }

            // Stake'leri al
            foreach (var user in users)
            {
                var transaction = ChipTransaction.Create(
                    userId: user.Id,
                    type: ChipTransactionType.GameStake,
                    amount: -stakeAmount,
                    balanceBefore: user.Chips,
                    description: $"Masa bahsi - Oyun {gameHistoryId}",
                    gameHistoryId: gameHistoryId,
                    idempotencyKey: $"{idempotencyKey}-{user.Id}");

                user.DeductChips(stakeAmount);
                await _unitOfWork.ChipTransactions.AddAsync(transaction, cancellationToken);
                await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Stakes collected for game {GameId}. Amount per player: {Amount}, Players: {Count}",
                gameHistoryId, stakeAmount, users.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stake collection failed for game {GameId}", gameHistoryId);

            try
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            }
            catch { }

            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RefundStakesAsync(
        Guid gameHistoryId,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"game-refund-{gameHistoryId}";

        // Çift işlem kontrolü
        var existingRefund = await _unitOfWork.ChipTransactions
            .GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (existingRefund != null)
        {
            return true; // Zaten iade edilmiş
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Bu oyun için stake işlemlerini bul
            var stakeTransactions = await _unitOfWork.ChipTransactions
                .GetByGameHistoryIdAsync(gameHistoryId, cancellationToken);

            var stakesToRefund = stakeTransactions
                .Where(t => t.Type == ChipTransactionType.GameStake)
                .ToList();

            if (stakesToRefund.Count == 0)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return true; // İade edilecek bir şey yok
            }

            var userIds = stakesToRefund.Select(t => t.UserId).Distinct().ToList();
            var users = await _unitOfWork.Users.GetByIdsAsync(userIds, cancellationToken);
            var userDict = users.ToDictionary(u => u.Id);

            foreach (var stake in stakesToRefund)
            {
                if (!userDict.TryGetValue(stake.UserId, out var user))
                    continue;

                long refundAmount = Math.Abs(stake.Amount);

                var refundTransaction = ChipTransaction.Create(
                    userId: user.Id,
                    type: ChipTransactionType.GameStake, // Negatif stake = iade
                    amount: refundAmount,
                    balanceBefore: user.Chips,
                    description: $"İptal edilen oyun iadesi - Oyun {gameHistoryId}",
                    gameHistoryId: gameHistoryId,
                    idempotencyKey: $"{idempotencyKey}-{user.Id}");

                user.AddChips(refundAmount);
                // TotalChipsWon'u geri al (AddChips artırıyor ama bu kazanç değil)
                // Bu edge case için ayrı bir metod gerekebilir

                await _unitOfWork.ChipTransactions.AddAsync(refundTransaction, cancellationToken);
                await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Stakes refunded for cancelled game {GameId}. Refunded {Count} players",
                gameHistoryId, stakesToRefund.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stake refund failed for game {GameId}", gameHistoryId);

            try
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            }
            catch { }

            return false;
        }
    }

    #endregion

    #region Bonus İşlemleri

    /// <inheritdoc />
    public async Task<ChipTransaction?> AddBonusAsync(
        Guid userId,
        long amount,
        ChipTransactionType bonusType,
        string description,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Bonus miktarı pozitif olmalıdır.", nameof(amount));
        }

        // Geçerli bonus tipleri kontrolü
        if (bonusType != ChipTransactionType.DailyBonus &&
            bonusType != ChipTransactionType.LevelUpBonus &&
            bonusType != ChipTransactionType.ReferralBonus &&
            bonusType != ChipTransactionType.GiftReceived &&
            bonusType != ChipTransactionType.AdminAdjustment)
        {
            throw new ArgumentException("Geçersiz bonus tipi.", nameof(bonusType));
        }

        // Idempotency kontrolü
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await _unitOfWork.ChipTransactions
                .GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

            if (existing != null)
            {
                _logger.LogWarning("Duplicate bonus attempt: {IdempotencyKey}", idempotencyKey);
                return existing;
            }
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return null;
            }

            var transaction = ChipTransaction.Create(
                userId: userId,
                type: bonusType,
                amount: amount,
                balanceBefore: user.Chips,
                description: description,
                idempotencyKey: idempotencyKey);

            user.AddChips(amount);

            await _unitOfWork.ChipTransactions.AddAsync(transaction, cancellationToken);
            await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Bonus added to user {UserId}. Type: {Type}, Amount: {Amount}",
                userId, bonusType, amount);

            return transaction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bonus addition failed for user {UserId}", userId);

            try
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            }
            catch { }

            return null;
        }
    }

    #endregion

    #region Bakiye Sorguları

    /// <inheritdoc />
    public async Task<long> GetBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        return user?.Chips ?? 0;
    }

    /// <inheritdoc />
    public async Task<bool> HasSufficientBalanceAsync(Guid userId, long amount, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        return user?.HasSufficientChips(amount) ?? false;
    }

    #endregion

    #region Yardımcı Metodlar

    /// <summary>
    /// Rake (platform komisyonu) hesaplar.
    /// </summary>
    private static long CalculateRake(long totalPot)
    {
        long rake = (long)(totalPot * RakePercentage / 100m);
        return Math.Min(rake, MaxRakeAmount);
    }

    #endregion
}
