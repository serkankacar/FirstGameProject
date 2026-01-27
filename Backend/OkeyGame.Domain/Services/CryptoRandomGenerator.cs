using System.Security.Cryptography;

namespace OkeyGame.Domain.Services;

/// <summary>
/// Kriptografik olarak güvenli, thread-safe rastgele sayı üreteci.
/// System.Security.Cryptography kütüphanesini sarmalayan (wrapper) sınıf.
/// 
/// GÜVENLIK NOTU:
/// - Standart Random sınıfı tahmin edilebilir ve güvenli değildir.
/// - Bu sınıf CSPRNG (Cryptographically Secure Pseudo-Random Number Generator) kullanır.
/// - Thread-safe implementasyon için lock mekanizması kullanılır.
/// - Provably Fair (matematiksel olarak kanıtlanabilir adalet) için gereklidir.
/// </summary>
public sealed class CryptoRandomGenerator : IDisposable
{
    #region Singleton Pattern

    private static readonly Lazy<CryptoRandomGenerator> _instance = 
        new(() => new CryptoRandomGenerator(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Thread-safe singleton instance'a erişim.
    /// </summary>
    public static CryptoRandomGenerator Instance => _instance.Value;

    #endregion

    #region Alanlar

    private readonly RandomNumberGenerator _rng;
    private readonly object _lock = new();
    private bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Yeni bir kriptografik rastgele sayı üreteci oluşturur.
    /// </summary>
    public CryptoRandomGenerator()
    {
        _rng = RandomNumberGenerator.Create();
    }

    #endregion

    #region Temel Metotlar

    /// <summary>
    /// Belirtilen uzunlukta rastgele byte dizisi üretir.
    /// </summary>
    /// <param name="length">Üretilecek byte sayısı</param>
    /// <returns>Rastgele byte dizisi</returns>
    public byte[] GetRandomBytes(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), 
                "Uzunluk pozitif bir değer olmalıdır.");
        }

        ThrowIfDisposed();

        var buffer = new byte[length];
        lock (_lock)
        {
            _rng.GetBytes(buffer);
        }
        return buffer;
    }

    /// <summary>
    /// 0 ile maxValue (hariç) arasında rastgele tam sayı üretir.
    /// Bias (eğilim) önlenmiş implementasyon.
    /// </summary>
    /// <param name="maxValue">Üst sınır (dahil değil)</param>
    /// <returns>Rastgele tam sayı [0, maxValue)</returns>
    public int NextInt(int maxValue)
    {
        if (maxValue <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxValue), 
                "Maksimum değer pozitif olmalıdır.");
        }

        ThrowIfDisposed();

        // Modulo bias'ı önlemek için rejection sampling kullanıyoruz
        // Bu, dağılımın tamamen uniform olmasını sağlar
        uint range = (uint)maxValue;
        uint threshold = (uint.MaxValue - range + 1) % range;

        uint result;
        do
        {
            result = GetRandomUInt32();
        } while (result < threshold);

        return (int)(result % range);
    }

    /// <summary>
    /// minValue (dahil) ile maxValue (hariç) arasında rastgele tam sayı üretir.
    /// </summary>
    /// <param name="minValue">Alt sınır (dahil)</param>
    /// <param name="maxValue">Üst sınır (dahil değil)</param>
    /// <returns>Rastgele tam sayı [minValue, maxValue)</returns>
    public int NextInt(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
        {
            throw new ArgumentException(
                "Minimum değer maksimum değerden küçük olmalıdır.");
        }

        ThrowIfDisposed();

        long range = (long)maxValue - minValue;
        if (range > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                "Aralık çok geniş.");
        }

        return minValue + NextInt((int)range);
    }

    /// <summary>
    /// 0.0 ile 1.0 arasında rastgele ondalıklı sayı üretir.
    /// </summary>
    /// <returns>Rastgele double [0.0, 1.0)</returns>
    public double NextDouble()
    {
        ThrowIfDisposed();

        // 53-bit hassasiyetle double üretimi
        ulong value = GetRandomUInt64() >> 11; // 53-bit mantissa
        return value / (double)(1UL << 53);
    }

    /// <summary>
    /// Rastgele boolean değer üretir.
    /// </summary>
    /// <returns>true veya false</returns>
    public bool NextBool()
    {
        ThrowIfDisposed();
        return (GetRandomBytes(1)[0] & 1) == 1;
    }

    #endregion

    #region Yardımcı Metotlar

    /// <summary>
    /// Rastgele unsigned 32-bit tam sayı üretir.
    /// </summary>
    private uint GetRandomUInt32()
    {
        var bytes = GetRandomBytes(4);
        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>
    /// Rastgele unsigned 64-bit tam sayı üretir.
    /// </summary>
    private ulong GetRandomUInt64()
    {
        var bytes = GetRandomBytes(8);
        return BitConverter.ToUInt64(bytes, 0);
    }

    /// <summary>
    /// Nesne dispose edilmişse hata fırlatır.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CryptoRandomGenerator));
        }
    }

    #endregion

    #region Provably Fair Desteği

    /// <summary>
    /// Seed değeri oluşturur. Provably Fair sistemi için kullanılır.
    /// Bu seed, oyun öncesi hash'lenerek oyunculara gösterilir.
    /// </summary>
    /// <returns>32 byte'lık rastgele seed (hex string olarak)</returns>
    public string GenerateServerSeed()
    {
        ThrowIfDisposed();
        var seedBytes = GetRandomBytes(32);
        return Convert.ToHexString(seedBytes);
    }

    /// <summary>
    /// Client seed ve server seed kombinasyonuyla deterministik
    /// rastgele sayı üretir. Doğrulama için kullanılır.
    /// </summary>
    /// <param name="serverSeed">Sunucu seed'i</param>
    /// <param name="clientSeed">İstemci seed'i</param>
    /// <param name="nonce">Tekrarlama önleyici sayaç</param>
    /// <returns>Kombine seed hash'i</returns>
    public static string CombineSeeds(string serverSeed, string clientSeed, long nonce)
    {
        if (string.IsNullOrWhiteSpace(serverSeed))
        {
            throw new ArgumentException("Server seed boş olamaz.", nameof(serverSeed));
        }

        if (string.IsNullOrWhiteSpace(clientSeed))
        {
            throw new ArgumentException("Client seed boş olamaz.", nameof(clientSeed));
        }

        var combined = $"{serverSeed}:{clientSeed}:{nonce}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _rng.Dispose();
                _disposed = true;
            }
        }
    }

    #endregion
}
