using System;
using UnityEngine;

namespace OkeyGame.Unity.Networking
{
    /// <summary>
    /// Oyun durumu yöneticisi.
    /// Server'dan gelen durumu saklar ve UI'a sunar.
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        #region Singleton

        private static GameStateManager _instance;
        public static GameStateManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameStateManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("GameStateManager");
                        _instance = go.AddComponent<GameStateManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Durum

        /// <summary>Mevcut oyun durumu.</summary>
        public GameStateData CurrentState { get; private set; }

        /// <summary>Oyun aktif mi?</summary>
        public bool IsGameActive => CurrentState != null && CurrentState.State == 1; // InProgress

        /// <summary>Bizim sıramız mı?</summary>
        public bool IsMyTurn => CurrentState?.Self?.IsCurrentTurn ?? false;

        /// <summary>Elimizdeki taşlar.</summary>
        public TileData[] MyHand => CurrentState?.Self?.Hand?.ToArray() ?? Array.Empty<TileData>();

        /// <summary>Gösterge taşı.</summary>
        public TileData IndicatorTile => CurrentState?.IndicatorTile;

        /// <summary>Atık yığınındaki üst taş.</summary>
        public TileData DiscardTopTile => CurrentState?.DiscardPileTopTile;

        /// <summary>Destede kalan taş sayısı.</summary>
        public int RemainingTileCount => CurrentState?.RemainingTileCount ?? 0;

        /// <summary>Provably Fair commitment hash.</summary>
        public string CommitmentHash { get; private set; }

        /// <summary>Server seed (oyun bitince açıklanır).</summary>
        public string RevealedServerSeed { get; private set; }

        #endregion

        #region Events

        /// <summary>Oyun durumu değişti.</summary>
        public event Action OnStateChanged;

        /// <summary>El güncellendi.</summary>
        public event Action OnHandUpdated;

        /// <summary>Sıra değişti.</summary>
        public event Action<int> OnTurnChanged;

        /// <summary>Taş çekildi.</summary>
        public event Action<TileData> OnTileAdded;

        /// <summary>Taş atıldı.</summary>
        public event Action<int> OnTileRemoved;

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

            SubscribeToNetworkEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            var network = GameNetworkManager.Instance;
            if (network == null) return;

            network.OnRoomCreated += HandleRoomCreated;
            network.OnRoomJoined += HandleRoomJoined;
            network.OnGameStarted += HandleGameStarted;
            network.OnTileDrawn += HandleTileDrawn;
            network.OnTileDiscarded += HandleTileDiscarded;
            network.OnDeckUpdated += HandleDeckUpdated;
            network.OnReconnected += HandleReconnected;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            var network = GameNetworkManager.Instance;
            if (network == null) return;

            network.OnRoomCreated -= HandleRoomCreated;
            network.OnRoomJoined -= HandleRoomJoined;
            network.OnGameStarted -= HandleGameStarted;
            network.OnTileDrawn -= HandleTileDrawn;
            network.OnTileDiscarded -= HandleTileDiscarded;
            network.OnDeckUpdated -= HandleDeckUpdated;
            network.OnReconnected -= HandleReconnected;
        }

        #endregion

        #region Event Handlers

        private void HandleRoomCreated(RoomCreatedData data)
        {
            CommitmentHash = data.CommitmentHash;
            Debug.Log($"[GameState] Oda oluşturuldu: {data.RoomId}, Commitment: {data.CommitmentHash}");
        }

        private void HandleRoomJoined(RoomJoinedData data)
        {
            CommitmentHash = data.CommitmentHash;
            Debug.Log($"[GameState] Odaya katıldı: {data.RoomId}");
        }

        private void HandleGameStarted(GameStartedData data)
        {
            CurrentState = data.InitialState;
            CommitmentHash = data.ServerSeedHash;

            Debug.Log($"[GameState] Oyun başladı! Elimde {CurrentState.Self.Hand.Count} taş var.");
            Debug.Log($"[GameState] Sıra: {(CurrentState.Self.IsCurrentTurn ? "BENDE" : "Rakipte")}");
            Debug.Log($"[GameState] Commitment Hash: {CommitmentHash}");

            OnStateChanged?.Invoke();
            OnHandUpdated?.Invoke();
            OnTurnChanged?.Invoke(CurrentState.CurrentTurnPosition);
        }

        private void HandleTileDrawn(TileDrawnData data)
        {
            if (CurrentState?.Self == null) return;

            // Eli güncelle
            CurrentState.Self.Hand.Add(data.Tile);

            Debug.Log($"[GameState] Taş çekildi: {data.Tile.Color}-{data.Tile.Value} (Okey: {data.Tile.IsOkey})");

            OnTileAdded?.Invoke(data.Tile);
            OnHandUpdated?.Invoke();
        }

        private void HandleTileDiscarded(TileDiscardedData data)
        {
            if (CurrentState == null) return;

            // Eğer biz attıysak, elimizden çıkar
            if (data.PlayerId == GameNetworkManager.Instance.PlayerId)
            {
                CurrentState.Self.Hand.RemoveAll(t => t.Id == data.TileId);
                CurrentState.Self.IsCurrentTurn = false;
                OnTileRemoved?.Invoke(data.TileId);
                OnHandUpdated?.Invoke();
            }

            // Sıra bilgisini güncelle
            CurrentState.CurrentTurnPosition = data.NextTurnPosition;
            CurrentState.Self.IsCurrentTurn = 
                (int)data.NextTurnPlayerId == CurrentState.Self.Position;

            Debug.Log($"[GameState] Taş atıldı: {data.TileId}, Yeni sıra: Pozisyon {data.NextTurnPosition}");

            OnTurnChanged?.Invoke(data.NextTurnPosition);
            OnStateChanged?.Invoke();
        }

        private void HandleDeckUpdated(DeckUpdatedData data)
        {
            if (CurrentState == null) return;

            CurrentState.RemainingTileCount = data.RemainingTileCount;

            Debug.Log($"[GameState] Deste güncellendi: {data.RemainingTileCount} taş kaldı");

            OnStateChanged?.Invoke();
        }

        private void HandleReconnected(ReconnectedData data)
        {
            CurrentState = data.GameState;

            Debug.Log($"[GameState] Yeniden bağlandı! {data.Message}");
            Debug.Log($"[GameState] Elimde {CurrentState.Self.Hand.Count} taş var.");

            OnStateChanged?.Invoke();
            OnHandUpdated?.Invoke();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Oyun durumunu sıfırlar.
        /// </summary>
        public void ResetState()
        {
            CurrentState = null;
            CommitmentHash = null;
            RevealedServerSeed = null;

            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Provably Fair doğrulaması için server seed'i ayarlar.
        /// Oyun bitince server tarafından gönderilir.
        /// </summary>
        public void SetRevealedServerSeed(string serverSeed)
        {
            RevealedServerSeed = serverSeed;
            Debug.Log($"[GameState] Server Seed açıklandı: {serverSeed}");
        }

        /// <summary>
        /// Belirli bir taşı elde arar.
        /// </summary>
        public TileData FindTileInHand(int tileId)
        {
            return CurrentState?.Self?.Hand?.Find(t => t.Id == tileId);
        }

        /// <summary>
        /// Rakip bilgisini pozisyona göre getirir.
        /// </summary>
        public OpponentData GetOpponent(int position)
        {
            return CurrentState?.Opponents?.Find(o => o.Position == position);
        }

        #endregion
    }
}
