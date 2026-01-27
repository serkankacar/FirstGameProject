using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;

namespace OkeyGame.Domain.AI;

/// <summary>
/// Okey oyunu için akıllı bot yapay zekası.
/// 
/// ADALET GARANTİSİ:
/// - Bot SADECE kendi elini ve açık bilgiyi (discard pile) görür
/// - Rakip elleri, deste sırasını veya sunucu verisini ASLA görmez
/// - Tüm kararlar heuristic fonksiyon ile matematiksel olarak verilir
/// - İnsan benzeri davranış için yapay gecikme eklenir
/// 
/// KULLANIM:
/// 1. Oyun başında: Initialize(hand, indicator)
/// 2. Sıra botta: DecideDrawSource() -> çek -> DecideDiscard(drawnTile)
/// 3. Rakip hamle: OnOpponentDiscard(tile) veya OnOpponentPickup(tile, playerId)
/// </summary>
public class OkeyBotAI
{
    #region Sabitler

    private const int MinThinkingTimeMs = 2000;
    private const int MaxThinkingTimeMs = 5000;
    private const int QuickDecisionTimeMs = 1500;
    private const int WinDecisionTimeMs = 3000;

    #endregion

    #region Alanlar

    private readonly BotDifficulty _difficulty;
    private readonly HeuristicWeights _weights;
    private readonly TileMemory _memory;
    private readonly Random _random;
    private readonly OkeyRuleEngine _ruleEngine;

    private List<Tile> _hand;
    private Guid _botPlayerId;
    private int _turnCount;
    private bool _isInitialized;

    #endregion

    #region Özellikler

    /// <summary>Bot'un ID'si.</summary>
    public Guid PlayerId => _botPlayerId;

    /// <summary>Bot zorluk seviyesi.</summary>
    public BotDifficulty Difficulty => _difficulty;

    /// <summary>Hafıza (debug için).</summary>
    public TileMemory Memory => _memory;

    /// <summary>Şu anki el (debug için).</summary>
    public IReadOnlyList<Tile> Hand => _hand.AsReadOnly();

    #endregion

    #region Constructor

