using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OkeyGame.API.Hubs;
using OkeyGame.API.Models;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.StateMachine;

namespace OkeyGame.API.Services;

/// <summary>
/// Tur yönetimi arayüzü.
/// </summary>
public interface ITurnHandlerService
{
    /// <summary>
    /// Oyunun ilk turunu başlatır.
    /// </summary>
    Task StartFirstTurnAsync(Guid roomId, Guid playerId, PlayerPosition position, bool isBot = false);

    /// <summary>
    /// Sırayı sonraki oyuncuya geçirir.
    /// </summary>
    Task AdvanceTurnAsync(Guid roomId);

    /// <summary>
    /// Taş çekme işlemini işler.
    /// </summary>
    Task<TurnActionResult> HandleDrawAsync(Guid roomId, Guid playerId, bool fromDiscard);

    /// <summary>
    /// Taş atma işlemini işler.
    /// </summary>
    Task<TurnActionResult> HandleDiscardAsync(Guid roomId, Guid playerId);

    /// <summary>
    /// Zaman aşımını işler ve auto-play yapar.
    /// </summary>
    Task HandleTimeoutAsync(Guid roomId, Guid playerId, int turnNumber);

    /// <summary>
    /// Oyuncu reconnection'ı işler.
    /// </summary>
    Task HandleReconnectionAsync(Guid roomId, Guid playerId);

    /// <summary>
    /// Oyuncu disconnection'ı işler.
    /// </summary>
    Task HandleDisconnectionAsync(Guid roomId, Guid playerId);
}

/// <summary>
/// Tur yönetimi servisi implementasyonu.
/// 
/// SORUMLULUKLAR:
/// - Tur başlatma ve geçişleri
/// - Timer yönetimi
/// - Auto-play koordinasyonu
/// - SignalR bildirimleri
/// 
/// BAĞIMLILIKLAR:
/// - IGameStateService: Redis state yönetimi
/// - ITurnTimerService: Zamanlayıcı
/// - IHubContext: SignalR bildirimleri
/// </summary>
public sealed class TurnHandlerService : ITurnHandlerService
{
    #region Sabitler

    private const int DefaultTurnDurationSeconds = 15;

    #endregion

    #region Alanlar

    private readonly IGameStateService _stateService;
    private readonly Application.Services.ITurnTimerService _timerService;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<TurnHandlerService> _logger;
    private readonly TurnManager _turnManager;
    private readonly AutoPlayService _autoPlayService;

    #endregion

    #region Constructor

    public TurnHandlerService(
        IGameStateService stateService,
        Application.Services.ITurnTimerService timerService,
        IHubContext<GameHub> hubContext,
        ILogger<TurnHandlerService> logger)
    {
        _stateService = stateService;
        _timerService = timerService;
        _hubContext = hubContext;
        _logger = logger;
        _turnManager = TurnManager.Instance;
        _autoPlayService = AutoPlayService.Instance;

        // Timer eventlerini dinle
        _timerService.OnTimeout += async (sender, args) =>
        {
            await HandleTimeoutAsync(args.RoomId, args.PlayerId, args.TurnNumber);
        };

        _timerService.OnTimerTick += async (sender, args) =>
        {
            await NotifyTimerTickAsync(args.RoomId, args.PlayerId, args.RemainingSeconds);
        };
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Oyunun ilk turunu başlatır.
    /// </summary>
    public async Task StartFirstTurnAsync(Guid roomId, Guid playerId, PlayerPosition position, bool isBot = false)
    {
        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            _logger.LogWarning("İlk tur başlatılamadı: Lock alınamadı. Oda {RoomId}", roomId);
            return;
        }

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            _logger.LogWarning("İlk tur başlatılamadı: Oda bulunamadı. Oda {RoomId}", roomId);
            return;
        }

        // State güncelle
        var now = DateTime.UtcNow;
        state.CurrentTurnPlayerId = playerId;
        state.CurrentTurnPosition = position;
        state.TurnNumber = 1;
        state.TurnPhase = (int)TurnPhase.WaitingForDiscard; // İlk oyuncu 15 taş aldı
        state.TurnStartedAt = now;
        state.TurnExpiresAt = now.AddSeconds(DefaultTurnDurationSeconds);
        state.HasDrawnThisTurn = true; // İlk oyuncu çekmiş sayılır
        state.GamePhase = (int)GamePhase.Playing;

        await _stateService.SaveRoomStateAsync(state);

        // Timer başlat
        _timerService.StartTimer(roomId, playerId, 1, DefaultTurnDurationSeconds);

        // Bildirimi gönder
        var player = state.Players.GetValueOrDefault(playerId);
        await NotifyTurnChangedAsync(state, player);

