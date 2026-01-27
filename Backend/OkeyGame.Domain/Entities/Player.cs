using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.Entities;

/// <summary>
/// Oyuncuyu temsil eden sınıf.
/// Oyuncunun elindeki taşları ve oyun durumunu yönetir.
/// </summary>
public class Player
{
    #region Özellikler

    /// <summary>
    /// Oyuncunun benzersiz kimlik numarası.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Oyuncunun görünen adı.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Oyuncunun SignalR bağlantı kimliği.
    /// Gerçek zamanlı iletişim için kullanılır.
    /// </summary>
    public string? ConnectionId { get; private set; }

    /// <summary>
    /// Oyuncunun masa üzerindeki pozisyonu.
    /// </summary>
    public PlayerPosition Position { get; private set; }

    /// <summary>
    /// Oyuncunun elindeki taşlar.
    /// Private set ile dışarıdan doğrudan değiştirilemez.
    /// </summary>
    public List<Tile> Hand { get; private set; }

    /// <summary>
    /// Oyuncunun bağlantı durumu.
    /// Bağlantı kopma senaryolarını yönetmek için kullanılır.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Oyuncunun sırasının gelip gelmediğini belirtir.
    /// </summary>
    public bool IsCurrentTurn { get; private set; }

    /// <summary>
    /// Oyuncunun son aktivite zamanı.
    /// AFK (Away From Keyboard) kontrolü için kullanılır.
    /// </summary>
    public DateTime LastActivityTime { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Yeni bir oyuncu oluşturur.
    /// </summary>
    /// <param name="id">Oyuncu ID'si</param>
    /// <param name="displayName">Görünen ad</param>
    /// <param name="position">Masa pozisyonu</param>
    public Player(Guid id, string displayName, PlayerPosition position)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Oyuncu adı boş olamaz.", nameof(displayName));
        }

        Id = id;
        DisplayName = displayName;
        Position = position;
        Hand = new List<Tile>();
        IsConnected = false;
        IsCurrentTurn = false;
        LastActivityTime = DateTime.UtcNow;
    }

    #endregion

    #region El Yönetimi (Hand Management)

    /// <summary>
    /// Oyuncunun eline taşları ekler.
    /// Dağıtım sırasında kullanılır.
    /// </summary>
    /// <param name="tiles">Eklenecek taşlar</param>
    public void AddTilesToHand(IEnumerable<Tile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        Hand.AddRange(tiles);
        UpdateActivity();
    }

    /// <summary>
    /// Oyuncunun eline tek bir taş ekler.
    /// Taş çekme sırasında kullanılır.
    /// </summary>
    /// <param name="tile">Eklenecek taş</param>
    public void AddTileToHand(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        Hand.Add(tile);
        UpdateActivity();
    }

    /// <summary>
    /// Oyuncunun elinden bir taş çıkarır.
    /// Taş atma sırasında kullanılır.
    /// </summary>
    /// <param name="tileId">Çıkarılacak taşın ID'si</param>
    /// <returns>Çıkarılan taş, bulunamazsa null</returns>
    public Tile? RemoveTileFromHand(int tileId)
    {
        var tile = Hand.FirstOrDefault(t => t.Id == tileId);
        if (tile != null)
        {
            Hand.Remove(tile);
            UpdateActivity();
        }
        return tile;
    }

    /// <summary>
    /// Oyuncunun elini temizler.
    /// Oyun sonu veya yeni oyun başlangıcında kullanılır.
    /// </summary>
    public void ClearHand()
    {
        Hand.Clear();
    }

    /// <summary>
    /// Oyuncunun elindeki taş sayısını döndürür.
    /// </summary>
    public int TileCount => Hand.Count;

    #endregion

    #region Bağlantı Yönetimi

    /// <summary>
    /// Oyuncunun bağlantı bilgilerini günceller.
    /// </summary>
    /// <param name="connectionId">Yeni SignalR bağlantı ID'si</param>
    public void Connect(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            throw new ArgumentException("Bağlantı ID'si boş olamaz.", nameof(connectionId));
        }

        ConnectionId = connectionId;
        IsConnected = true;
        UpdateActivity();
    }

    /// <summary>
    /// Oyuncunun bağlantısını keser.
    /// Bağlantı kopma durumunda çağrılır.
    /// </summary>
    public void Disconnect()
    {
        IsConnected = false;
        // ConnectionId'yi koruyoruz, yeniden bağlanma için
    }

    #endregion

    #region Sıra Yönetimi

    /// <summary>
    /// Oyuncunun sırasını başlatır.
    /// </summary>
    public void StartTurn()
    {
        IsCurrentTurn = true;
        UpdateActivity();
    }

    /// <summary>
    /// Oyuncunun sırasını bitirir.
    /// </summary>
    public void EndTurn()
    {
        IsCurrentTurn = false;
    }

    #endregion

    #region Yardımcı Metotlar

    /// <summary>
    /// Son aktivite zamanını günceller.
    /// </summary>
    private void UpdateActivity()
    {
        LastActivityTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Oyuncunun belirtilen süre içinde aktif olup olmadığını kontrol eder.
    /// </summary>
    /// <param name="timeoutSeconds">Zaman aşımı süresi (saniye)</param>
    public bool IsActive(int timeoutSeconds = 60)
    {
        return (DateTime.UtcNow - LastActivityTime).TotalSeconds < timeoutSeconds;
    }

    public override string ToString()
    {
        return $"{DisplayName} (Pozisyon: {Position}, Taş: {TileCount})";
    }

    #endregion
}
