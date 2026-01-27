using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;
using Xunit;

namespace OkeyGame.Tests;

/// <summary>
/// Tile sınıfı için birim testleri.
/// </summary>
public class TileTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateTile()
    {
        // Arrange & Act
        var tile = Tile.Create(1, TileColor.Red, 7);

        // Assert
        Assert.Equal(1, tile.Id);
        Assert.Equal(TileColor.Red, tile.Color);
        Assert.Equal(7, tile.Value);
        Assert.False(tile.IsOkey);
        Assert.False(tile.IsFalseJoker);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Create_InvalidValue_ShouldThrowException(int invalidValue)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            Tile.Create(1, TileColor.Blue, invalidValue));
    }

    [Fact]
    public void CreateFalseJoker_ShouldCreateFalseJokerTile()
    {
        // Arrange & Act
        var tile = Tile.CreateFalseJoker(104);

        // Assert
        Assert.Equal(104, tile.Id);
        Assert.True(tile.IsFalseJoker);
        Assert.False(tile.IsOkey);
    }

    [Fact]
    public void AsOkey_ShouldReturnNewTileWithOkeyFlag()
    {
        // Arrange
        var original = Tile.Create(5, TileColor.Yellow, 10);

        // Act
        var okeyTile = original.AsOkey();

        // Assert
        Assert.True(okeyTile.IsOkey);
        Assert.False(original.IsOkey); // Orijinal değişmemeli
        Assert.Equal(original.Id, okeyTile.Id);
        Assert.Equal(original.Color, okeyTile.Color);
        Assert.Equal(original.Value, okeyTile.Value);
    }

    [Fact]
    public void Equals_SameTileId_ShouldBeEqual()
    {
        // Arrange
        var tile1 = Tile.Create(10, TileColor.Black, 5);
        var tile2 = Tile.Create(10, TileColor.Red, 13); // Farklı renk ve değer, aynı ID

        // Act & Assert
        Assert.Equal(tile1, tile2);
        Assert.True(tile1 == tile2);
    }

    [Fact]
    public void Equals_DifferentTileId_ShouldNotBeEqual()
    {
        // Arrange
        var tile1 = Tile.Create(1, TileColor.Black, 5);
        var tile2 = Tile.Create(2, TileColor.Black, 5);

        // Act & Assert
        Assert.NotEqual(tile1, tile2);
        Assert.True(tile1 != tile2);
    }
}

/// <summary>
/// TileFactory sınıfı için birim testleri.
/// </summary>
public class TileFactoryTests
{
    [Fact]
    public void CreateFullSet_ShouldCreate106Tiles()
    {
        // Act
        var tiles = TileFactory.CreateFullSet();

        // Assert
        Assert.Equal(106, tiles.Count);
    }

    [Fact]
    public void CreateFullSet_ShouldHaveUniqueIds()
    {
        // Act
        var tiles = TileFactory.CreateFullSet();

        // Assert
        var uniqueIds = tiles.Select(t => t.Id).Distinct().Count();
        Assert.Equal(106, uniqueIds);
    }

    [Fact]
    public void CreateFullSet_ShouldHave2FalseJokers()
    {
        // Act
        var tiles = TileFactory.CreateFullSet();

        // Assert
        var falseJokers = tiles.Count(t => t.IsFalseJoker);
        Assert.Equal(2, falseJokers);
    }

    [Fact]
    public void CreateFullSet_ShouldHave104NormalTiles()
    {
        // Act
        var tiles = TileFactory.CreateFullSet();

        // Assert
        var normalTiles = tiles.Count(t => !t.IsFalseJoker);
        Assert.Equal(104, normalTiles);
    }

    [Fact]
    public void CreateFullSet_EachColorValuePair_ShouldHave2Tiles()
    {
        // Act
        var tiles = TileFactory.CreateFullSet();

        // Assert
        foreach (TileColor color in Enum.GetValues<TileColor>())
        {
            for (int value = 1; value <= 13; value++)
            {
                var count = tiles.Count(t => 
                    !t.IsFalseJoker && t.Color == color && t.Value == value);
                Assert.Equal(2, count);
            }
        }
    }

    [Fact]
    public void ValidateSet_ValidSet_ShouldReturnTrue()
    {
        // Arrange
        var tiles = TileFactory.CreateFullSet();

        // Act
        var result = TileFactory.ValidateSet(tiles);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void MarkOkeyTiles_Indicator7Red_ShouldMark8RedAsOkey()
    {
        // Arrange
        var tiles = TileFactory.CreateFullSet();
        var indicator = tiles.First(t => t.Color == TileColor.Red && t.Value == 7);

        // Act
        var markedTiles = TileFactory.MarkOkeyTiles(tiles, indicator);

        // Assert
        var okeyTiles = markedTiles.Where(t => t.IsOkey).ToList();
        Assert.Equal(2, okeyTiles.Count); // Aynı renkten 2 taş Okey olmalı
        Assert.All(okeyTiles, t => Assert.Equal(TileColor.Red, t.Color));
        Assert.All(okeyTiles, t => Assert.Equal(8, t.Value));
    }

    [Fact]
    public void MarkOkeyTiles_Indicator13Blue_ShouldMark1BlueAsOkey()
    {
        // Arrange - 13 gösterge ise, Okey 1 olmalı (döngüsel)
        var tiles = TileFactory.CreateFullSet();
        var indicator = tiles.First(t => t.Color == TileColor.Blue && t.Value == 13);

        // Act
        var markedTiles = TileFactory.MarkOkeyTiles(tiles, indicator);

        // Assert
        var okeyTiles = markedTiles.Where(t => t.IsOkey).ToList();
        Assert.Equal(2, okeyTiles.Count);
        Assert.All(okeyTiles, t => Assert.Equal(TileColor.Blue, t.Color));
        Assert.All(okeyTiles, t => Assert.Equal(1, t.Value)); // 13+1 = 1 (döngüsel)
    }
}
