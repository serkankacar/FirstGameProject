using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.AI;

/// <summary>
/// Bot'un taş hafızası.
/// Masaya atılan ve rakiplerin çektiği taşları takip eder.
/// ADALET: Bot sadece açık bilgiyi (discard pile) görür, rakip ellerini görmez.
/// </summary>
public class TileMemory
{
    #region Sabitler

    /// <summary>Her renkten toplam taş sayısı (1-13 × 2 kopya = 26).</summary>
    private const int TilesPerColor = 26;

    /// <summary>Toplam taş sayısı (4 renk × 26 + 2 sahte okey = 106).</summary>
    private const int TotalTiles = 106;

    #endregion

    #region Veri Yapıları

    /// <summary>
    /// Her taş için görülme durumu.
    /// Key: (Color, Value), Value: Görülen kopya sayısı (0, 1 veya 2)
    /// </summary>
    private readonly Dictionary<(TileColor Color, int Value), int> _seenTiles = new();

    /// <summary>Atılan taşlar listesi (sıralı).</summary>
    private readonly List<Tile> _discardedTiles = new();

    /// <summary>Hangi oyuncu hangi taşı çekti.</summary>
    private readonly Dictionary<Guid, List<Tile>> _playerPickups = new();

    /// <summary>Gösterge taşı (Okey'i belirler).</summary>
    public Tile? IndicatorTile { get; private set; }

    /// <summary>Okey taşının rengi ve değeri.</summary>
    public (TileColor Color, int Value)? OkeyIdentity { get; private set; }

    #endregion

    #region Public Metodlar

    /// <summary>
    /// Gösterge taşını ayarlar ve Okey'i hesaplar.
    /// </summary>
    public void SetIndicator(Tile indicator)
    {
        IndicatorTile = indicator;
        
        // Okey = Göstergenin bir fazlası, aynı renk
        int okeyValue = indicator.Value == 13 ? 1 : indicator.Value + 1;
        OkeyIdentity = (indicator.Color, okeyValue);

        // Gösterge taşını da gördük
        RecordSeenTile(indicator);
    }

    /// <summary>
    /// Bir taşın masaya atıldığını kaydeder.
    /// </summary>
    public void RecordDiscard(Tile tile, Guid? playerId = null)
    {
        _discardedTiles.Add(tile);
        RecordSeenTile(tile);
    }

    /// <summary>
    /// Bir oyuncunun discard'dan taş çektiğini kaydeder.
    /// </summary>
    public void RecordPickupFromDiscard(Tile tile, Guid playerId)
    {
        if (!_playerPickups.ContainsKey(playerId))
        {
            _playerPickups[playerId] = new List<Tile>();
        }
        _playerPickups[playerId].Add(tile);

        // Discard'dan çıkar (artık orda değil)
        var lastIndex = _discardedTiles.FindLastIndex(t => t.Id == tile.Id);
        if (lastIndex >= 0)
        {
            _discardedTiles.RemoveAt(lastIndex);
        }
    }

    /// <summary>
    /// Bir taşın görüldüğünü kaydeder.
    /// </summary>
    public void RecordSeenTile(Tile tile)
    {
        if (tile.IsFalseJoker) return; // Sahte okey ayrı takip edilir

        var key = (tile.Color, tile.Value);
        if (!_seenTiles.ContainsKey(key))
        {
            _seenTiles[key] = 0;
        }
        _seenTiles[key] = Math.Min(2, _seenTiles[key] + 1);
    }

    /// <summary>
    /// Bir taştan kaç kopya görüldüğünü döndürür.
    /// </summary>
    public int GetSeenCount(TileColor color, int value)
    {
        var key = (color, value);
        return _seenTiles.GetValueOrDefault(key, 0);
    }

    /// <summary>
    /// Bir taşın hala destede olma olasılığını hesaplar.
    /// </summary>
    public double GetAvailabilityProbability(TileColor color, int value)
    {
        int seenCount = GetSeenCount(color, value);
        int remaining = 2 - seenCount; // Her taştan 2 kopya var

        if (remaining <= 0) return 0.0;

        // Basit olasılık: Kalan taş sayısı / Olası toplam
        return remaining / 2.0;
    }

    /// <summary>
    /// Son atılan taşı döndürür.
    /// </summary>
    public Tile? GetLastDiscardedTile()
    {
        return _discardedTiles.Count > 0 ? _discardedTiles[^1] : null;
    }

    /// <summary>
    /// Tüm atılan taşları döndürür.
    /// </summary>
    public IReadOnlyList<Tile> GetDiscardedTiles()
    {
        return _discardedTiles.AsReadOnly();
    }

    /// <summary>
    /// Bir oyuncunun discard'dan çektiği taşları döndürür.
    /// </summary>
    public IReadOnlyList<Tile> GetPlayerPickups(Guid playerId)
    {
        return _playerPickups.GetValueOrDefault(playerId, new List<Tile>()).AsReadOnly();
    }

    /// <summary>
    /// Hafızayı temizler.
    /// </summary>
    public void Clear()
    {
        _seenTiles.Clear();
        _discardedTiles.Clear();
        _playerPickups.Clear();
        IndicatorTile = null;
        OkeyIdentity = null;
    }

    #endregion

    #region Analiz Metodları

    /// <summary>
    /// Bir Run için eksik taşların bulunabilirlik olasılığını hesaplar.
    /// Örn: Elimde Mavi 5-7 var, 6 lazım. 6'nın olasılığı nedir?
    /// </summary>
    public double GetRunCompletionProbability(TileColor color, int startValue, int endValue)
    {
        double probability = 1.0;

        for (int v = startValue; v <= endValue; v++)
        {
            double p = GetAvailabilityProbability(color, v);
            probability *= p;
        }

        return probability;
    }

    /// <summary>
    /// Bir Group için eksik taşların bulunabilirlik olasılığını hesaplar.
    /// Örn: Elimde Sarı 7, Mavi 7 var. Siyah veya Kırmızı 7 lazım.
    /// </summary>
    public double GetGroupCompletionProbability(int value, IEnumerable<TileColor> existingColors)
    {
        var existing = existingColors.ToHashSet();
        var missingColors = Enum.GetValues<TileColor>().Where(c => !existing.Contains(c));

        // En az bir rengin bulunabilir olması yeterli
        double anyAvailable = 0.0;
        foreach (var color in missingColors)
        {
            anyAvailable += GetAvailabilityProbability(color, value);
        }

        return Math.Min(1.0, anyAvailable);
    }

    /// <summary>
    /// Bir taşın Okey olup olmadığını kontrol eder.
    /// </summary>
    public bool IsOkeyTile(Tile tile)
    {
        if (tile.IsOkey) return true;
        if (OkeyIdentity == null) return false;

        return tile.Color == OkeyIdentity.Value.Color && 
               tile.Value == OkeyIdentity.Value.Value;
    }

    #endregion
}
