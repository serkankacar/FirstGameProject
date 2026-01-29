using UnityEngine;
using OkeyGame.Network;
using OkeyGame.Game;

namespace OkeyGame.Core
{
    /// <summary>
    /// Oyun başlangıç scripti - Singleton manager'ları başlatır
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameSettings _gameSettings;

        [Header("Manager Prefabs (Optional)")]
        [SerializeField] private GameObject _gameManagerPrefab;
        [SerializeField] private GameObject _apiServicePrefab;
        [SerializeField] private GameObject _signalRPrefab;
        [SerializeField] private GameObject _gameTableControllerPrefab;

        private void Awake()
        {
            // GameSettings'i başlat
            if (_gameSettings != null)
            {
                GameSettings.Instance = _gameSettings;
            }
            else
            {
                // Resources klasöründen yükle
                _gameSettings = Resources.Load<GameSettings>("GameSettings");
                
                if (_gameSettings != null)
                {
                    GameSettings.Instance = _gameSettings;
                    Debug.Log("[Bootstrap] GameSettings Resources'dan yüklendi!");
                }
                else
                {
                    // Runtime'da varsayılan oluştur
                    _gameSettings = ScriptableObject.CreateInstance<GameSettings>();
                    GameSettings.Instance = _gameSettings;
                    Debug.Log("[Bootstrap] GameSettings varsayılan değerlerle oluşturuldu (http://localhost:57392)");
                }
            }

            // Manager'ları oluştur
            CreateManagerIfNeeded<GameManager>(_gameManagerPrefab, "GameManager");
            CreateManagerIfNeeded<ApiService>(_apiServicePrefab, "ApiService");
            CreateManagerIfNeeded<SignalRConnection>(_signalRPrefab, "SignalRConnection");
            CreateManagerIfNeeded<GameTableController>(_gameTableControllerPrefab, "GameTableController");
            
            Debug.Log("[Bootstrap] Game initialized successfully!");
        }

        private void CreateManagerIfNeeded<T>(GameObject prefab, string name) where T : Component
        {
            if (FindAnyObjectByType<T>() != null)
            {
                return; // Zaten var
            }

            GameObject managerObj;
            if (prefab != null)
            {
                managerObj = Instantiate(prefab);
                managerObj.name = name;
            }
            else
            {
                managerObj = new GameObject(name);
                managerObj.AddComponent<T>();
            }

            DontDestroyOnLoad(managerObj);
        }

        /// <summary>
        /// Oyun ayarlarını runtime'da güncelle
        /// </summary>
        public void UpdateServerUrl(string newUrl)
        {
            if (_gameSettings != null)
            {
                // Note: ScriptableObject değerlerini runtime'da değiştirmek 
                // geçici olacaktır, kalıcı için PlayerPrefs kullanın
                Debug.Log($"[Bootstrap] Server URL: {newUrl}");
            }
        }
    }
}
