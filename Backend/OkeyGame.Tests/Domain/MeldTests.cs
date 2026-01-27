using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;
using OkeyGame.Domain.ValueObjects;
using Xunit;

namespace OkeyGame.Tests.Domain;

/// <summary>
/// Meld (Per) testleri.
/// </summary>
public class MeldTests
{
    #region Run (Sıralı Per) Testleri

    [Fact]
    public void CreateRun_WithConsecutiveSameColor_ShouldBeValid()
    {
        // Arrange - Kırmızı 5-6-7
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 5),
            Tile.Create(2, TileColor.Red, 6),
            Tile.Create(3, TileColor.Red, 7)
        };

        // Act
        var meld = Meld.CreateRun(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(MeldType.Run, meld.Type);
        Assert.Equal(0, meld.OkeyCount);
    }

    [Fact]
    public void CreateRun_WithWrapAround_ShouldBeValid()
    {
        // Arrange - Mavi 12-13-1
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Blue, 12),
            Tile.Create(2, TileColor.Blue, 13),
            Tile.Create(3, TileColor.Blue, 1)
        };

        // Act
        var meld = Meld.CreateRun(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(MeldType.Run, meld.Type);
    }

    [Fact]
    public void CreateRun_WithDifferentColors_ShouldBeInvalid()
    {
        // Arrange - Farklı renkler
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 5),
            Tile.Create(2, TileColor.Blue, 6),
            Tile.Create(3, TileColor.Red, 7)
        };

        // Act
        var meld = Meld.CreateRun(tiles);

        // Assert
        Assert.False(meld.IsValid);
        Assert.Equal(MeldType.Invalid, meld.Type);
    }

    [Fact]
    public void CreateRun_WithGap_ShouldBeInvalid()
    {
        // Arrange - Boşluk var (5-6-8)
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 5),
            Tile.Create(2, TileColor.Red, 6),
            Tile.Create(3, TileColor.Red, 8)
        };

        // Act
        var meld = Meld.CreateRun(tiles);

        // Assert
        Assert.False(meld.IsValid);
    }

    [Fact]
    public void CreateRun_WithOkey_ShouldFillGap()
    {
        // Arrange - Okey ile boşluk doldurma (5-Okey-7)
        var okey = Tile.Create(100, TileColor.Yellow, 6).AsOkey();
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 5),
            okey,
            Tile.Create(3, TileColor.Red, 7)
        };

        // Act
        var meld = Meld.CreateRun(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(MeldType.Run, meld.Type);
        Assert.Equal(1, meld.OkeyCount);
    }

    [Fact]
    public void CreateRun_FourTiles_ShouldBeValid()
    {
        // Arrange - 4'lü sıralı per
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Black, 3),
            Tile.Create(2, TileColor.Black, 4),
            Tile.Create(3, TileColor.Black, 5),
            Tile.Create(4, TileColor.Black, 6)
        };

        // Act
        var meld = Meld.CreateRun(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(4, meld.Tiles.Count);
    }

    [Fact]
    public void CreateRun_TwoTiles_ShouldBeInvalid()
    {
        // Arrange - 2 taş yeterli değil
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 5),
            Tile.Create(2, TileColor.Red, 6)
        };

        // Act
        var meld = Meld.CreateRun(tiles);

        // Assert
        Assert.False(meld.IsValid);
    }

    #endregion

    #region Group (Düz Per) Testleri

    [Fact]
    public void CreateGroup_ThreeDifferentColors_ShouldBeValid()
    {
        // Arrange - Farklı renklerden 7'ler
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Yellow, 7),
            Tile.Create(2, TileColor.Blue, 7),
            Tile.Create(3, TileColor.Black, 7)
        };

        // Act
        var meld = Meld.CreateGroup(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(MeldType.Group, meld.Type);
    }

    [Fact]
    public void CreateGroup_FourDifferentColors_ShouldBeValid()
    {
        // Arrange - 4 farklı renkten 10'lar
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Yellow, 10),
            Tile.Create(2, TileColor.Blue, 10),
            Tile.Create(3, TileColor.Black, 10),
            Tile.Create(4, TileColor.Red, 10)
        };

        // Act
        var meld = Meld.CreateGroup(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(4, meld.Tiles.Count);
    }

    [Fact]
    public void CreateGroup_SameColor_ShouldBeInvalid()
    {
        // Arrange - Aynı renkten taşlar
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 7),
            Tile.Create(2, TileColor.Red, 7),
            Tile.Create(3, TileColor.Blue, 7)
        };

        // Act
        var meld = Meld.CreateGroup(tiles);

        // Assert
        Assert.False(meld.IsValid);
    }

    [Fact]
    public void CreateGroup_DifferentValues_ShouldBeInvalid()
    {
        // Arrange - Farklı değerler
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Yellow, 7),
            Tile.Create(2, TileColor.Blue, 8),
            Tile.Create(3, TileColor.Black, 7)
        };

        // Act
        var meld = Meld.CreateGroup(tiles);

        // Assert
        Assert.False(meld.IsValid);
    }

    [Fact]
    public void CreateGroup_WithOkey_ShouldBeValid()
    {
        // Arrange - 2 renk + Okey
        var okey = Tile.Create(100, TileColor.Yellow, 1).AsOkey();
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Yellow, 7),
            Tile.Create(2, TileColor.Blue, 7),
            okey
        };

        // Act
        var meld = Meld.CreateGroup(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(1, meld.OkeyCount);
    }

    [Fact]
    public void CreateGroup_FiveTiles_ShouldBeInvalid()
    {
        // Arrange - 5 taş (maksimum 4)
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Yellow, 7),
            Tile.Create(2, TileColor.Blue, 7),
            Tile.Create(3, TileColor.Black, 7),
            Tile.Create(4, TileColor.Red, 7),
            Tile.CreateFalseJoker(5) // Okey ile 5. taş
        };

        // Act
        var meld = Meld.CreateGroup(tiles);

        // Assert
        Assert.False(meld.IsValid);
    }

    #endregion

    #region TryCreate Testleri

    [Fact]
    public void TryCreate_DetectsRunAutomatically()
    {
        // Arrange
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Yellow, 1),
            Tile.Create(2, TileColor.Yellow, 2),
            Tile.Create(3, TileColor.Yellow, 3)
        };

        // Act
        var meld = Meld.TryCreate(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(MeldType.Run, meld.Type);
    }

    [Fact]
    public void TryCreate_DetectsGroupAutomatically()
    {
        // Arrange
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Yellow, 5),
            Tile.Create(2, TileColor.Blue, 5),
            Tile.Create(3, TileColor.Red, 5)
        };

        // Act
        var meld = Meld.TryCreate(tiles);

        // Assert
        Assert.True(meld.IsValid);
        Assert.Equal(MeldType.Group, meld.Type);
    }

    #endregion
}
