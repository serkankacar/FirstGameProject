using OkeyGame.Domain.AI;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;

namespace OkeyGame.Domain.StateMachine;

/// <summary>
/// Otomatik oynatma servisi.
/// 
/// KULLANIM SENARYOLARI:
/// 1. Oyuncu zaman aşımına uğradığında
/// 2. Oyuncu bağlantısı koptuğunda
/// 3. AFK (Away From Keyboard) durumunda
/// 
/// BOT MANTIĞI:
/// - OkeyBotAI'ın Easy modunu kullanır (hızlı ve basit kararlar)
/// - Oyuncunun elini analiz eder
/// - En mantıklı taşı seçer (en az değerli/bağlantısız taş)
/// 
/// ADALET:
/// - Bot sadece oyuncunun elini görür (hile yok)
/// - Desteden rastgele taş çeker
/// - Karar verme süresi minimumda tutulur
/// </summary>
public sealed class AutoPlayService
{
    #region Sabitler

    /// <summary>
    /// Auto-play için yapay gecikme (ms).
    /// </summary>
    private const int AutoPlayDelayMs = 500;

    /// <summary>
    /// Auto-play için varsayılan zorluk.
    /// Easy: Hızlı, basit kararlar.
    /// </summary>
    private const BotDifficulty DefaultDifficulty = BotDifficulty.Easy;

    #endregion

    #region Singleton

    private static readonly Lazy<AutoPlayService> _instance = new(() => new AutoPlayService());

    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static AutoPlayService Instance => _instance.Value;

    private AutoPlayService() 
    {
        _ruleEngine = OkeyRuleEngine.Instance;
    }

    #endregion

    #region Alanlar

    private readonly OkeyRuleEngine _ruleEngine;

    #endregion

    #region Auto-Play İşlemleri

    /// <summary>
    /// Zaman aşımı durumunda otomatik hamle yapar.
    /// </summary>
    /// <param name="hand">Oyuncunun eli</param>
    /// <param name="indicatorTile">Gösterge taşı</param>
    /// <param name="lastDiscardedTile">Son atılan taş (discard'dan çekmek için)</param>
    /// <param name="drawTileFromDeck">Desteden taş çekme fonksiyonu</param>
    /// <param name="turnContext">Mevcut tur context'i</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Otomatik hamle sonucu</returns>
    public async Task<AutoPlayResult> PlayTurnAsync(
        List<Tile> hand,
        Tile indicatorTile,
        Tile? lastDiscardedTile,
        Func<Tile> drawTileFromDeck,
        TurnContext turnContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(indicatorTile);
        ArgumentNullException.ThrowIfNull(turnContext);

        // Boş el kontrolü
        if (hand.Count == 0)
        {
            return new AutoPlayResult
            {
                Success = false,
                DrewFromDiscard = false,
                DrawnTile = null,
                DiscardedTile = null,
                IsWinning = false,
                Reason = AutoPlayReason.Error,
                Message = "El boş, otomatik hamle yapılamaz."
            };
        }

        try
        {
            // Yapay gecikme (çok hızlı olmasın)
            await Task.Delay(AutoPlayDelayMs, cancellationToken);

            // Bot AI oluştur (Easy mod)
            var botAI = new OkeyBotAI(DefaultDifficulty, turnContext.CurrentPlayerId, seed: null);
            botAI.Initialize(hand, indicatorTile);

            Tile drawnTile;
            bool drewFromDiscard;

            // 1. Taş çekme kararı
            if (!turnContext.HasDrawnTile)
            {
                var drawDecision = botAI.DecideDrawSource(lastDiscardedTile);

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

                hand.Add(drawnTile);
            }
            else
            {
                // Zaten taş çekilmişse (ara bağlantı kopması)
                drawnTile = null!;
                drewFromDiscard = false;
            }

            // 2. Taş atma kararı
            var discardDecision = botAI.DecideDiscard(drawnTile);

            // Kazanma kontrolü
            bool isWinning = discardDecision.Type == BotDecisionType.DeclareWin;

            // Atılacak taşı bul
            Tile tileToDiscard = discardDecision.Tile ?? SelectLeastValuableTile(hand, indicatorTile);

            return new AutoPlayResult
            {
                Success = true,
                DrewFromDiscard = drewFromDiscard,
                DrawnTile = drawnTile,
                DiscardedTile = tileToDiscard,
                IsWinning = isWinning,
                Reason = AutoPlayReason.Timeout,
                Message = "Süre doldu, otomatik hamle yapıldı."
            };
        }
        catch (OperationCanceledException)
        {
            return new AutoPlayResult
            {
                Success = false,
                Reason = AutoPlayReason.Cancelled,
                Message = "İşlem iptal edildi."
            };
        }
        catch (Exception ex)
        {
            // Hata durumunda en basit hamleyi yap
            return HandleAutoPlayError(hand, indicatorTile, ex);
        }
    }

