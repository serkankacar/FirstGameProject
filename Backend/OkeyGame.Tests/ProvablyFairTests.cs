using OkeyGame.Application.DTOs;
using OkeyGame.Application.Services;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;
using OkeyGame.Domain.ValueObjects;
using Xunit;

namespace OkeyGame.Tests;

/// <summary>
/// Provably Fair sistemi için birim testleri.
/// Bu testler, sistemin güvenilirliğini ve doğruluğunu kanıtlar.
/// </summary>
public class ProvablyFairTests
{
    #region Commitment Testleri

    [Fact]
    public void CreateCommitment_ShouldGenerateValidHash()
    {
        // Arrange
        var tiles = CreateSampleTiles(10);

        // Act
        var commitment = ProvablyFairCommitment.Create(
            tiles,
            nonce: 1,
            tile => new { tile.Id, Color = tile.Color.ToString(), tile.Value });

        // Assert
        Assert.NotNull(commitment);
        Assert.NotNull(commitment.CommitmentHash);
        Assert.Equal(64, commitment.CommitmentHash.Length); // SHA256 = 64 hex chars
        Assert.False(commitment.IsRevealed);
    }

    [Fact]
    public void CreateCommitment_SameInput_ShouldProduceSameHash()
    {
        // Arrange
        var serverSeed = Guid.NewGuid();
        var initialState = "[{\"Id\":1,\"Color\":\"Red\",\"Value\":5}]";
        long nonce = 42;

        // Act
        var commitment1 = ProvablyFairCommitment.CreateFromRaw(serverSeed, initialState, nonce);
        var commitment2 = ProvablyFairCommitment.CreateFromRaw(serverSeed, initialState, nonce);

        // Assert
        Assert.Equal(commitment1.CommitmentHash, commitment2.CommitmentHash);
    }

    [Fact]
    public void CreateCommitment_DifferentServerSeed_ShouldProduceDifferentHash()
    {
        // Arrange
        var initialState = "[{\"Id\":1,\"Color\":\"Red\",\"Value\":5}]";
        long nonce = 1;

        // Act
        var commitment1 = ProvablyFairCommitment.CreateFromRaw(Guid.NewGuid(), initialState, nonce);
        var commitment2 = ProvablyFairCommitment.CreateFromRaw(Guid.NewGuid(), initialState, nonce);

        // Assert
        Assert.NotEqual(commitment1.CommitmentHash, commitment2.CommitmentHash);
    }

    [Fact]
    public void CreateCommitment_DifferentNonce_ShouldProduceDifferentHash()
    {
        // Arrange
        var serverSeed = Guid.NewGuid();
        var initialState = "[{\"Id\":1,\"Color\":\"Red\",\"Value\":5}]";

        // Act
        var commitment1 = ProvablyFairCommitment.CreateFromRaw(serverSeed, initialState, nonce: 1);
        var commitment2 = ProvablyFairCommitment.CreateFromRaw(serverSeed, initialState, nonce: 2);

        // Assert
        Assert.NotEqual(commitment1.CommitmentHash, commitment2.CommitmentHash);
    }

    [Fact]
    public void CreateCommitment_WithClientSeed_ShouldProduceDifferentHash()
    {
        // Arrange
        var serverSeed = Guid.NewGuid();
        var initialState = "[{\"Id\":1,\"Color\":\"Red\",\"Value\":5}]";
        long nonce = 1;

        // Act
        var commitmentWithout = ProvablyFairCommitment.CreateFromRaw(serverSeed, initialState, nonce);
        var commitmentWith = ProvablyFairCommitment.CreateFromRaw(serverSeed, initialState, nonce, "client123");

        // Assert
        Assert.NotEqual(commitmentWithout.CommitmentHash, commitmentWith.CommitmentHash);
    }

    #endregion

    #region Reveal Testleri

    [Fact]
    public void Reveal_ShouldExposeData()
    {
        // Arrange
        var commitment = ProvablyFairCommitment.CreateFromRaw(
            Guid.NewGuid(),
            "[{\"Id\":1}]",
            nonce: 1);

        // Act
        commitment.Reveal();

        // Assert
        Assert.True(commitment.IsRevealed);
        Assert.NotNull(commitment.RevealedAt);
        
        var revealData = commitment.GetRevealData();
        Assert.NotNull(revealData.ServerSeed);
        Assert.NotNull(revealData.InitialState);
    }

