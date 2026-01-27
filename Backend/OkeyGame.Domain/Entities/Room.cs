using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.Entities;

/// <summary>
/// Oyun odasını temsil eden sınıf.
/// Masa durumu, oyuncular ve oyun kurallarını yönetir.
/// </summary>
public class Room
{
    #region Sabitler

    /// <summary>Bir odadaki maksimum oyuncu sayısı</summary>
    public const int MaxPlayers = 4;

    /// <summary>Oyun başlatmak için gereken minimum oyuncu sayısı</summary>
    public const int MinPlayersToStart = 4;

    #endregion

    #region Özellikler

    /// <summary>
    /// Odanın benzersiz kimlik numarası.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Odanın görünen adı.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Odadaki oyuncular.
    /// </summary>
    public List<Player> Players { get; }

    /// <summary>
    /// Oyunun mevcut durumu.
    /// </summary>
    public GameState State { get; private set; }

    /// <summary>
    /// Sıradaki oyuncunun pozisyonu.
    /// </summary>
    public PlayerPosition CurrentTurnPosition { get; private set; }

    /// <summary>
    /// Odanın oluşturulma zamanı.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Odanın son güncelleme zamanı.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Oda özel mi (şifreli).
    /// </summary>
    public bool IsPrivate { get; private set; }

    /// <summary>
    /// Oda şifresi (özel odalar için).
    /// </summary>
    public string? Password { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Yeni bir oda oluşturur.
    /// </summary>
    /// <param name="name">Oda adı</param>
    /// <param name="isPrivate">Özel oda mı?</param>
    /// <param name="password">Oda şifresi (özel odalar için)</param>
    public Room(string name, bool isPrivate = false, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Oda adı boş olamaz.", nameof(name));
        }

        if (isPrivate && string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Özel odalar için şifre zorunludur.", nameof(password));
        }

        Id = Guid.NewGuid();
        Name = name;
        Players = new List<Player>(MaxPlayers);
        State = GameState.WaitingForPlayers;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsPrivate = isPrivate;
        Password = password;
        CurrentTurnPosition = PlayerPosition.South; // Varsayılan başlangıç
    }

    #endregion

    #region Oyuncu Yönetimi

    /// <summary>
    /// Odaya yeni bir oyuncu ekler.
    /// </summary>
    /// <param name="player">Eklenecek oyuncu</param>
    /// <returns>Ekleme başarılı mı?</returns>
    public bool AddPlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        // Oda dolu mu kontrolü
        if (Players.Count >= MaxPlayers)
        {
            return false;
        }

        // Oyun başladıysa yeni oyuncu eklenemez
        if (State != GameState.WaitingForPlayers)
        {
            return false;
        }

        // Aynı ID'ye sahip oyuncu var mı kontrolü
        if (Players.Any(p => p.Id == player.Id))
        {
            return false;
        }

