using OkeyGame.Domain.Entities;

namespace OkeyGame.Domain.AI;

/// <summary>
/// Taş puanlama sonucu.
/// Heuristic fonksiyonun çıktısı.
/// </summary>
public class TileScore : IComparable<TileScore>
{
    /// <summary>Değerlendirilen taş.</summary>
    public Tile Tile { get; }

    /// <summary>Toplam puan (yüksek = değerli, düşük = atılabilir).</summary>
    public int TotalScore { get; private set; }

    /// <summary>Puan detayları.</summary>
    public Dictionary<string, int> ScoreBreakdown { get; } = new();

    /// <summary>Taşın per potansiyeli açıklaması.</summary>
    public string Explanation { get; set; } = "";

    public TileScore(Tile tile)
    {
        Tile = tile;
    }

    /// <summary>
    /// Puan ekler.
    /// </summary>
    public void AddScore(string reason, int points)
    {
        ScoreBreakdown[reason] = points;
        TotalScore += points;
    }

    public int CompareTo(TileScore? other)
    {
        if (other == null) return 1;
        return TotalScore.CompareTo(other.TotalScore);
    }

    public override string ToString()
    {
        return $"{Tile}: {TotalScore} puan ({string.Join(", ", ScoreBreakdown.Select(kv => $"{kv.Key}:{kv.Value}"))})";
    }
}
