using OkeyGame.Domain.Entities;

namespace OkeyGame.Domain.AI;

/// <summary>
/// Bot yöneticisi.
/// Birden fazla botu yönetir ve oda entegrasyonu sağlar.
/// </summary>
public class BotManager
{
    #region Alanlar

    private readonly Dictionary<Guid, OkeyBotAI> _bots = new();
    private readonly object _lock = new();

    #endregion

    #region Özellikler

    /// <summary>Aktif bot sayısı.</summary>
    public int ActiveBotCount => _bots.Count;

    /// <summary>Tüm bot ID'leri.</summary>
    public IEnumerable<Guid> BotIds => _bots.Keys.ToList();

    #endregion

    #region Bot Yönetimi

    /// <summary>
    /// Yeni bir bot oluşturur.
    /// </summary>
    public OkeyBotAI CreateBot(BotDifficulty difficulty, string? name = null)
    {
        var botId = Guid.NewGuid();
        var bot = new OkeyBotAI(difficulty, botId);

        lock (_lock)
        {
            _bots[botId] = bot;
        }

        return bot;
    }

    /// <summary>
    /// Mevcut ID ile bot oluşturur.
    /// </summary>
    public OkeyBotAI CreateBot(Guid botId, BotDifficulty difficulty)
    {
        var bot = new OkeyBotAI(difficulty, botId);

        lock (_lock)
        {
            _bots[botId] = bot;
        }

        return bot;
    }

    /// <summary>
    /// Bot'u döndürür.
    /// </summary>
    public OkeyBotAI? GetBot(Guid botId)
    {
        lock (_lock)
        {
            return _bots.GetValueOrDefault(botId);
        }
    }

    /// <summary>
    /// Bot'u kaldırır.
    /// </summary>
    public bool RemoveBot(Guid botId)
    {
        lock (_lock)
        {
            return _bots.Remove(botId);
        }
    }

    /// <summary>
    /// Verilen ID'nin bot olup olmadığını kontrol eder.
    /// </summary>
    public bool IsBot(Guid playerId)
    {
        lock (_lock)
        {
            return _bots.ContainsKey(playerId);
        }
    }

    /// <summary>
    /// Tüm botları temizler.
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            _bots.Clear();
        }
    }

    #endregion

    #region Oyun Olayları

    /// <summary>
    /// Tüm botlara taş atıldığını bildirir.
    /// </summary>
    public void NotifyDiscard(Tile tile, Guid discardingPlayerId)
    {
        lock (_lock)
        {
            foreach (var bot in _bots.Values)
            {
                if (bot.PlayerId != discardingPlayerId)
                {
                    bot.OnOpponentDiscard(tile, discardingPlayerId);
                }
            }
        }
    }

    /// <summary>
    /// Tüm botlara discard'dan taş çekildiğini bildirir.
    /// </summary>
    public void NotifyPickup(Tile tile, Guid pickingPlayerId)
    {
        lock (_lock)
        {
            foreach (var bot in _bots.Values)
            {
                if (bot.PlayerId != pickingPlayerId)
                {
                    bot.OnOpponentPickup(tile, pickingPlayerId);
                }
            }
        }
    }

    #endregion

    #region Oda Yardımcıları

    /// <summary>
    /// Odaya eksik oyuncu kadar bot ekler.
    /// </summary>
    /// <param name="currentPlayerCount">Mevcut oyuncu sayısı</param>
    /// <param name="maxPlayers">Maksimum oyuncu sayısı (genelde 4)</param>
    /// <param name="difficulty">Bot zorluk seviyesi</param>
    /// <returns>Eklenen bot ID'leri</returns>
    public List<Guid> FillRoomWithBots(int currentPlayerCount, int maxPlayers, BotDifficulty difficulty = BotDifficulty.Normal)
    {
        var addedBots = new List<Guid>();
        int botsNeeded = maxPlayers - currentPlayerCount;

        for (int i = 0; i < botsNeeded; i++)
        {
            var bot = CreateBot(difficulty);
            addedBots.Add(bot.PlayerId);
        }

        return addedBots;
    }

    /// <summary>
    /// Rastgele zorluk seçerek bot ekler (daha gerçekçi).
    /// </summary>
    public List<Guid> FillRoomWithRandomBots(int currentPlayerCount, int maxPlayers)
    {
        var addedBots = new List<Guid>();
        int botsNeeded = maxPlayers - currentPlayerCount;
        var random = new Random();

        for (int i = 0; i < botsNeeded; i++)
        {
            // Ağırlıklı zorluk dağılımı: Easy %10, Normal %50, Hard %30, Expert %10
            var difficulty = random.Next(100) switch
            {
                < 10 => BotDifficulty.Easy,
                < 60 => BotDifficulty.Normal,
                < 90 => BotDifficulty.Hard,
                _ => BotDifficulty.Expert
            };

            var bot = CreateBot(difficulty);
            addedBots.Add(bot.PlayerId);
        }

        return addedBots;
    }

    #endregion
}