    /// <summary>
    /// Sadece taş atma işlemi yapar (taş zaten çekilmişse).
    /// El 15 taş olmalı (14 + 1 çekilen).
    /// </summary>
    public async Task<AutoPlayResult> DiscardOnlyAsync(
        List<Tile> hand,
        Tile indicatorTile,
        TurnContext turnContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(indicatorTile);
        ArgumentNullException.ThrowIfNull(turnContext);

        if (hand.Count == 0)
        {
            return new AutoPlayResult
            {
                Success = false,
                DrewFromDiscard = false,
                DrawnTile = null,
                DiscardedTile = null,
                IsWinning = false,
                Reason = AutoPlayReason.Error,
                Message = "El boş, taş atılamaz."
            };
        }

        await Task.Delay(AutoPlayDelayMs, cancellationToken);

        // Önce kazanma kontrolü
        var ruleEngine = OkeyRuleEngine.Instance;
        var winCheck = ruleEngine.CheckWinningHand(hand);
        
        if (winCheck.IsWinning && winCheck.DiscardTile != null)
        {
            return new AutoPlayResult
            {
                Success = true,
                DrewFromDiscard = turnContext.DrewFromDiscard,
                DrawnTile = null,
                DiscardedTile = winCheck.DiscardTile,
                IsWinning = true,
                Reason = AutoPlayReason.Timeout,
                Message = "Süre doldu, kazanan el tespit edildi!"
            };
        }

        // En değersiz taşı at
        Tile tileToDiscard = SelectLeastValuableTile(hand, indicatorTile);

        return new AutoPlayResult
        {
            Success = true,
            DrewFromDiscard = turnContext.DrewFromDiscard,
            DrawnTile = null,
            DiscardedTile = tileToDiscard,
            IsWinning = false,
            Reason = AutoPlayReason.Timeout,
            Message = "Süre doldu, otomatik taş atıldı."
        };
    }

    #endregion

    #region Yardımcı Metodlar

    /// <summary>
    /// Hata durumunda en basit hamleyi yapar.
    /// </summary>
    private AutoPlayResult HandleAutoPlayError(List<Tile> hand, Tile indicatorTile, Exception ex)
    {
        // En değersiz taşı at
        var tileToDiscard = SelectLeastValuableTile(hand, indicatorTile);

        return new AutoPlayResult
        {
            Success = true,
            DrewFromDiscard = false,
            DrawnTile = null,
            DiscardedTile = tileToDiscard,
            IsWinning = false,
            Reason = AutoPlayReason.Error,
            Message = $"Hata nedeniyle basit hamle yapıldı: {ex.Message}"
        };
    }

