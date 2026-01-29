using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using OkeyGame.Core;
using OkeyGame.Models;
using OkeyGame.Network;

using GameState = OkeyGame.Core.GameState;

namespace OkeyGame.UI
{
    /// <summary>
    /// Lobi ekranı controller - Oda listesi ve oda oluşturma
    /// </summary>
    public class LobbyScreen : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        
        // Header
        private Button _backButton;
        private Button _refreshButton;

        // Filters
        private Button[] _filterButtons;
        private string _currentFilter = "all";

        // Room list
        private VisualElement _roomsContainer;
        private VisualElement _emptyState;

        // Create room
        private Button _createRoomButton;
        private VisualElement _createRoomModal;
        private TextField _roomNameInput;
        private Button[] _stakeButtons;
        private Button _modalCancel;
        private Button _modalCreate;

        private long _selectedStake = 500;
        private List<RoomInfo> _rooms = new();

        private void OnEnable()
        {
            _root = _uiDocument.rootVisualElement;
            InitializeUIReferences();
            RegisterCallbacks();
            
            // Load rooms
            RefreshRooms();
        }

        private void InitializeUIReferences()
        {
            _backButton = _root.Q<Button>("back-button");
            _refreshButton = _root.Q<Button>("refresh-button");

            _filterButtons = new[]
            {
                _root.Q<Button>("filter-all"),
                _root.Q<Button>("filter-waiting"),
                _root.Q<Button>("filter-low"),
                _root.Q<Button>("filter-medium"),
                _root.Q<Button>("filter-high")
            };

            _roomsContainer = _root.Q<VisualElement>("rooms-container");
            _emptyState = _root.Q<VisualElement>("empty-state");

            _createRoomButton = _root.Q<Button>("create-room-button");
            _createRoomModal = _root.Q<VisualElement>("create-room-modal");
            _roomNameInput = _root.Q<TextField>("room-name-input");
            
            _stakeButtons = new[]
            {
                _root.Q<Button>("stake-100"),
                _root.Q<Button>("stake-500"),
                _root.Q<Button>("stake-1000"),
                _root.Q<Button>("stake-5000"),
                _root.Q<Button>("stake-10000")
            };

            _modalCancel = _root.Q<Button>("modal-cancel");
            _modalCreate = _root.Q<Button>("modal-create");
        }

        private void RegisterCallbacks()
        {
            _backButton.clicked += OnBackClicked;
            _refreshButton.clicked += RefreshRooms;
            _createRoomButton.clicked += OnCreateRoomClicked;
            _modalCancel.clicked += HideCreateRoomModal;
            _modalCreate.clicked += OnConfirmCreateRoom;

            // Filter buttons
            for (int i = 0; i < _filterButtons.Length; i++)
            {
                int index = i;
                _filterButtons[i].clicked += () => OnFilterClicked(index);
            }

            // Stake buttons
            long[] stakes = { 100, 500, 1000, 5000, 10000 };
            for (int i = 0; i < _stakeButtons.Length; i++)
            {
                int index = i;
                long stake = stakes[i];
                _stakeButtons[i].clicked += () => OnStakeSelected(index, stake);
            }
        }

        private async void RefreshRooms()
        {
            var response = await ApiService.Instance.GetRoomsAsync();
            
            if (response.Success && response.Data != null)
            {
                _rooms = response.Data.Rooms ?? new List<RoomInfo>();
                UpdateRoomList();
            }
            else
            {
                Debug.LogError($"[Lobby] Failed to get rooms: {response.Error}");
            }
        }

        private void UpdateRoomList()
        {
            _roomsContainer.Clear();

            var filteredRooms = FilterRooms(_rooms);

            if (filteredRooms.Count == 0)
            {
                _emptyState.style.display = DisplayStyle.Flex;
                return;
            }

            _emptyState.style.display = DisplayStyle.None;

            foreach (var room in filteredRooms)
            {
                var roomCard = CreateRoomCard(room);
                _roomsContainer.Add(roomCard);
            }
        }

        private List<RoomInfo> FilterRooms(List<RoomInfo> rooms)
        {
            return _currentFilter switch
            {
                "waiting" => rooms.Where(r => r.CurrentPlayerCount < r.MaxPlayers).ToList(),
                "low" => rooms.Where(r => r.Stake <= 500).ToList(),
                "medium" => rooms.Where(r => r.Stake > 500 && r.Stake <= 2000).ToList(),
                "high" => rooms.Where(r => r.Stake > 2000).ToList(),
                _ => rooms
            };
        }