    [Fact]
    public void Reveal_CalledTwice_ShouldThrow()
    {
        // Arrange
        var commitment = ProvablyFairCommitment.CreateFromRaw(
            Guid.NewGuid(),
            "[{\"Id\":1}]",
            nonce: 1);

        // Act
        commitment.Reveal();

        // Assert
        Assert.Throws<InvalidOperationException>(() => commitment.Reveal());
    }

    [Fact]
    public void GetRevealData_BeforeReveal_ShouldThrow()
    {
        // Arrange
        var commitment = ProvablyFairCommitment.CreateFromRaw(
            Guid.NewGuid(),
            "[{\"Id\":1}]",
            nonce: 1);

        // Assert
        Assert.Throws<InvalidOperationException>(() => commitment.GetRevealData());
    }

    #endregion

    #region Verification Testleri

    [Fact]
    public void Verify_ValidData_ShouldReturnTrue()
    {
        // Arrange
        var serverSeed = Guid.NewGuid();
        var initialState = "[{\"Id\":1,\"Color\":\"Red\",\"Value\":5}]";
        long nonce = 1;

        var commitment = ProvablyFairCommitment.CreateFromRaw(serverSeed, initialState, nonce);
        commitment.Reveal();
        var revealData = commitment.GetRevealData();

        // Act
        var result = ProvablyFairVerifier.Verify(revealData);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(commitment.CommitmentHash, result.ComputedHash);
    }

