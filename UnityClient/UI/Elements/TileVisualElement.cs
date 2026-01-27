using System;
using OkeyGame.Unity.UI.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace OkeyGame.Unity.UI.Elements
{
    /// <summary>
    /// Tek bir Okey taşını temsil eden VisualElement.
    /// UI Toolkit ile tasarlanmıştır.
    /// </summary>
    public class TileVisualElement : VisualElement
    {
        #region USS Sınıf İsimleri

        public const string UssClassName = "tile";
        public const string UssSelectedClassName = "tile--selected";
        public const string UssDraggingClassName = "tile--dragging";
        public const string UssOkeyClassName = "tile--okey";
        public const string UssFalseJokerClassName = "tile--false-joker";
        public const string UssYellowClassName = "tile--yellow";
        public const string UssBlueClassName = "tile--blue";
        public const string UssBlackClassName = "tile--black";
        public const string UssRedClassName = "tile--red";

        #endregion

        #region Boyutlar

        /// <summary>Taş genişliği (piksel).</summary>
        public const float TileWidth = 48f;

        /// <summary>Taş yüksekliği (piksel).</summary>
        public const float TileHeight = 64f;

        /// <summary>Taşlar arası boşluk.</summary>
        public const float TileSpacing = 4f;

        #endregion

        #region Özellikler

        /// <summary>Bu elementin taş modeli.</summary>
        public TileModel Model { get; private set; }

        /// <summary>Taş seçili mi?</summary>
        public bool IsSelected
        {
            get => Model?.IsSelected ?? false;
            set
            {
                if (Model != null)
                {
                    Model.IsSelected = value;
                    UpdateSelectionState();
                }
            }
        }

        /// <summary>Sürükleniyor mu?</summary>
        public bool IsDragging { get; private set; }

        #endregion

        #region Alt Elemanlar

        private readonly VisualElement _background;
        private readonly Label _valueLabel;
        private readonly VisualElement _okeyIndicator;

        #endregion

        #region Events

        /// <summary>Taş tıklandığında.</summary>
        public event Action<TileVisualElement> OnClicked;

        /// <summary>Sürükleme başladığında.</summary>
        public event Action<TileVisualElement> OnDragStarted;

        /// <summary>Sürükleme bittiğinde.</summary>
        public event Action<TileVisualElement, Vector2> OnDragEnded;

        #endregion

        #region Constructor

        public TileVisualElement()
        {
            // Temel stil
            AddToClassList(UssClassName);
            style.width = TileWidth;
            style.height = TileHeight;
            style.position = Position.Relative;

            // Arka plan
            _background = new VisualElement
            {
                name = "tile-background",
                style =
                {
                    position = Position.Absolute,
                    top = 0,
                    left = 0,
                    right = 0,
                    bottom = 0,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderTopWidth = 2,
                    borderBottomWidth = 2,
                    borderLeftWidth = 2,
                    borderRightWidth = 2,
                    borderTopColor = new Color(0.3f, 0.3f, 0.3f),
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f),
                    borderLeftColor = new Color(0.3f, 0.3f, 0.3f),
                    borderRightColor = new Color(0.3f, 0.3f, 0.3f),
                    backgroundColor = new Color(0.95f, 0.93f, 0.88f) // Fildişi
                }
            };
            Add(_background);

            // Değer etiketi
            _valueLabel = new Label
            {
                name = "tile-value",
                style =
                {
                    position = Position.Absolute,
                    top = 0,
                    left = 0,
                    right = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            Add(_valueLabel);

            // Okey göstergesi (köşede küçük elmas)
            _okeyIndicator = new VisualElement
            {
                name = "okey-indicator",
                style =
                {
                    position = Position.Absolute,
                    top = 2,
                    right = 2,
                    width = 10,
                    height = 10,
                    backgroundColor = new Color(1f, 0.84f, 0f), // Altın
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5,
                    display = DisplayStyle.None
                }
            };
            Add(_okeyIndicator);

            // Tıklama olayı
            RegisterCallback<ClickEvent>(OnClick);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Taş modelini bağlar ve görünümü günceller.
        /// </summary>
        public void BindModel(TileModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            UpdateVisuals();
        }

        /// <summary>
        /// Sürükleme durumunu ayarlar.
        /// </summary>
        public void SetDragging(bool dragging)
        {
            IsDragging = dragging;

            if (dragging)
            {
                AddToClassList(UssDraggingClassName);
                style.opacity = 0.8f;
                BringToFront();
            }
            else
            {
                RemoveFromClassList(UssDraggingClassName);
                style.opacity = 1f;
            }
        }

        /// <summary>
        /// Taşı belirtilen pozisyona taşır (animasyonsuz).
        /// </summary>
        public void SetPosition(float x, float y)
        {
            style.left = x;
            style.top = y;
        }

        #endregion

        #region Private Methods

        private void UpdateVisuals()
        {
            if (Model == null) return;

            // Değer metnini ayarla
            _valueLabel.text = Model.GetDisplayText();

            // Renk sınıflarını temizle
            RemoveFromClassList(UssYellowClassName);
            RemoveFromClassList(UssBlueClassName);
            RemoveFromClassList(UssBlackClassName);
            RemoveFromClassList(UssRedClassName);
            RemoveFromClassList(UssOkeyClassName);
            RemoveFromClassList(UssFalseJokerClassName);

            // Özel taşlar
            if (Model.IsFalseJoker)
            {
                AddToClassList(UssFalseJokerClassName);
                _valueLabel.style.color = new Color(0.5f, 0f, 0.5f); // Mor
                _valueLabel.style.fontSize = 28;
                _okeyIndicator.style.display = DisplayStyle.None;
                return;
            }

            if (Model.IsOkey)
            {
                AddToClassList(UssOkeyClassName);
                _okeyIndicator.style.display = DisplayStyle.Flex;
            }
            else
            {
                _okeyIndicator.style.display = DisplayStyle.None;
            }

            // Renk sınıfını ekle
            switch (Model.Color)
            {
                case TileColor.Yellow:
                    AddToClassList(UssYellowClassName);
                    _valueLabel.style.color = new Color(0.8f, 0.6f, 0f);
                    break;
                case TileColor.Blue:
                    AddToClassList(UssBlueClassName);
                    _valueLabel.style.color = new Color(0.1f, 0.4f, 0.8f);
                    break;
                case TileColor.Black:
                    AddToClassList(UssBlackClassName);
                    _valueLabel.style.color = new Color(0.15f, 0.15f, 0.15f);
                    break;
                case TileColor.Red:
                    AddToClassList(UssRedClassName);
                    _valueLabel.style.color = new Color(0.8f, 0.1f, 0.2f);
                    break;
            }

            UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            if (IsSelected)
            {
                AddToClassList(UssSelectedClassName);
                _background.style.borderTopColor = new Color(1f, 0.84f, 0f);
                _background.style.borderBottomColor = new Color(1f, 0.84f, 0f);
                _background.style.borderLeftColor = new Color(1f, 0.84f, 0f);
                _background.style.borderRightColor = new Color(1f, 0.84f, 0f);
                _background.style.borderTopWidth = 3;
                _background.style.borderBottomWidth = 3;
                _background.style.borderLeftWidth = 3;
                _background.style.borderRightWidth = 3;
            }
            else
            {
                RemoveFromClassList(UssSelectedClassName);
                _background.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
                _background.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
                _background.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
                _background.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
                _background.style.borderTopWidth = 2;
                _background.style.borderBottomWidth = 2;
                _background.style.borderLeftWidth = 2;
                _background.style.borderRightWidth = 2;
            }
        }

        private void OnClick(ClickEvent evt)
        {
            OnClicked?.Invoke(this);
            evt.StopPropagation();
        }

        #endregion

        #region Factory

        /// <summary>
        /// Yeni bir TileVisualElement oluşturur ve modeli bağlar.
        /// </summary>
        public static TileVisualElement Create(TileModel model)
        {
            var element = new TileVisualElement();
            element.BindModel(model);
            return element;
        }

        #endregion
    }
}
