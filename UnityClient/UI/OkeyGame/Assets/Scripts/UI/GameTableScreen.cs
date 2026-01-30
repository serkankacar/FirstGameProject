using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using OkeyGame.Core;
using OkeyGame.Game;
using OkeyGame.Models;

namespace OkeyGame.UI
{
    /// <summary>
    /// Oyun masası ekranı controller
    /// </summary>
    public class GameTableScreen : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        
        // Top bar
        private Label _roomName;
        private Label _stakeBadge;
        private Label _timerText;
        private Button _menuButton;

        // Center area
        private VisualElement _indicatorTile;
        private Label _indicatorNumber;
        private Button _deckPile;
        private Label _deckCount;
        private Button _discardPile;
        private Label _discardTileNumber;

        // Opponents
        private VisualElement[] _opponentInfos = new VisualElement[3];
        private Label[] _opponentNames = new Label[3];
        private Label[] _opponentTileCounts = new Label[3];

        // Player hand
        private Label _playerStatus;
        private Button _sortColorButton;
        private Button _sortNumberButton;
        private Button _discardButton;
        private Button _winButton;
        private VisualElement _handTilesContainer;

        // Game over
        private VisualElement _gameOverModal;
        private Label _winnerName;
        private Label _winType;
        private VisualElement _resultsContainer;
        private Button _continueButton;

        // Tile visuals
        private readonly Dictionary<int, VisualElement> _tileElements = new();
        public bool IsInitialized { get; private set; } = false;

        private void OnEnable()
        {
            _root = _uiDocument.rootVisualElement;
            InitializeUIReferences();
            RegisterCallbacks();
            SubscribeToGameEvents();
            IsInitialized = true;
        }

        private void Start()
        {
            // Oyun başlayınca backend'den veri bekleniyor
            Debug.Log("[GameTableScreen] Waiting for game to start from backend...");
        }

        private void OnDisable()
        {
            UnsubscribeFromGameEvents();
        }

        private void InitializeUIReferences()
        {
            if (_root == null)
            {
                Debug.LogError("[GameTableScreen] Root is null!");
                return;
            }
            
            // Top bar
            _roomName = _root.Q<Label>("room-name");
            _stakeBadge = _root.Q<Label>("stake-badge");
            _timerText = _root.Q<Label>("timer-text");
            _menuButton = _root.Q<Button>("menu-button");

            // Center area
            _indicatorTile = _root.Q<VisualElement>("indicator-tile");
            _indicatorNumber = _root.Q<Label>("indicator-number");
            _deckPile = _root.Q<Button>("deck-pile");
            _deckCount = _root.Q<Label>("deck-count");
            _discardPile = _root.Q<Button>("discard-pile");
            _discardTileNumber = _root.Q<Label>("discard-tile-number");

            // Opponents (seats 1, 2, 3 - player is seat 0)
            for (int i = 0; i < 3; i++)
            {
                _opponentInfos[i] = _root.Q<VisualElement>($"opponent-info-{i + 1}");
                _opponentNames[i] = _root.Q<Label>($"opponent-name-{i + 1}");
                _opponentTileCounts[i] = _root.Q<Label>($"opponent-tiles-{i + 1}");
            }

            // Player hand
            _playerStatus = _root.Q<Label>("player-status");
            _sortColorButton = _root.Q<Button>("sort-color-button");
            _sortNumberButton = _root.Q<Button>("sort-number-button");
            _discardButton = _root.Q<Button>("discard-button");
            _winButton = _root.Q<Button>("win-button");
            _handTilesContainer = _root.Q<VisualElement>("hand-tiles-container");

            // Game over
            _gameOverModal = _root.Q<VisualElement>("game-over-modal");
            _winnerName = _root.Q<Label>("winner-name");
            _winType = _root.Q<Label>("win-type");
            _resultsContainer = _root.Q<VisualElement>("results-container");
            _continueButton = _root.Q<Button>("continue-button");
            
            Debug.Log($"[GameTableScreen] UI initialized - HandContainer: {_handTilesContainer != null}, DeckPile: {_deckPile != null}");
        }

