using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OkeyGame.Unity.Networking
{
    /// <summary>
    /// Unity için SignalR WebSocket Client.
    /// Microsoft.AspNetCore.SignalR.Client paketine ihtiyaç duymadan çalışır.
    /// SignalR JSON Text Protocol kullanır.
    /// </summary>
    public class SignalRWebSocketClient : IDisposable
    {
        #region Constants
        
        private const char RecordSeparator = '\u001e';
        private const int ReceiveBufferSize = 8192;
        
        #endregion
        
        #region Fields
        
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private string _hubUrl;
        private bool _isConnected;
        private bool _isNegotiationComplete;
        
        // Handler registry
        private readonly Dictionary<string, Action<string>> _handlers = new Dictionary<string, Action<string>>();
        
        #endregion
        
        #region Properties
        
        public bool IsConnected => _isConnected && _socket?.State == WebSocketState.Open;
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;
        
        #endregion
        
        #region Connection
        
        /// <summary>
        /// SignalR Hub'a bağlanır.
        /// </summary>
        /// <param name="hubUrl">Hub URL (örn: http://localhost:5000/gamehub)</param>
        public async Task<bool> ConnectAsync(string hubUrl)
        {
            try
            {
                _hubUrl = hubUrl;
                
                Debug.Log($"[SignalR] Connecting to: {hubUrl}");
                
                // Önce negotiate yapıyoruz - skip edip direkt WebSocket deneyelim
                // SignalR 3.x'te negotiate optional
                
                // WebSocket URL oluştur
                var wsUrl = hubUrl
                    .Replace("https://", "wss://")
                    .Replace("http://", "ws://");
                
                if (!wsUrl.Contains("?"))
                    wsUrl += "?";
                else
                    wsUrl += "&";
                    
                // Transport olarak websocket belirt
                wsUrl += "transport=WebSockets";
                
                Debug.Log($"[SignalR] WebSocket URL: {wsUrl}");
                
                // Socket oluştur
                _socket = new ClientWebSocket();
                _cts = new CancellationTokenSource();
                
                // SSL sertifika bypass (development)
                // ClientWebSocket'te bu doğrudan desteklenmiyor, ServicePointManager kullan
                System.Net.ServicePointManager.ServerCertificateValidationCallback = 
                    (sender, certificate, chain, sslPolicyErrors) => true;
                
                // Bağlan
                await _socket.ConnectAsync(new Uri(wsUrl), _cts.Token);
                
                Debug.Log($"[SignalR] WebSocket connected, state: {_socket.State}");
                
                // SignalR Handshake gönder
                await SendHandshakeAsync();
                
                // Receive loop başlat
                _ = ReceiveLoopAsync();
                
                // Handshake cevabını bekle (kısa timeout ile)
                var startTime = DateTime.Now;
                while (!_isNegotiationComplete && (DateTime.Now - startTime).TotalSeconds < 5)
                {
                    await Task.Delay(100);
                }
                
                if (_isNegotiationComplete)
                {
                    _isConnected = true;
                    MainThreadDispatcher.Enqueue(() => OnConnected?.Invoke());
                    Debug.Log("[SignalR] Connection established!");
                    return true;
                }
                else
                {
                    Debug.LogError("[SignalR] Handshake timeout");
                    await DisconnectAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalR] Connection error: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => OnError?.Invoke(ex.Message));
                return false;
            }
        }
        
        /// <summary>
        /// Bağlantıyı kapatır.
        /// </summary>
        public async Task DisconnectAsync()
        {
            _isConnected = false;
            _isNegotiationComplete = false;
            
            try
            {
                _cts?.Cancel();
                
                if (_socket?.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SignalR] Disconnect error: {ex.Message}");
            }
            finally
            {
                _socket?.Dispose();
                _socket = null;
                _cts?.Dispose();
                _cts = null;
            }
            
            MainThreadDispatcher.Enqueue(() => OnDisconnected?.Invoke("Disconnected"));
        }
        
        #endregion
        
        #region Handshake
        
        private async Task SendHandshakeAsync()
        {
            // SignalR handshake mesajı
            var handshake = "{\"protocol\":\"json\",\"version\":1}" + RecordSeparator;
            var bytes = Encoding.UTF8.GetBytes(handshake);
            
            await _socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
                
            Debug.Log("[SignalR] Handshake sent");
        }
        
        #endregion
        
        #region Receive Loop
        
        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[ReceiveBufferSize];
            var messageBuffer = new StringBuilder();
            
            try
            {
                while (_socket?.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        _cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[SignalR] WebSocket closed by server");
                        break;
                    }
                    
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuffer.Append(text);
                    
                    if (result.EndOfMessage)
                    {
                        var fullMessage = messageBuffer.ToString();
                        messageBuffer.Clear();
                        
                        // SignalR mesajları RecordSeparator ile ayrılır
                        var messages = fullMessage.Split(new[] { RecordSeparator }, StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (var msg in messages)
                        {
                            ProcessMessage(msg);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal kapatma
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalR] Receive error: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
                MainThreadDispatcher.Enqueue(() => OnDisconnected?.Invoke("Connection lost"));
            }
        }
        
        #endregion
        
        #region Message Processing
        
        private void ProcessMessage(string message)
        {
            Debug.Log($"[SignalR] Received: {message}");
            
            // Boş handshake cevabı: {} veya {"error":null}
            if (message == "{}" || message.Contains("\"error\":null"))
            {
                Debug.Log("[SignalR] Handshake complete");
                _isNegotiationComplete = true;
                return;
            }
            
            // Hata kontrolü
            if (message.Contains("\"error\":"))
            {
                var errorMatch = Regex.Match(message, "\"error\":\"([^\"]+)\"");
                if (errorMatch.Success)
                {
                    var error = errorMatch.Groups[1].Value;
                    Debug.LogError($"[SignalR] Server error: {error}");
                    MainThreadDispatcher.Enqueue(() => OnError?.Invoke(error));
                }
                return;
            }
            
            // SignalR Hub mesajı kontrolü
            // type: 1 = Invocation, 2 = StreamItem, 3 = Completion, 6 = Ping
            var typeMatch = Regex.Match(message, "\"type\":(\\d+)");
            if (!typeMatch.Success) return;
            
            var type = int.Parse(typeMatch.Groups[1].Value);
            
            switch (type)
            {
                case 1: // Invocation - Hub method çağrısı (server -> client)
                    HandleInvocation(message);
                    break;
                    
                case 3: // Completion - Method çağrısı tamamlandı
                    HandleCompletion(message);
                    break;
                    
                case 6: // Ping
                    _ = SendPongAsync();
                    break;
            }
        }
        
        private void HandleInvocation(string message)
        {
            // target: method adı, arguments: parametreler
            var targetMatch = Regex.Match(message, "\"target\":\"([^\"]+)\"");
            if (!targetMatch.Success) return;
            
            var target = targetMatch.Groups[1].Value;
            
            // Arguments array'ini çıkar
            var argsMatch = Regex.Match(message, "\"arguments\":\\[(.*)\\]");
            var argsJson = argsMatch.Success ? argsMatch.Groups[1].Value : "";
            
            Debug.Log($"[SignalR] Invocation: {target}({argsJson})");
            
            // Handler var mı kontrol et
            if (_handlers.TryGetValue(target, out var handler))
            {
                MainThreadDispatcher.Enqueue(() => handler(argsJson));
            }
            else
            {
                Debug.LogWarning($"[SignalR] No handler for: {target}");
            }
        }
        
        private void HandleCompletion(string message)
        {
            // Completion mesajları şimdilik loglayalım
            var invocationIdMatch = Regex.Match(message, "\"invocationId\":\"([^\"]+)\"");
            if (invocationIdMatch.Success)
            {
                Debug.Log($"[SignalR] Completion: {invocationIdMatch.Groups[1].Value}");
            }
            
            // Error var mı?
            if (message.Contains("\"error\":"))
            {
                var errorMatch = Regex.Match(message, "\"error\":\"([^\"]+)\"");
                if (errorMatch.Success)
                {
                    var error = errorMatch.Groups[1].Value;
                    Debug.LogError($"[SignalR] Method error: {error}");
                    MainThreadDispatcher.Enqueue(() => OnError?.Invoke(error));
                }
            }
        }
        
        private async Task SendPongAsync()
        {
            // Pong: type 6
            var pong = "{\"type\":6}" + RecordSeparator;
            var bytes = Encoding.UTF8.GetBytes(pong);
            
            try
            {
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SignalR] Pong error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Handler Registration
        
        /// <summary>
        /// Bir Hub method'u için handler kayıt eder.
        /// </summary>
        public void On(string methodName, Action<string> handler)
        {
            _handlers[methodName] = handler;
            Debug.Log($"[SignalR] Handler registered: {methodName}");
        }
        
        /// <summary>
        /// Handler'ı kaldırır.
        /// </summary>
        public void Off(string methodName)
        {
            _handlers.Remove(methodName);
        }
        
        #endregion
        
        #region Invoke Methods
        
        private int _invocationId = 0;
        
        /// <summary>
        /// Hub method çağırır (parametresiz).
        /// </summary>
        public Task InvokeAsync(string methodName)
        {
            return InvokeAsync(methodName, Array.Empty<object>());
        }
        
        /// <summary>
        /// Hub method çağırır (tek parametre).
        /// </summary>
        public Task InvokeAsync<T>(string methodName, T arg)
        {
            return InvokeAsync(methodName, new object[] { arg });
        }
        
        /// <summary>
        /// Hub method çağırır (iki parametre).
        /// </summary>
        public Task InvokeAsync<T1, T2>(string methodName, T1 arg1, T2 arg2)
        {
            return InvokeAsync(methodName, new object[] { arg1, arg2 });
        }
        
        /// <summary>
        /// Hub method çağırır.
        /// </summary>
        public async Task InvokeAsync(string methodName, object[] arguments)
        {
            if (!IsConnected)
            {
                Debug.LogError($"[SignalR] Cannot invoke {methodName}: Not connected");
                throw new InvalidOperationException("Not connected to SignalR Hub");
            }
            
            var invocationId = (++_invocationId).ToString();
            
            // SignalR Invocation mesajı oluştur
            var argsJson = BuildArgumentsJson(arguments);
            var message = $"{{\"type\":1,\"invocationId\":\"{invocationId}\",\"target\":\"{methodName}\",\"arguments\":[{argsJson}]}}" + RecordSeparator;
            
            Debug.Log($"[SignalR] Invoke: {methodName}({argsJson})");
            
            var bytes = Encoding.UTF8.GetBytes(message);
            
            await _socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
        }
        
        private string BuildArgumentsJson(object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
                return "";
            
            var parts = new List<string>();
            
            foreach (var arg in arguments)
            {
                if (arg == null)
                {
                    parts.Add("null");
                }
                else if (arg is string s)
                {
                    // Escape special characters
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
                else if (arg is Guid g)
                {
                    parts.Add($"\"{g}\"");
                }
                else
                {
                    // Complex object - JsonUtility ile serialize et
                    var json = JsonUtility.ToJson(arg);
                    parts.Add(json);
                }
            }
            
            return string.Join(",", parts);
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            DisconnectAsync().Wait(1000);
        }
        
        #endregion
    }
}
