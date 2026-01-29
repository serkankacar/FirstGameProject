using System;
using System.Collections.Generic;
using UnityEngine;

namespace OkeyGame.Core
{
    /// <summary>
    /// Ana oyun yöneticisi - Singleton pattern
    /// Oyunun tüm yaşam döngüsünü yönetir
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState _currentState = GameState.MainMenu;
        
        [Header("Player Info")]
        [SerializeField] private string _playerId;
        [SerializeField] private string _playerName;
        [SerializeField] private long _playerChips;
        [SerializeField] private int _playerElo;
        
        [Header("Current Room")]
        [SerializeField] private string _currentRoomId;
        [SerializeField] private int _currentSeatIndex = -1;

        // Events
        public static event Action<GameState> OnGameStateChanged;
        public event Action<long> OnChipsChanged;
        public event Action<int> OnEloChanged;
        public event Action OnPlayerInfoUpdated;

        // Properties
        public GameState CurrentState => _currentState;
        public string PlayerId => _playerId;
        public string PlayerName => _playerName;
        public long PlayerChips => _playerChips;
        public int PlayerElo => _playerElo;
        public string CurrentRoomId => _currentRoomId;
        public int CurrentSeatIndex => _currentSeatIndex;
        public bool IsInRoom => !string.IsNullOrEmpty(_currentRoomId);

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeGame();
        }

        private void InitializeGame()
        {
            Debug.Log("[GameManager] Initializing...");
            
            // Load cached player info if exists
            LoadPlayerInfo();
        }

        #region State Management

        public void ChangeState(GameState newState)
        {
            if (_currentState == newState) return;

            Debug.Log($"[GameManager] State changed: {_currentState} -> {newState}");
            _currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }

        #endregion

        #region Player Info

        public void SetPlayerInfo(string playerId, string playerName, long chips, int elo)
        {
            _playerId = playerId;
            _playerName = playerName;
            _playerChips = chips;
            _playerElo = elo;

            SavePlayerInfo();
            OnPlayerInfoUpdated?.Invoke();

            Debug.Log($"[GameManager] Player info set: {playerName} (Chips: {chips}, ELO: {elo})");
        }

        public void UpdateChips(long newChips)
        {
            _playerChips = newChips;
            PlayerPrefs.SetString("PlayerChips", newChips.ToString());
            OnChipsChanged?.Invoke(newChips);
        }

        public void UpdateElo(int newElo)
        {
            _playerElo = newElo;
            PlayerPrefs.SetInt("PlayerElo", newElo);
            OnEloChanged?.Invoke(newElo);
        }

        private void SavePlayerInfo()
        {
            PlayerPrefs.SetString("PlayerId", _playerId);
            PlayerPrefs.SetString("PlayerName", _playerName);
            PlayerPrefs.SetString("PlayerChips", _playerChips.ToString());
            PlayerPrefs.SetInt("PlayerElo", _playerElo);
            PlayerPrefs.Save();
        }

        private void LoadPlayerInfo()
        {
            _playerId = PlayerPrefs.GetString("PlayerId", "");
            _playerName = PlayerPrefs.GetString("PlayerName", "");
            
            if (long.TryParse(PlayerPrefs.GetString("PlayerChips", "10000"), out long chips))
                _playerChips = chips;
            
            _playerElo = PlayerPrefs.GetInt("PlayerElo", 1200);
        }

        public void ClearPlayerInfo()
        {
            PlayerPrefs.DeleteKey("PlayerId");
            PlayerPrefs.DeleteKey("PlayerName");
            PlayerPrefs.DeleteKey("PlayerChips");
            PlayerPrefs.DeleteKey("PlayerElo");
            PlayerPrefs.Save();

            _playerId = "";
            _playerName = "";
            _playerChips = 10000;
            _playerElo = 1200;
        }

        #endregion

        #region Room Management

        public void JoinRoom(string roomId, int seatIndex)
        {
            _currentRoomId = roomId;
            _currentSeatIndex = seatIndex;
            ChangeState(GameState.InRoom);
            Debug.Log($"[GameManager] Joined room: {roomId}, Seat: {seatIndex}");
        }

        public void LeaveRoom()
        {
            Debug.Log($"[GameManager] Left room: {_currentRoomId}");
            _currentRoomId = null;
            _currentSeatIndex = -1;
            ChangeState(GameState.Lobby);
        }

        public void StartGame()
        {
            ChangeState(GameState.Playing);
            Debug.Log("[GameManager] Game started!");
        }

        public void EndGame(GameResult result)
        {
            Debug.Log($"[GameManager] Game ended. Winner: {result.WinnerId}, Win Type: {result.WinType}");
            ChangeState(GameState.GameOver);
        }

        #endregion

        private void OnApplicationQuit()
        {
            SavePlayerInfo();
        }
    }

    public enum GameState
    {
        Initializing,
        MainMenu,
        Login,
        Lobby,
        InRoom,
        Playing,
        GameOver,
        Disconnected
    }

    [Serializable]
    public class GameResult
    {
        public string WinnerId;
        public string WinType; // Normal, Pairs, OkeyDiscard, DeckEmpty
        public int WinScore;
        public Dictionary<string, int> PlayerScores;
        public Dictionary<string, int> ChipChanges;
        public Dictionary<string, int> EloChanges;
    }
}
