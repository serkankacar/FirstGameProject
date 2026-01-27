using OkeyGame.Domain.Entities;
using OkeyGame.Domain.Enums;

namespace OkeyGame.Domain.Services;

/// <summary>
/// Okey taş setini oluşturan fabrika sınıfı.
/// 
/// OKEY TAŞLARI KURALI:
/// - 4 renk x 13 sayı x 2 kopya = 104 normal taş
/// - 2 adet Sahte Okey (False Joker) = 2 taş
/// - TOPLAM: 106 taş
/// </summary>
public static class TileFactory
{
    #region Sabitler

    /// <summary>Toplam taş sayısı</summary>
    public const int TotalTileCount = 106;

    /// <summary>Normal taş sayısı (sahte okeyler hariç)</summary>
    public const int NormalTileCount = 104;

    /// <summary>Sahte Okey sayısı</summary>
    public const int FalseJokerCount = 2;

    /// <summary>Renk sayısı</summary>
    public const int ColorCount = 4;

    /// <summary>Her renkteki maksimum değer</summary>
    public const int MaxValue = 13;

    /// <summary>Her renkten kaç kopya var</summary>
    public const int CopyCount = 2;

    #endregion

    #region Factory Metotları

    /// <summary>
    /// Tam bir Okey taş seti oluşturur (106 taş).
    /// Her taşa benzersiz bir ID atanır.
    /// </summary>
    /// <returns>106 taşlık liste</returns>
    public static List<Tile> CreateFullSet()
    {
        var tiles = new List<Tile>(TotalTileCount);
        int currentId = 0;

        // 4 renk için döngü
        foreach (TileColor color in Enum.GetValues<TileColor>())
        {
            // Her renkten 2 kopya
            for (int copy = 0; copy < CopyCount; copy++)
            {
                // 1'den 13'e kadar değerler
                for (int value = 1; value <= MaxValue; value++)
                {
                    tiles.Add(Tile.Create(currentId++, color, value));
                }
            }
        }

        // 2 adet Sahte Okey ekle
        for (int i = 0; i < FalseJokerCount; i++)
        {
            tiles.Add(Tile.CreateFalseJoker(currentId++));
        }

        // Taş sayısı doğrulaması
        if (tiles.Count != TotalTileCount)
        {
            throw new InvalidOperationException(
                $"Taş seti oluşturma hatası! Beklenen: {TotalTileCount}, Oluşturulan: {tiles.Count}");
        }

        return tiles;
    }

    /// <summary>
    /// Belirtilen renk ve değerdeki tüm taşları bulur.
    /// </summary>
    /// <param name="tiles">Taş listesi</param>
    /// <param name="color">Aranan renk</param>
    /// <param name="value">Aranan değer</param>
    /// <returns>Eşleşen taşlar</returns>
    public static IEnumerable<Tile> FindTiles(IEnumerable<Tile> tiles, TileColor color, int value)
    {
        return tiles.Where(t => t.Color == color && t.Value == value && !t.IsFalseJoker);
    }

    /// <summary>
    /// Tüm Sahte Okeyleri bulur.
    /// </summary>
    /// <param name="tiles">Taş listesi</param>
    /// <returns>Sahte Okeyler</returns>
    public static IEnumerable<Tile> FindFalseJokers(IEnumerable<Tile> tiles)
    {
        return tiles.Where(t => t.IsFalseJoker);
    }

    /// <summary>
    /// Gösterge taşına göre Okey (Joker) taşlarını belirler ve işaretler.
    /// 
    /// OKEY BELİRLEME KURALI:
    /// - Gösterge taşının bir üstündeki değer Okey olur
    /// - Gösterge 13 ise, Okey 1 olur (döngüsel)
    /// - Aynı renkteki 2 taş Okey olarak işaretlenir
    /// </summary>
    /// <param name="tiles">Taş listesi</param>
    /// <param name="indicatorTile">Gösterge taşı</param>
    /// <returns>Okey işaretli yeni taş listesi</returns>
    public static List<Tile> MarkOkeyTiles(List<Tile> tiles, Tile indicatorTile)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(indicatorTile);

        if (indicatorTile.IsFalseJoker)
        {
            throw new ArgumentException(
                "Gösterge taşı Sahte Okey olamaz.", 
                nameof(indicatorTile));
        }

        // Okey değerini hesapla (göstergenin bir üstü, 13->1 döngüsel)
        int okeyValue = indicatorTile.Value == MaxValue ? 1 : indicatorTile.Value + 1;
        TileColor okeyColor = indicatorTile.Color;

        // Yeni liste oluştur (immutability için)
        var result = new List<Tile>(tiles.Count);

        foreach (var tile in tiles)
        {
            if (!tile.IsFalseJoker && 
                tile.Color == okeyColor && 
                tile.Value == okeyValue)
            {
                // Bu taş Okey, işaretle
                result.Add(tile.AsOkey());
            }
            else
            {
                result.Add(tile);
            }
        }

        return result;
    }

    /// <summary>
    /// Taş setinin geçerliliğini doğrular.
    /// </summary>
    /// <param name="tiles">Kontrol edilecek taş listesi</param>
    /// <returns>Doğrulama sonucu</returns>
    public static (bool IsValid, string? ErrorMessage) ValidateSet(List<Tile> tiles)
    {
        if (tiles == null)
        {
            return (false, "Taş listesi null olamaz.");
        }

        if (tiles.Count != TotalTileCount)
        {
            return (false, $"Taş sayısı {TotalTileCount} olmalıdır. Mevcut: {tiles.Count}");
        }

        // Benzersiz ID kontrolü
        var uniqueIds = tiles.Select(t => t.Id).Distinct().Count();
        if (uniqueIds != TotalTileCount)
        {
            return (false, "Her taşın benzersiz bir ID'si olmalıdır.");
        }

        // Normal taş sayısı kontrolü
        var normalTileCount = tiles.Count(t => !t.IsFalseJoker);
        if (normalTileCount != NormalTileCount)
        {
            return (false, $"Normal taş sayısı {NormalTileCount} olmalıdır. Mevcut: {normalTileCount}");
        }

        // Sahte Okey sayısı kontrolü
        var falseJokerCount = tiles.Count(t => t.IsFalseJoker);
        if (falseJokerCount != FalseJokerCount)
        {
            return (false, $"Sahte Okey sayısı {FalseJokerCount} olmalıdır. Mevcut: {falseJokerCount}");
        }

        // Her renk ve değer için 2 taş olmalı
        foreach (TileColor color in Enum.GetValues<TileColor>())
        {
            for (int value = 1; value <= MaxValue; value++)
            {
                var count = tiles.Count(t => 
                    !t.IsFalseJoker && t.Color == color && t.Value == value);
                
                if (count != CopyCount)
                {
                    return (false, 
                        $"{color} rengi {value} değerinden {CopyCount} adet olmalı. Mevcut: {count}");
                }
            }
        }

        return (true, null);
    }

    #endregion
}