        _logger.LogInformation(
            "İlk tur başlatıldı: Oda {RoomId}, Oyuncu {PlayerId}, Pozisyon {Position}",
            roomId, playerId, position);
    }

    /// <summary>
    /// Sırayı sonraki oyuncuya geçirir.
    /// </summary>
    public async Task AdvanceTurnAsync(Guid roomId)
    {
        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null) return;

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null) return;

        // Sonraki pozisyonu hesapla
        var nextPosition = _turnManager.GetNextPosition(state.CurrentTurnPosition);

        // Sonraki oyuncuyu bul
        var nextPlayer = state.Players.Values
            .FirstOrDefault(p => p.Position == nextPosition);

        if (nextPlayer == null)
        {
            _logger.LogError("Sonraki oyuncu bulunamadı: Oda {RoomId}, Pozisyon {Position}", roomId, nextPosition);
            return;
        }

        // State güncelle
        var now = DateTime.UtcNow;
        state.CurrentTurnPlayerId = nextPlayer.PlayerId;
        state.CurrentTurnPosition = nextPosition;
        state.TurnNumber++;
        state.TurnPhase = (int)TurnPhase.WaitingForDraw;
        state.TurnStartedAt = now;
        state.TurnExpiresAt = now.AddSeconds(DefaultTurnDurationSeconds);
        state.HasDrawnThisTurn = false;
        state.IsAutoPlay = false;

        // Önceki oyuncunun durumunu güncelle
        if (state.CurrentTurnPlayerId.HasValue)
        {
            var prevPlayer = state.Players.GetValueOrDefault(state.CurrentTurnPlayerId.Value);
            if (prevPlayer != null)
            {
                prevPlayer.IsCurrentTurn = false;
                prevPlayer.HasDrawnThisTurn = false;
            }
        }

        // Yeni oyuncunun durumunu güncelle
        nextPlayer.IsCurrentTurn = true;

        await _stateService.SaveRoomStateAsync(state);

        // Timer'ı yeniden başlat
        _timerService.StartTimer(roomId, nextPlayer.PlayerId, state.TurnNumber, DefaultTurnDurationSeconds);

        // Bildirimi gönder
        await NotifyTurnChangedAsync(state, nextPlayer);

        _logger.LogDebug(
            "Sıra geçti: Oda {RoomId}, Oyuncu {PlayerId}, Tur {TurnNumber}",
            roomId, nextPlayer.PlayerId, state.TurnNumber);
    }

    /// <summary>
    /// Taş çekme işlemini işler.
    /// </summary>
    public async Task<TurnActionResult> HandleDrawAsync(Guid roomId, Guid playerId, bool fromDiscard)
    {
        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return TurnActionResult.Failure("Oda bulunamadı.");
        }

        // Sıra kontrolü
        if (state.CurrentTurnPlayerId != playerId)
        {
            return TurnActionResult.Failure("Sıra sizde değil.");
        }

        // Faz kontrolü
        if (state.TurnPhase != (int)TurnPhase.WaitingForDraw)
        {
            return TurnActionResult.Failure("Taş çekme aşaması değil.");
        }

        // Zaten çektiyse
        if (state.HasDrawnThisTurn)
        {
            return TurnActionResult.Failure("Bu turda zaten taş çektiniz.");
        }

        // State güncelle
        state.TurnPhase = (int)TurnPhase.WaitingForDiscard;
        state.HasDrawnThisTurn = true;

        var player = state.Players.GetValueOrDefault(playerId);
        if (player != null)
        {
            player.HasDrawnThisTurn = true;
        }

        await _stateService.SaveRoomStateAsync(state);

        _logger.LogDebug("Taş çekildi: Oda {RoomId}, Oyuncu {PlayerId}, Discard: {FromDiscard}",
            roomId, playerId, fromDiscard);

        // TurnContext oluştur ve döndür
        var context = TurnContext.StartNew(
            roomId, playerId, state.CurrentTurnPosition,
            state.TurnNumber, false, true, DefaultTurnDurationSeconds)
            .WithTileDrawn(fromDiscard);

        return TurnActionResult.Success(context, "Taş çekildi.");
    }

    /// <summary>
    /// Taş atma işlemini işler.
    /// </summary>
    public async Task<TurnActionResult> HandleDiscardAsync(Guid roomId, Guid playerId)
    {
        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null)
        {
            return TurnActionResult.Failure("Oda bulunamadı.");
        }

        // Sıra kontrolü
        if (state.CurrentTurnPlayerId != playerId)
        {
            return TurnActionResult.Failure("Sıra sizde değil.");
        }

        // Faz kontrolü
        if (state.TurnPhase != (int)TurnPhase.WaitingForDiscard)
        {
            return TurnActionResult.Failure("Önce taş çekmelisiniz.");
        }

        // Taş çekmeden atılamaz
        if (!state.HasDrawnThisTurn)
        {
            return TurnActionResult.Failure("Bu turda henüz taş çekmediniz.");
        }

        // State güncelle
        state.TurnPhase = (int)TurnPhase.TurnCompleted;

        await _stateService.SaveRoomStateAsync(state);

        // Timer'ı durdur
        _timerService.StopTimer(roomId);

        _logger.LogDebug("Taş atıldı: Oda {RoomId}, Oyuncu {PlayerId}", roomId, playerId);

        var context = TurnContext.StartNew(
            roomId, playerId, state.CurrentTurnPosition,
            state.TurnNumber, false, true, DefaultTurnDurationSeconds)
            .WithTileDrawn(false)
            .WithTileDiscarded();

        return TurnActionResult.Success(context, "Taş atıldı, sıra sonraki oyuncuya geçiyor.");
    }

    /// <summary>
    /// Zaman aşımını işler.
    /// </summary>
    public async Task HandleTimeoutAsync(Guid roomId, Guid playerId, int turnNumber)
    {
        _logger.LogInformation(
            "Zaman aşımı işleniyor: Oda {RoomId}, Oyuncu {PlayerId}, Tur {TurnNumber}",
            roomId, playerId, turnNumber);

        using var lockHandle = await _stateService.AcquireLockAsync(roomId, TimeSpan.FromSeconds(5));
        if (lockHandle == null) return;

        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null) return;

        // Tur numarası eşleşmeli (eski timeout'ları ignore et)
        if (state.TurnNumber != turnNumber || state.CurrentTurnPlayerId != playerId)
        {
            _logger.LogDebug("Eski timeout ignore edildi: Beklenen tur {Expected}, Mevcut {Current}",
                turnNumber, state.TurnNumber);
            return;
        }

        // Auto-play moduna geç
        state.IsAutoPlay = true;
        await _stateService.SaveRoomStateAsync(state);

        // Auto-play bildirimini gönder
        await _hubContext.NotifyAutoPlayTriggeredAsync(roomId, new AutoPlayTriggeredDto
        {
            PlayerId = playerId,
            Reason = "Timeout"
        });

        // Gerçek auto-play işlemi GameService'te yapılacak
        // Burada sadece state güncellemesi ve bildirim yapıyoruz
        _logger.LogInformation("Auto-play tetiklendi: Oda {RoomId}, Oyuncu {PlayerId}", roomId, playerId);
    }

    /// <summary>
    /// Oyuncu reconnection'ı işler.
    /// </summary>
    public async Task HandleReconnectionAsync(Guid roomId, Guid playerId)
    {
        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null) return;

        var player = state.Players.GetValueOrDefault(playerId);
        if (player == null) return;

        player.IsConnected = true;
        player.LastConnectedAt = DateTime.UtcNow;
        player.DisconnectedAt = null;

        // Sırası bu oyuncudaysa ve süre kritikse, ek süre ver
        if (state.CurrentTurnPlayerId == playerId)
        {
            var timerInfo = _timerService.GetTimerInfo(roomId);
            if (timerInfo != null && timerInfo.RemainingSeconds < 5)
            {
                _timerService.ExtendTimer(roomId, TurnManager.ReconnectionGraceSeconds);
                
                // Yeni süreyi state'e yansıt
                state.TurnExpiresAt = DateTime.UtcNow.AddSeconds(TurnManager.ReconnectionGraceSeconds);
            }
        }

        await _stateService.SaveRoomStateAsync(state);

        _logger.LogInformation("Oyuncu yeniden bağlandı: Oda {RoomId}, Oyuncu {PlayerId}", roomId, playerId);
    }

    /// <summary>
    /// Oyuncu disconnection'ı işler.
    /// </summary>
    public async Task HandleDisconnectionAsync(Guid roomId, Guid playerId)
    {
        var state = await _stateService.GetRoomStateAsync(roomId);
        if (state == null) return;

        var player = state.Players.GetValueOrDefault(playerId);
        if (player == null) return;

        player.IsConnected = false;
        player.DisconnectedAt = DateTime.UtcNow;

        await _stateService.SaveRoomStateAsync(state);

        _logger.LogInformation("Oyuncu bağlantısı koptu: Oda {RoomId}, Oyuncu {PlayerId}", roomId, playerId);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Sıra değişimi bildirimini gönderir.
    /// </summary>
    private async Task NotifyTurnChangedAsync(GameRoomState state, PlayerState? player)
    {
        if (player == null) return;

        var dto = new TurnChangedDto
        {
            PlayerId = player.PlayerId,
            PlayerName = player.DisplayName,
            Position = (int)player.Position,
            TimeLeft = DefaultTurnDurationSeconds,
            TurnNumber = state.TurnNumber,
            TurnPhase = ((TurnPhase)state.TurnPhase).ToString()
        };

        await _hubContext.NotifyTurnChangedAsync(state.RoomId, dto);
    }

    /// <summary>
    /// Timer tick bildirimini gönderir.
    /// </summary>
    private async Task NotifyTimerTickAsync(Guid roomId, Guid playerId, int remainingSeconds)
    {
        var dto = new TimerTickDto
        {
            PlayerId = playerId,
            TimeLeft = remainingSeconds,
            IsCritical = remainingSeconds <= 10
        };

        await _hubContext.NotifyTimerTickAsync(roomId, dto);
    }

    #endregion
}
