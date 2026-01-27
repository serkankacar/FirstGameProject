using System;
using UnityEngine;

namespace OkeyGame.Unity.ProvablyFair
{
    /// <summary>
    /// Unity'de Provably Fair sistemini yöneten MonoBehaviour sınıfı.
    /// 
    /// KULLANIM:
    /// 1. Bu script'i bir GameObject'e ekleyin
    /// 2. Oyun başında OnCommitmentReceived() ile commitment'ı alın
    /// 3. Oyun sonunda OnRevealReceived() ile doğrulama yapın
    /// 4. OnVerificationComplete event'ini dinleyerek sonucu gösterin
    /// </summary>
    public class ProvablyFairManager : MonoBehaviour
    {
        #region Singleton

        private static ProvablyFairManager _instance;
        
        public static ProvablyFairManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ProvablyFairManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("ProvablyFairManager");
                        _instance = go.AddComponent<ProvablyFairManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Doğrulama tamamlandığında tetiklenir.
        /// </summary>
        public event Action<VerificationResult> OnVerificationComplete;

        /// <summary>
        /// Commitment alındığında tetiklenir.
        /// </summary>
        public event Action<CommitmentData> OnCommitmentStored;

        #endregion

        #region Özellikler

        /// <summary>
        /// Mevcut oyunun commitment'ı.
        /// </summary>
        public CommitmentData CurrentCommitment { get; private set; }

        /// <summary>
        /// İstemci seed'i.
        /// </summary>
        public string ClientSeed { get; private set; }

        /// <summary>
        /// Son doğrulama sonucu.
        /// </summary>
        public VerificationResult LastVerificationResult { get; private set; }

        /// <summary>
        /// Otomatik client seed oluşturulsun mu?
        /// </summary>
        [SerializeField]
        private bool _autoGenerateClientSeed = true;

        /// <summary>
        /// Doğrulama sonucunu logla.
        /// </summary>
        [SerializeField]
        private bool _logVerificationResult = true;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #endregion

        #region Commitment Yönetimi

