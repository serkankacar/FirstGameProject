namespace OkeyGame.Domain.Enums;

/// <summary>
/// Oyuncunun masa üzerindeki pozisyonunu temsil eder.
/// Okey'de 4 oyuncu bulunur ve saat yönünde sıralanır.
/// </summary>
public enum PlayerPosition
{
    /// <summary>Güney pozisyonu (Alt)</summary>
    South = 0,
    
    /// <summary>Doğu pozisyonu (Sağ)</summary>
    East = 1,
    
    /// <summary>Kuzey pozisyonu (Üst)</summary>
    North = 2,
    
    /// <summary>Batı pozisyonu (Sol)</summary>
    West = 3
}
