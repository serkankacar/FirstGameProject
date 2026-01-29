using System;
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

        // Properties
        public Models.GameState CurrentGameState => _gameState;
        public bool IsMyTurn => _isMyTurn;
        public float TurnTimeRemaining => _turnTimeRemaining;
        public bool HasDrawnThisTurn => _hasDrawnThisTurn;
        public List<OkeyTile> MyHand => _myHand;
        public OkeyTile SelectedTile => _selectedTile;
        public OkeyTile IndicatorTile => _indicatorTile;
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
            }
        }

        private void HandleTileDiscarded(string playerId, OkeyTile tile)
        {
            if (playerId == GameManager.Instance.PlayerId)
            {
                // Benim attığım taş
                _myHand.RemoveAll(t => t.Id == tile.Id);
                _selectedTile = null;
                _drawnTile = null;
                _hasDrawnThisTurn = false;
                OnTileDiscarded?.Invoke(tile);
                OnHandUpdated?.Invoke(_myHand);
                Debug.Log($"[GameTable] Discarded tile: {tile}");
            }
            else
            {
                // Rakip taş attı
                Debug.Log($"[GameTable] Player {playerId} discarded: {tile}");
            }
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

        public void DrawFromDeck()
        {
            if (!CanDraw())
            {
                Debug.LogWarning("[GameTable] Cannot draw from deck");
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
                Debug.LogWarning("[GameTable] No tile in discard pile");
                return;
            }

            SignalRConnection.Instance.DrawFromDiscard();
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
                Debug.LogWarning("[GameTable] Cannot declare win");
                return;
            }

            var sets = AnalyzeHand();
            if (sets == null || sets.Count == 0)
            {
                Debug.LogWarning("[GameTable] Invalid hand for winning");
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
            return _isMyTurn && !_hasDrawnThisTurn && 
                   _gameState?.Phase == Models.GamePhase.WaitingForDraw;
        }

        public bool CanDiscard()
        {
            return _isMyTurn && _hasDrawnThisTurn && 
                   _gameState?.Phase == Models.GamePhase.WaitingForDiscard;
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
        /// </summary>
        public List<List<OkeyTile>> AnalyzeHand()
        {
            // Bu basit bir implementasyon - gerçek oyunda daha kompleks algoritma gerekir
            var sets = new List<List<OkeyTile>>();
            var remaining = new List<OkeyTile>(_myHand);

            // Önce grupları bul (aynı numara, farklı renk)
            for (int number = 1; number <= 13; number++)
            {
                var sameTiles = remaining.Where(t => t.Number == number || t.IsOkey).ToList();
                if (sameTiles.Count >= 3)
                {
                    var group = sameTiles.Take(Math.Min(4, sameTiles.Count)).ToList();
                    sets.Add(group);
                    foreach (var t in group) remaining.Remove(t);
                }
            }

            // Sonra runları bul (aynı renk, ardışık numara)
            foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
            {
                var colorTiles = remaining.Where(t => t.Color == color || t.IsOkey)
                                          .OrderBy(t => t.Number).ToList();
                
                var run = new List<OkeyTile>();
                int expectedNumber = -1;

                foreach (var tile in colorTiles)
                {
                    if (expectedNumber == -1 || tile.Number == expectedNumber || tile.IsOkey)
                    {
                        run.Add(tile);
                        expectedNumber = (tile.IsOkey ? expectedNumber : tile.Number) + 1;
                    }
                    else
                    {
                        if (run.Count >= 3)
                        {
                            sets.Add(new List<OkeyTile>(run));
                            foreach (var t in run) remaining.Remove(t);
                        }
                        run.Clear();
                        run.Add(tile);
                        expectedNumber = tile.Number + 1;
                    }
                }

                if (run.Count >= 3)
                {
                    sets.Add(run);
                    foreach (var t in run) remaining.Remove(t);
                }
            }

            // Tüm taşlar set içinde mi kontrol et
            if (remaining.Count == 0)
            {
                return sets;
            }

            return null; // Geçerli el değil
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
    }
}
