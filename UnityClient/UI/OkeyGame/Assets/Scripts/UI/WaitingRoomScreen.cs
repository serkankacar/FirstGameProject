using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using OkeyGame.Core;
using OkeyGame.Models;
using OkeyGame.Network;
using OkeyGame.Game;

using GameState = OkeyGame.Core.GameState;

namespace OkeyGame.UI
{
    /// <summary>
    /// Bekleme odası ekranı - Oyuncular oyun başlamadan önce burada bekler
    /// </summary>
    public class WaitingRoomScreen : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        
        private VisualElement _root;
        
        // UI Elements
        private Label _roomNameLabel;
        private Label _stakeLabel;
        private VisualElement _playersContainer;
        private Button _startGameButton;
        private Button _startWithBotsButton;
        private Button _leaveRoomButton;
        private Label _statusLabel;
        private VisualElement _loadingOverlay;
        
        // Room state
        private RoomInfo _currentRoom;
        private List<PlayerInfo> _players = new();
        private bool _isHost;
        
        private void OnEnable()
        {
            _root = _uiDocument.rootVisualElement;
            InitializeUI();
            RegisterCallbacks();
            RegisterNetworkEvents();
        }
        
        private void OnDisable()
        {
            UnregisterNetworkEvents();
        }
        
        private void InitializeUI()
        {
            // Header
            _roomNameLabel = _root.Q<Label>("room-name-label");
            _stakeLabel = _root.Q<Label>("stake-label");
            
            // Players grid
            _playersContainer = _root.Q<VisualElement>("players-container");
            
            // Buttons
            _startGameButton = _root.Q<Button>("start-game-button");
            _startWithBotsButton = _root.Q<Button>("start-with-bots-button");
            _leaveRoomButton = _root.Q<Button>("leave-room-button");
            
            // Status
            _statusLabel = _root.Q<Label>("status-label");
            _loadingOverlay = _root.Q<VisualElement>("loading-overlay");
            
            // Initially hide start buttons
            UpdateStartButtons(false);
        }
        
        private void RegisterCallbacks()
        {
            _startGameButton?.RegisterCallback<ClickEvent>(OnStartGameClicked);
            _startWithBotsButton?.RegisterCallback<ClickEvent>(OnStartWithBotsClicked);
            _leaveRoomButton?.RegisterCallback<ClickEvent>(OnLeaveRoomClicked);
        }
        
        private void RegisterNetworkEvents()
        {
            if (SignalRConnection.Instance != null)
            {
                SignalRConnection.Instance.OnPlayerJoined += HandlePlayerJoined;
                SignalRConnection.Instance.OnPlayerLeft += HandlePlayerLeft;
                SignalRConnection.Instance.OnGameStarted += HandleGameStarted;
                SignalRConnection.Instance.OnError += HandleError;
            }
        }
        
        private void UnregisterNetworkEvents()
        {
            if (SignalRConnection.Instance != null)
            {
                SignalRConnection.Instance.OnPlayerJoined -= HandlePlayerJoined;
                SignalRConnection.Instance.OnPlayerLeft -= HandlePlayerLeft;
                SignalRConnection.Instance.OnGameStarted -= HandleGameStarted;
                SignalRConnection.Instance.OnError -= HandleError;
            }
        }
        
        /// <summary>
        /// Odaya giriş yapıldığında çağrılır
        /// </summary>
        public void SetRoom(RoomInfo room, bool isHost)
        {
            _currentRoom = room;
            _isHost = isHost;
            _players.Clear();
            
            if (room.Players != null)
            {
                _players.AddRange(room.Players);
            }
            
            // Mevcut oyuncuyu ekle (eğer listede yoksa)
            var myPlayer = new PlayerInfo
            {
                Id = GameManager.Instance?.PlayerId ?? "local-player",
                PlayerId = GameManager.Instance?.PlayerId ?? "local-player",
                Name = GameManager.Instance?.PlayerName ?? "Ben",
                PlayerName = GameManager.Instance?.PlayerName ?? "Ben",
                Elo = GameManager.Instance?.PlayerElo ?? 1200,
                IsHost = isHost,
                SeatIndex = 0
            };
            
            if (!_players.Exists(p => p.Id == myPlayer.Id || p.PlayerId == myPlayer.PlayerId))
            {
                _players.Insert(0, myPlayer);
            }
            
            Debug.Log($"[WaitingRoom] SetRoom called - Room: {room.Name}, IsHost: {isHost}, Players: {_players.Count}");
            
            UpdateUI();
        }
        
        private void UpdateUI()
        {
            if (_currentRoom == null) return;
            
            // Header
            if (_roomNameLabel != null)
                _roomNameLabel.text = _currentRoom.Name ?? _currentRoom.RoomName ?? "Oda";
            
            if (_stakeLabel != null)
                _stakeLabel.text = $"{_currentRoom.Stake:N0} Chip";
            
            // Players
            UpdatePlayersDisplay();
            
            // Status
            UpdateStatus();
            
            // Start buttons (only for host)
            UpdateStartButtons(_isHost);
        }
        
        private void UpdatePlayersDisplay()
        {
            if (_playersContainer == null) return;
            
            _playersContainer.Clear();
            
            // 4 koltuk göster
            for (int i = 0; i < 4; i++)
            {
                var seatElement = CreateSeatElement(i);
                _playersContainer.Add(seatElement);
            }
        }
        
