using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.ValueObjects;

namespace OkeyGame.Domain.Services;

/// <summary>
/// Okey oyunu kural motoru.
/// El değerlendirmesi, kazanma kontrolü ve puanlama işlemlerini yapar.
/// 
/// PERFORMANS:
/// - Backtracking algoritması ile optimum çözüm bulur
/// - Memoization ile tekrarlı hesaplamaları önler
/// - Ortalama durumda O(n!) yerine pruning ile O(n²) civarı
/// </summary>
public class OkeyRuleEngine
{
    #region Sabitler

    /// <summary>Kazanmak için gereken taş sayısı (bitiş taşı dahil).</summary>
    public const int WinningHandSize = 15;

    /// <summary>Per yapıldıktan sonra kalan taş sayısı.</summary>
    public const int MeldedHandSize = 14;

    /// <summary>Çifte için gereken çift sayısı.</summary>
    public const int RequiredPairCount = 7;

    /// <summary>Minimum per boyutu.</summary>
    public const int MinMeldSize = 3;

    /// <summary>Maksimum group boyutu.</summary>
    public const int MaxGroupSize = 4;

    #endregion

    #region Singleton

    private static readonly Lazy<OkeyRuleEngine> _instance = 
        new(() => new OkeyRuleEngine());

    public static OkeyRuleEngine Instance => _instance.Value;

    private OkeyRuleEngine() { }

    #endregion

    #region Ana Metodlar

    /// <summary>
    /// Elin kazanan el olup olmadığını kontrol eder.
    /// 15 taşlık el için: 14 taş per + 1 bitiş taşı.
    /// </summary>
    public WinningHandResult CheckWinningHand(IReadOnlyList<Tile> hand)
    {
        if (hand == null || hand.Count != WinningHandSize)
        {
            return WinningHandResult.NotWinning("El 15 taş olmalıdır.");
        }

        // Her taşı potansiyel bitiş taşı olarak dene
        var results = new List<WinningHandResult>();

        for (int i = 0; i < hand.Count; i++)
        {
            var discardTile = hand[i];
            var remainingTiles = hand.Where((_, index) => index != i).ToList();

            // Perlerle bitiş kontrolü
            var meldResult = TryFormMelds(remainingTiles);
            if (meldResult.Success)
            {
                var winType = DetermineWinType(discardTile, meldResult.Melds);
                var score = CalculateScore(winType, meldResult.Melds, discardTile);

                results.Add(new WinningHandResult(
                    isWinning: true,
                    winType: winType,
                    melds: meldResult.Melds,
                    discardTile: discardTile,
                    score: score
                ));
            }
        }

        // Çifte bitiş kontrolü
        var pairsResult = CheckPairs(hand);
        if (pairsResult.IsWinning)
        {
            results.Add(pairsResult);
        }

        // En yüksek puanlı sonucu döndür
        if (results.Count > 0)
        {
            return results.OrderByDescending(r => r.Score).First();
        }

        return WinningHandResult.NotWinning("Geçerli bir el bulunamadı.");
    }

    /// <summary>
    /// 14 taşlık elin per oluşturup oluşturmadığını kontrol eder.
    /// </summary>
    public bool CanFormMelds(IReadOnlyList<Tile> hand)
    {
        if (hand == null || hand.Count != MeldedHandSize)
        {
            return false;
        }

        return TryFormMelds(hand.ToList()).Success;
    }

