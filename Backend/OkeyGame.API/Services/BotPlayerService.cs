using Microsoft.AspNetCore.SignalR;
using OkeyGame.API.Hubs;
using OkeyGame.API.Models;
using OkeyGame.Application.DTOs;
using OkeyGame.Domain.AI;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using System.Text.Json;

namespace OkeyGame.API.Services;

/// <summary>
/// Bot oyuncuları yöneten servis.
/// Eksik oyuncu olduğunda otomatik bot ekler ve bot hamlelerini yönetir.
/// </summary>
public interface IBotPlayerService
{
    /// <summary>
    /// Odaya bot oyuncular ekler.
    /// </summary>
    Task<List<Guid>> AddBotsToRoomAsync(Guid roomId, int botsNeeded, BotDifficulty difficulty = BotDifficulty.Normal);

    /// <summary>
    /// Odayı 4 oyuncuya tamamlamak için gerekli botları ekler.
    /// </summary>
    Task<List<Guid>> FillRoomWithBotsAsync(Guid roomId, BotDifficulty difficulty = BotDifficulty.Normal);

    /// <summary>
    /// Bot oyuncuyu odadan çıkarır.
    /// </summary>
    Task<bool> RemoveBotFromRoomAsync(Guid roomId, Guid botId);

    /// <summary>
    /// Belirtilen ID'nin bot olup olmadığını kontrol eder.
    /// </summary>
    bool IsBot(Guid playerId);

    /// <summary>
    /// Bot için oyun başlangıcında el ve gösterge taşını ayarlar.
    /// </summary>
    Task InitializeBotHandAsync(Guid roomId, Guid botId, List<Tile> hand, Tile indicatorTile);

    /// <summary>
    /// Bot'un hamle yapmasını sağlar.
    /// </summary>
    Task<BotMoveResult> ProcessBotTurnAsync(Guid roomId, Guid botId, Tile? lastDiscardedTile);

    /// <summary>
    /// Sıra botta ise otomatik hamle yapar.
    /// </summary>
    Task CheckAndProcessBotTurnAsync(Guid roomId);
}

/// <summary>
/// Bot hamle sonucu.
/// </summary>
public class BotMoveResult
{
    public bool Success { get; set; }
    public bool DrewFromDiscard { get; set; }
    public int? DrawnTileId { get; set; }
    public int? DiscardedTileId { get; set; }
    public bool IsWinningMove { get; set; }
    public string? Message { get; set; }
    public int ThinkingTimeMs { get; set; }
}

/// <summary>
/// Bot oyuncu servisi implementasyonu.
/// </summary>
public class BotPlayerService : IBotPlayerService
{
    #region Alanlar

    private readonly IGameStateService _stateService;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<BotPlayerService> _logger;
    private readonly BotManager _botManager;
    private readonly JsonSerializerOptions _jsonOptions;

    // Bot isimleri havuzu
    private static readonly string[] BotNames = new[]
    {
        "Ahmet Bot", "Mehmet Bot", "Ayşe Bot", "Fatma Bot",
        "Ali Bot", "Veli Bot", "Zeynep Bot", "Elif Bot",
        "Mustafa Bot", "Hüseyin Bot", "Hatice Bot", "Emine Bot"
    };

    private static readonly Random _random = new();

    #endregion

    #region Constructor

    public BotPlayerService(
        IGameStateService stateService,
        IHubContext<GameHub> hubContext,
        ILogger<BotPlayerService> logger)
    {
        _stateService = stateService;
        _hubContext = hubContext;
        _logger = logger;
        _botManager = new BotManager();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public async Task<List<Guid>> AddBotsToRoomAsync(Guid roomId, int botsNeeded, BotDifficulty difficulty = BotDifficulty.Normal)
    {
        var addedBots = new List<Guid>();

        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            _logger.LogWarning("Bot eklenemedi: Lock alınamadı. Oda {RoomId}", roomId);
            return addedBots;
        }

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            _logger.LogWarning("Bot eklenemedi: Oda bulunamadı. Oda {RoomId}", roomId);
            return addedBots;
        }

