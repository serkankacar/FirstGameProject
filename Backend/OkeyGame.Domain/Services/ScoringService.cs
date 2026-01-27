using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.Services;

/// <summary>
/// Puanlama sistemi.
/// Oyun sonunda oyuncuların puanlarını hesaplar.
/// </summary>
public class ScoringService
{
    #region Sabitler - Puan Değerleri

    /// <summary>Normal bitiş puanı (düşülen rakiplere).</summary>
    public const int NormalWinPenalty = 2;

    /// <summary>Çifte bitiş puanı.</summary>
    public const int PairsWinPenalty = 4;

    /// <summary>Okey atarak bitiş puanı.</summary>
    public const int OkeyDiscardPenalty = 4;

    /// <summary>Elle okey varsa ek ceza.</summary>
    public const int HandOkeyPenalty = 2;

    /// <summary>Deste bittiğinde herkes bu puanı alır.</summary>
    public const int DeckEmptyPenalty = 1;

    #endregion

    #region Singleton

    private static readonly Lazy<ScoringService> _instance = 
        new(() => new ScoringService());

    public static ScoringService Instance => _instance.Value;

    private ScoringService() { }

    #endregion

    #region Ana Metodlar

    /// <summary>
    /// Oyun sonunda tüm oyuncuların puanlarını hesaplar.
    /// </summary>
    public GameScoreResult CalculateGameScore(
        Guid winnerId,
        WinType winType,
        Dictionary<Guid, List<Tile>> playerHands,
        Tile? indicatorTile = null)
    {
        var result = new GameScoreResult
        {
            WinnerId = winnerId,
            WinType = winType
        };

        // Kazanan 0 puan alır
        result.Scores[winnerId] = 0;

        // Temel ceza puanı
        int basePenalty = GetBasePenalty(winType);

        // Diğer oyuncuların puanlarını hesapla
        foreach (var (playerId, hand) in playerHands)
        {
            if (playerId == winnerId) continue;

            int penalty = basePenalty;

            // Elde Okey var mı?
            int okeyCount = hand.Count(t => t.IsOkey);
            penalty += okeyCount * HandOkeyPenalty;

            // Elde False Joker var mı? (Okey yerine kullanılabilirdi)
            int falseJokerCount = hand.Count(t => t.IsFalseJoker);
            if (falseJokerCount > 0 && winType == WinType.OkeyDiscard)
            {
                // Okey atarak bitirildi ve elde sahte okey var
                penalty += falseJokerCount;
            }

            result.Scores[playerId] = penalty;
            result.Details[playerId] = CreatePenaltyDetails(basePenalty, okeyCount, falseJokerCount, winType);
        }

        return result;
    }

    /// <summary>
    /// Deste bittiğinde puanları hesaplar.
    /// </summary>
    public GameScoreResult CalculateDeckEmptyScore(Dictionary<Guid, List<Tile>> playerHands)
    {
        var result = new GameScoreResult
        {
            WinnerId = Guid.Empty,
            WinType = WinType.DeckEmpty
        };

        foreach (var (playerId, hand) in playerHands)
        {
            int penalty = DeckEmptyPenalty;

            // Elde Okey var mı?
            int okeyCount = hand.Count(t => t.IsOkey);
            penalty += okeyCount * HandOkeyPenalty;

            result.Scores[playerId] = penalty;
            result.Details[playerId] = $"Deste bitti: {DeckEmptyPenalty} + Okey({okeyCount}×{HandOkeyPenalty})";
        }

        return result;
    }

    /// <summary>
    /// Taş değerlerinden toplam puanı hesaplar.
    /// Bazı varyantlarda elde kalan taşların değeri sayılır.
    /// </summary>
    public int CalculateHandValue(IReadOnlyList<Tile> hand, Tile? indicatorTile = null)
    {
        int total = 0;

        foreach (var tile in hand)
        {
            if (tile.IsOkey)
            {
                // Okey'in değeri, göstergenin bir üstü
                if (indicatorTile != null)
                {
                    int okeyValue = indicatorTile.Value == 13 ? 1 : indicatorTile.Value + 1;
                    total += okeyValue;
                }
                else
                {
                    total += 13; // Varsayılan en yüksek değer
                }
            }
            else if (tile.IsFalseJoker)
            {
                total += 0; // Sahte okey değersiz
            }
            else
            {
                total += tile.Value;
            }
        }

        return total;
    }

    #endregion

    #region Lider Tablosu

    /// <summary>
    /// Çoklu oyun sonunda toplam skorları hesaplar.
    /// </summary>
    public Dictionary<Guid, int> CalculateTotalScores(List<GameScoreResult> gameResults)
    {
        var totals = new Dictionary<Guid, int>();

        foreach (var result in gameResults)
        {
            foreach (var (playerId, score) in result.Scores)
            {
                if (!totals.ContainsKey(playerId))
                {
                    totals[playerId] = 0;
                }
                totals[playerId] += score;
            }
        }

        return totals;
    }

    /// <summary>
    /// Oyuncu sıralamasını döndürür (en düşük puan en iyi).
    /// </summary>
    public List<(Guid PlayerId, int TotalScore, int Rank)> GetRankings(Dictionary<Guid, int> totalScores)
    {
        return totalScores
            .OrderBy(x => x.Value)
            .Select((x, index) => (x.Key, x.Value, index + 1))
            .ToList();
    }

    #endregion

    #region Yardımcı Metodlar

    private int GetBasePenalty(WinType winType)
    {
        return winType switch
        {
            WinType.Normal => NormalWinPenalty,
            WinType.Pairs => PairsWinPenalty,
            WinType.OkeyDiscard => OkeyDiscardPenalty,
            WinType.DeckEmpty => DeckEmptyPenalty,
            _ => 0
        };
    }

    private string CreatePenaltyDetails(int basePenalty, int okeyCount, int falseJokerCount, WinType winType)
    {
        var parts = new List<string>();

        string winTypeStr = winType switch
        {
            WinType.Normal => "Normal bitiş",
            WinType.Pairs => "Çifte bitiş",
            WinType.OkeyDiscard => "Okey atarak bitiş",
            _ => "Bitiş"
        };

        parts.Add($"{winTypeStr}: {basePenalty}");

        if (okeyCount > 0)
        {
            parts.Add($"Elde Okey: {okeyCount}×{HandOkeyPenalty}");
        }

        if (falseJokerCount > 0 && winType == WinType.OkeyDiscard)
        {
            parts.Add($"Elde Sahte Okey: {falseJokerCount}");
        }

        return string.Join(" + ", parts);
    }

    #endregion
}

/// <summary>
/// Oyun skor sonucu.
/// </summary>
public class GameScoreResult
{
    /// <summary>Kazanan oyuncu ID'si.</summary>
    public Guid WinnerId { get; set; }

    /// <summary>Bitiş türü.</summary>
    public WinType WinType { get; set; }

    /// <summary>Oyuncu puanları.</summary>
    public Dictionary<Guid, int> Scores { get; } = new();

    /// <summary>Puan detayları.</summary>
    public Dictionary<Guid, string> Details { get; } = new();

    /// <summary>Oyun zamanı.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Oyun Sonucu ({WinType}) ===");
        sb.AppendLine($"Kazanan: {WinnerId}");
        sb.AppendLine("Puanlar:");

        foreach (var (playerId, score) in Scores.OrderBy(x => x.Value))
        {
            var detail = Details.GetValueOrDefault(playerId, "");
            sb.AppendLine($"  {playerId}: {score} puan {(string.IsNullOrEmpty(detail) ? "" : $"({detail})")}");
        }

        return sb.ToString();
    }
}