        private void RegisterCallbacks()
        {
            _menuButton?.RegisterCallback<ClickEvent>(evt => OnMenuClicked());
            _deckPile?.RegisterCallback<ClickEvent>(evt => OnDeckClicked());
            _discardPile?.RegisterCallback<ClickEvent>(evt => OnDiscardPileClicked());
            _sortColorButton?.RegisterCallback<ClickEvent>(evt => OnSortByColorClicked());
            _sortNumberButton?.RegisterCallback<ClickEvent>(evt => OnSortByNumberClicked());
            _discardButton?.RegisterCallback<ClickEvent>(evt => OnDiscardButtonClicked());
            _winButton?.RegisterCallback<ClickEvent>(evt => OnWinButtonClicked());
            _continueButton?.RegisterCallback<ClickEvent>(evt => OnContinueClicked());
        }

        private void SubscribeToGameEvents()
        {
            var controller = GameTableController.Instance;
            if (controller != null)
            {
                controller.OnHandUpdated += UpdateHandDisplay;
                controller.OnTurnChanged += UpdateTurnDisplay;
                controller.OnTurnTimerTick += UpdateTimer;
                controller.OnTileSelected += OnTileSelected;
                controller.OnTileDeselected += OnTileDeselected;
                controller.OnIndicatorSet += UpdateIndicator;
                controller.OnGameEnded += ShowGameOverModal;
                controller.OnAutoPlayWarning += ShowAutoPlayWarning;
                controller.OnTileDiscarded += OnTileDiscardedByPlayer;
                controller.OnTileDrawn += OnTileDrawnByPlayer;
                controller.OnOpponentDiscarded += OnOpponentDiscardedTile;
                controller.OnOpponentDrew += OnOpponentDrewTile;
            }
        }

        private void UnsubscribeFromGameEvents()
        {
            var controller = GameTableController.Instance;
            if (controller != null)
            {
                controller.OnHandUpdated -= UpdateHandDisplay;
                controller.OnTurnChanged -= UpdateTurnDisplay;
                controller.OnTurnTimerTick -= UpdateTimer;
                controller.OnTileSelected -= OnTileSelected;
                controller.OnTileDeselected -= OnTileDeselected;
                controller.OnIndicatorSet -= UpdateIndicator;
                controller.OnGameEnded -= ShowGameOverModal;
                controller.OnAutoPlayWarning -= ShowAutoPlayWarning;
                controller.OnTileDiscarded -= OnTileDiscardedByPlayer;
                controller.OnTileDrawn -= OnTileDrawnByPlayer;
                controller.OnOpponentDiscarded -= OnOpponentDiscardedTile;
                controller.OnOpponentDrew -= OnOpponentDrewTile;
            }
        }

        #region UI Updates

        private void UpdateHandDisplay(List<OkeyTile> hand)
        {
            if (_handTilesContainer == null) return;
            
            _handTilesContainer.Clear();
            _tileElements.Clear();

            if (hand == null || hand.Count == 0)
            {
                Debug.Log("[GameTableScreen] Hand is empty or null");
                return;
            }

            foreach (var tile in hand)
            {
                var tileElement = CreateTileElement(tile);
                _handTilesContainer.Add(tileElement);
                _tileElements[tile.Id] = tileElement;
            }

            UpdateActionButtons();
            Debug.Log($"[GameTableScreen] Updated hand display with {hand.Count} tiles");
        }

