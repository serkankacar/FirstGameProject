namespace OkeyGame.Domain.Enums;

/// <summary>
/// Oyun bitiş türleri.
/// </summary>
public enum WinType
{
    /// <summary>Henüz bitiş yok.</summary>
    None = 0,

    /// <summary>Normal bitiş (perlerle).</summary>
    Normal = 1,

    /// <summary>Çifte bitiş (7 çift).</summary>
    Pairs = 2,

    /// <summary>Okey atarak bitiş (en yüksek puan).</summary>
    OkeyDiscard = 3,

    /// <summary>Elle bitmeden önce deste bitti.</summary>
    DeckEmpty = 4
}
