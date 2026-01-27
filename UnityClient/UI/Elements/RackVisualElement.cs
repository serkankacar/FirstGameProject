using System;
using System.Collections.Generic;
using OkeyGame.Unity.UI.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace OkeyGame.Unity.UI.Elements
{
    /// <summary>
    /// Oyuncunun ıstakasını (taş dizme alanı) temsil eden VisualElement.
    /// İki sıralı grid yapısına sahiptir.
    /// </summary>
    public class RackVisualElement : VisualElement
    {
        #region USS Sınıf İsimleri

        public const string UssClassName = "rack";
        public const string UssRowClassName = "rack__row";
        public const string UssSlotClassName = "rack__slot";
        public const string UssSlotHighlightClassName = "rack__slot--highlight";
        public const string UssSlotOccupiedClassName = "rack__slot--occupied";

        #endregion

        #region Sabitler

        /// <summary>Sütun sayısı.</summary>
        public const int ColumnCount = 15;

        /// <summary>Satır sayısı.</summary>
        public const int RowCount = 2;

        /// <summary>Toplam slot sayısı.</summary>
        public const int TotalSlots = ColumnCount * RowCount;

        #endregion

        #region Özellikler

        /// <summary>Istakadaki taşlar (pozisyona göre).</summary>
        private readonly TileVisualElement[] _slots = new TileVisualElement[TotalSlots];

        /// <summary>Slot elemanları.</summary>
        private readonly VisualElement[] _slotElements = new VisualElement[TotalSlots];

        /// <summary>Satır container'ları.</summary>
        private readonly VisualElement[] _rows = new VisualElement[RowCount];

        /// <summary>Vurgulanan slot indeksi (-1 = yok).</summary>
        private int _highlightedSlotIndex = -1;

        #endregion

        #region Events

        /// <summary>Taş bir slot'a bırakıldığında.</summary>
        public event Action<TileVisualElement, int> OnTileDropped;

        /// <summary>Taş seçildiğinde.</summary>
        public event Action<TileVisualElement> OnTileSelected;

        /// <summary>Taş çift tıklandığında (atma için).</summary>
        public event Action<TileVisualElement> OnTileDoubleClicked;

        #endregion

        #region Constructor

        public RackVisualElement()
        {
            AddToClassList(UssClassName);

            // Temel stil
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Center;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.backgroundColor = new Color(0.4f, 0.25f, 0.15f); // Ahşap rengi
            style.borderTopLeftRadius = 10;
            style.borderTopRightRadius = 10;
            style.borderBottomLeftRadius = 10;
            style.borderBottomRightRadius = 10;

            // Satırları oluştur
            for (int row = 0; row < RowCount; row++)
            {
                var rowElement = CreateRow(row);
                _rows[row] = rowElement;
                Add(rowElement);
            }

            // Drop zone olarak kaydet
            RegisterCallback<PointerMoveEvent>(OnPointerMoveOverRack);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeaveRack);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Taşları ıstakaya yerleştirir.
        /// </summary>
        public void SetTiles(List<TileModel> tiles)
        {
            ClearAllTiles();

            if (tiles == null) return;

            for (int i = 0; i < tiles.Count && i < TotalSlots; i++)
            {
                var tile = tiles[i];
                tile.RackIndex = i;
                tile.RackRow = i / ColumnCount;

                var tileElement = CreateTileElement(tile);
                PlaceTileInSlot(tileElement, i);
            }
        }

        /// <summary>
        /// Tek bir taş ekler.
        /// </summary>
        public void AddTile(TileModel tile)
        {
            // İlk boş slot'u bul
            int emptySlot = FindFirstEmptySlot();
            if (emptySlot < 0)
            {
                Debug.LogWarning("[Rack] Istaka dolu, taş eklenemiyor!");
                return;
            }

            tile.RackIndex = emptySlot;
            tile.RackRow = emptySlot / ColumnCount;

            var tileElement = CreateTileElement(tile);
            PlaceTileInSlot(tileElement, emptySlot);
        }

        /// <summary>
        /// Belirtilen ID'li taşı kaldırır.
        /// </summary>
        public void RemoveTile(int tileId)
        {
            for (int i = 0; i < TotalSlots; i++)
            {
                var tile = _slots[i];
                if (tile?.Model?.Id == tileId)
                {
                    RemoveTileFromSlot(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Tüm taşları temizler.
        /// </summary>
        public void ClearAllTiles()
        {
            for (int i = 0; i < TotalSlots; i++)
            {
                RemoveTileFromSlot(i);
            }
        }

        /// <summary>
        /// Taşları sıralar.
        /// </summary>
        public void SortTiles(SortMode mode)
        {
            if (mode == SortMode.None) return;

            // Mevcut taşları topla
            var tiles = new List<TileModel>();
            for (int i = 0; i < TotalSlots; i++)
            {
                if (_slots[i]?.Model != null)
                {
                    tiles.Add(_slots[i].Model);
                }
            }

            // Sırala
            switch (mode)
            {
                case SortMode.ByColor:
                    tiles.Sort((a, b) => a.CompareByColor(b));
                    break;
                case SortMode.ByValue:
                    tiles.Sort((a, b) => a.CompareByValue(b));
                    break;
            }

            // Yeniden yerleştir
            SetTiles(tiles);

            Debug.Log($"[Rack] Taşlar sıralandı: {mode}");
        }

        /// <summary>
        /// Bir taşı hedef slot'a taşır.
        /// </summary>
        public void MoveTile(TileVisualElement tile, int targetSlotIndex)
        {
            if (tile?.Model == null) return;
            if (targetSlotIndex < 0 || targetSlotIndex >= TotalSlots) return;

            int sourceIndex = tile.Model.RackIndex;
            if (sourceIndex == targetSlotIndex) return;

            // Hedef dolu mu?
            var targetTile = _slots[targetSlotIndex];
            if (targetTile != null)
            {
                // Yer değiştir (swap)
                SwapTiles(sourceIndex, targetSlotIndex);
            }
            else
            {
                // Boş slot'a taşı
                RemoveTileFromSlot(sourceIndex);
                PlaceTileInSlot(tile, targetSlotIndex);
            }

            ClearHighlight();
        }

        /// <summary>
        /// Pointer pozisyonundan slot indeksini bulur.
        /// </summary>
        public int GetSlotIndexAtPosition(Vector2 localPosition)
        {
            for (int i = 0; i < TotalSlots; i++)
            {
                var slot = _slotElements[i];
                if (slot == null) continue;

                var slotBounds = slot.worldBound;
                if (slotBounds.Contains(localPosition))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Belirtilen slot'u vurgular.
        /// </summary>
        public void HighlightSlot(int slotIndex)
        {
            ClearHighlight();

            if (slotIndex >= 0 && slotIndex < TotalSlots)
            {
                _highlightedSlotIndex = slotIndex;
                _slotElements[slotIndex]?.AddToClassList(UssSlotHighlightClassName);
            }
        }

        /// <summary>
        /// Vurgulamayı temizler.
        /// </summary>
        public void ClearHighlight()
        {
            if (_highlightedSlotIndex >= 0 && _highlightedSlotIndex < TotalSlots)
            {
                _slotElements[_highlightedSlotIndex]?.RemoveFromClassList(UssSlotHighlightClassName);
            }
            _highlightedSlotIndex = -1;
        }

        #endregion

        #region Private Methods

        private VisualElement CreateRow(int rowIndex)
        {
            var row = new VisualElement
            {
                name = $"rack-row-{rowIndex}"
            };
            row.AddToClassList(UssRowClassName);
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = rowIndex > 0 ? 8 : 0;

            // Slot'ları oluştur
            for (int col = 0; col < ColumnCount; col++)
            {
                int slotIndex = rowIndex * ColumnCount + col;
                var slot = CreateSlot(slotIndex);
                _slotElements[slotIndex] = slot;
                row.Add(slot);
            }

            return row;
        }

        private VisualElement CreateSlot(int index)
        {
            var slot = new VisualElement
            {
                name = $"rack-slot-{index}"
            };
            slot.AddToClassList(UssSlotClassName);

            slot.style.width = TileVisualElement.TileWidth + TileVisualElement.TileSpacing;
            slot.style.height = TileVisualElement.TileHeight + TileVisualElement.TileSpacing;
            slot.style.marginRight = 2;
            slot.style.borderTopLeftRadius = 4;
            slot.style.borderTopRightRadius = 4;
            slot.style.borderBottomLeftRadius = 4;
            slot.style.borderBottomRightRadius = 4;
            slot.style.backgroundColor = new Color(0.35f, 0.22f, 0.12f, 0.5f);
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;

            return slot;
        }

        private TileVisualElement CreateTileElement(TileModel model)
        {
            var tileElement = TileVisualElement.Create(model);

            // Click event
            tileElement.OnClicked += HandleTileClicked;

            // Drag manipulator ekle
            var dragManipulator = new TileDragManipulator(tileElement);
            dragManipulator.OnDragStart += HandleTileDragStart;
            dragManipulator.OnDragMove += HandleTileDragMove;
            dragManipulator.OnDragEnd += HandleTileDragEnd;
            tileElement.AddManipulator(dragManipulator);

            return tileElement;
        }

        private void PlaceTileInSlot(TileVisualElement tile, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= TotalSlots) return;

            var slot = _slotElements[slotIndex];
            if (slot == null) return;

            // Mevcut tile'ı kaldır
            if (_slots[slotIndex] != null)
            {
                RemoveTileFromSlot(slotIndex);
            }

            // Yeni tile'ı yerleştir
            _slots[slotIndex] = tile;
            tile.Model.RackIndex = slotIndex;
            tile.Model.RackRow = slotIndex / ColumnCount;

            // Pozisyonu relative yap
            tile.style.position = Position.Relative;
            tile.style.left = StyleKeyword.Auto;
            tile.style.top = StyleKeyword.Auto;

            slot.Add(tile);
            slot.AddToClassList(UssSlotOccupiedClassName);
        }

        private void RemoveTileFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= TotalSlots) return;

            var tile = _slots[slotIndex];
            if (tile == null) return;

            tile.RemoveFromHierarchy();
            _slots[slotIndex] = null;
            _slotElements[slotIndex]?.RemoveFromClassList(UssSlotOccupiedClassName);
        }

        private void SwapTiles(int index1, int index2)
        {
            var tile1 = _slots[index1];
            var tile2 = _slots[index2];

            if (tile1 == null && tile2 == null) return;

            // Geçici olarak kaldır
            if (tile1 != null) tile1.RemoveFromHierarchy();
            if (tile2 != null) tile2.RemoveFromHierarchy();

            // Swap
            _slots[index1] = tile2;
            _slots[index2] = tile1;

            // Yeniden yerleştir
            if (tile2 != null)
            {
                tile2.Model.RackIndex = index1;
                tile2.Model.RackRow = index1 / ColumnCount;
                tile2.style.position = Position.Relative;
                _slotElements[index1].Add(tile2);
            }

            if (tile1 != null)
            {
                tile1.Model.RackIndex = index2;
                tile1.Model.RackRow = index2 / ColumnCount;
                tile1.style.position = Position.Relative;
                _slotElements[index2].Add(tile1);
            }

            // Slot durumlarını güncelle
            UpdateSlotOccupiedState(index1);
            UpdateSlotOccupiedState(index2);
        }

        private void UpdateSlotOccupiedState(int index)
        {
            if (index < 0 || index >= TotalSlots) return;

            if (_slots[index] != null)
            {
                _slotElements[index]?.AddToClassList(UssSlotOccupiedClassName);
            }
            else
            {
                _slotElements[index]?.RemoveFromClassList(UssSlotOccupiedClassName);
            }
        }

        private int FindFirstEmptySlot()
        {
            for (int i = 0; i < TotalSlots; i++)
            {
                if (_slots[i] == null)
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion

        #region Event Handlers

        private void HandleTileClicked(TileVisualElement tile)
        {
            OnTileSelected?.Invoke(tile);
        }

        private void HandleTileDragStart(TileVisualElement tile, Vector2 position)
        {
            // Taşı slot'tan çıkar ama referansı tut
            int slotIndex = tile.Model.RackIndex;
            if (slotIndex >= 0)
            {
                _slotElements[slotIndex]?.RemoveFromClassList(UssSlotOccupiedClassName);
            }
        }

        private void HandleTileDragMove(TileVisualElement tile, Vector2 position)
        {
            // En yakın slot'u vurgula
            int nearestSlot = GetSlotIndexAtPosition(position);
            HighlightSlot(nearestSlot);
        }

        private void HandleTileDragEnd(TileVisualElement tile, Vector2 position)
        {
            int targetSlot = GetSlotIndexAtPosition(position);

            if (targetSlot >= 0)
            {
                MoveTile(tile, targetSlot);
                OnTileDropped?.Invoke(tile, targetSlot);
            }
            else
            {
                // Orijinal pozisyona geri dön
                int originalSlot = tile.Model.RackIndex;
                if (originalSlot >= 0)
                {
                    PlaceTileInSlot(tile, originalSlot);
                }
            }

            ClearHighlight();
        }

        private void OnPointerMoveOverRack(PointerMoveEvent evt)
        {
            // Sürükleme sırasında slot vurgulama için
        }

        private void OnPointerLeaveRack(PointerLeaveEvent evt)
        {
            ClearHighlight();
        }

        #endregion
    }
}