        for (int i = 0; i < botsNeeded && state.Players.Count < 4; i++)
        {
            // Bot oluştur
            var bot = _botManager.CreateBot(difficulty);
            var botName = GetRandomBotName();
            var position = GetNextAvailablePosition(state);

            // Oyuncu olarak ekle
            state.Players[bot.PlayerId] = new PlayerState
            {
                PlayerId = bot.PlayerId,
                DisplayName = botName,
                Position = position,
                IsConnected = true, // Bot her zaman bağlı
                LastConnectedAt = DateTime.UtcNow,
                HandTileIds = new List<int>()
            };

            addedBots.Add(bot.PlayerId);

            _logger.LogInformation(
                "Bot eklendi: {BotId} ({BotName}), Pozisyon: {Position}, Zorluk: {Difficulty}",
                bot.PlayerId, botName, position, difficulty);
        }

        await _stateService.SaveRoomStateAsync(state);

        // Diğer oyunculara bildiri gönder
        foreach (var botId in addedBots)
        {
            var botPlayer = state.Players[botId];
            await _hubContext.Clients.Group($"room:{roomId}")
                .SendAsync("OnPlayerJoined", new
                {
                    PlayerId = botId,
                    PlayerName = botPlayer.DisplayName,
                    Position = (int)botPlayer.Position,
                    IsBot = true,
                    TotalPlayers = state.Players.Count
                });
        }

