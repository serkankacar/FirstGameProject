using OkeyGame.Domain.Services;
using Xunit;

namespace OkeyGame.Tests;

/// <summary>
/// CryptoRandomGenerator sınıfı için birim testleri.
/// </summary>
public class CryptoRandomGeneratorTests
{
    [Fact]
    public void GetRandomBytes_ValidLength_ShouldReturnCorrectSize()
    {
        // Arrange
        var rng = new CryptoRandomGenerator();
        int length = 32;

        // Act
        var bytes = rng.GetRandomBytes(length);

        // Assert
        Assert.Equal(length, bytes.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GetRandomBytes_InvalidLength_ShouldThrowException(int invalidLength)
    {
        // Arrange
        var rng = new CryptoRandomGenerator();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.GetRandomBytes(invalidLength));
    }

    [Fact]
    public void NextInt_ValidMaxValue_ShouldReturnWithinRange()
    {
        // Arrange
        var rng = new CryptoRandomGenerator();
        int maxValue = 100;
        int iterations = 1000;

        // Act & Assert
        for (int i = 0; i < iterations; i++)
        {
            int result = rng.NextInt(maxValue);
            Assert.InRange(result, 0, maxValue - 1);
        }
    }

    [Fact]
    public void NextInt_MinMax_ShouldReturnWithinRange()
    {
        // Arrange
        var rng = new CryptoRandomGenerator();
        int minValue = 10;
        int maxValue = 50;
        int iterations = 1000;

        // Act & Assert
        for (int i = 0; i < iterations; i++)
        {
            int result = rng.NextInt(minValue, maxValue);
            Assert.InRange(result, minValue, maxValue - 1);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextInt_InvalidMaxValue_ShouldThrowException(int invalidMax)
    {
        // Arrange
        var rng = new CryptoRandomGenerator();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(invalidMax));
    }

    [Fact]
    public void NextDouble_ShouldReturnBetween0And1()
    {
        // Arrange
        var rng = new CryptoRandomGenerator();
        int iterations = 1000;

        // Act & Assert
        for (int i = 0; i < iterations; i++)
        {
            double result = rng.NextDouble();
            Assert.InRange(result, 0.0, 1.0);
        }
    }

    [Fact]
    public void NextBool_ShouldReturnBothValues()
    {
        // Arrange
        var rng = new CryptoRandomGenerator();
        bool hasTrue = false;
        bool hasFalse = false;
        int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            bool result = rng.NextBool();
            if (result) hasTrue = true;
            else hasFalse = true;

            if (hasTrue && hasFalse) break;
        }

        // Assert
        Assert.True(hasTrue, "NextBool hiç true döndürmedi");
        Assert.True(hasFalse, "NextBool hiç false döndürmedi");
    }

    [Fact]
    public void GenerateServerSeed_ShouldReturn64HexCharacters()
    {
        // Arrange
        var rng = new CryptoRandomGenerator();

        // Act
        var seed = rng.GenerateServerSeed();

        // Assert
        Assert.Equal(64, seed.Length); // 32 byte = 64 hex karakter
        Assert.True(seed.All(c => char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void GenerateServerSeed_MultipleCalls_ShouldReturnDifferentSeeds()
    {
        // Arrange
        var rng = new CryptoRandomGenerator();

        // Act
        var seed1 = rng.GenerateServerSeed();
        var seed2 = rng.GenerateServerSeed();

        // Assert
        Assert.NotEqual(seed1, seed2);
    }

    [Fact]
    public void CombineSeeds_ValidInputs_ShouldReturnHash()
    {
        // Arrange
        string serverSeed = "abc123";
        string clientSeed = "def456";
        long nonce = 1;

        // Act
        var result = CryptoRandomGenerator.CombineSeeds(serverSeed, clientSeed, nonce);

        // Assert
        Assert.Equal(64, result.Length); // SHA256 = 32 byte = 64 hex
    }

    [Fact]
    public void CombineSeeds_SameInputs_ShouldReturnSameHash()
    {
        // Arrange
        string serverSeed = "test123";
        string clientSeed = "client456";
        long nonce = 42;

        // Act
        var result1 = CryptoRandomGenerator.CombineSeeds(serverSeed, clientSeed, nonce);
        var result2 = CryptoRandomGenerator.CombineSeeds(serverSeed, clientSeed, nonce);

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void CombineSeeds_DifferentNonce_ShouldReturnDifferentHash()
    {
        // Arrange
        string serverSeed = "test123";
        string clientSeed = "client456";

        // Act
        var result1 = CryptoRandomGenerator.CombineSeeds(serverSeed, clientSeed, 1);
        var result2 = CryptoRandomGenerator.CombineSeeds(serverSeed, clientSeed, 2);

        // Assert
        Assert.NotEqual(result1, result2);
    }

    [Theory]
    [InlineData(null, "client", "Server seed boş olamaz.")]
    [InlineData("", "client", "Server seed boş olamaz.")]
    [InlineData("server", null, "Client seed boş olamaz.")]
    [InlineData("server", "", "Client seed boş olamaz.")]
    public void CombineSeeds_InvalidInputs_ShouldThrowException(
        string? serverSeed, string? clientSeed, string expectedMessage)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            CryptoRandomGenerator.CombineSeeds(serverSeed!, clientSeed!, 1));
        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void Singleton_Instance_ShouldBeSameReference()
    {
        // Act
        var instance1 = CryptoRandomGenerator.Instance;
        var instance2 = CryptoRandomGenerator.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }
}