        Players.Add(player);
        UpdateTimestamp();
        return true;
    }

    /// <summary>
    /// Odadan bir oyuncuyu çıkarır.
    /// </summary>
    /// <param name="playerId">Çıkarılacak oyuncunun ID'si</param>
    /// <returns>Çıkarma başarılı mı?</returns>
    public bool RemovePlayer(Guid playerId)
    {
        var player = Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null)
        {
            return false;
        }

        Players.Remove(player);
        UpdateTimestamp();

        // Oyun devam ediyorsa ve yeterli oyuncu kalmadıysa iptal et
        if (State == GameState.InProgress && Players.Count < MinPlayersToStart)
        {
            State = GameState.Cancelled;
        }

        return true;
    }

    /// <summary>
    /// Belirtilen ID'ye sahip oyuncuyu bulur.
    /// </summary>
    /// <param name="playerId">Aranan oyuncu ID'si</param>
    /// <returns>Bulunan oyuncu veya null</returns>
    public Player? GetPlayer(Guid playerId)
    {
        return Players.FirstOrDefault(p => p.Id == playerId);
    }

    /// <summary>
    /// Belirtilen bağlantı ID'sine sahip oyuncuyu bulur.
    /// </summary>
    /// <param name="connectionId">SignalR bağlantı ID'si</param>
    /// <returns>Bulunan oyuncu veya null</returns>
    public Player? GetPlayerByConnection(string connectionId)
    {
        return Players.FirstOrDefault(p => p.ConnectionId == connectionId);
    }

    /// <summary>
    /// Belirtilen pozisyondaki oyuncuyu döndürür.
    /// </summary>
    /// <param name="position">Oyuncu pozisyonu</param>
    /// <returns>Bulunan oyuncu veya null</returns>
    public Player? GetPlayerByPosition(PlayerPosition position)
    {
        return Players.FirstOrDefault(p => p.Position == position);
    }

    /// <summary>
    /// Odanın dolu olup olmadığını kontrol eder.
    /// </summary>
    public bool IsFull => Players.Count >= MaxPlayers;

    /// <summary>
    /// Oyunun başlatılıp başlatılamayacağını kontrol eder.
    /// </summary>
    public bool CanStartGame => Players.Count >= MinPlayersToStart && 
                                 State == GameState.WaitingForPlayers;

    #endregion

    #region Oyun Durumu Yönetimi

    /// <summary>
    /// Oyun durumunu günceller.
    /// </summary>
    /// <param name="newState">Yeni durum</param>
    public void SetState(GameState newState)
    {
        // Geçersiz durum geçişlerini kontrol et
        if (!IsValidStateTransition(State, newState))
        {
            throw new InvalidOperationException(
                $"Geçersiz durum geçişi: {State} -> {newState}");
        }

        State = newState;
        UpdateTimestamp();
    }

    /// <summary>
    /// Durum geçişinin geçerli olup olmadığını kontrol eder.
    /// </summary>
    private static bool IsValidStateTransition(GameState current, GameState next)
    {
        return (current, next) switch
        {
            (GameState.WaitingForPlayers, GameState.Shuffling) => true,
            (GameState.Shuffling, GameState.Dealing) => true,
            (GameState.Dealing, GameState.InProgress) => true,
            (GameState.InProgress, GameState.Finished) => true,
            (_, GameState.Cancelled) => true, // Her durumdan iptal edilebilir
            _ => false
        };
    }

    #endregion

    #region Sıra Yönetimi

    /// <summary>
    /// Sırayı bir sonraki oyuncuya geçirir.
    /// Saat yönünde döner.
    /// </summary>
    public void AdvanceTurn()
    {
        // Mevcut oyuncunun sırasını bitir
        var currentPlayer = GetPlayerByPosition(CurrentTurnPosition);
        currentPlayer?.EndTurn();

        // Sırayı bir sonraki pozisyona geçir (saat yönünde)
        CurrentTurnPosition = (PlayerPosition)(((int)CurrentTurnPosition + 1) % 4);

        // Yeni oyuncunun sırasını başlat
        var nextPlayer = GetPlayerByPosition(CurrentTurnPosition);
        nextPlayer?.StartTurn();

        UpdateTimestamp();
    }

    /// <summary>
    /// Başlangıç sırasını belirler.
    /// </summary>
    /// <param name="position">Başlayacak oyuncunun pozisyonu</param>
    public void SetStartingPlayer(PlayerPosition position)
    {
        CurrentTurnPosition = position;
        var startingPlayer = GetPlayerByPosition(position);
        startingPlayer?.StartTurn();
        UpdateTimestamp();
    }

    /// <summary>
    /// Sıranın geldiği oyuncuyu döndürür.
    /// </summary>
    public Player? GetCurrentPlayer()
    {
        return GetPlayerByPosition(CurrentTurnPosition);
    }

    #endregion

    #region Yardımcı Metotlar

    /// <summary>
    /// Güncelleme zamanını günceller.
    /// </summary>
    private void UpdateTimestamp()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Oda şifresini doğrular.
    /// </summary>
    /// <param name="password">Kontrol edilecek şifre</param>
    public bool ValidatePassword(string? password)
    {
        if (!IsPrivate) return true;
        return Password == password;
    }

    public override string ToString()
    {
        return $"Oda: {Name} ({Players.Count}/{MaxPlayers} oyuncu, Durum: {State})";
    }

    #endregion
}
