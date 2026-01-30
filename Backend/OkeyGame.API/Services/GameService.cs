using System.Text.Json;
using OkeyGame.API.Models;
using OkeyGame.Application.DTOs;
using OkeyGame.Application.Services;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;

namespace OkeyGame.API.Services;

/// <summary>
/// Oyun işlemlerini yöneten servis.
/// GameHub'dan çağrılır, iş mantığını içerir.
/// </summary>
public interface IGameService
{
    // Oda İşlemleri
    Task<GameRoomState> CreateRoomAsync(string roomName, Guid creatorId, string creatorName, long stake = 500);
    Task<(bool Success, string? Error)> JoinRoomAsync(Guid roomId, Guid playerId, string playerName, string connectionId);
    Task<(bool Success, string? Error)> LeaveRoomAsync(Guid roomId, Guid playerId);
    Task<(bool Success, string? Error)> StartGameAsync(Guid roomId);

    // Oyun Aksiyonları
    Task<DrawTileResultDto> DrawTileAsync(Guid roomId, Guid playerId, bool fromDiscard = false);
    Task<DiscardTileResultDto> DiscardTileAsync(Guid roomId, Guid playerId, int tileId);

    // Durum Sorgulama
    Task<GameStateDto?> GetGameStateForPlayerAsync(Guid roomId, Guid playerId);
    Task<GameRoomState?> GetRoomStateAsync(Guid roomId);

    // Reconnection
    Task<(bool Success, Guid? RoomId)> TryReconnectAsync(Guid playerId, string newConnectionId);
    Task HandleDisconnectAsync(Guid playerId, string connectionId);
}

/// <summary>
/// Oyun servisi implementasyonu.
/// </summary>
public class GameService : IGameService
{
    #region Alanlar

    private readonly IGameStateService _stateService;
    private readonly ProvablyFairService _provablyFairService;
    private readonly ILogger<GameService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Sabitler
    private const int MaxPlayers = 4;
    private const int FirstPlayerTileCount = 15;
    private const int OtherPlayersTileCount = 14;
    private const int ReconnectionTimeoutSeconds = 30;

    #endregion

    #region Constructor

    public GameService(
        IGameStateService stateService,
        ProvablyFairService provablyFairService,
        ILogger<GameService> logger)
    {
        _stateService = stateService;
        _provablyFairService = provablyFairService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #endregion

    #region Oda İşlemleri

    /// <summary>
    /// Yeni bir oyun odası oluşturur.
    /// </summary>
    public async Task<GameRoomState> CreateRoomAsync(string roomName, Guid creatorId, string creatorName, long stake = 500)
    {
        var roomId = Guid.NewGuid();

        var state = new GameRoomState
        {
            RoomId = roomId,
            RoomName = roomName,
            Stake = stake,
            State = GameState.WaitingForPlayers,
            CreatedAt = DateTime.UtcNow
        };

        // İlk oyuncuyu ekle
        state.Players[creatorId] = new PlayerState
        {
            PlayerId = creatorId,
            DisplayName = creatorName,
            Position = PlayerPosition.South,
            IsConnected = true,
            LastConnectedAt = DateTime.UtcNow
        };

        await _stateService.SaveRoomStateAsync(state);
        await _stateService.AddToActiveRoomsAsync(roomId);

        _logger.LogInformation("Oda oluşturuldu: {RoomId} - {RoomName}", roomId, roomName);

        return state;
    }

    /// <summary>
    /// Odaya oyuncu ekler.
    /// </summary>
    public async Task<(bool Success, string? Error)> JoinRoomAsync(
        Guid roomId, Guid playerId, string playerName, string connectionId)
    {
        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            return (false, "Oda şu anda meşgul, lütfen tekrar deneyin.");
        }

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return (false, "Oda bulunamadı.");
        }

        if (state.State != GameState.WaitingForPlayers)
        {
            return (false, "Oyun başlamış, yeni oyuncu kabul edilmiyor.");
        }

        if (state.Players.Count >= MaxPlayers)
        {
            return (false, "Oda dolu.");
        }