        private VisualElement CreateTileElement(OkeyTile tile)
        {
            var element = new VisualElement();
            element.AddToClassList("tile");
            element.userData = tile; // Tile referansını sakla
            
            // Okey taşı ise özel görsel
            if (tile.IsOkey)
            {
                element.AddToClassList("okey");
                
                // Okey göstergesi - köşede yıldız
                var okeyBadge = new Label("★");
                okeyBadge.AddToClassList("okey-badge");
                element.Add(okeyBadge);
            }
            
            // Bu turda çekilen taş mı?
            var drawnTile = GameTableController.Instance?.DrawnTile;
            if (drawnTile != null && drawnTile.Id == tile.Id)
            {
                element.AddToClassList("just-drawn");
            }

            var numberLabel = new Label();
            numberLabel.AddToClassList("tile-number");
            
            if (tile.IsFalseOkey)
            {
                numberLabel.text = "★";
                numberLabel.AddToClassList("false-joker");
                
                var jokerLabel = new Label("JOKER");
                jokerLabel.AddToClassList("tile-joker-label");
                element.Add(jokerLabel);
            }
            else
            {
                numberLabel.text = tile.Number.ToString();
            }

            // Color class
            switch (tile.Color)
            {
                case TileColor.Yellow: numberLabel.AddToClassList("yellow"); break;
                case TileColor.Blue: numberLabel.AddToClassList("blue"); break;
                case TileColor.Black: numberLabel.AddToClassList("black"); break;
                case TileColor.Red: numberLabel.AddToClassList("red"); break;
            }

            element.Add(numberLabel);

            // Sürükle-bırak için pointer eventleri
            SetupTileDragDrop(element, tile);

            // Click handler
            element.RegisterCallback<ClickEvent>(evt => OnTileClicked(tile));

            return element;
        }
        
        private int _dragStartIndex = -1;
        private VisualElement _draggedTile;
        
        private void SetupTileDragDrop(VisualElement element, OkeyTile tile)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return; // Sol tık
                
