using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.ValueObjects;

/// <summary>
/// Per (Set) değer nesnesi.
/// Bir per, en az 3 taştan oluşan geçerli bir kombinasyondur.
/// </summary>
public class Meld : IEquatable<Meld>
{
    #region Özellikler

    /// <summary>Peri oluşturan taşlar.</summary>
    public IReadOnlyList<Tile> Tiles { get; }

    /// <summary>Per türü (Run veya Group).</summary>
    public MeldType Type { get; }

    /// <summary>Per geçerli mi?</summary>
    public bool IsValid { get; }

    /// <summary>Kullanılan Okey sayısı.</summary>
    public int OkeyCount { get; }

    /// <summary>Perin puan değeri.</summary>
    public int PointValue { get; }

    #endregion

    #region Constructor

    private Meld(IReadOnlyList<Tile> tiles, MeldType type, bool isValid, int okeyCount)
    {
        Tiles = tiles;
        Type = type;
        IsValid = isValid;
        OkeyCount = okeyCount;
        PointValue = CalculatePointValue();
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Verilen taşlardan per oluşturmaya çalışır.
    /// </summary>
    public static Meld TryCreate(IEnumerable<Tile> tiles)
    {
        var tileList = tiles.ToList();

        if (tileList.Count < 3)
        {
            return new Meld(tileList, MeldType.Invalid, false, 0);
        }

        // Okey sayısını hesapla
        int okeyCount = tileList.Count(t => t.IsOkey || t.IsFalseJoker);

        // Run mı kontrol et
        if (IsValidRun(tileList))
        {
            return new Meld(tileList, MeldType.Run, true, okeyCount);
        }

        // Group mu kontrol et
        if (IsValidGroup(tileList))
        {
            return new Meld(tileList, MeldType.Group, true, okeyCount);
        }

        return new Meld(tileList, MeldType.Invalid, false, okeyCount);
    }

    /// <summary>
    /// Sıralı per (Run) oluşturur.
    /// </summary>
    public static Meld CreateRun(IEnumerable<Tile> tiles)
    {
        var tileList = tiles.ToList();
        int okeyCount = tileList.Count(t => t.IsOkey || t.IsFalseJoker);
        bool isValid = IsValidRun(tileList);
        return new Meld(tileList, isValid ? MeldType.Run : MeldType.Invalid, isValid, okeyCount);
    }

    /// <summary>
    /// Düz per (Group) oluşturur.
    /// </summary>
    public static Meld CreateGroup(IEnumerable<Tile> tiles)
    {
        var tileList = tiles.ToList();
        int okeyCount = tileList.Count(t => t.IsOkey || t.IsFalseJoker);
        bool isValid = IsValidGroup(tileList);
        return new Meld(tileList, isValid ? MeldType.Group : MeldType.Invalid, isValid, okeyCount);
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// Sıralı per kontrolü (aynı renk, ardışık sayılar).
    /// Özel durum: 12-13-1 geçerlidir.
    /// </summary>
    private static bool IsValidRun(List<Tile> tiles)
    {
        if (tiles.Count < 3 || tiles.Count > 13) return false;

        // Normal taşları ve Okey'leri ayır
        var normalTiles = tiles.Where(t => !t.IsOkey && !t.IsFalseJoker).ToList();
        int okeyCount = tiles.Count - normalTiles.Count;

        if (normalTiles.Count == 0)
        {
            // Tamamı Okey - her durumda geçerli
            return okeyCount >= 3;
        }

        // Tüm normal taşlar aynı renkte olmalı
        var color = normalTiles[0].Color;
        if (normalTiles.Any(t => t.Color != color))
        {
            return false;
        }

        // Değerleri sırala
        var values = normalTiles.Select(t => t.Value).OrderBy(v => v).ToList();

        // Aralıkları hesapla ve Okey'lerin yetip yetmediğini kontrol et
        return CanFormSequence(values, okeyCount, tiles.Count);
    }

    /// <summary>
    /// Verilen değerlerden belirtilen uzunlukta ardışık sıra oluşturulabilir mi?
    /// </summary>
    private static bool CanFormSequence(List<int> sortedValues, int okeyCount, int totalLength)
    {
        if (sortedValues.Count == 0) return okeyCount >= 3;

        // Olası başlangıç noktalarını dene
        int minStart = Math.Max(1, sortedValues[0] - okeyCount);
        int maxStart = Math.Min(13 - totalLength + 1, sortedValues[0]);

        // Özel durum: 12-13-1 wrap-around
        bool canWrap = totalLength <= 3 && sortedValues.Contains(1) && 
                       (sortedValues.Contains(13) || sortedValues.Contains(12));

        for (int start = minStart; start <= maxStart; start++)
        {
            if (CanFitSequenceAt(sortedValues, okeyCount, start, totalLength))
            {
                return true;
            }
        }

        // Wrap-around kontrolü (12-13-1)
        if (canWrap)
        {
            return CanFitWrapAroundSequence(sortedValues, okeyCount, totalLength);
        }

        return false;
    }

    private static bool CanFitSequenceAt(List<int> values, int okeyCount, int start, int length)
    {
        int okeysNeeded = 0;
        var valueSet = new HashSet<int>(values);

        for (int i = 0; i < length; i++)
        {
            int expectedValue = start + i;
            if (expectedValue > 13) return false;

            if (!valueSet.Contains(expectedValue))
            {
                okeysNeeded++;
                if (okeysNeeded > okeyCount) return false;
            }
        }

        return true;
    }

    private static bool CanFitWrapAroundSequence(List<int> values, int okeyCount, int length)
    {
        // 12-13-1 veya 13-1 + Okey
        var valueSet = new HashSet<int>(values);
        int okeysNeeded = 0;

        int[] wrapSequence = length == 3 ? new[] { 12, 13, 1 } : new[] { 13, 1 };

        foreach (var v in wrapSequence.Take(length))
        {
            if (!valueSet.Contains(v))
            {
                okeysNeeded++;
            }
        }

        return okeysNeeded <= okeyCount;
    }

    /// <summary>
    /// Düz per kontrolü (farklı renkler, aynı sayı).
    /// </summary>
    private static bool IsValidGroup(List<Tile> tiles)
    {
        if (tiles.Count < 3 || tiles.Count > 4) return false;

        // Normal taşları ve Okey'leri ayır
        var normalTiles = tiles.Where(t => !t.IsOkey && !t.IsFalseJoker).ToList();
        int okeyCount = tiles.Count - normalTiles.Count;

        if (normalTiles.Count == 0)
        {
            // Tamamı Okey
            return okeyCount >= 3 && okeyCount <= 4;
        }

        // Tüm normal taşlar aynı değerde olmalı
        var value = normalTiles[0].Value;
        if (normalTiles.Any(t => t.Value != value))
        {
            return false;
        }

        // Renkler farklı olmalı
        var colors = normalTiles.Select(t => t.Color).ToHashSet();
        if (colors.Count != normalTiles.Count)
        {
            return false;
        }

        // En fazla 4 farklı renk olabilir
        return colors.Count + okeyCount <= 4;
    }

    #endregion

    #region Scoring

    private int CalculatePointValue()
    {
        if (!IsValid) return 0;

        return Tiles.Sum(t =>
        {
            if (t.IsOkey || t.IsFalseJoker) return 0; // Okey değeri sonradan hesaplanır
            return t.Value;
        });
    }

    #endregion

    #region Equality

    public bool Equals(Meld? other)
    {
        if (other == null) return false;
        if (Tiles.Count != other.Tiles.Count) return false;

        var thisTileIds = Tiles.Select(t => t.Id).OrderBy(id => id).ToList();
        var otherTileIds = other.Tiles.Select(t => t.Id).OrderBy(id => id).ToList();

        return thisTileIds.SequenceEqual(otherTileIds);
    }

    public override bool Equals(object? obj) => Equals(obj as Meld);

    public override int GetHashCode()
    {
        return Tiles.Aggregate(0, (hash, tile) => hash ^ tile.Id.GetHashCode());
    }

    #endregion

    #region Display

    public override string ToString()
    {
        var tilesStr = string.Join(", ", Tiles.Select(t => t.ToString()));
        return $"{Type}: [{tilesStr}]";
    }

    #endregion
}

/// <summary>
/// Per türleri.
/// </summary>
public enum MeldType
{
    /// <summary>Geçersiz kombinasyon.</summary>
    Invalid = 0,

    /// <summary>Sıralı per (aynı renk, ardışık sayılar).</summary>
    Run = 1,

    /// <summary>Düz per (farklı renkler, aynı sayı).</summary>
    Group = 2
}