        // Oyuncu zaten odada mı?
        if (state.Players.ContainsKey(playerId))
        {
            // Reconnection durumu
            state.Players[playerId].ConnectionId = connectionId;
            state.Players[playerId].IsConnected = true;
            state.Players[playerId].LastConnectedAt = DateTime.UtcNow;
            state.Players[playerId].DisconnectedAt = null;
        }
        else
        {
            // Yeni oyuncu
            var position = GetNextAvailablePosition(state);
            state.Players[playerId] = new PlayerState
            {
                PlayerId = playerId,
                DisplayName = playerName,
                Position = position,
                ConnectionId = connectionId,
                IsConnected = true,
                LastConnectedAt = DateTime.UtcNow
            };
        }

        await _stateService.SaveRoomStateAsync(state);
        await _stateService.SaveConnectionMappingAsync(playerId, roomId, connectionId);

        _logger.LogInformation("Oyuncu odaya katıldı: {PlayerId} -> {RoomId}", playerId, roomId);

        return (true, null);
    }

    /// <summary>
    /// Oyuncuyu odadan çıkarır.
    /// </summary>
    public async Task<(bool Success, string? Error)> LeaveRoomAsync(Guid roomId, Guid playerId)
    {
        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            return (false, "İşlem gerçekleştirilemedi.");
        }

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return (false, "Oda bulunamadı.");
        }

        if (!state.Players.ContainsKey(playerId))
        {
            return (false, "Oyuncu odada değil.");
        }

        state.Players.Remove(playerId);
        await _stateService.RemoveConnectionMappingAsync(playerId);

        // Oda boşaldıysa sil
        if (state.Players.Count == 0)
        {
            await _stateService.DeleteRoomStateAsync(roomId);
            _logger.LogInformation("Oda silindi (boş): {RoomId}", roomId);
        }
        else
        {
            // Oyun devam ediyorsa iptal et
            if (state.State == GameState.InProgress)
            {
                state.State = GameState.Cancelled;
            }
            await _stateService.SaveRoomStateAsync(state);
        }

        return (true, null);
    }

    /// <summary>
    /// Oyunu başlatır.
    /// </summary>
    public async Task<(bool Success, string? Error)> StartGameAsync(Guid roomId)
    {
        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            return (false, "İşlem gerçekleştirilemedi.");
        }

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return (false, "Oda bulunamadı.");
        }

        if (state.State != GameState.WaitingForPlayers)
        {
            return (false, "Oyun zaten başlamış.");
        }

        // En az 1 oyuncu olmalı ve toplam 4 oyuncu (bot dahil) olmalı
        if (state.Players.Count == 0)
        {
            return (false, "Odada oyuncu bulunmuyor.");
        }

        if (state.Players.Count < MaxPlayers)
        {
            return (false, $"Oyun başlatmak için {MaxPlayers} oyuncu gerekli. Bot eklemek için StartGameWithBots kullanın.");
        }

        // Taşları oluştur ve karıştır
        var tiles = TileFactory.CreateFullSet();
        FisherYatesShuffle.Shuffle(tiles);

        // Provably Fair commitment oluştur
        var commitment = _provablyFairService.CreateCommitment(roomId, tiles);
        state.CommitmentHash = commitment.CommitmentHash;
        state.ServerSeed = commitment.ServerSeed.ToString();
        state.InitialState = commitment.InitialState;
        state.Nonce = commitment.Nonce;

        // Taşları JSON olarak sakla
        state.AllTilesJson = JsonSerializer.Serialize(tiles.Select(t => new
        {
            t.Id,
            Color = t.Color.ToString(),
            t.Value,
            t.IsFalseJoker
        }), _jsonOptions);

        // Gösterge taşını belirle
        var normalTiles = tiles.Where(t => !t.IsFalseJoker).ToList();
        var rng = CryptoRandomGenerator.Instance;
        var indicatorIndex = rng.NextInt(normalTiles.Count);
        var indicatorTile = normalTiles[indicatorIndex];
        state.IndicatorTileId = indicatorTile.Id;

        // Okeyleri işaretle
        tiles = TileFactory.MarkOkeyTiles(tiles, indicatorTile);

        // Taşları dağıt
        var tileQueue = new Queue<Tile>(tiles);
        var orderedPlayers = state.Players.Values.OrderBy(p => p.Position).ToList();

        for (int i = 0; i < orderedPlayers.Count; i++)
        {
            var player = orderedPlayers[i];
            var tileCount = (i == 0) ? FirstPlayerTileCount : OtherPlayersTileCount;

            player.HandTileIds = new List<int>();
            for (int j = 0; j < tileCount && tileQueue.Count > 0; j++)
            {
                player.HandTileIds.Add(tileQueue.Dequeue().Id);
            }

            // İlk oyuncu sırada başlar
            if (i == 0)
            {
                player.IsCurrentTurn = true;
                state.CurrentTurnPlayerId = player.PlayerId;
                state.CurrentTurnPosition = player.Position;
            }
        }

        // Kalan taşları desteye koy
        state.DeckTileIds = tileQueue.Select(t => t.Id).ToList();
        state.DiscardPileTileIds = new List<int>();

        // Durumu güncelle
        state.State = GameState.InProgress;
        state.GameStartedAt = DateTime.UtcNow;
        state.TurnStartedAt = DateTime.UtcNow;

        await _stateService.SaveRoomStateAsync(state);

        _logger.LogInformation("Oyun başlatıldı: {RoomId}", roomId);

        return (true, null);
    }

    #endregion

    #region Oyun Aksiyonları

    /// <summary>
    /// Desteden veya atık yığınından taş çeker.
    /// </summary>
    public async Task<DrawTileResultDto> DrawTileAsync(Guid roomId, Guid playerId, bool fromDiscard = false)
    {
        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            return new DrawTileResultDto { Success = false, ErrorMessage = "İşlem gerçekleştirilemedi." };
        }

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return new DrawTileResultDto { Success = false, ErrorMessage = "Oda bulunamadı." };
        }

        if (state.State != GameState.InProgress)
        {
            return new DrawTileResultDto { Success = false, ErrorMessage = "Oyun aktif değil." };
        }

        if (!state.Players.TryGetValue(playerId, out var player))
        {
            return new DrawTileResultDto { Success = false, ErrorMessage = "Oyuncu bulunamadı." };
        }

        if (!player.IsCurrentTurn)
        {
            return new DrawTileResultDto { Success = false, ErrorMessage = "Sıra sizde değil." };
        }

        if (player.HasDrawnThisTurn)
        {
            return new DrawTileResultDto { Success = false, ErrorMessage = "Bu turda zaten taş çektiniz." };
        }

        int drawnTileId;

        if (fromDiscard)
        {
            if (state.DiscardPileTileIds.Count == 0)
            {
                return new DrawTileResultDto { Success = false, ErrorMessage = "Atık yığınında taş yok." };
            }
            drawnTileId = state.DiscardPileTileIds[^1];
            state.DiscardPileTileIds.RemoveAt(state.DiscardPileTileIds.Count - 1);
        }
        else
        {
            if (state.DeckTileIds.Count == 0)
            {
                return new DrawTileResultDto { Success = false, ErrorMessage = "Destede taş kalmadı." };
            }
            drawnTileId = state.DeckTileIds[0];
            state.DeckTileIds.RemoveAt(0);
        }

        player.HandTileIds.Add(drawnTileId);
        player.HasDrawnThisTurn = true;
        player.LastActivityAt = DateTime.UtcNow;

        await _stateService.SaveRoomStateAsync(state);

        // Çekilen taşı bul ve DTO oluştur
        var drawnTile = GetTileFromState(state, drawnTileId);

        return new DrawTileResultDto
        {
            Success = true,
            DrawnTile = drawnTile != null ? MapToTileDto(drawnTile, state.IndicatorTileId) : null
        };
    }

    /// <summary>
    /// Taş atar ve sırayı devreder.
    /// </summary>
    public async Task<DiscardTileResultDto> DiscardTileAsync(Guid roomId, Guid playerId, int tileId)
    {
        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            return new DiscardTileResultDto { Success = false, ErrorMessage = "İşlem gerçekleştirilemedi." };
        }

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return new DiscardTileResultDto { Success = false, ErrorMessage = "Oda bulunamadı." };
        }

        if (state.State != GameState.InProgress)
        {
            return new DiscardTileResultDto { Success = false, ErrorMessage = "Oyun aktif değil." };
        }

        if (!state.Players.TryGetValue(playerId, out var player))
        {
            return new DiscardTileResultDto { Success = false, ErrorMessage = "Oyuncu bulunamadı." };
        }

        if (!player.IsCurrentTurn)
        {
            return new DiscardTileResultDto { Success = false, ErrorMessage = "Sıra sizde değil." };
        }

        if (!player.HandTileIds.Contains(tileId))
        {
            return new DiscardTileResultDto { Success = false, ErrorMessage = "Bu taş elinizde yok." };
        }

        // Atılan taşın bilgisini al
        var discardedTile = GetTileFromState(state, tileId);

        // Taşı elden çıkar ve atık yığınına ekle
        player.HandTileIds.Remove(tileId);
        state.DiscardPileTileIds.Add(tileId);

        // Sırayı sonraki oyuncuya geçir
        player.IsCurrentTurn = false;
        player.HasDrawnThisTurn = false;

        var nextPosition = (PlayerPosition)(((int)player.Position + 1) % 4);
        var nextPlayer = state.Players.Values.FirstOrDefault(p => p.Position == nextPosition);

        if (nextPlayer != null)
        {
            nextPlayer.IsCurrentTurn = true;
            state.CurrentTurnPlayerId = nextPlayer.PlayerId;
            state.CurrentTurnPosition = nextPosition;
        }

        state.TurnStartedAt = DateTime.UtcNow;
        player.LastActivityAt = DateTime.UtcNow;

        await _stateService.SaveRoomStateAsync(state);

        _logger.LogDebug("Taş atıldı: {PlayerId} -> Tile {TileId}", playerId, tileId);

        return new DiscardTileResultDto 
        { 
            Success = true,
            DiscardedTile = discardedTile != null ? new TileDto
            {
                Id = discardedTile.Id,
                Color = discardedTile.Color,
                Value = discardedTile.Value,
                IsOkey = false,
                IsFalseJoker = discardedTile.IsFalseJoker
            } : null
        };
    }

    #endregion

    #region Durum Sorgulama

    /// <summary>
    /// Oyuncu için oyun durumu DTO'su oluşturur.
    /// </summary>
    public async Task<GameStateDto?> GetGameStateForPlayerAsync(Guid roomId, Guid playerId)
    {
        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null) return null;

        if (!state.Players.TryGetValue(playerId, out var player))
        {
            return null;
        }

        // Oyuncunun elini oluştur
        var handTiles = player.HandTileIds
            .Select(id => GetTileFromState(state, id))
            .Where(t => t != null)
            .Select(t => MapToTileDto(t!, state.IndicatorTileId))
            .ToList();

        var selfDto = new PlayerDto
        {
            Id = player.PlayerId,
            DisplayName = player.DisplayName,
            Position = player.Position,
            Hand = handTiles,
            IsCurrentTurn = player.IsCurrentTurn,
            IsConnected = player.IsConnected
        };

        // Rakipleri oluştur (eller GİZLİ)
        var opponents = state.Players.Values
            .Where(p => p.PlayerId != playerId)
            .Select(p => new OpponentDto
            {
                Id = p.PlayerId,
                DisplayName = p.DisplayName,
                Position = p.Position,
                TileCount = p.HandTileIds.Count,
                IsCurrentTurn = p.IsCurrentTurn,
                IsConnected = p.IsConnected
            })
            .ToList();

        // Gösterge taşı
        var indicatorTile = state.IndicatorTileId.HasValue
            ? GetTileFromState(state, state.IndicatorTileId.Value)
            : null;

        // Atık yığını üstündeki taş
        TileDto? discardTop = null;
        if (state.DiscardPileTileIds.Count > 0)
        {
            var topTile = GetTileFromState(state, state.DiscardPileTileIds[^1]);
            if (topTile != null)
            {
                discardTop = MapToTileDto(topTile, state.IndicatorTileId);
            }
        }

        return new GameStateDto
        {
            RoomId = state.RoomId,
            State = state.State,
            CurrentTurnPosition = state.CurrentTurnPosition,
            Self = selfDto,
            Opponents = opponents,
            IndicatorTile = indicatorTile != null ? MapToTileDto(indicatorTile, state.IndicatorTileId) : null!,
            RemainingTileCount = state.DeckTileIds.Count,
            DiscardPileTopTile = discardTop,
            GameStartedAt = state.GameStartedAt ?? DateTime.UtcNow,
            ServerTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Oda durumunu döndürür.
    /// </summary>
    public async Task<GameRoomState?> GetRoomStateAsync(Guid roomId)
    {
        return await _stateService.GetRoomStateAsync(roomId);
    }

    #endregion

    #region Reconnection

    /// <summary>
    /// Bağlantısı kopan oyuncuyu eski odasına yeniden bağlar.
    /// </summary>
    public async Task<(bool Success, Guid? RoomId)> TryReconnectAsync(Guid playerId, string newConnectionId)
    {
        var mapping = await _stateService.GetConnectionMappingAsync(playerId);
        if (mapping == null)
        {
            _logger.LogDebug("Oyuncu için bağlantı eşleştirmesi bulunamadı: {PlayerId}", playerId);
            return (false, null);
        }

        var state = await _stateService.GetRoomStateAsync(mapping.RoomId);
        if (state == null)
        {
            _logger.LogDebug("Oda artık mevcut değil: {RoomId}", mapping.RoomId);
            await _stateService.RemoveConnectionMappingAsync(playerId);
            return (false, null);
        }

        if (!state.Players.TryGetValue(playerId, out var player))
        {
            _logger.LogDebug("Oyuncu artık odada değil: {PlayerId}", playerId);
            return (false, null);
        }

        // Bağlantı kopma süresi kontrolü
        if (player.DisconnectedAt.HasValue)
        {
            var disconnectedDuration = DateTime.UtcNow - player.DisconnectedAt.Value;
            if (disconnectedDuration.TotalSeconds > ReconnectionTimeoutSeconds)
            {
                _logger.LogInformation("Reconnection timeout aşıldı: {PlayerId}", playerId);
                return (false, null);
            }
        }

        // Yeniden bağlan
        player.ConnectionId = newConnectionId;
        player.IsConnected = true;
        player.LastConnectedAt = DateTime.UtcNow;
        player.DisconnectedAt = null;

        await _stateService.SaveRoomStateAsync(state);
        await _stateService.SaveConnectionMappingAsync(playerId, mapping.RoomId, newConnectionId);

        _logger.LogInformation("Oyuncu yeniden bağlandı: {PlayerId} -> {RoomId}", playerId, mapping.RoomId);

        return (true, mapping.RoomId);
    }

    /// <summary>
    /// Bağlantı koptuğunda çağrılır.
    /// </summary>
    public async Task HandleDisconnectAsync(Guid playerId, string connectionId)
    {
        var mapping = await _stateService.GetConnectionMappingAsync(playerId);
        if (mapping == null) return;

        var state = await _stateService.GetRoomStateAsync(mapping.RoomId);
        if (state == null) return;

        if (state.Players.TryGetValue(playerId, out var player))
        {
            // Sadece aynı connection ise disconnect olarak işaretle
            if (player.ConnectionId == connectionId)
            {
                player.IsConnected = false;
                player.DisconnectedAt = DateTime.UtcNow;
                await _stateService.SaveRoomStateAsync(state);

                _logger.LogInformation("Oyuncu bağlantısı koptu: {PlayerId}", playerId);
            }
        }
    }

    #endregion

    #region Yardımcı Metotlar

    /// <summary>
    /// Sonraki boş pozisyonu döndürür.
    /// </summary>
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

    /// <summary>
    /// State içinden taşı ID ile bulur.
    /// </summary>
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
            var tile = Tile.Create(tileData.Id, color, tileData.Value);

            // Okey kontrolü
            if (state.IndicatorTileId.HasValue)
            {
                var indicator = tilesData?.FirstOrDefault(t => t.Id == state.IndicatorTileId.Value);
                if (indicator != null && !indicator.IsFalseJoker)
                {
                    var indicatorColor = Enum.Parse<TileColor>(indicator.Color);
                    var okeyValue = indicator.Value == 13 ? 1 : indicator.Value + 1;

                    if (color == indicatorColor && tileData.Value == okeyValue)
                    {
                        tile = tile.AsOkey();
                    }
                }
            }

            return tile;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tile'ı TileDto'ya çevirir.
    /// </summary>
    private static TileDto MapToTileDto(Tile tile, int? indicatorTileId)
    {
        return new TileDto
        {
            Id = tile.Id,
            Color = tile.Color,
            Value = tile.Value,
            IsOkey = tile.IsOkey,
            IsFalseJoker = tile.IsFalseJoker
        };
    }

    #endregion

    #region Nested Classes

    private class TileData
    {
        public int Id { get; set; }
        public string Color { get; set; } = "";
        public int Value { get; set; }
        public bool IsFalseJoker { get; set; }
    }

    #endregion
}
