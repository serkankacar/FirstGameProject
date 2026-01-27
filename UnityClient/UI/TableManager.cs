using System;
using System.Collections.Generic;
using OkeyGame.Unity.Networking;
using OkeyGame.Unity.UI.Elements;
using OkeyGame.Unity.UI.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace OkeyGame.Unity.UI
{
    /// <summary>
    /// Okey masasını yöneten ana script.
    /// Backend'den gelen verileri görselleştirir.
    /// 
    /// KULLANIM:
    /// 1. UIDocument component'ine bu script'i ekleyin
    /// 2. UXML'de gerekli container'ları tanımlayın
    /// 3. Oyun başladığında InitializeTable() çağrılır
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TableManager : MonoBehaviour
    {
        #region Singleton

        private static TableManager _instance;
        public static TableManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<TableManager>();
                }
                return _instance;
            }
        }

        #endregion

        #region Inspector Ayarları

        [Header("UI Toolkit")]
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private VisualTreeAsset _tileTemplate;
        [SerializeField] private StyleSheet _gameStyleSheet;

        [Header("Oyun Ayarları")]
        [SerializeField] private SortMode _defaultSortMode = SortMode.ByColor;
        [SerializeField] private bool _autoSortOnReceive = false;

        #endregion

        #region UI Elemanları

        private VisualElement _root;
        private RackVisualElement _playerRack;
        private VisualElement _discardPileContainer;
        private VisualElement _indicatorContainer;
        private VisualElement _deckContainer;
        private Label _deckCountLabel;
        private Label _turnIndicatorLabel;
        private VisualElement[] _opponentContainers = new VisualElement[3];

        #endregion

        #region Durum

        /// <summary>Elimizdeki taş modelleri.</summary>
        private readonly List<TileModel> _handTiles = new List<TileModel>();

        /// <summary>Mevcut sıralama modu.</summary>
        public SortMode CurrentSortMode { get; private set; }

        /// <summary>Seçili taş (varsa).</summary>
        public TileVisualElement SelectedTile { get; private set; }

        /// <summary>Gösterge taşı.</summary>
        public TileModel IndicatorTile { get; private set; }

        /// <summary>Bizim sıramız mı?</summary>
        public bool IsMyTurn { get; private set; }

        #endregion

        #region Events

        /// <summary>Taş atma isteği.</summary>
        public event Action<int> OnDiscardTileRequested;

        /// <summary>Desteden çekme isteği.</summary>
        public event Action OnDrawFromDeckRequested;

        /// <summary>Atıktan çekme isteği.</summary>
        public event Action OnDrawFromDiscardRequested;

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

            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            CurrentSortMode = _defaultSortMode;
        }

        private void Start()
        {
            InitializeUI();
            SubscribeToNetworkEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        #endregion

        #region UI Başlatma

        private void InitializeUI()
        {
            _root = _uiDocument.rootVisualElement;

            if (_gameStyleSheet != null)
            {
                _root.styleSheets.Add(_gameStyleSheet);
            }

            // Ana container'ları bul veya oluştur
            FindOrCreateContainers();

            // Player rack oluştur
            _playerRack = new RackVisualElement();
            _playerRack.OnTileSelected += HandleTileSelected;
            _playerRack.OnTileDropped += HandleTileDropped;

            var rackContainer = _root.Q<VisualElement>("player-rack-container");
            if (rackContainer != null)
            {
                rackContainer.Add(_playerRack);
            }
            else
            {
                // Container yoksa kendimiz oluştur
                var bottomContainer = new VisualElement
                {
                    name = "player-rack-container",
                    style =
                    {
                        position = Position.Absolute,
                        bottom = 20,
                        left = 0,
                        right = 0,
                        alignItems = Align.Center
                    }
                };
                bottomContainer.Add(_playerRack);
                _root.Add(bottomContainer);
            }

            Debug.Log("[TableManager] UI başlatıldı");
        }

        private void FindOrCreateContainers()
        {
            // Discard pile
            _discardPileContainer = _root.Q<VisualElement>("discard-pile");
            if (_discardPileContainer == null)
            {
                _discardPileContainer = CreateCenterContainer("discard-pile", "Atık");
            }
            _discardPileContainer.RegisterCallback<ClickEvent>(OnDiscardPileClicked);

            // Deck
            _deckContainer = _root.Q<VisualElement>("deck");
            if (_deckContainer == null)
            {
                _deckContainer = CreateCenterContainer("deck", "Deste");
            }
            _deckContainer.RegisterCallback<ClickEvent>(OnDeckClicked);

            // Deck count label
            _deckCountLabel = _deckContainer.Q<Label>("deck-count");
            if (_deckCountLabel == null)
            {
                _deckCountLabel = new Label("0") { name = "deck-count" };
                _deckCountLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _deckContainer.Add(_deckCountLabel);
            }

            // Indicator
            _indicatorContainer = _root.Q<VisualElement>("indicator");
            if (_indicatorContainer == null)
            {
                _indicatorContainer = CreateCenterContainer("indicator", "Gösterge");
            }

            // Turn indicator
            _turnIndicatorLabel = _root.Q<Label>("turn-indicator");
            if (_turnIndicatorLabel == null)
            {
                _turnIndicatorLabel = new Label
                {
                    name = "turn-indicator",
                    text = "",
                    style =
                    {
                        position = Position.Absolute,
                        top = 20,
                        left = 0,
                        right = 0,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 24,
                        color = Color.white
                    }
                };
                _root.Add(_turnIndicatorLabel);
            }

            // Opponent containers
            for (int i = 0; i < 3; i++)
            {
                _opponentContainers[i] = _root.Q<VisualElement>($"opponent-{i}");
            }
        }

        private VisualElement CreateCenterContainer(string name, string label)
        {
            var container = new VisualElement
            {
                name = name,
                style =
                {
                    width = 100,
                    height = 100,
                    backgroundColor = new Color(0.3f, 0.2f, 0.1f),
                    borderTopLeftRadius = 10,
                    borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            var labelElement = new Label(label)
            {
                style =
                {
                    color = new Color(0.9f, 0.9f, 0.9f),
                    fontSize = 12
                }
            };
            container.Add(labelElement);

            return container;
        }

        #endregion

        #region Network Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            var stateManager = GameStateManager.Instance;
            if (stateManager == null) return;

            stateManager.OnStateChanged += HandleStateChanged;
            stateManager.OnHandUpdated += HandleHandUpdated;
            stateManager.OnTileAdded += HandleTileAdded;
            stateManager.OnTileRemoved += HandleTileRemoved;
            stateManager.OnTurnChanged += HandleTurnChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            var stateManager = GameStateManager.Instance;
            if (stateManager == null) return;

            stateManager.OnStateChanged -= HandleStateChanged;
            stateManager.OnHandUpdated -= HandleHandUpdated;
            stateManager.OnTileAdded -= HandleTileAdded;
            stateManager.OnTileRemoved -= HandleTileRemoved;
            stateManager.OnTurnChanged -= HandleTurnChanged;
        }

        #endregion

        #region Network Event Handlers

        private void HandleStateChanged()
        {
            var stateManager = GameStateManager.Instance;
            if (stateManager?.CurrentState == null) return;

            // Deste sayısını güncelle
            UpdateDeckCount(stateManager.RemainingTileCount);

            // Gösterge taşını güncelle
            if (stateManager.IndicatorTile != null)
            {
                UpdateIndicator(TileModel.FromNetworkData(stateManager.IndicatorTile));
            }

            // Atık yığınını güncelle
            if (stateManager.DiscardTopTile != null)
            {
                UpdateDiscardPile(TileModel.FromNetworkData(stateManager.DiscardTopTile));
            }

            // Sıra durumunu güncelle
            IsMyTurn = stateManager.IsMyTurn;
            UpdateTurnIndicator();
        }

        private void HandleHandUpdated()
        {
            var stateManager = GameStateManager.Instance;
            if (stateManager == null) return;

            var networkTiles = stateManager.MyHand;
            _handTiles.Clear();

            foreach (var networkTile in networkTiles)
            {
                _handTiles.Add(TileModel.FromNetworkData(networkTile));
            }

            // Otomatik sıralama
            if (_autoSortOnReceive && CurrentSortMode != SortMode.None)
            {
                SortHand(CurrentSortMode);
            }

            RefreshRack();
        }

        private void HandleTileAdded(TileData networkTile)
        {
            var tile = TileModel.FromNetworkData(networkTile);
            _handTiles.Add(tile);
            _playerRack.AddTile(tile);

            Debug.Log($"[TableManager] Taş eklendi: {tile}");
        }

        private void HandleTileRemoved(int tileId)
        {
            _handTiles.RemoveAll(t => t.Id == tileId);
            _playerRack.RemoveTile(tileId);

            if (SelectedTile?.Model?.Id == tileId)
            {
                SelectedTile = null;
            }

            Debug.Log($"[TableManager] Taş kaldırıldı: {tileId}");
        }

        private void HandleTurnChanged(int position)
        {
            IsMyTurn = GameStateManager.Instance?.IsMyTurn ?? false;
            UpdateTurnIndicator();
        }

        #endregion

        #region UI Update Methods

        /// <summary>
        /// Istakayı yeniler.
        /// </summary>
        public void RefreshRack()
        {
            _playerRack?.SetTiles(_handTiles);
        }

        /// <summary>
        /// Deste sayısını günceller.
        /// </summary>
        public void UpdateDeckCount(int count)
        {
            if (_deckCountLabel != null)
            {
                _deckCountLabel.text = count.ToString();
            }
        }

        /// <summary>
        /// Gösterge taşını günceller.
        /// </summary>
        public void UpdateIndicator(TileModel indicator)
        {
            IndicatorTile = indicator;

            if (_indicatorContainer == null) return;

            // Mevcut taşı temizle
            var existingTile = _indicatorContainer.Q<TileVisualElement>();
            existingTile?.RemoveFromHierarchy();

            if (indicator != null)
            {
                var tileElement = TileVisualElement.Create(indicator);
                _indicatorContainer.Add(tileElement);
            }
        }

        /// <summary>
        /// Atık yığınını günceller.
        /// </summary>
        public void UpdateDiscardPile(TileModel topTile)
        {
            if (_discardPileContainer == null) return;

            // Mevcut taşı temizle
            var existingTile = _discardPileContainer.Q<TileVisualElement>();
            existingTile?.RemoveFromHierarchy();

            if (topTile != null)
            {
                var tileElement = TileVisualElement.Create(topTile);
                _discardPileContainer.Add(tileElement);
            }
        }

        /// <summary>
        /// Sıra göstergesini günceller.
        /// </summary>
        private void UpdateTurnIndicator()
        {
            if (_turnIndicatorLabel == null) return;

            if (IsMyTurn)
            {
                _turnIndicatorLabel.text = "🎯 SİZİN SIRANIZ";
                _turnIndicatorLabel.style.color = new Color(0f, 1f, 0.5f);
            }
            else
            {
                _turnIndicatorLabel.text = "⏳ Rakip oynuyor...";
                _turnIndicatorLabel.style.color = new Color(1f, 1f, 0.5f);
            }
        }

        #endregion

        #region Sıralama

        /// <summary>
        /// Eli sıralar.
        /// </summary>
        public void SortHand(SortMode mode)
        {
            CurrentSortMode = mode;

            switch (mode)
            {
                case SortMode.ByColor:
                    _handTiles.Sort((a, b) => a.CompareByColor(b));
                    break;
                case SortMode.ByValue:
                    _handTiles.Sort((a, b) => a.CompareByValue(b));
                    break;
            }

            RefreshRack();
            Debug.Log($"[TableManager] El sıralandı: {mode}");
        }

        /// <summary>
        /// Renge göre sırala butonu için.
        /// </summary>
        public void SortByColor()
        {
            SortHand(SortMode.ByColor);
        }

        /// <summary>
        /// Değere göre sırala butonu için.
        /// </summary>
        public void SortByValue()
        {
            SortHand(SortMode.ByValue);
        }

        #endregion

        #region Kullanıcı Etkileşimleri

        private void HandleTileSelected(TileVisualElement tile)
        {
            // Önceki seçimi kaldır
            if (SelectedTile != null && SelectedTile != tile)
            {
                SelectedTile.IsSelected = false;
            }

            // Yeni seçim
            if (SelectedTile == tile)
            {
                // Çift tıklama - taş at
                if (IsMyTurn)
                {
                    DiscardSelectedTile();
                }
            }
            else
            {
                SelectedTile = tile;
                tile.IsSelected = true;
            }
        }

        private void HandleTileDropped(TileVisualElement tile, int slotIndex)
        {
            // Istaka içinde yer değiştirme
            Debug.Log($"[TableManager] Taş bırakıldı: {tile.Model} -> Slot {slotIndex}");
        }

        private void OnDeckClicked(ClickEvent evt)
        {
            if (!IsMyTurn)
            {
                Debug.Log("[TableManager] Sıranız değil!");
                return;
            }

            OnDrawFromDeckRequested?.Invoke();
            GameNetworkManager.Instance?.DrawTileAsync();
        }

        private void OnDiscardPileClicked(ClickEvent evt)
        {
            if (!IsMyTurn)
            {
                Debug.Log("[TableManager] Sıranız değil!");
                return;
            }

            OnDrawFromDiscardRequested?.Invoke();
            GameNetworkManager.Instance?.DrawFromDiscardAsync();
        }

        /// <summary>
        /// Seçili taşı atar.
        /// </summary>
        public void DiscardSelectedTile()
        {
            if (SelectedTile?.Model == null)
            {
                Debug.Log("[TableManager] Atılacak taş seçilmedi!");
                return;
            }

            if (!IsMyTurn)
            {
                Debug.Log("[TableManager] Sıranız değil!");
                return;
            }

            int tileId = SelectedTile.Model.Id;
            OnDiscardTileRequested?.Invoke(tileId);
            GameNetworkManager.Instance?.ThrowTileAsync(tileId);

            SelectedTile = null;
        }

        #endregion

        #region Test / Demo

        /// <summary>
        /// Test için örnek taşlarla doldurur.
        /// </summary>
        [ContextMenu("Demo Tiles")]
        public void LoadDemoTiles()
        {
            _handTiles.Clear();

            // 15 test taşı oluştur
            int id = 1;
            for (int i = 0; i < 15; i++)
            {
                var color = (TileColor)(i % 4);
                var value = (i % 13) + 1;
                var tile = TileModel.FromData(id++, (int)color, value, i == 7, i == 14);
                _handTiles.Add(tile);
            }

            if (_autoSortOnReceive)
            {
                SortHand(CurrentSortMode);
            }
            else
            {
                RefreshRack();
            }

            // Gösterge taşı
            UpdateIndicator(TileModel.FromData(100, 0, 5, false, false));

            // Deste sayısı
            UpdateDeckCount(49);

            Debug.Log("[TableManager] Demo taşlar yüklendi");
        }

        #endregion
    }
}
