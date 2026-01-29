using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using OkeyGame.Models;

namespace OkeyGame.Network
{
    /// <summary>
    /// SignalR benzeri WebSocket bağlantısı
    /// Unity'de native SignalR client yerine WebSocket kullanıyoruz
    /// </summary>
    public class SignalRConnection : MonoBehaviour
    {
        public static SignalRConnection Instance { get; private set; }

        [Header("Connection State")]
        [SerializeField] private ConnectionState _state = ConnectionState.Disconnected;
        [SerializeField] private string _connectionId;
        [SerializeField] private int _reconnectAttempts;

        // WebSocket
        private WebSocketClient _webSocket;
        private string _hubUrl;
        private string _authToken;
        private bool _isReconnecting;

        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        public event Action<string> OnReconnecting;

        // Game Events (Backend'den gelen mesajlar)
        public event Action<RoomInfo> OnRoomJoined;
        public event Action<PlayerInfo> OnPlayerJoined;
        public event Action<string> OnPlayerLeft;
        public event Action<GameState> OnGameStateUpdate;
        public event Action<string, OkeyTile> OnTileDrawn;
        public event Action<string, OkeyTile> OnTileDiscarded;
        public event Action<int, float> OnTurnChanged;
        public event Action<GameEndResult> OnGameEnded;
        public event Action<string> OnChatMessage;
        public event Action<string, string> OnPlayerAction; // PlayerId, Action

        public ConnectionState State => _state;
        public string ConnectionId => _connectionId;
        public bool IsConnected => _state == ConnectionState.Connected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        #region Connection Management

        public async Task<bool> ConnectAsync(string hubUrl, string authToken = null)
        {
            if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
            {
                Debug.LogWarning("[SignalR] Already connected or connecting");
                return _state == ConnectionState.Connected;
            }

            _hubUrl = hubUrl;
            _authToken = authToken;
            _state = ConnectionState.Connecting;
            _reconnectAttempts = 0;

            Debug.Log($"[SignalR] Connecting to {hubUrl}...");

            try
            {
                _webSocket = new WebSocketClient();
                
                // WebSocket URL'ini oluştur
                var wsUrl = hubUrl.Replace("https://", "wss://").Replace("http://", "ws://");
                if (!string.IsNullOrEmpty(authToken))
                {
                    wsUrl += $"?access_token={authToken}";
                }

                _webSocket.OnOpen += HandleOpen;
                _webSocket.OnClose += HandleClose;
                _webSocket.OnError += HandleError;
                _webSocket.OnMessage += HandleMessage;

                await _webSocket.ConnectAsync(wsUrl);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalR] Connection failed: {ex.Message}");
                _state = ConnectionState.Disconnected;
                OnError?.Invoke(ex.Message);
                return false;
            }
        }

        public void Disconnect()
        {
            if (_webSocket != null)
            {
                _webSocket.OnOpen -= HandleOpen;
                _webSocket.OnClose -= HandleClose;
                _webSocket.OnError -= HandleError;
                _webSocket.OnMessage -= HandleMessage;
                _webSocket.Close();
                _webSocket = null;
            }

            _state = ConnectionState.Disconnected;
            _connectionId = null;
            Debug.Log("[SignalR] Disconnected");
        }

        private async void TryReconnect()
        {
            if (_isReconnecting) return;
            
            var settings = Core.GameSettings.Instance;
            if (_reconnectAttempts >= settings.MaxReconnectAttempts)
            {
                Debug.LogError("[SignalR] Max reconnect attempts reached");
                _state = ConnectionState.Disconnected;
                OnDisconnected?.Invoke();
                return;
            }

            _isReconnecting = true;
            _reconnectAttempts++;
            _state = ConnectionState.Reconnecting;

            OnReconnecting?.Invoke($"Reconnecting... ({_reconnectAttempts}/{settings.MaxReconnectAttempts})");
            Debug.Log($"[SignalR] Reconnecting attempt {_reconnectAttempts}...");

            await Task.Delay((int)(settings.ReconnectDelay * 1000));

            _isReconnecting = false;
            await ConnectAsync(_hubUrl, _authToken);
        }

        #endregion

        #region WebSocket Handlers

        private void HandleOpen()
        {
            _state = ConnectionState.Connected;
            _reconnectAttempts = 0;
            Debug.Log("[SignalR] Connected successfully");
            
            // Handshake mesajı gönder (SignalR protocol)
            SendHandshake();
            
            OnConnected?.Invoke();
        }

        private void HandleClose(string reason)
        {
            Debug.Log($"[SignalR] Connection closed: {reason}");
            
            if (_state != ConnectionState.Disconnected)
            {
                TryReconnect();
            }
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[SignalR] Error: {error}");
            OnError?.Invoke(error);
        }