        private VisualElement CreateRoomCard(RoomInfo room)
        {
            var card = new VisualElement();
            card.AddToClassList("room-card");

            // Room info section
            var infoSection = new VisualElement();
            infoSection.AddToClassList("room-info");

            var nameLabel = new Label(room.Name);
            nameLabel.AddToClassList("room-name");
            infoSection.Add(nameLabel);

            var detailsRow = new VisualElement();
            detailsRow.AddToClassList("room-details");

            var stakeLabel = new Label($"{room.Stake:N0} Chip");
            stakeLabel.AddToClassList("room-stake");
            detailsRow.Add(stakeLabel);

            var playersLabel = new Label($"{room.CurrentPlayerCount}/{room.MaxPlayers} Oyuncu");
            playersLabel.AddToClassList("room-players");
            detailsRow.Add(playersLabel);

            infoSection.Add(detailsRow);
            card.Add(infoSection);

            // Status badge
            var statusLabel = new Label();
            statusLabel.AddToClassList("room-status");

            if (room.CurrentPlayerCount >= room.MaxPlayers)
            {
                statusLabel.text = "Dolu";
                statusLabel.AddToClassList("full");
            }
            else if (room.IsGameStarted)
            {
                statusLabel.text = "Oyunda";
                statusLabel.AddToClassList("playing");
            }
            else
            {
                statusLabel.text = "Bekliyor";
                statusLabel.AddToClassList("waiting");
            }
            card.Add(statusLabel);

            // Join button
            var joinButton = new Button(() => OnJoinRoomClicked(room));
            joinButton.text = "Katıl";
            joinButton.AddToClassList("join-button");
            
            bool canJoin = room.CurrentPlayerCount < room.MaxPlayers && 
                          !room.IsGameStarted &&
                          GameManager.Instance.PlayerChips >= room.Stake;
            joinButton.SetEnabled(canJoin);

            card.Add(joinButton);

            return card;
        }

        private void OnFilterClicked(int index)
        {
            // Update selected state
            foreach (var btn in _filterButtons)
            {
                btn.RemoveFromClassList("selected");
            }
            _filterButtons[index].AddToClassList("selected");

            _currentFilter = index switch
            {
                1 => "waiting",
                2 => "low",
                3 => "medium",
                4 => "high",
                _ => "all"
            };

            UpdateRoomList();
        }

        private async void OnJoinRoomClicked(RoomInfo room)
        {
            Debug.Log($"[Lobby] Joining room: {room.Name}");

            var success = await SignalRConnection.Instance.JoinRoom(room.Id);

            if (success)
            {
                GameManager.Instance.ChangeState(GameState.InRoom);
                // TODO: Navigate to waiting room or game
            }
            else
            {
                Debug.LogError("[Lobby] Failed to join room");
            }
        }

        private void OnCreateRoomClicked()
        {
            _roomNameInput.value = $"Oda_{GameManager.Instance.PlayerName}";
            _createRoomModal.style.display = DisplayStyle.Flex;
        }

        private void HideCreateRoomModal()
        {
            _createRoomModal.style.display = DisplayStyle.None;
        }

        private void OnStakeSelected(int index, long stake)
        {
            _selectedStake = stake;

            foreach (var btn in _stakeButtons)
            {
                btn.RemoveFromClassList("selected");
            }
            _stakeButtons[index].AddToClassList("selected");
        }

        private async void OnConfirmCreateRoom()
        {
            string roomName = _roomNameInput.value;
            if (string.IsNullOrWhiteSpace(roomName))
            {
                roomName = $"Oda_{GameManager.Instance.PlayerName}";
            }

            // Check if player has enough chips
            if (GameManager.Instance.PlayerChips < _selectedStake)
            {
                Debug.LogError("[Lobby] Not enough chips!");
                return;
            }

            HideCreateRoomModal();

            Debug.Log($"[Lobby] Creating room: {roomName}, stake: {_selectedStake}");

            // SignalR bağlı mı kontrol et
            if (SignalRConnection.Instance == null || !SignalRConnection.Instance.IsConnected)
            {
                Debug.LogWarning("[Lobby] SignalR not connected, trying to connect...");
                var hubUrl = GameSettings.Instance?.SignalRHubUrl ?? "http://localhost:57392/gamehub";
                var connected = await SignalRConnection.Instance.ConnectAsync(hubUrl, null);
                if (!connected)
                {
                    Debug.LogError("[Lobby] Failed to connect to SignalR");
                    return;
                }
            }

            var success = await SignalRConnection.Instance.CreateRoom(roomName, _selectedStake);

            if (success)
            {
                Debug.Log("[Lobby] Room created successfully!");
                GameManager.Instance.ChangeState(GameState.InRoom);
            }
            else
            {
                Debug.LogError("[Lobby] Failed to create room");
            }
        }

        private void OnBackClicked()
        {
            GameManager.Instance.ChangeState(GameState.MainMenu);
        }
    }
}
