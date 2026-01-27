using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.Entities;

/// <summary>
/// Okey taşını temsil eden Immutable (değiştirilemez) sınıf.
/// Bir kez oluşturulduktan sonra değerleri değiştirilemez.
/// Bu, thread-safety ve veri bütünlüğü sağlar.
/// </summary>
public sealed class Tile : IEquatable<Tile>
{
    #region Özellikler

    /// <summary>
    /// Taşın benzersiz kimlik numarası.
    /// Her taş için unique bir değer (0-105 arası).
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Taşın rengi (Sarı, Mavi, Siyah, Kırmızı).
    /// </summary>
    public TileColor Color { get; }

    /// <summary>
    /// Taşın üzerindeki sayı değeri (1-13 arası).
    /// Sahte Okey (False Joker) için 0 değeri kullanılır.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Taşın Okey (Joker) olup olmadığını belirtir.
    /// Gösterge taşına göre dinamik olarak belirlenir.
    /// </summary>
    public bool IsOkey { get; }

    /// <summary>
    /// Taşın Sahte Okey (False Joker) olup olmadığını belirtir.
    /// Sette 2 adet sahte okey bulunur.
    /// </summary>
    public bool IsFalseJoker { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// Yeni bir taş oluşturur.
    /// </summary>
    /// <param name="id">Taşın benzersiz ID'si</param>
    /// <param name="color">Taşın rengi</param>
    /// <param name="value">Taşın değeri (1-13)</param>
    /// <param name="isFalseJoker">Sahte Okey mi?</param>
    /// <param name="isOkey">Okey (Joker) mi?</param>
    private Tile(int id, TileColor color, int value, bool isFalseJoker = false, bool isOkey = false)
    {
        Id = id;
        Color = color;
        Value = value;
        IsFalseJoker = isFalseJoker;
        IsOkey = isOkey;
    }

    #endregion

    #region Factory Metotları

    /// <summary>
    /// Normal bir taş oluşturur.
    /// </summary>
    public static Tile Create(int id, TileColor color, int value)
    {
        ValidateValue(value);
        return new Tile(id, color, value);
    }

    /// <summary>
    /// Sahte Okey (False Joker) taşı oluşturur.
    /// </summary>
    public static Tile CreateFalseJoker(int id)
    {
        // Sahte Okey'ler genellikle özel bir renk veya değerle gösterilir
        // Burada Black renk ve 0 değeri kullanıyoruz
        return new Tile(id, TileColor.Black, 0, isFalseJoker: true);
    }

    /// <summary>
    /// Mevcut taşın Okey (Joker) olarak işaretlenmiş kopyasını döndürür.
    /// Immutability prensibine uygun olarak yeni instance oluşturur.
    /// </summary>
    public Tile AsOkey()
    {
        return new Tile(Id, Color, Value, IsFalseJoker, isOkey: true);
    }

    /// <summary>
    /// Mevcut taşın Okey işareti kaldırılmış kopyasını döndürür.
    /// </summary>
    public Tile AsNormal()
    {
        return new Tile(Id, Color, Value, IsFalseJoker, isOkey: false);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Taş değerinin geçerli aralıkta olup olmadığını kontrol eder.
    /// </summary>
    private static void ValidateValue(int value)
    {
        if (value < 1 || value > 13)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Taş değeri 1 ile 13 arasında olmalıdır.");
        }
    }

    #endregion

    #region Equality & Hashing

    public bool Equals(Tile? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Tile);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Tile? left, Tile? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Tile? left, Tile? right)
    {
        return !(left == right);
    }

    #endregion

    #region Display

    public override string ToString()
    {
        if (IsFalseJoker) return "[Sahte Okey]";
        if (IsOkey) return $"[OKEY: {Color}-{Value}]";
        return $"[{Color}-{Value}]";
    }

    #endregion
}