        /// <summary>
        /// Sunucudan gelen commitment'ı saklar.
        /// Oyun başında çağrılır.
        /// </summary>
        /// <param name="commitmentJson">Sunucudan gelen JSON</param>
        public void OnCommitmentReceived(string commitmentJson)
        {
            try
            {
                CurrentCommitment = JsonUtility.FromJson<CommitmentData>(commitmentJson);

                // Otomatik client seed oluştur
                if (_autoGenerateClientSeed && CurrentCommitment.AcceptsClientSeed)
                {
                    ClientSeed = ProvablyFairVerifier.GenerateSecureClientSeed();
                    Debug.Log($"[ProvablyFair] Client seed oluşturuldu: {ClientSeed}");
                }

                Debug.Log($"[ProvablyFair] Commitment alındı: {CurrentCommitment.CommitmentHash.Substring(0, 16)}...");

                OnCommitmentStored?.Invoke(CurrentCommitment);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProvablyFair] Commitment parse hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Commitment'ı doğrudan obje olarak ayarlar.
        /// </summary>
        public void SetCommitment(CommitmentData commitment)
        {
            CurrentCommitment = commitment;

            if (_autoGenerateClientSeed && commitment.AcceptsClientSeed)
            {
                ClientSeed = ProvablyFairVerifier.GenerateSecureClientSeed();
            }

            OnCommitmentStored?.Invoke(commitment);
        }

        /// <summary>
        /// Manuel client seed ayarlar.
        /// </summary>
        /// <param name="seed">Client seed</param>
        public void SetClientSeed(string seed)
        {
            ClientSeed = seed;
            Debug.Log($"[ProvablyFair] Client seed ayarlandı: {seed}");
        }

        /// <summary>
        /// Mevcut client seed'i döndürür.
        /// Sunucuya gönderilmek üzere.
        /// </summary>
        public string GetClientSeed()
        {
            return ClientSeed;
        }

        #endregion

        #region Doğrulama

        /// <summary>
        /// Sunucudan gelen reveal verileriyle doğrulama yapar.
        /// Oyun sonunda çağrılır.
        /// </summary>
        /// <param name="revealJson">Sunucudan gelen reveal JSON</param>
        public void OnRevealReceived(string revealJson)
        {
            try
            {
                var revealData = JsonUtility.FromJson<RevealData>(revealJson);
                VerifyGame(revealData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProvablyFair] Reveal parse hatası: {ex.Message}");
                
                LastVerificationResult = VerificationResult.Error($"Parse hatası: {ex.Message}");
                OnVerificationComplete?.Invoke(LastVerificationResult);
            }
        }

        /// <summary>
        /// Reveal verileriyle doğrulama yapar.
        /// </summary>
        public void VerifyGame(RevealData revealData)
        {
            if (CurrentCommitment == null)
            {
                LastVerificationResult = VerificationResult.Error("Commitment bulunamadı. Oyun başında commitment alınmamış.");
                OnVerificationComplete?.Invoke(LastVerificationResult);
                return;
            }

            // Hash kontrolü: Reveal'daki hash, sakladığımız commitment ile aynı mı?
            if (revealData.CommitmentHash != CurrentCommitment.CommitmentHash)
            {
                LastVerificationResult = new VerificationResult
                {
                    IsValid = false,
                    ComputedHash = null,
                    ExpectedHash = CurrentCommitment.CommitmentHash,
                    Message = "❌ Commitment hash'leri eşleşmiyor! Sunucu farklı bir hash göndermiş olabilir.",
                    VerifiedAt = DateTime.UtcNow
                };
                OnVerificationComplete?.Invoke(LastVerificationResult);
                return;
            }

            // Ana doğrulama
            LastVerificationResult = ProvablyFairVerifier.Verify(revealData);

            if (_logVerificationResult)
            {
                if (LastVerificationResult.IsValid)
                {
                    Debug.Log($"[ProvablyFair] ✅ {LastVerificationResult.Message}");
                }
                else
                {
                    Debug.LogWarning($"[ProvablyFair] ❌ {LastVerificationResult.Message}");
                }
            }

            OnVerificationComplete?.Invoke(LastVerificationResult);
        }

        /// <summary>
        /// Manuel parametrelerle doğrulama yapar.
        /// </summary>
        public VerificationResult VerifyManual(
            string serverSeed,
            string initialState,
            long nonce,
            string expectedHash,
            string clientSeed = null)
        {
            var result = ProvablyFairVerifier.Verify(
                serverSeed, 
                initialState, 
                nonce, 
                expectedHash, 
                clientSeed);

            LastVerificationResult = result;
            OnVerificationComplete?.Invoke(result);

            return result;
        }

        #endregion

        #region UI Yardımcıları

        /// <summary>
        /// Kullanıcıya gösterilebilecek özet bilgi döndürür.
        /// </summary>
        public string GetVerificationSummary()
        {
            if (LastVerificationResult == null)
            {
                return "Henüz doğrulama yapılmadı.";
            }

            return LastVerificationResult.IsValid
                ? $"✅ Oyun Adil\nHash: {LastVerificationResult.ComputedHash?.Substring(0, 16)}..."
                : $"❌ Doğrulama Başarısız\n{LastVerificationResult.Message}";
        }

        /// <summary>
        /// Commitment hash'inin kısa halini döndürür.
        /// UI'da göstermek için.
        /// </summary>
        public string GetCommitmentHashShort()
        {
            if (CurrentCommitment == null) return "Yok";
            return CurrentCommitment.CommitmentHash.Substring(0, 16) + "...";
        }

        #endregion

        #region Temizlik

        /// <summary>
        /// Yeni oyun için state'i temizler.
        /// </summary>
        public void Reset()
        {
            CurrentCommitment = null;
            ClientSeed = null;
            LastVerificationResult = null;
            Debug.Log("[ProvablyFair] State temizlendi.");
        }

        #endregion
    }
}