    [Fact]
    public void Verify_TamperedServerSeed_ShouldReturnFalse()
    {
        // Arrange
        var commitment = ProvablyFairCommitment.CreateFromRaw(
            Guid.NewGuid(),
            "[{\"Id\":1}]",
            nonce: 1);

        commitment.Reveal();
        var revealData = commitment.GetRevealData();

        // Act - ServerSeed'i değiştir (hile simülasyonu)
        var result = ProvablyFairVerifier.Verify(
            Guid.NewGuid().ToString(), // Farklı seed
            revealData.InitialState,
            revealData.Nonce,
            revealData.CommitmentHash);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_TamperedInitialState_ShouldReturnFalse()
    {
        // Arrange
        var commitment = ProvablyFairCommitment.CreateFromRaw(
            Guid.NewGuid(),
            "[{\"Id\":1,\"Color\":\"Red\",\"Value\":5}]",
            nonce: 1);

        commitment.Reveal();
        var revealData = commitment.GetRevealData();

        // Act - InitialState'i değiştir (hile simülasyonu)
        var result = ProvablyFairVerifier.Verify(
            revealData.ServerSeed,
            "[{\"Id\":2,\"Color\":\"Blue\",\"Value\":7}]", // Farklı state
            revealData.Nonce,
            revealData.CommitmentHash);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_TamperedNonce_ShouldReturnFalse()
    {
        // Arrange
        var commitment = ProvablyFairCommitment.CreateFromRaw(
            Guid.NewGuid(),
            "[{\"Id\":1}]",
            nonce: 1);

        commitment.Reveal();
        var revealData = commitment.GetRevealData();

        // Act - Nonce'u değiştir
        var result = ProvablyFairVerifier.Verify(
            revealData.ServerSeed,
            revealData.InitialState,
            999, // Farklı nonce
            revealData.CommitmentHash);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_WithClientSeed_ShouldWork()
    {
        // Arrange
        var serverSeed = Guid.NewGuid();
        var initialState = "[{\"Id\":1}]";
        long nonce = 1;
        var clientSeed = "user-provided-seed-12345";

        var commitment = ProvablyFairCommitment.CreateFromRaw(
            serverSeed, initialState, nonce, clientSeed);

        commitment.Reveal();
        var revealData = commitment.GetRevealData();

        // Act
        var result = ProvablyFairVerifier.Verify(revealData);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Verify_MissingClientSeed_ShouldReturnFalse()
    {
        // Arrange - Client seed ile commitment oluştur
        var commitment = ProvablyFairCommitment.CreateFromRaw(
            Guid.NewGuid(),
            "[{\"Id\":1}]",
            nonce: 1,
            clientSeed: "my-client-seed");

        commitment.Reveal();
        var revealData = commitment.GetRevealData();

        // Act - Client seed OLMADAN doğrula
        var result = ProvablyFairVerifier.Verify(
            revealData.ServerSeed,
            revealData.InitialState,
            revealData.Nonce,
            revealData.CommitmentHash,
            clientSeed: null); // Client seed yok

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Service Testleri

    [Fact]
    public void ProvablyFairService_FullFlow_ShouldWork()
    {
        // Arrange
        var service = new ProvablyFairService();
        var roomId = Guid.NewGuid();
        var tiles = CreateSampleTiles(10);

        // Act 1: Commitment oluştur
        var commitment = service.CreateCommitment(roomId, tiles);
        var commitmentDto = service.GetCommitmentDto(roomId);

        Assert.NotNull(commitment);
        Assert.NotNull(commitmentDto);
        Assert.Equal(commitment.CommitmentHash, commitmentDto.CommitmentHash);

        // Act 2: Client seed ayarla
        var updatedCommitment = service.SetClientSeed(roomId, "client-123");
        Assert.NotEqual(commitment.CommitmentHash, updatedCommitment!.CommitmentHash);

        // Act 3: Reveal et
        var revealDto = service.RevealCommitment(roomId);
        Assert.NotNull(revealDto);
        Assert.Equal(updatedCommitment.CommitmentHash, revealDto.CommitmentHash);

        // Act 4: Doğrula
        var verifyResult = service.VerifyFromReveal(revealDto);
        Assert.True(verifyResult.IsValid);
    }

    [Fact]
    public void ProvablyFairService_MultipleRooms_ShouldBeIndependent()
    {
        // Arrange
        var service = new ProvablyFairService();
        var room1 = Guid.NewGuid();
        var room2 = Guid.NewGuid();

        // Act
        service.CreateCommitment(room1, CreateSampleTiles(5));
        service.CreateCommitment(room2, CreateSampleTiles(5));

        var dto1 = service.GetCommitmentDto(room1);
        var dto2 = service.GetCommitmentDto(room2);

        // Assert
        Assert.NotNull(dto1);
        Assert.NotNull(dto2);
        Assert.NotEqual(dto1.CommitmentHash, dto2.CommitmentHash);
        Assert.NotEqual(dto1.Nonce, dto2.Nonce);
    }

    #endregion

    #region Hash Consistency Testleri

    [Fact]
    public void ComputeHash_ShouldBeConsistent()
    {
        // Arrange
        var serverSeed = "550e8400-e29b-41d4-a716-446655440000";
        var initialState = "[{\"Id\":1,\"Color\":\"Red\",\"Value\":5}]";
        long nonce = 42;

        // Act - Aynı değerlerle birden fazla hesaplama
        var hash1 = ProvablyFairVerifier.ComputeHash(serverSeed, initialState, nonce);
        var hash2 = ProvablyFairVerifier.ComputeHash(serverSeed, initialState, nonce);
        var hash3 = ProvablyFairVerifier.ComputeHash(serverSeed, initialState, nonce);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public void ComputeHash_ShouldBe64Characters()
    {
        // Arrange & Act
        var hash = ProvablyFairVerifier.ComputeHash(
            Guid.NewGuid().ToString(),
            "test",
            1);

        // Assert
        Assert.Equal(64, hash.Length);
        Assert.True(hash.All(c => char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void ComputeHash_ShouldBeLowercase()
    {
        // Arrange & Act
        var hash = ProvablyFairVerifier.ComputeHash(
            Guid.NewGuid().ToString(),
            "test",
            1);

        // Assert
        Assert.Equal(hash.ToLowerInvariant(), hash);
    }

    #endregion

    #region Yardımcı Metotlar

    private static List<Tile> CreateSampleTiles(int count)
    {
        var tiles = new List<Tile>();
        for (int i = 0; i < count; i++)
        {
            var color = (TileColor)(i % 4);
            var value = (i % 13) + 1;
            tiles.Add(Tile.Create(i, color, value));
        }
        return tiles;
    }

    #endregion
}
