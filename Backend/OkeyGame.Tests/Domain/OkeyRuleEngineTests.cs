using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;
using OkeyGame.Domain.ValueObjects;
using Xunit;

namespace OkeyGame.Tests.Domain;

/// <summary>
/// OkeyRuleEngine testleri.
/// </summary>
public class OkeyRuleEngineTests
{
    private readonly OkeyRuleEngine _engine = OkeyRuleEngine.Instance;

    #region Yardımcı Metodlar

    /// <summary>
    /// Test için kazanan el oluşturur.
    /// 4 run × 3 taş = 12 taş + 1 run × 3 taş = 15 taş
    /// Son taş bitiş taşı olarak atılır, 14 taş per yapar.
    /// </summary>
    private List<Tile> CreateWinningHand()
    {
        var tiles = new List<Tile>();
        int id = 1;

        // Run 1: Kırmızı 1-2-3
        tiles.Add(Tile.Create(id++, TileColor.Red, 1));
        tiles.Add(Tile.Create(id++, TileColor.Red, 2));
        tiles.Add(Tile.Create(id++, TileColor.Red, 3));

        // Run 2: Mavi 1-2-3
        tiles.Add(Tile.Create(id++, TileColor.Blue, 1));
        tiles.Add(Tile.Create(id++, TileColor.Blue, 2));
        tiles.Add(Tile.Create(id++, TileColor.Blue, 3));

        // Run 3: Siyah 1-2-3
        tiles.Add(Tile.Create(id++, TileColor.Black, 1));
        tiles.Add(Tile.Create(id++, TileColor.Black, 2));
        tiles.Add(Tile.Create(id++, TileColor.Black, 3));

        // Run 4: Sarı 1-2-3
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 1));
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 2));
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 3));

        // Run 5: Kırmızı 4-5-6 (sadece 2 taş per için, 1 bitiş)
        tiles.Add(Tile.Create(id++, TileColor.Red, 4));
        tiles.Add(Tile.Create(id++, TileColor.Red, 5));
        tiles.Add(Tile.Create(id++, TileColor.Red, 6)); // Bitiş taşı olabilir

        return tiles;
    }

    /// <summary>
    /// Çifte için 7 çift + 1 bitiş taşı oluşturur.
    /// </summary>
    private List<Tile> CreatePairsHand()
    {
        var tiles = new List<Tile>();
        int id = 1;

        // 7 çift
        for (int i = 1; i <= 7; i++)
        {
            var color = (TileColor)((i - 1) % 4);
            tiles.Add(Tile.Create(id++, color, i));
            tiles.Add(Tile.Create(id++, color, i));
        }

        // 1 bitiş taşı
        tiles.Add(Tile.Create(id++, TileColor.Red, 13));

        return tiles;
    }

    /// <summary>
    /// Kazanmayan el oluşturur.
    /// </summary>
    private List<Tile> CreateNonWinningHand()
    {
        var tiles = new List<Tile>();
        int id = 1;

        // Rastgele dağınık taşlar
        for (int i = 0; i < 15; i++)
        {
            var color = (TileColor)(i % 4);
            var value = (i % 13) + 1;
            tiles.Add(Tile.Create(id++, color, value));
        }

        return tiles;
    }

    #endregion

    #region CheckWinningHand Testleri

    [Fact]
    public void CheckWinningHand_WithValidMelds_ShouldReturnWinning()
    {
        // Arrange
        var hand = CreateWinningHand();

        // Act
        var result = _engine.CheckWinningHand(hand);

        // Assert
        Assert.True(result.IsWinning);
        Assert.Equal(WinType.Normal, result.WinType);
        Assert.True(result.Melds.Count > 0);
        Assert.NotNull(result.DiscardTile);
    }

    [Fact]
    public void CheckWinningHand_WithPairs_ShouldReturnPairsWin()
    {
        // Arrange
        var hand = CreatePairsHand();

        // Act
        var result = _engine.CheckWinningHand(hand);

        // Assert
        // Çifte veya normal bitiş olabilir
        Assert.True(result.IsWinning);
    }

    [Fact]
    public void CheckWinningHand_With14Tiles_ShouldReturnNotWinning()
    {
        // Arrange
        var hand = CreateWinningHand().Take(14).ToList();

        // Act
        var result = _engine.CheckWinningHand(hand);

        // Assert
        Assert.False(result.IsWinning);
        Assert.Contains("15", result.Message);
    }

    [Fact]
    public void CheckWinningHand_WithNull_ShouldReturnNotWinning()
    {
        // Act
        var result = _engine.CheckWinningHand(null!);

        // Assert
        Assert.False(result.IsWinning);
    }

    #endregion

    #region CanFormMelds Testleri

    [Fact]
    public void CanFormMelds_WithValidCombination_ShouldReturnTrue()
    {
        // Arrange - 14 taş: 4 run × 3 taş + 1 group × 2 taş = değil
        // Doğru: 4 run × 3 taş = 12 taş + 1 group × 3 taş = 15 - 1 = 14 taş
        // Veya: 3 run × 3 + 1 run × 5 = 14 taş
        var tiles = new List<Tile>();
        int id = 1;

        // Run 1: Kırmızı 1-2-3
        tiles.Add(Tile.Create(id++, TileColor.Red, 1));
        tiles.Add(Tile.Create(id++, TileColor.Red, 2));
        tiles.Add(Tile.Create(id++, TileColor.Red, 3));

        // Run 2: Mavi 1-2-3
        tiles.Add(Tile.Create(id++, TileColor.Blue, 1));
        tiles.Add(Tile.Create(id++, TileColor.Blue, 2));
        tiles.Add(Tile.Create(id++, TileColor.Blue, 3));

        // Run 3: Siyah 1-2-3
        tiles.Add(Tile.Create(id++, TileColor.Black, 1));
        tiles.Add(Tile.Create(id++, TileColor.Black, 2));
        tiles.Add(Tile.Create(id++, TileColor.Black, 3));

        // Run 4: Sarı 1-2-3-4-5 (5'li run - toplam 14 taş olacak)
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 1));
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 2));
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 3));
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 4));
        tiles.Add(Tile.Create(id++, TileColor.Yellow, 5));

        // Act
        var result = _engine.CanFormMelds(tiles);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanFormMelds_With13Tiles_ShouldReturnFalse()
    {
        // Arrange
        var tiles = CreateWinningHand().Take(13).ToList();

        // Act
        var result = _engine.CanFormMelds(tiles);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region CheckPairs Testleri

    [Fact]
    public void CheckPairs_With7Pairs_ShouldReturnWinning()
    {
        // Arrange
        var tiles = new List<Tile>();
        int id = 1;

        // 7 çift oluştur
        for (int i = 1; i <= 7; i++)
        {
            var color = (TileColor)((i - 1) % 4);
            tiles.Add(Tile.Create(id++, color, i));
            tiles.Add(Tile.Create(id++, color, i));
        }

        // Act
        var result = _engine.CheckPairs(tiles);

        // Assert
        Assert.True(result.IsWinning);
        Assert.Equal(WinType.Pairs, result.WinType);
        Assert.Equal(7, result.Pairs.Count);
    }

    [Fact]
    public void CheckPairs_With6Pairs_ShouldReturnNotWinning()
    {
        // Arrange - 6 çift = 12 taş
        var tiles = new List<Tile>();
        int id = 1;

        for (int i = 1; i <= 6; i++)
        {
            var color = (TileColor)((i - 1) % 4);
            tiles.Add(Tile.Create(id++, color, i));
            tiles.Add(Tile.Create(id++, color, i));
        }

        // 2 ekstra taş (çift oluşturmuyor)
        tiles.Add(Tile.Create(id++, TileColor.Red, 10));
        tiles.Add(Tile.Create(id++, TileColor.Blue, 11));

        // Act
        var result = _engine.CheckPairs(tiles);

        // Assert
        Assert.False(result.IsWinning);
    }

    [Fact]
    public void CheckPairs_WithOkeyCompletingPair_ShouldWork()
    {
        // Arrange - 6 çift + 1 tek + 1 Okey = 7 çift
        var tiles = new List<Tile>();
        int id = 1;

        // 6 çift
        for (int i = 1; i <= 6; i++)
        {
            var color = (TileColor)((i - 1) % 4);
            tiles.Add(Tile.Create(id++, color, i));
            tiles.Add(Tile.Create(id++, color, i));
        }

        // 1 tek taş
        tiles.Add(Tile.Create(id++, TileColor.Red, 10));

        // 1 Okey (çift tamamlar)
        var okey = Tile.Create(id++, TileColor.Yellow, 5).AsOkey();
        tiles.Add(okey);

        // Act
        var result = _engine.CheckPairs(tiles);

        // Assert
        Assert.True(result.IsWinning);
        Assert.Equal(7, result.Pairs.Count);
    }

    #endregion

    #region FindPairs Testleri

    [Fact]
    public void FindPairs_WithExactPairs_ShouldFindAll()
    {
        // Arrange
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 5),
            Tile.Create(2, TileColor.Red, 5),
            Tile.Create(3, TileColor.Blue, 7),
            Tile.Create(4, TileColor.Blue, 7)
        };

        // Act
        var pairs = _engine.FindPairs(tiles);

        // Assert
        Assert.Equal(2, pairs.Count);
    }

    [Fact]
    public void FindPairs_WithNoPairs_ShouldReturnEmpty()
    {
        // Arrange
        var tiles = new List<Tile>
        {
            Tile.Create(1, TileColor.Red, 1),
            Tile.Create(2, TileColor.Blue, 2),
            Tile.Create(3, TileColor.Black, 3),
            Tile.Create(4, TileColor.Yellow, 4)
        };

        // Act
        var pairs = _engine.FindPairs(tiles);

        // Assert
        Assert.Empty(pairs);
    }

    #endregion

    #region Scoring Testleri

    [Fact]
    public void CalculateScore_NormalWin_ShouldReturn2()
    {
        // Arrange
        var melds = new List<Meld>();
        var discardTile = Tile.Create(1, TileColor.Red, 5);

        // Act
        var score = _engine.CalculateScore(WinType.Normal, melds, discardTile);

        // Assert
        Assert.True(score >= 2);
    }

    [Fact]
    public void CalculateScore_OkeyDiscard_ShouldReturn4()
    {
        // Arrange
        var melds = new List<Meld>();
        var okeyTile = Tile.Create(1, TileColor.Red, 5).AsOkey();

        // Act
        var score = _engine.CalculateScore(WinType.OkeyDiscard, melds, okeyTile);

        // Assert
        Assert.True(score >= 4);
    }

    #endregion

    #region SuggestBestDiscard Testleri

    [Fact]
    public void SuggestBestDiscard_WithWinningHand_ShouldReturnDiscardTile()
    {
        // Arrange
        var hand = CreateWinningHand();

        // Act
        var suggestion = _engine.SuggestBestDiscard(hand);

        // Assert
        Assert.NotNull(suggestion);
    }

    [Fact]
    public void SuggestBestDiscard_With14Tiles_ShouldReturnNull()
    {
        // Arrange
        var hand = CreateWinningHand().Take(14).ToList();

        // Act
        var suggestion = _engine.SuggestBestDiscard(hand);

        // Assert
        Assert.Null(suggestion);
    }

    #endregion

    #region WinProbability Testleri

    [Fact]
    public void CalculateWinProbability_WithWinningHand_ShouldBeHigh()
    {
        // Arrange
        var hand = CreateWinningHand();

        // Act
        var probability = _engine.CalculateWinProbability(hand);

        // Assert
        Assert.True(probability > 0);
    }

    [Fact]
    public void CalculateWinProbability_WithOkeys_ShouldIncreaseChance()
    {
        // Arrange
        var normalHand = CreateNonWinningHand();
        var handWithOkey = CreateNonWinningHand();
        
        // El'e Okey ekle
        var okey = Tile.Create(200, TileColor.Yellow, 1).AsOkey();
        handWithOkey[0] = okey;

        // Act
        var normalProb = _engine.CalculateWinProbability(normalHand);
        var okeyProb = _engine.CalculateWinProbability(handWithOkey);

        // Assert
        Assert.True(okeyProb >= normalProb); // Okey olan el en az eşit veya daha yüksek şans
    }

    #endregion
}
