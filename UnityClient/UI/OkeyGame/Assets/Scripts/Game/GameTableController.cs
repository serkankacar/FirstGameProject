using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OkeyGame.Core;
using OkeyGame.Models;
using OkeyGame.Network;

namespace OkeyGame.Game
{
    /// <summary>
    /// Oyun masası controller - Oyun mantığını yönetir
    /// </summary>
    public class GameTableController : MonoBehaviour
    {
        public static GameTableController Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private Models.GameState _gameState;
        [SerializeField] private bool _isMyTurn;
        [SerializeField] private float _turnTimeRemaining;
        [SerializeField] private bool _hasDrawnThisTurn;

        [Header("My Hand")]
        [SerializeField] private List<OkeyTile> _myHand = new();
        [SerializeField] private OkeyTile _selectedTile;
        [SerializeField] private OkeyTile _drawnTile; // Bu turda çekilen taş

        [Header("Indicator")]
        [SerializeField] private OkeyTile _indicatorTile;
        [SerializeField] private int _okeyNumber;
        [SerializeField] private TileColor _okeyColor;

        [Header("Demo Mode")]
        private Stack<OkeyTile> _demoDeck = new();
        private List<OkeyTile>[] _botHands = new List<OkeyTile>[3];
        private bool _isDemoMode = false;

        // Events
        public event Action<List<OkeyTile>> OnHandUpdated;
        public event Action<OkeyTile> OnTileSelected;
        public event Action<OkeyTile> OnTileDeselected;
        public event Action<int, float> OnTurnChanged;
        public event Action<OkeyTile> OnTileDrawn;
        public event Action<OkeyTile> OnTileDiscarded;
        public event Action<OkeyTile> OnIndicatorSet;
        public event Action<float> OnTurnTimerTick;
        public event Action OnAutoPlayWarning;
        public event Action<GameEndResult> OnGameEnded;
        
        // Yeni event'ler - bot/rakip aksiyonları için
        public event Action<string, OkeyTile> OnOpponentDiscarded;  // playerId, tile
        public event Action<string, bool> OnOpponentDrew;  // playerId, fromDiscard

        // Properties
        public Models.GameState CurrentGameState => _gameState;
        public bool IsMyTurn => _isMyTurn;
        public float TurnTimeRemaining => _turnTimeRemaining;
        public bool HasDrawnThisTurn => _hasDrawnThisTurn;
        public List<OkeyTile> MyHand => _myHand;
        public OkeyTile SelectedTile => _selectedTile;
        public OkeyTile DrawnTile => _drawnTile; // Bu turda çekilen taş
        public OkeyTile IndicatorTile => _indicatorTile;
        public int OkeyNumber => _okeyNumber;
        public TileColor OkeyColor => _okeyColor;
        public int DeckCount => _gameState?.DeckRemainingCount ?? 0;
        public OkeyTile LastDiscardedTile => _gameState?.LastDiscardedTile;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            // SignalR eventlerini dinle
            if (SignalRConnection.Instance != null)
            {
                SignalRConnection.Instance.OnGameStarted += HandleGameStarted;
                SignalRConnection.Instance.OnGameStateUpdate += HandleGameStateUpdate;
                SignalRConnection.Instance.OnTileDrawn += HandleTileDrawn;
                SignalRConnection.Instance.OnTileDiscarded += HandleTileDiscarded;
                SignalRConnection.Instance.OnTurnChanged += HandleTurnChanged;
                SignalRConnection.Instance.OnGameEnded += HandleGameEnded;
            }
        }

        private void OnDisable()
        {
            if (SignalRConnection.Instance != null)
            {
                SignalRConnection.Instance.OnGameStarted -= HandleGameStarted;
                SignalRConnection.Instance.OnGameStateUpdate -= HandleGameStateUpdate;
                SignalRConnection.Instance.OnTileDrawn -= HandleTileDrawn;
                SignalRConnection.Instance.OnTileDiscarded -= HandleTileDiscarded;
                SignalRConnection.Instance.OnTurnChanged -= HandleTurnChanged;
                SignalRConnection.Instance.OnGameEnded -= HandleGameEnded;
            }
        }

