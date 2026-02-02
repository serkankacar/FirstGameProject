namespace OkeyGame.Domain.Services;

/// <summary>
/// Fisher-Yates (Knuth) Shuffle algoritması implementasyonu.
/// 
/// NEDEN FISHER-YATES?
/// - O(n) zaman karmaşıklığı (en verimli)
/// - Uniform dağılım garantisi (her permütasyon eşit olasılıklı)
/// - In-place algoritma (ekstra bellek gerektirmez)
/// - Kriptografik RNG ile birlikte kullanıldığında tamamen öngörülemez
/// 
/// ALGORİTMA:
/// 1. Listenin sonundan başla (i = n-1)
/// 2. [0, i] aralığında rastgele bir indeks seç (j)
/// 3. i ve j indekslerindeki elemanları değiştir
/// 4. i'yi bir azalt ve tekrarla
/// </summary>
public static class FisherYatesShuffle
{
    #region Shuffle Metotları

    /// <summary>
    /// Listeyi Fisher-Yates algoritması ile güvenli bir şekilde karıştırır.
    /// Kriptografik olarak güvenli RNG kullanır.
    /// </summary>
    /// <typeparam name="T">Liste eleman tipi</typeparam>
    /// <param name="list">Karıştırılacak liste</param>
    public static void Shuffle<T>(IList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (list.Count <= 1)
        {
            return; // Karıştırılacak bir şey yok
        }

        var rng = CryptoRandomGenerator.Instance;
        ShuffleWithRng(list, rng);
    }

    /// <summary>
    /// Listeyi belirtilen RNG ile karıştırır.
    /// Test edilebilirlik için ayrı RNG kabul eder.
    /// </summary>
    /// <typeparam name="T">Liste eleman tipi</typeparam>
    /// <param name="list">Karıştırılacak liste</param>
    /// <param name="rng">Rastgele sayı üreteci</param>
    public static void ShuffleWithRng<T>(IList<T> list, IRandomGenerator rng)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(rng);

        if (list.Count <= 1)
        {
            return;
        }

        // Fisher-Yates algoritması
        // Sondan başa doğru ilerle
        for (int i = list.Count - 1; i > 0; i--)
        {
            // [0, i] aralığında rastgele indeks seç
            int j = rng.NextInt(i + 1);

            // Elemanları değiştir (swap)
            if (i != j)
            {
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>
    /// Orijinal listeyi değiştirmeden karıştırılmış yeni bir liste döndürür.
    /// Immutable koleksiyonlar için kullanışlıdır.
    /// </summary>
    /// <typeparam name="T">Liste eleman tipi</typeparam>
    /// <param name="source">Kaynak liste</param>
    /// <returns>Karıştırılmış yeni liste</returns>
    public static List<T> ShuffleToNew<T>(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var list = source.ToList();
        Shuffle(list);
        return list;
    }

    /// <summary>
    /// Orijinal listeyi değiştirmeden, belirtilen RNG ile karıştırılmış yeni liste döndürür.
    /// </summary>
    /// <typeparam name="T">Liste eleman tipi</typeparam>
    /// <param name="source">Kaynak liste</param>
    /// <param name="rng">Rastgele sayı üreteci</param>
    /// <returns>Karıştırılmış yeni liste</returns>
    public static List<T> ShuffleToNewWithRng<T>(IEnumerable<T> source, IRandomGenerator rng)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rng);

        var list = source.ToList();
        ShuffleWithRng(list, rng);
        return list;
    }

    #endregion

    #region Yardımcı Metotlar

    /// <summary>
    /// Karıştırma işleminin kalitesini test eder.
    /// Geliştirme/Debug amaçlı kullanılır.
    /// </summary>
    /// <param name="iterations">Test iterasyon sayısı</param>
    /// <param name="listSize">Test listesi boyutu</param>
    /// <returns>Chi-square test sonucu ve dağılım istatistikleri</returns>
    public static ShuffleQualityResult TestShuffleQuality(int iterations = 10000, int listSize = 10)
    {
        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "İterasyon sayısı pozitif olmalıdır.");
        }

        if (listSize <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(listSize), "Liste boyutu 2 veya daha büyük olmalıdır.");
        }

        // Her pozisyon için her elemanın kaç kez göründüğünü say
        var positionCounts = new int[listSize, listSize];

        for (int iter = 0; iter < iterations; iter++)
        {
            // 0'dan listSize-1'e kadar sayıları içeren liste oluştur
            var list = Enumerable.Range(0, listSize).ToList();
            Shuffle(list);

            // Her pozisyonda hangi eleman var, say
            for (int pos = 0; pos < listSize; pos++)
            {
                int element = list[pos];
                positionCounts[pos, element]++;
            }
        }

        // Beklenen değer: her pozisyonda her eleman eşit sıklıkta olmalı
        double expected = (double)iterations / listSize;
        double chiSquare = 0;

        for (int pos = 0; pos < listSize; pos++)
        {
            for (int elem = 0; elem < listSize; elem++)
            {
                double observed = positionCounts[pos, elem];
                double diff = observed - expected;
                chiSquare += (diff * diff) / expected;
            }
        }

        // Serbestlik derecesi: (listSize - 1)^2
        int degreesOfFreedom = (listSize - 1) * (listSize - 1);

        return new ShuffleQualityResult
        {
            ChiSquareValue = chiSquare,
            DegreesOfFreedom = degreesOfFreedom,
            Iterations = iterations,
            ListSize = listSize,
            ExpectedCountPerCell = expected,
            // Chi-square kritik değer (p=0.05) için kabaca kontrol
            IsUniform = chiSquare < (degreesOfFreedom * 2) // Basitleştirilmiş kontrol
        };
    }

    #endregion
}

/// <summary>
/// Karıştırma kalite testi sonucu.
/// </summary>
public class ShuffleQualityResult
{
    /// <summary>Chi-square test değeri</summary>
    public double ChiSquareValue { get; init; }
    
    /// <summary>Serbestlik derecesi</summary>
    public int DegreesOfFreedom { get; init; }
    
    /// <summary>Test iterasyon sayısı</summary>
    public int Iterations { get; init; }
    
    /// <summary>Test listesi boyutu</summary>
    public int ListSize { get; init; }
    
    /// <summary>Her hücredeki beklenen sayı</summary>
    public double ExpectedCountPerCell { get; init; }
    
    /// <summary>Dağılım uniform mi?</summary>
    public bool IsUniform { get; init; }

    public override string ToString()
    {
        return $"Chi-Square: {ChiSquareValue:F2}, " +
               $"DF: {DegreesOfFreedom}, " +
               $"Uniform: {IsUniform}";
    }
}