    /// <summary>
    /// Çifte (7 çift) kontrolü yapar.
    /// </summary>
    public WinningHandResult CheckPairs(IReadOnlyList<Tile> hand)
    {
        if (hand == null || hand.Count < MeldedHandSize)
        {
            return WinningHandResult.NotWinning("Yetersiz taş.");
        }

        // 15 taşlık elden en uygun 14 taşı seç
        var tilesToCheck = hand.Count == WinningHandSize 
            ? FindBestPairsCombination(hand) 
            : hand.ToList();

        if (tilesToCheck.Count != MeldedHandSize)
        {
            return WinningHandResult.NotWinning("14 taş gerekli.");
        }

        var pairs = FindPairs(tilesToCheck);

        if (pairs.Count == RequiredPairCount)
        {
            // Bitiş taşını bul
            Tile? discardTile = null;
            if (hand.Count == WinningHandSize)
            {
                var usedTileIds = pairs.SelectMany(p => p).Select(t => t.Id).ToHashSet();
                discardTile = hand.FirstOrDefault(t => !usedTileIds.Contains(t.Id));
            }

            var score = CalculatePairsScore(pairs, discardTile);

            return new WinningHandResult(
                isWinning: true,
                winType: WinType.Pairs,
                pairs: pairs,
                discardTile: discardTile,
                score: score
            );
        }

        return WinningHandResult.NotWinning($"Sadece {pairs.Count} çift bulundu, 7 gerekli.");
    }

    #endregion

    #region Per Oluşturma (Backtracking)

    /// <summary>
    /// Verilen taşlardan geçerli per kombinasyonu bulmaya çalışır.
    /// Backtracking algoritması kullanır.
    /// </summary>
    private MeldFormationResult TryFormMelds(List<Tile> tiles)
    {
        if (tiles.Count == 0)
        {
            return MeldFormationResult.Successful(new List<Meld>());
        }

        if (tiles.Count < MinMeldSize)
        {
            return MeldFormationResult.Failed();
        }

        // Taşları sırala (renk ve değere göre)
        var sortedTiles = tiles
            .OrderBy(t => t.Color)
            .ThenBy(t => t.Value)
            .ToList();

        var result = new List<Meld>();
        var memo = new Dictionary<string, bool>();

        if (BacktrackMelds(sortedTiles, result, memo))
        {
            return MeldFormationResult.Successful(result);
        }

        return MeldFormationResult.Failed();
    }

    /// <summary>
    /// Backtracking ile per kombinasyonu arar.
    /// </summary>
    private bool BacktrackMelds(List<Tile> remaining, List<Meld> melds, Dictionary<string, bool> memo)
    {
        // Tüm taşlar kullanıldı
        if (remaining.Count == 0)
        {
            return true;
        }

        // Yeterli taş kalmadı
        if (remaining.Count < MinMeldSize)
        {
            return false;
        }

        // Memoization kontrolü
        var state = GetStateKey(remaining);
        if (memo.TryGetValue(state, out bool cached))
        {
            return cached;
        }

        // İlk taşı al ve olası perler oluştur
        var firstTile = remaining[0];

        // Run (Sıralı per) dene
        var runCandidates = FindRunCandidates(remaining, firstTile);
        foreach (var runTiles in runCandidates)
        {
            var meld = Meld.CreateRun(runTiles);
            if (meld.IsValid)
            {
                var newRemaining = remaining.Except(runTiles).ToList();
                melds.Add(meld);

                if (BacktrackMelds(newRemaining, melds, memo))
                {
                    memo[state] = true;
                    return true;
                }

                melds.RemoveAt(melds.Count - 1);
            }
        }

        // Group (Düz per) dene
        var groupCandidates = FindGroupCandidates(remaining, firstTile);
        foreach (var groupTiles in groupCandidates)
        {
            var meld = Meld.CreateGroup(groupTiles);
            if (meld.IsValid)
            {
                var newRemaining = remaining.Except(groupTiles).ToList();
                melds.Add(meld);

                if (BacktrackMelds(newRemaining, melds, memo))
                {
                    memo[state] = true;
                    return true;
                }

                melds.RemoveAt(melds.Count - 1);
            }
        }

        memo[state] = false;
        return false;
    }

