namespace OkeyGame.Domain.Entities;

/// <summary>
/// Kullanıcı entity'si.
/// Oyuncunun kalıcı verilerini (çip, ELO, profil) yönetir.
/// 
/// NOT: Player sınıfı oyun içi geçici state tutar.
/// User sınıfı kalıcı veritabanı entity'sidir.
/// </summary>
public class User
{
    #region Sabitler

    /// <summary>Yeni kullanıcı başlangıç çipi.</summary>
    public const long DefaultChips = 10_000;

    /// <summary>Yeni kullanıcı başlangıç ELO puanı.</summary>
    public const int DefaultEloScore = 1200;

    /// <summary>Minimum ELO puanı (alt sınır).</summary>
    public const int MinEloScore = 100;

    /// <summary>Kullanıcı adı minimum uzunluğu.</summary>
    public const int MinUsernameLength = 3;

    /// <summary>Kullanıcı adı maksimum uzunluğu.</summary>
    public const int MaxUsernameLength = 20;

    #endregion

    #region Primary Key

    /// <summary>
    /// Kullanıcının benzersiz kimlik numarası.
    /// </summary>
    public Guid Id { get; private set; }

    #endregion

    #region Profil Bilgileri

    /// <summary>
    /// Kullanıcı adı (unique).
    /// </summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>
    /// Görünen ad.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Avatar URL'i.
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>
    /// Hesap oluşturulma tarihi.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Son giriş tarihi.
    /// </summary>
    public DateTime LastLoginAt { get; private set; }

    /// <summary>
    /// Hesap aktif mi?
    /// </summary>
    public bool IsActive { get; private set; }

    #endregion

    #region Ekonomi

    /// <summary>
    /// Mevcut çip miktarı.
    /// Thread-safe güncellemeler için veritabanı transaction'ı kullanılmalı.
    /// </summary>
    public long Chips { get; private set; }

    /// <summary>
    /// Toplam kazanılan çip (istatistik için).
    /// </summary>
    public long TotalChipsWon { get; private set; }

    /// <summary>
    /// Toplam kaybedilen çip (istatistik için).
    /// </summary>
    public long TotalChipsLost { get; private set; }

    #endregion

    #region Sıralama

    /// <summary>
    /// ELO puanı (sıralama için).
    /// </summary>
    public int EloScore { get; private set; }

    /// <summary>
    /// En yüksek ELO puanı (tarihi rekor).
    /// </summary>
    public int HighestEloScore { get; private set; }

    /// <summary>
    /// Toplam oyun sayısı.
    /// </summary>
    public int TotalGamesPlayed { get; private set; }

    /// <summary>
    /// Kazanılan oyun sayısı.
    /// </summary>
    public int TotalGamesWon { get; private set; }

    /// <summary>
    /// Kazanma oranı (%).
    /// </summary>
    public double WinRate => TotalGamesPlayed > 0 
        ? (double)TotalGamesWon / TotalGamesPlayed * 100 
        : 0;

    #endregion

    #region Concurrency

    /// <summary>
    /// Optimistic concurrency token.
    /// Eşzamanlı güncellemeleri tespit etmek için.
    /// </summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    #endregion

    #region Constructor

    /// <summary>
    /// EF Core için private constructor.
    /// </summary>
    private User() { }

    /// <summary>
    /// Yeni kullanıcı oluşturur.
    /// </summary>
    public User(string username, string displayName)
    {
        ValidateUsername(username);
        ValidateDisplayName(displayName);

        Id = Guid.NewGuid();
        Username = username.ToLowerInvariant();
        DisplayName = displayName;
        Chips = DefaultChips;
        EloScore = DefaultEloScore;
        HighestEloScore = DefaultEloScore;
        CreatedAt = DateTime.UtcNow;
        LastLoginAt = DateTime.UtcNow;
        IsActive = true;
        TotalGamesPlayed = 0;
        TotalGamesWon = 0;
        TotalChipsWon = 0;
        TotalChipsLost = 0;
    }

