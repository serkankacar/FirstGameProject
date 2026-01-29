using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace OkeyGame.Network
{
    /// <summary>
    /// Unity için basit WebSocket wrapper
    /// Unity 2021+ için NativeWebSocket veya System.Net.WebSockets kullanılabilir
    /// </summary>
    public class WebSocketClient
    {
        private System.Net.WebSockets.ClientWebSocket _ws;
        private bool _isConnected;
        private byte[] _receiveBuffer = new byte[8192];

        public event Action OnOpen;
        public event Action<string> OnClose;
        public event Action<string> OnError;
        public event Action<string> OnMessage;

        public bool IsConnected => _isConnected;

        public async Task ConnectAsync(string url)
        {
            try
            {
                _ws = new System.Net.WebSockets.ClientWebSocket();
                
                // SSL sertifika doğrulamasını devre dışı bırak (development için)
                System.Net.ServicePointManager.ServerCertificateValidationCallback = 
                    (sender, certificate, chain, sslPolicyErrors) => true;
                
                // Timeout ayarla
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                Debug.Log($"[WebSocket] Connecting to: {url}");
                await _ws.ConnectAsync(new Uri(url), cts.Token);
                _isConnected = true;
                
                Debug.Log("[WebSocket] Connected successfully!");
                OnOpen?.Invoke();
                
                // Mesaj dinlemeye başla
                _ = ReceiveLoopAsync();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                OnError?.Invoke(ex.Message);
                throw;
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new ArraySegment<byte>(_receiveBuffer);
            
            try
            {
                while (_ws.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(buffer, System.Threading.CancellationToken.None);
                    
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        OnClose?.Invoke(result.CloseStatusDescription ?? "Connection closed");
                        break;
                    }
                    
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                    {
                        var message = System.Text.Encoding.UTF8.GetString(_receiveBuffer, 0, result.Count);
                        
                        // SignalR mesajları \u001e ile ayrılır
                        var messages = message.Split('\u001e');
                        foreach (var msg in messages)
                        {
                            if (!string.IsNullOrEmpty(msg))
                            {
                                // Main thread'de event'i tetikle
                                UnityMainThreadDispatcher.Enqueue(() => OnMessage?.Invoke(msg));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke(ex.Message));
            }
        }

        public void Send(string message)
        {
            if (!_isConnected || _ws.State != System.Net.WebSockets.WebSocketState.Open)
            {
                Debug.LogWarning("[WebSocket] Cannot send: Not connected");
                return;
            }

            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(message);
                var buffer = new ArraySegment<byte>(bytes);
                _ = _ws.SendAsync(buffer, System.Net.WebSockets.WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocket] Send error: {ex.Message}");
            }
        }

        public void Close()
        {
            _isConnected = false;
            
            if (_ws != null && _ws.State == System.Net.WebSockets.WebSocketState.Open)
            {
                try
                {
                    _ = _ws.CloseAsync(
                        System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                        "Client closing",
                        System.Threading.CancellationToken.None);
                }
                catch { }
            }
            
            _ws?.Dispose();
            _ws = null;
        }
    }
}
