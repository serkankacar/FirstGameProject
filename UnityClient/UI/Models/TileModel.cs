using System;

namespace OkeyGame.Unity.UI.Models
{
    /// <summary>
    /// Taş veri modeli.
    /// Backend'den gelen TileData'nın Unity tarafındaki karşılığı.
    /// </summary>
    [Serializable]
    public class TileModel : IEquatable<TileModel>, IComparable<TileModel>
    {
        #region Sabitler

        /// <summary>Renk sayısı (Sarı, Mavi, Siyah, Kırmızı).</summary>
        public const int ColorCount = 4;

        /// <summary>Maksimum taş değeri.</summary>
        public const int MaxValue = 13;

        /// <summary>Minimum taş değeri.</summary>
        public const int MinValue = 1;

        #endregion

        #region Özellikler

        /// <summary>Taş benzersiz kimliği.</summary>
        public int Id { get; private set; }

        /// <summary>Taş rengi (0-3).</summary>
        public TileColor Color { get; private set; }

        /// <summary>Taş değeri (1-13).</summary>
        public int Value { get; private set; }

        /// <summary>Bu taş Okey (Joker) mi?</summary>
        public bool IsOkey { get; private set; }

        /// <summary>Bu taş Sahte Okey mi?</summary>
        public bool IsFalseJoker { get; private set; }

        /// <summary>Istakadaki pozisyon indeksi.</summary>
        public int RackIndex { get; set; } = -1;

        /// <summary>Istakadaki satır (0 = üst, 1 = alt).</summary>
        public int RackRow { get; set; } = 0;

        /// <summary>Seçili mi?</summary>
        public bool IsSelected { get; set; }

        #endregion

        #region Constructor

        private TileModel() { }

        /// <summary>
        /// JSON verisinden TileModel oluşturur.
        /// </summary>
        public static TileModel FromData(int id, int color, int value, bool isOkey, bool isFalseJoker)
        {
            return new TileModel
            {
                Id = id,
                Color = (TileColor)color,
                Value = value,
                IsOkey = isOkey,
                IsFalseJoker = isFalseJoker
            };
        }

        /// <summary>
        /// NetworkManager'dan gelen TileData'dan oluşturur.
        /// </summary>
        public static TileModel FromNetworkData(Networking.TileData data)
        {
            return new TileModel
            {
                Id = data.Id,
                Color = (TileColor)data.Color,
                Value = data.Value,
                IsOkey = data.IsOkey,
                IsFalseJoker = data.IsFalseJoker
            };
        }

        #endregion

        #region Sıralama

        /// <summary>
        /// Renge göre sıralar (önce renk, sonra değer).
        /// </summary>
        public int CompareByColor(TileModel other)
        {
            if (other == null) return 1;

            // False joker'ler en sona
            if (IsFalseJoker && !other.IsFalseJoker) return 1;
            if (!IsFalseJoker && other.IsFalseJoker) return -1;

            // Okey'ler en sona (false joker'den önce)
            if (IsOkey && !other.IsOkey) return 1;
            if (!IsOkey && other.IsOkey) return -1;

            // Renk karşılaştırması
            var colorCompare = Color.CompareTo(other.Color);
            if (colorCompare != 0) return colorCompare;

            // Aynı renkse değere göre
            return Value.CompareTo(other.Value);
        }

        /// <summary>
        /// Değere göre sıralar (önce değer, sonra renk).
        /// </summary>
        public int CompareByValue(TileModel other)
        {
            if (other == null) return 1;

            // False joker'ler en sona
            if (IsFalseJoker && !other.IsFalseJoker) return 1;
            if (!IsFalseJoker && other.IsFalseJoker) return -1;

            // Okey'ler en sona (false joker'den önce)
            if (IsOkey && !other.IsOkey) return 1;
            if (!IsOkey && other.IsOkey) return -1;

            // Değer karşılaştırması
            var valueCompare = Value.CompareTo(other.Value);
            if (valueCompare != 0) return valueCompare;

            // Aynı değerse renge göre
            return Color.CompareTo(other.Color);
        }

        /// <summary>
        /// Varsayılan sıralama (renge göre).
        /// </summary>
        public int CompareTo(TileModel other)
        {
            return CompareByColor(other);
        }

        #endregion

        #region Equality

        public bool Equals(TileModel other)
        {
            if (other == null) return false;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TileModel);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        #endregion

        #region Görüntüleme

        /// <summary>
        /// Renk kodunu hex olarak döndürür.
        /// </summary>
        public string GetColorHex()
        {
            return Color switch
            {
                TileColor.Yellow => "#FFD700",
                TileColor.Blue => "#1E90FF",
                TileColor.Black => "#2C2C2C",
                TileColor.Red => "#DC143C",
                _ => "#FFFFFF"
            };
        }

        /// <summary>
        /// Görüntülenecek metni döndürür.
        /// </summary>
        public string GetDisplayText()
        {
            if (IsFalseJoker) return "★";
            if (IsOkey) return "◆";
            return Value.ToString();
        }

        public override string ToString()
        {
            if (IsFalseJoker) return "FalseJoker";
            if (IsOkey) return $"Okey({Color}-{Value})";
            return $"{Color}-{Value}";
        }

        #endregion
    }

    /// <summary>
    /// Taş renkleri.
    /// </summary>
    public enum TileColor
    {
        Yellow = 0,
        Blue = 1,
        Black = 2,
        Red = 3
    }
}