    /// <summary>
    /// Mevcut kullanıcıyı yükler (ID ile).
    /// </summary>
    public static User Load(
        Guid id,
        string username,
        string displayName,
        long chips,
        int eloScore,
        int highestEloScore,
        int totalGamesPlayed,
        int totalGamesWon,
        long totalChipsWon,
        long totalChipsLost,
        DateTime createdAt,
        DateTime lastLoginAt,
        bool isActive,
        string? avatarUrl = null)
    {
        return new User
        {
            Id = id,
            Username = username,
            DisplayName = displayName,
            Chips = chips,
            EloScore = eloScore,
            HighestEloScore = highestEloScore,
            TotalGamesPlayed = totalGamesPlayed,
            TotalGamesWon = totalGamesWon,
            TotalChipsWon = totalChipsWon,
            TotalChipsLost = totalChipsLost,
            CreatedAt = createdAt,
            LastLoginAt = lastLoginAt,
            IsActive = isActive,
            AvatarUrl = avatarUrl
        };
    }

    #endregion

    #region Ekonomi İşlemleri

    /// <summary>
    /// Çip ekler (kazanç).
    /// </summary>
    /// <param name="amount">Eklenecek miktar (pozitif olmalı)</param>
    /// <returns>Yeni çip miktarı</returns>
    public long AddChips(long amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Çip miktarı pozitif olmalıdır.", nameof(amount));
        }

        Chips += amount;
        TotalChipsWon += amount;
        return Chips;
    }

    /// <summary>
    /// Çip düşer (kayıp veya harcama).
    /// </summary>
    /// <param name="amount">Düşülecek miktar (pozitif olmalı)</param>
    /// <returns>Yeni çip miktarı</returns>
    /// <exception cref="InvalidOperationException">Yetersiz bakiye</exception>
    public long DeductChips(long amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Çip miktarı pozitif olmalıdır.", nameof(amount));
        }

        if (Chips < amount)
        {
            throw new InvalidOperationException(
                $"Yetersiz bakiye. Mevcut: {Chips}, İstenen: {amount}");
        }

        Chips -= amount;
        TotalChipsLost += amount;
        return Chips;
    }

    /// <summary>
    /// Yeterli çip var mı kontrol eder.
    /// </summary>
    public bool HasSufficientChips(long amount) => Chips >= amount;

    #endregion

    #region ELO İşlemleri

    /// <summary>
    /// ELO puanını günceller.
    /// </summary>
    /// <param name="newScore">Yeni ELO puanı</param>
    public void UpdateEloScore(int newScore)
    {
        // Alt sınır kontrolü
        EloScore = Math.Max(newScore, MinEloScore);

        // Tarihi rekor güncelleme
        if (EloScore > HighestEloScore)
        {
            HighestEloScore = EloScore;
        }
    }

    /// <summary>
    /// ELO puanı değişikliği uygular.
    /// </summary>
    /// <param name="change">Değişim miktarı (pozitif veya negatif)</param>
    public void ApplyEloChange(int change)
    {
        UpdateEloScore(EloScore + change);
    }

    #endregion

    #region Oyun İstatistikleri

    /// <summary>
    /// Oyun sonucu kaydeder.
    /// </summary>
    /// <param name="isWin">Kazandı mı?</param>
    public void RecordGameResult(bool isWin)
    {
        TotalGamesPlayed++;
        if (isWin)
        {
            TotalGamesWon++;
        }
    }

    #endregion

    #region Profil Güncelleme

    /// <summary>
    /// Görünen adı günceller.
    /// </summary>
    public void UpdateDisplayName(string newDisplayName)
    {
        ValidateDisplayName(newDisplayName);
        DisplayName = newDisplayName;
    }

    /// <summary>
    /// Avatar URL'ini günceller.
    /// </summary>
    public void UpdateAvatar(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
    }

    /// <summary>
    /// Son giriş zamanını günceller.
    /// </summary>
    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Hesabı deaktif eder.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Hesabı aktif eder.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    #endregion

    #region Validasyon

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Kullanıcı adı boş olamaz.", nameof(username));
        }

        if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
        {
            throw new ArgumentException(
                $"Kullanıcı adı {MinUsernameLength}-{MaxUsernameLength} karakter arasında olmalıdır.",
                nameof(username));
        }

        // Sadece alfanumerik ve alt çizgi
        if (!username.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException(
                "Kullanıcı adı sadece harf, rakam ve alt çizgi içerebilir.",
                nameof(username));
        }
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Görünen ad boş olamaz.", nameof(displayName));
        }

        if (displayName.Length > MaxUsernameLength * 2)
        {
            throw new ArgumentException(
                $"Görünen ad maksimum {MaxUsernameLength * 2} karakter olabilir.",
                nameof(displayName));
        }
    }

    #endregion
}