    /// <summary>
    /// En az değerli taşı seçer.
    /// 
    /// Öncelik sırası:
    /// 1. İzole taşlar (hiçbir perle bağlantısı yok)
    /// 2. Düşük numaralı taşlar
    /// 3. Okey olmayan taşlar
    /// </summary>
    private Tile SelectLeastValuableTile(List<Tile> hand, Tile indicatorTile)
    {
        if (hand.Count == 0)
        {
            throw new InvalidOperationException("El boş, taş atılamaz.");
        }

        // Okey taşlarını ayır
        var okeyValue = GetOkeyValue(indicatorTile);
        var okeyColor = indicatorTile.Color;

        // İzole taşları bul
        var isolatedTiles = hand
            .Where(t => !IsOkeyTile(t, okeyValue, okeyColor) && !t.IsFalseJoker)
            .Where(t => !HasAdjacentTile(t, hand))
            .OrderBy(t => t.Value)
            .ToList();

        if (isolatedTiles.Count > 0)
        {
            return isolatedTiles.First();
        }

        // İzole taş yoksa, en düşük değerli okey olmayan taşı seç
        var nonOkeyTiles = hand
            .Where(t => !IsOkeyTile(t, okeyValue, okeyColor) && !t.IsFalseJoker)
            .OrderBy(t => t.Value)
            .ToList();

        if (nonOkeyTiles.Count > 0)
        {
            return nonOkeyTiles.First();
        }

        // Hepsi okey veya sahte okey ise, ilk taşı at
        return hand.First();
    }

    /// <summary>
    /// Okey değerini hesaplar.
    /// Göstergenin bir üstü okey'dir (13'ten sonra 1).
    /// </summary>
    private int GetOkeyValue(Tile indicator)
    {
        return indicator.Value == 13 ? 1 : indicator.Value + 1;
    }

    /// <summary>
    /// Taşın okey olup olmadığını kontrol eder.
    /// </summary>
    private bool IsOkeyTile(Tile tile, int okeyValue, TileColor okeyColor)
    {
        return tile.Value == okeyValue && tile.Color == okeyColor;
    }

    /// <summary>
    /// Taşın komşusu (ardışık veya aynı numara) olup olmadığını kontrol eder.
    /// </summary>
    private bool HasAdjacentTile(Tile tile, List<Tile> hand)
    {
        // Aynı renkte ardışık kontrol (per olasılığı)
        bool hasRun = hand.Any(t => 
            t.Id != tile.Id && 
            t.Color == tile.Color && 
            Math.Abs(t.Value - tile.Value) <= 1);

        // Aynı numarada farklı renk kontrol (grup olasılığı)
        bool hasGroup = hand
            .Where(t => t.Id != tile.Id && t.Value == tile.Value)
            .Select(t => t.Color)
            .Distinct()
            .Count() >= 2; // En az 2 farklı renk (bu taş dahil 3)

        return hasRun || hasGroup;
    }

    #endregion
}

/// <summary>
/// Otomatik oynatma sonucu.
/// </summary>
public sealed record AutoPlayResult
{
    /// <summary>İşlem başarılı mı?</summary>
    public bool Success { get; init; }

    /// <summary>Discard'dan mı çekildi?</summary>
    public bool DrewFromDiscard { get; init; }

    /// <summary>Çekilen taş (varsa).</summary>
    public Tile? DrawnTile { get; init; }

    /// <summary>Atılan taş.</summary>
    public Tile? DiscardedTile { get; init; }

    /// <summary>Oyuncu kazandı mı?</summary>
    public bool IsWinning { get; init; }

    /// <summary>Otomatik oynatma nedeni.</summary>
    public AutoPlayReason Reason { get; init; }

    /// <summary>Açıklama mesajı.</summary>
    public string? Message { get; init; }

    /// <summary>Zaman damgası.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Otomatik oynatma nedeni.
/// </summary>
public enum AutoPlayReason
{
    /// <summary>Süre doldu.</summary>
    Timeout,

    /// <summary>Bağlantı koptu.</summary>
    Disconnected,

    /// <summary>Oyuncu AFK.</summary>
    AFK,

    /// <summary>İşlem iptal edildi.</summary>
    Cancelled,

    /// <summary>Hata oluştu.</summary>
    Error
}
