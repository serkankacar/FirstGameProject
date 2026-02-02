using OkeyGame.Application.DTOs;
using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;
using OkeyGame.Domain.Services;

namespace OkeyGame.Application.Services;

/// <summary>
/// Okey oyunu ana motor sınıfı.
/// Server-Authoritative (Sunucu Yetkili) mimari ile çalışır.
/// 
/// SUNUCU YETKİLİ MİMARİ:
/// - Tüm oyun mantığı sunucuda çalışır
/// - İstemci sadece input gönderir ve sonuçları alır
/// - Hile yapılması imkansız hale getirilir
/// - İstemciye sadece görmesi gereken bilgiler gönderilir
/// </summary>
public class OkeyGameEngine
{
    #region Sabitler

    /// <summary>İlk oyuncunun alacağı taş sayısı (eli açan)</summary>
    private const int FirstPlayerTileCount = 15;

    /// <summary>Diğer oyuncuların alacağı taş sayısı</summary>
    private const int OtherPlayersTileCount = 14;

    /// <summary>Toplam oyuncu sayısı</summary>
    private const int TotalPlayers = 4;

    #endregion

    #region Oyun Durumu

    /// <summary>Oyun odası</summary>
    private Room _room;

    /// <summary>Tüm taşlar (karıştırılmış)</summary>
    private List<Tile> _allTiles;

    /// <summary>Deste (çekilecek taşlar)</summary>
    private Stack<Tile> _deck;

    /// <summary>Atık yığını</summary>
    private Stack<Tile> _discardPile;

    /// <summary>Gösterge taşı</summary>
    private Tile _indicatorTile;

    /// <summary>Oyunun başlama zamanı</summary>
    private DateTime _gameStartedAt;

    /// <summary>Provably Fair için sunucu seed'i</summary>
    private string _serverSeed;

    /// <summary>Sunucu seed'inin hash'i (oyunculara önceden gösterilir)</summary>
    private string _serverSeedHash;

    #endregion

    #region Constructor

    /// <summary>
    /// Yeni bir oyun motoru oluşturur.
    /// </summary>
    /// <param name="room">Oyun odası</param>
    public OkeyGameEngine(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (room.Players.Count != TotalPlayers)
        {
            throw new InvalidOperationException(
                $"Oyun başlatmak için {TotalPlayers} oyuncu gereklidir. " +
                $"Mevcut: {room.Players.Count}");
        }

        _room = room;
        _allTiles = new List<Tile>();
        _deck = new Stack<Tile>();
        _discardPile = new Stack<Tile>();
        _indicatorTile = null!;
        _serverSeed = string.Empty;
        _serverSeedHash = string.Empty;
    }

    #endregion

    #region Oyun Başlatma

