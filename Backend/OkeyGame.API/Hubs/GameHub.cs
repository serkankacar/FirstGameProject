using Microsoft.AspNetCore.SignalR;
using OkeyGame.API.Models;
using OkeyGame.API.Services;
using OkeyGame.Application.DTOs;
using OkeyGame.Domain.AI;
using OkeyGame.Domain.Enums;

namespace OkeyGame.API.Hubs;

/// <summary>
/// Okey oyunu için SignalR Hub.
/// Gerçek zamanlı oyun iletişimini yönetir.
/// 
/// CLIENT METODLARI (Server -> Client):
/// - OnRoomJoined: Odaya katılım onayı
/// - OnPlayerJoined: Yeni oyuncu katıldı
/// - OnPlayerLeft: Oyuncu ayrıldı
/// - OnGameStarted: Oyun başladı
/// - OnGameStateUpdated: Oyun durumu güncellendi
/// - OnTileDrawn: Taş çekildi (sadece çeken oyuncuya)
/// - OnTileDiscarded: Taş atıldı (herkese)
/// - OnTurnChanged: Sıra değişti
/// - OnPlayerReconnected: Oyuncu yeniden bağlandı
/// - OnPlayerDisconnected: Oyuncu bağlantısı koptu
/// - OnError: Hata mesajı
/// </summary>
public class GameHub : Hub
{
    #region Alanlar

    private readonly IGameService _gameService;
    private readonly IGameStateService _stateService;
    private readonly IBotPlayerService _botService;
    private readonly ILogger<GameHub> _logger;

    // SignalR Group prefix'i
    private const string RoomGroupPrefix = "room:";

    #endregion

    #region Constructor

    public GameHub(
        IGameService gameService,
        IGameStateService stateService,
        IBotPlayerService botService,
        ILogger<GameHub> logger)
    {
        _gameService = gameService;
        _stateService = stateService;
        _botService = botService;
        _logger = logger;
    }

    #endregion

    #region Bağlantı Yaşam Döngüsü

    /// <summary>
    /// İstemci bağlandığında çağrılır.
    /// Reconnection mantığını burada uyguluyoruz.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var playerId = GetPlayerIdFromContext();
        
