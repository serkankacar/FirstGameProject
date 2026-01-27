using System;
using System.Security.Cryptography;
using System.Text;

namespace OkeyGame.Unity.ProvablyFair
{
    /// <summary>
    /// Unity istemcisi için Provably Fair doğrulama sınıfı.
    /// Sunucu ile aynı algoritmayı kullanarak hash doğrulaması yapar.
    /// 
    /// KULLANIM:
    /// 1. Oyun başında sunucudan CommitmentHash al ve sakla
    /// 2. Oyun sonunda sunucudan ServerSeed, InitialState, Nonce al
    /// 3. ProvablyFairVerifier.Verify() ile doğrula
    /// 4. Sonuç true ise oyun adildi, false ise hile var demektir
    /// </summary>
    public static class ProvablyFairVerifier
    {
        #region Doğrulama

        /// <summary>
        /// Sunucudan gelen verileri doğrular.
        /// </summary>
        /// <param name="serverSeed">Sunucu seed'i</param>
        /// <param name="initialState">Taşların başlangıç dizilişi (JSON)</param>
        /// <param name="nonce">Oyun sayacı</param>
        /// <param name="expectedHash">Oyun başında alınan commitment hash</param>
        /// <param name="clientSeed">İstemci seed'i (opsiyonel)</param>
        /// <returns>Doğrulama sonucu</returns>
        public static VerificationResult Verify(
            string serverSeed,
            string initialState,
            long nonce,
            string expectedHash,
            string clientSeed = null)
        {
            if (string.IsNullOrEmpty(serverSeed))
            {
                return VerificationResult.Error("Server seed boş olamaz.");
            }

            if (string.IsNullOrEmpty(initialState))
            {
                return VerificationResult.Error("Initial state boş olamaz.");
            }

            if (string.IsNullOrEmpty(expectedHash))
            {
                return VerificationResult.Error("Expected hash boş olamaz.");
            }

            try
            {
                // Hash'i hesapla
                string computedHash = ComputeHash(serverSeed, initialState, nonce, clientSeed);

                // Karşılaştır (case-insensitive)
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
                        ? "✅ Oyun adil! Hash doğrulaması başarılı." 
                        : "❌ Uyarı! Hash'ler eşleşmiyor, olası hile.",
                    VerifiedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return VerificationResult.Error($"Doğrulama hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// RevealData objesi ile doğrulama yapar.
        /// </summary>
        public static VerificationResult Verify(RevealData revealData)
        {
            if (revealData == null)
            {
                return VerificationResult.Error("Reveal data null olamaz.");
            }

            return Verify(
                revealData.ServerSeed,
                revealData.InitialState,
                revealData.Nonce,
                revealData.CommitmentHash,
                revealData.ClientSeed);
        }

        #endregion

        #region Hash Hesaplama

        /// <summary>
        /// HMAC-SHA256 hash hesaplar.
        /// Sunucu ile aynı algoritmayı kullanır.
        /// </summary>
        /// <param name="serverSeed">Sunucu seed'i (key olarak kullanılır)</param>
        /// <param name="initialState">Başlangıç durumu</param>
        /// <param name="nonce">Oyun sayacı</param>
        /// <param name="clientSeed">İstemci seed'i (opsiyonel)</param>
        /// <returns>Hesaplanan hash (hex lowercase)</returns>
        public static string ComputeHash(
            string serverSeed,
            string initialState,
            long nonce,
            string clientSeed = null)
        {
            // Mesajı oluştur: InitialState:Nonce[:ClientSeed]
            var messageBuilder = new StringBuilder();
            messageBuilder.Append(initialState);
            messageBuilder.Append(':');
            messageBuilder.Append(nonce);

            if (!string.IsNullOrEmpty(clientSeed))
            {
                messageBuilder.Append(':');
                messageBuilder.Append(clientSeed);
            }

            string message = messageBuilder.ToString();

            // HMAC-SHA256 hesapla
            byte[] keyBytes = Encoding.UTF8.GetBytes(serverSeed);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);
                return ByteArrayToHexString(hashBytes).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Byte array'i hex string'e dönüştürür.
        /// Unity eski versiyonları için Convert.ToHexString yerine bu kullanılır.
        /// </summary>
        private static string ByteArrayToHexString(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }

        #endregion

        #region Yardımcı Metotlar

        /// <summary>
        /// İstemci tarafında rastgele seed oluşturur.
        /// </summary>
        /// <returns>Rastgele client seed</returns>
        public static string GenerateClientSeed()
        {
            // Unity'de System.Guid kullanılabilir
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Daha güvenli client seed oluşturur (RNG ile).
        /// </summary>
        /// <param name="length">Byte uzunluğu</param>
        /// <returns>Rastgele client seed</returns>
        public static string GenerateSecureClientSeed(int length = 16)
        {
            byte[] randomBytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return ByteArrayToHexString(randomBytes);
        }

        #endregion
    }

    /// <summary>
    /// Doğrulama sonucu.
    /// </summary>
    public class VerificationResult
    {
        /// <summary>
        /// Doğrulama başarılı mı?
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Hesaplanan hash.
        /// </summary>
        public string ComputedHash { get; set; }

        /// <summary>
        /// Beklenen hash (commitment).
        /// </summary>
        public string ExpectedHash { get; set; }

        /// <summary>
        /// Sonuç mesajı.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Doğrulama zamanı.
        /// </summary>
        public DateTime VerifiedAt { get; set; }

        /// <summary>
        /// Hata sonucu oluşturur.
        /// </summary>
        public static VerificationResult Error(string message)
        {
            return new VerificationResult
            {
                IsValid = false,
                ComputedHash = null,
                ExpectedHash = null,
                Message = message,
                VerifiedAt = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return Message;
        }
    }

    /// <summary>
    /// Sunucudan gelen reveal verileri.
    /// JSON deserialize edilebilir.
    /// </summary>
    [Serializable]
    public class RevealData
    {
        public string ServerSeed;
        public string InitialState;
        public long Nonce;
        public string ClientSeed;
        public string CommitmentHash;
        public string CreatedAt;
        public string RevealedAt;
    }

    /// <summary>
    /// Sunucudan gelen commitment verileri.
    /// Oyun başında alınır ve saklanır.
    /// </summary>
    [Serializable]
    public class CommitmentData
    {
        public string CommitmentHash;
        public long Nonce;
        public string CreatedAt;
        public bool AcceptsClientSeed;
    }
}
