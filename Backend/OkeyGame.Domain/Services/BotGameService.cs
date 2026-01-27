using OkeyGame.Domain.AI;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.Services;

/// <summary>
/// Bot oyun servisi.
/// Bot hamlelerini yönetir ve gecikme uygular.
/// </summary>
public class BotGameService
{
    #region Alanlar

    private readonly BotManager _botManager;
    private readonly OkeyRuleEngine _ruleEngine;

    #endregion

    #region Constructor

    public BotGameService(BotManager botManager)
    {
        _botManager = botManager;
        _ruleEngine = OkeyRuleEngine.Instance;
    }

    #endregion

    #region Bot Hamle İşleme

    /// <summary>
    /// Bot'un sırasını oynatır.
    /// Async olarak gecikme ile çalışır.
    /// </summary>
    /// <param name="botId">Bot ID</param>
    /// <param name="lastDiscardedTile">Son atılan taş</param>
    /// <param name="drawTileFromDeck">Desteden taş çekme fonksiyonu</param>
    /// <param name="onDecision">Karar callback'i</param>
    public async Task<BotTurnResult> PlayBotTurnAsync(
        Guid botId,
        Tile? lastDiscardedTile,
        Func<Tile> drawTileFromDeck,
        CancellationToken cancellationToken = default)
    {
        var bot = _botManager.GetBot(botId);
        if (bot == null)
        {
            throw new InvalidOperationException($"Bot bulunamadı: {botId}");
        }

        // 1. Çekme kararı
        var drawDecision = bot.DecideDrawSource(lastDiscardedTile);

        // İnsan benzeri gecikme
        await Task.Delay(drawDecision.ThinkingTimeMs, cancellationToken);

        Tile drawnTile;
        bool drewFromDiscard;

        if (drawDecision.Type == BotDecisionType.DrawFromDiscard && lastDiscardedTile != null)
        {
            drawnTile = lastDiscardedTile;
            drewFromDiscard = true;
        }
        else
        {
            drawnTile = drawTileFromDeck();
            drewFromDiscard = false;
        }

        // 2. Atma kararı
        var discardDecision = bot.DecideDiscard(drawnTile);

        // İnsan benzeri gecikme
        await Task.Delay(discardDecision.ThinkingTimeMs, cancellationToken);

        // 3. Sonucu döndür
        return new BotTurnResult
        {
            BotId = botId,
            DrewFromDiscard = drewFromDiscard,
            DrawnTile = drawnTile,
            DiscardedTile = discardDecision.Tile!,
            IsWinning = discardDecision.Type == BotDecisionType.DeclareWin,
            DrawDecision = drawDecision,
            DiscardDecision = discardDecision
        };
    }

    /// <summary>
    /// Bot'u oyun için başlatır.
    /// </summary>
    public void InitializeBot(Guid botId, IEnumerable<Tile> hand, Tile indicatorTile)
    {
        var bot = _botManager.GetBot(botId);
        if (bot == null)
        {
            throw new InvalidOperationException($"Bot bulunamadı: {botId}");
        }

        bot.Initialize(hand, indicatorTile);
    }

    /// <summary>
    /// Tüm botları oyun için başlatır.
    /// </summary>
    public void InitializeAllBots(Dictionary<Guid, List<Tile>> botHands, Tile indicatorTile)
    {
        foreach (var (botId, hand) in botHands)
        {
            var bot = _botManager.GetBot(botId);
            if (bot != null)
            {
                bot.Initialize(hand, indicatorTile);
            }
        }
    }

    #endregion
}

/// <summary>
/// Bot tur sonucu.
/// </summary>
public class BotTurnResult
{
    /// <summary>Bot ID.</summary>
    public Guid BotId { get; set; }

    /// <summary>Discard'dan mı çekildi?</summary>
    public bool DrewFromDiscard { get; set; }

    /// <summary>Çekilen taş.</summary>
    public Tile DrawnTile { get; set; } = null!;

    /// <summary>Atılan taş.</summary>
    public Tile DiscardedTile { get; set; } = null!;

    /// <summary>Bot kazandı mı?</summary>
    public bool IsWinning { get; set; }

    /// <summary>Çekme kararı detayı.</summary>
    public BotDecision DrawDecision { get; set; } = null!;

    /// <summary>Atma kararı detayı.</summary>
    public BotDecision DiscardDecision { get; set; } = null!;

    public override string ToString()
    {
        string action = DrewFromDiscard ? "Discard'dan çekti" : "Desteden çekti";
        string result = IsWinning ? " -> KAZANDI!" : "";
        return $"Bot {BotId}: {action} ({DrawnTile}), Attı: {DiscardedTile}{result}";
    }
}