        private void Update()
        {
            if (_isMyTurn && _turnTimeRemaining > 0)
            {
                _turnTimeRemaining -= Time.deltaTime;
                OnTurnTimerTick?.Invoke(_turnTimeRemaining);

                // Auto-play uyarısı
                var settings = GameSettings.Instance;
                if (_turnTimeRemaining <= settings.AutoPlayWarningSeconds && _turnTimeRemaining > settings.AutoPlayWarningSeconds - 0.1f)
                {
                    OnAutoPlayWarning?.Invoke();
                }
            }
        }

        #region SignalR Handlers

        private void HandleGameStarted(GameStartedData data)
        {
            Debug.Log($"[GameTable] Game started! Room: {data.RoomId}");
            
            // Reset state
            _hasDrawnThisTurn = false;
            _selectedTile = null;
            _drawnTile = null;
            
            // Set initial hand
            if (data.InitialHand != null)
            {
                _myHand = data.InitialHand;
            }
            
            // Set indicator
            if (data.IndicatorTile != null)
            {
                SetIndicator(data.IndicatorTile);
            }
            
            // Initialize game state
            _gameState = new Models.GameState
            {
                RoomId = data.RoomId,
                DeckRemainingCount = data.DeckCount,
                CurrentTurnSeatIndex = data.FirstTurnSeatIndex,
                IndicatorTile = data.IndicatorTile
            };
            
            // Check if it's my turn
            var mySeatIndex = GameManager.Instance.CurrentSeatIndex;
            _isMyTurn = data.FirstTurnSeatIndex == mySeatIndex;
            
            // Set default turn time from settings
            _turnTimeRemaining = GameSettings.Instance?.TurnTimeoutSeconds ?? 30f;
            
            // Mark okey tiles
            MarkOkeyTiles();
            
            // Notify UI
            OnHandUpdated?.Invoke(_myHand);
            OnTurnChanged?.Invoke(data.FirstTurnSeatIndex, _turnTimeRemaining);
            
            Debug.Log($"[GameTable] Initial hand: {_myHand.Count} tiles, My turn: {_isMyTurn}");
        }

        private void HandleGameStateUpdate(Models.GameState state)
        {
            _gameState = state;
            
            // Elimizi güncelle
            if (state.MyHand != null)
            {
                _myHand = state.MyHand;
                MarkOkeyTiles();
                OnHandUpdated?.Invoke(_myHand);
            }

            // Gösterge taşı
            if (state.IndicatorTile != null && (_indicatorTile == null || _indicatorTile.Id != state.IndicatorTile.Id))
            {
                SetIndicator(state.IndicatorTile);
            }

            // Sıra kontrolü
            var mySeatIndex = GameManager.Instance.CurrentSeatIndex;
            _isMyTurn = state.CurrentTurnSeatIndex == mySeatIndex;
            _turnTimeRemaining = state.TurnTimeRemaining;

            Debug.Log($"[GameTable] State updated. My turn: {_isMyTurn}, Deck: {state.DeckRemainingCount}");
        }

        private void HandleTileDrawn(string playerId, OkeyTile tile)
        {
            if (playerId == GameManager.Instance.PlayerId)
            {
                // Benim çektiğim taş
                _drawnTile = tile;
                _hasDrawnThisTurn = true;
                _myHand.Add(tile);
                MarkOkeyTiles();
                OnTileDrawn?.Invoke(tile);
                OnHandUpdated?.Invoke(_myHand);
                Debug.Log($"[GameTable] Drew tile: {tile}");
            }
            else
            {
                // Rakip taş çekti (kapalı)
                Debug.Log($"[GameTable] Player {playerId} drew a tile");
                OnOpponentDrew?.Invoke(playerId, false);
            }
            
            // Deste sayısını güncelle
            if (_gameState != null)
            {
                _gameState.DeckRemainingCount = Math.Max(0, _gameState.DeckRemainingCount - 1);
            }
        }

