using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace OkeyGame.Unity.Networking
{
    /// <summary>
    /// Unity için SignalR bağlantı yöneticisi.
    /// GameHub ile iletişim kurar.
    /// 
    /// KULLANIM:
    /// 1. NuGet'ten Microsoft.AspNetCore.SignalR.Client paketi yükleyin
    /// 2. GameNetworkManager.Instance.ConnectAsync() çağırın
    /// 3. Event'lere abone olun (OnGameStarted, OnTileDiscarded, vb.)
    /// 
    /// NOT: Bu dosya SignalR Client kütüphanesi gerektirir.
    /// Unity'de com.unity.nuget.mono-cecil ile veya 
    /// DLL olarak import edilebilir.
    /// </summary>
    public class GameNetworkManager : MonoBehaviour
    {
        #region Singleton

        private static GameNetworkManager _instance;
        public static GameNetworkManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameNetworkManager");
                    _instance = go.AddComponent<GameNetworkManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        #endregion

        #region Ayarlar

        [Header("Bağlantı Ayarları")]
        [SerializeField] private string _serverUrl = "https://localhost:5001";
        [SerializeField] private float _reconnectionDelay = 2f;
        [SerializeField] private int _maxReconnectionAttempts = 10;

        #endregion

        #region Durum

        public Guid PlayerId { get; private set; }
        public Guid? CurrentRoomId { get; private set; }
        public bool IsConnected { get; private set; }
        public string ConnectionState { get; private set; } = "Disconnected";

        private int _reconnectionAttempts;

        #endregion

        #region Events - Bağlantı

        /// <summary>Bağlantı kuruldu.</summary>
        public event Action OnConnected;
        
        /// <summary>Bağlantı koptu.</summary>
        public event Action<string> OnDisconnected;
        
        /// <summary>Yeniden bağlanma deneniyor.</summary>
        public event Action<int, int> OnReconnecting;
        
        /// <summary>Yeniden bağlandı.</summary>
        public event Action<ReconnectedData> OnReconnected;

        #endregion

        #region Events - Oda

        /// <summary>Oda oluşturuldu.</summary>
        public event Action<RoomCreatedData> OnRoomCreated;
        
        /// <summary>Odaya katılındı.</summary>
        public event Action<RoomJoinedData> OnRoomJoined;
        
        /// <summary>Odadan ayrılındı.</summary>
        public event Action<Guid> OnRoomLeft;
        
        /// <summary>Yeni oyuncu katıldı.</summary>
        public event Action<PlayerJoinedData> OnPlayerJoined;
        
        /// <summary>Oyuncu ayrıldı.</summary>
        public event Action<Guid> OnPlayerLeft;

        #endregion

        #region Events - Oyun

        /// <summary>Oyun başladı.</summary>
        public event Action<GameStartedData> OnGameStarted;
        
        /// <summary>Taş çekildi (sadece kendimiz).</summary>
        public event Action<TileDrawnData> OnTileDrawn;
        
        /// <summary>Rakip taş çekti.</summary>
        public event Action<OpponentDrewData> OnOpponentDrewTile;
        
        /// <summary>Taş atıldı.</summary>
        public event Action<TileDiscardedData> OnTileDiscarded;
        
        /// <summary>Deste güncellendi.</summary>
        public event Action<DeckUpdatedData> OnDeckUpdated;
        
        /// <summary>Oyuncu bağlantısı koptu.</summary>
        public event Action<PlayerDisconnectedData> OnPlayerDisconnected;
        
        /// <summary>Oyuncu yeniden bağlandı.</summary>
        public event Action<Guid> OnPlayerReconnected;
        
        /// <summary>Hata oluştu.</summary>
        public event Action<string> OnError;

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

            // Oyuncu ID'sini yükle veya oluştur
            LoadOrCreatePlayerId();
        }

        private void OnDestroy()
        {
            DisconnectAsync().ConfigureAwait(false);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                // Uygulama arka plana geçti
                Debug.Log("[GameNetwork] Uygulama arka plana geçti");
            }
            else
            {
                // Uygulama ön plana geldi - reconnect dene
                Debug.Log("[GameNetwork] Uygulama ön plana geldi, reconnect deneniyor...");
                ReconnectIfNeeded();
            }
        }

        #endregion

        #region Bağlantı Yönetimi

        /// <summary>
        /// Sunucuya bağlanır.
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                ConnectionState = "Connecting";
                Debug.Log($"[GameNetwork] Bağlanılıyor: {_serverUrl}/gamehub");

                // TODO: SignalR HubConnection oluştur
                // _connection = new HubConnectionBuilder()
                //     .WithUrl($"{_serverUrl}/gamehub?playerId={PlayerId}")
                //     .WithAutomaticReconnect(new RetryPolicy())
                //     .Build();
                
                // RegisterHandlers();
                // await _connection.StartAsync();

                IsConnected = true;
                ConnectionState = "Connected";
                _reconnectionAttempts = 0;

                OnConnected?.Invoke();
                Debug.Log("[GameNetwork] Bağlantı başarılı!");

                return true;
            }
            catch (Exception ex)
            {
                ConnectionState = "Error";
                Debug.LogError($"[GameNetwork] Bağlantı hatası: {ex.Message}");
                OnError?.Invoke($"Bağlantı hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Bağlantıyı kapatır.
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                // TODO: await _connection?.StopAsync();
                IsConnected = false;
                ConnectionState = "Disconnected";
                CurrentRoomId = null;
                OnDisconnected?.Invoke("Manual disconnect");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] Disconnect hatası: {ex.Message}");
            }
        }

        private void ReconnectIfNeeded()
        {
            if (!IsConnected && _reconnectionAttempts < _maxReconnectionAttempts)
            {
                StartCoroutine(ReconnectCoroutine());
            }
        }

        private System.Collections.IEnumerator ReconnectCoroutine()
        {
            while (!IsConnected && _reconnectionAttempts < _maxReconnectionAttempts)
            {
                _reconnectionAttempts++;
                OnReconnecting?.Invoke(_reconnectionAttempts, _maxReconnectionAttempts);
                
                Debug.Log($"[GameNetwork] Reconnection denemesi {_reconnectionAttempts}/{_maxReconnectionAttempts}");

                var task = ConnectAsync();
                yield return new WaitUntil(() => task.IsCompleted);

                if (!IsConnected)
                {
                    yield return new WaitForSeconds(_reconnectionDelay);
                }
            }
        }

        #endregion

        #region Oda İşlemleri

        /// <summary>
        /// Yeni oda oluşturur.
        /// </summary>
        public async Task CreateRoomAsync(string roomName, string playerName)
        {
            EnsureConnected();
            Debug.Log($"[GameNetwork] Oda oluşturuluyor: {roomName}");
            
            // TODO: await _connection.InvokeAsync("CreateRoom", roomName, playerName);
        }

        /// <summary>
        /// Odaya katılır.
        /// </summary>
        public async Task JoinRoomAsync(Guid roomId, string playerName)
        {
            EnsureConnected();
            Debug.Log($"[GameNetwork] Odaya katılınıyor: {roomId}");
            
            // TODO: await _connection.InvokeAsync("JoinRoom", roomId, playerName);
        }

        /// <summary>
        /// Odadan ayrılır.
        /// </summary>
        public async Task LeaveRoomAsync()
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log($"[GameNetwork] Odadan ayrılınıyor: {CurrentRoomId}");
            
            // TODO: await _connection.InvokeAsync("LeaveRoom", CurrentRoomId.Value);
        }

        /// <summary>
        /// Oyunu başlatır.
        /// </summary>
        public async Task StartGameAsync()
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log($"[GameNetwork] Oyun başlatılıyor: {CurrentRoomId}");
            
            // TODO: await _connection.InvokeAsync("StartGame", CurrentRoomId.Value);
        }

        #endregion

        #region Oyun Aksiyonları

        /// <summary>
        /// Desteden taş çeker.
        /// </summary>
        public async Task DrawTileAsync()
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log("[GameNetwork] Desteden taş çekiliyor");
            
            // TODO: await _connection.InvokeAsync("DrawTile", CurrentRoomId.Value);
        }

        /// <summary>
        /// Atık yığınından taş çeker.
        /// </summary>
        public async Task DrawFromDiscardAsync()
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log("[GameNetwork] Atıktan taş çekiliyor");
            
            // TODO: await _connection.InvokeAsync("DrawFromDiscard", CurrentRoomId.Value);
        }

        /// <summary>
        /// Taş atar.
        /// </summary>
        public async Task ThrowTileAsync(int tileId)
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log($"[GameNetwork] Taş atılıyor: {tileId}");
            
            // TODO: await _connection.InvokeAsync("ThrowTile", CurrentRoomId.Value, tileId);
        }

        #endregion

        #region Yardımcı Metodlar

        private void LoadOrCreatePlayerId()
        {
            var savedId = PlayerPrefs.GetString("PlayerId", null);
            if (string.IsNullOrEmpty(savedId) || !Guid.TryParse(savedId, out var id))
            {
                PlayerId = Guid.NewGuid();
                PlayerPrefs.SetString("PlayerId", PlayerId.ToString());
                PlayerPrefs.Save();
                Debug.Log($"[GameNetwork] Yeni PlayerId oluşturuldu: {PlayerId}");
            }
            else
            {
                PlayerId = id;
                Debug.Log($"[GameNetwork] PlayerId yüklendi: {PlayerId}");
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Sunucuya bağlı değil!");
            }
        }

        #endregion

        #region SignalR Handler Kayıtları (TODO: Implement)

        /*
        private void RegisterHandlers()
        {
            _connection.On<object>("OnRoomCreated", data => {
                MainThreadDispatcher.Enqueue(() => {
                    var roomData = JsonConvert.DeserializeObject<RoomCreatedData>(data.ToString());
                    CurrentRoomId = roomData.RoomId;
                    OnRoomCreated?.Invoke(roomData);
                });
            });

            _connection.On<object>("OnRoomJoined", data => {
                MainThreadDispatcher.Enqueue(() => {
                    var roomData = JsonConvert.DeserializeObject<RoomJoinedData>(data.ToString());
                    CurrentRoomId = roomData.RoomId;
                    OnRoomJoined?.Invoke(roomData);
                });
            });

            _connection.On<object>("OnGameStarted", data => {
                MainThreadDispatcher.Enqueue(() => {
                    var gameData = JsonConvert.DeserializeObject<GameStartedData>(data.ToString());
                    OnGameStarted?.Invoke(gameData);
                });
            });

            _connection.On<object>("OnTileDrawn", data => {
                MainThreadDispatcher.Enqueue(() => {
                    var tileData = JsonConvert.DeserializeObject<TileDrawnData>(data.ToString());
                    OnTileDrawn?.Invoke(tileData);
                });
            });

            _connection.On<object>("OnTileDiscarded", data => {
                MainThreadDispatcher.Enqueue(() => {
                    var discardData = JsonConvert.DeserializeObject<TileDiscardedData>(data.ToString());
                    OnTileDiscarded?.Invoke(discardData);
                });
            });

            _connection.On<object>("OnError", data => {
                MainThreadDispatcher.Enqueue(() => {
                    var errorData = JsonConvert.DeserializeObject<ErrorData>(data.ToString());
                    OnError?.Invoke(errorData.Message);
                });
            });

            _connection.On<object>("OnReconnected", data => {
                MainThreadDispatcher.Enqueue(() => {
                    var reconnectData = JsonConvert.DeserializeObject<ReconnectedData>(data.ToString());
                    CurrentRoomId = reconnectData.RoomId;
                    OnReconnected?.Invoke(reconnectData);
                });
            });

            // ... diğer handler'lar
        }
        */

        #endregion
    }

    #region Data Classes

    [Serializable]
    public class RoomCreatedData
    {
        public Guid RoomId;
        public string RoomName;
        public int Position;
        public string CommitmentHash;
    }

    [Serializable]
    public class RoomJoinedData
    {
        public Guid RoomId;
        public string RoomName;
        public int Position;
        public List<PlayerInfo> Players;
        public string CommitmentHash;
    }

    [Serializable]
    public class PlayerInfo
    {
        public Guid PlayerId;
        public string DisplayName;
        public int Position;
        public bool IsConnected;
    }

    [Serializable]
    public class PlayerJoinedData
    {
        public Guid PlayerId;
        public string PlayerName;
        public int Position;
        public int TotalPlayers;
    }

    [Serializable]
    public class GameStartedData
    {
        public Guid RoomId;
        public GameStateData InitialState;
        public string ServerSeedHash;
    }

    [Serializable]
    public class GameStateData
    {
        public Guid RoomId;
        public int State;
        public int CurrentTurnPosition;
        public PlayerData Self;
        public List<OpponentData> Opponents;
        public TileData IndicatorTile;
        public int RemainingTileCount;
        public TileData DiscardPileTopTile;
    }

    [Serializable]
    public class PlayerData
    {
        public Guid Id;
        public string DisplayName;
        public int Position;
        public List<TileData> Hand;
        public bool IsCurrentTurn;
        public bool IsConnected;
    }

    [Serializable]
    public class OpponentData
    {
        public Guid Id;
        public string DisplayName;
        public int Position;
        public int TileCount;
        public bool IsCurrentTurn;
        public bool IsConnected;
    }

    [Serializable]
    public class TileData
    {
        public int Id;
        public int Color;
        public int Value;
        public bool IsOkey;
        public bool IsFalseJoker;
    }

    [Serializable]
    public class TileDrawnData
    {
        public TileData Tile;
        public bool FromDiscard;
        public string Timestamp;
    }

    [Serializable]
    public class OpponentDrewData
    {
        public Guid PlayerId;
        public bool FromDiscard;
        public string Timestamp;
    }

    [Serializable]
    public class TileDiscardedData
    {
        public Guid PlayerId;
        public int TileId;
        public Guid NextTurnPlayerId;
        public int NextTurnPosition;
        public string Timestamp;
    }

    [Serializable]
    public class DeckUpdatedData
    {
        public int RemainingTileCount;
        public int DiscardPileCount;
    }

    [Serializable]
    public class PlayerDisconnectedData
    {
        public Guid PlayerId;
        public int ReconnectionTimeoutSeconds;
        public string Timestamp;
    }

    [Serializable]
    public class ReconnectedData
    {
        public Guid RoomId;
        public GameStateData GameState;
        public string Message;
    }

    #endregion
}