        private VisualElement CreateSeatElement(int seatIndex)
        {
            var seat = new VisualElement();
            seat.AddToClassList("player-seat");
            
            PlayerInfo player = seatIndex < _players.Count ? _players[seatIndex] : null;
            
            if (player != null)
            {
                // Occupied seat
                seat.AddToClassList("occupied");
                
                var avatar = new VisualElement();
                avatar.AddToClassList("player-avatar");
                // Avatar icon placeholder
                seat.Add(avatar);
                
                var nameLabel = new Label(player.Name ?? player.PlayerName ?? $"Oyuncu {seatIndex + 1}");
                nameLabel.AddToClassList("player-name");
                seat.Add(nameLabel);
                
                var eloLabel = new Label($"ELO: {player.Elo}");
                eloLabel.AddToClassList("player-elo");
                seat.Add(eloLabel);
                
                // Host badge
                if (seatIndex == 0 || player.IsHost)
                {
                    var hostBadge = new Label("Ev Sahibi");
                    hostBadge.AddToClassList("host-badge");
                    seat.Add(hostBadge);
                }
            }
            else
            {
                // Empty seat
                seat.AddToClassList("empty");
                
                var emptyLabel = new Label("Bekleniyor...");
                emptyLabel.AddToClassList("empty-seat-label");
                seat.Add(emptyLabel);
            }
            
            return seat;
        }
        
        private void UpdateStatus()
        {
            if (_statusLabel == null) return;
            
            int playerCount = _players.Count;
            int needed = 4 - playerCount;
            
            if (needed > 0)
            {
                _statusLabel.text = $"Oyun başlaması için {needed} oyuncu daha gerekli";
            }
            else
            {
                _statusLabel.text = "Tüm oyuncular hazır! Oyunu başlatabilirsiniz.";
            }
        }
        
        private void UpdateStartButtons(bool show)
        {
            if (_startGameButton != null)
            {
                _startGameButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                _startGameButton.SetEnabled(_players.Count == 4);
            }
            
            if (_startWithBotsButton != null)
            {
                _startWithBotsButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                _startWithBotsButton.SetEnabled(_players.Count >= 1 && _players.Count < 4);
            }
        }
        
        #region Button Handlers
        
        private async void OnStartGameClicked(ClickEvent evt)
        {
            Debug.Log("[WaitingRoom] Starting game...");
            
            ShowLoading("Oyun başlatılıyor...");
            
            if (SignalRConnection.Instance == null || !SignalRConnection.Instance.IsConnected)
            {
                Debug.LogError("[WaitingRoom] Not connected to server!");
                HideLoading();
                return;
            }
            
            var success = await SignalRConnection.Instance.StartGame(_currentRoom.Id);
            
            if (!success)
            {
                HideLoading();
                Debug.LogError("[WaitingRoom] Failed to start game");
            }
        }
        
        private async void OnStartWithBotsClicked(ClickEvent evt)
        {
            Debug.Log("[WaitingRoom] Starting game with bots...");
            
            ShowLoading("Botlar ekleniyor ve oyun başlatılıyor...");
            
            if (SignalRConnection.Instance == null || !SignalRConnection.Instance.IsConnected)
            {
                Debug.LogError("[WaitingRoom] Not connected to server!");
                HideLoading();
                return;
            }
            
            var success = await SignalRConnection.Instance.StartGameWithBots(_currentRoom.Id, botDifficulty: 1);
            
            if (!success)
            {
                HideLoading();
                Debug.LogError("[WaitingRoom] Failed to start game with bots");
            }
        }
        
        private void OnLeaveRoomClicked(ClickEvent evt)
        {
            Debug.Log("[WaitingRoom] Leaving room...");
            
            SignalRConnection.Instance.LeaveRoom();
            GameManager.Instance.LeaveRoom();
        }
        
        #endregion
        
        #region Network Event Handlers
        
        private void HandlePlayerJoined(PlayerInfo player)
        {
            Debug.Log($"[WaitingRoom] Player joined: {player.Name ?? player.PlayerName}");
            
            // Check if already exists
            bool exists = _players.Exists(p => p.Id == player.Id || p.PlayerId == player.PlayerId);
            if (!exists)
            {
                _players.Add(player);
                UpdateUI();
            }
        }
        
        private void HandlePlayerLeft(string playerId)
        {
            Debug.Log($"[WaitingRoom] Player left: {playerId}");
            
            _players.RemoveAll(p => p.Id == playerId || p.PlayerId == playerId);
            UpdateUI();
        }
        
        private void HandleGameStarted(GameStartedData data)
        {
            Debug.Log("[WaitingRoom] Game started!");
            
            HideLoading();
            
            // Transition to game
            GameManager.Instance.StartGame();
        }
        
        private void HandleError(string error)
        {
            Debug.LogError($"[WaitingRoom] Error: {error}");
            HideLoading();
            
            // Show error to user
            if (_statusLabel != null)
            {
                _statusLabel.text = $"Hata: {error}";
            }
        }
        
        #endregion
        
        #region Loading
        
        public void ShowLoading(string message = "Yükleniyor...")
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.style.display = DisplayStyle.Flex;
                var loadingLabel = _loadingOverlay.Q<Label>("loading-message");
                if (loadingLabel != null)
                {
                    loadingLabel.text = message;
                }
            }
        }
        
        public void HideLoading()
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.style.display = DisplayStyle.None;
            }
        }
        
        #endregion
    }
}