        private void HandleTileDiscarded(string playerId, OkeyTile tile)
        {
            bool isMe = playerId == GameManager.Instance.PlayerId;
            
            if (isMe)
            {
                // Benim attığım taş
                _myHand.RemoveAll(t => t.Id == tile.Id);
                _selectedTile = null;
                _drawnTile = null;
                _hasDrawnThisTurn = false;
                OnHandUpdated?.Invoke(_myHand);
                Debug.Log($"[GameTable] I discarded tile: {tile}");
            }
            else
            {
                // Rakip/Bot taş attı
                Debug.Log($"[GameTable] Opponent {playerId} discarded: {tile.Color} {tile.Number}");
                OnOpponentDiscarded?.Invoke(playerId, tile);
            }
            
            // Atılan taşı atık yığınına ekle (her durumda)
            if (_gameState != null && tile != null)
            {
                _gameState.LastDiscardedTile = tile;
                Debug.Log($"[GameTable] Discard pile updated: {tile.Color} {tile.Number}");
            }
            
            // Her zaman OnTileDiscarded çağır - UI güncellemesi için
            OnTileDiscarded?.Invoke(tile);
        }

        private void HandleTurnChanged(int seatIndex, float timeRemaining)
        {
            var mySeatIndex = GameManager.Instance.CurrentSeatIndex;
            _isMyTurn = seatIndex == mySeatIndex;
            _turnTimeRemaining = timeRemaining;
            _hasDrawnThisTurn = false;
            _drawnTile = null;

            OnTurnChanged?.Invoke(seatIndex, timeRemaining);
            Debug.Log($"[GameTable] Turn changed to seat {seatIndex}. My turn: {_isMyTurn}");
        }

        private void HandleGameEnded(GameEndResult result)
        {
            _isMyTurn = false;
            OnGameEnded?.Invoke(result);
            GameManager.Instance.EndGame(new Core.GameResult
            {
                WinnerId = result.WinnerId,
                WinType = result.WinType,
                WinScore = result.WinScore
            });
        }

        #endregion

        #region Game Actions

        private bool IsDemoMode()
        {
            return _isDemoMode || SignalRConnection.Instance == null || 
                   !SignalRConnection.Instance.IsConnected || 
                   string.IsNullOrEmpty(SignalRConnection.Instance.CurrentRoomId);
        }

        public void DrawFromDeck()
        {
            if (!CanDraw())
            {
                Debug.LogWarning("[GameTable] Cannot draw from deck");
                return;
            }

            if (IsDemoMode())
            {
                DemoDrawFromDeck();
                return;
            }

            SignalRConnection.Instance.DrawFromDeck();
        }

        public void DrawFromDiscard()
        {
            if (!CanDraw())
            {
                Debug.LogWarning("[GameTable] Cannot draw from discard");
                return;
            }

            if (_gameState?.LastDiscardedTile == null)
            {
                Debug.Log("[GameTable] No tile in discard pile - draw from deck instead");
                DrawFromDeck();
                return;
            }

            if (IsDemoMode())
            {
                DemoDrawFromDiscard();
                return;
            }

            SignalRConnection.Instance.DrawFromDiscard();
        }

        private void DemoDrawFromDiscard()
        {
            if (!_isMyTurn || _hasDrawnThisTurn) return;
            
            var tile = _gameState?.LastDiscardedTile;
            if (tile == null) return;
            
            _drawnTile = tile;
            _hasDrawnThisTurn = true;
            _myHand.Add(tile);
            _gameState.LastDiscardedTile = null;
            MarkOkeyTiles();
            
            if (_gameState != null)
            {
                _gameState.Phase = Models.GamePhase.WaitingForDiscard;
            }
            
            OnTileDrawn?.Invoke(tile);
            OnHandUpdated?.Invoke(_myHand);
            OnTurnChanged?.Invoke(0, _turnTimeRemaining);
            
            Debug.Log($"[GameTable] Demo drew from discard: {tile.Color} {tile.Number}");
        }

        public void DiscardTile(OkeyTile tile)
        {
            if (!CanDiscard())
            {
                Debug.LogWarning("[GameTable] Cannot discard");
                return;
            }

            if (tile == null)
            {
                Debug.LogWarning("[GameTable] No tile selected to discard");
                return;
            }

            if (IsDemoMode())
            {
                DemoDiscardTile(tile);
                return;
            }

            SignalRConnection.Instance.DiscardTile(tile.Id);
        }

        public void DiscardSelectedTile()
        {
            DiscardTile(_selectedTile);
        }

