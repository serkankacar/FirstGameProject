namespace OkeyGame.Domain.AI;

/// <summary>
/// Heuristic fonksiyon ağırlıkları.
/// Bot zorluk seviyesine göre ayarlanabilir.
/// </summary>
public class HeuristicWeights
{
    #region Temel Puanlar

    /// <summary>Yalnız kalan taş (komşusu yok).</summary>
    public int IsolatedTile { get; set; } = 2;

    /// <summary>Yan yana gelen taş (5-6 gibi).</summary>
    public int AdjacentPair { get; set; } = 5;

    /// <summary>Bir boşlukla ayrılmış (5-7 gibi, 6 lazım).</summary>
    public int GapPair { get; set; } = 3;

    /// <summary>Aynı değer farklı renk (7 sarı, 7 mavi).</summary>
    public int SameValuePair { get; set; } = 4;

    /// <summary>Per olmuş taşlar (3+ taş).</summary>
    public int CompletedMeld { get; set; } = 10;

    /// <summary>Okey taşı (çok değerli).</summary>
    public int OkeyTile { get; set; } = 15;

    /// <summary>Sahte Okey (göstergenin kendisi).</summary>
    public int FalseJoker { get; set; } = 1;

    #endregion

    #region Hafıza Bazlı Ayarlamalar

    /// <summary>Eksik taş görüldüyse (per olma ihtimali düştü).</summary>
    public int MissingTileSeen { get; set; } = -3;

    /// <summary>Her iki kopya da görüldüyse (imkansız per).</summary>
    public int BothCopiesSeen { get; set; } = -5;

    /// <summary>Rakip o taşı çekti (tehlikeli).</summary>
    public int OpponentPickedRelated { get; set; } = -2;

    #endregion

    #region Stratejik Ayarlamalar

    /// <summary>Yüksek değerli taş bonusu (bitiremezsen ceza).</summary>
    public int HighValueBonus { get; set; } = 1;

    /// <summary>Düşük değerli taş bonusu (güvenli atış).</summary>
    public int LowValueBonus { get; set; } = 0;

    /// <summary>Orta oyun (taş sayısı azaldıkça risk artar).</summary>
    public int LateGameRiskMultiplier { get; set; } = 2;

    #endregion

    #region Preset'ler

    /// <summary>Kolay bot için ağırlıklar.</summary>
    public static HeuristicWeights Easy => new()
    {
        IsolatedTile = 1,
        AdjacentPair = 3,
        GapPair = 2,
        SameValuePair = 2,
        CompletedMeld = 5,
        OkeyTile = 8,
        FalseJoker = 0,
        MissingTileSeen = 0,
        BothCopiesSeen = 0,
        OpponentPickedRelated = 0,
        HighValueBonus = 0,
        LowValueBonus = 0,
        LateGameRiskMultiplier = 1
    };

    /// <summary>Normal bot için ağırlıklar.</summary>
    public static HeuristicWeights Normal => new()
    {
        IsolatedTile = 2,
        AdjacentPair = 5,
        GapPair = 3,
        SameValuePair = 4,
        CompletedMeld = 10,
        OkeyTile = 12,
        FalseJoker = 1,
        MissingTileSeen = -2,
        BothCopiesSeen = -3,
        OpponentPickedRelated = -1,
        HighValueBonus = 1,
        LowValueBonus = 0,
        LateGameRiskMultiplier = 1
    };

    /// <summary>Zor bot için ağırlıklar.</summary>
    public static HeuristicWeights Hard => new()
    {
        IsolatedTile = 2,
        AdjacentPair = 6,
        GapPair = 4,
        SameValuePair = 5,
        CompletedMeld = 12,
        OkeyTile = 15,
        FalseJoker = 1,
        MissingTileSeen = -3,
        BothCopiesSeen = -5,
        OpponentPickedRelated = -2,
        HighValueBonus = 1,
        LowValueBonus = 0,
        LateGameRiskMultiplier = 2
    };

    /// <summary>Uzman bot için ağırlıklar.</summary>
    public static HeuristicWeights Expert => new()
    {
        IsolatedTile = 3,
        AdjacentPair = 7,
        GapPair = 5,
        SameValuePair = 6,
        CompletedMeld = 15,
        OkeyTile = 20,
        FalseJoker = 2,
        MissingTileSeen = -4,
        BothCopiesSeen = -8,
        OpponentPickedRelated = -3,
        HighValueBonus = 2,
        LowValueBonus = 0,
        LateGameRiskMultiplier = 3
    };

    /// <summary>Zorluk seviyesine göre ağırlık döndürür.</summary>
    public static HeuristicWeights ForDifficulty(BotDifficulty difficulty)
    {
        return difficulty switch
        {
            BotDifficulty.Easy => Easy,
            BotDifficulty.Normal => Normal,
            BotDifficulty.Hard => Hard,
            BotDifficulty.Expert => Expert,
            _ => Normal
        };
    }

    #endregion
}
