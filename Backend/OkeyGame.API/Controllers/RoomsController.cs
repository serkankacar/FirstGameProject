using Microsoft.AspNetCore.Mvc;
using OkeyGame.API.Services;

namespace OkeyGame.API.Controllers;

/// <summary>
/// Oda yönetimi REST API controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IGameStateService _gameStateService;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(IGameStateService gameStateService, ILogger<RoomsController> logger)
    {
        _gameStateService = gameStateService;
        _logger = logger;
    }

    /// <summary>
    /// Tüm aktif odaları listele
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<RoomListResponse>> GetRooms()
    {
        try
        {
            var activeRoomIds = await _gameStateService.GetActiveRoomIdsAsync();
            var rooms = new List<RoomDto>();

            foreach (var roomId in activeRoomIds)
            {
                var roomState = await _gameStateService.GetRoomStateAsync(roomId);
                if (roomState != null)
                {
                    rooms.Add(new RoomDto
                    {
                        Id = roomState.RoomId,
                        Name = roomState.RoomName,
                        Stake = roomState.Stake,
                        CurrentPlayerCount = roomState.Players.Count,
                        MaxPlayers = 4,
                        IsGameStarted = roomState.IsGameStarted
                    });
                }
            }

            return Ok(new RoomListResponse { Rooms = rooms });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Odalar listelenirken hata oluştu");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Belirli bir odanın detaylarını getir
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RoomDto>> GetRoom(Guid id)
    {
        var roomState = await _gameStateService.GetRoomStateAsync(id);
        
        if (roomState == null)
        {
            return NotFound(new { Error = "Room not found" });
        }

        return Ok(new RoomDto
        {
            Id = roomState.RoomId,
            Name = roomState.RoomName,
            Stake = roomState.Stake,
            CurrentPlayerCount = roomState.Players.Count,
            MaxPlayers = 4,
            IsGameStarted = roomState.IsGameStarted
        });
    }
}

public class RoomListResponse
{
    public List<RoomDto> Rooms { get; set; } = new();
}

public class RoomDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Stake { get; set; }
    public int CurrentPlayerCount { get; set; }
    public int MaxPlayers { get; set; } = 4;
    public bool IsGameStarted { get; set; }
}