        public void DeclareWin()
        {
            if (!CanDeclareWin())
            {
                Debug.LogWarning($"[GameTable] Cannot declare win - IsMyTurn: {_isMyTurn}, HasDrawn: {_hasDrawnThisTurn}, HandCount: {_myHand.Count}");
                return;
            }

            var sets = AnalyzeHand();
            if (sets == null || sets.Count == 0)
            {
                Debug.LogWarning("[GameTable] Invalid hand for winning - sets not valid");
                Debug.Log($"[GameTable] Hand analysis failed. Hand: {string.Join(", ", _myHand.Select(t => $"{t.Color}{t.Number}"))}");
                return;
            }

            if (IsDemoMode())
            {
                DemoDeclareWin(sets);
                return;
            }

            var setIds = sets.Select(s => s.Select(t => t.Id).ToList()).ToList();
            SignalRConnection.Instance.DeclareWin(setIds);
        }

        #endregion

        #region Tile Selection

        public void SelectTile(OkeyTile tile)
        {
            if (_selectedTile != null && _selectedTile.Id == tile.Id)
            {
                // Deselect
                DeselectTile();
                return;
            }

            _selectedTile = tile;
            OnTileSelected?.Invoke(tile);
        }

        public void DeselectTile()
        {
            if (_selectedTile != null)
            {
                var tile = _selectedTile;
                _selectedTile = null;
                OnTileDeselected?.Invoke(tile);
            }
        }

        public void SwapTiles(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _myHand.Count || 
                toIndex < 0 || toIndex >= _myHand.Count)
                return;

