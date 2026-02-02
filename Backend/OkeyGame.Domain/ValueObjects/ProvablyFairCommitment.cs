using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OkeyGame.Domain.ValueObjects;

/// <summary>
/// Provably Fair (Kanıtlanabilir Adillik) sistemi için Commitment Value Object.
/// 
/// NASIL ÇALIŞIR?
/// 1. Oyun başında sunucu ServerSeed ve InitialState oluşturur
/// 2. Bu veriler HMAC-SHA256 ile hashlenir → CommitmentHash
/// 3. CommitmentHash oyunculara gösterilir (Commitment)
/// 4. Oyun sonunda ServerSeed ve InitialState açıklanır
/// 5. Oyuncular hash'i yeniden hesaplayarak doğrulama yapar
/// 
/// BU SİSTEM NE SAĞLAR?
/// - Sunucu oyun başlamadan önce taş dizilişini değiştiremez
/// - Oyuncular oyun sonunda her şeyi doğrulayabilir
/// - "Oyun hileli" iddialarına matematiksel kanıt sunulur
/// </summary>
public sealed class ProvablyFairCommitment : IEquatable<ProvablyFairCommitment>
{
    #region Özellikler

    /// <summary>
    /// Sunucu tarafından üretilen benzersiz seed.
    /// Oyun sonuna kadar gizli tutulur.
    /// </summary>
    public Guid ServerSeed { get; }

    /// <summary>
    /// İstemci tarafından sağlanan seed (opsiyonel).
    /// Sunucunun tek başına kontrol edemeyeceği entropi ekler.
    /// </summary>
    public string? ClientSeed { get; private set; }

    /// <summary>
    /// Taşların başlangıç dizilişi (JSON formatında).
    /// </summary>
    public string InitialState { get; }

    /// <summary>
    /// Oyun numarası/sayacı.
    /// Aynı seed'lerle farklı sonuçlar üretmek için kullanılır.
    /// </summary>
    public long Nonce { get; }

    /// <summary>
    /// HMAC-SHA256 ile hesaplanmış commitment hash.
    /// Bu değer oyun başında oyunculara gösterilir.
    /// </summary>
    public string CommitmentHash { get; }

    /// <summary>
    /// Commitment'ın oluşturulma zamanı (UTC).
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Commitment açıklandı mı?
    /// True ise ServerSeed ve InitialState artık görülebilir.
    /// </summary>
    public bool IsRevealed { get; private set; }

    /// <summary>
    /// Açıklanma zamanı (varsa).
    /// </summary>
    public DateTime? RevealedAt { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Yeni bir Provably Fair Commitment oluşturur.
    /// </summary>
    /// <param name="serverSeed">Sunucu seed'i</param>
    /// <param name="initialState">Taşların başlangıç dizilişi</param>
    /// <param name="nonce">Oyun sayacı</param>
    /// <param name="clientSeed">İstemci seed'i (opsiyonel)</param>
    private ProvablyFairCommitment(
        Guid serverSeed, 
        string initialState, 
        long nonce,
        string? clientSeed = null)
    {
        if (string.IsNullOrWhiteSpace(initialState))
        {
            throw new ArgumentException("Initial state boş olamaz.", nameof(initialState));
        }

        ServerSeed = serverSeed;
        InitialState = initialState;
        Nonce = nonce;
        ClientSeed = clientSeed;
        CreatedAt = DateTime.UtcNow;
        IsRevealed = false;

        // Commitment hash'i hesapla
        CommitmentHash = ComputeCommitmentHash();
    }

    #endregion

    #region Factory Metotları

    /// <summary>
    /// Taş listesinden yeni bir commitment oluşturur.
    /// </summary>
    /// <typeparam name="T">Taş tipi</typeparam>
    /// <param name="tiles">Karıştırılmış taş listesi</param>
    /// <param name="nonce">Oyun sayacı</param>
    /// <param name="tileSerializer">Taşı serialize eden fonksiyon</param>
    /// <param name="serverSeed">Özel sunucu seed'i (opsiyonel)</param>
    /// <returns>Yeni commitment</returns>
    public static ProvablyFairCommitment Create<T>(
        IEnumerable<T> tiles,
        long nonce,
        Func<T, object> tileSerializer,
        Guid? serverSeed = null)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(tileSerializer);

        // Kriptografik olarak güvenli ServerSeed oluştur (verilmediyse)
        var actualServerSeed = serverSeed ?? GenerateSecureGuid();

        // Taşları JSON'a serialize et
        var tileData = tiles.Select(tileSerializer).ToList();
        var initialState = JsonSerializer.Serialize(tileData, new JsonSerializerOptions
        {
            WriteIndented = false // Compact JSON
        });

        return new ProvablyFairCommitment(actualServerSeed, initialState, nonce);
    }

