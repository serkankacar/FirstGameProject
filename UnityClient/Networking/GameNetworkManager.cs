using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace OkeyGame.Unity.Networking
{
    /// <summary>
    /// Unity için SignalR bağlantı yöneticisi.
    /// GameHub ile iletişim kurar.
    /// SignalRWebSocketClient kullanarak WebSocket üzerinden bağlanır.
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
        [SerializeField] private string _serverUrl = "http://localhost:57392";
        [SerializeField] private float _reconnectionDelay = 2f;
        [SerializeField] private int _maxReconnectionAttempts = 10;

        #endregion

        #region Durum

        public Guid PlayerId { get; private set; }
        public Guid? CurrentRoomId { get; private set; }
        public bool IsConnected => _connection?.IsConnected ?? false;
        public string ConnectionState { get; private set; } = "Disconnected";

        private int _reconnectionAttempts;
        private SignalRWebSocketClient _connection;

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

                // Mevcut bağlantıyı temizle
                if (_connection != null)
                {
                    await _connection.DisconnectAsync();
                    _connection = null;
                }

                // Yeni bağlantı oluştur
                _connection = new SignalRWebSocketClient();
                
                // Event handler'ları kaydet
                _connection.OnConnected += () => {
                    Debug.Log("[GameNetwork] SignalR bağlantısı kuruldu");
                    OnConnected?.Invoke();
                };
                
                _connection.OnDisconnected += (reason) => {
                    Debug.Log($"[GameNetwork] SignalR bağlantısı koptu: {reason}");
                    ConnectionState = "Disconnected";
                    OnDisconnected?.Invoke(reason);
                };
                
                _connection.OnError += (error) => {
                    Debug.LogError($"[GameNetwork] SignalR hatası: {error}");
                    OnError?.Invoke(error);
                };
                
                // Hub method handler'larını kaydet
                RegisterHandlers();

                // Bağlan
                var hubUrl = $"{_serverUrl}/gamehub?playerId={PlayerId}";
                var connected = await _connection.ConnectAsync(hubUrl);

                if (connected)
                {
                    ConnectionState = "Connected";
                    _reconnectionAttempts = 0;
                    Debug.Log("[GameNetwork] Bağlantı başarılı!");
                    return true;
                }
                else
                {
                    ConnectionState = "Error";
                    Debug.LogError("[GameNetwork] Bağlantı kurulamadı");
                    return false;
                }
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
        /// SignalR Hub method handler'larını kayıt eder.
        /// </summary>
        private void RegisterHandlers()
        {
            // Oda olayları
            _connection.On("RoomJoined", (argsJson) => {
                var data = ParseRoomJoinedData(argsJson);
                if (data != null)
                {
                    CurrentRoomId = data.RoomId;
                    OnRoomJoined?.Invoke(data);
                }
            });
            
            _connection.On("OnRoomCreated", (argsJson) => {
                var data = ParseRoomCreatedData(argsJson);
                if (data != null)
                {
                    CurrentRoomId = data.RoomId;
                    OnRoomCreated?.Invoke(data);
                }
            });
            
            _connection.On("OnPlayerJoined", (argsJson) => {
                var data = ParsePlayerJoinedData(argsJson);
                if (data != null)
                {
                    OnPlayerJoined?.Invoke(data);
                }
            });
            
            _connection.On("OnPlayerLeft", (argsJson) => {
                var guidMatch = Regex.Match(argsJson, "\"?([0-9a-fA-F-]{36})\"?");
                if (guidMatch.Success && Guid.TryParse(guidMatch.Groups[1].Value, out var playerId))
                {
                    OnPlayerLeft?.Invoke(playerId);
                }
            });
            
            // Oyun olayları
            _connection.On("OnGameStarted", (argsJson) => {
                var data = ParseGameStartedData(argsJson);
                if (data != null)
                {
                    OnGameStarted?.Invoke(data);
                }
            });
            
            _connection.On("OnTileDrawn", (argsJson) => {
                var data = ParseTileDrawnData(argsJson);
                if (data != null)
                {
                    OnTileDrawn?.Invoke(data);
                }
            });
            
            _connection.On("OnTileDiscarded", (argsJson) => {
                var data = ParseTileDiscardedData(argsJson);
                if (data != null)
                {
                    OnTileDiscarded?.Invoke(data);
                }
            });
            
            _connection.On("OnOpponentDrewTile", (argsJson) => {
                var data = ParseOpponentDrewData(argsJson);
                if (data != null)
                {
                    OnOpponentDrewTile?.Invoke(data);
                }
            });
            
            _connection.On("OnDeckUpdated", (argsJson) => {
                var data = ParseDeckUpdatedData(argsJson);
                if (data != null)
                {
                    OnDeckUpdated?.Invoke(data);
                }
            });
            
            _connection.On("OnPlayerDisconnected", (argsJson) => {
                var data = ParsePlayerDisconnectedData(argsJson);
                if (data != null)
                {
                    OnPlayerDisconnected?.Invoke(data);
                }
            });
            
            _connection.On("OnPlayerReconnected", (argsJson) => {
                var guidMatch = Regex.Match(argsJson, "\"PlayerId\":\"([^\"]+)\"");
                if (guidMatch.Success && Guid.TryParse(guidMatch.Groups[1].Value, out var playerId))
                {
                    OnPlayerReconnected?.Invoke(playerId);
                }
            });
            
            _connection.On("OnReconnected", (argsJson) => {
                var data = ParseReconnectedData(argsJson);
                if (data != null)
                {
                    CurrentRoomId = data.RoomId;
                    OnReconnected?.Invoke(data);
                }
            });
            
            _connection.On("OnError", (argsJson) => {
                var msgMatch = Regex.Match(argsJson, "\"(?:Message|message)\":\"([^\"]+)\"");
                var errorMessage = msgMatch.Success ? msgMatch.Groups[1].Value : argsJson;
                OnError?.Invoke(errorMessage);
            });
        }

        /// <summary>
        /// Bağlantıyı kapatır.
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await _connection.DisconnectAsync();
                    _connection = null;
                }
                
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
            
            // stake varsayılan 0
            await _connection.InvokeAsync("CreateRoom", roomName, 0L);
        }

        /// <summary>
        /// Yeni oda oluşturur (stake ile).
        /// </summary>
        public async Task CreateRoomAsync(string roomName, long stake)
        {
            EnsureConnected();
            Debug.Log($"[GameNetwork] Oda oluşturuluyor: {roomName}, Stake: {stake}");
            
            await _connection.InvokeAsync("CreateRoom", roomName, stake);
        }

        /// <summary>
        /// Odaya katılır.
        /// </summary>
        public async Task JoinRoomAsync(Guid roomId, string playerName)
        {
            EnsureConnected();
            Debug.Log($"[GameNetwork] Odaya katılınıyor: {roomId}");
            
            await _connection.InvokeAsync<string>("JoinRoom", roomId.ToString());
        }

        /// <summary>
        /// Odadan ayrılır.
        /// </summary>
        public async Task LeaveRoomAsync()
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log($"[GameNetwork] Odadan ayrılınıyor: {CurrentRoomId}");
            
            await _connection.InvokeAsync<string>("LeaveRoom", CurrentRoomId.Value.ToString());
            CurrentRoomId = null;
        }

        /// <summary>
        /// Oyunu başlatır.
        /// Eksik oyuncu varsa otomatik olarak bot eklenir.
        /// </summary>
        public async Task StartGameAsync()
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log($"[GameNetwork] Oyun başlatılıyor: {CurrentRoomId}");
            
            await _connection.InvokeAsync<string>("StartGame", CurrentRoomId.Value.ToString());
        }

        /// <summary>
        /// Oyunu belirli zorluk seviyesinde botlarla başlatır.
        /// </summary>
        /// <param name="difficulty">Bot zorluk seviyesi (0=Easy, 1=Normal, 2=Hard, 3=Expert)</param>
        public async Task StartGameWithBotsAsync(int difficulty = 1)
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log($"[GameNetwork] Oyun botlarla başlatılıyor: {CurrentRoomId}, Zorluk: {difficulty}");
            
            await _connection.InvokeAsync("StartGameWithBots", CurrentRoomId.Value.ToString(), difficulty);
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
            
            await _connection.InvokeAsync<string>("DrawTile", CurrentRoomId.Value.ToString());
        }

        /// <summary>
        /// Atık yığınından taş çeker.
        /// </summary>
        public async Task DrawFromDiscardAsync()
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log("[GameNetwork] Atıktan taş çekiliyor");
            
            await _connection.InvokeAsync<string>("DrawFromDiscard", CurrentRoomId.Value.ToString());
        }

        /// <summary>
        /// Taş atar.
        /// </summary>
        public async Task ThrowTileAsync(int tileId)
        {
            if (!CurrentRoomId.HasValue) return;
            
            EnsureConnected();
            Debug.Log($"[GameNetwork] Taş atılıyor: {tileId}");
            
            await _connection.InvokeAsync("ThrowTile", CurrentRoomId.Value.ToString(), tileId);
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

        #region JSON Parsing Methods
        
        private RoomJoinedData ParseRoomJoinedData(string json)
        {
            try
            {
                var data = new RoomJoinedData();
                
                // Id veya RoomId parse et
                var idMatch = Regex.Match(json, "\"(?:Id|RoomId)\":\"?([0-9a-fA-F-]+)\"?");
                if (idMatch.Success && Guid.TryParse(idMatch.Groups[1].Value, out var roomId))
                    data.RoomId = roomId;
                
                // RoomName veya Name
                var nameMatch = Regex.Match(json, "\"(?:RoomName|Name)\":\"([^\"]+)\"");
                if (nameMatch.Success)
                    data.RoomName = nameMatch.Groups[1].Value;
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] RoomJoinedData parse error: {ex.Message}");
                return null;
            }
        }
        
        private RoomCreatedData ParseRoomCreatedData(string json)
        {
            try
            {
                var data = new RoomCreatedData();
                
                var idMatch = Regex.Match(json, "\"(?:Id|RoomId)\":\"?([0-9a-fA-F-]+)\"?");
                if (idMatch.Success && Guid.TryParse(idMatch.Groups[1].Value, out var roomId))
                    data.RoomId = roomId;
                
                var nameMatch = Regex.Match(json, "\"(?:RoomName|Name)\":\"([^\"]+)\"");
                if (nameMatch.Success)
                    data.RoomName = nameMatch.Groups[1].Value;
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] RoomCreatedData parse error: {ex.Message}");
                return null;
            }
        }
        
        private PlayerJoinedData ParsePlayerJoinedData(string json)
        {
            try
            {
                var data = new PlayerJoinedData();
                
                var idMatch = Regex.Match(json, "\"PlayerId\":\"([^\"]+)\"");
                if (idMatch.Success && Guid.TryParse(idMatch.Groups[1].Value, out var playerId))
                    data.PlayerId = playerId;
                
                var nameMatch = Regex.Match(json, "\"PlayerName\":\"([^\"]+)\"");
                if (nameMatch.Success)
                    data.PlayerName = nameMatch.Groups[1].Value;
                
                var posMatch = Regex.Match(json, "\"Position\":(\\d+)");
                if (posMatch.Success)
                    data.Position = int.Parse(posMatch.Groups[1].Value);
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] PlayerJoinedData parse error: {ex.Message}");
                return null;
            }
        }
        
        private GameStartedData ParseGameStartedData(string json)
        {
            try
            {
                var data = new GameStartedData();
                
                // RoomId parse
                var idMatch = Regex.Match(json, "\"[Rr]oomId\":\"([^\"]+)\"");
                if (idMatch.Success && Guid.TryParse(idMatch.Groups[1].Value, out var roomId))
                    data.RoomId = roomId;
                
                // ServerSeedHash parse
                var hashMatch = Regex.Match(json, "\"[Ss]erverSeedHash\":\"([^\"]+)\"");
                if (hashMatch.Success)
                    data.ServerSeedHash = hashMatch.Groups[1].Value;
                
                // InitialState parse - nested object
                data.InitialState = ParseGameStateData(json);
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] GameStartedData parse error: {ex.Message}");
                return null;
            }
        }

        private GameStateData ParseGameStateData(string json)
        {
            try
            {
                var state = new GameStateData();
                
                // CurrentTurnPosition
                var turnPosMatch = Regex.Match(json, "\"[Cc]urrentTurnPosition\":(\\d+)");
                if (turnPosMatch.Success)
                    state.CurrentTurnPosition = int.Parse(turnPosMatch.Groups[1].Value);
                
                // RemainingTileCount
                var remainingMatch = Regex.Match(json, "\"[Rr]emainingTileCount\":(\\d+)");
                if (remainingMatch.Success)
                    state.RemainingTileCount = int.Parse(remainingMatch.Groups[1].Value);
                
                // State
                var stateMatch = Regex.Match(json, "\"[Ss]tate\":(\\d+)");
                if (stateMatch.Success)
                    state.State = int.Parse(stateMatch.Groups[1].Value);
                
                // Self player data
                state.Self = ParseSelfPlayerData(json);
                
                // Opponents
                state.Opponents = ParseOpponentsData(json);
                
                // IndicatorTile
                state.IndicatorTile = ParseIndicatorTile(json);
                
                return state;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] GameStateData parse error: {ex.Message}");
                return new GameStateData();
            }
        }

        private PlayerData ParseSelfPlayerData(string json)
        {
            try
            {
                var player = new PlayerData();
                player.Hand = new List<TileData>();
                
                // Self object'i bul
                var selfMatch = Regex.Match(json, "\"[Ss]elf\":\\s*\\{([^}]+(?:\\{[^}]*\\}[^}]*)*)\\}");
                if (!selfMatch.Success)
                    return player;
                
                var selfJson = selfMatch.Groups[1].Value;
                
                // Id
                var idMatch = Regex.Match(selfJson, "\"[Ii]d\":\"([^\"]+)\"");
                if (idMatch.Success && Guid.TryParse(idMatch.Groups[1].Value, out var id))
                    player.Id = id;
                
                // DisplayName
                var nameMatch = Regex.Match(selfJson, "\"[Dd]isplayName\":\"([^\"]+)\"");
                if (nameMatch.Success)
                    player.DisplayName = nameMatch.Groups[1].Value;
                
                // Position
                var posMatch = Regex.Match(selfJson, "\"[Pp]osition\":(\\d+)");
                if (posMatch.Success)
                    player.Position = int.Parse(posMatch.Groups[1].Value);
                
                // IsCurrentTurn
                var turnMatch = Regex.Match(selfJson, "\"[Ii]sCurrentTurn\":(true|false)");
                if (turnMatch.Success)
                    player.IsCurrentTurn = turnMatch.Groups[1].Value == "true";
                
                // IsConnected
                var connMatch = Regex.Match(selfJson, "\"[Ii]sConnected\":(true|false)");
                if (connMatch.Success)
                    player.IsConnected = connMatch.Groups[1].Value == "true";
                
                // Hand tiles - array parsing
                var handMatch = Regex.Match(json, "\"[Hh]and\":\\s*\\[([^\\]]*)\\]");
                if (handMatch.Success)
                {
                    var tilesJson = handMatch.Groups[1].Value;
                    var tileMatches = Regex.Matches(tilesJson, "\\{([^}]+)\\}");
                    
                    foreach (Match tileMatch in tileMatches)
                    {
                        var tile = ParseSingleTile(tileMatch.Groups[1].Value);
                        if (tile != null)
                            player.Hand.Add(tile);
                    }
                }
                
                return player;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] ParseSelfPlayerData error: {ex.Message}");
                return new PlayerData { Hand = new List<TileData>() };
            }
        }

        private List<OpponentData> ParseOpponentsData(string json)
        {
            var opponents = new List<OpponentData>();
            
            try
            {
                var opponentsMatch = Regex.Match(json, "\"[Oo]pponents\":\\s*\\[([^\\]]+)\\]");
                if (!opponentsMatch.Success)
                    return opponents;
                
                var opponentsJson = opponentsMatch.Groups[1].Value;
                var oppMatches = Regex.Matches(opponentsJson, "\\{([^}]+)\\}");
                
                foreach (Match oppMatch in oppMatches)
                {
                    var opp = new OpponentData();
                    var oppJson = oppMatch.Groups[1].Value;
                    
                    var idMatch = Regex.Match(oppJson, "\"[Ii]d\":\"([^\"]+)\"");
                    if (idMatch.Success && Guid.TryParse(idMatch.Groups[1].Value, out var id))
                        opp.Id = id;
                    
                    var nameMatch = Regex.Match(oppJson, "\"[Dd]isplayName\":\"([^\"]+)\"");
                    if (nameMatch.Success)
                        opp.DisplayName = nameMatch.Groups[1].Value;
                    
                    var posMatch = Regex.Match(oppJson, "\"[Pp]osition\":(\\d+)");
                    if (posMatch.Success)
                        opp.Position = int.Parse(posMatch.Groups[1].Value);
                    
                    var countMatch = Regex.Match(oppJson, "\"[Tt]ileCount\":(\\d+)");
                    if (countMatch.Success)
                        opp.TileCount = int.Parse(countMatch.Groups[1].Value);
                    
                    var turnMatch = Regex.Match(oppJson, "\"[Ii]sCurrentTurn\":(true|false)");
                    if (turnMatch.Success)
                        opp.IsCurrentTurn = turnMatch.Groups[1].Value == "true";
                    
                    var connMatch = Regex.Match(oppJson, "\"[Ii]sConnected\":(true|false)");
                    if (connMatch.Success)
                        opp.IsConnected = connMatch.Groups[1].Value == "true";
                    
                    opponents.Add(opp);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] ParseOpponentsData error: {ex.Message}");
            }
            
            return opponents;
        }

        private TileData ParseIndicatorTile(string json)
        {
            try
            {
                var indicatorMatch = Regex.Match(json, "\"[Ii]ndicatorTile\":\\s*\\{([^}]+)\\}");
                if (!indicatorMatch.Success)
                    return null;
                
                return ParseSingleTile(indicatorMatch.Groups[1].Value);
            }
            catch
            {
                return null;
            }
        }

        private TileData ParseSingleTile(string tileJson)
        {
            try
            {
                var tile = new TileData();
                
                var idMatch = Regex.Match(tileJson, "\"[Ii]d\":(\\d+)");
                if (idMatch.Success)
                    tile.Id = int.Parse(idMatch.Groups[1].Value);
                
                var colorMatch = Regex.Match(tileJson, "\"[Cc]olor\":(\\d+)");
                if (colorMatch.Success)
                    tile.Color = int.Parse(colorMatch.Groups[1].Value);
                
                var valueMatch = Regex.Match(tileJson, "\"[Vv]alue\":(\\d+)");
                if (valueMatch.Success)
                    tile.Value = int.Parse(valueMatch.Groups[1].Value);
                
                var okeyMatch = Regex.Match(tileJson, "\"[Ii]sOkey\":(true|false)");
                if (okeyMatch.Success)
                    tile.IsOkey = okeyMatch.Groups[1].Value == "true";
                
                var falseJokerMatch = Regex.Match(tileJson, "\"[Ii]sFalseJoker\":(true|false)");
                if (falseJokerMatch.Success)
                    tile.IsFalseJoker = falseJokerMatch.Groups[1].Value == "true";
                
                return tile;
            }
            catch
            {
                return null;
            }
        }
        
        private TileDrawnData ParseTileDrawnData(string json)
        {
            try
            {
                var data = new TileDrawnData();
                // Tile içindeki veriler
                data.Tile = new TileData();
                
                var tileIdMatch = Regex.Match(json, "\"Id\":(\\d+)");
                if (tileIdMatch.Success)
                    data.Tile.Id = int.Parse(tileIdMatch.Groups[1].Value);
                
                var colorMatch = Regex.Match(json, "\"Color\":(\\d+)");
                if (colorMatch.Success)
                    data.Tile.Color = int.Parse(colorMatch.Groups[1].Value);
                
                var valueMatch = Regex.Match(json, "\"Value\":(\\d+)");
                if (valueMatch.Success)
                    data.Tile.Value = int.Parse(valueMatch.Groups[1].Value);
                
                var fromDiscardMatch = Regex.Match(json, "\"FromDiscard\":(true|false)");
                if (fromDiscardMatch.Success)
                    data.FromDiscard = fromDiscardMatch.Groups[1].Value == "true";
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] TileDrawnData parse error: {ex.Message}");
                return null;
            }
        }
        
        private TileDiscardedData ParseTileDiscardedData(string json)
        {
            try
            {
                var data = new TileDiscardedData();
                
                var playerIdMatch = Regex.Match(json, "\"PlayerId\":\"([^\"]+)\"");
                if (playerIdMatch.Success && Guid.TryParse(playerIdMatch.Groups[1].Value, out var playerId))
                    data.PlayerId = playerId;
                
                var tileIdMatch = Regex.Match(json, "\"TileId\":(\\d+)");
                if (tileIdMatch.Success)
                    data.TileId = int.Parse(tileIdMatch.Groups[1].Value);
                
                // NextTurnPlayerId
                var nextPlayerMatch = Regex.Match(json, "\"NextTurnPlayerId\":\"([^\"]+)\"");
                if (nextPlayerMatch.Success && Guid.TryParse(nextPlayerMatch.Groups[1].Value, out var nextPlayerId))
                    data.NextTurnPlayerId = nextPlayerId;
                
                // NextTurnPosition
                var nextPosMatch = Regex.Match(json, "\"NextTurnPosition\":(\\d+)");
                if (nextPosMatch.Success)
                    data.NextTurnPosition = int.Parse(nextPosMatch.Groups[1].Value);
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] TileDiscardedData parse error: {ex.Message}");
                return null;
            }
        }
        
        private OpponentDrewData ParseOpponentDrewData(string json)
        {
            try
            {
                var data = new OpponentDrewData();
                
                var playerIdMatch = Regex.Match(json, "\"PlayerId\":\"([^\"]+)\"");
                if (playerIdMatch.Success && Guid.TryParse(playerIdMatch.Groups[1].Value, out var playerId))
                    data.PlayerId = playerId;
                
                var fromDiscardMatch = Regex.Match(json, "\"FromDiscard\":(true|false)");
                if (fromDiscardMatch.Success)
                    data.FromDiscard = fromDiscardMatch.Groups[1].Value == "true";
                
                // IsBot flag
                var isBotMatch = Regex.Match(json, "\"IsBot\":(true|false)");
                if (isBotMatch.Success)
                    data.IsBot = isBotMatch.Groups[1].Value == "true";
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] OpponentDrewData parse error: {ex.Message}");
                return null;
            }
        }
        
        private DeckUpdatedData ParseDeckUpdatedData(string json)
        {
            try
            {
                var data = new DeckUpdatedData();
                
                var remainingMatch = Regex.Match(json, "\"RemainingTileCount\":(\\d+)");
                if (remainingMatch.Success)
                    data.RemainingTileCount = int.Parse(remainingMatch.Groups[1].Value);
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] DeckUpdatedData parse error: {ex.Message}");
                return null;
            }
        }
        
        private PlayerDisconnectedData ParsePlayerDisconnectedData(string json)
        {
            try
            {
                var data = new PlayerDisconnectedData();
                
                var playerIdMatch = Regex.Match(json, "\"PlayerId\":\"([^\"]+)\"");
                if (playerIdMatch.Success && Guid.TryParse(playerIdMatch.Groups[1].Value, out var playerId))
                    data.PlayerId = playerId;
                
                var timeoutMatch = Regex.Match(json, "\"ReconnectionTimeoutSeconds\":(\\d+)");
                if (timeoutMatch.Success)
                    data.ReconnectionTimeoutSeconds = int.Parse(timeoutMatch.Groups[1].Value);
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] PlayerDisconnectedData parse error: {ex.Message}");
                return null;
            }
        }
        
        private ReconnectedData ParseReconnectedData(string json)
        {
            try
            {
                var data = new ReconnectedData();
                
                var roomIdMatch = Regex.Match(json, "\"RoomId\":\"([^\"]+)\"");
                if (roomIdMatch.Success && Guid.TryParse(roomIdMatch.Groups[1].Value, out var roomId))
                    data.RoomId = roomId;
                
                var messageMatch = Regex.Match(json, "\"Message\":\"([^\"]+)\"");
                if (messageMatch.Success)
                    data.Message = messageMatch.Groups[1].Value;
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameNetwork] ReconnectedData parse error: {ex.Message}");
                return null;
            }
        }

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
        public bool IsBot;
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
        public bool IsBot;
        public string Timestamp;
    }

    [Serializable]
    public class TileDiscardedData
    {
        public Guid PlayerId;
        public int TileId;
        public Guid NextTurnPlayerId;
        public int NextTurnPosition;
        public bool IsBot;
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