    /// <summary>
    /// Yeni bir bot AI oluşturur.
    /// </summary>
    /// <param name="difficulty">Zorluk seviyesi</param>
    /// <param name="botPlayerId">Bot'un oyuncu ID'si</param>
    /// <param name="seed">Rastgelelik için seed (test için sabit)</param>
    public OkeyBotAI(BotDifficulty difficulty, Guid botPlayerId, int? seed = null)
    {
        _difficulty = difficulty;
        _botPlayerId = botPlayerId;
        _weights = HeuristicWeights.ForDifficulty(difficulty);
        _memory = new TileMemory();
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _ruleEngine = OkeyRuleEngine.Instance;
        _hand = new List<Tile>();
        _turnCount = 0;
        _isInitialized = false;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Bot'u oyun başında başlatır.
    /// </summary>
    /// <param name="hand">Bot'un eli (14 taş)</param>
    /// <param name="indicatorTile">Gösterge taşı</param>
    public void Initialize(IEnumerable<Tile> hand, Tile indicatorTile)
    {
        _hand = hand.ToList();
        _memory.Clear();
        _memory.SetIndicator(indicatorTile);
        _turnCount = 0;
        _isInitialized = true;

        // Kendi elimizdeki taşları hafızaya kaydet
        foreach (var tile in _hand)
        {
            _memory.RecordSeenTile(tile);
        }
    }

    #endregion

    #region Karar Verme - Çekme

    /// <summary>
    /// Desteden mi yoksa discard'dan mı çekeceğine karar verir.
    /// </summary>
    /// <param name="lastDiscardedTile">Son atılan taş (varsa)</param>
    /// <returns>Çekme kararı</returns>
    public BotDecision DecideDrawSource(Tile? lastDiscardedTile)
    {
        EnsureInitialized();

        int thinkingTime = GenerateThinkingTime();

        // Discard boşsa veya yoksa, desteden çek
        if (lastDiscardedTile == null)
        {
            return BotDecision.DrawFromDeck(100, "Discard boş, desteden çekiyorum.", thinkingTime);
        }

        // Discard'daki taşın değerini hesapla
        var discardValue = EvaluateTileForHand(lastDiscardedTile);

        // Zorluk seviyesine göre karar eşiği
        int pickupThreshold = _difficulty switch
        {
            BotDifficulty.Easy => 8,      // Sadece çok iyi taşları al
            BotDifficulty.Normal => 6,
            BotDifficulty.Hard => 5,
            BotDifficulty.Expert => 4,    // Fırsat varsa al
            _ => 6
        };

        if (discardValue.TotalScore >= pickupThreshold)
        {
            int confidence = Math.Min(100, discardValue.TotalScore * 10);
            return BotDecision.DrawFromDiscard(
                lastDiscardedTile, 
                confidence,
                $"Discard'dan alıyorum: {discardValue.Explanation}",
                thinkingTime
            );
        }

        // Desteden çek
        return BotDecision.DrawFromDeck(
            80,
            $"Discard ({lastDiscardedTile}) işime yaramıyor, desteden çekiyorum.",
            thinkingTime
        );
    }

    #endregion

    #region Karar Verme - Atma

    /// <summary>
    /// Hangi taşı atacağına karar verir.
    /// Çekilen taş ile birlikte el 15 taş olmalı.
    /// </summary>
    /// <param name="drawnTile">Çekilen taş</param>
    /// <returns>Atma kararı</returns>
    public BotDecision DecideDiscard(Tile drawnTile)
    {
        EnsureInitialized();

        // Çekilen taşı ele ekle
        _hand.Add(drawnTile);
        _memory.RecordSeenTile(drawnTile);
        _turnCount++;

        int thinkingTime = GenerateThinkingTime();

        // Önce kazanma kontrolü
        var winCheck = _ruleEngine.CheckWinningHand(_hand);
        if (winCheck.IsWinning && winCheck.DiscardTile != null)
        {
            return BotDecision.Win(
                winCheck.DiscardTile,
                100,
                $"KAZANDIM! {winCheck.WinType} - {winCheck.Score} puan",
                WinDecisionTimeMs
            );
        }

        // Tüm taşları puanla
        var scores = _hand.Select(EvaluateTileForHand).ToList();

        // En düşük puanlı taşı bul
        var worstTile = scores.OrderBy(s => s.TotalScore).First();

        // Okey atma kontrolü (Okey'i atmak çok kötü bir hareket)
        if (_memory.IsOkeyTile(worstTile.Tile))
        {
            // Okey dışında en düşük puanlıyı bul
            var nonOkeyWorst = scores
                .Where(s => !_memory.IsOkeyTile(s.Tile))
                .OrderBy(s => s.TotalScore)
                .FirstOrDefault();

            if (nonOkeyWorst != null)
            {
                worstTile = nonOkeyWorst;
            }
            // Eğer tüm taşlar Okey ise, mecburen at (bu neredeyse imkansız)
        }

        // Elden çıkar
        _hand.Remove(worstTile.Tile);

        int confidence = 100 - worstTile.TotalScore * 5;

        return BotDecision.Discard(
            worstTile.Tile,
            Math.Max(10, confidence),
            $"En az yararlı taş: {worstTile}",
            thinkingTime
        );
    }

    #endregion

    #region Heuristic Fonksiyon

    /// <summary>
    /// Bir taşın el için değerini hesaplar.
    /// Yüksek puan = değerli, düşük puan = atılabilir.
    /// </summary>
    private TileScore EvaluateTileForHand(Tile tile)
    {
        var score = new TileScore(tile);
        var explanations = new List<string>();

        // 1. Okey kontrolü
        if (_memory.IsOkeyTile(tile) || tile.IsOkey)
        {
            score.AddScore("Okey", _weights.OkeyTile);
            explanations.Add("Okey taşı");
        }

        // 2. Sahte Okey kontrolü
        if (tile.IsFalseJoker)
        {
            score.AddScore("SahteOkey", _weights.FalseJoker);
            explanations.Add("Sahte Okey");
        }

        // 3. Run potansiyeli (aynı renk komşular)
        int runScore = CalculateRunPotential(tile);
        if (runScore > 0)
        {
            score.AddScore("RunPotansiyeli", runScore);
            explanations.Add($"Sıralı per potansiyeli (+{runScore})");
        }

        // 4. Group potansiyeli (aynı değer farklı renkler)
        int groupScore = CalculateGroupPotential(tile);
        if (groupScore > 0)
        {
            score.AddScore("GroupPotansiyeli", groupScore);
            explanations.Add($"Düz per potansiyeli (+{groupScore})");
        }

        // 5. Yalnızlık kontrolü
        if (runScore == 0 && groupScore == 0 && !tile.IsOkey && !tile.IsFalseJoker)
        {
            score.AddScore("Yalnız", _weights.IsolatedTile);
            explanations.Add("Yalnız taş");
        }

        // 6. Hafıza bazlı ayarlamalar
        ApplyMemoryAdjustments(tile, score, explanations);

        // 7. Değer bazlı ayarlamalar
        if (tile.Value >= 10)
        {
            score.AddScore("YüksekDeğer", _weights.HighValueBonus);
        }

        // 8. Geç oyun çarpanı
        if (_turnCount > 10)
        {
            int lateBonus = (score.TotalScore * _weights.LateGameRiskMultiplier) / 10;
            score.AddScore("GeçOyun", lateBonus);
        }

        score.Explanation = string.Join(", ", explanations);
        return score;
    }

    /// <summary>
    /// Sıralı per (Run) potansiyelini hesaplar.
    /// </summary>
    private int CalculateRunPotential(Tile tile)
    {
        if (tile.IsOkey || tile.IsFalseJoker) return 0;

        int score = 0;
        var sameColorTiles = _hand
            .Where(t => t.Color == tile.Color && t.Id != tile.Id && !t.IsOkey && !t.IsFalseJoker)
            .Select(t => t.Value)
            .ToHashSet();

        // Yan yana komşu kontrolü
        bool hasLeft = sameColorTiles.Contains(tile.Value - 1);
        bool hasRight = sameColorTiles.Contains(tile.Value + 1);

        if (hasLeft && hasRight)
        {
            // Ortada: 3'lü per parçası
            score += _weights.CompletedMeld;
        }
        else if (hasLeft || hasRight)
        {
            // Bir komşu var
            score += _weights.AdjacentPair;

            // İkinci komşu var mı? (4-5 ve 6 da varsa)
            if (hasLeft && sameColorTiles.Contains(tile.Value - 2))
                score += _weights.AdjacentPair / 2;
            if (hasRight && sameColorTiles.Contains(tile.Value + 2))
                score += _weights.AdjacentPair / 2;
        }

        // Boşluklu komşu (5 ve 7 varsa, 6 lazım)
        bool hasGapLeft = sameColorTiles.Contains(tile.Value - 2) && !hasLeft;
        bool hasGapRight = sameColorTiles.Contains(tile.Value + 2) && !hasRight;

        if (hasGapLeft || hasGapRight)
        {
            score += _weights.GapPair;

            // Eksik taşın bulunabilirlik olasılığını kontrol et
            int missingValue = hasGapLeft ? tile.Value - 1 : tile.Value + 1;
            double availability = _memory.GetAvailabilityProbability(tile.Color, missingValue);
            
            if (availability < 0.5)
            {
                score += _weights.MissingTileSeen;
            }
        }

        return score;
    }

    /// <summary>
    /// Düz per (Group) potansiyelini hesaplar.
    /// </summary>
    private int CalculateGroupPotential(Tile tile)
    {
        if (tile.IsOkey || tile.IsFalseJoker) return 0;

        var sameValueTiles = _hand
            .Where(t => t.Value == tile.Value && t.Id != tile.Id && !t.IsOkey && !t.IsFalseJoker)
            .Select(t => t.Color)
            .Distinct()
            .ToList();

        int colorCount = sameValueTiles.Count + 1; // Kendisi dahil

        if (colorCount >= 3)
        {
            // Group tamamlandı
            return _weights.CompletedMeld + (colorCount - 3) * 2;
        }
        else if (colorCount == 2)
        {
            // 2 renk var, 1 lazım
            double availability = _memory.GetGroupCompletionProbability(tile.Value, sameValueTiles.Append(tile.Color));
            int score = _weights.SameValuePair;

            if (availability < 0.5)
            {
                score += _weights.MissingTileSeen;
            }

            return score;
        }

        return 0;
    }

    /// <summary>
    /// Hafıza bazlı puan ayarlamaları uygular.
    /// </summary>
    private void ApplyMemoryAdjustments(Tile tile, TileScore score, List<string> explanations)
    {
        if (tile.IsOkey || tile.IsFalseJoker) return;

        // Komşu taşların görülme durumu
        var neighbors = new[]
        {
            (tile.Color, tile.Value - 1),
            (tile.Color, tile.Value + 1)
        };

        foreach (var (color, value) in neighbors)
        {
            if (value < 1 || value > 13) continue;

            int seenCount = _memory.GetSeenCount(color, value);
            if (seenCount >= 2)
            {
                score.AddScore("KomşuTükendi", _weights.BothCopiesSeen);
                explanations.Add($"Komşu {color} {value} tükendi");
            }
            else if (seenCount == 1)
            {
                score.AddScore("KomşuGörüldü", _weights.MissingTileSeen / 2);
            }
        }
    }

    #endregion

    #region Rakip Hamle Takibi

    /// <summary>
    /// Bir rakibin taş attığını kaydeder.
    /// </summary>
    public void OnOpponentDiscard(Tile tile, Guid opponentId)
    {
        _memory.RecordDiscard(tile, opponentId);
    }

    /// <summary>
    /// Bir rakibin discard'dan taş çektiğini kaydeder.
    /// </summary>
    public void OnOpponentPickup(Tile tile, Guid opponentId)
    {
        _memory.RecordPickupFromDiscard(tile, opponentId);
    }

    /// <summary>
    /// Desteden taş çekildiğini kaydeder (taşın ne olduğu bilinmez).
    /// </summary>
    public void OnDeckDraw()
    {
        // Bot desteden çekilen taşı görmez (adil oyun)
        // Sadece deste azaldığını takip edebilir
    }

    #endregion

    #region Yardımcı Metodlar

    /// <summary>
    /// İnsan benzeri düşünme süresi üretir.
    /// </summary>
    private int GenerateThinkingTime()
    {
        // Zorluk seviyesine göre düşünme süresi
        int baseTime = _difficulty switch
        {
            BotDifficulty.Easy => MinThinkingTimeMs,
            BotDifficulty.Normal => (MinThinkingTimeMs + MaxThinkingTimeMs) / 2,
            BotDifficulty.Hard => MaxThinkingTimeMs - 500,
            BotDifficulty.Expert => MaxThinkingTimeMs,
            _ => MinThinkingTimeMs
        };

        // ±500ms rastgele varyasyon
        int variation = _random.Next(-500, 500);
        
        return Math.Max(QuickDecisionTimeMs, baseTime + variation);
    }

    /// <summary>
    /// Bot'un başlatıldığını kontrol eder.
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Bot başlatılmadı. Önce Initialize() çağırın.");
        }
    }

    /// <summary>
    /// Debug için el durumunu döndürür.
    /// </summary>
    public string GetHandSummary()
    {
        var scores = _hand.Select(EvaluateTileForHand).OrderByDescending(s => s.TotalScore).ToList();
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Bot El Özeti ({_difficulty}) ===");
        sb.AppendLine($"Taş sayısı: {_hand.Count}, Tur: {_turnCount}");
        sb.AppendLine("Taşlar (değere göre sıralı):");
        
        foreach (var score in scores)
        {
            string okeyMark = _memory.IsOkeyTile(score.Tile) ? " [OKEY]" : "";
            sb.AppendLine($"  {score}{okeyMark}");
        }

        return sb.ToString();
    }

    #endregion
}
