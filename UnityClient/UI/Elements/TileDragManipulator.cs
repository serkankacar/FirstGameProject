using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OkeyGame.Unity.UI.Elements
{
    /// <summary>
    /// Taş sürükle-bırak işleyicisi.
    /// Unity'nin PointerManipulator sınıfını kullanır.
    /// </summary>
    public class TileDragManipulator : PointerManipulator
    {
        #region Alanlar

        private readonly TileVisualElement _tile;
        private Vector2 _startPosition;
        private Vector2 _pointerStartPosition;
        private bool _isDragging;
        private VisualElement _originalParent;
        private int _originalIndex;

        #endregion

        #region Events

        /// <summary>Sürükleme başladığında.</summary>
        public event Action<TileVisualElement, Vector2> OnDragStart;

        /// <summary>Sürüklenirken (her frame).</summary>
        public event Action<TileVisualElement, Vector2> OnDragMove;

        /// <summary>Sürükleme bittiğinde.</summary>
        public event Action<TileVisualElement, Vector2> OnDragEnd;

        /// <summary>Sürükleme iptal edildiğinde.</summary>
        public event Action<TileVisualElement> OnDragCancel;

        #endregion

        #region Ayarlar

        /// <summary>Sürükleme başlamak için gereken minimum mesafe.</summary>
        public float DragThreshold { get; set; } = 5f;

        /// <summary>Sürükleme sırasında taşın opaklığı.</summary>
        public float DragOpacity { get; set; } = 0.8f;

        #endregion

        #region Constructor

        public TileDragManipulator(TileVisualElement tile)
        {
            _tile = tile ?? throw new ArgumentNullException(nameof(tile));
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        #endregion

        #region PointerManipulator Override

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        #endregion

        #region Pointer Handlers

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt)) return;

            // Başlangıç pozisyonlarını kaydet
            _startPosition = new Vector2(_tile.resolvedStyle.left, _tile.resolvedStyle.top);
            _pointerStartPosition = evt.position;
            _isDragging = false;

            // Orijinal parent ve index'i kaydet
            _originalParent = _tile.parent;
            _originalIndex = _originalParent?.IndexOf(_tile) ?? -1;

            // Pointer'ı yakala
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!target.HasPointerCapture(evt.pointerId)) return;

            Vector2 pointerDelta = evt.position - _pointerStartPosition;

            // Sürükleme eşiğini kontrol et
            if (!_isDragging && pointerDelta.magnitude > DragThreshold)
            {
                StartDrag(evt.position);
            }

            if (_isDragging)
            {
                // Taşı yeni pozisyona taşı
                Vector2 newPosition = _startPosition + pointerDelta;
                _tile.style.left = newPosition.x;
                _tile.style.top = newPosition.y;

                OnDragMove?.Invoke(_tile, evt.position);
            }

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!target.HasPointerCapture(evt.pointerId)) return;

            target.ReleasePointer(evt.pointerId);

            if (_isDragging)
            {
                EndDrag(evt.position);
            }

            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_isDragging)
            {
                CancelDrag();
            }
        }

        #endregion

        #region Sürükleme İşlemleri

        private void StartDrag(Vector2 pointerPosition)
        {
            _isDragging = true;
            _tile.SetDragging(true);

            // Pozisyonu absolute yap
            _tile.style.position = Position.Absolute;

            OnDragStart?.Invoke(_tile, pointerPosition);

            Debug.Log($"[DragManipulator] Sürükleme başladı: Tile {_tile.Model?.Id}");
        }

        private void EndDrag(Vector2 pointerPosition)
        {
            _isDragging = false;
            _tile.SetDragging(false);

            OnDragEnd?.Invoke(_tile, pointerPosition);

            Debug.Log($"[DragManipulator] Sürükleme bitti: Tile {_tile.Model?.Id}");
        }

        private void CancelDrag()
        {
            _isDragging = false;
            _tile.SetDragging(false);

            // Orijinal pozisyona geri dön
            _tile.style.left = _startPosition.x;
            _tile.style.top = _startPosition.y;

            OnDragCancel?.Invoke(_tile);

            Debug.Log($"[DragManipulator] Sürükleme iptal edildi: Tile {_tile.Model?.Id}");
        }

        #endregion

        #region Yardımcı Metodlar

        /// <summary>
        /// Taşı orijinal pozisyonuna döndürür.
        /// </summary>
        public void RevertToOriginalPosition()
        {
            _tile.style.left = _startPosition.x;
            _tile.style.top = _startPosition.y;
        }

        #endregion
    }
}
