namespace OkeyGame.Application.Interfaces;

/// <summary>
/// ELO puanı hesaplama servisi interface'i.
/// </summary>
public interface IEloCalculationService
{
    /// <summary>
    /// Oyun sonucuna göre ELO değişimlerini hesaplar.
    /// </summary>
    /// <param name="winnerId">Kazanan oyuncu ID</param>
    /// <param name="playerEloScores">Tüm oyuncuların mevcut ELO puanları</param>
    /// <param name="winType">Kazanma tipi (etki çarpanı için)</param>
    /// <returns>Her oyuncu için ELO değişimi</returns>
    EloCalculationResult Calculate(
        Guid winnerId,
        IReadOnlyDictionary<Guid, int> playerEloScores,
        Domain.Enums.WinType winType);

    /// <summary>
    /// İki oyuncu arasındaki 1v1 ELO değişimini hesaplar.
    /// </summary>
    (int WinnerChange, int LoserChange) CalculateHeadToHead(
        int winnerElo,
        int loserElo,
        bool isDraw = false);
}

/// <summary>
/// ELO hesaplama sonucu.
/// </summary>
public record EloCalculationResult
{
    /// <summary>Her oyuncu için ELO değişimi.</summary>
    public IReadOnlyDictionary<Guid, int> EloChanges { get; init; } 
        = new Dictionary<Guid, int>();

    /// <summary>Her oyuncu için yeni ELO puanı.</summary>
    public IReadOnlyDictionary<Guid, int> NewEloScores { get; init; } 
        = new Dictionary<Guid, int>();

    /// <summary>Ortalama rakip ELO'su.</summary>
    public double AverageOpponentElo { get; init; }

    /// <summary>Kazanma tipi çarpanı.</summary>
    public double WinTypeMultiplier { get; init; }
}
