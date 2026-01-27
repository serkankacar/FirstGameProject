namespace OkeyGame.Domain.Enums;

/// <summary>
/// Oyunun mevcut durumunu temsil eder.
/// Oyun akışını yönetmek için kullanılır.
/// </summary>
public enum GameState
{
    /// <summary>Oyuncular bekleniyor</summary>
    WaitingForPlayers = 0,
    
    /// <summary>Taşlar karıştırılıyor</summary>
    Shuffling = 1,
    
    /// <summary>Taşlar dağıtılıyor</summary>
    Dealing = 2,
    
    /// <summary>Oyun devam ediyor</summary>
    InProgress = 3,
    
    /// <summary>Oyun bitti</summary>
    Finished = 4,
    
    /// <summary>Oyun iptal edildi</summary>
    Cancelled = 5
}