        private void HandleMessage(string message)
        {
            Debug.Log($"[SignalR] Received: {message}");

            try
            {
                // SignalR JSON protokolü - basit parsing
                // Format: {"type":1,"target":"MethodName","arguments":[...]}
                
                // Type kontrolü
                if (message.Contains("\"type\":6"))
                {
                    // Ping
                    SendPong();
                    return;
                }
                
                if (message.Contains("\"type\":7"))
                {
                    // Close
                    Debug.Log("[SignalR] Server closed connection");
                    return;
                }
                
                // Handshake response kontrolü
                if (message.Contains("{}") || string.IsNullOrEmpty(message.Trim()))
                {
                    Debug.Log("[SignalR] Handshake completed");
                    return;
                }
                
                // Invocation (type:1) - Hub method çağrısı
                if (message.Contains("\"type\":1") && message.Contains("\"target\":"))
                {
                    // Target'ı parse et
                    var targetMatch = System.Text.RegularExpressions.Regex.Match(message, "\"target\":\"([^\"]+)\"");
                    if (targetMatch.Success)
                    {
                        var methodName = targetMatch.Groups[1].Value;
                        Debug.Log($"[SignalR] Hub method called: {methodName}");
                        
                        // Arguments'ı parse et
                        var argsMatch = System.Text.RegularExpressions.Regex.Match(message, "\"arguments\":\\[(.*)\\]");
                        if (argsMatch.Success)
                        {
                            var argsJson = argsMatch.Groups[1].Value;
                            ProcessHubMethodWithJson(methodName, argsJson);
                        }
                        else
                        {
                            ProcessHubMethodWithJson(methodName, "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalR] Message parse error: {ex.Message}\nMessage: {message}");
            }
        }

        private void ProcessHubMethodWithJson(string methodName, string argsJson)
        {
            Debug.Log($"[SignalR] Processing method: {methodName}, args: {argsJson}");
            
            switch (methodName)
            {
                case "ReceiveConnectionId":
                    _connectionId = argsJson.Trim('"');
                    Debug.Log($"[SignalR] Connection ID: {_connectionId}");
                    break;

                case "RoomJoined":
                    Debug.Log($"[SignalR] RoomJoined event received!");
                    try
                    {
                        // JSON object'i parse et
                        var roomInfo = JsonUtility.FromJson<RoomInfo>(argsJson);
                        if (roomInfo != null)
                        {
                            Debug.Log($"[SignalR] Room joined: {roomInfo.Name ?? roomInfo.RoomName}");
                            OnRoomJoined?.Invoke(roomInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[SignalR] RoomJoined parse error: {ex.Message}");
                        // Yine de success olarak işaretle
                        OnRoomJoined?.Invoke(new RoomInfo { Name = "Oda", Id = System.Guid.NewGuid().ToString() });
                    }
                    break;

                case "OnError":
                    Debug.LogError($"[SignalR] Server error: {argsJson}");
                    OnError?.Invoke(argsJson);
                    break;

                default:
                    Debug.Log($"[SignalR] Unknown method: {methodName}");
                    break;
            }
        }

        private void ProcessHubMethod(string methodName, string[] arguments)
        {
            switch (methodName)
            {
                case "ReceiveConnectionId":
                    _connectionId = arguments[0];
                    Debug.Log($"[SignalR] Connection ID: {_connectionId}");
                    break;

                case "RoomJoined":
                    var roomInfo = JsonUtility.FromJson<RoomInfo>(arguments[0]);
                    OnRoomJoined?.Invoke(roomInfo);
                    break;

                case "PlayerJoined":
                    var playerInfo = JsonUtility.FromJson<PlayerInfo>(arguments[0]);
                    OnPlayerJoined?.Invoke(playerInfo);
                    break;

                case "PlayerLeft":
                    OnPlayerLeft?.Invoke(arguments[0]);
                    break;

                case "GameStateUpdate":
                    var gameState = JsonUtility.FromJson<GameState>(arguments[0]);
                    OnGameStateUpdate?.Invoke(gameState);
                    break;

                case "TileDrawn":
                    var drawTile = JsonUtility.FromJson<OkeyTile>(arguments[1]);
                    OnTileDrawn?.Invoke(arguments[0], drawTile);
                    break;

                case "TileDiscarded":
                    var discardTile = JsonUtility.FromJson<OkeyTile>(arguments[1]);
                    OnTileDiscarded?.Invoke(arguments[0], discardTile);
                    break;

                case "TurnChanged":
                    int seatIndex = int.Parse(arguments[0]);
                    float timeRemaining = float.Parse(arguments[1]);
                    OnTurnChanged?.Invoke(seatIndex, timeRemaining);
                    break;

                case "GameEnded":
                    var result = JsonUtility.FromJson<GameEndResult>(arguments[0]);
                    OnGameEnded?.Invoke(result);
                    break;

                case "ChatMessage":
                    OnChatMessage?.Invoke(arguments[0]);
                    break;

                case "PlayerAction":
                    OnPlayerAction?.Invoke(arguments[0], arguments[1]);
                    break;

                default:
                    Debug.LogWarning($"[SignalR] Unknown method: {methodName}");
                    break;
            }
        }

        #endregion

        #region Send Methods (Client -> Server)

        private void SendHandshake()
        {
            // SignalR JSON protocol handshake
            var handshake = "{\"protocol\":\"json\",\"version\":1}\u001e";
            _webSocket?.Send(handshake);
        }

        private void SendPong()
        {
            var pong = "{\"type\":6}\u001e";
            _webSocket?.Send(pong);
        }

        public void InvokeAsync(string methodName, params object[] args)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[SignalR] Cannot invoke {methodName}: Not connected");
                return;
            }

            // JSON elle oluştur çünkü JsonUtility object[] serialize edemez
            var argsJson = BuildArgumentsJson(args);
            var json = $"{{\"type\":1,\"target\":\"{methodName}\",\"arguments\":[{argsJson}]}}\u001e";
            
            if (Core.GameSettings.Instance?.LogNetworkMessages == true)
            {
                Debug.Log($"[SignalR] Sending: {json}");
            }

            _webSocket?.Send(json);
        }

        private string BuildArgumentsJson(object[] args)
        {
            if (args == null || args.Length == 0) return "";

            var parts = new List<string>();
            foreach (var arg in args)
            {
                if (arg == null)
                {
                    parts.Add("null");
                }
                else if (arg is string s)
                {
                    // Escape special chars
                    s = s.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    parts.Add($"\"{s}\"");
                }
                else if (arg is bool b)
                {
                    parts.Add(b ? "true" : "false");
                }
                else if (arg is int || arg is long || arg is float || arg is double)
                {
                    parts.Add(arg.ToString());
                }
                else if (arg is System.Guid g)
                {
                    parts.Add($"\"{g}\"");
                }
                else if (arg is System.Collections.IList list)
                {
                    // List'i JSON array'e çevir
                    var items = new List<string>();
                    foreach (var item in list)
                    {
                        items.Add(JsonUtility.ToJson(item));
                    }
                    parts.Add($"[{string.Join(",", items)}]");
                }
                else
                {
                    // Complex object
                    parts.Add(JsonUtility.ToJson(arg));
                }
            }
            return string.Join(",", parts);
        }

        // Oyun aksiyonları

        public void LeaveRoom()
        {
            InvokeAsync("LeaveRoom");
        }

        public void SetReady(bool isReady)
        {
            InvokeAsync("SetReady", isReady);
        }

        /// <summary>
        /// Yeni oda oluştur
        /// </summary>
        public async Task<bool> CreateRoom(string roomName, long stake)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[SignalR] Cannot create room: Not connected");
                return false;
            }

            var tcs = new TaskCompletionSource<bool>();
            
            void OnRoomJoinedHandler(RoomInfo room)
            {
                OnRoomJoined -= OnRoomJoinedHandler;
                tcs.TrySetResult(true);
            }

            void OnErrorHandler(string error)
            {
                OnError -= OnErrorHandler;
                tcs.TrySetResult(false);
            }

            OnRoomJoined += OnRoomJoinedHandler;
            OnError += OnErrorHandler;

            InvokeAsync("CreateRoom", roomName, stake);

            // 10 saniye timeout
            var timeoutTask = Task.Delay(10000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                OnRoomJoined -= OnRoomJoinedHandler;
                OnError -= OnErrorHandler;
                Debug.LogWarning("[SignalR] Create room timeout");
                return false;
            }

            return await tcs.Task;
        }

        /// <summary>
        /// Odaya katıl (async)
        /// </summary>
        public async Task<bool> JoinRoom(string roomId)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[SignalR] Cannot join room: Not connected");
                return false;
            }

