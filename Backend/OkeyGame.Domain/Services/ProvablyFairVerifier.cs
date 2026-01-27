using System.Security.Cryptography;
using System.Text;
using OkeyGame.Domain.ValueObjects;

namespace OkeyGame.Domain.Services;

/// <summary>
/// Provably Fair sistemini doğrulayan servis.
/// Hem sunucu hem de istemci tarafında kullanılabilir.
/// 
/// DOĞRULAMA SÜRECİ:
/// 1. Oyun sonunda sunucu ServerSeed, InitialState, Nonce açıklar
/// 2. İstemci bu verilerle hash'i yeniden hesaplar
/// 3. Hesaplanan hash, oyun başındaki CommitmentHash ile karşılaştırılır
/// 4. Eşleşiyorsa, oyun adildir (sunucu hile yapmamıştır)
/// </summary>
public static class ProvablyFairVerifier
{
    #region Doğrulama Metotları

    /// <summary>
    /// Açıklanan verilerin commitment hash ile eşleşip eşleşmediğini doğrular.
    /// </summary>
    /// <param name="revealData">Açıklanan veriler</param>
    /// <returns>Doğrulama sonucu</returns>
    public static VerificationResult Verify(RevealData revealData)
    {
        ArgumentNullException.ThrowIfNull(revealData);

        try
        {
            // Hash'i yeniden hesapla
            var computedHash = ComputeHash(
                revealData.ServerSeed,
                revealData.InitialState,
                revealData.Nonce,
                revealData.ClientSeed);

            // Karşılaştır
            bool isValid = string.Equals(
                computedHash, 
                revealData.CommitmentHash, 
                StringComparison.OrdinalIgnoreCase);

            return new VerificationResult
            {
                IsValid = isValid,
                ComputedHash = computedHash,
                ExpectedHash = revealData.CommitmentHash,
                Message = isValid 
                    ? "✅ Doğrulama başarılı! Oyun adildi." 
                    : "❌ Doğrulama başarısız! Hash'ler eşleşmiyor.",
                VerifiedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new VerificationResult
            {
                IsValid = false,
                ComputedHash = null,
                ExpectedHash = revealData.CommitmentHash,
                Message = $"❌ Doğrulama hatası: {ex.Message}",
                VerifiedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Ham verilerle doğrulama yapar.
    /// </summary>
    /// <param name="serverSeed">Sunucu seed'i</param>
    /// <param name="initialState">Başlangıç durumu</param>
    /// <param name="nonce">Oyun sayacı</param>
    /// <param name="expectedHash">Beklenen hash</param>
    /// <param name="clientSeed">İstemci seed'i (opsiyonel)</param>
    /// <returns>Doğrulama sonucu</returns>
    public static VerificationResult Verify(
        string serverSeed,
        string initialState,
        long nonce,
        string expectedHash,
        string? clientSeed = null)
    {
        ArgumentNullException.ThrowIfNull(serverSeed);
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(expectedHash);

        try
        {
            var computedHash = ComputeHash(serverSeed, initialState, nonce, clientSeed);

            bool isValid = string.Equals(
                computedHash, 
                expectedHash, 
                StringComparison.OrdinalIgnoreCase);

            return new VerificationResult
            {
                IsValid = isValid,
                ComputedHash = computedHash,
                ExpectedHash = expectedHash,
                Message = isValid 
                    ? "✅ Doğrulama başarılı! Oyun adildi." 
                    : "❌ Doğrulama başarısız! Hash'ler eşleşmiyor.",
                VerifiedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new VerificationResult
            {
                IsValid = false,
                ComputedHash = null,
                ExpectedHash = expectedHash,
                Message = $"❌ Doğrulama hatası: {ex.Message}",
                VerifiedAt = DateTime.UtcNow
            };
        }
    }

    #endregion

    #region Hash Hesaplama

    /// <summary>
    /// HMAC-SHA256 hash hesaplar.
    /// Bu metot hem sunucu hem de istemci tarafında aynı sonucu üretir.
    /// </summary>
    /// <param name="serverSeed">Sunucu seed'i</param>
    /// <param name="initialState">Başlangıç durumu</param>
    /// <param name="nonce">Oyun sayacı</param>
    /// <param name="clientSeed">İstemci seed'i (opsiyonel)</param>
    /// <returns>Hesaplanan hash (hex string)</returns>
    public static string ComputeHash(
        string serverSeed,
        string initialState,
        long nonce,
        string? clientSeed = null)
    {
        // Mesajı oluştur
        var message = BuildMessage(initialState, nonce, clientSeed);
        
        // ServerSeed'i key olarak kullan
        var key = Encoding.UTF8.GetBytes(serverSeed);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(key);
        var hashBytes = hmac.ComputeHash(messageBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Hash için mesaj string'ini oluşturur.
    /// </summary>
    private static string BuildMessage(string initialState, long nonce, string? clientSeed)
    {
        var sb = new StringBuilder();
        sb.Append(initialState);
        sb.Append(':');
        sb.Append(nonce);
        
        if (!string.IsNullOrEmpty(clientSeed))
        {
            sb.Append(':');
            sb.Append(clientSeed);
        }

        return sb.ToString();
    }

    #endregion

    #region Taş Dizilişi Doğrulama

    /// <summary>
    /// Taş dizilişinin InitialState ile eşleşip eşleşmediğini kontrol eder.
    /// </summary>
    /// <typeparam name="T">Taş tipi</typeparam>
    /// <param name="tiles">Kontrol edilecek taşlar</param>
    /// <param name="initialState">Beklenen diziliş (JSON)</param>
    /// <param name="tileSerializer">Taşı serialize eden fonksiyon</param>
    /// <returns>Eşleşme durumu</returns>
    public static bool VerifyTileOrder<T>(
        IEnumerable<T> tiles,
        string initialState,
        Func<T, object> tileSerializer)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(tileSerializer);

        var tileData = tiles.Select(tileSerializer).ToList();
        var currentState = System.Text.Json.JsonSerializer.Serialize(tileData, 
            new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

        return string.Equals(currentState, initialState, StringComparison.Ordinal);
    }

    #endregion
}

/// <summary>
/// Doğrulama sonucu.
/// </summary>
public sealed class VerificationResult
{
    /// <summary>
    /// Doğrulama başarılı mı?
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Hesaplanan hash.
    /// </summary>
    public string? ComputedHash { get; init; }

    /// <summary>
    /// Beklenen hash (commitment hash).
    /// </summary>
    public required string ExpectedHash { get; init; }

    /// <summary>
    /// Doğrulama mesajı.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Doğrulama zamanı.
    /// </summary>
    public required DateTime VerifiedAt { get; init; }

    public override string ToString()
    {
        return Message;
    }
}
