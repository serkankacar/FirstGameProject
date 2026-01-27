using OkeyGame.Domain.Entities;
using Xunit;

namespace OkeyGame.Tests.Persistence;

/// <summary>
/// ChipTransaction entity testleri.
/// </summary>
public class ChipTransactionTests
{
    [Fact]
    public void Create_ValidParameters_CreatesTransaction()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var transaction = ChipTransaction.Create(
            userId: userId,
            type: ChipTransactionType.GameWin,
            amount: 1000,
            balanceBefore: 5000,
            description: "Test win");

        // Assert
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(userId, transaction.UserId);
        Assert.Equal(ChipTransactionType.GameWin, transaction.Type);
        Assert.Equal(1000, transaction.Amount);
        Assert.Equal(5000, transaction.BalanceBefore);
        Assert.Equal(6000, transaction.BalanceAfter);
        Assert.Equal("Test win", transaction.Description);
        Assert.StartsWith("TXN-", transaction.ReferenceNumber);
        Assert.Null(transaction.IdempotencyKey);
    }

    [Fact]
    public void Create_WithGameHistoryId_StoresGameId()
    {
        // Arrange
        var gameHistoryId = Guid.NewGuid();

        // Act
        var transaction = ChipTransaction.Create(
            userId: Guid.NewGuid(),
            type: ChipTransactionType.GameStake,
            amount: -1000,
            balanceBefore: 5000,
            description: "Game stake",
            gameHistoryId: gameHistoryId);

        // Assert
        Assert.Equal(gameHistoryId, transaction.GameHistoryId);
    }

    [Fact]
    public void Create_WithIdempotencyKey_StoresKey()
    {
        // Arrange
        var idempotencyKey = "game-settle-123";

        // Act
        var transaction = ChipTransaction.Create(
            userId: Guid.NewGuid(),
            type: ChipTransactionType.GameWin,
            amount: 1000,
            balanceBefore: 5000,
            description: "Win",
            idempotencyKey: idempotencyKey);

        // Assert
        Assert.Equal(idempotencyKey, transaction.IdempotencyKey);
    }

    [Fact]
    public void Create_NegativeAmount_CalculatesBalanceAfterCorrectly()
    {
        // Act
        var transaction = ChipTransaction.Create(
            userId: Guid.NewGuid(),
            type: ChipTransactionType.GameStake,
            amount: -1000,
            balanceBefore: 5000,
            description: "Stake");

        // Assert
        Assert.Equal(4000, transaction.BalanceAfter);
    }

    [Fact]
    public void Create_GeneratesUniqueReferenceNumbers()
    {
        // Act
        var transaction1 = ChipTransaction.Create(
            Guid.NewGuid(), ChipTransactionType.DailyBonus, 100, 0, "Bonus 1");
        var transaction2 = ChipTransaction.Create(
            Guid.NewGuid(), ChipTransactionType.DailyBonus, 100, 0, "Bonus 2");

        // Assert
        Assert.NotEqual(transaction1.ReferenceNumber, transaction2.ReferenceNumber);
    }

    [Theory]
    [InlineData(ChipTransactionType.GameStake)]
    [InlineData(ChipTransactionType.GameWin)]
    [InlineData(ChipTransactionType.GameLoss)]
    [InlineData(ChipTransactionType.DailyBonus)]
    [InlineData(ChipTransactionType.LevelUpBonus)]
    [InlineData(ChipTransactionType.ReferralBonus)]
    [InlineData(ChipTransactionType.Purchase)]
    [InlineData(ChipTransactionType.GiftSent)]
    [InlineData(ChipTransactionType.GiftReceived)]
    [InlineData(ChipTransactionType.AdminAdjustment)]
    public void Create_AllTransactionTypes_Succeed(ChipTransactionType type)
    {
        // Act
        var transaction = ChipTransaction.Create(
            userId: Guid.NewGuid(),
            type: type,
            amount: 100,
            balanceBefore: 1000,
            description: $"Test {type}");

        // Assert
        Assert.Equal(type, transaction.Type);
    }
}