        return addedBots;
    }

    /// <inheritdoc/>
    public async Task<List<Guid>> FillRoomWithBotsAsync(Guid roomId, BotDifficulty difficulty = BotDifficulty.Normal)
    {
        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return new List<Guid>();
        }

        int botsNeeded = 4 - state.Players.Count;
        if (botsNeeded <= 0)
        {
            return new List<Guid>();
        }

        return await AddBotsToRoomAsync(roomId, botsNeeded, difficulty);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveBotFromRoomAsync(Guid roomId, Guid botId)
    {
        if (!_botManager.IsBot(botId))
        {
            return false;
        }

        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null) return false;

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null) return false;

        if (!state.Players.Remove(botId))
        {
            return false;
        }

        _botManager.RemoveBot(botId);
        await _stateService.SaveRoomStateAsync(state);

        // Bildiri gönder
        await _hubContext.Clients.Group($"room:{roomId}")
            .SendAsync("OnPlayerLeft", new
            {
                PlayerId = botId,
                IsBot = true,
                Timestamp = DateTime.UtcNow
            });

        _logger.LogInformation("Bot odadan çıkarıldı: {BotId}, Oda: {RoomId}", botId, roomId);
        return true;
    }

    /// <inheritdoc/>
    public bool IsBot(Guid playerId)
    {
        return _botManager.IsBot(playerId);
    }

    /// <inheritdoc/>
    public async Task InitializeBotHandAsync(Guid roomId, Guid botId, List<Tile> hand, Tile indicatorTile)
    {
        var bot = _botManager.GetBot(botId);
        if (bot == null)
        {
            _logger.LogWarning("Bot bulunamadı: {BotId}", botId);
            return;
        }

        bot.Initialize(hand, indicatorTile);
        _logger.LogDebug("Bot eli başlatıldı: {BotId}, {TileCount} taş", botId, hand.Count);
    }

    /// <inheritdoc/>
    public async Task<BotMoveResult> ProcessBotTurnAsync(Guid roomId, Guid botId, Tile? lastDiscardedTile)
    {
        var bot = _botManager.GetBot(botId);
        if (bot == null)
        {
            return new BotMoveResult { Success = false, Message = "Bot bulunamadı." };
        }

        // 1. Çekme kararı ver
        var drawDecision = bot.DecideDrawSource(lastDiscardedTile);

        // İnsan benzeri düşünme süresi için bekle
        await Task.Delay(drawDecision.ThinkingTimeMs);

        // 2. Taşı çek
        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            return new BotMoveResult { Success = false, Message = "Lock alınamadı." };
        }

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return new BotMoveResult { Success = false, Message = "Oda bulunamadı." };
        }

        var player = state.Players.GetValueOrDefault(botId);
        if (player == null)
        {
            return new BotMoveResult { Success = false, Message = "Bot oyuncu bulunamadı." };
        }

        // Önceki oyuncunun atık yığınını bul
        var currentPos = (int)player.Position;
        var prevPos = (currentPos - 1 + 4) % 4;
        var prevPlayer = state.Players.Values.FirstOrDefault(p => (int)p.Position == prevPos);
        List<int>? targetDiscardPile = null;

        if (prevPlayer != null && state.DiscardPiles.TryGetValue(prevPlayer.PlayerId, out var pile))
        {
            targetDiscardPile = pile;
        }

        bool drewFromDiscard = drawDecision.ShouldDrawFromDiscard;
        int drawnTileId;

        if (drewFromDiscard && targetDiscardPile != null && targetDiscardPile.Count > 0)
        {
            drawnTileId = targetDiscardPile[^1];
            targetDiscardPile.RemoveAt(targetDiscardPile.Count - 1);
        }
        else if (state.DeckTileIds.Count > 0)
        {
            drawnTileId = state.DeckTileIds[0];
            state.DeckTileIds.RemoveAt(0);
            drewFromDiscard = false;
        }
        else
        {
            return new BotMoveResult { Success = false, Message = "Çekilecek taş yok." };
        }

        player.HandTileIds.Add(drawnTileId);
        player.HasDrawnThisTurn = true;

        // Çekilen taşı Tile nesnesine dönüştür
        var drawnTile = GetTileFromState(state, drawnTileId);
        if (drawnTile == null)
        {
            return new BotMoveResult { Success = false, Message = "Taş bilgisi alınamadı." };
        }

        // Bot bildirimi - rakipler için
        await _hubContext.Clients.Group($"room:{roomId}")
            .SendAsync("OnOpponentDrewTile", new
            {
                PlayerId = botId,
                FromDiscard = drewFromDiscard,
                IsBot = true,
                Timestamp = DateTime.UtcNow
            });

        // 3. Atma kararı ver
        var discardDecision = bot.DecideDiscard(drawnTile);

        // Atma için düşünme süresi
        await Task.Delay(discardDecision.ThinkingTimeMs);

        // 4. Taşı at
        if (!state.DiscardPiles.ContainsKey(botId))
        {
            state.DiscardPiles[botId] = new List<int>();
        }
        var botDiscardPile = state.DiscardPiles[botId];

        if (discardDecision.Tile == null || !player.HandTileIds.Contains(discardDecision.Tile.Id))
        {
            // Fallback: Rastgele taş at
            var fallbackTileId = player.HandTileIds[_random.Next(player.HandTileIds.Count)];
            player.HandTileIds.Remove(fallbackTileId);
            botDiscardPile.Add(fallbackTileId);
        }
        else
        {
            player.HandTileIds.Remove(discardDecision.Tile.Id);
            botDiscardPile.Add(discardDecision.Tile.Id);
        }

        player.HasDrawnThisTurn = false;
        player.IsCurrentTurn = false;

        // Sırayı sonraki oyuncuya geçir
        var nextPosition = (PlayerPosition)(((int)player.Position + 1) % 4);
        var nextPlayer = state.Players.Values.FirstOrDefault(p => p.Position == nextPosition);

        if (nextPlayer != null)
        {
            nextPlayer.IsCurrentTurn = true;
            state.CurrentTurnPlayerId = nextPlayer.PlayerId;
            state.CurrentTurnPosition = nextPosition;
        }

        state.TurnStartedAt = DateTime.UtcNow;
        await _stateService.SaveRoomStateAsync(state);

        // Atılan taş bildirimi - tam taş bilgisiyle
        var discardedTileId = discardDecision.Tile?.Id ?? state.DiscardPiles[botId][^1];
        var discardedTileInfo = GetTileFromState(state, discardedTileId);
        
        await _hubContext.Clients.Group($"room:{roomId}")
            .SendAsync("OnTileDiscarded", new
            {
                PlayerId = botId,
                TileId = discardedTileId,
                Tile = discardedTileInfo != null ? new {
                    Id = discardedTileInfo.Id,
                    Color = discardedTileInfo.Color.ToString(),
                    Number = discardedTileInfo.Value,
                    IsFalseJoker = discardedTileInfo.IsFalseJoker
                } : null,
                IsBot = true,
                NextTurnPlayerId = state.CurrentTurnPlayerId,
                NextTurnPosition = (int)state.CurrentTurnPosition,
                Timestamp = DateTime.UtcNow
            });

        // Deste güncelleme bildirimi
        await _hubContext.Clients.Group($"room:{roomId}")
            .SendAsync("OnDeckUpdated", new
            {
                RemainingTileCount = state.DeckTileIds.Count,
                DiscardPileCount = state.DiscardPiles.Values.Sum(p => p.Count)
            });

        _logger.LogDebug(
            "Bot hamle yaptı: {BotId}, Çekilen: {DrawnTileId}, Atılan: {DiscardedTileId}",
            botId, drawnTileId, discardedTileId);

        // Sonraki oyuncu da bot ise, onun turunu başlat
        await CheckAndProcessBotTurnAsync(roomId);

        return new BotMoveResult
        {
            Success = true,
            DrewFromDiscard = drewFromDiscard,
            DrawnTileId = drawnTileId,
            DiscardedTileId = discardedTileId,
            IsWinningMove = discardDecision.IsWinning,
            ThinkingTimeMs = drawDecision.ThinkingTimeMs + discardDecision.ThinkingTimeMs,
            Message = discardDecision.IsWinning ? "Bot oyunu kazandı!" : "Bot hamle yaptı."
        };
    }

    /// <inheritdoc/>
    public async Task CheckAndProcessBotTurnAsync(Guid roomId)
    {
        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null || state.State != GameState.InProgress)
        {
            return;
        }

        var currentPlayerId = state.CurrentTurnPlayerId;
        if (!currentPlayerId.HasValue)
        {
            return;
        }

        // Sıradaki oyuncu bot mu?
        if (!_botManager.IsBot(currentPlayerId.Value))
        {
            return;
        }

        _logger.LogDebug("Bot sırası tespit edildi: {BotId}, Oda: {RoomId}", currentPlayerId.Value, roomId);

        // Son atılan taşı bul (Önceki oyuncunun attığı)
        Tile? lastDiscardedTile = null;

        var currentPos = (int)state.Players[currentPlayerId.Value].Position;
        var prevPos = (currentPos - 1 + 4) % 4;
        var prevPlayer = state.Players.Values.FirstOrDefault(p => (int)p.Position == prevPos);

        if (prevPlayer != null &&
            state.DiscardPiles.TryGetValue(prevPlayer.PlayerId, out var pile) &&
            pile.Count > 0)
        {
            var lastTileId = pile[^1];
            lastDiscardedTile = GetTileFromState(state, lastTileId);
        }

        // Bot hamlesini işle (küçük bir gecikme ile - insan benzeri davranış)
        await Task.Delay(500);
        await ProcessBotTurnAsync(roomId, currentPlayerId.Value, lastDiscardedTile);
    }

    #endregion

    #region Private Methods

    private static string GetRandomBotName()
    {
        return BotNames[_random.Next(BotNames.Length)];
    }

    private static PlayerPosition GetNextAvailablePosition(GameRoomState state)
    {
        var usedPositions = state.Players.Values.Select(p => p.Position).ToHashSet();

        foreach (PlayerPosition pos in Enum.GetValues<PlayerPosition>())
        {
            if (!usedPositions.Contains(pos))
            {
                return pos;
            }
        }

        return PlayerPosition.South; // Fallback
    }

    private Tile? GetTileFromState(GameRoomState state, int tileId)
    {
        if (string.IsNullOrEmpty(state.AllTilesJson)) return null;

        try
        {
            var tilesData = JsonSerializer.Deserialize<List<TileData>>(state.AllTilesJson, _jsonOptions);
            var tileData = tilesData?.FirstOrDefault(t => t.Id == tileId);

            if (tileData == null) return null;

            if (tileData.IsFalseJoker)
            {
                return Tile.CreateFalseJoker(tileData.Id);
            }

            var color = Enum.Parse<TileColor>(tileData.Color);
            return Tile.Create(tileData.Id, color, tileData.Value);
        }
        catch
        {
            return null;
        }
    }

    private class TileData
    {
        public int Id { get; set; }
        public string Color { get; set; } = "";
        public int Value { get; set; }
        public bool IsFalseJoker { get; set; }
    }

    #endregion
}