            var tcs = new TaskCompletionSource<bool>();
            
            void OnRoomJoinedHandler(RoomInfo room)
            {
                OnRoomJoined -= OnRoomJoinedHandler;
                tcs.TrySetResult(true);
            }

            void OnErrorHandler(string error)
            {
                OnError -= OnErrorHandler;
                tcs.TrySetResult(false);
            }

            OnRoomJoined += OnRoomJoinedHandler;
            OnError += OnErrorHandler;

            InvokeAsync("JoinRoom", roomId);

            // 10 saniye timeout
            var timeoutTask = Task.Delay(10000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                OnRoomJoined -= OnRoomJoinedHandler;
                OnError -= OnErrorHandler;
                Debug.LogWarning("[SignalR] Join room timeout");
                return false;
            }

            return await tcs.Task;
        }

        public void DrawFromDeck()
        {
            InvokeAsync("DrawFromDeck");
        }

        public void DrawFromDiscard()
        {
            InvokeAsync("DrawFromDiscard");
        }

        public void DiscardTile(int tileId)
        {
            InvokeAsync("DiscardTile", tileId);
        }

        public void DeclareWin(List<List<int>> sets)
        {
            InvokeAsync("DeclareWin", sets);
        }

        public void SendChatMessage(string message)
        {
            InvokeAsync("SendChatMessage", message);
        }

        #endregion
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    [Serializable]
    public class SignalRMessage
    {
        public int type;
        public string target;
        public string[] arguments;
        public string invocationId;
    }

    [Serializable]
    public class SignalRInvocation
    {
        public int type;
        public string target;
        public object[] arguments;
    }
}
