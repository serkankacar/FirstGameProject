using OkeyGame.Domain.Services;
using Xunit;

namespace OkeyGame.Tests;

/// <summary>
/// FisherYatesShuffle sınıfı için birim testleri.
/// </summary>
public class FisherYatesShuffleTests
{
    [Fact]
    public void Shuffle_ShouldNotChangeListSize()
    {
        // Arrange
        var list = Enumerable.Range(1, 100).ToList();
        int originalSize = list.Count;

        // Act
        FisherYatesShuffle.Shuffle(list);

        // Assert
        Assert.Equal(originalSize, list.Count);
    }

    [Fact]
    public void Shuffle_ShouldContainSameElements()
    {
        // Arrange
        var original = Enumerable.Range(1, 50).ToList();
        var list = original.ToList();

        // Act
        FisherYatesShuffle.Shuffle(list);

        // Assert
        Assert.Equal(original.OrderBy(x => x), list.OrderBy(x => x));
    }

    [Fact]
    public void Shuffle_ShouldChangeOrder()
    {
        // Arrange
        var list = Enumerable.Range(1, 100).ToList();
        var original = list.ToList();

        // Act
        FisherYatesShuffle.Shuffle(list);

        // Assert - En az bir eleman farklı pozisyonda olmalı
        // (Teorik olarak aynı kalabilir ama 100 elemanlı listede olasılık neredeyse 0)
        Assert.NotEqual(original, list);
    }

    [Fact]
    public void Shuffle_EmptyList_ShouldNotThrow()
    {
        // Arrange
        var list = new List<int>();

        // Act & Assert
        var exception = Record.Exception(() => FisherYatesShuffle.Shuffle(list));
        Assert.Null(exception);
    }

    [Fact]
    public void Shuffle_SingleElement_ShouldNotThrow()
    {
        // Arrange
        var list = new List<int> { 42 };

        // Act
        FisherYatesShuffle.Shuffle(list);

        // Assert
        Assert.Single(list);
        Assert.Equal(42, list[0]);
    }

    [Fact]
    public void ShuffleToNew_ShouldNotModifyOriginal()
    {
        // Arrange
        var original = Enumerable.Range(1, 50).ToList();
        var originalCopy = original.ToList();

        // Act
        var shuffled = FisherYatesShuffle.ShuffleToNew(original);

        // Assert
        Assert.Equal(originalCopy, original); // Orijinal değişmemeli
        Assert.NotEqual(original, shuffled); // Karıştırılmış farklı olmalı
    }

    [Fact]
    public void Shuffle_NullList_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            FisherYatesShuffle.Shuffle<int>(null!));
    }

    [Fact]
    public void ShuffleToNew_NullSource_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            FisherYatesShuffle.ShuffleToNew<int>(null!));
    }

    [Fact]
    public void TestShuffleQuality_ShouldReturnUniformDistribution()
    {
        // Arrange & Act
        var result = FisherYatesShuffle.TestShuffleQuality(
            iterations: 10000, 
            listSize: 10);

        // Assert
        Assert.True(result.IsUniform, 
            $"Dağılım uniform değil! Chi-Square: {result.ChiSquareValue}");
    }

    [Fact]
    public void Shuffle_MultipleTimes_ShouldProduceDifferentResults()
    {
        // Arrange
        var original = Enumerable.Range(1, 52).ToList();
        var results = new List<List<int>>();

        // Act - 10 kez karıştır
        for (int i = 0; i < 10; i++)
        {
            var shuffled = FisherYatesShuffle.ShuffleToNew(original);
            results.Add(shuffled);
        }

        // Assert - En az bazıları farklı olmalı
        var uniqueResults = results
            .Select(r => string.Join(",", r))
            .Distinct()
            .Count();

        Assert.True(uniqueResults > 1, "Karıştırma her seferinde aynı sonucu verdi!");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TestShuffleQuality_InvalidIterations_ShouldThrow(int invalidIterations)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            FisherYatesShuffle.TestShuffleQuality(iterations: invalidIterations));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TestShuffleQuality_InvalidListSize_ShouldThrow(int invalidSize)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            FisherYatesShuffle.TestShuffleQuality(listSize: invalidSize));
    }
}
