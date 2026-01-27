namespace OkeyGame.Domain.AI;

/// <summary>
/// Bot zorluk seviyeleri.
/// </summary>
public enum BotDifficulty
{
    /// <summary>Kolay - Rastgele hamleler, az hafıza.</summary>
    Easy = 1,

    /// <summary>Normal - Temel strateji, orta hafıza.</summary>
    Normal = 2,

    /// <summary>Zor - Gelişmiş strateji, tam hafıza.</summary>
    Hard = 3,

    /// <summary>Uzman - En iyi strateji, mükemmel hafıza.</summary>
    Expert = 4
}