    /// <summary>
    /// Verilen taş için olası Run (sıralı per) adaylarını bulur.
    /// </summary>
    private List<List<Tile>> FindRunCandidates(List<Tile> tiles, Tile startTile)
    {
        var candidates = new List<List<Tile>>();

        if (startTile.IsOkey || startTile.IsFalseJoker)
        {
            // Okey başlangıç taşı olarak kullanılamaz (diğer perlerde kullanılmalı)
            return candidates;
        }

        var sameColorTiles = tiles
            .Where(t => t.Color == startTile.Color || t.IsOkey || t.IsFalseJoker)
            .ToList();

        var okeys = tiles.Where(t => t.IsOkey || t.IsFalseJoker).ToList();

        // 3'lü, 4'lü, 5'li... run'ları dene
        for (int length = MinMeldSize; length <= Math.Min(13, sameColorTiles.Count); length++)
        {
            var runCombinations = GenerateRunCombinations(sameColorTiles, startTile, length, okeys);
            candidates.AddRange(runCombinations);
        }

        return candidates;
    }

    private List<List<Tile>> GenerateRunCombinations(List<Tile> sameColorTiles, Tile startTile, int length, List<Tile> okeys)
    {
        var result = new List<List<Tile>>();

        // startTile.Value'dan başlayan ardışık sayıları bul
        var sequence = new List<Tile> { startTile };
        var usedOkeys = new List<Tile>();
        int currentValue = startTile.Value;

        for (int i = 1; i < length; i++)
        {
            int nextValue = currentValue + 1;
            
            // Wrap-around: 13'ten sonra 1 (sadece 12-13-1 için)
            if (nextValue > 13)
            {
                if (currentValue == 13 && i == length - 1)
                {
                    nextValue = 1;
                }
                else
                {
                    break;
                }
            }

            // Bu değerde taş var mı?
            var nextTile = sameColorTiles.FirstOrDefault(t => 
                !t.IsOkey && !t.IsFalseJoker && 
                t.Value == nextValue && 
                !sequence.Contains(t));

            if (nextTile != null)
            {
                sequence.Add(nextTile);
                currentValue = nextValue;
            }
            else if (usedOkeys.Count < okeys.Count)
            {
                // Okey kullan
                var okey = okeys.FirstOrDefault(o => !usedOkeys.Contains(o));
                if (okey != null)
                {
                    sequence.Add(okey);
                    usedOkeys.Add(okey);
                    currentValue = nextValue;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        if (sequence.Count == length)
        {
            result.Add(sequence.ToList());
        }

        return result;
    }

    /// <summary>
    /// Verilen taş için olası Group (düz per) adaylarını bulur.
    /// </summary>
    private List<List<Tile>> FindGroupCandidates(List<Tile> tiles, Tile startTile)
    {
        var candidates = new List<List<Tile>>();

        if (startTile.IsOkey || startTile.IsFalseJoker)
        {
            return candidates;
        }

        var sameValueTiles = tiles
            .Where(t => t.Value == startTile.Value && !t.IsOkey && !t.IsFalseJoker)
            .ToList();

        var okeys = tiles.Where(t => t.IsOkey || t.IsFalseJoker).ToList();

        // Farklı renklerden taşları grupla
        var colorGroups = sameValueTiles
            .GroupBy(t => t.Color)
            .ToDictionary(g => g.Key, g => g.First());

        // 3'lü ve 4'lü grupları oluştur
        var colors = colorGroups.Keys.ToList();

        // 3'lü kombinasyonlar
        if (colors.Count >= 3)
        {
            for (int i = 0; i < colors.Count - 2; i++)
            {
                for (int j = i + 1; j < colors.Count - 1; j++)
                {
                    for (int k = j + 1; k < colors.Count; k++)
                    {
                        var group = new List<Tile>
                        {
                            colorGroups[colors[i]],
                            colorGroups[colors[j]],
                            colorGroups[colors[k]]
                        };
                        candidates.Add(group);
                    }
                }
            }
        }

        // 4'lü kombinasyonlar
        if (colors.Count == 4)
        {
            candidates.Add(colors.Select(c => colorGroups[c]).ToList());
        }

        // Okey ile tamamlanmış gruplar
        if (colors.Count >= 2 && okeys.Count > 0)
        {
            // 2 renk + 1 okey = 3'lü
            for (int i = 0; i < colors.Count - 1; i++)
            {
                for (int j = i + 1; j < colors.Count; j++)
                {
                    var group = new List<Tile>
                    {
                        colorGroups[colors[i]],
                        colorGroups[colors[j]],
                        okeys[0]
                    };
                    candidates.Add(group);
                }
            }
        }

        if (colors.Count >= 3 && okeys.Count > 0)
        {
            // 3 renk + 1 okey = 4'lü
            for (int i = 0; i < colors.Count - 2; i++)
            {
                for (int j = i + 1; j < colors.Count - 1; j++)
                {
                    for (int k = j + 1; k < colors.Count; k++)
                    {
                        var group = new List<Tile>
                        {
                            colorGroups[colors[i]],
                            colorGroups[colors[j]],
                            colorGroups[colors[k]],
                            okeys[0]
                        };
                        candidates.Add(group);
                    }
                }
            }
        }

        return candidates;
    }

    private string GetStateKey(List<Tile> tiles)
    {
        return string.Join(",", tiles.Select(t => t.Id).OrderBy(id => id));
    }

    #endregion

    #region Çift Bulma

    /// <summary>
    /// Verilen taşlardaki çiftleri bulur.
    /// </summary>
    public List<List<Tile>> FindPairs(IReadOnlyList<Tile> tiles)
    {
        var pairs = new List<List<Tile>>();
        var used = new HashSet<int>();

        // Normal taşları grupla (renk ve değere göre)
        var groups = tiles
            .Where(t => !t.IsOkey && !t.IsFalseJoker)
            .GroupBy(t => (t.Color, t.Value))
            .ToList();

        foreach (var group in groups)
        {
            var groupTiles = group.ToList();
            
            // Her gruptan çift çıkar
            while (groupTiles.Count >= 2)
            {
                var t1 = groupTiles[0];
                var t2 = groupTiles[1];

                if (!used.Contains(t1.Id) && !used.Contains(t2.Id))
                {
                    pairs.Add(new List<Tile> { t1, t2 });
                    used.Add(t1.Id);
                    used.Add(t2.Id);
                }

                groupTiles.RemoveAt(0);
                if (groupTiles.Count > 0) groupTiles.RemoveAt(0);
            }
        }

        // Okey'lerle çift tamamlama
        var okeys = tiles.Where(t => (t.IsOkey || t.IsFalseJoker) && !used.Contains(t.Id)).ToList();
        var singleTiles = tiles.Where(t => !used.Contains(t.Id) && !t.IsOkey && !t.IsFalseJoker).ToList();

        foreach (var single in singleTiles)
        {
            if (okeys.Count > 0)
            {
                var okey = okeys[0];
                pairs.Add(new List<Tile> { single, okey });
                used.Add(single.Id);
                used.Add(okey.Id);
                okeys.RemoveAt(0);
            }
        }

        // Kalan Okey'lerle çift
        while (okeys.Count >= 2)
        {
            pairs.Add(new List<Tile> { okeys[0], okeys[1] });
            used.Add(okeys[0].Id);
            used.Add(okeys[1].Id);
            okeys.RemoveRange(0, 2);
        }

        return pairs;
    }

    /// <summary>
    /// 15 taşlık elden en iyi çift kombinasyonunu bulur.
    /// </summary>
    private List<Tile> FindBestPairsCombination(IReadOnlyList<Tile> hand)
    {
        // Her taşı çıkararak en çok çift oluşturan kombinasyonu bul
        var best = hand.Take(MeldedHandSize).ToList();
        int bestPairCount = 0;

        for (int i = 0; i < hand.Count; i++)
        {
            var remaining = hand.Where((_, idx) => idx != i).ToList();
            var pairs = FindPairs(remaining);

            if (pairs.Count > bestPairCount)
            {
                bestPairCount = pairs.Count;
                best = remaining;
            }
        }

        return best;
    }

    #endregion

    #region Bitiş Türü ve Puanlama

    /// <summary>
    /// Bitiş türünü belirler.
    /// </summary>
    private WinType DetermineWinType(Tile discardTile, List<Meld> melds)
    {
        if (discardTile.IsOkey)
        {
            return WinType.OkeyDiscard;
        }

        return WinType.Normal;
    }

    /// <summary>
    /// Puanı hesaplar.
    /// </summary>
    public int CalculateScore(WinType winType, List<Meld> melds, Tile? discardTile)
    {
        // Temel puan hesaplama
        int baseScore = winType switch
        {
            WinType.OkeyDiscard => 4,  // Okey atarak bitirme (en yüksek)
            WinType.Pairs => 4,         // Çifte bitirme
            WinType.Normal => 2,        // Normal bitirme
            _ => 0
        };

        // Her per için bonus
        int meldBonus = melds?.Sum(m => m.OkeyCount > 0 ? 0 : 1) ?? 0;

        return baseScore + meldBonus;
    }

    /// <summary>
    /// Çifte bitirme puanını hesaplar.
    /// </summary>
    private int CalculatePairsScore(List<List<Tile>> pairs, Tile? discardTile)
    {
        int baseScore = 4; // Çifte bitirme bonus

        // Okey atarak çifte bitirme
        if (discardTile?.IsOkey == true)
        {
            baseScore += 2;
        }

        return baseScore;
    }

    #endregion

    #region Yardımcı Metodlar

    /// <summary>
    /// Elin potansiyel bitiş şansını yüzde olarak hesaplar.
    /// AI ve ipucu sistemi için kullanılabilir.
    /// </summary>
    public double CalculateWinProbability(IReadOnlyList<Tile> hand)
    {
        if (hand.Count < MeldedHandSize) return 0;

        var tiles = hand.ToList();
        int okeyCount = tiles.Count(t => t.IsOkey || t.IsFalseJoker);
        
        // Her potansiyel bitiş taşı için kontrol
        int nearWinCount = 0;

        // Basit heuristik: Kaç taş değişirse kazanılır?
        for (int i = 0; i < tiles.Count; i++)
        {
            var withoutOne = tiles.Where((_, idx) => idx != i).ToList();
            
            // Bu 14 taş per yapabilir mi?
            var meldResult = TryFormMelds(withoutOne);
            if (meldResult.Success)
            {
                nearWinCount++;
            }
        }

        // Okey sayısına göre bonus
        double okeyBonus = okeyCount * 0.1;

        return Math.Min(1.0, (nearWinCount / 15.0) + okeyBonus);
    }

    /// <summary>
    /// Elden atılabilecek en iyi taşı önerir.
    /// </summary>
    public Tile? SuggestBestDiscard(IReadOnlyList<Tile> hand)
    {
        if (hand.Count != WinningHandSize) return null;

        // Kazanma kontrolü
        var winResult = CheckWinningHand(hand);
        if (winResult.IsWinning)
        {
            return winResult.DiscardTile;
        }

        // En az değerli, en az per potansiyeli olan taşı bul
        var candidates = hand
            .Where(t => !t.IsOkey && !t.IsFalseJoker)
            .Select(t => new
            {
                Tile = t,
                Score = CalculateTileUtility(t, hand)
            })
            .OrderBy(x => x.Score)
            .ToList();

        return candidates.FirstOrDefault()?.Tile;
    }

    private int CalculateTileUtility(Tile tile, IReadOnlyList<Tile> hand)
    {
        int score = 0;

        // Aynı renkten komşu değerler var mı?
        var sameColor = hand.Where(t => t.Color == tile.Color && t.Id != tile.Id).ToList();
        score += sameColor.Count(t => Math.Abs(t.Value - tile.Value) == 1) * 10;

        // Aynı değerden farklı renkler var mı?
        var sameValue = hand.Where(t => t.Value == tile.Value && t.Id != tile.Id).ToList();
        score += sameValue.Count * 8;

        // Yüksek değerli taşlar daha değerli
        score += tile.Value;

        return score;
    }

    #endregion
}
