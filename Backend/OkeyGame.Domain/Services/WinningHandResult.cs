using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.ValueObjects;

namespace OkeyGame.Domain.Services;

/// <summary>
/// Kazanan el sonucu.
/// </summary>
public class WinningHandResult
{
    #region Özellikler

    /// <summary>El kazandı mı?</summary>
    public bool IsWinning { get; }

    /// <summary>Bitiş türü.</summary>
    public WinType WinType { get; }

    /// <summary>Oluşturulan perler.</summary>
    public IReadOnlyList<Meld> Melds { get; }

    /// <summary>Oluşturulan çiftler (çifte bitirme için).</summary>
    public IReadOnlyList<List<Tile>> Pairs { get; }

    /// <summary>Atılan bitiş taşı.</summary>
    public Tile? DiscardTile { get; }

    /// <summary>Kazanılan puan.</summary>
    public int Score { get; }

    /// <summary>Hata veya bilgi mesajı.</summary>
    public string Message { get; }

    #endregion

    #region Constructor

    public WinningHandResult(
        bool isWinning,
        WinType winType = WinType.None,
        List<Meld>? melds = null,
        List<List<Tile>>? pairs = null,
        Tile? discardTile = null,
        int score = 0,
        string message = "")
    {
        IsWinning = isWinning;
        WinType = winType;
        Melds = melds?.AsReadOnly() ?? new List<Meld>().AsReadOnly();
        Pairs = pairs?.AsReadOnly() ?? new List<List<Tile>>().AsReadOnly();
        DiscardTile = discardTile;
        Score = score;
        Message = message;
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Kazanmayan el sonucu oluşturur.
    /// </summary>
    public static WinningHandResult NotWinning(string reason)
    {
        return new WinningHandResult(
            isWinning: false,
            message: reason
        );
    }

    /// <summary>
    /// Normal kazanan el sonucu oluşturur.
    /// </summary>
    public static WinningHandResult NormalWin(List<Meld> melds, Tile discardTile, int score)
    {
        return new WinningHandResult(
            isWinning: true,
            winType: WinType.Normal,
            melds: melds,
            discardTile: discardTile,
            score: score
        );
    }

    /// <summary>
    /// Okey atarak kazanan el sonucu oluşturur.
    /// </summary>
    public static WinningHandResult OkeyDiscardWin(List<Meld> melds, Tile okeyTile, int score)
    {
        return new WinningHandResult(
            isWinning: true,
            winType: WinType.OkeyDiscard,
            melds: melds,
            discardTile: okeyTile,
            score: score
        );
    }

    /// <summary>
    /// Çifte kazanan el sonucu oluşturur.
    /// </summary>
    public static WinningHandResult PairsWin(List<List<Tile>> pairs, Tile? discardTile, int score)
    {
        return new WinningHandResult(
            isWinning: true,
            winType: WinType.Pairs,
            pairs: pairs,
            discardTile: discardTile,
            score: score
        );
    }

    #endregion

    #region Display

    public override string ToString()
    {
        if (!IsWinning)
        {
            return $"Kazanmadı: {Message}";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🎉 KAZANDI! Tür: {WinType}, Puan: {Score}");

        if (Melds.Count > 0)
        {
            sb.AppendLine("Perler:");
            foreach (var meld in Melds)
            {
                sb.AppendLine($"  - {meld}");
            }
        }

        if (Pairs.Count > 0)
        {
            sb.AppendLine("Çiftler:");
            foreach (var pair in Pairs)
            {
                sb.AppendLine($"  - {pair[0]} & {pair[1]}");
            }
        }

        if (DiscardTile != null)
        {
            sb.AppendLine($"Bitiş taşı: {DiscardTile}");
        }

        return sb.ToString();
    }

    #endregion
}

/// <summary>
/// Per oluşturma sonucu.
/// </summary>
public class MeldFormationResult
{
    /// <summary>Başarılı mı?</summary>
    public bool Success { get; }

    /// <summary>Oluşturulan perler.</summary>
    public List<Meld> Melds { get; }

    private MeldFormationResult(bool success, List<Meld>? melds = null)
    {
        Success = success;
        Melds = melds ?? new List<Meld>();
    }

    public static MeldFormationResult Successful(List<Meld> melds) => new(true, melds);
    public static MeldFormationResult Failed() => new(false);
}