            (_myHand[fromIndex], _myHand[toIndex]) = (_myHand[toIndex], _myHand[fromIndex]);
            OnHandUpdated?.Invoke(_myHand);
        }

        #endregion

        #region Helpers

        public bool CanDraw()
        {
            if (!_isMyTurn || _hasDrawnThisTurn) return false;
            
            // Demo modda phase kontrolü esnek
            if (IsDemoMode())
            {
                return _gameState == null || _gameState.Phase == Models.GamePhase.WaitingForDraw;
            }
            
            return _gameState?.Phase == Models.GamePhase.WaitingForDraw;
        }

        public bool CanDiscard()
        {
            if (!_isMyTurn || !_hasDrawnThisTurn) return false;
            
            // Demo modda phase kontrolü esnek
            if (IsDemoMode())
            {
                return _gameState == null || _gameState.Phase == Models.GamePhase.WaitingForDiscard;
            }
            
            return _gameState?.Phase == Models.GamePhase.WaitingForDiscard;
        }

        public bool CanDeclareWin()
        {
            return _isMyTurn && _hasDrawnThisTurn && _myHand.Count == 15;
        }

        private void SetIndicator(OkeyTile indicator)
        {
            _indicatorTile = indicator;
            
            // Okey'i hesapla (göstergenin bir üstü)
            _okeyNumber = indicator.Number == 13 ? 1 : indicator.Number + 1;
            _okeyColor = indicator.Color;

            OnIndicatorSet?.Invoke(indicator);
            MarkOkeyTiles();
            
            Debug.Log($"[GameTable] Indicator: {indicator}, Okey: {_okeyColor} {_okeyNumber}");
        }

        private void MarkOkeyTiles()
        {
            foreach (var tile in _myHand)
            {
                // Sahte okey her zaman okey yerine geçer
                tile.IsOkey = tile.IsFalseOkey || 
                              (tile.Color == _okeyColor && tile.Number == _okeyNumber);
            }
        }

        /// <summary>
        /// Eli analiz et ve geçerli setleri bul
        /// Okey kuralları:
        /// - Per (Run): Aynı renk, ardışık 3+ taş (örn: Sarı 3-4-5)
        /// - Grup: Aynı numara, farklı renk 3-4 taş (örn: 7 Sarı, 7 Mavi, 7 Kırmızı)
        /// - Okey (joker) herhangi bir taş yerine kullanılabilir
        /// - Tüm 15 taş geçerli setlerde olmalı
        /// </summary>
        public List<List<OkeyTile>> AnalyzeHand()
        {
            Debug.Log($"[GameTable] Analyzing hand with {_myHand.Count} tiles...");
            
            // Backtracking ile tüm kombinasyonları dene
            var result = TryFindValidSets(new List<OkeyTile>(_myHand), new List<List<OkeyTile>>());
            
            if (result != null)
            {
                Debug.Log($"[GameTable] Found valid sets: {result.Count} sets");
                foreach (var set in result)
                {
                    Debug.Log($"  Set: {string.Join(", ", set.Select(t => $"{t.Color}{t.Number}{(t.IsOkey ? "*" : "")}"))}");
                }
            }
            else
            {
                Debug.Log("[GameTable] No valid combination found");
            }
            
            return result;
        }

        private List<List<OkeyTile>> TryFindValidSets(List<OkeyTile> remaining, List<List<OkeyTile>> currentSets)
        {
            // Base case: tüm taşlar kullanıldı
            if (remaining.Count == 0)
            {
                return currentSets;
            }

            // Her olası set kombinasyonunu dene
            var possibleSets = FindAllPossibleSets(remaining);
            
            foreach (var set in possibleSets)
            {
                var newRemaining = new List<OkeyTile>(remaining);
                foreach (var tile in set)
                {
                    newRemaining.Remove(tile);
                }

                var newSets = new List<List<OkeyTile>>(currentSets) { set };
                var result = TryFindValidSets(newRemaining, newSets);
                
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private List<List<OkeyTile>> FindAllPossibleSets(List<OkeyTile> tiles)
        {
            var sets = new List<List<OkeyTile>>();
            
            // 1. Grupları bul (aynı numara, farklı renk)
            for (int num = 1; num <= 13; num++)
            {
                var sameTiles = tiles.Where(t => t.Number == num && !t.IsOkey).ToList();
                var okeys = tiles.Where(t => t.IsOkey).ToList();
                
                // 3 veya 4'lü gruplar
                if (sameTiles.Count >= 3)
                {
                    // Farklı renklerden mi kontrol et
                    var uniqueColors = sameTiles.Select(t => t.Color).Distinct().Count();
                    if (uniqueColors >= 3)
                    {
                        var group = sameTiles.GroupBy(t => t.Color).Select(g => g.First()).Take(4).ToList();
                        if (group.Count >= 3)
                        {
                            sets.Add(group);
                        }
                    }
                }
                
                // Okey ile grup (2 taş + 1 okey)
                if (sameTiles.Count >= 2 && okeys.Count >= 1)
                {
                    var uniqueColors = sameTiles.Select(t => t.Color).Distinct().Count();
                    if (uniqueColors >= 2)
                    {
                        var group = sameTiles.GroupBy(t => t.Color).Select(g => g.First()).Take(2).ToList();
                        group.Add(okeys.First());
                        sets.Add(group);
                    }
                }
            }

            // 2. Perler (Run) bul (aynı renk, ardışık numara)
            foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
            {
                var colorTiles = tiles.Where(t => t.Color == color && !t.IsOkey)
                                      .OrderBy(t => t.Number).ToList();
                var okeys = tiles.Where(t => t.IsOkey).ToList();

                // Ardışık 3+ taş ara
                for (int startNum = 1; startNum <= 11; startNum++)
                {
                    for (int length = 3; length <= 13 - startNum + 1; length++)
                    {
                        var run = new List<OkeyTile>();
                        int okeyUsed = 0;
                        bool valid = true;

                        for (int i = 0; i < length && valid; i++)
                        {
                            int targetNum = startNum + i;
                            var tile = colorTiles.FirstOrDefault(t => t.Number == targetNum && !run.Contains(t));
                            
                            if (tile != null)
                            {
                                run.Add(tile);
                            }
                            else if (okeyUsed < okeys.Count)
                            {
                                run.Add(okeys[okeyUsed]);
                                okeyUsed++;
                            }
                            else
                            {
                                valid = false;
                            }
                        }

                        if (valid && run.Count >= 3)
                        {
                            sets.Add(run);
                        }
                    }
                }
            }

            return sets;
        }

        /// <summary>
        /// Çiftler kontrolü (7 çift = kazanma)
        /// </summary>
        public bool CheckPairs()
        {
            if (_myHand.Count != 14) return false;

            var sorted = _myHand.OrderBy(t => t.Color).ThenBy(t => t.Number).ToList();
            
            for (int i = 0; i < sorted.Count; i += 2)
            {
                if (i + 1 >= sorted.Count) return false;
                
                var t1 = sorted[i];
                var t2 = sorted[i + 1];

                // Aynı taş veya okey ile eşleşme
                if (!((t1.Color == t2.Color && t1.Number == t2.Number) || t1.IsOkey || t2.IsOkey))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Hand Sorting

        /// <summary>
        /// Taşları renge ve numaraya göre sırala
        /// </summary>
        public void SortHandByColor()
        {
            _myHand = _myHand.OrderBy(t => t.Color).ThenBy(t => t.Number).ToList();
            OnHandUpdated?.Invoke(_myHand);
            Debug.Log("[GameTable] Hand sorted by color");
        }

        /// <summary>
        /// Taşları numaraya ve renge göre sırala (gruplar için)
        /// </summary>
        public void SortHandByNumber()
        {
            _myHand = _myHand.OrderBy(t => t.Number).ThenBy(t => t.Color).ToList();
            OnHandUpdated?.Invoke(_myHand);
            Debug.Log("[GameTable] Hand sorted by number");
        }

        #endregion

        #region Demo Mode

        /// <summary>
        /// Demo modda oyunu başlat - offline test için
        /// </summary>
        public void StartDemoGame()
        {
            Debug.Log("[GameTable] Starting demo game...");
            _isDemoMode = true;
            
            // Reset state
            _hasDrawnThisTurn = false;
            _selectedTile = null;
            _drawnTile = null;
            
            // Deste oluştur
            InitializeDemoDeck();
            
            // Gösterge taşı
            var indicator = DrawFromDemoDeck();
            SetIndicator(indicator);
            
            // Oyuncu eli
            _myHand = new List<OkeyTile>();
            for (int i = 0; i < 14; i++)
            {
                var tile = DrawFromDemoDeck();
                if (tile != null) _myHand.Add(tile);
            }
            
            // Bot elleri
            for (int botIndex = 0; botIndex < 3; botIndex++)
            {
                _botHands[botIndex] = new List<OkeyTile>();
                for (int i = 0; i < 14; i++)
                {
                    var tile = DrawFromDemoDeck();
                    if (tile != null) _botHands[botIndex].Add(tile);
                }
            }
            
            // Game state
            _gameState = new Models.GameState
            {
                RoomId = "demo-room",
                DeckRemainingCount = _demoDeck.Count,
                CurrentTurnSeatIndex = 0,
                IndicatorTile = indicator,
                Phase = Models.GamePhase.WaitingForDraw,
                Players = new List<PlayerInfo>
                {
                    new PlayerInfo { Id = "player", Username = "Sen", IsBot = false },
                    new PlayerInfo { Id = "bot-1", Username = "Bot 1", IsBot = true },
                    new PlayerInfo { Id = "bot-2", Username = "Bot 2", IsBot = true },
                    new PlayerInfo { Id = "bot-3", Username = "Bot 3", IsBot = true }
                }
            };
            
            _isMyTurn = true;
            _turnTimeRemaining = GameSettings.Instance?.TurnTimeoutSeconds ?? 30f;
            
            MarkOkeyTiles();
            
            OnHandUpdated?.Invoke(_myHand);
            OnTurnChanged?.Invoke(0, _turnTimeRemaining);
            OnIndicatorSet?.Invoke(indicator);
            
            Debug.Log($"[GameTable] Demo started! Hand: {_myHand.Count}, Deck: {_demoDeck.Count}");
        }

        private void InitializeDemoDeck()
        {
            var allTiles = new List<OkeyTile>();
            var colors = new[] { TileColor.Red, TileColor.Blue, TileColor.Yellow, TileColor.Black };
            int tileId = 1;
            
            // 4 renk x 13 numara x 2 kopya = 104 taş
            for (int copy = 0; copy < 2; copy++)
            {
                foreach (var color in colors)
                {
                    for (int number = 1; number <= 13; number++)
                    {
                        allTiles.Add(new OkeyTile
                        {
                            Id = tileId++,
                            Color = color,
                            Number = number,
                            IsFaceDown = false,
                            IsFalseOkey = false
                        });
                    }
                }
            }
            
            // 2 sahte okey
            for (int i = 0; i < 2; i++)
            {
                allTiles.Add(new OkeyTile
                {
                    Id = tileId++,
                    Color = TileColor.Black,
                    Number = 0,
                    IsFaceDown = false,
                    IsFalseOkey = true
                });
            }
            
            // Fisher-Yates shuffle
            var random = new System.Random();
            for (int i = allTiles.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (allTiles[i], allTiles[j]) = (allTiles[j], allTiles[i]);
            }
            
            _demoDeck = new Stack<OkeyTile>(allTiles);
        }

        private OkeyTile DrawFromDemoDeck()
        {
            return _demoDeck.Count > 0 ? _demoDeck.Pop() : null;
        }

        private void DemoDrawFromDeck()
        {
            var tile = DrawFromDemoDeck();
            if (tile == null) return;
            
            _drawnTile = tile;
            _hasDrawnThisTurn = true;
            _myHand.Add(tile);
            MarkOkeyTiles();
            
            if (_gameState != null)
            {
                _gameState.DeckRemainingCount = _demoDeck.Count;
                _gameState.Phase = Models.GamePhase.WaitingForDiscard;
            }
            
            OnTileDrawn?.Invoke(tile);
            OnHandUpdated?.Invoke(_myHand);
            OnTurnChanged?.Invoke(0, _turnTimeRemaining);
            
            Debug.Log($"[GameTable] Demo drew: {tile.Color} {tile.Number}");
        }

        private void DemoDiscardTile(OkeyTile tile)
        {
            _myHand.Remove(tile);
            _selectedTile = null;
            _drawnTile = null;
            _hasDrawnThisTurn = false;
            
            if (_gameState != null)
            {
                _gameState.LastDiscardedTile = tile;
                _gameState.Phase = Models.GamePhase.WaitingForDraw;
            }
            
            OnTileDiscarded?.Invoke(tile);
            OnHandUpdated?.Invoke(_myHand);
            
            Debug.Log($"[GameTable] Demo discarded: {tile.Color} {tile.Number}");
            
            StartCoroutine(SimulateBotTurns());
        }

        private void DemoDeclareWin(List<List<OkeyTile>> sets)
        {
            Debug.Log($"[GameTable] DEMO WIN! Sets: {sets.Count}");
            
            var result = new GameEndResult
            {
                WinnerId = "player",
                WinnerName = "Sen",
                WinType = "Normal",
                IsMyWin = true
            };
            
            OnGameEnded?.Invoke(result);
        }

        private IEnumerator SimulateBotTurns()
        {
            var random = new System.Random();
            
            for (int botIndex = 0; botIndex < 3; botIndex++)
            {
                int botSeat = botIndex + 1;
                string botId = $"bot-{botIndex + 1}";
                
                if (_botHands[botIndex] == null || _botHands[botIndex].Count == 0) continue;
                
                _isMyTurn = false;
                OnTurnChanged?.Invoke(botSeat, 30f);
                
                yield return new WaitForSeconds(0.8f + (float)random.NextDouble() * 0.5f);
                
                // Bot taş çeker
                var drawnTile = DrawFromDemoDeck();
                if (drawnTile != null)
                {
                    _botHands[botIndex].Add(drawnTile);
                    OnOpponentDrew?.Invoke(botId, false);
                    if (_gameState != null) _gameState.DeckRemainingCount = _demoDeck.Count;
                }
                
                yield return new WaitForSeconds(0.5f + (float)random.NextDouble() * 0.5f);
                
                // Bot taş atar
                if (_botHands[botIndex].Count > 0)
                {
                    int idx = random.Next(_botHands[botIndex].Count);
                    var discarded = _botHands[botIndex][idx];
                    _botHands[botIndex].RemoveAt(idx);
                    
                    if (_gameState != null) _gameState.LastDiscardedTile = discarded;
                    
                    OnOpponentDiscarded?.Invoke(botId, discarded);
                    OnTileDiscarded?.Invoke(discarded);
                }
                
                yield return new WaitForSeconds(0.2f);
            }
            
            // Sıra oyuncuya
            _isMyTurn = true;
            _hasDrawnThisTurn = false;
            _turnTimeRemaining = GameSettings.Instance?.TurnTimeoutSeconds ?? 30f;
            
            if (_gameState != null) _gameState.Phase = Models.GamePhase.WaitingForDraw;
            
            OnTurnChanged?.Invoke(0, _turnTimeRemaining);
        }

        #endregion
    }
}
