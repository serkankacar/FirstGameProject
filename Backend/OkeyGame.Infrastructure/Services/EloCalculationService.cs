using OkeyGame.Application.Interfaces;
using OkeyGame.Domain.Enums;

namespace OkeyGame.Infrastructure.Services;

/// <summary>
/// ELO puanı hesaplama servisi.
/// 
/// TASARIM:
/// - Standart ELO formülü kullanılır
/// - K-faktör: Oyuncu tecrübesine göre ayarlanır
/// - Çarpanlar: Kazanma tipine göre bonus/ceza
/// 
/// ELO FORMÜLÜ:
/// Expected Score = 1 / (1 + 10^((RatingB - RatingA) / 400))
/// New Rating = Old Rating + K * (Actual Score - Expected Score)
/// 
/// 4 OYUNCULU ADAPTASYON:
/// - Kazanan: Tüm kaybedenlere karşı 1-0 olarak hesaplanır
/// - Kaybedenler: Kazanana karşı 0-1, birbirlerine karşı 0.5-0.5
/// </summary>
public class EloCalculationService : IEloCalculationService
{
    #region Sabitler

    /// <summary>Yeni oyuncular için K-faktör (daha hızlı değişim).</summary>
    public const int KFactorNewPlayer = 40;

    /// <summary>Normal oyuncular için K-faktör.</summary>
    public const int KFactorNormal = 20;

    /// <summary>Deneyimli oyuncular için K-faktör (daha yavaş değişim).</summary>
    public const int KFactorExperienced = 10;

    /// <summary>Yeni oyuncu sınırı (oyun sayısı).</summary>
    public const int NewPlayerThreshold = 30;

    /// <summary>Deneyimli oyuncu sınırı.</summary>
    public const int ExperiencedPlayerThreshold = 100;

    /// <summary>Minimum ELO değişimi (pozitif veya negatif).</summary>
    public const int MinEloChange = 1;

    /// <summary>Maksimum ELO değişimi.</summary>
    public const int MaxEloChange = 50;

    #endregion

    #region Win Type Çarpanları

    private static readonly Dictionary<WinType, double> WinTypeMultipliers = new()
    {
        { WinType.None, 1.0 },
        { WinType.Normal, 1.0 },
        { WinType.Pairs, 1.5 },      // Çifte: %50 bonus
        { WinType.OkeyDiscard, 2.0 }, // Okey atarak: %100 bonus
        { WinType.DeckEmpty, 0.5 }    // Deste bitimi: %50 azaltma
    };

    #endregion

    /// <inheritdoc />
    public EloCalculationResult Calculate(
        Guid winnerId,
        IReadOnlyDictionary<Guid, int> playerEloScores,
        WinType winType)
    {
        ArgumentNullException.ThrowIfNull(playerEloScores);

        if (playerEloScores.Count < 2)
        {
            throw new ArgumentException("En az 2 oyuncu gerekli.", nameof(playerEloScores));
        }

        if (!playerEloScores.ContainsKey(winnerId))
        {
            throw new ArgumentException("Kazanan oyuncu listede bulunamadı.", nameof(winnerId));
        }

        var eloChanges = new Dictionary<Guid, int>();
        var newEloScores = new Dictionary<Guid, int>();

        int winnerCurrentElo = playerEloScores[winnerId];
        var loserIds = playerEloScores.Keys.Where(id => id != winnerId).ToList();

        // Ortalama rakip ELO hesapla
        double averageOpponentElo = loserIds.Average(id => playerEloScores[id]);

        // Win type çarpanı
        double multiplier = WinTypeMultipliers.GetValueOrDefault(winType, 1.0);

        // Kazananın toplam kazancı
        int winnerTotalGain = 0;

        foreach (var loserId in loserIds)
        {
            int loserElo = playerEloScores[loserId];

            // 1v1 hesapla
            var (winnerGain, loserLoss) = CalculateHeadToHeadInternal(
                winnerCurrentElo, loserElo, multiplier);

            winnerTotalGain += winnerGain;

            // Kaybedenin değişimi (negatif)
            if (eloChanges.ContainsKey(loserId))
            {
                eloChanges[loserId] += loserLoss;
            }
            else
            {
                eloChanges[loserId] = loserLoss;
            }
        }

        // Kazananın değişimi
        eloChanges[winnerId] = winnerTotalGain;

        // Yeni ELO puanları
        foreach (var (playerId, currentElo) in playerEloScores)
        {
            int change = eloChanges.GetValueOrDefault(playerId, 0);
            newEloScores[playerId] = Math.Max(100, currentElo + change); // Min 100 ELO
        }

        return new EloCalculationResult
        {
            EloChanges = eloChanges,
            NewEloScores = newEloScores,
            AverageOpponentElo = averageOpponentElo,
            WinTypeMultiplier = multiplier
        };
    }

    /// <inheritdoc />
    public (int WinnerChange, int LoserChange) CalculateHeadToHead(
        int winnerElo,
        int loserElo,
        bool isDraw = false)
    {
        return CalculateHeadToHeadInternal(winnerElo, loserElo, 1.0, isDraw);
    }

    #region Yardımcı Metodlar

    private static (int WinnerChange, int LoserChange) CalculateHeadToHeadInternal(
        int winnerElo,
        int loserElo,
        double multiplier,
        bool isDraw = false)
    {
        // Expected score hesapla
        double expectedWinner = CalculateExpectedScore(winnerElo, loserElo);
        double expectedLoser = 1 - expectedWinner;

        // Actual score
        double actualWinner = isDraw ? 0.5 : 1.0;
        double actualLoser = isDraw ? 0.5 : 0.0;

        // K-faktör (basitleştirilmiş - ideal olarak oyun sayısına göre belirlenir)
        int kFactor = KFactorNormal;

        // ELO değişimi hesapla
        int winnerChange = (int)Math.Round(kFactor * (actualWinner - expectedWinner) * multiplier);
        int loserChange = (int)Math.Round(kFactor * (actualLoser - expectedLoser) * multiplier);

        // Minimum değişim uygula
        if (Math.Abs(winnerChange) < MinEloChange && !isDraw)
        {
            winnerChange = actualWinner > expectedWinner ? MinEloChange : -MinEloChange;
        }

        if (Math.Abs(loserChange) < MinEloChange && !isDraw)
        {
            loserChange = actualLoser > expectedLoser ? MinEloChange : -MinEloChange;
        }

        // Maksimum sınırla
        winnerChange = Math.Clamp(winnerChange, -MaxEloChange, MaxEloChange);
        loserChange = Math.Clamp(loserChange, -MaxEloChange, MaxEloChange);

        return (winnerChange, loserChange);
    }

    /// <summary>
    /// Beklenen skor hesaplar (0-1 arası).
    /// </summary>
    private static double CalculateExpectedScore(int playerElo, int opponentElo)
    {
        return 1.0 / (1.0 + Math.Pow(10, (opponentElo - playerElo) / 400.0));
    }

    /// <summary>
    /// Oyuncu tecrübesine göre K-faktör belirler.
    /// </summary>
    public static int GetKFactor(int gamesPlayed)
    {
        if (gamesPlayed < NewPlayerThreshold)
            return KFactorNewPlayer;
        
        if (gamesPlayed < ExperiencedPlayerThreshold)
            return KFactorNormal;
        
        return KFactorExperienced;
    }

    #endregion
}