    /// <summary>
    /// Ham verilerden commitment oluşturur.
    /// </summary>
    public static ProvablyFairCommitment CreateFromRaw(
        Guid serverSeed,
        string initialState,
        long nonce,
        string? clientSeed = null)
    {
        return new ProvablyFairCommitment(serverSeed, initialState, nonce, clientSeed);
    }

    #endregion

    #region Hash Hesaplama

    /// <summary>
    /// HMAC-SHA256 ile commitment hash'i hesaplar.
    /// 
    /// FORMAT: HMAC-SHA256(ServerSeed, InitialState + ":" + Nonce + ":" + ClientSeed)
    /// </summary>
    private string ComputeCommitmentHash()
    {
        // Mesajı oluştur
        var message = BuildMessage();
        
        // ServerSeed'i key olarak kullan
        var key = Encoding.UTF8.GetBytes(ServerSeed.ToString());
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(key);
        var hashBytes = hmac.ComputeHash(messageBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Hash için mesaj string'ini oluşturur.
    /// </summary>
    private string BuildMessage()
    {
        var sb = new StringBuilder();
        sb.Append(InitialState);
        sb.Append(':');
        sb.Append(Nonce);
        
        if (!string.IsNullOrEmpty(ClientSeed))
        {
            sb.Append(':');
            sb.Append(ClientSeed);
        }

        return sb.ToString();
    }

    #endregion

    #region Client Seed

    /// <summary>
    /// İstemci seed'ini ayarlar.
    /// Sadece oyun başlamadan önce çağrılabilir.
    /// </summary>
    /// <param name="clientSeed">İstemci seed'i</param>
    public ProvablyFairCommitment WithClientSeed(string clientSeed)
    {
        if (IsRevealed)
        {
            throw new InvalidOperationException(
                "Commitment açıklandıktan sonra client seed değiştirilemez.");
        }

        return new ProvablyFairCommitment(ServerSeed, InitialState, Nonce, clientSeed);
    }

    #endregion

    #region Reveal (Açıklama)

    /// <summary>
    /// Commitment'ı açıklar. Oyun sonunda çağrılır.
    /// Bu işlemden sonra ServerSeed ve InitialState görülebilir.
    /// </summary>
    public void Reveal()
    {
        if (IsRevealed)
        {
            throw new InvalidOperationException("Commitment zaten açıklanmış.");
        }

        IsRevealed = true;
        RevealedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Oyun sonunda açıklanacak verileri döndürür.
    /// </summary>
    /// <returns>Açıklama verileri</returns>
    public RevealData GetRevealData()
    {
        if (!IsRevealed)
        {
            throw new InvalidOperationException(
                "Commitment henüz açıklanmadı. Önce Reveal() çağrılmalı.");
        }

        return new RevealData
        {
            ServerSeed = ServerSeed.ToString(),
            InitialState = InitialState,
            Nonce = Nonce,
            ClientSeed = ClientSeed,
            CommitmentHash = CommitmentHash,
            CreatedAt = CreatedAt,
            RevealedAt = RevealedAt!.Value
        };
    }

    #endregion

    #region Yardımcı Metotlar

    /// <summary>
    /// Kriptografik olarak güvenli Guid oluşturur.
    /// </summary>
    private static Guid GenerateSecureGuid()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return new Guid(bytes);
    }

    #endregion

    #region Equality

    public bool Equals(ProvablyFairCommitment? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CommitmentHash == other.CommitmentHash;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ProvablyFairCommitment);
    }

    public override int GetHashCode()
    {
        return CommitmentHash.GetHashCode();
    }

    public override string ToString()
    {
        return $"Commitment: {CommitmentHash[..16]}... (Revealed: {IsRevealed})";
    }

    #endregion
}

/// <summary>
/// Oyun sonunda açıklanan veriler.
/// </summary>
public sealed class RevealData
{
    public required string ServerSeed { get; init; }
    public required string InitialState { get; init; }
    public required long Nonce { get; init; }
    public string? ClientSeed { get; init; }
    public required string CommitmentHash { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime RevealedAt { get; init; }
}