        if (playerId.HasValue)
        {
            _logger.LogInformation("Oyuncu bağlandı: {PlayerId}, Connection: {ConnectionId}", 
                playerId.Value, Context.ConnectionId);

            // Reconnection kontrolü
            var (reconnected, roomId) = await _gameService.TryReconnectAsync(
                playerId.Value, Context.ConnectionId);

            if (reconnected && roomId.HasValue)
            {
                // Odaya yeniden ekle
                await Groups.AddToGroupAsync(Context.ConnectionId, GetRoomGroup(roomId.Value));

                // Oyun durumunu gönder
                var gameState = await _gameService.GetGameStateForPlayerAsync(roomId.Value, playerId.Value);
                if (gameState != null)
                {
                    await Clients.Caller.SendAsync("OnReconnected", new
                    {
                        RoomId = roomId.Value,
                        GameState = gameState,
                        Message = "Oyuna yeniden bağlandınız!"
                    });

                    // Diğer oyunculara bildir
                    await Clients.OthersInGroup(GetRoomGroup(roomId.Value))
                        .SendAsync("OnPlayerReconnected", new
                        {
                            PlayerId = playerId.Value,
                            Timestamp = DateTime.UtcNow
                        });

                    _logger.LogInformation("Oyuncu yeniden bağlandı: {PlayerId} -> Oda {RoomId}", 
                        playerId.Value, roomId.Value);
                }
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// İstemci bağlantısı koptuğunda çağrılır.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetPlayerIdFromContext();

        if (playerId.HasValue)
        {
            _logger.LogInformation("Oyuncu bağlantısı koptu: {PlayerId}, Connection: {ConnectionId}", 
                playerId.Value, Context.ConnectionId);

            // Bağlantı kopma işlemi
            await _gameService.HandleDisconnectAsync(playerId.Value, Context.ConnectionId);

            // Hangi odadaysa o odaya bildir
            var mapping = await _stateService.GetConnectionMappingAsync(playerId.Value);
            if (mapping != null)
            {
                await Clients.Group(GetRoomGroup(mapping.RoomId))
                    .SendAsync("OnPlayerDisconnected", new
                    {
                        PlayerId = playerId.Value,
                        ReconnectionTimeoutSeconds = 30,
                        Timestamp = DateTime.UtcNow
                    });
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Oda İşlemleri

    /// <summary>
    /// Yeni oda oluşturur.
    /// </summary>
    /// <param name="roomName">Oda adı</param>
    /// <param name="stake">Masa bahis miktarı</param>
    public async Task CreateRoom(string roomName, long stake)
    {
        var playerId = GetPlayerIdFromContext();
        var playerName = GetPlayerNameFromContext() ?? "Oyuncu";
        
        // Eğer kimlik doğrulama yoksa, demo mod olarak devam et
        if (!playerId.HasValue)
        {
            playerId = Guid.NewGuid();
            _logger.LogInformation("Demo mod: Yeni oyuncu ID atandı: {PlayerId}", playerId.Value);
        }

        try
        {
            var roomState = await _gameService.CreateRoomAsync(roomName, playerId.Value, playerName, stake);

            // Odaya katıl
            await Groups.AddToGroupAsync(Context.ConnectionId, GetRoomGroup(roomState.RoomId));

            // Bağlantı eşleştirmesini kaydet
            await _stateService.SaveConnectionMappingAsync(
                playerId.Value, roomState.RoomId, Context.ConnectionId);

            // Aktif odalara ekle
            await _stateService.AddToActiveRoomsAsync(roomState.RoomId);

            // Onay gönder - RoomJoined event'i Unity'nin beklediği format
            await Clients.Caller.SendAsync("RoomJoined", new
            {
                Id = roomState.RoomId.ToString(),
                Name = roomState.RoomName,
                Stake = roomState.Stake,
                CurrentPlayerCount = roomState.Players.Count,
                MaxPlayers = 4,
                IsGameStarted = roomState.IsGameStarted
            });

            _logger.LogInformation("Oda oluşturuldu: {RoomId} ({RoomName}) tarafından {PlayerId}, Stake: {Stake}", 
                roomState.RoomId, roomName, playerId.Value, stake);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oda oluşturma hatası");
            await SendError("Oda oluşturulamadı: " + ex.Message);
        }
    }

    /// <summary>
    /// Mevcut bir odaya katılır.
    /// </summary>
    /// <param name="roomId">Oda ID'si (string olarak gelir)</param>
    public async Task JoinRoom(string roomIdStr)
    {
        if (!Guid.TryParse(roomIdStr, out var roomId))
        {
            await SendError("Geçersiz oda ID'si.");
            return;
        }

        var playerId = GetPlayerIdFromContext();
        var playerName = GetPlayerNameFromContext() ?? "Oyuncu";
        
        if (!playerId.HasValue)
        {
            playerId = Guid.NewGuid();
            _logger.LogInformation("Demo mod: Yeni oyuncu ID atandı: {PlayerId}", playerId.Value);
        }

        try
        {
            var (success, error) = await _gameService.JoinRoomAsync(
                roomId, playerId.Value, playerName, Context.ConnectionId);

            if (!success)
            {
                await SendError(error ?? "Odaya katılınamadı.");
                return;
            }

            // Gruba ekle
            await Groups.AddToGroupAsync(Context.ConnectionId, GetRoomGroup(roomId));

            // Oda durumunu al
            var roomState = await _gameService.GetRoomStateAsync(roomId);
            var playerState = roomState?.Players.GetValueOrDefault(playerId.Value);

            // Katılana onay gönder - RoomJoined event'i
            await Clients.Caller.SendAsync("RoomJoined", new
            {
                Id = roomId.ToString(),
                Name = roomState?.RoomName ?? "Oda",
                Stake = roomState?.Stake ?? 0,
                CurrentPlayerCount = roomState?.Players.Count ?? 0,
                MaxPlayers = 4,
                IsGameStarted = roomState?.IsGameStarted ?? false
            });

            // Diğer oyunculara bildir
            await Clients.OthersInGroup(GetRoomGroup(roomId))
                .SendAsync("OnPlayerJoined", new
                {
                    PlayerId = playerId.Value,
                    PlayerName = playerName,
                    Position = playerState?.Position,
                    TotalPlayers = roomState?.Players.Count
                });

            _logger.LogInformation("Oyuncu odaya katıldı: {PlayerId} -> {RoomId}", 
                playerId.Value, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Odaya katılma hatası");
            await SendError("Odaya katılınamadı: " + ex.Message);
        }
    }

    /// <summary>
    /// Odadan ayrılır.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    public async Task LeaveRoom(Guid roomId)
    {
        var playerId = GetPlayerIdFromContext();
        if (!playerId.HasValue)
        {
            await SendError("Kimlik doğrulama gerekli.");
            return;
        }

        try
        {
            var (success, error) = await _gameService.LeaveRoomAsync(roomId, playerId.Value);

            if (!success)
            {
                await SendError(error ?? "Odadan ayrılınamadı.");
                return;
            }

            // Gruptan çıkar
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetRoomGroup(roomId));

            // Onay gönder
            await Clients.Caller.SendAsync("OnRoomLeft", new { RoomId = roomId });

            // Diğer oyunculara bildir
            await Clients.Group(GetRoomGroup(roomId))
                .SendAsync("OnPlayerLeft", new
                {
                    PlayerId = playerId.Value,
                    Timestamp = DateTime.UtcNow
                });

            _logger.LogInformation("Oyuncu odadan ayrıldı: {PlayerId} <- {RoomId}", 
                playerId.Value, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Odadan ayrılma hatası");
            await SendError("Odadan ayrılınamadı: " + ex.Message);
        }
    }

    /// <summary>
    /// Oyunu başlatır.
    /// Yeterli oyuncu yoksa otomatik olarak bot ekler.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    public async Task StartGame(Guid roomId)
    {
        var playerId = GetPlayerIdFromContext();
        if (!playerId.HasValue)
        {
            await SendError("Kimlik doğrulama gerekli.");
            return;
        }

        try
        {
            // Mevcut oda durumunu kontrol et
            var roomState = await _gameService.GetRoomStateAsync(roomId);
            if (roomState == null)
            {
                await SendError("Oda bulunamadı.");
                return;
            }

            // Yeterli oyuncu yoksa bot ekle
            if (roomState.Players.Count < 4)
            {
                _logger.LogInformation(
                    "Eksik oyuncu tespit edildi: {CurrentCount}/4. Bot ekleniyor...",
                    roomState.Players.Count);

                var addedBots = await _botService.FillRoomWithBotsAsync(roomId, BotDifficulty.Normal);
                
                _logger.LogInformation(
                    "{BotCount} bot eklendi. Oda: {RoomId}",
                    addedBots.Count, roomId);
            }

            var (success, error) = await _gameService.StartGameAsync(roomId);

            if (!success)
            {
                await SendError(error ?? "Oyun başlatılamadı.");
                return;
            }

            // Güncel oda durumunu al
            roomState = await _gameService.GetRoomStateAsync(roomId);
            if (roomState == null) return;

            // Tüm insan oyunculara oyun durumunu gönder (botlara değil)
            foreach (var player in roomState.Players.Values)
            {
                // Bot oyunculara SignalR mesajı göndermeye gerek yok
                if (_botService.IsBot(player.PlayerId))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(player.ConnectionId)) continue;

                var gameState = await _gameService.GetGameStateForPlayerAsync(roomId, player.PlayerId);
                if (gameState == null) continue;

                await Clients.Client(player.ConnectionId).SendAsync("OnGameStarted", new GameStartDto
                {
                    RoomId = roomId,
                    InitialState = gameState,
                    ServerSeedHash = roomState.CommitmentHash ?? ""
                });
            }

            _logger.LogInformation("Oyun başlatıldı: {RoomId}", roomId);

            // İlk oyuncu bot ise, bot turunu başlat
            await _botService.CheckAndProcessBotTurnAsync(roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oyun başlatma hatası");
            await SendError("Oyun başlatılamadı: " + ex.Message);
        }
    }

    /// <summary>
    /// Oyunu botlarla başlatır.
    /// </summary>
    /// <param name="roomIdStr">Oda ID'si</param>
    /// <param name="botDifficulty">Bot zorluk seviyesi (0=Easy, 1=Normal, 2=Hard, 3=Expert)</param>
    public async Task StartGameWithBots(string roomIdStr, int botDifficulty = 1)
    {
        if (!Guid.TryParse(roomIdStr, out var roomId))
        {
            await SendError("Geçersiz oda ID'si.");
            return;
        }

        var difficulty = (BotDifficulty)Math.Clamp(botDifficulty, 0, 3);
        
        // Botları ekle
        var addedBots = await _botService.FillRoomWithBotsAsync(roomId, difficulty);
        _logger.LogInformation("{BotCount} bot eklendi ({Difficulty}). Oda: {RoomId}",
            addedBots.Count, difficulty, roomId);

        // Oyunu başlat
        await StartGame(roomId);
    }

    #endregion

    #region Oyun Aksiyonları

    /// <summary>
    /// Desteden taş çeker.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    public async Task DrawTile(Guid roomId)
    {
        await DrawTileInternal(roomId, fromDiscard: false);
    }

    /// <summary>
    /// Atık yığınından taş çeker.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    public async Task DrawFromDiscard(Guid roomId)
    {
        await DrawTileInternal(roomId, fromDiscard: true);
    }

    /// <summary>
    /// Taş çekme iç metodu.
    /// </summary>
    private async Task DrawTileInternal(Guid roomId, bool fromDiscard)
    {
        var playerId = GetPlayerIdFromContext();
        if (!playerId.HasValue)
        {
            await SendError("Kimlik doğrulama gerekli.");
            return;
        }

        try
        {
            var result = await _gameService.DrawTileAsync(roomId, playerId.Value, fromDiscard);

            if (!result.Success)
            {
                await SendError(result.ErrorMessage ?? "Taş çekilemedi.");
                return;
            }

            // Çekilen taşı sadece çeken oyuncuya gönder
            await Clients.Caller.SendAsync("OnTileDrawn", new
            {
                Tile = result.DrawnTile,
                FromDiscard = fromDiscard,
                Timestamp = DateTime.UtcNow
            });

            // Diğer oyunculara taş çekildiğini bildir (taş bilgisi OLMADAN)
            await Clients.OthersInGroup(GetRoomGroup(roomId))
                .SendAsync("OnOpponentDrewTile", new
                {
                    PlayerId = playerId.Value,
                    FromDiscard = fromDiscard,
                    Timestamp = DateTime.UtcNow
                });

            // Deste sayısını güncelle
            var roomState = await _gameService.GetRoomStateAsync(roomId);
            if (roomState != null)
            {
                await Clients.Group(GetRoomGroup(roomId))
                    .SendAsync("OnDeckUpdated", new
                    {
                        RemainingTileCount = roomState.DeckTileIds.Count,
                        DiscardPileCount = roomState.DiscardPileTileIds.Count
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Taş çekme hatası");
            await SendError("Taş çekilemedi: " + ex.Message);
        }
    }

    /// <summary>
    /// Taş atar.
    /// </summary>
    /// <param name="roomId">Oda ID'si</param>
    /// <param name="tileId">Atılacak taşın ID'si</param>
    public async Task ThrowTile(Guid roomId, int tileId)
    {
        var playerId = GetPlayerIdFromContext();
        if (!playerId.HasValue)
        {
            await SendError("Kimlik doğrulama gerekli.");
            return;
        }

        try
        {
            var result = await _gameService.DiscardTileAsync(roomId, playerId.Value, tileId);

            if (!result.Success)
            {
                await SendError(result.ErrorMessage ?? "Taş atılamadı.");
                return;
            }

            // Atılan taşı herkese bildir (tam taş bilgisiyle)
            var roomState = await _gameService.GetRoomStateAsync(roomId);
            
            await Clients.Group(GetRoomGroup(roomId))
                .SendAsync("OnTileDiscarded", new
                {
                    PlayerId = playerId.Value,
                    TileId = tileId,
                    Tile = result.DiscardedTile != null ? new {
                        Id = result.DiscardedTile.Id,
                        Color = result.DiscardedTile.Color.ToString(),
                        Number = result.DiscardedTile.Value,
                        IsFalseJoker = result.DiscardedTile.IsFalseJoker
                    } : null,
                    NextTurnPlayerId = roomState?.CurrentTurnPlayerId,
                    NextTurnPosition = roomState?.CurrentTurnPosition,
                    Timestamp = DateTime.UtcNow
                });

            _logger.LogDebug("Taş atıldı: {PlayerId} -> Tile {TileId}", playerId.Value, tileId);

            // Sonraki oyuncu bot ise, bot turunu başlat
            if (roomState != null)
            {
                await _botService.CheckAndProcessBotTurnAsync(roomId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Taş atma hatası");
            await SendError("Taş atılamadı: " + ex.Message);
        }
    }

    #endregion

    #region Yardımcı Metodlar

    /// <summary>
    /// Context'ten oyuncu ID'sini alır.
    /// JWT token veya query string'den okunabilir.
    /// </summary>
    private Guid? GetPlayerIdFromContext()
    {
        // 1. JWT Claim'den okumayı dene
        var userIdClaim = Context.User?.FindFirst("sub")?.Value 
            ?? Context.User?.FindFirst("userId")?.Value;
        
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var claimId))
        {
            return claimId;
        }

        // 2. Query string'den okumayı dene (development için)
        var queryPlayerId = Context.GetHttpContext()?.Request.Query["playerId"].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryPlayerId) && Guid.TryParse(queryPlayerId, out var queryId))
        {
            return queryId;
        }

        // 3. Header'dan okumayı dene
        var headerPlayerId = Context.GetHttpContext()?.Request.Headers["X-Player-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerPlayerId) && Guid.TryParse(headerPlayerId, out var headerId))
        {
            return headerId;
        }

        return null;
    }

    /// <summary>
    /// Context'ten oyuncu adını alır.
    /// </summary>
    private string? GetPlayerNameFromContext()
    {
        // JWT Claim'den okumayı dene
        var nameClaim = Context.User?.FindFirst("name")?.Value 
            ?? Context.User?.FindFirst("username")?.Value;
        
        if (!string.IsNullOrEmpty(nameClaim))
        {
            return nameClaim;
        }

        // Query string'den okumayı dene
        var queryName = Context.GetHttpContext()?.Request.Query["playerName"].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryName))
        {
            return queryName;
        }

        return null;
    }

    /// <summary>
    /// Hata mesajı gönderir.
    /// </summary>
    private async Task SendError(string message)
    {
        await Clients.Caller.SendAsync("OnError", new
        {
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Oda grubu adını döndürür.
    /// </summary>
    private static string GetRoomGroup(Guid roomId) => $"{RoomGroupPrefix}{roomId}";

    #endregion
}
