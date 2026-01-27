using OkeyGame.Domain.Entities;

namespace OkeyGame.Domain.AI;

/// <summary>
/// Bot karar türleri.
/// </summary>
public enum BotDecisionType
{
    /// <summary>Desteden taş çek.</summary>
    DrawFromDeck,

    /// <summary>Discard'dan taş çek.</summary>
    DrawFromDiscard,

    /// <summary>Taş at.</summary>
    DiscardTile,

    /// <summary>Oyunu bitir (kazandım).</summary>
    DeclareWin
}

/// <summary>
/// Bot kararı.
/// </summary>
public class BotDecision
{
    /// <summary>Karar türü.</summary>
    public BotDecisionType Type { get; }

    /// <summary>İlgili taş (discard veya çekme için).</summary>
    public Tile? Tile { get; }

    /// <summary>Kararın güvenilirlik puanı (0-100).</summary>
    public int Confidence { get; }

    /// <summary>Kararın açıklaması (debug için).</summary>
    public string Reasoning { get; }

    /// <summary>Simüle edilecek düşünme süresi (ms).</summary>
    public int ThinkingTimeMs { get; }

    private BotDecision(BotDecisionType type, Tile? tile, int confidence, string reasoning, int thinkingTimeMs)
    {
        Type = type;
        Tile = tile;
        Confidence = confidence;
        Reasoning = reasoning;
        ThinkingTimeMs = thinkingTimeMs;
    }

    #region Factory Methods

    public static BotDecision DrawFromDeck(int confidence, string reasoning, int thinkingTimeMs)
    {
        return new BotDecision(BotDecisionType.DrawFromDeck, null, confidence, reasoning, thinkingTimeMs);
    }

    public static BotDecision DrawFromDiscard(Tile tile, int confidence, string reasoning, int thinkingTimeMs)
    {
        return new BotDecision(BotDecisionType.DrawFromDiscard, tile, confidence, reasoning, thinkingTimeMs);
    }

    public static BotDecision Discard(Tile tile, int confidence, string reasoning, int thinkingTimeMs)
    {
        return new BotDecision(BotDecisionType.DiscardTile, tile, confidence, reasoning, thinkingTimeMs);
    }

    public static BotDecision Win(Tile discardTile, int confidence, string reasoning, int thinkingTimeMs)
    {
        return new BotDecision(BotDecisionType.DeclareWin, discardTile, confidence, reasoning, thinkingTimeMs);
    }

    #endregion

    public override string ToString()
    {
        return $"[{Type}] {Tile?.ToString() ?? "N/A"} (Confidence: {Confidence}%) - {Reasoning}";
    }
}
