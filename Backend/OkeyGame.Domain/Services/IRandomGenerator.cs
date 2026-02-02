namespace OkeyGame.Domain.Services;

/// <summary>
/// Rastgele sayı üreteci arayüzü.
/// Farklı RNG implementasyonlarının (Crypto, Deterministic, Mock) kullanılmasını sağlar.
/// </summary>
public interface IRandomGenerator
{
    /// <summary>
    /// 0 ile maxValue (hariç) arasında rastgele tam sayı üretir.
    /// </summary>
    /// <param name="maxValue">Üst sınır (dahil değil)</param>
    /// <returns>Rastgele tam sayı [0, maxValue)</returns>
    int NextInt(int maxValue);
}