    /// <summary>
    /// Oyunu başlatır: taşları oluşturur, karıştırır ve dağıtır.
    /// </summary>
    /// <returns>Başarı durumu ve hata mesajı</returns>
    public (bool Success, string? ErrorMessage) StartGame()
    {
        try
        {
            // 1. Oyun durumunu kontrol et
            if (_room.State != GameState.WaitingForPlayers)
            {
                return (false, "Oyun zaten başlamış veya bitmiş.");
            }

            // 2. Provably Fair için seed oluştur
            GenerateProvablyFairSeeds();

            // 3. Durumu güncelle
            _room.SetState(GameState.Shuffling);

            // 4. Taşları oluştur
            _allTiles = TileFactory.CreateFullSet();

            // 5. Taş setini doğrula
            var validation = TileFactory.ValidateSet(_allTiles);
            if (!validation.IsValid)
            {
                return (false, $"Taş seti geçersiz: {validation.ErrorMessage}");
            }

            // 6. Taşları karıştır (Fisher-Yates + CSPRNG)
            FisherYatesShuffle.Shuffle(_allTiles);

            // 7. Durumu güncelle
            _room.SetState(GameState.Dealing);

            // 8. Gösterge taşını belirle ve Okeyleri işaretle
            DetermineIndicatorAndOkeys();

            // 9. Taşları dağıt
            DealTiles();

            // 10. Desteyi oluştur
            CreateDeck();

            // 11. Oyunu başlat
            _room.SetState(GameState.InProgress);
            _gameStartedAt = DateTime.UtcNow;

            // 12. İlk oyuncunun sırasını başlat (15 taş alan)
            _room.SetStartingPlayer(PlayerPosition.South);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Oyun başlatma hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// Provably Fair için sunucu seed'lerini oluşturur.
    /// </summary>
    private void GenerateProvablyFairSeeds()
    {
        var rng = CryptoRandomGenerator.Instance;
        _serverSeed = rng.GenerateServerSeed();
        
        // Hash'i oluştur (oyunculara önceden gösterilir)
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(_serverSeed));
        _serverSeedHash = Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Gösterge taşını belirler ve Okey taşlarını işaretler.
    /// </summary>
    private void DetermineIndicatorAndOkeys()
    {
        var rng = CryptoRandomGenerator.Instance;

        // Gösterge için rastgele bir taş seç (Sahte Okey olamaz)
        List<Tile> normalTiles = _allTiles.Where(t => !t.IsFalseJoker).ToList();
        int indicatorIndex = rng.NextInt(normalTiles.Count);
        _indicatorTile = normalTiles[indicatorIndex];

        // Okeyleri işaretle
        _allTiles = TileFactory.MarkOkeyTiles(_allTiles, _indicatorTile);
    }

    /// <summary>
    /// Taşları oyunculara Okey kurallarına göre dağıtır.
    /// 
    /// DAĞITIM KURALI:
    /// - 1. oyuncu (eli açan): 15 taş
    /// - Diğer 3 oyuncu: 14'er taş
    /// - Toplam dağıtılan: 15 + 14*3 = 57 taş
    /// - Destede kalan: 106 - 57 - 1(gösterge) = 48 taş
    /// </summary>
    private void DealTiles()
    {
        // Taşları dağıtım için kopyala
        var tilesToDeal = new Queue<Tile>(_allTiles);

        // Oyuncuları pozisyona göre sırala
        var orderedPlayers = _room.Players
            .OrderBy(p => p.Position)
            .ToList();

        for (int i = 0; i < orderedPlayers.Count; i++)
        {
            var player = orderedPlayers[i];
            int tileCount = (i == 0) ? FirstPlayerTileCount : OtherPlayersTileCount;

            var playerTiles = new List<Tile>(tileCount);
            for (int j = 0; j < tileCount; j++)
            {
                if (tilesToDeal.Count > 0)
                {
                    playerTiles.Add(tilesToDeal.Dequeue());
                }
            }

            player.AddTilesToHand(playerTiles);
        }

        // Kalan taşları geçici olarak sakla (deck oluşturma için)
        _allTiles = tilesToDeal.ToList();
    }

    /// <summary>
    /// Dağıtımdan sonra kalan taşlardan deste oluşturur.
    /// </summary>
    private void CreateDeck()
    {
        _deck = new Stack<Tile>(_allTiles);
        _allTiles.Clear(); // Artık referans tutmayalım
    }

    #endregion

    #region Oyun Aksiyonları

    /// <summary>
    /// Desteden taş çeker.
    /// </summary>
    /// <param name="playerId">Taş çeken oyuncunun ID'si</param>
    /// <returns>İşlem sonucu</returns>
    public DrawTileResultDto DrawTile(Guid playerId)
    {
        // Oyuncu kontrolü
        var player = _room.GetPlayer(playerId);
        if (player == null)
        {
            return new DrawTileResultDto
            {
                Success = false,
                ErrorMessage = "Oyuncu bulunamadı."
            };
        }

        // Sıra kontrolü
        if (!player.IsCurrentTurn)
        {
            return new DrawTileResultDto
            {
                Success = false,
                ErrorMessage = "Sıra sizde değil."
            };
        }

        // Deste kontrolü
        if (_deck.Count == 0)
        {
            return new DrawTileResultDto
            {
                Success = false,
                ErrorMessage = "Destede taş kalmadı."
            };
        }

        // Taş çek
        var drawnTile = _deck.Pop();
        player.AddTileToHand(drawnTile);

        return new DrawTileResultDto
        {
            Success = true,
            DrawnTile = MapToTileDto(drawnTile),
            UpdatedState = GetGameStateForPlayer(playerId)
        };
    }

    /// <summary>
    /// Atık yığınından taş çeker (üstteki taşı alır).
    /// </summary>
    /// <param name="playerId">Taş çeken oyuncunun ID'si</param>
    /// <returns>İşlem sonucu</returns>
    public DrawTileResultDto DrawFromDiscard(Guid playerId)
    {
        var player = _room.GetPlayer(playerId);
        if (player == null)
        {
            return new DrawTileResultDto
            {
                Success = false,
                ErrorMessage = "Oyuncu bulunamadı."
            };
        }

        if (!player.IsCurrentTurn)
        {
            return new DrawTileResultDto
            {
                Success = false,
                ErrorMessage = "Sıra sizde değil."
            };
        }

        if (_discardPile.Count == 0)
        {
            return new DrawTileResultDto
            {
                Success = false,
                ErrorMessage = "Atık yığınında taş yok."
            };
        }

        var drawnTile = _discardPile.Pop();
        player.AddTileToHand(drawnTile);

        return new DrawTileResultDto
        {
            Success = true,
            DrawnTile = MapToTileDto(drawnTile),
            UpdatedState = GetGameStateForPlayer(playerId)
        };
    }

    /// <summary>
    /// Taş atar ve sırayı bir sonraki oyuncuya geçirir.
    /// </summary>
    /// <param name="playerId">Taş atan oyuncunun ID'si</param>
    /// <param name="tileId">Atılacak taşın ID'si</param>
    /// <returns>İşlem sonucu</returns>
    public DiscardTileResultDto DiscardTile(Guid playerId, int tileId)
    {
        var player = _room.GetPlayer(playerId);
        if (player == null)
        {
            return new DiscardTileResultDto
            {
                Success = false,
                ErrorMessage = "Oyuncu bulunamadı."
            };
        }

        if (!player.IsCurrentTurn)
        {
            return new DiscardTileResultDto
            {
                Success = false,
                ErrorMessage = "Sıra sizde değil."
            };
        }

        var tile = player.RemoveTileFromHand(tileId);
        if (tile == null)
        {
            return new DiscardTileResultDto
            {
                Success = false,
                ErrorMessage = "Taş elinizde bulunamadı."
            };
        }

        // Atık yığınına ekle
        _discardPile.Push(tile);

        // Sırayı sonraki oyuncuya geçir
        _room.AdvanceTurn();

        return new DiscardTileResultDto
        {
            Success = true,
            UpdatedState = GetGameStateForPlayer(playerId)
        };
    }

    #endregion

    #region DTO Oluşturma

    /// <summary>
    /// Belirtilen oyuncu için oyun durumu DTO'su oluşturur.
    /// GÜVENLİK: Sadece oyuncunun görmesi gereken bilgiler döndürülür.
    /// </summary>
    /// <param name="playerId">Oyuncu ID'si</param>
    /// <returns>Oyuncu için özelleştirilmiş oyun durumu</returns>
    public GameStateDto GetGameStateForPlayer(Guid playerId)
    {
        var player = _room.GetPlayer(playerId);
        if (player == null)
        {
            throw new InvalidOperationException("Oyuncu bulunamadı.");
        }

        // Kendi bilgilerini oluştur (el dahil)
        var selfDto = new PlayerDto
        {
            Id = player.Id,
            DisplayName = player.DisplayName,
            Position = player.Position,
            Hand = player.Hand.Select(MapToTileDto).ToList(),
            IsCurrentTurn = player.IsCurrentTurn,
            IsConnected = player.IsConnected,
            DiscardPileTopTile = null // OkeyGameEngine eski mantığı kullanıyor
        };

        // Rakip bilgilerini oluştur (el HARİÇ - sadece taş sayısı)
        var opponentDtos = _room.Players
            .Where(p => p.Id != playerId)
            .Select(p => new OpponentDto
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                Position = p.Position,
                TileCount = p.TileCount, // Sadece sayı, taşlar DEĞİL
                IsCurrentTurn = p.IsCurrentTurn,
                IsConnected = p.IsConnected,
                DiscardPileTopTile = null // OkeyGameEngine eski mantığı kullanıyor
            })
            .ToList();

        return new GameStateDto
        {
            RoomId = _room.Id,
            State = _room.State,
            CurrentTurnPosition = _room.CurrentTurnPosition,
            Self = selfDto,
            Opponents = opponentDtos,
            IndicatorTile = MapToTileDto(_indicatorTile),
            RemainingTileCount = _deck.Count, // Sadece sayı
            GameStartedAt = _gameStartedAt,
            ServerTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Oyun başlangıç DTO'su oluşturur.
    /// </summary>
    /// <param name="playerId">Oyuncu ID'si</param>
    /// <returns>Oyun başlangıç bilgileri</returns>
    public GameStartDto GetGameStartDto(Guid playerId)
    {
        return new GameStartDto
        {
            RoomId = _room.Id,
            InitialState = GetGameStateForPlayer(playerId),
            ServerSeedHash = _serverSeedHash // Hash gönderilir, seed DEĞİL
        };
    }

    /// <summary>
    /// Tile nesnesini TileDto'ya dönüştürür.
    /// </summary>
    private static TileDto MapToTileDto(Tile tile)
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

    #region Provably Fair

    /// <summary>
    /// Oyun sonunda sunucu seed'ini açıklar.
    /// Bu sayede oyuncular karıştırmanın adil olduğunu doğrulayabilir.
    /// </summary>
    /// <returns>Sunucu seed'i</returns>
    public string RevealServerSeed()
    {
        if (_room.State != GameState.Finished)
        {
            throw new InvalidOperationException(
                "Sunucu seed'i sadece oyun bittikten sonra açıklanabilir.");
        }

        return _serverSeed;
    }

    #endregion

    #region Durum Sorgulama

    /// <summary>
    /// Destede kalan taş sayısını döndürür.
    /// </summary>
    public int RemainingTileCount => _deck.Count;

    /// <summary>
    /// Oyunun devam edip etmediğini kontrol eder.
    /// </summary>
    public bool IsGameInProgress => _room.State == GameState.InProgress;

    /// <summary>
    /// Gösterge taşını döndürür.
    /// </summary>
    public Tile IndicatorTile => _indicatorTile;

    #endregion
}