                _draggedTile = element;
                _dragStartIndex = _handTilesContainer.IndexOf(element);
                element.AddToClassList("dragging");
                element.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            
            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_draggedTile != element) return;
                // Sürükleme görselini güncelle - opacity azalt
                element.style.opacity = 0.7f;
            });
            
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_draggedTile != element) return;
                
                element.RemoveFromClassList("dragging");
                element.style.opacity = 1f;
                element.ReleasePointer(evt.pointerId);
                
                // Bırakılan pozisyondaki taşı bul
                int dropIndex = FindDropIndex(evt.position);
                
                if (dropIndex >= 0 && dropIndex != _dragStartIndex)
                {
                    // Taşları yer değiştir
                    GameTableController.Instance?.SwapTiles(_dragStartIndex, dropIndex);
                }
                
                _draggedTile = null;
                _dragStartIndex = -1;
                evt.StopPropagation();
            });
        }
        
        private int FindDropIndex(Vector2 pointerPosition)
        {
            if (_handTilesContainer == null) return -1;
            
            for (int i = 0; i < _handTilesContainer.childCount; i++)
            {
                var child = _handTilesContainer[i];
                if (child.worldBound.Contains(pointerPosition))
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpdateTurnDisplay(int seatIndex, float timeRemaining)
        {
            var controller = GameTableController.Instance;
            bool isMyTurn = controller?.IsMyTurn ?? false;

            // Update status text
            if (isMyTurn)
            {
                if (controller.HasDrawnThisTurn)
                {
                    _playerStatus.text = "Bir taş atın!";
                }
                else
                {
                    _playerStatus.text = "Sıra sizde! Taş çekin.";
                }
                _playerStatus.AddToClassList("my-turn");
            }
            else
            {
                _playerStatus.text = "Rakibin sırası...";
                _playerStatus.RemoveFromClassList("my-turn");
            }

            // Update deck/discard clickability
            UpdateDrawIndicators();
            UpdateActionButtons();

            // Highlight current player's info
            UpdateOpponentHighlights(seatIndex);
        }

        private void UpdateTimer(float timeRemaining)
        {
            int seconds = Mathf.CeilToInt(timeRemaining);
            _timerText.text = seconds.ToString();

            _timerText.RemoveFromClassList("warning");
            _timerText.RemoveFromClassList("critical");

            if (seconds <= 5)
            {
                _timerText.AddToClassList("critical");
            }
            else if (seconds <= 10)
            {
                _timerText.AddToClassList("warning");
            }
        }

        private void UpdateIndicator(OkeyTile indicator)
        {
            if (indicator == null || _indicatorNumber == null) return;
            
            _indicatorNumber.text = indicator.Number.ToString();
            
            // Update color
            _indicatorNumber.RemoveFromClassList("yellow");
            _indicatorNumber.RemoveFromClassList("blue");
            _indicatorNumber.RemoveFromClassList("black");
            _indicatorNumber.RemoveFromClassList("red");

            switch (indicator.Color)
            {
                case TileColor.Yellow: _indicatorNumber.AddToClassList("yellow"); break;
                case TileColor.Blue: _indicatorNumber.AddToClassList("blue"); break;
                case TileColor.Black: _indicatorNumber.AddToClassList("black"); break;
                case TileColor.Red: _indicatorNumber.AddToClassList("red"); break;
            }
            
            // Okey bilgisini göster (indicator'dan hesapla)
            var controller = GameTableController.Instance;
            if (controller != null)
            {
                int okeyNum = controller.OkeyNumber;
                TileColor okeyColor = controller.OkeyColor;
                Debug.Log($"[GameTableScreen] Okey: {okeyColor} {okeyNum}");
            }
        }

        private void UpdateDrawIndicators()
        {
            var controller = GameTableController.Instance;
            bool canDraw = controller?.CanDraw() ?? false;

            _deckPile?.EnableInClassList("can-draw", canDraw);
            
            bool hasDiscard = controller?.LastDiscardedTile != null;
            _discardPile?.EnableInClassList("can-draw", canDraw && hasDiscard);

            // Update discard tile display
            if (_discardTileNumber != null)
            {
                if (controller?.LastDiscardedTile != null)
                {
                    var tile = controller.LastDiscardedTile;
                    _discardTileNumber.text = tile.IsFalseOkey ? "J" : tile.Number.ToString();
                    
                    _discardTileNumber.RemoveFromClassList("yellow");
                    _discardTileNumber.RemoveFromClassList("blue");
                    _discardTileNumber.RemoveFromClassList("black");
                    _discardTileNumber.RemoveFromClassList("red");

                    switch (tile.Color)
                    {
                        case TileColor.Yellow: _discardTileNumber.AddToClassList("yellow"); break;
                        case TileColor.Blue: _discardTileNumber.AddToClassList("blue"); break;
                        case TileColor.Black: _discardTileNumber.AddToClassList("black"); break;
                        case TileColor.Red: _discardTileNumber.AddToClassList("red"); break;
                    }
                }
                else
                {
                    _discardTileNumber.text = "";
                }
            }

            // Update deck count
            if (_deckCount != null)
            {
                _deckCount.text = controller?.DeckCount.ToString() ?? "0";
            }
        }

        private void UpdateActionButtons()
        {
            var controller = GameTableController.Instance;
            
            _discardButton?.SetEnabled(controller?.CanDiscard() == true && controller?.SelectedTile != null);
            _winButton?.SetEnabled(controller?.CanDeclareWin() == true);
        }

        private void UpdateOpponentHighlights(int currentTurnSeat)
        {
            int mySeat = GameManager.Instance?.CurrentSeatIndex ?? 0;

            for (int i = 0; i < 3; i++)
            {
                // Calculate which seat this opponent UI represents
                int opponentSeat = (mySeat + i + 1) % 4;
                
                if (opponentSeat == currentTurnSeat)
                {
                    _opponentInfos[i].AddToClassList("current-turn");
                }
                else
                {
                    _opponentInfos[i].RemoveFromClassList("current-turn");
                }
            }
        }

        private void OnTileSelected(OkeyTile tile)
        {
            if (_tileElements.TryGetValue(tile.Id, out var element))
            {
                element.AddToClassList("selected");
            }
            UpdateActionButtons();
        }

        private void OnTileDeselected(OkeyTile tile)
        {
            if (_tileElements.TryGetValue(tile.Id, out var element))
            {
                element.RemoveFromClassList("selected");
            }
            UpdateActionButtons();
        }

        private void ShowAutoPlayWarning()
        {
            Debug.Log("[GameTable] Auto-play warning!");
            // TODO: Show visual/audio warning
            // Vibrate, flash timer, play sound
        }

        private void ShowGameOverModal(GameEndResult result)
        {
            Debug.Log($"[GameTableScreen] Showing game over modal. Winner: {result?.WinnerName}");
            
            if (_gameOverModal == null)
            {
                Debug.LogError("[GameTableScreen] Game over modal not found!");
                return;
            }
            
            _winnerName.text = $"Kazanan: {result?.WinnerName ?? "Bilinmiyor"}";
            _winType.text = GetWinTypeText(result?.WinType ?? "Normal");

            _resultsContainer?.Clear();
            
            if (result?.PlayerResults != null)
            {
                foreach (var playerResult in result.PlayerResults)
                {
                    var row = new VisualElement();
                    row.AddToClassList("result-row");

                    var nameLabel = new Label(playerResult.Username);
                    nameLabel.AddToClassList("result-player");
                    row.Add(nameLabel);

                    var chipsLabel = new Label();
                    chipsLabel.AddToClassList("result-chips");
                    
                    if (playerResult.ChipChange > 0)
                    {
                        chipsLabel.text = $"+{playerResult.ChipChange:N0}";
                        chipsLabel.AddToClassList("positive");
                    }
                    else
                    {
                        chipsLabel.text = $"{playerResult.ChipChange:N0}";
                        chipsLabel.AddToClassList("negative");
                    }
                    row.Add(chipsLabel);

                    _resultsContainer?.Add(row);
                }
            }
            else
            {
                // Demo modda basit sonuç göster
                var row = new VisualElement();
                row.AddToClassList("result-row");
                
                var nameLabel = new Label(result?.IsMyWin == true ? "Tebrikler! Kazandınız!" : "Oyun bitti");
                nameLabel.AddToClassList("result-player");
                row.Add(nameLabel);
                
                _resultsContainer?.Add(row);
            }

            _gameOverModal.style.display = DisplayStyle.Flex;
        }

        private string GetWinTypeText(string winType)
        {
            return winType switch
            {
                "Normal" => "Normal kazanç",
                "Pairs" => "Çiftler ile kazandı! (x1.5)",
                "OkeyDiscard" => "Okey atarak kazandı! (x2)",
                "DeckEmpty" => "Deste bitti (x0.5)",
                _ => winType
            };
        }

        #endregion

        #region Event Handlers

        private void OnTileClicked(OkeyTile tile)
        {
            GameTableController.Instance?.SelectTile(tile);
        }

        private void OnDeckClicked()
        {
            Debug.Log("[GameTableScreen] Deck clicked - attempting to draw");
            GameTableController.Instance?.DrawFromDeck();
        }

        private void OnDiscardPileClicked()
        {
            Debug.Log("[GameTableScreen] Discard pile clicked - attempting to draw");
            GameTableController.Instance?.DrawFromDiscard();
        }

        private void OnSortByColorClicked()
        {
            Debug.Log("[GameTableScreen] Sort by color clicked");
            GameTableController.Instance?.SortHandByColor();
        }

        private void OnSortByNumberClicked()
        {
            Debug.Log("[GameTableScreen] Sort by number clicked");
            GameTableController.Instance?.SortHandByNumber();
        }

        private void OnDiscardButtonClicked()
        {
            Debug.Log("[GameTableScreen] Discard button clicked");
            GameTableController.Instance?.DiscardSelectedTile();
        }

        private void OnWinButtonClicked()
        {
            GameTableController.Instance?.DeclareWin();
        }

        private void OnMenuClicked()
        {
            Debug.Log("[GameTable] Menu clicked");
            // TODO: Show pause menu
        }

        private void OnContinueClicked()
        {
            _gameOverModal.style.display = DisplayStyle.None;
            GameManager.Instance?.LeaveRoom();
            SceneController.Instance?.ShowLobby();
        }

        /// <summary>
        /// Bir taş atıldığında çağrılır - atık yığınını günceller
        /// </summary>
        private void OnTileDiscardedByPlayer(OkeyTile tile)
        {
            Debug.Log($"[GameTableScreen] Tile discarded: {tile?.Color} {tile?.Number}");
            UpdateDrawIndicators();
            UpdateDiscardPileDisplay(tile);
        }

        /// <summary>
        /// Bir taş çekildiğinde çağrılır - deste sayısını günceller
        /// </summary>
        private void OnTileDrawnByPlayer(OkeyTile tile)
        {
            Debug.Log($"[GameTableScreen] Tile drawn: {tile?.Color} {tile?.Number}");
            UpdateDrawIndicators();
        }

        /// <summary>
        /// Rakip/Bot taş attığında çağrılır - görsel feedback verir
        /// </summary>
        private void OnOpponentDiscardedTile(string playerId, OkeyTile tile)
        {
            Debug.Log($"[GameTableScreen] Opponent {playerId} discarded: {tile?.Color} {tile?.Number}");
            
            // Atılan taşı güncelle
            UpdateDiscardPileDisplay(tile);
            
            // Hangi oyuncunun attığını bul ve görsel feedback ver
            int opponentIndex = GetOpponentIndexByPlayerId(playerId);
            if (opponentIndex >= 0 && opponentIndex < _opponentInfos.Length && _opponentInfos[opponentIndex] != null)
            {
                // Oyuncu panelini kısa süreliğine vurgula (taş attı efekti)
                var panel = _opponentInfos[opponentIndex];
                panel.AddToClassList("discarding");
                
                // 800ms sonra efekti kaldır
                panel.schedule.Execute(() =>
                {
                    panel.RemoveFromClassList("discarding");
                }).StartingIn(800);
            }
            
            // Atık yığınını animasyonla vurgula
            if (_discardPile != null)
            {
                _discardPile.AddToClassList("new-tile");
                _discardPile.schedule.Execute(() =>
                {
                    _discardPile.RemoveFromClassList("new-tile");
                }).StartingIn(500);
            }
        }

        /// <summary>
        /// Rakip/Bot taş çektiğinde çağrılır - görsel feedback verir
        /// </summary>
        private void OnOpponentDrewTile(string playerId, bool fromDiscard)
        {
            Debug.Log($"[GameTableScreen] Opponent {playerId} drew a tile (fromDiscard: {fromDiscard})");
            
            // Hangi oyuncunun çektiğini bul ve görsel feedback ver
            int opponentIndex = GetOpponentIndexByPlayerId(playerId);
            if (opponentIndex >= 0 && opponentIndex < _opponentInfos.Length && _opponentInfos[opponentIndex] != null)
            {
                var panel = _opponentInfos[opponentIndex];
                panel.AddToClassList("drawing");
                
                // 600ms sonra efekti kaldır
                panel.schedule.Execute(() =>
                {
                    panel.RemoveFromClassList("drawing");
                }).StartingIn(600);
            }
            
            // Deste sayısını güncelle
            UpdateDrawIndicators();
        }

        /// <summary>
        /// Atık yığını görüntüsünü günceller
        /// </summary>
        private void UpdateDiscardPileDisplay(OkeyTile tile)
        {
            if (_discardTileNumber == null || tile == null) return;
            
            _discardTileNumber.text = tile.IsFalseOkey ? "J" : tile.Number.ToString();
            
            // Renk sınıflarını güncelle
            _discardTileNumber.RemoveFromClassList("yellow");
            _discardTileNumber.RemoveFromClassList("blue");
            _discardTileNumber.RemoveFromClassList("black");
            _discardTileNumber.RemoveFromClassList("red");

            switch (tile.Color)
            {
                case TileColor.Yellow: _discardTileNumber.AddToClassList("yellow"); break;
                case TileColor.Blue: _discardTileNumber.AddToClassList("blue"); break;
                case TileColor.Black: _discardTileNumber.AddToClassList("black"); break;
                case TileColor.Red: _discardTileNumber.AddToClassList("red"); break;
            }
            
            Debug.Log($"[GameTableScreen] Discard pile updated to show: {tile.Color} {tile.Number}");
        }

        /// <summary>
        /// PlayerId'ye göre rakip indeksini bulur
        /// </summary>
        private int GetOpponentIndexByPlayerId(string playerId)
        {
            // Demo modda bot-X formatından index çıkar
            if (playerId.StartsWith("bot-"))
            {
                if (int.TryParse(playerId.Replace("bot-", ""), out int botNum))
                {
                    return botNum - 1; // bot-1 -> 0, bot-2 -> 1, bot-3 -> 2
                }
            }
            
            // Online modda seat index'e göre hesapla
            // Basit hesaplama: playerId hash'inden index
            if (!string.IsNullOrEmpty(playerId))
            {
                // Seat index bazlı hesaplama yapabiliriz
                // Şimdilik hash'den modulo 3 alalım
                return Mathf.Abs(playerId.GetHashCode()) % 3;
            }
            
            return -1;
        }

        #endregion
    }
}
